using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Inference;

/// <summary>
/// Service for managing a pool of GPU workers for inference operations
/// </summary>
public interface IGpuPoolService
{
    /// <summary>
    /// Initialize the GPU pool with available devices
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Load a model to a specific GPU's VRAM
    /// </summary>
    /// <param name="gpuId">Target GPU ID (e.g., "gpu_0")</param>
    /// <param name="request">Model loading request</param>
    Task<LoadModelResponse> LoadModelToGpuAsync(string gpuId, LoadModelRequest request);

    /// <summary>
    /// Unload model from a specific GPU's VRAM
    /// </summary>
    /// <param name="gpuId">Target GPU ID</param>
    /// <param name="modelId">Model ID to unload</param>
    Task<bool> UnloadModelFromGpuAsync(string gpuId, string? modelId = null);

    /// <summary>
    /// Get the status of all GPUs in the pool
    /// </summary>
    Task<GpuPoolStatusResponse> GetPoolStatusAsync();

    /// <summary>
    /// Get the status of a specific GPU
    /// </summary>
    /// <param name="gpuId">GPU ID to query</param>
    Task<GpuStatusResponse?> GetGpuStatusAsync(string gpuId);

    /// <summary>
    /// Run inference on a specific GPU
    /// </summary>
    /// <param name="gpuId">Target GPU ID</param>
    /// <param name="request">Inference request</param>
    Task<InferenceResponse> RunInferenceOnGpuAsync(string gpuId, InferenceRequest request);

    /// <summary>
    /// Find the best available GPU for a model type (load balancing)
    /// </summary>
    /// <param name="modelType">Type of model to load</param>
    Task<string?> FindBestAvailableGpuAsync(ModelType modelType);

    /// <summary>
    /// Get all GPUs that have a specific model loaded
    /// </summary>
    /// <param name="modelId">Model ID to search for</param>
    Task<IEnumerable<string>> GetGpusWithModelAsync(string modelId);

    /// <summary>
    /// Clean up memory on a specific GPU without unloading models
    /// </summary>
    /// <param name="gpuId">Target GPU ID</param>
    Task<bool> CleanupGpuMemoryAsync(string gpuId);

    /// <summary>
    /// Get SDXL-specific capabilities and readiness for a GPU
    /// </summary>
    /// <param name="gpuId">Target GPU ID</param>
    Task<SDXLCapabilitiesResponse> GetSDXLCapabilitiesAsync(string gpuId);

    /// <summary>
    /// Load an SDXL model suite (base + refiner + VAE) to a specific GPU
    /// </summary>
    /// <param name="gpuId">Target GPU ID</param>
    /// <param name="request">SDXL suite loading request</param>
    Task<SDXLSuiteLoadResponse> LoadSDXLModelSuiteAsync(string gpuId, LoadSDXLModelSuiteRequest request);

    /// <summary>
    /// Run enhanced SDXL inference with auto-GPU selection
    /// </summary>
    /// <param name="request">Enhanced SDXL request</param>
    Task<EnhancedInferenceResponse> RunEnhancedSDXLAsync(EnhancedSDXLRequest request);

    /// <summary>
    /// Get readiness status of all GPUs for SDXL workloads
    /// </summary>
    Task<SDXLReadinessResponse> GetSDXLReadinessAsync();

    /// <summary>
    /// Batch load the same model to multiple GPUs
    /// </summary>
    /// <param name="request">Batch loading request</param>
    Task<BatchLoadResponse> BatchLoadModelAsync(BatchLoadModelRequest request);

    /// <summary>
    /// Clean up memory on all GPUs in the pool
    /// </summary>
    Task<BatchOperationResponse> CleanupAllGpuMemoryAsync();

    /// <summary>
    /// Auto-balance workloads across available GPUs
    /// </summary>
    /// <param name="workloadType">Type of workload to balance</param>
    Task<LoadBalancingResponse> AutoBalanceWorkloadsAsync(string workloadType = "sdxl");

    /// <summary>
    /// Get detailed performance metrics for all GPUs
    /// </summary>
    Task<GpuPerformanceMetrics> GetPerformanceMetricsAsync();
}

/// <summary>
/// SDXL-specific capabilities and readiness information for a GPU
/// </summary>
public class SDXLCapabilitiesResponse
{
    public string GpuId { get; set; } = string.Empty;
    public bool SDXLSupported { get; set; }
    public long AvailableMemoryMB { get; set; }
    public int RecommendedBatchSize { get; set; }
    public SDXLSupportedFeatures SupportedFeatures { get; set; } = new();
    public GpuPerformanceProfile PerformanceProfile { get; set; } = new();
    public List<string> OptimalModelFormats { get; set; } = new();
    public Dictionary<string, object> TechnicalSpecs { get; set; } = new();
}

/// <summary>
/// SDXL features supported by a GPU
/// </summary>
public class SDXLSupportedFeatures
{
    public bool Text2Img { get; set; } = true;
    public bool Img2Img { get; set; }
    public bool Inpainting { get; set; }
    public bool ControlNet { get; set; }
    public bool LoRA { get; set; } = true;
    public bool Refiner { get; set; }
    public bool HighResolutionGeneration { get; set; }
    public bool BatchProcessing { get; set; }
}

/// <summary>
/// GPU performance profile for SDXL workloads
/// </summary>
public class GpuPerformanceProfile
{
    public string Profile { get; set; } = string.Empty; // high, medium, balanced, economy
    public int MaxResolution { get; set; }
    public int OptimalSteps { get; set; }
    public int MaxBatchSize { get; set; }
    public double EstimatedSecondsPerImage { get; set; }
    public string Features { get; set; } = string.Empty; // all, most, standard, basic
}

/// <summary>
/// Request for loading SDXL model suite to a GPU
/// </summary>
public class LoadSDXLModelSuiteRequest
{
    public string ModelName { get; set; } = string.Empty;
    public string BaseModelPath { get; set; } = string.Empty;
    public string? RefinerModelPath { get; set; }
    public string? VaeModelPath { get; set; }
    public string? ControlNetPath { get; set; }
    public string? LoraPath { get; set; }
    public bool ForceReload { get; set; } = false;
    public Dictionary<string, object> LoadingOptions { get; set; } = new();
}

/// <summary>
/// Response for SDXL model suite loading operations
/// </summary>
public class SDXLSuiteLoadResponse
{
    public bool Success { get; set; }
    public string GpuId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public List<ComponentLoadResult> ComponentResults { get; set; } = new();
    public long TotalMemoryUsageBytes { get; set; }
    public double TotalLoadTimeSeconds { get; set; }
    public DateTime LoadedAt { get; set; }
}

/// <summary>
/// Result for individual SDXL component loading
/// </summary>
public class ComponentLoadResult
{
    public string ComponentType { get; set; } = string.Empty; // base, refiner, vae, controlnet, lora
    public string? ModelId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public long MemoryUsageBytes { get; set; }
    public double LoadTimeSeconds { get; set; }
}

/// <summary>
/// Enhanced inference response with detailed metrics
/// </summary>
public class EnhancedInferenceResponse
{
    public bool Success { get; set; }
    public string? SessionId { get; set; }
    public string? Message { get; set; }
    public string GpuUsed { get; set; } = string.Empty;
    public List<string>? OutputPaths { get; set; }
    public InferenceMetrics? Metrics { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Inference performance metrics
/// </summary>
public class InferenceMetrics
{
    public double TotalTimeSeconds { get; set; }
    public double LoadTimeSeconds { get; set; }
    public double InferenceTimeSeconds { get; set; }
    public double PostProcessingTimeSeconds { get; set; }
    public long MemoryUsedBytes { get; set; }
    public int StepsCompleted { get; set; }
    public double PerformanceScore { get; set; }
}

/// <summary>
/// SDXL readiness status for all GPUs
/// </summary>
public class SDXLReadinessResponse
{
    public int TotalGpus { get; set; }
    public int SDXLReadyGpus { get; set; }
    public double ReadinessPercent { get; set; }
    public List<GpuReadinessInfo> Gpus { get; set; } = new();
    public OptimalWorkloadDistribution OptimalDistribution { get; set; } = new();
}

/// <summary>
/// Individual GPU readiness information
/// </summary>
public class GpuReadinessInfo
{
    public string GpuId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long AvailableMemoryMB { get; set; }
    public long TotalMemoryMB { get; set; }
    public double MemoryUsagePercent { get; set; }
    public bool SDXLReady { get; set; }
    public int RecommendedMaxBatchSize { get; set; }
    public SDXLSupportedFeatures SupportedFeatures { get; set; } = new();
    public GpuPerformanceProfile PerformanceProfile { get; set; } = new();
    public int CurrentlyLoadedModels { get; set; }
    public bool IsAvailable { get; set; }
}

/// <summary>
/// Optimal workload distribution recommendations
/// </summary>
public class OptimalWorkloadDistribution
{
    public List<WorkloadRecommendation> Recommendations { get; set; } = new();
    public string Strategy { get; set; } = string.Empty; // balanced, performance, memory-optimized
    public double EfficiencyScore { get; set; }
}

/// <summary>
/// Workload recommendation for a specific GPU
/// </summary>
public class WorkloadRecommendation
{
    public string GpuId { get; set; } = string.Empty;
    public string RecommendedWorkload { get; set; } = string.Empty;
    public int RecommendedBatchSize { get; set; }
    public double ExpectedThroughput { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Request for batch loading models to multiple GPUs
/// </summary>
public class BatchLoadModelRequest
{
    public string ModelPath { get; set; } = string.Empty;
    public List<string> GpuIds { get; set; } = new();
    public ModelType ModelType { get; set; } = ModelType.SDXL;
    public string? ModelId { get; set; }
    public string? ModelName { get; set; }
    public bool EnableParallelLoading { get; set; } = true;
    public Dictionary<string, object> LoadingOptions { get; set; } = new();
}

/// <summary>
/// Response for batch model loading operations
/// </summary>
public class BatchLoadResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<GpuLoadResult> Results { get; set; } = new();
    public int TotalRequested { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public double TotalLoadTimeSeconds { get; set; }
}

/// <summary>
/// Individual GPU load result in batch operation
/// </summary>
public class GpuLoadResult
{
    public string GpuId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ModelId { get; set; }
    public string? Message { get; set; }
    public double LoadTimeSeconds { get; set; }
    public long MemoryUsageBytes { get; set; }
}

/// <summary>
/// Response for batch operations (cleanup, unload, etc.)
/// </summary>
public class BatchOperationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<GpuOperationResult> Results { get; set; } = new();
    public int TotalGpus { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public double TotalOperationTimeSeconds { get; set; }
}

/// <summary>
/// Individual GPU operation result
/// </summary>
public class GpuOperationResult
{
    public string GpuId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public double OperationTimeSeconds { get; set; }
    public Dictionary<string, object> OperationData { get; set; } = new();
}

/// <summary>
/// Load balancing response with optimization recommendations
/// </summary>
public class LoadBalancingResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string WorkloadType { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public List<LoadBalancingRecommendation> Recommendations { get; set; } = new();
    public double ExpectedImprovement { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Load balancing recommendation for a specific action
/// </summary>
public class LoadBalancingRecommendation
{
    public string Action { get; set; } = string.Empty; // load, unload, migrate, optimize
    public string GpuId { get; set; } = string.Empty;
    public string? TargetGpuId { get; set; }
    public string? ModelId { get; set; }
    public string Description { get; set; } = string.Empty;
    public double ExpectedBenefit { get; set; }
    public int Priority { get; set; }
}

/// <summary>
/// Comprehensive GPU performance metrics
/// </summary>
public class GpuPerformanceMetrics
{
    public DateTime MetricsGeneratedAt { get; set; }
    public List<GpuMetrics> GpuMetrics { get; set; } = new();
    public PoolPerformanceMetrics PoolMetrics { get; set; } = new();
    public List<PerformanceAlert> Alerts { get; set; } = new();
    public Dictionary<string, object> RawMetrics { get; set; } = new();
}

/// <summary>
/// Performance metrics for an individual GPU
/// </summary>
public class GpuMetrics
{
    public string GpuId { get; set; } = string.Empty;
    public double UtilizationPercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double TemperatureCelsius { get; set; }
    public double PowerUsageWatts { get; set; }
    public int ActiveInferenceSessions { get; set; }
    public double AverageInferenceTimeSeconds { get; set; }
    public int CompletedInferences { get; set; }
    public DateTime LastActivity { get; set; }
    public List<string> LoadedModels { get; set; } = new();
}

/// <summary>
/// Overall pool performance metrics
/// </summary>
public class PoolPerformanceMetrics
{
    public double AverageUtilization { get; set; }
    public double TotalThroughput { get; set; }
    public double AverageLatency { get; set; }
    public int TotalActiveInferences { get; set; }
    public double MemoryEfficiency { get; set; }
    public double PowerEfficiency { get; set; }
    public string BottleneckAnalysis { get; set; } = string.Empty;
}

/// <summary>
/// Performance alert for monitoring
/// </summary>
public class PerformanceAlert
{
    public string Level { get; set; } = string.Empty; // info, warning, error, critical
    public string Message { get; set; } = string.Empty;
    public string GpuId { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double ThresholdValue { get; set; }
    public DateTime DetectedAt { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}
