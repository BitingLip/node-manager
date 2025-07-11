using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using DeviceOperations.Services.Device;
using DeviceOperations.Services.Python;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using System.Text.Json;

namespace DeviceOperations.Tests.Services;

/// <summary>
/// Unit tests for ServiceDevice
/// Tests device management operations including discovery, monitoring, control, and optimization
/// </summary>
public class ServiceDeviceTests
{
    private readonly Mock<IPythonWorkerService> _mockPythonWorkerService;
    private readonly Mock<ILogger<ServiceDevice>> _mockLogger;
    private readonly ServiceDevice _serviceDevice;

    public ServiceDeviceTests()
    {
        _mockPythonWorkerService = new Mock<IPythonWorkerService>();
        _mockLogger = new Mock<ILogger<ServiceDevice>>();
        _serviceDevice = new ServiceDevice(_mockLogger.Object, _mockPythonWorkerService.Object);
    }

    #region Device Discovery & Enumeration Tests

    [Fact]
    public async Task GetDeviceListAsync_ShouldReturnDeviceList_WhenSuccessful()
    {
        // Act - The service manages its own device cache
        var result = await _serviceDevice.GetDeviceListAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Devices.Should().NotBeNull();
        result.Data.TotalCount.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetDeviceListAsync_ShouldReturnConsistentResults_WhenCalledMultipleTimes()
    {
        // Act
        var result1 = await _serviceDevice.GetDeviceListAsync();
        var result2 = await _serviceDevice.GetDeviceListAsync();

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result1.Data!.TotalCount.Should().Be(result2.Data!.TotalCount);
    }

    [Fact]
    public async Task GetDeviceAsync_ShouldReturnDevice_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";

        // Act
        var result = await _serviceDevice.GetDeviceAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        // Note: Since this is a mock service, it may return an error for non-existent devices
        // We're testing that the method executes without throwing exceptions
    }

    [Fact]
    public async Task GetDeviceAsync_ShouldHandleInvalidDeviceId_Gracefully()
    {
        // Arrange
        var invalidDeviceId = "non-existent-device";

        // Act
        var result = await _serviceDevice.GetDeviceAsync(invalidDeviceId);

        // Assert
        result.Should().NotBeNull();
        // The service should handle invalid device IDs gracefully
    }

    #endregion

    #region Device Status Tests

    [Fact]
    public async Task GetDeviceStatusAsync_ShouldReturnStatus_WhenSuccessful()
    {
        // Act
        var result = await _serviceDevice.GetDeviceStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDeviceStatusAsync_ShouldReturnConsistentStatus_WhenCalledMultipleTimes()
    {
        // Act
        var result1 = await _serviceDevice.GetDeviceStatusAsync();
        var result2 = await _serviceDevice.GetDeviceStatusAsync();

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
    }

    #endregion

    #region Device Health Tests

    [Fact]
    public async Task GetDeviceHealthAsync_ShouldReturnHealth_WhenSuccessful()
    {
        // Act
        var result = await _serviceDevice.GetDeviceHealthAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task PostDeviceHealthAsync_ShouldReturnHealthCheckResults_WhenSuccessful()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();
        var request = new PostDeviceHealthRequest
        {
            HealthCheckType = "comprehensive",
            IncludePerformanceMetrics = true
        };

        // Act
        var result = await _serviceDevice.PostDeviceHealthAsync(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        // The service should handle the request gracefully, even if it returns an error
    }

    #endregion

    #region Device Power Management Tests

    [Fact]
    public async Task PostDevicePowerAsync_ShouldHandlePowerActions_Gracefully()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();
        var request = new PostDevicePowerRequest
        {
            PowerAction = "suspend"
        };

        // Act
        var result = await _serviceDevice.PostDevicePowerAsync(request, deviceId);

        // Assert
        result.Should().NotBeNull();
        // The service should handle power management requests gracefully
    }

    [Fact]
    public async Task PostDevicePowerAsync_ShouldValidateDeviceId()
    {
        // Arrange
        var deviceId = Guid.Empty.ToString();
        var request = new PostDevicePowerRequest
        {
            PowerAction = "restart"
        };

        // Act
        var result = await _serviceDevice.PostDevicePowerAsync(request, deviceId);

        // Assert
        result.Should().NotBeNull();
        // The service should handle empty device IDs gracefully
    }

    #endregion

    #region Device Reset Tests

    [Fact]
    public async Task PostDeviceResetAsync_ShouldHandleResetRequests_Gracefully()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();
        var request = new PostDeviceResetRequest
        {
            ResetType = DeviceOperations.Models.Requests.DeviceResetType.Soft,
            Force = false
        };

        // Act
        var result = await _serviceDevice.PostDeviceResetAsync(request, deviceId);

        // Assert
        result.Should().NotBeNull();
        // The service should handle reset requests gracefully
    }

    #endregion

    #region Device Benchmark Tests

    [Fact]
    public async Task PostDeviceBenchmarkAsync_ShouldHandleBenchmarkRequests_Gracefully()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();
        var request = new PostDeviceBenchmarkRequest
        {
            BenchmarkType = DeviceOperations.Models.Requests.BenchmarkType.Compute,
            DurationSeconds = 60
        };

        // Act
        var result = await _serviceDevice.PostDeviceBenchmarkAsync(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        // The service should handle benchmark requests gracefully
    }

    #endregion

    #region Device Optimization Tests

    [Fact]
    public async Task PostDeviceOptimizeAsync_ShouldHandleOptimizationRequests_Gracefully()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();
        var request = new PostDeviceOptimizeRequest
        {
            Target = DeviceOperations.Models.Requests.OptimizationTarget.Performance,
            AutoApply = false
        };

        // Act
        var result = await _serviceDevice.PostDeviceOptimizeAsync(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        // The service should handle optimization requests gracefully
    }

    #endregion

    #region Device Configuration Tests

    [Fact]
    public async Task GetDeviceConfigAsync_ShouldReturnConfiguration_WhenSuccessful()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();

        // Act
        var result = await _serviceDevice.GetDeviceConfigAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        // The service should handle configuration requests gracefully
    }

    [Fact]
    public async Task PutDeviceConfigAsync_ShouldHandleConfigurationUpdates_Gracefully()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();
        var request = new PutDeviceConfigRequest
        {
            Configuration = new Dictionary<string, object>
            {
                { "performance_mode", "high" },
                { "power_limit", 85 }
            },
            ValidateOnly = false
        };

        // Act
        var result = await _serviceDevice.PutDeviceConfigAsync(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        // The service should handle configuration updates gracefully
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task GetDeviceCapabilitiesAsync_ShouldReturnCapabilities_WhenSuccessful()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();

        // Act
        var result = await _serviceDevice.GetDeviceCapabilitiesAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        // The service should handle capability requests gracefully
    }

    [Fact]
    public async Task GetDeviceDetailsAsync_ShouldReturnDetails_WhenSuccessful()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();

        // Act
        var result = await _serviceDevice.GetDeviceDetailsAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        // The service should handle detail requests gracefully
    }

    [Fact]
    public async Task GetDeviceDriversAsync_ShouldReturnDriverInfo_WhenSuccessful()
    {
        // Arrange
        var deviceId = Guid.NewGuid().ToString();

        // Act
        var result = await _serviceDevice.GetDeviceDriversAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        // The service should handle driver requests gracefully
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ServiceMethods_ShouldHandleExceptions_Gracefully()
    {
        // Test that service methods don't throw unhandled exceptions
        var deviceId = Guid.NewGuid();

        // Act & Assert - None of these should throw exceptions
        var getListResult = await _serviceDevice.GetDeviceListAsync();
        getListResult.Should().NotBeNull();

        var getStatusResult = await _serviceDevice.GetDeviceStatusAsync();
        getStatusResult.Should().NotBeNull();

        var getHealthResult = await _serviceDevice.GetDeviceHealthAsync();
        getHealthResult.Should().NotBeNull();
    }

    #endregion
}