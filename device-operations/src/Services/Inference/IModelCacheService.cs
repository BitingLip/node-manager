using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Inference;

/// <summary>
/// Service for managing models in shared RAM cache for fast GPU loading
/// </summary>
public interface IModelCacheService
{
    /// <summary>
    /// Initialize the model cache service
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Load a model into shared RAM cache
    /// </summary>
    /// <param name="request">Model cache request</param>
    Task<ModelCacheResponse> CacheModelAsync(CacheModelRequest request);

    /// <summary>
    /// Remove a model from shared RAM cache
    /// </summary>
    /// <param name="modelId">Model ID to remove</param>
    Task<bool> UncacheModelAsync(string modelId);

    /// <summary>
    /// Check if a model is available in cache
    /// </summary>
    /// <param name="modelId">Model ID to check</param>
    Task<bool> IsModelCachedAsync(string modelId);

    /// <summary>
    /// Get information about a cached model
    /// </summary>
    /// <param name="modelId">Model ID to query</param>
    Task<CachedModelInfo?> GetCachedModelInfoAsync(string modelId);

    /// <summary>
    /// Get all cached models
    /// </summary>
    Task<ModelCacheStatusResponse> GetCacheStatusAsync();

    /// <summary>
    /// Load a model from cache to a specific GPU
    /// </summary>
    /// <param name="modelId">Cached model ID</param>
    /// <param name="gpuId">Target GPU ID</param>
    Task<LoadModelResponse> LoadCachedModelToGpuAsync(string modelId, string gpuId);

    /// <summary>
    /// Clean up unused cache entries
    /// </summary>
    Task CleanupCacheAsync();

    /// <summary>
    /// Get memory usage statistics for the cache
    /// </summary>
    Task<CacheMemoryStats> GetMemoryStatsAsync();

    /// <summary>
    /// Cache an SDXL model suite with base, refiner, and VAE components
    /// </summary>
    /// <param name="request">SDXL model suite cache request</param>
    Task<SDXLModelSuiteCacheResponse> CacheSDXLModelSuiteAsync(CacheSDXLModelSuiteRequest request);

    /// <summary>
    /// Get information about cached SDXL model suites
    /// </summary>
    Task<List<SDXLModelSuiteInfo>> GetCachedSDXLModelSuitesAsync();

    /// <summary>
    /// Remove an entire SDXL model suite from cache (base, refiner, VAE)
    /// </summary>
    /// <param name="suiteName">Name of the SDXL model suite</param>
    Task<bool> UncacheSDXLModelSuiteAsync(string suiteName);

    /// <summary>
    /// Preload common SDXL models for fast deployment
    /// </summary>
    Task<PreloadCommonModelsResponse> PreloadCommonSDXLModelsAsync();

    /// <summary>
    /// Validate model files and get detailed information before caching
    /// </summary>
    /// <param name="modelPaths">List of model file paths to validate</param>
    Task<List<ModelValidationResult>> ValidateModelsAsync(List<string> modelPaths);

    /// <summary>
    /// Get cache efficiency metrics for optimization
    /// </summary>
    Task<CacheEfficiencyMetrics> GetCacheEfficiencyMetricsAsync();
}

/// <summary>
/// Request for caching a model in RAM
/// </summary>
public class CacheModelRequest
{
    public string ModelPath { get; set; } = string.Empty;
    public string? ModelId { get; set; }
    public string? ModelName { get; set; }
    public ModelType ModelType { get; set; } = ModelType.SDXL;
    public bool ForceReload { get; set; } = false;
}

/// <summary>
/// Response for model caching operations
/// </summary>
public class ModelCacheResponse
{
    public bool Success { get; set; }
    public string? ModelId { get; set; }
    public string? Message { get; set; }
    public long ModelSizeBytes { get; set; }
    public double LoadTimeSeconds { get; set; }
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// Information about a cached model
/// </summary>
public class CachedModelInfo
{
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ModelPath { get; set; } = string.Empty;
    public ModelType ModelType { get; set; }
    public long ModelSizeBytes { get; set; }
    public DateTime CachedAt { get; set; }
    public DateTime LastUsed { get; set; }
    public int UseCount { get; set; }
    public List<string> LoadedOnGpus { get; set; } = new();
}

/// <summary>
/// Status of the model cache
/// </summary>
public class ModelCacheStatusResponse
{
    public List<CachedModelInfo> CachedModels { get; set; } = new();
    public int TotalCachedModels { get; set; }
    public long TotalCacheMemoryBytes { get; set; }
    public long AvailableMemoryBytes { get; set; }
    public double CacheHitRatio { get; set; }
}

/// <summary>
/// Memory statistics for the cache
/// </summary>
public class CacheMemoryStats
{
    public long TotalSystemMemoryBytes { get; set; }
    public long UsedCacheMemoryBytes { get; set; }
    public long AvailableMemoryBytes { get; set; }
    public double MemoryUsagePercentage { get; set; }
    public int ActiveCacheEntries { get; set; }
}

/// <summary>
/// Request for caching an SDXL model suite with all components
/// </summary>
public class CacheSDXLModelSuiteRequest
{
    public string SuiteName { get; set; } = string.Empty;
    public string BaseModelPath { get; set; } = string.Empty;
    public string? RefinerModelPath { get; set; }
    public string? VaeModelPath { get; set; }
    public string? ControlNetPath { get; set; }
    public string? LoraPath { get; set; }
    public bool ForceReload { get; set; } = false;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Response for SDXL model suite caching operations
/// </summary>
public class SDXLModelSuiteCacheResponse
{
    public bool Success { get; set; }
    public string SuiteName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public List<ComponentCacheResult> ComponentResults { get; set; } = new();
    public long TotalSizeBytes { get; set; }
    public double TotalLoadTimeSeconds { get; set; }
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// Result for individual component caching
/// </summary>
public class ComponentCacheResult
{
    public string ComponentType { get; set; } = string.Empty; // base, refiner, vae, controlnet, lora
    public string? ModelId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public long SizeBytes { get; set; }
    public double LoadTimeSeconds { get; set; }
}

/// <summary>
/// Information about a cached SDXL model suite
/// </summary>
public class SDXLModelSuiteInfo
{
    public string SuiteName { get; set; } = string.Empty;
    public string BaseModelId { get; set; } = string.Empty;
    public string? RefinerModelId { get; set; }
    public string? VaeModelId { get; set; }
    public string? ControlNetModelId { get; set; }
    public string? LoraModelId { get; set; }
    public long TotalSizeBytes { get; set; }
    public DateTime CachedAt { get; set; }
    public DateTime LastUsed { get; set; }
    public int UseCount { get; set; }
    public List<string> LoadedOnGpus { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Response for preloading common models
/// </summary>
public class PreloadCommonModelsResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<ComponentCacheResult> PreloadResults { get; set; } = new();
    public int TotalAttempted { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public long TotalSizeBytes { get; set; }
}

/// <summary>
/// Result of model file validation
/// </summary>
public class ModelValidationResult
{
    public string ModelPath { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public bool FileExists { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ModelFormat { get; set; } // safetensors, ckpt, onnx, etc.
    public ModelType? DetectedModelType { get; set; }
    public string? ModelName { get; set; }
    public string? Hash { get; set; }
    public Dictionary<string, object> ModelMetadata { get; set; } = new();
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
}

/// <summary>
/// Cache efficiency metrics for optimization
/// </summary>
public class CacheEfficiencyMetrics
{
    public double CacheHitRatio { get; set; }
    public double CacheMissRatio { get; set; }
    public long TotalCacheRequests { get; set; }
    public long TotalCacheHits { get; set; }
    public long TotalCacheMisses { get; set; }
    public double AverageLoadTimeSeconds { get; set; }
    public double MemoryUtilizationPercentage { get; set; }
    public int MostUsedModelCount { get; set; }
    public int UnusedModelCount { get; set; }
    public List<ModelUsageStatistic> TopUsedModels { get; set; } = new();
    public List<ModelUsageStatistic> UnusedModels { get; set; } = new();
    public DateTime MetricsGeneratedAt { get; set; }
}

/// <summary>
/// Model usage statistics
/// </summary>
public class ModelUsageStatistic
{
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int UseCount { get; set; }
    public DateTime LastUsed { get; set; }
    public long SizeBytes { get; set; }
    public double AvgLoadTimeSeconds { get; set; }
}
