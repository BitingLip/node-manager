using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Models.Common;

namespace DeviceOperations.Services.SDXL;

/// <summary>
/// Enhanced SDXL service providing advanced generation capabilities
/// </summary>
public interface IEnhancedSDXLService
{
    /// <summary>
    /// Generate images using enhanced SDXL pipeline with full control
    /// </summary>
    /// <param name="request">Enhanced SDXL generation request</param>
    /// <returns>Generated images with detailed metrics</returns>
    Task<EnhancedSDXLResponse> GenerateEnhancedSDXLAsync(EnhancedSDXLRequest request);
    
    /// <summary>
    /// Validate SDXL request and provide detailed feedback
    /// </summary>
    /// <param name="request">SDXL request to validate</param>
    /// <returns>Validation results with warnings and recommendations</returns>
    Task<PromptValidationResponse> ValidateSDXLRequestAsync(EnhancedSDXLRequest request);
    
    /// <summary>
    /// Get available SDXL schedulers with detailed information
    /// </summary>
    /// <returns>List of supported schedulers with metadata</returns>
    Task<SDXLSchedulersResponse> GetAvailableSchedulersAsync();
    
    /// <summary>
    /// Get available ControlNet types and configurations
    /// </summary>
    /// <returns>List of supported ControlNet types</returns>
    Task<ControlNetTypesResponse> GetControlNetTypesAsync();
    
    /// <summary>
    /// Get system capabilities for SDXL generation
    /// </summary>
    /// <returns>System capabilities and available resources</returns>
    Task<SDXLCapabilitiesResponse> GetSDXLCapabilitiesAsync();
    
    /// <summary>
    /// Get performance estimates for an SDXL request
    /// </summary>
    /// <param name="request">SDXL request to estimate</param>
    /// <returns>Performance estimates including time and memory usage</returns>
    Task<SDXLPerformanceEstimate> GetPerformanceEstimateAsync(EnhancedSDXLRequest request);
    
    /// <summary>
    /// Optimize SDXL request for better performance
    /// </summary>
    /// <param name="request">Original SDXL request</param>
    /// <param name="optimization">Optimization preferences</param>
    /// <returns>Optimized request with performance improvements</returns>
    Task<SDXLOptimizationResult> OptimizeSDXLRequestAsync(EnhancedSDXLRequest request, SDXLOptimizationPreferences optimization);
    
    /// <summary>
    /// Get SDXL model information and compatibility
    /// </summary>
    /// <returns>Available SDXL models and their capabilities</returns>
    Task<SDXLModelsResponse> GetAvailableSDXLModelsAsync();
    
    /// <summary>
    /// Analyze image for img2img or inpainting workflows
    /// </summary>
    /// <param name="imagePath">Path to image to analyze</param>
    /// <returns>Image analysis results</returns>
    Task<ImageAnalysisResult> AnalyzeImageAsync(string imagePath);
    
    /// <summary>
    /// Generate ControlNet conditioning data from image
    /// </summary>
    /// <param name="request">ControlNet preprocessing request</param>
    /// <returns>Generated conditioning data</returns>
    Task<ControlNetPreprocessResult> GenerateControlNetConditioningAsync(ControlNetPreprocessRequest request);
    
    /// <summary>
    /// Batch generate multiple SDXL images with queue management
    /// </summary>
    /// <param name="requests">List of SDXL generation requests</param>
    /// <returns>Batch generation response with queue status</returns>
    Task<BatchSDXLResponse> BatchGenerateSDXLAsync(List<EnhancedSDXLRequest> requests);
    
    /// <summary>
    /// Get generation queue status and management
    /// </summary>
    /// <returns>Current queue status and statistics</returns>
    Task<SDXLQueueStatus> GetGenerationQueueStatusAsync();
}

// Supporting response models for enhanced SDXL service
public class SDXLSchedulersResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<SchedulerInfo> Schedulers { get; set; } = new();
    public string RecommendedScheduler { get; set; } = "";
    public Dictionary<string, int> DefaultSteps { get; set; } = new();
}

public class SchedulerInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int RecommendedSteps { get; set; }
    public double QualityScore { get; set; }
    public double SpeedScore { get; set; }
    public List<string> BestFor { get; set; } = new();
}

public class ControlNetTypesResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<ControlNetTypeInfo> Types { get; set; } = new();
    public Dictionary<string, double> RecommendedWeights { get; set; } = new();
}

public class ControlNetTypeInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double RecommendedWeight { get; set; }
    public List<string> SupportedFormats { get; set; } = new();
    public string PreprocessorModel { get; set; } = "";
}

public class SDXLCapabilitiesResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool SDXLSupport { get; set; }
    public Dictionary<string, bool> Features { get; set; } = new();
    public List<string> SupportedSchedulers { get; set; } = new();
    public List<string> SupportedFormats { get; set; } = new();
    public int MaxResolution { get; set; }
    public int MaxBatchSize { get; set; }
    public List<string> MemoryOptimizations { get; set; } = new();
    public List<string> PostprocessingOptions { get; set; } = new();
    public List<LoadedModelInfo> LoadedModels { get; set; } = new();
    public SystemResourceInfo Resources { get; set; } = new();
}

public class LoadedModelInfo
{
    public string ModelId { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public DateTime LoadedAt { get; set; }
    public long MemoryUsage { get; set; }
    public string ModelType { get; set; } = "";
}

public class SystemResourceInfo
{
    public long TotalVRAM { get; set; }
    public long AvailableVRAM { get; set; }
    public double GPUUtilization { get; set; }
    public int AvailableGPUs { get; set; }
    public List<string> GPUNames { get; set; } = new();
}

public class SDXLPerformanceEstimate
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int EstimatedVRAMUsageMB { get; set; }
    public double EstimatedTimeSeconds { get; set; }
    public double EstimatedCostCredits { get; set; }
    public List<string> PerformanceRecommendations { get; set; } = new();
    public Dictionary<string, object> DetailedBreakdown { get; set; } = new();
}

public class SDXLOptimizationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public EnhancedSDXLRequest OptimizedRequest { get; set; } = new();
    public List<string> OptimizationsApplied { get; set; } = new();
    public SDXLPerformanceEstimate PerformanceImprovement { get; set; } = new();
    public double ConfidenceScore { get; set; }
}

public class SDXLOptimizationPreferences
{
    public string Priority { get; set; } = "balanced"; // "speed", "quality", "memory", "balanced"
    public int? TargetVRAMUsageMB { get; set; }
    public double? TargetTimeSeconds { get; set; }
    public bool PreserveQuality { get; set; } = true;
    public Dictionary<string, object> CustomConstraints { get; set; } = new();
}

public class SDXLModelsResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<SDXLModelInfo> Models { get; set; } = new();
    public string RecommendedModel { get; set; } = "";
}

public class SDXLModelInfo
{
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public List<string> SupportedFeatures { get; set; } = new();
    public int RecommendedVRAM { get; set; }
    public string LicenseType { get; set; } = "";
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ImageAnalysisResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string ImagePath { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public double AspectRatio { get; set; }
    public List<string> DetectedObjects { get; set; } = new();
    public Dictionary<string, double> ColorAnalysis { get; set; } = new();
    public Dictionary<string, object> TechnicalMetadata { get; set; } = new();
}

public class ControlNetPreprocessRequest
{
    public string ImagePath { get; set; } = "";
    public string ControlNetType { get; set; } = "";
    public Dictionary<string, object> PreprocessingOptions { get; set; } = new();
}

public class ControlNetPreprocessResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string ConditioningImagePath { get; set; } = "";
    public string ControlNetType { get; set; } = "";
    public Dictionary<string, object> PreprocessingMetadata { get; set; } = new();
}

public class BatchSDXLResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string BatchId { get; set; } = "";
    public int TotalRequests { get; set; }
    public int QueuedRequests { get; set; }
    public List<string> RequestIds { get; set; } = new();
    public DateTime QueuedAt { get; set; }
    public DateTime EstimatedCompletionTime { get; set; }
}

public class SDXLQueueStatus
{
    public int TotalQueued { get; set; }
    public int CurrentlyProcessing { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public double AverageProcessingTime { get; set; }
    public DateTime? EstimatedQueueClearTime { get; set; }
    public List<QueuedRequestInfo> QueuedRequests { get; set; } = new();
}

public class QueuedRequestInfo
{
    public string RequestId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EstimatedCompletion { get; set; }
    public int QueuePosition { get; set; }
}
