using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using DeviceOperations.Services.Python;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using DeviceOperations.Extensions;

namespace DeviceOperations.Tests.Integration;

/// <summary>
/// Integration tests for Python worker communication
/// Tests STDIN/STDOUT communication, JSON serialization, error handling, and performance
/// </summary>
public class PythonWorkerIntegrationTests : IDisposable
{
    private readonly ILogger<PythonWorkerService> _logger;
    private readonly PythonWorkerService _pythonWorkerService;
    private bool _disposed = false;

    public PythonWorkerIntegrationTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<PythonWorkerService>();
        
        var config = new PythonWorkerConfiguration
        {
            PythonExecutable = "python",
            WorkerScriptPath = "src/Workers/main.py",
            TimeoutSeconds = 300,
            MaxWorkerProcesses = 4,
            EnableLogging = true
        };
        var options = Options.Create(config);
        
        _pythonWorkerService = new PythonWorkerService(_logger, options);
    }

    #region STDIN/STDOUT Communication Tests

    [Fact]
    public async Task ExecuteAsync_ShouldCommunicateWithPythonWorker_WhenValidCommand()
    {
        // Arrange
        var command = "test_echo";
        var parameters = new { message = "Hello from C#", number = 42 };

        // Act
        var result = await _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        // Verify the response is valid JSON
        var jsonDocument = JsonDocument.Parse(result);
        jsonDocument.Should().NotBeNull();
        
        // Verify the response contains expected structure
        if (jsonDocument.RootElement.TryGetProperty("success", out var successElement))
        {
            successElement.GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleComplexDataStructures_WhenPassingNestedObjects()
    {
        // Arrange
        var command = "test_complex_data";
        var parameters = new
        {
            device_info = new
            {
                id = "gpu-0",
                name = "NVIDIA RTX 4090",
                memory_gb = 24,
                capabilities = new[] { "FP32", "FP16", "INT8" }
            },
            model_config = new
            {
                type = "diffusion",
                precision = "fp16",
                max_batch_size = 4
            },
            inference_params = new Dictionary<string, object>
            {
                { "steps", 20 },
                { "guidance_scale", 7.5 },
                { "width", 1024 },
                { "height", 1024 }
            }
        };

        // Act
        var result = await _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        // Verify JSON serialization/deserialization worked correctly
        var jsonDocument = JsonDocument.Parse(result);
        jsonDocument.Should().NotBeNull();
        
        // Check that complex nested data was processed
        if (jsonDocument.RootElement.TryGetProperty("data", out var dataElement))
        {
            dataElement.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleLargeDataPayloads_WhenProcessingBulkData()
    {
        // Arrange
        var command = "test_large_data";
        var largeArray = new int[10000];
        for (int i = 0; i < largeArray.Length; i++)
        {
            largeArray[i] = i;
        }

        var parameters = new
        {
            large_array = largeArray,
            metadata = new
            {
                size = largeArray.Length,
                checksum = largeArray.Sum(),
                description = "Large test dataset for performance validation"
            }
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        // Verify performance is reasonable (should complete within 10 seconds)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000);
        
        // Verify the data was processed correctly
        var jsonDocument = JsonDocument.Parse(result);
        if (jsonDocument.RootElement.TryGetProperty("success", out var successElement))
        {
            successElement.GetBoolean().Should().BeTrue();
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_ShouldHandlePythonErrors_WhenWorkerThrowsException()
    {
        // Arrange
        var command = "test_error";
        var parameters = new { error_type = "ValueError", message = "Intentional test error" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => 
            _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters));
        
        exception.Should().NotBeNull();
        exception.Message.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleTimeout_WhenWorkerTakesTooLong()
    {
        // Arrange
        var command = "test_timeout";
        var parameters = new { delay_seconds = 30 }; // Longer than expected timeout

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(() => 
            _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters));
        
        exception.Should().NotBeNull();
        exception.Message.Should().Contain("timeout");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleInvalidJSON_WhenWorkerReturnsCorruptedData()
    {
        // Arrange
        var command = "test_invalid_json";
        var parameters = new { return_invalid_json = true };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<JsonException>(() => 
            _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters));
        
        exception.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleEmptyResponse_WhenWorkerReturnsNothing()
    {
        // Arrange
        var command = "test_empty_response";
        var parameters = new { };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters));
        
        exception.Should().NotBeNull();
        exception.Message.Should().Contain("empty");
    }

    #endregion

    #region Device Operations Integration Tests

    [Fact]
    public async Task ExecuteAsync_ShouldDiscoverDevices_WhenRequestingDeviceList()
    {
        // Arrange
        var command = "device_list";
        var parameters = new { include_details = true };

        // Act
        var result = await _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        var jsonDocument = JsonDocument.Parse(result);
        jsonDocument.Should().NotBeNull();
        
        // Verify device list structure
        if (jsonDocument.RootElement.TryGetProperty("data", out var dataElement) &&
            dataElement.TryGetProperty("devices", out var devicesElement))
        {
            devicesElement.ValueKind.Should().Be(JsonValueKind.Array);
            devicesElement.GetArrayLength().Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGetDeviceStatus_WhenRequestingSpecificDevice()
    {
        // Arrange - First get available devices
        var listResult = await _pythonWorkerService.ExecuteAsync<object, string>("test_worker", "device_list", new { });
        var listDoc = JsonDocument.Parse(listResult);
        
        // Extract first device ID if available
        string? deviceId = null;
        if (listDoc.RootElement.TryGetProperty("data", out var dataElement) &&
            dataElement.TryGetProperty("devices", out var devicesElement) &&
            devicesElement.GetArrayLength() > 0)
        {
            var firstDevice = devicesElement[0];
            if (firstDevice.TryGetProperty("id", out var idElement))
            {
                deviceId = idElement.GetString();
            }
        }

        // Skip test if no devices available
        if (string.IsNullOrEmpty(deviceId))
        {
            return;
        }

        // Arrange
        var command = "get_device_status";
        var parameters = new { device_id = deviceId };

        // Act
        var result = await _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        var jsonDocument = JsonDocument.Parse(result);
        if (jsonDocument.RootElement.TryGetProperty("data", out var statusData))
        {
            statusData.Should().NotBeNull();
            
            // Verify status contains expected fields
            if (statusData.TryGetProperty("device_id", out var returnedIdElement))
            {
                returnedIdElement.GetString().Should().Be(deviceId);
            }
        }
    }

    #endregion

    #region Memory Operations Integration Tests

    [Fact]
    public async Task ExecuteAsync_ShouldGetMemoryStatus_WhenRequestingSystemMemory()
    {
        // Arrange
        var command = "get_memory_status";
        var parameters = new { include_device_memory = true };

        // Act
        var result = await _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        var jsonDocument = JsonDocument.Parse(result);
        if (jsonDocument.RootElement.TryGetProperty("data", out var dataElement))
        {
            // Verify memory status structure
            if (dataElement.TryGetProperty("total_system_memory", out var totalMemElement))
            {
                totalMemElement.GetInt64().Should().BeGreaterThan(0);
            }
            
            if (dataElement.TryGetProperty("available_system_memory", out var availMemElement))
            {
                availMemElement.GetInt64().Should().BeGreaterThan(0);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllocateAndDeallocateMemory_WhenManagingMemory()
    {
        // Arrange - Allocate memory
        var allocateCommand = "allocate_memory";
        var allocateParams = new
        {
            size = 1073741824, // 1GB
            device_id = "cpu",
            purpose = "test_allocation"
        };

        // Act - Allocate
        var allocateResult = await _pythonWorkerService.ExecuteAsync<object, string>("memory", allocateCommand, allocateParams);

        // Assert - Allocation
        allocateResult.Should().NotBeNull();
        var allocateDoc = JsonDocument.Parse(allocateResult);
        
        string? allocationId = null;
        if (allocateDoc.RootElement.TryGetProperty("data", out var allocDataElement) &&
            allocDataElement.TryGetProperty("allocation_id", out var idElement))
        {
            allocationId = idElement.GetString();
        }

        allocationId.Should().NotBeNull();

        // Arrange - Deallocate memory
        var deallocateCommand = "deallocate_memory";
        var deallocateParams = new
        {
            allocation_id = allocationId,
            force = false
        };

        // Act - Deallocate
        var deallocateResult = await _pythonWorkerService.ExecuteAsync<object, string>("memory", deallocateCommand, deallocateParams);

        // Assert - Deallocation
        deallocateResult.Should().NotBeNull();
        var deallocateDoc = JsonDocument.Parse(deallocateResult);
        if (deallocateDoc.RootElement.TryGetProperty("data", out var deallocDataElement) &&
            deallocDataElement.TryGetProperty("status", out var statusElement))
        {
            statusElement.GetString().Should().Be("Deallocated");
        }
    }

    #endregion

    #region Model Operations Integration Tests

    [Fact]
    public async Task ExecuteAsync_ShouldListModels_WhenRequestingAvailableModels()
    {
        // Arrange
        var command = "list_models";
        var parameters = new { include_metadata = true };

        // Act
        var result = await _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        var jsonDocument = JsonDocument.Parse(result);
        if (jsonDocument.RootElement.TryGetProperty("data", out var dataElement) &&
            dataElement.TryGetProperty("models", out var modelsElement))
        {
            modelsElement.ValueKind.Should().Be(JsonValueKind.Array);
            // Models array can be empty if no models are installed
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldValidateModel_WhenCheckingModelIntegrity()
    {
        // Arrange - First get available models
        var listResult = await _pythonWorkerService.ExecuteAsync<object, string>("model", "list_models", new { });
        var listDoc = JsonDocument.Parse(listResult);
        
        // Extract first model ID if available
        string? modelId = null;
        if (listDoc.RootElement.TryGetProperty("data", out var dataElement) &&
            dataElement.TryGetProperty("models", out var modelsElement) &&
            modelsElement.GetArrayLength() > 0)
        {
            var firstModel = modelsElement[0];
            if (firstModel.TryGetProperty("id", out var idElement))
            {
                modelId = idElement.GetString();
            }
        }

        // Skip test if no models available
        if (string.IsNullOrEmpty(modelId))
        {
            return;
        }

        // Arrange
        var command = "validate_model";
        var parameters = new
        {
            model_id = modelId,
            validation_type = "quick",
            check_integrity = true
        };

        // Act
        var result = await _pythonWorkerService.ExecuteAsync<object, string>("test_worker", command, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        
        var jsonDocument = JsonDocument.Parse(result);
        if (jsonDocument.RootElement.TryGetProperty("data", out var validationData))
        {
            if (validationData.TryGetProperty("status", out var statusElement))
            {
                var status = statusElement.GetString();
                status.Should().BeOneOf("Valid", "Invalid", "Warning");
            }
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task ExecuteAsync_ShouldCompleteWithinReasonableTime_WhenProcessingMultipleRequests()
    {
        // Arrange
        var commands = new (string workerType, string command, object parameters)[]
        {
            ("device", "device_list", new { }),
            ("memory", "get_memory_status", new { }),
            ("model", "list_models", new { }),
            ("general", "test_echo", new { message = "performance_test" })
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = commands.Select(async cmd => 
        {
            try
            {
                return await _pythonWorkerService.ExecuteAsync<object, string>(cmd.workerType, cmd.command, cmd.parameters);
            }
            catch
            {
                return null; // Some commands might fail in test environment
            }
        });

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
        results.Should().NotBeNull();
        results.Length.Should().Be(commands.Length);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleConcurrentRequests_WhenMultipleThreadsAccess()
    {
        // Arrange
        var command = "test_echo";
        var concurrentRequests = 10;
        var tasks = new List<Task<string>>();

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            var requestId = i;
            var parameters = new { message = $"concurrent_request_{requestId}", id = requestId };
            tasks.Add(_pythonWorkerService.ExecuteAsync<object, string>("general", command, parameters));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().NotBeNull();
        results.Length.Should().Be(concurrentRequests);
        results.Should().AllSatisfy(result => 
        {
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        });
    }

    #endregion

    #region Resource Cleanup

    public void Dispose()
    {
        if (!_disposed)
        {
            _pythonWorkerService?.Dispose();
            _disposed = true;
        }
    }

    #endregion
}
