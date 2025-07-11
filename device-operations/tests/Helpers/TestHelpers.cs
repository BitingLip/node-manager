using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using DeviceOperations.Services.Python;
using DeviceOperations.Services.Device;
using DeviceOperations.Services.Memory;
using DeviceOperations.Services.Model;
using DeviceOperations.Services.Inference;
using DeviceOperations.Services.Postprocessing;
using DeviceOperations.Services.Processing;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Responses;
using FluentAssertions;

namespace DeviceOperations.Tests.Helpers;

/// <summary>
/// Test helper utilities for setting up services and configurations
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Create a service collection configured for testing
    /// </summary>
    public static IServiceCollection CreateTestServices()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole().AddDebug());
        
        // Add Python worker service
        services.AddSingleton<IPythonWorkerService, PythonWorkerService>();
        
        // Add all domain services
        services.AddScoped<IServiceDevice, ServiceDevice>();
        services.AddScoped<IServiceMemory, ServiceMemory>();
        services.AddScoped<IServiceModel, ServiceModel>();
        services.AddScoped<IServiceInference, ServiceInference>();
        services.AddScoped<IServicePostprocessing, ServicePostprocessing>();
        services.AddScoped<IServiceProcessing, ServiceProcessing>();
        
        return services;
    }
    
    /// <summary>
    /// Create a service provider configured for testing
    /// </summary>
    public static IServiceProvider CreateTestServiceProvider()
    {
        return CreateTestServices().BuildServiceProvider();
    }
    
    /// <summary>
    /// Generate test data for device information
    /// </summary>
    public static DeviceInfo CreateTestDeviceInfo(string name = "Test Device", string type = "GPU")
    {
        return new DeviceInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Type = type == "GPU" ? DeviceType.GPU : DeviceType.CPU,
            Status = DeviceStatus.Available,
            IsAvailable = true,
            DriverVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Generate test data for model information
    /// </summary>
    public static ModelInfo CreateTestModelInfo(string name = "Test Model", string type = "TextToImage")
    {
        return new ModelInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Type = type == "TextToImage" ? ModelType.SD15 : ModelType.Unknown,
            Version = "1.0",
            Status = ModelStatus.Available,
            FileSize = 1073741824, // 1GB
            Metadata = new ModelMetadata
            {
                CreatedDate = DateTime.UtcNow.AddDays(-15)
            },
            LastUpdated = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Generate test data for memory allocation
    /// </summary>
    public static MemoryAllocation CreateTestMemoryAllocation(long size = 1073741824, string purpose = "Testing")
    {
        return new MemoryAllocation
        {
            Id = Guid.NewGuid().ToString(),
            Size = size,
            Purpose = purpose,
            CreatedAt = DateTime.UtcNow,
            Status = MemoryAllocationStatus.Active
        };
    }
    
    /// <summary>
    /// Generate test device status
    /// </summary>
    public static DeviceStatus CreateTestDeviceStatus(string status = "Available")
    {
        // DeviceStatus is an enum, return the appropriate enum value
        return status == "Available" ? DeviceStatus.Available : DeviceStatus.Unknown;
    }
    
    /// <summary>
    /// Generate test inference session
    /// </summary>
    public static InferenceSession CreateTestInferenceSession(Guid? modelId = null, Guid? deviceId = null)
    {
        return new InferenceSession
        {
            Id = Guid.NewGuid().ToString(),
            ModelId = (modelId ?? Guid.NewGuid()).ToString(),
            DeviceId = (deviceId ?? Guid.NewGuid()).ToString(),
            Status = SessionStatus.Running,
            Progress = new SessionProgress { CurrentStep = 5, TotalSteps = 10 },
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            LastUpdated = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Wait for a condition to be true with timeout
    /// </summary>
    public static async Task<bool> WaitForConditionAsync(Func<Task<bool>> condition, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        var pollTime = pollInterval ?? TimeSpan.FromMilliseconds(100);
        var endTime = DateTime.UtcNow + timeout;
        
        while (DateTime.UtcNow < endTime)
        {
            if (await condition())
            {
                return true;
            }
            
            await Task.Delay(pollTime);
        }
        
        return false;
    }
    
    /// <summary>
    /// Wait for a condition to be true with timeout (synchronous version)
    /// </summary>
    public static async Task<bool> WaitForConditionAsync(Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        var pollTime = pollInterval ?? TimeSpan.FromMilliseconds(100);
        var endTime = DateTime.UtcNow + timeout;
        
        while (DateTime.UtcNow < endTime)
        {
            if (condition())
            {
                return true;
            }
            
            await Task.Delay(pollTime);
        }
        
        return false;
    }
    
    /// <summary>
    /// Create a test JSON response for Python worker
    /// </summary>
    public static string CreateTestPythonResponse<T>(T data, bool success = true, string? message = null)
    {
        var response = new
        {
            success = success,
            data = data,
            message = message ?? (success ? "Success" : "Error")
        };
        
        return System.Text.Json.JsonSerializer.Serialize(response);
    }
    
    /// <summary>
    /// Create test error response for Python worker
    /// </summary>
    public static string CreateTestPythonErrorResponse(string error, string? details = null)
    {
        var response = new
        {
            success = false,
            error = error,
            details = details
        };
        
        return System.Text.Json.JsonSerializer.Serialize(response);
    }
    
    /// <summary>
    /// Verify API response structure and content
    /// </summary>
    public static void VerifyApiResponse<T>(ApiResponse<T> response, bool expectedSuccess = true)
    {
        response.Should().NotBeNull();
        response.Success.Should().Be(expectedSuccess);
        
        if (expectedSuccess)
        {
            response.Data.Should().NotBeNull();
            response.Message.Should().NotBeNullOrEmpty();
            response.Error.Should().BeNull();
        }
        else
        {
            response.Error.Should().NotBeNull();
            response.Error!.Message.Should().NotBeNullOrEmpty();
        }
    }
    
    /// <summary>
    /// Create test configuration for performance testing
    /// </summary>
    public static Dictionary<string, object> CreatePerformanceTestConfig()
    {
        return new Dictionary<string, object>
        {
            { "MaxConcurrentRequests", 10 },
            { "RequestTimeoutSeconds", 30 },
            { "MemoryLimitBytes", 17179869184 }, // 16GB
            { "MaxInferenceTime", TimeSpan.FromMinutes(5) },
            { "EnablePerformanceMetrics", true },
            { "LogLevel", "Information" }
        };
    }
    
    /// <summary>
    /// Clean up test resources
    /// </summary>
    public static async Task CleanupTestResourcesAsync(IServiceProvider serviceProvider)
    {
        var memoryService = serviceProvider.GetService<IServiceMemory>();
        var modelService = serviceProvider.GetService<IServiceModel>();
        
        if (memoryService != null)
        {
            try
            {
                // Get memory status and clean up any test allocations
                var memoryStatus = await memoryService.GetMemoryStatusAsync();
                if (memoryStatus.Success && memoryStatus.Data?.MemoryStatus != null)
                {
                    // Memory cleanup - just verify system is responsive
                    // DeviceMemories property doesn't exist in GetMemoryStatusResponse
                    // Using MemoryStatus dictionary instead
                    var hasMemoryData = memoryStatus.Data.MemoryStatus.Count > 0;
                    hasMemoryData.Should().BeTrue();
                }
            }
            catch
            {
                // Cleanup is best effort - don't fail tests if cleanup fails
            }
        }
        
        if (modelService != null)
        {
            try
            {
                // Verify models are in a clean state
                var modelsStatus = await modelService.GetModelsAsync();
                modelsStatus.Success.Should().BeTrue();
            }
            catch
            {
                // Cleanup is best effort - don't fail tests if cleanup fails
            }
        }
    }
}
