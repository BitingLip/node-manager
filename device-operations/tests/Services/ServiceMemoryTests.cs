using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using DeviceOperations.Services.Memory;
using DeviceOperations.Services.Python;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using System.Text.Json;

namespace DeviceOperations.Tests.Services;

/// <summary>
/// Unit tests for ServiceMemory
/// Tests memory management operations including allocation, monitoring, optimization, and cleanup
/// </summary>
public class ServiceMemoryTests
{
    private readonly Mock<IPythonWorkerService> _mockPythonWorkerService;
    private readonly Mock<ILogger<ServiceMemory>> _mockLogger;
    private readonly ServiceMemory _serviceMemory;

    public ServiceMemoryTests()
    {
        _mockPythonWorkerService = new Mock<IPythonWorkerService>();
        _mockLogger = new Mock<ILogger<ServiceMemory>>();
        _serviceMemory = new ServiceMemory(_mockPythonWorkerService.Object, _mockLogger.Object);
    }

    #region Memory Status Tests

    [Fact]
    public async Task GetMemoryStatusAsync_ShouldReturnMemoryStatus_WhenSuccessful()
    {
        // Arrange
        var expectedResponse = new GetMemoryStatusResponse
        {
            TotalSystemMemory = 34359738368, // 32GB
            AvailableSystemMemory = 16106127360, // 15GB
            UsedSystemMemory = 18253611008, // 17GB
            DeviceMemories = new List<DeviceMemoryStatus>
            {
                new DeviceMemoryStatus
                {
                    DeviceId = Guid.NewGuid(),
                    DeviceName = "NVIDIA RTX 4090",
                    TotalMemory = 25769803776, // 24GB
                    UsedMemory = 8589934592, // 8GB
                    AvailableMemory = 17179869184, // 16GB
                    MemoryUtilization = 33.33f
                }
            },
            OverallMemoryUtilization = 53.12f,
            MemoryPressure = "Normal",
            RecommendedActions = new List<string>()
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.GetMemoryStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.TotalSystemMemory.Should().Be(34359738368);
        result.Data.DeviceMemories.Should().HaveCount(1);
        result.Data.MemoryPressure.Should().Be("Normal");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("get_memory_status", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task GetMemoryStatusAsync_ShouldReturnError_WhenPythonWorkerFails()
    {
        // Arrange
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new Exception("Python worker communication failed"));

        // Act
        var result = await _serviceMemory.GetMemoryStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("Failed to retrieve memory status");
    }

    [Fact]
    public async Task GetMemoryStatusDeviceAsync_ShouldReturnDeviceMemoryStatus_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetMemoryStatusDeviceResponse
        {
            DeviceId = Guid.NewGuid(),
            DeviceName = "NVIDIA RTX 4090",
            TotalMemory = 25769803776, // 24GB
            UsedMemory = 8589934592, // 8GB
            AvailableMemory = 17179869184, // 16GB
            MemoryUtilization = 33.33f,
            MemoryBandwidth = 1008.0f, // GB/s
            MemoryTemperature = 65.5f,
            AllocationDetails = new List<MemoryAllocation>
            {
                new MemoryAllocation
                {
                    Id = Guid.NewGuid(),
                    Size = 4294967296, // 4GB
                    Purpose = "Model Storage",
                    AllocationTime = DateTime.UtcNow.AddMinutes(-30),
                    IsActive = true
                }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.GetMemoryStatusDeviceAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.DeviceName.Should().Be("NVIDIA RTX 4090");
        result.Data.MemoryUtilization.Should().Be(33.33f);
        result.Data.AllocationDetails.Should().HaveCount(1);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("get_memory_status_device", 
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(deviceId))), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetMemoryStatusDeviceAsync_ShouldThrowArgumentException_WhenInvalidDeviceId(string invalidDeviceId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _serviceMemory.GetMemoryStatusDeviceAsync(invalidDeviceId));
    }

    #endregion

    #region Memory Allocation Tests

    [Fact]
    public async Task PostMemoryAllocateAsync_ShouldAllocateMemory_WhenRequestValid()
    {
        // Arrange
        var request = new PostMemoryAllocateRequest
        {
            Size = 4294967296, // 4GB
            DeviceId = "test-device-id",
            Purpose = "Model Loading",
            Priority = "High",
            Alignment = 256,
            AllowSwap = false
        };

        var expectedResponse = new PostMemoryAllocateResponse
        {
            AllocationId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            AllocatedSize = request.Size,
            ActualSize = request.Size,
            MemoryAddress = "0x7F8B40000000",
            Purpose = request.Purpose,
            AllocationTime = DateTime.UtcNow,
            Status = "Allocated",
            FragmentationLevel = 15.2f,
            AllocationTime_Microseconds = 1250.5
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryAllocateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.AllocatedSize.Should().Be(4294967296);
        result.Data.Status.Should().Be("Allocated");
        result.Data.Purpose.Should().Be("Model Loading");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("allocate_memory",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains("Model Loading") && 
                              JsonSerializer.Serialize(o).Contains("4294967296"))), Times.Once);
    }

    [Fact]
    public async Task PostMemoryAllocateAsync_ShouldThrowArgumentNullException_WhenRequestIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _serviceMemory.PostMemoryAllocateAsync(null!));
    }

    [Fact]
    public async Task DeleteMemoryDeallocateAsync_ShouldDeallocateMemory_WhenRequestValid()
    {
        // Arrange
        var request = new DeleteMemoryDeallocateRequest
        {
            AllocationId = Guid.NewGuid(),
            DeviceId = "test-device-id",
            Force = false,
            ValidateBeforeDeallocation = true
        };

        var expectedResponse = new DeleteMemoryDeallocateResponse
        {
            AllocationId = request.AllocationId,
            DeviceId = Guid.NewGuid(),
            DeallocatedSize = 4294967296, // 4GB
            Status = "Deallocated",
            DeallocationTime = DateTime.UtcNow,
            FragmentationImprovement = 8.5f,
            DeallocationTime_Microseconds = 450.2,
            CleanupOperations = new List<string>
            {
                "Memory cleared",
                "Cache invalidated",
                "Reference count decremented"
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.DeleteMemoryDeallocateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Deallocated");
        result.Data.DeallocatedSize.Should().Be(4294967296);
        result.Data.CleanupOperations.Should().HaveCount(3);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("deallocate_memory",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(request.AllocationId.ToString()))), Times.Once);
    }

    #endregion

    #region Memory Transfer Tests

    [Fact]
    public async Task PostMemoryTransferAsync_ShouldTransferMemory_WhenRequestValid()
    {
        // Arrange
        var request = new PostMemoryTransferRequest
        {
            SourceDeviceId = "source-device-id",
            TargetDeviceId = "target-device-id",
            Size = 2147483648, // 2GB
            TransferType = "HostToDevice",
            Priority = "High",
            UseAsyncTransfer = true
        };

        var expectedResponse = new PostMemoryTransferResponse
        {
            TransferId = Guid.NewGuid(),
            SourceDeviceId = Guid.NewGuid(),
            TargetDeviceId = Guid.NewGuid(),
            TransferredSize = request.Size,
            TransferType = request.TransferType,
            Status = "Completed",
            TransferTime = TimeSpan.FromSeconds(2.5),
            TransferRate = 858993459.2f, // bytes/sec
            CompletedAt = DateTime.UtcNow,
            ErrorCount = 0,
            RetryCount = 0
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryTransferAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Completed");
        result.Data.TransferType.Should().Be("HostToDevice");
        result.Data.TransferredSize.Should().Be(2147483648);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("transfer_memory",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains("HostToDevice") && 
                              JsonSerializer.Serialize(o).Contains("2147483648"))), Times.Once);
    }

    [Fact]
    public async Task PostMemoryCopyAsync_ShouldCopyMemory_WhenRequestValid()
    {
        // Arrange
        var request = new PostMemoryCopyRequest
        {
            SourceAllocationId = Guid.NewGuid(),
            TargetDeviceId = "target-device-id",
            Size = 1073741824, // 1GB
            CopyType = "DeviceToDevice",
            ValidateAfterCopy = true
        };

        var expectedResponse = new PostMemoryCopyResponse
        {
            CopyId = Guid.NewGuid(),
            SourceAllocationId = request.SourceAllocationId,
            TargetAllocationId = Guid.NewGuid(),
            CopiedSize = request.Size,
            CopyType = request.CopyType,
            Status = "Completed",
            CopyTime = TimeSpan.FromSeconds(1.2),
            ValidationResult = "Passed",
            CompletedAt = DateTime.UtcNow,
            ErrorCount = 0
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryCopyAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Completed");
        result.Data.ValidationResult.Should().Be("Passed");
        result.Data.CopiedSize.Should().Be(1073741824);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("copy_memory",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(request.SourceAllocationId.ToString()))), Times.Once);
    }

    #endregion

    #region Memory Optimization Tests

    [Fact]
    public async Task PostMemoryClearAsync_ShouldClearMemory_WhenRequestValid()
    {
        // Arrange
        var request = new PostMemoryClearRequest
        {
            DeviceId = "test-device-id",
            ClearType = "UnusedAllocations",
            Force = false,
            PreserveCriticalData = true
        };

        var expectedResponse = new PostMemoryClearResponse
        {
            DeviceId = Guid.NewGuid(),
            ClearType = request.ClearType,
            Status = "Completed",
            ClearedSize = 2147483648, // 2GB
            FragmentationImprovement = 25.5f,
            ClearTime = TimeSpan.FromSeconds(3.8),
            CompletedAt = DateTime.UtcNow,
            ClearedAllocations = new List<Guid>
            {
                Guid.NewGuid(),
                Guid.NewGuid()
            },
            RemainingAllocations = 3
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryClearAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Completed");
        result.Data.ClearedSize.Should().Be(2147483648);
        result.Data.ClearedAllocations.Should().HaveCount(2);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("clear_memory",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains("UnusedAllocations"))), Times.Once);
    }

    [Fact]
    public async Task PostMemoryOptimizeAsync_ShouldOptimizeMemory_WhenRequestValid()
    {
        // Arrange
        var request = new PostMemoryOptimizeRequest
        {
            DeviceId = "test-device-id",
            OptimizationType = "Defragment",
            AggressiveOptimization = false,
            TargetFragmentationLevel = 10.0f
        };

        var expectedResponse = new PostMemoryOptimizeResponse
        {
            DeviceId = Guid.NewGuid(),
            OptimizationType = request.OptimizationType,
            Status = "Completed",
            OptimizationTime = TimeSpan.FromSeconds(8.5),
            FragmentationBefore = 35.2f,
            FragmentationAfter = 8.7f,
            FragmentationImprovement = 26.5f,
            MemoryReclaimed = 536870912, // 512MB
            OptimizationsApplied = new List<string>
            {
                "Memory defragmentation",
                "Allocation consolidation",
                "Cache optimization"
            },
            PerformanceImprovement = 15.3f
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryOptimizeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Completed");
        result.Data.FragmentationImprovement.Should().Be(26.5f);
        result.Data.OptimizationsApplied.Should().HaveCount(3);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("optimize_memory",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains("Defragment"))), Times.Once);
    }

    [Fact]
    public async Task PostMemoryDefragmentAsync_ShouldDefragmentMemory_WhenRequestValid()
    {
        // Arrange
        var request = new PostMemoryDefragmentRequest
        {
            DeviceId = "test-device-id",
            DefragmentationType = "Full",
            AllowLiveDefragmentation = true,
            MaxDefragmentationTime = TimeSpan.FromMinutes(10)
        };

        var expectedResponse = new PostMemoryDefragmentResponse
        {
            DeviceId = Guid.NewGuid(),
            DefragmentationType = request.DefragmentationType,
            Status = "Completed",
            DefragmentationTime = TimeSpan.FromSeconds(12.3),
            FragmentationBefore = 42.8f,
            FragmentationAfter = 5.2f,
            FragmentationReduction = 37.6f,
            BlocksMoved = 156,
            LargestFreeBlock = 8589934592, // 8GB
            MemoryEfficiencyImprovement = 28.5f
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryDefragmentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Completed");
        result.Data.FragmentationReduction.Should().Be(37.6f);
        result.Data.BlocksMoved.Should().Be(156);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("defragment_memory",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains("Full"))), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PostMemoryAllocateAsync_ShouldLogError_WhenExceptionOccurs()
    {
        // Arrange
        var request = new PostMemoryAllocateRequest
        {
            Size = 4294967296,
            DeviceId = "test-device-id",
            Purpose = "Model Loading"
        };

        var exception = new Exception("Test exception");
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _serviceMemory.PostMemoryAllocateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error allocating memory")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMemoryStatusAsync_ShouldHandleInvalidResponse_Gracefully()
    {
        // Arrange
        // Return invalid JSON response
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync("invalid json response");

        // Act
        var result = await _serviceMemory.GetMemoryStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    #endregion

    #region Resource Cleanup Tests

    [Fact]
    public void Dispose_ShouldNotThrow_WhenCalled()
    {
        // Act & Assert
        var exception = Record.Exception(() => _serviceMemory.Dispose());
        exception.Should().BeNull();
    }

    #endregion
}
