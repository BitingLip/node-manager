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
using RequestsMemoryAllocationType = DeviceOperations.Models.Requests.MemoryAllocationType;
using CommonOptimizationTarget = DeviceOperations.Models.Common.OptimizationTarget;

namespace DeviceOperations.Tests.Services;

/// <summary>
/// Unit tests for ServiceMemory
/// Tests memory management operations including allocation, monitoring, optimization, and cleanup
/// </summary>
public class ServiceMemoryTestsFixed
{
    private readonly Mock<IPythonWorkerService> _mockPythonWorkerService;
    private readonly Mock<ILogger<ServiceMemory>> _mockLogger;
    private readonly ServiceMemory _serviceMemory;

    public ServiceMemoryTestsFixed()
    {
        _mockPythonWorkerService = new Mock<IPythonWorkerService>();
        _mockLogger = new Mock<ILogger<ServiceMemory>>();
        _serviceMemory = new ServiceMemory(_mockLogger.Object, _mockPythonWorkerService.Object);
    }

    #region Memory Status Tests

    [Fact]
    public async Task GetMemoryStatusAsync_ShouldReturnMemoryStatus_WhenSuccessful()
    {
        // Arrange
        var expectedResponse = new GetMemoryStatusResponse
        {
            MemoryStatus = new Dictionary<string, object>
            {
                ["TotalSystemMemory"] = 34359738368L, // 32GB
                ["AvailableSystemMemory"] = 16106127360L, // 15GB
                ["UsedSystemMemory"] = 18253611008L, // 17GB
                ["OverallMemoryUtilization"] = 53.12f,
                ["MemoryPressure"] = "Normal"
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.GetMemoryStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.MemoryStatus.Should().ContainKey("TotalSystemMemory");
        result.Data.MemoryStatus.Should().ContainKey("MemoryPressure");

        _mockPythonWorkerService.Verify(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMemoryStatusAsync_ShouldReturnError_WhenPythonWorkerFails()
    {
        // Arrange
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Python worker communication failed"));

        // Act
        var result = await _serviceMemory.GetMemoryStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMemoryStatusDeviceAsync_ShouldReturnDeviceMemoryStatus_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetMemoryStatusDeviceResponse
        {
            MemoryStatus = new Dictionary<string, object>
            {
                ["DeviceId"] = deviceId,
                ["DeviceName"] = "NVIDIA RTX 4090",
                ["TotalMemory"] = 25769803776L, // 24GB
                ["UsedMemory"] = 8589934592L, // 8GB
                ["AvailableMemory"] = 17179869184L, // 16GB
                ["MemoryUtilization"] = 33.33f
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.GetMemoryStatusDeviceAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.MemoryStatus.Should().ContainKey("DeviceName");

        _mockPythonWorkerService.Verify(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.Is<object>(o => JsonSerializer.Serialize(o, (JsonSerializerOptions?)null).Contains(deviceId)), It.IsAny<CancellationToken>()), Times.Once);
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
            SizeBytes = 4294967296, // 4GB
            MemoryType = "GPU"
        };

        var expectedResponse = new PostMemoryAllocateResponse
        {
            AllocationId = Guid.NewGuid().ToString(),
            Success = true
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryAllocateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AllocationId.Should().NotBeNullOrEmpty();
        result.Data.Success.Should().BeTrue();

        _mockPythonWorkerService.Verify(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.Is<object>(o => JsonSerializer.Serialize(o, (JsonSerializerOptions?)null).Contains("Model Loading")), It.IsAny<CancellationToken>()), Times.Once);
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
            AllocationId = Guid.NewGuid().ToString(),
            Force = false
        };

        var expectedResponse = new DeleteMemoryDeallocateResponse
        {
            Success = true,
            Message = "Memory deallocated successfully"
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.DeleteMemoryDeallocateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Success.Should().BeTrue();
        result.Data.Message.Should().Contain("successfully");

        _mockPythonWorkerService.Verify(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.Is<object>(o => JsonSerializer.Serialize(o, (JsonSerializerOptions?)null).Contains(request.AllocationId)), It.IsAny<CancellationToken>()), Times.Once);
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
            SizeBytes = 2147483648 // 2GB
        };

        var expectedResponse = new PostMemoryTransferResponse
        {
            TransferId = Guid.NewGuid().ToString(),
            Success = true
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryTransferAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Success.Should().BeTrue();
        result.Data.TransferId.Should().NotBeNullOrEmpty();

        _mockPythonWorkerService.Verify(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.Is<object>(o => JsonSerializer.Serialize(o, (JsonSerializerOptions?)null).Contains("source-device-id")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostMemoryCopyAsync_ShouldCopyMemory_WhenRequestValid()
    {
        // Arrange
        var request = new PostMemoryCopyRequest
        {
            SourceId = Guid.NewGuid().ToString(),
            TargetId = Guid.NewGuid().ToString()
        };

        var expectedResponse = new PostMemoryCopyResponse
        {
            Success = true,
            Message = "Memory copied successfully"
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryCopyAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Success.Should().BeTrue();

        _mockPythonWorkerService.Verify(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.Is<object>(o => JsonSerializer.Serialize(o, (JsonSerializerOptions?)null).Contains(request.SourceId)), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Memory Optimization Tests

    [Fact]
    public async Task PostMemoryClearAsync_ShouldClearMemory_WhenRequestValid()
    {
        // Arrange
        var request = new PostMemoryClearRequest
        {
            MemoryType = "all",
            Force = false
        };

        var expectedResponse = new PostMemoryClearResponse
        {
            Success = true,
            ClearedBytes = 2147483648
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryClearAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Success.Should().BeTrue();
        result.Data.ClearedBytes.Should().Be(2147483648);

        _mockPythonWorkerService.Verify(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostMemoryOptimizeAsync_ShouldOptimizeMemory_WhenRequestValid()
    {
        // Arrange
        var request = new PostMemoryOptimizeRequest
        {
            Target = DeviceOperations.Models.Requests.OptimizationTarget.MemoryUsage
        };

        var expectedResponse = new PostMemoryOptimizeResponse
        {
            Success = true,
            Message = "Memory optimization completed successfully"
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryOptimizeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Success.Should().BeTrue();
        result.Data.Message.Should().Contain("optimization");

        _mockPythonWorkerService.Verify(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.Is<object>(o => JsonSerializer.Serialize(o, (JsonSerializerOptions?)null).Contains("Defragment")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostMemoryDefragmentAsync_ShouldDefragmentMemory_WhenRequestValid()
    {
        // Arrange
        var request = new PostMemoryDefragmentRequest
        {
            MemoryType = "GPU"
        };

        var expectedResponse = new PostMemoryDefragmentResponse
        {
            Success = true,
            DefragmentedBytes = 8589934592, // 8GB
            DeviceId = Guid.NewGuid(),
            FragmentationReduced = 37.6f,
            Message = "Defragmentation completed successfully"
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceMemory.PostMemoryDefragmentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Success.Should().BeTrue();
        result.Data.FragmentationReduced.Should().Be(37.6f);

        _mockPythonWorkerService.Verify(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.Is<object>(o => JsonSerializer.Serialize(o, (JsonSerializerOptions?)null).Contains("Balanced")), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PostMemoryAllocateAsync_ShouldLogError_WhenExceptionOccurs()
    {
        // Arrange
        var request = new PostMemoryAllocateRequest
        {
            SizeBytes = 4294967296,
            MemoryType = "GPU"
        };

        var exception = new Exception("Test exception");
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error allocating memory")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMemoryStatusAsync_ShouldHandleInvalidResponse_Gracefully()
    {
        // Arrange
        // Return invalid JSON response
        _mockPythonWorkerService.Setup(x => x.ExecuteRawAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("invalid json response");

        // Act
        var result = await _serviceMemory.GetMemoryStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    #endregion
}
