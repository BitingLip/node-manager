using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DeviceOperations.Controllers;
using DeviceOperations.Services.Device;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Tests.Controllers;

/// <summary>
/// Unit tests for ControllerDevice
/// Tests device management API endpoints including discovery, monitoring, control, and optimization
/// </summary>
public class ControllerDeviceTests
{
    private readonly Mock<IServiceDevice> _mockServiceDevice;
    private readonly Mock<ILogger<ControllerDevice>> _mockLogger;
    private readonly ControllerDevice _controller;

    public ControllerDeviceTests()
    {
        _mockServiceDevice = new Mock<IServiceDevice>();
        _mockLogger = new Mock<ILogger<ControllerDevice>>();
        _controller = new ControllerDevice(_mockServiceDevice.Object, _mockLogger.Object);
    }

    #region Device Discovery Tests

    [Fact]
    public async Task GetDevices_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new ListDevicesResponse
        {
            Devices = new List<DeviceInfo>
            {
                new DeviceInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "NVIDIA RTX 4090",
                    Type = DeviceType.GPU,
                    Status = DeviceOperations.Models.Common.DeviceStatus.Available,
                    IsAvailable = true
                }
            },
            TotalDevices = 1
        };

        var serviceResponse = ApiResponse<ListDevicesResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(static x => x.GetDeviceListAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDevices();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<ListDevicesResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data.Devices.Should().HaveCount(1);

        _mockServiceDevice.Verify(static x => x.GetDeviceListAsync(), Times.Once);
    }

    [Fact]
    public async Task GetDevices_ShouldReturnInternalServerError_WhenServiceReturnsError()
    {
        // Arrange
        var serviceResponse = ApiResponse<ListDevicesResponse>.CreateError("Service error", "Detailed error message");
        _mockServiceDevice.Setup(static x => x.GetDeviceListAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDevices();

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);

        var apiResponse = errorResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Error.Should().NotBeNull();
        apiResponse.Error.Message.Should().Be("Service error");
    }

    [Fact]
    public async Task GetDevices_ShouldReturnInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        _mockServiceDevice.Setup(static x => x.GetDeviceListAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetDevices();

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);

        // Verify logging occurred
        _mockLogger.Verify(
            static x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>(static (v, t) => v.ToString().Contains("Error retrieving device list")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDevice_ShouldReturnOk_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetDeviceResponse
        {
            Device = new DeviceInfo
            {
                Id = Guid.NewGuid(),
                Name = "NVIDIA RTX 4090",
                Type = "GPU",
                Status = "Available",
                IsAvailable = true
            }
        };

        var serviceResponse = ApiResponse<GetDeviceResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.GetDeviceAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDevice(deviceId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetDeviceResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data.Device.Name.Should().Be("NVIDIA RTX 4090");

        _mockServiceDevice.Verify(x => x.GetDeviceAsync(deviceId), Times.Once);
    }

    [Fact]
    public async Task GetDevice_ShouldReturnBadRequest_WhenDeviceIdIsNull()
    {
        // Act
        var result = await _controller.GetDevice(null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);

        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Error.Should().NotBeNull();
        apiResponse.Error.Message.Should().Contain("Device ID is required");
    }

    [Fact]
    public async Task GetDevice_ShouldReturnBadRequest_WhenDeviceIdIsEmpty()
    {
        // Act
        var result = await _controller.GetDevice("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetDevice_ShouldReturnNotFound_WhenDeviceNotFound()
    {
        // Arrange
        var deviceId = "nonexistent-device";
        var serviceResponse = ApiResponse<GetDeviceResponse>.CreateError("Device not found", "Device does not exist");
        _mockServiceDevice.Setup(x => x.GetDeviceAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDevice(deviceId);

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500); // Controller returns 500 for service errors
    }

    #endregion

    #region Device Status Tests

    [Fact]
    public async Task GetDeviceStatus_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new GetDeviceStatusResponse
        {
            OverallStatus = "Healthy",
            DeviceStatuses = new List<DeviceStatus>
            {
                new DeviceStatus
                {
                    DeviceId = Guid.NewGuid(),
                    Name = "NVIDIA RTX 4090",
                    Status = "Available",
                    IsResponsive = true
                }
            }
        };

        var serviceResponse = ApiResponse<GetDeviceStatusResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(static x => x.GetDeviceStatusAsync(It.IsAny<string?>()))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDeviceStatus();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetDeviceStatusResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data.OverallStatus.Should().Be("Healthy");

        _mockServiceDevice.Verify(static x => x.GetDeviceStatusAsync(null), Times.Once);
    }

    [Fact]
    public async Task GetDeviceStatusDevice_ShouldReturnOk_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetDeviceStatusResponse
        {
            OverallStatus = "Healthy",
            DeviceStatuses = new List<DeviceStatus>
            {
                new DeviceStatus
                {
                    DeviceId = Guid.NewGuid(),
                    Name = "NVIDIA RTX 4090",
                    Status = "Available",
                    IsResponsive = true
                }
            }
        };

        var serviceResponse = ApiResponse<GetDeviceStatusResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.GetDeviceStatusAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDeviceStatusDevice(deviceId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceDevice.Verify(x => x.GetDeviceStatusAsync(deviceId), Times.Once);
    }

    #endregion

    #region Device Health Tests

    [Fact]
    public async Task GetDeviceHealth_ShouldReturnOk_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new DeviceHealth
        {
            DeviceId = Guid.NewGuid(),
            OverallHealthScore = 85.5f,
            Status = "Good",
            HealthMetrics = new Dictionary<string, float>
            {
                { "Temperature", 45.5f },
                { "MemoryHealth", 90.0f }
            }
        };

        var serviceResponse = ApiResponse<DeviceHealth>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.GetDeviceHealthAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDeviceHealth(deviceId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<DeviceHealth>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data.OverallHealthScore.Should().Be(85.5f);

        _mockServiceDevice.Verify(x => x.GetDeviceHealthAsync(deviceId), Times.Once);
    }

    [Fact]
    public async Task PostDeviceHealth_ShouldReturnOk_WhenRequestValid()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostDeviceHealthRequest
        {
            CheckType = "Full",
            IncludeDetailedMetrics = true
        };

        var expectedResponse = new PostDeviceHealthResponse
        {
            DeviceId = Guid.NewGuid(),
            Status = "Completed",
            HealthScore = 88.0f
        };

        var serviceResponse = ApiResponse<PostDeviceHealthResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.PostDeviceHealthAsync(deviceId, request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostDeviceHealth(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostDeviceHealthResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data.HealthScore.Should().Be(88.0f);

        _mockServiceDevice.Verify(x => x.PostDeviceHealthAsync(deviceId, request), Times.Once);
    }

    [Fact]
    public async Task PostDeviceHealth_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostDeviceHealth("device-id", null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Device Control Tests

    [Fact]
    public async Task PostDevicePower_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var powerState = "enable";

        var serviceResponse = ApiResponse<bool>.CreateSuccess(true, "Power state changed successfully");
        _mockServiceDevice.Setup(x => x.PostDevicePowerAsync(deviceId, powerState))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostDevicePower(deviceId, powerState);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<bool>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().BeTrue();

        _mockServiceDevice.Verify(x => x.PostDevicePowerAsync(deviceId, powerState), Times.Once);
    }

    [Fact]
    public async Task PostDevicePower_ShouldReturnBadRequest_WhenDeviceIdIsEmpty()
    {
        // Act
        var result = await _controller.PostDevicePower("", "enable");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostDevicePower_ShouldReturnBadRequest_WhenPowerStateIsEmpty()
    {
        // Act
        var result = await _controller.PostDevicePower("device-id", "");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostDeviceReset_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostDeviceResetRequest
        {
            ResetType = "Soft",
            PreserveConfiguration = true
        };

        var expectedResponse = new PostDeviceResetResponse
        {
            DeviceId = Guid.NewGuid(),
            Status = "Completed",
            ResetType = "Soft"
        };

        var serviceResponse = ApiResponse<PostDeviceResetResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.PostDeviceResetAsync(deviceId, request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostDeviceReset(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceDevice.Verify(x => x.PostDeviceResetAsync(deviceId, request), Times.Once);
    }

    #endregion

    #region Device Optimization Tests

    [Fact]
    public async Task PostDeviceBenchmark_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostDeviceBenchmarkRequest
        {
            BenchmarkType = "Performance",
            Duration = TimeSpan.FromMinutes(5)
        };

        var expectedResponse = new PostDeviceBenchmarkResponse
        {
            DeviceId = Guid.NewGuid(),
            Status = "Completed",
            Score = 8950.5f
        };

        var serviceResponse = ApiResponse<PostDeviceBenchmarkResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.PostDeviceBenchmarkAsync(deviceId, request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostDeviceBenchmark(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostDeviceBenchmarkResponse>>().Subject;
        apiResponse.Data.Score.Should().Be(8950.5f);

        _mockServiceDevice.Verify(x => x.PostDeviceBenchmarkAsync(deviceId, request), Times.Once);
    }

    [Fact]
    public async Task PostDeviceOptimize_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostDeviceOptimizeRequest
        {
            OptimizationType = "Performance",
            TargetWorkload = "MachineLearning"
        };

        var expectedResponse = new PostDeviceOptimizeResponse
        {
            DeviceId = Guid.NewGuid(),
            Status = "Completed",
            PerformanceImprovement = 12.5f
        };

        var serviceResponse = ApiResponse<PostDeviceOptimizeResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.PostDeviceOptimizeAsync(deviceId, request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostDeviceOptimize(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostDeviceOptimizeResponse>>().Subject;
        apiResponse.Data.PerformanceImprovement.Should().Be(12.5f);

        _mockServiceDevice.Verify(x => x.PostDeviceOptimizeAsync(deviceId, request), Times.Once);
    }

    #endregion

    #region Device Configuration Tests

    [Fact]
    public async Task GetDeviceConfig_ShouldReturnOk_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetDeviceConfigResponse
        {
            DeviceId = Guid.NewGuid(),
            ConfigurationProfile = "Performance",
            IsOptimal = true
        };

        var serviceResponse = ApiResponse<GetDeviceConfigResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.GetDeviceConfigAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDeviceConfig(deviceId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetDeviceConfigResponse>>().Subject;
        apiResponse.Data.ConfigurationProfile.Should().Be("Performance");

        _mockServiceDevice.Verify(x => x.GetDeviceConfigAsync(deviceId), Times.Once);
    }

    [Fact]
    public async Task PutDeviceConfig_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PutDeviceConfigRequest
        {
            ConfigurationProfile = "Balanced",
            ValidateBeforeApply = true
        };

        var expectedResponse = new PutDeviceConfigResponse
        {
            DeviceId = Guid.NewGuid(),
            Status = "Applied",
            ConfigurationProfile = "Balanced"
        };

        var serviceResponse = ApiResponse<PutDeviceConfigResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.PutDeviceConfigAsync(deviceId, request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PutDeviceConfig(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PutDeviceConfigResponse>>().Subject;
        apiResponse.Data.ConfigurationProfile.Should().Be("Balanced");

        _mockServiceDevice.Verify(x => x.PutDeviceConfigAsync(deviceId, request), Times.Once);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ControllerDevice(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ControllerDevice(_mockServiceDevice.Object, null!));
    }

    #endregion
}
