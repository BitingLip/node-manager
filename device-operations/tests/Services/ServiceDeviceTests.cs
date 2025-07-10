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
        _serviceDevice = new ServiceDevice(_mockPythonWorkerService.Object, _mockLogger.Object);
    }

    #region Device Discovery & Enumeration Tests

    [Fact]
    public async Task GetDeviceListAsync_ShouldReturnDeviceList_WhenSuccessful()
    {
        // Arrange
        var expectedResponse = new ListDevicesResponse
        {
            Devices = new List<DeviceInfo>
            {
                new DeviceInfo
                {
                    Id = Guid.NewGuid(),
                    Name = "NVIDIA RTX 4090",
                    Type = "GPU",
                    Status = "Available",
                    IsAvailable = true,
                    DriverVersion = "531.79",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            TotalDevices = 1,
            AvailableDevices = 1,
            UnavailableDevices = 0
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.GetDeviceListAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Devices.Should().HaveCount(1);
        result.Data.TotalDevices.Should().Be(1);
        result.Data.AvailableDevices.Should().Be(1);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("device_list", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task GetDeviceListAsync_ShouldReturnError_WhenPythonWorkerFails()
    {
        // Arrange
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new Exception("Python worker communication failed"));

        // Act
        var result = await _serviceDevice.GetDeviceListAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("Failed to retrieve device list");
    }

    [Fact]
    public async Task GetDeviceAsync_ShouldReturnDevice_WhenDeviceExists()
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
                IsAvailable = true,
                DriverVersion = "531.79",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.GetDeviceAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Device.Should().NotBeNull();
        result.Data.Device.Name.Should().Be("NVIDIA RTX 4090");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("get_device", It.Is<object>(o => 
            JsonSerializer.Serialize(o).Contains(deviceId))), Times.Once);
    }

    [Fact]
    public async Task GetDeviceAsync_ShouldThrowArgumentException_WhenDeviceIdIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _serviceDevice.GetDeviceAsync(null!));
    }

    [Fact]
    public async Task GetDeviceAsync_ShouldThrowArgumentException_WhenDeviceIdIsEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _serviceDevice.GetDeviceAsync(""));
    }

    #endregion

    #region Device Status & Health Tests

    [Fact]
    public async Task GetDeviceStatusAsync_ShouldReturnStatus_WhenNoDeviceSpecified()
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
                    IsResponsive = true,
                    LastChecked = DateTime.UtcNow,
                    Temperature = 45.5f,
                    MemoryUsage = 2048,
                    PowerUsage = 150.0f
                }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.GetDeviceStatusAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.OverallStatus.Should().Be("Healthy");
        result.Data.DeviceStatuses.Should().HaveCount(1);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("get_device_status", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task GetDeviceHealthAsync_ShouldReturnHealth_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new DeviceHealth
        {
            DeviceId = Guid.NewGuid(),
            OverallHealthScore = 85.5f,
            Status = "Good",
            LastCheckTime = DateTime.UtcNow,
            HealthMetrics = new Dictionary<string, float>
            {
                { "Temperature", 45.5f },
                { "MemoryHealth", 90.0f },
                { "PowerEfficiency", 85.0f }
            },
            Issues = new List<string>(),
            Recommendations = new List<string> { "Monitor temperature under heavy load" }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.GetDeviceHealthAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.OverallHealthScore.Should().Be(85.5f);
        result.Data.Status.Should().Be("Good");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("get_device_health", 
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(deviceId))), Times.Once);
    }

    [Fact]
    public async Task PostDeviceHealthAsync_ShouldPerformHealthCheck_WhenRequestValid()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostDeviceHealthRequest
        {
            CheckType = "Full",
            IncludeDetailedMetrics = true,
            TimeoutSeconds = 30
        };

        var expectedResponse = new PostDeviceHealthResponse
        {
            DeviceId = Guid.NewGuid(),
            CheckType = "Full",
            Status = "Completed",
            HealthScore = 88.0f,
            ExecutionTime = TimeSpan.FromSeconds(5.2),
            Metrics = new Dictionary<string, object>
            {
                { "CPUTemperature", 42.0f },
                { "GPUTemperature", 55.0f },
                { "MemoryHealth", 95.0f }
            },
            Issues = new List<string>(),
            Recommendations = new List<string> { "Consider cleaning GPU fans" }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.PostDeviceHealthAsync(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Completed");
        result.Data.HealthScore.Should().Be(88.0f);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("perform_device_health_check",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(deviceId) && 
                              JsonSerializer.Serialize(o).Contains("Full"))), Times.Once);
    }

    #endregion

    #region Device Control Tests

    [Fact]
    public async Task PostDevicePowerAsync_ShouldControlPower_WhenValidState()
    {
        // Arrange
        var deviceId = "test-device-id";
        var powerState = "enable";

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = true });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.PostDevicePowerAsync(deviceId, powerState);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("control_device_power",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(deviceId) && 
                              JsonSerializer.Serialize(o).Contains("enable"))), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PostDevicePowerAsync_ShouldThrowArgumentException_WhenInvalidDeviceId(string invalidDeviceId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _serviceDevice.PostDevicePowerAsync(invalidDeviceId, "enable"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PostDevicePowerAsync_ShouldThrowArgumentException_WhenInvalidPowerState(string invalidPowerState)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _serviceDevice.PostDevicePowerAsync("device-id", invalidPowerState));
    }

    [Fact]
    public async Task PostDeviceResetAsync_ShouldResetDevice_WhenRequestValid()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostDeviceResetRequest
        {
            ResetType = "Soft",
            PreserveConfiguration = true,
            TimeoutSeconds = 60
        };

        var expectedResponse = new PostDeviceResetResponse
        {
            DeviceId = Guid.NewGuid(),
            ResetType = "Soft",
            Status = "Completed",
            ExecutionTime = TimeSpan.FromSeconds(10.5),
            CompletedAt = DateTime.UtcNow,
            ConfigurationPreserved = true,
            Operations = new List<string>
            {
                "Driver reset",
                "Memory cleared",
                "Configuration restored"
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.PostDeviceResetAsync(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Completed");
        result.Data.ResetType.Should().Be("Soft");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("reset_device",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(deviceId) && 
                              JsonSerializer.Serialize(o).Contains("Soft"))), Times.Once);
    }

    #endregion

    #region Device Optimization Tests

    [Fact]
    public async Task PostDeviceBenchmarkAsync_ShouldRunBenchmark_WhenRequestValid()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostDeviceBenchmarkRequest
        {
            BenchmarkType = "Performance",
            Duration = TimeSpan.FromMinutes(5),
            IncludeMemoryTest = true,
            IncludeComputeTest = true
        };

        var expectedResponse = new PostDeviceBenchmarkResponse
        {
            DeviceId = Guid.NewGuid(),
            BenchmarkType = "Performance",
            Status = "Completed",
            ExecutionTime = TimeSpan.FromMinutes(5.2),
            Score = 8950.5f,
            Results = new Dictionary<string, object>
            {
                { "ComputeScore", 9200.0f },
                { "MemoryScore", 8700.0f },
                { "ThermalScore", 8500.0f }
            },
            Metrics = new Dictionary<string, float>
            {
                { "AverageTemperature", 72.5f },
                { "PeakMemoryUsage", 18.2f },
                { "PowerEfficiency", 85.0f }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.PostDeviceBenchmarkAsync(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Completed");
        result.Data.Score.Should().Be(8950.5f);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("run_device_benchmark",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(deviceId) && 
                              JsonSerializer.Serialize(o).Contains("Performance"))), Times.Once);
    }

    [Fact]
    public async Task PostDeviceOptimizeAsync_ShouldOptimizeDevice_WhenRequestValid()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostDeviceOptimizeRequest
        {
            OptimizationType = "Performance",
            TargetWorkload = "MachineLearning",
            AggressiveOptimization = false
        };

        var expectedResponse = new PostDeviceOptimizeResponse
        {
            DeviceId = Guid.NewGuid(),
            OptimizationType = "Performance",
            Status = "Completed",
            ExecutionTime = TimeSpan.FromSeconds(15.3),
            OptimizationsApplied = new List<string>
            {
                "Memory frequency optimization",
                "Power limit adjustment",
                "Thermal profile update"
            },
            PerformanceImprovement = 12.5f,
            Recommendations = new List<string>
            {
                "Monitor temperatures during heavy workloads"
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.PostDeviceOptimizeAsync(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Completed");
        result.Data.PerformanceImprovement.Should().Be(12.5f);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("optimize_device",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(deviceId) && 
                              JsonSerializer.Serialize(o).Contains("Performance"))), Times.Once);
    }

    #endregion

    #region Device Configuration Tests

    [Fact]
    public async Task GetDeviceConfigAsync_ShouldReturnConfig_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetDeviceConfigResponse
        {
            DeviceId = Guid.NewGuid(),
            Configuration = new Dictionary<string, object>
            {
                { "PowerLimit", 350 },
                { "MemoryClockOffset", 500 },
                { "CoreClockOffset", 100 },
                { "ThermalTarget", 83 }
            },
            ConfigurationProfile = "Performance",
            LastModified = DateTime.UtcNow.AddHours(-2),
            IsOptimal = true
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.GetDeviceConfigAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.ConfigurationProfile.Should().Be("Performance");
        result.Data.IsOptimal.Should().BeTrue();

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("get_device_config",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(deviceId))), Times.Once);
    }

    [Fact]
    public async Task PutDeviceConfigAsync_ShouldUpdateConfig_WhenRequestValid()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PutDeviceConfigRequest
        {
            Configuration = new Dictionary<string, object>
            {
                { "PowerLimit", 320 },
                { "MemoryClockOffset", 400 }
            },
            ConfigurationProfile = "Balanced",
            ValidateBeforeApply = true
        };

        var expectedResponse = new PutDeviceConfigResponse
        {
            DeviceId = Guid.NewGuid(),
            Status = "Applied",
            UpdatedConfiguration = request.Configuration,
            ConfigurationProfile = "Balanced",
            ValidationResults = new Dictionary<string, bool>
            {
                { "PowerLimitValid", true },
                { "MemoryOffsetValid", true }
            },
            AppliedAt = DateTime.UtcNow
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceDevice.PutDeviceConfigAsync(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Applied");
        result.Data.ConfigurationProfile.Should().Be("Balanced");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("update_device_config",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(deviceId) && 
                              JsonSerializer.Serialize(o).Contains("Balanced"))), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetDeviceListAsync_ShouldLogError_WhenExceptionOccurs()
    {
        // Arrange
        var exception = new Exception("Test exception");
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _serviceDevice.GetDeviceListAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving device list")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostDevicePowerAsync_ShouldHandleInvalidResponse_Gracefully()
    {
        // Arrange
        var deviceId = "test-device-id";
        var powerState = "enable";
        
        // Return invalid JSON response
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync("invalid json response");

        // Act & Assert
        var result = await _serviceDevice.PostDevicePowerAsync(deviceId, powerState);
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    #endregion

    #region Resource Cleanup Tests

    [Fact]
    public void Dispose_ShouldNotThrow_WhenCalled()
    {
        // Act & Assert
        var exception = Record.Exception(() => _serviceDevice.Dispose());
        exception.Should().BeNull();
    }

    #endregion
}
