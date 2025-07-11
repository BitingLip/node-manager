using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DeviceOperations.Controllers;
using DeviceOperations.Services.Memory;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Tests.Controllers;

/// <summary>
/// Unit tests for ControllerMemory
/// Tests memory management API endpoints including allocation, monitoring, optimization, and cleanup
/// </summary>
public class ControllerMemoryTests
{
    private readonly Mock<IServiceMemory> _mockServiceMemory;
    private readonly Mock<ILogger<ControllerMemory>> _mockLogger;
    private readonly ControllerMemory _controller;

    public ControllerMemoryTests()
    {
        _mockServiceMemory = new Mock<IServiceMemory>();
        _mockLogger = new Mock<ILogger<ControllerMemory>>();
        _controller = new ControllerMemory(_mockServiceMemory.Object, _mockLogger.Object);
    }

    #region Memory Status Tests

    [Fact]
    public async Task GetMemoryStatus_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new GetMemoryStatusResponse
        {
            MemoryStatus = new Dictionary<string, object>
            {
                ["total_memory_gb"] = 16,
                ["used_memory_gb"] = 8,
                ["available_memory_gb"] = 8,
                ["utilization_percentage"] = 50.0,
                ["device_count"] = 2
            }
        };

        var serviceResponse = ApiResponse<GetMemoryStatusResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.GetMemoryStatusAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetMemoryStatus();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetMemoryStatusResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.MemoryStatus.Should().ContainKey("total_memory_gb");

        _mockServiceMemory.Verify(x => x.GetMemoryStatusAsync(), Times.Once);
    }

    [Fact]
    public async Task GetMemoryStatus_WithDeviceId_ShouldReturnOk_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetMemoryStatusResponse
        {
            MemoryStatus = new Dictionary<string, object>
            {
                ["device_id"] = deviceId,
                ["total_memory_gb"] = 8,
                ["used_memory_gb"] = 2,
                ["available_memory_gb"] = 6,
                ["utilization_percentage"] = 25.0
            }
        };

        var serviceResponse = ApiResponse<GetMemoryStatusResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.GetMemoryStatusAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetMemoryStatus(deviceId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceMemory.Verify(x => x.GetMemoryStatusAsync(deviceId), Times.Once);
    }

    [Fact]
    public async Task GetMemoryStatus_ShouldReturnBadRequest_WhenDeviceIdIsEmpty()
    {
        // Act
        var result = await _controller.GetMemoryStatus("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Memory Usage Tests

    [Fact]
    public async Task GetMemoryUsage_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new GetMemoryUsageResponse
        {
            DeviceId = Guid.NewGuid(),
            UsageData = new Dictionary<string, object>
            {
                { "total_memory", 16384 },
                { "used_memory", 4096 },
                { "free_memory", 12288 }
            },
            Timestamp = DateTime.UtcNow
        };

        var serviceResponse = ApiResponse<GetMemoryUsageResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.GetMemoryUsageAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetMemoryUsage();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetMemoryUsageResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.UsageData.Should().ContainKey("total_memory");

        _mockServiceMemory.Verify(x => x.GetMemoryUsageAsync(), Times.Once);
    }

    [Fact]
    public async Task GetMemoryUsage_WithDeviceId_ShouldReturnOk_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetMemoryUsageResponse
        {
            DeviceId = Guid.NewGuid(),
            UsageData = new Dictionary<string, object>
            {
                { "device_memory", 8192 },
                { "used_memory", 2048 },
                { "free_memory", 6144 }
            },
            Timestamp = DateTime.UtcNow
        };

        var serviceResponse = ApiResponse<GetMemoryUsageResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.GetMemoryUsageAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetMemoryUsage(deviceId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceMemory.Verify(x => x.GetMemoryUsageAsync(deviceId), Times.Once);
    }

    #endregion

    #region Memory Allocation Tests

    [Fact]
    public async Task GetMemoryAllocations_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new GetMemoryAllocationsResponse
        {
            DeviceId = Guid.NewGuid(),
            LastUpdated = DateTime.UtcNow
        };

        var serviceResponse = ApiResponse<GetMemoryAllocationsResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.GetMemoryAllocationsAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetMemoryAllocations();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceMemory.Verify(x => x.GetMemoryAllocationsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetMemoryAllocations_WithDeviceId_ShouldReturnOk_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetMemoryAllocationsResponse
        {
            DeviceId = Guid.NewGuid(),
            LastUpdated = DateTime.UtcNow
        };

        var serviceResponse = ApiResponse<GetMemoryAllocationsResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.GetMemoryAllocationsAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetMemoryAllocations(deviceId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceMemory.Verify(x => x.GetMemoryAllocationsAsync(deviceId), Times.Once);
    }

    [Fact]
    public async Task GetMemoryAllocation_ShouldReturnOk_WhenAllocationExists()
    {
        // Arrange
        var allocationId = "test-allocation-id";
        var expectedResponse = new GetMemoryAllocationResponse
        {
            AllocationId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            AllocationSize = 1024,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var serviceResponse = ApiResponse<GetMemoryAllocationResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.GetMemoryAllocationAsync(allocationId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetMemoryAllocation(allocationId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceMemory.Verify(x => x.GetMemoryAllocationAsync(allocationId), Times.Once);
    }

    [Fact]
    public async Task PostMemoryAllocate_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new PostMemoryAllocateRequest
        {
            SizeBytes = 1048576, // 1MB
            MemoryType = "GPU"
        };

        var expectedResponse = new PostMemoryAllocateResponse
        {
            Success = true,
            AllocationId = Guid.NewGuid().ToString()
        };

        var serviceResponse = ApiResponse<PostMemoryAllocateResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.PostMemoryAllocateAsync(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostMemoryAllocate(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostMemoryAllocateResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();

        _mockServiceMemory.Verify(x => x.PostMemoryAllocateAsync(request), Times.Once);
    }

    [Fact]
    public async Task PostMemoryAllocate_WithDeviceId_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostMemoryAllocateRequest
        {
            SizeBytes = 2097152, // 2MB
            MemoryType = "VRAM"
        };

        var expectedResponse = new PostMemoryAllocateResponse
        {
            Success = true,
            AllocationId = Guid.NewGuid().ToString()
        };

        var serviceResponse = ApiResponse<PostMemoryAllocateResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.PostMemoryAllocateAsync(request, deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostMemoryAllocate(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceMemory.Verify(x => x.PostMemoryAllocateAsync(request, deviceId), Times.Once);
    }

    [Fact]
    public async Task DeleteMemoryAllocation_ShouldReturnOk_WhenAllocationExists()
    {
        // Arrange
        var allocationId = "test-allocation-id";
        var expectedResponse = new DeleteMemoryAllocationResponse
        {
            AllocationId = Guid.NewGuid()
        };

        var serviceResponse = ApiResponse<DeleteMemoryAllocationResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.DeleteMemoryAllocationAsync(allocationId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DeleteMemoryAllocation(allocationId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceMemory.Verify(x => x.DeleteMemoryAllocationAsync(allocationId), Times.Once);
    }

    #endregion

    #region Memory Operations Tests

    [Fact]
    public async Task PostMemoryClear_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new PostMemoryClearRequest
        {
            MemoryType = "VRAM",
            Force = false
        };

        var expectedResponse = new PostMemoryClearResponse
        {
            Success = true,
            ClearedBytes = 1048576 // 1MB
        };

        var serviceResponse = ApiResponse<PostMemoryClearResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.PostMemoryClearAsync(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostMemoryClear(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostMemoryClearResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();

        _mockServiceMemory.Verify(x => x.PostMemoryClearAsync(request), Times.Once);
    }

    [Fact]
    public async Task PostMemoryClear_WithDeviceId_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostMemoryClearRequest
        {
            MemoryType = "RAM",
            Force = true
        };

        var expectedResponse = new PostMemoryClearResponse
        {
            Success = true,
            ClearedBytes = 2097152 // 2MB
        };

        var serviceResponse = ApiResponse<PostMemoryClearResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.PostMemoryClearAsync(request, deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostMemoryClear(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceMemory.Verify(x => x.PostMemoryClearAsync(request, deviceId), Times.Once);
    }

    [Fact]
    public async Task PostMemoryDefragment_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new PostMemoryDefragmentRequest
        {
            MemoryType = "VRAM"
        };

        var expectedResponse = new PostMemoryDefragmentResponse
        {
            Success = true,
            DefragmentedBytes = 1048576, // 1MB
            FragmentationReduced = 25.0f
        };

        var serviceResponse = ApiResponse<PostMemoryDefragmentResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.PostMemoryDefragmentAsync(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostMemoryDefragment(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostMemoryDefragmentResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();

        _mockServiceMemory.Verify(x => x.PostMemoryDefragmentAsync(request), Times.Once);
    }

    [Fact]
    public async Task PostMemoryDefragment_WithDeviceId_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostMemoryDefragmentRequest
        {
            MemoryType = "RAM"
        };

        var expectedResponse = new PostMemoryDefragmentResponse
        {
            Success = true,
            DefragmentedBytes = 2097152, // 2MB
            FragmentationReduced = 30.0f
        };

        var serviceResponse = ApiResponse<PostMemoryDefragmentResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.PostMemoryDefragmentAsync(request, deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostMemoryDefragment(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceMemory.Verify(x => x.PostMemoryDefragmentAsync(request, deviceId), Times.Once);
    }

    #endregion

    #region Memory Transfer Tests

    [Fact]
    public async Task PostMemoryTransfer_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new PostMemoryTransferRequest
        {
            SourceDeviceId = "source-device",
            TargetDeviceId = "target-device",
            SizeBytes = 1048576 // 1MB
        };

        var expectedResponse = new PostMemoryTransferResponse
        {
            TransferId = Guid.NewGuid().ToString(),
            Success = true
        };

        var serviceResponse = ApiResponse<PostMemoryTransferResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.PostMemoryTransferAsync(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostMemoryTransfer(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostMemoryTransferResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();

        _mockServiceMemory.Verify(x => x.PostMemoryTransferAsync(request), Times.Once);
    }

    [Fact]
    public async Task GetMemoryTransfer_ShouldReturnOk_WhenTransferExists()
    {
        // Arrange
        var transferId = "test-transfer-id";
        var expectedResponse = new GetMemoryTransferResponse
        {
            TransferId = Guid.NewGuid(),
            Status = "Completed",
            Progress = 100.0f,
            CompletedAt = DateTime.UtcNow
        };

        var serviceResponse = ApiResponse<GetMemoryTransferResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceMemory.Setup(x => x.GetMemoryTransferAsync(transferId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetMemoryTransfer(transferId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceMemory.Verify(x => x.GetMemoryTransferAsync(transferId), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PostMemoryAllocate_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostMemoryAllocate(null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostMemoryClear_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostMemoryClear(null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostMemoryTransfer_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostMemoryTransfer(null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetMemoryAllocation_ShouldReturnBadRequest_WhenAllocationIdIsEmpty()
    {
        // Act
        var result = await _controller.GetMemoryAllocation("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetMemoryTransfer_ShouldReturnBadRequest_WhenTransferIdIsEmpty()
    {
        // Act
        var result = await _controller.GetMemoryTransfer("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetMemoryStatus_ShouldReturnInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        _mockServiceMemory.Setup(x => x.GetMemoryStatusAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetMemoryStatus();

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error in GetMemoryStatus")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ControllerMemory(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ControllerMemory(_mockServiceMemory.Object, null!));
    }

    #endregion
}
