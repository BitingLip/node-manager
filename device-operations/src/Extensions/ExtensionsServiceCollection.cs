using DeviceOperations.Services.Device;
using DeviceOperations.Services.Memory;
using DeviceOperations.Services.Model;
using DeviceOperations.Services.Inference;
using DeviceOperations.Services.Postprocessing;
using DeviceOperations.Services.Processing;
using DeviceOperations.Services.Environment;
using DeviceOperations.Services.Python;
using DeviceOperations.Services;
using DeviceOperations.Models.Configuration;

namespace DeviceOperations.Extensions;

/// <summary>
/// Extension methods for service collection registration and dependency injection configuration
/// </summary>
public static class ExtensionsServiceCollection
{
    /// <summary>
    /// Register all application services for dependency injection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection RegisterApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Python worker communication services
        services.AddSingleton<IPythonWorkerService, PythonWorkerService>();
        services.AddSingleton<IOptimizedPythonWorkerService, OptimizedPythonWorkerService>();
        
        // Register field transformation service for inference
        services.AddScoped<InferenceFieldTransformer>();
        
        // Register environment service
        services.AddScoped<IServiceEnvironment, ServiceEnvironment>();
        
        // Register core domain services
        services.AddScoped<IServiceDevice, ServiceDevice>();
        services.AddScoped<IServiceMemory, ServiceMemory>();
        services.AddScoped<IServiceModel, ServiceModel>();
        services.AddScoped<IServiceInference, ServiceInference>();
        services.AddScoped<IServicePostprocessing, ServicePostprocessing>();
        services.AddScoped<IServiceProcessing, ServiceProcessing>();

        // Configure application settings
        services.Configure<PythonWorkerConfiguration>(configuration.GetSection("PythonWorkers"));
        services.Configure<DeviceConfiguration>(configuration.GetSection("Devices"));
        services.Configure<ModelConfiguration>(configuration.GetSection("Models"));
        services.Configure<InferenceServiceOptions>(configuration.GetSection(InferenceServiceOptions.ConfigurationKey));

        return services;
    }
}

/// <summary>
/// Configuration model for Python worker settings
/// </summary>
public class PythonWorkerConfiguration
{
    public string PythonExecutable { get; set; } = "python";
    public string WorkerScriptPath { get; set; } = "src/Workers/main.py";
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxWorkerProcesses { get; set; } = 4;
    public bool EnableLogging { get; set; } = true;
}

/// <summary>
/// Configuration model for device settings
/// </summary>
public class DeviceConfiguration
{
    public bool EnableDirectML { get; set; } = true;
    public bool EnableCUDA { get; set; } = true;
    public int MaxConcurrentOperations { get; set; } = 2;
    public bool AutoOptimizeMemory { get; set; } = true;
}

/// <summary>
/// Configuration model for model settings
/// </summary>
public class ModelConfiguration
{
    public string ModelDirectory { get; set; } = "../models";
    public string CacheDirectory { get; set; } = "cache";
    public int MaxCachedModels { get; set; } = 3;
    public bool EnableRAMCaching { get; set; } = true;
}
