using DeviceOperations.Services.Core;
using DeviceOperations.Services.Memory;
using DeviceOperations.Services.Inference;
using DeviceOperations.Services.Training;
using DeviceOperations.Services.Integration;
using DeviceOperations.Services.Workers;
using DeviceOperations.Services.SDXL;
using DeviceOperations.Services.Testing;
using DeviceOperations.Services.Interfaces;
using DeviceOperations.Services.Enhanced;

namespace DeviceOperations.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeviceOperationsServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Core services
        services.AddSingleton<IDeviceService, DirectMLService>();
        
        // Memory services
        services.AddSingleton<IMemoryOperationsService, MemoryOperationsService>();
        
        // GPU Pool and Model Cache services
        services.AddSingleton<IModelCacheService, ModelCacheService>();
        services.AddSingleton<IGpuPoolService, GpuPoolService>();
        
        // Enhanced Protocol Transformation services (Phase 1 Critical Fix)
        services.AddSingleton<IEnhancedRequestTransformer, EnhancedRequestTransformer>();
        services.AddSingleton<IEnhancedResponseHandler, EnhancedResponseHandler>();
        services.AddSingleton<WorkerTypeResolver>();
        
        // Enhanced Worker services
        services.AddSingleton<IPyTorchWorkerService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<PyTorchWorkerService>>();
            var requestTransformer = provider.GetService<IEnhancedRequestTransformer>();
            var responseHandler = provider.GetService<IEnhancedResponseHandler>();
            return new PyTorchWorkerService("gpu_0", logger, requestTransformer, responseHandler);
        });
        
        // Inference services - using direct communication instead of HTTP bridge
        services.AddSingleton<PyTorchDirectMLService>(); // Keep for compatibility
        services.AddSingleton<DirectMLWorkerService>(); // New direct communication service
        services.AddSingleton<IInferenceService, InferenceService>(); // Use direct communication service
        
        // Training services
        services.AddSingleton<ITrainingService, TrainingService>();
        
        // Enhanced SDXL services
        services.AddSingleton<IEnhancedSDXLService, EnhancedSDXLService>();
        
        // Testing services
        services.AddSingleton<ITestingService, TestingService>();
        
        // Integration services (Phase 5) - Commented out temporarily due to build issues
        // services.AddSingleton<IDeviceMonitorIntegrationService, DeviceMonitorIntegrationService>();
        
        // Initialize services on startup
        services.AddHostedService<DirectMLInitializationService>();
        
        // Add configuration
        services.Configure<DeviceOperationsOptions>(configuration.GetSection("DeviceOperations"));
        
        return services;
    }
}

public class DeviceOperationsOptions
{
    public int MaxMemoryAllocationGB { get; set; } = 24;
    public string DefaultDevice { get; set; } = "gpu_0";
    public string ModelCachePath { get; set; } = "./models";
    public bool EnableGPUScheduling { get; set; } = true;
    public InferenceOptions Inference { get; set; } = new();
    public TrainingOptions Training { get; set; } = new();
}

public class InferenceOptions
{
    public int MaxConcurrentSessions { get; set; } = 5;
    public int SessionTimeoutMinutes { get; set; } = 30;
}

public class TrainingOptions
{
    public string CheckpointPath { get; set; } = "./checkpoints";
    public int MaxBatchSize { get; set; } = 32;
}

// Background service to initialize DirectML on startup
public class DirectMLInitializationService : BackgroundService
{
    private readonly IDeviceService _deviceService;
    private readonly IMemoryOperationsService _memoryService;
    private readonly IInferenceService _inferenceService;
    private readonly ITrainingService _trainingService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DirectMLInitializationService> _logger;
    private volatile bool _initialized = false;

    public DirectMLInitializationService(
        IDeviceService deviceService, 
        IMemoryOperationsService memoryService,
        IInferenceService inferenceService,
        ITrainingService trainingService,
        IServiceProvider serviceProvider,
        ILogger<DirectMLInitializationService> logger)
    {
        _deviceService = deviceService;
        _memoryService = memoryService;
        _inferenceService = inferenceService;
        _trainingService = trainingService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_initialized)
        {
            // Just wait indefinitely to keep the service running
            try
            {
                await Task.Delay(-1, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when the service is stopped
            }
            return;
        }

        try
        {
            _logger.LogInformation("Initializing DirectML devices on startup...");
            await _deviceService.InitializeAsync();
            var devices = await _deviceService.GetAvailableDevicesAsync();
            _logger.LogInformation($"Found {devices.Count()} available devices");

            _logger.LogInformation("Initializing Memory Operations Service...");
            await _memoryService.InitializeAsync();
            _logger.LogInformation("Memory Operations Service initialized successfully");

            _logger.LogInformation("Initializing Inference Service...");
            await _inferenceService.InitializeAsync();
            _logger.LogInformation("Inference Service initialized successfully");

            _logger.LogInformation("Initializing Training Service...");
            // Training service doesn't need explicit initialization, it's ready on construction
            _logger.LogInformation("Training Service initialized successfully");

            _logger.LogInformation("Initializing GPU Pool Service...");
            var gpuPoolService = _serviceProvider.GetRequiredService<IGpuPoolService>();
            await gpuPoolService.InitializeAsync();
            _logger.LogInformation("GPU Pool Service initialized successfully");

            _logger.LogInformation("Initializing Model Cache Service...");
            var modelCacheService = _serviceProvider.GetRequiredService<IModelCacheService>();
            await modelCacheService.InitializeAsync();
            _logger.LogInformation("Model Cache Service initialized successfully");
            
            _logger.LogInformation("All services initialized successfully - GPU Pool Management ready!");
            
            _initialized = true;
            
            // Keep the service running but don't initialize again
            try
            {
                await Task.Delay(-1, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when the service is stopped
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize services");
            throw;
        }
    }
}
