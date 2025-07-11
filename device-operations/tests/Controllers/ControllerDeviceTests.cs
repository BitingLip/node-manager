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
    public async Task GetDeviceList_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new GetDeviceListResponse
        {
            Devices = new List<DeviceInfo>
            {
                new DeviceInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "NVIDIA RTX 4090",
                    Type = DeviceType.GPU,
                    Status = DeviceStatus.Available,
                    IsAvailable = true
                }
            },
            TotalCount = 1
        };

        var serviceResponse = ApiResponse<GetDeviceListResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.GetDeviceListAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDeviceList();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetDeviceListResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Devices.Should().HaveCount(1);

        _mockServiceDevice.Verify(x => x.GetDeviceListAsync(), Times.Once);
    }

    [Fact]
    public async Task GetDeviceList_ShouldReturnBadRequest_WhenServiceReturnsError()
    {
        // Arrange
        var serviceResponse = ApiResponse<GetDeviceListResponse>.CreateError("Service error", "Detailed error message");
        _mockServiceDevice.Setup(x => x.GetDeviceListAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDeviceList();

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);

        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse<GetDeviceListResponse>>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Error.Should().NotBeNull();
        apiResponse.Error!.Message.Should().Be("Service error");
    }

    [Fact]
    public async Task GetDeviceList_ShouldReturnInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        _mockServiceDevice.Setup(x => x.GetDeviceListAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetDeviceList();

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error in GetDeviceList")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Device Status Tests

    [Fact]
    public async Task GetDeviceStatus_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new GetDeviceStatusResponse
        {
            DeviceId = "test-device",
            Status = DeviceStatus.Available,
            StatusDescription = "Device is available",
            Utilization = new DeviceUtilization
            {
                CpuUtilization = 25.0,
                MemoryUtilization = 45.0,
                GpuUtilization = 0.0
            }
        };

        var serviceResponse = ApiResponse<GetDeviceStatusResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.GetDeviceStatusAsync(It.IsAny<string?>()))
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
        apiResponse.Data!.Status.Should().Be(DeviceStatus.Available);

        _mockServiceDevice.Verify(x => x.GetDeviceStatusAsync(null), Times.Once);
    }

    [Fact]
    public async Task GetDeviceStatus_ShouldReturnOk_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetDeviceStatusResponse
        {
            DeviceId = deviceId,
            Status = DeviceStatus.Available,
            StatusDescription = "Device is available"
        };

        var serviceResponse = ApiResponse<GetDeviceStatusResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.GetDeviceStatusAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDeviceStatus(deviceId);

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
        var expectedResponse = new GetDeviceHealthResponse
        {
            DeviceId = Guid.NewGuid(),
            HealthStatus = "Good",
            HealthMetrics = new Dictionary<string, object>
            {
                { "Temperature", 45.5 },
                { "MemoryHealth", 90.0 }
            }
        };

        var serviceResponse = ApiResponse<GetDeviceHealthResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.GetDeviceHealthAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetDeviceHealth(deviceId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetDeviceHealthResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.HealthStatus.Should().Be("Good");

        _mockServiceDevice.Verify(x => x.GetDeviceHealthAsync(deviceId), Times.Once);
    }

    #endregion

    #region Device Control Tests

    [Fact]
    public async Task PostDevicePower_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostDevicePowerRequest
        {
            PowerAction = "enable",
            Parameters = new Dictionary<string, object>()
        };

        var expectedResponse = new PostDevicePowerResponse
        {
            Success = true,
            PowerState = "enabled",
            Message = "Power state changed successfully"
        };

        var serviceResponse = ApiResponse<PostDevicePowerResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceDevice.Setup(x => x.PostDevicePowerAsync(request, deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostDevicePower(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostDevicePowerResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();

        _mockServiceDevice.Verify(x => x.PostDevicePowerAsync(request, deviceId), Times.Once);
    }

    [Fact]
    public async Task PostDevicePower_ShouldReturnBadRequest_WhenDeviceIdIsEmpty()
    {
        // Arrange
        var request = new PostDevicePowerRequest { PowerAction = "enable" };

        // Act
        var result = await _controller.PostDevicePower("", request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostDevicePower_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostDevicePower("device-id", null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
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
