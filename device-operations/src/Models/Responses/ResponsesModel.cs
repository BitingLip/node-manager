namespace DeviceOperations.Models.Responses;

using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;

/// <summary>
/// Model operation responses
/// </summary>
public static class ResponsesModel
{
    /// <summary>
    /// Response for listing models
    /// </summary>
    public class ListModelsResponse
    {
        /// <summary>
        /// List of models
        /// </summary>
        public List<ModelInfo> Models { get; set; } = new();

        /// <summary>
        /// Total number of models
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Model type distribution
        /// </summary>
        public Dictionary<ModelType, int> TypeDistribution { get; set; } = new();

        /// <summary>
        /// Status distribution
        /// </summary>
        public Dictionary<ModelStatus, int> StatusDistribution { get; set; } = new();
    }

    /// <summary>
    /// Response for getting model information
    /// </summary>
    public class GetModelResponse
    {
        /// <summary>
        /// Model information
        /// </summary>
        public ModelInfo Model { get; set; } = new();

        /// <summary>
        /// Model compatibility information
        /// </summary>
        public ModelCompatibilityInfo Compatibility { get; set; } = new();

        /// <summary>
        /// Model usage statistics
        /// </summary>
        public ModelUsageStats? UsageStats { get; set; }

        /// <summary>
        /// Current model status on devices
        /// </summary>
        public List<ModelDeviceStatus> DeviceStatus { get; set; } = new();
    }

    /// <summary>
    /// Response for loading a model
    /// </summary>
    public class LoadModelResponse
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Load success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Load time in milliseconds
        /// </summary>
        public double LoadTimeMs { get; set; }

        /// <summary>
        /// Memory usage after loading
        /// </summary>
        public ModelMemoryUsage MemoryUsage { get; set; } = new();

        /// <summary>
        /// Model optimization applied
        /// </summary>
        public List<string> OptimizationsApplied { get; set; } = new();

        /// <summary>
        /// Load warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Load message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Load completion timestamp
        /// </summary>
        public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for unloading a model
    /// </summary>
    public class UnloadModelResponse
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Unload success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Unload time in milliseconds
        /// </summary>
        public double UnloadTimeMs { get; set; }

        /// <summary>
        /// Memory freed in bytes
        /// </summary>
        public long MemoryFreedBytes { get; set; }

        /// <summary>
        /// Cache cleared
        /// </summary>
        public bool CacheCleared { get; set; }

        /// <summary>
        /// Unload message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Unload completion timestamp
        /// </summary>
        public DateTime UnloadedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for validating model compatibility
    /// </summary>
    public class ValidateModelResponse
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Overall compatibility status
        /// </summary>
        public bool IsCompatible { get; set; }

        /// <summary>
        /// Validation results
        /// </summary>
        public ModelValidationResults ValidationResults { get; set; } = new();

        /// <summary>
        /// Compatibility score (0-100)
        /// </summary>
        public double CompatibilityScore { get; set; }

        /// <summary>
        /// Performance estimation
        /// </summary>
        public ModelPerformanceEstimation? PerformanceEstimation { get; set; }

        /// <summary>
        /// Validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Validation recommendations
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Response for optimizing a model
    /// </summary>
    public class OptimizeModelResponse
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Optimization success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Optimization time in milliseconds
        /// </summary>
        public double OptimizationTimeMs { get; set; }

        /// <summary>
        /// Optimization results
        /// </summary>
        public ModelOptimizationResults Results { get; set; } = new();

        /// <summary>
        /// Optimized model information
        /// </summary>
        public ModelInfo? OptimizedModel { get; set; }

        /// <summary>
        /// Optimization message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Optimization completion timestamp
        /// </summary>
        public DateTime OptimizedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for benchmarking a model
    /// </summary>
    public class BenchmarkModelResponse
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Benchmark success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Benchmark results
        /// </summary>
        public ModelBenchmarkResults Results { get; set; } = new();

        /// <summary>
        /// Benchmark duration in seconds
        /// </summary>
        public double BenchmarkDurationSeconds { get; set; }

        /// <summary>
        /// Performance metrics
        /// </summary>
        public DeviceOperations.Models.Common.ModelPerformance PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Memory usage during benchmark
        /// </summary>
        public ModelMemoryUsage MemoryUsage { get; set; } = new();

        /// <summary>
        /// Benchmark message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Benchmark completion timestamp
        /// </summary>
        public DateTime BenchmarkedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for getting model status
    /// </summary>
    public class GetModelStatusResponse
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Current model status
        /// </summary>
        public ModelStatus Status { get; set; }

        /// <summary>
        /// Status description
        /// </summary>
        public string StatusDescription { get; set; } = string.Empty;

        /// <summary>
        /// Model memory usage
        /// </summary>
        public ModelMemoryUsage? MemoryUsage { get; set; }

        /// <summary>
        /// Model performance metrics
        /// </summary>
        public DeviceOperations.Models.Common.ModelPerformance? PerformanceMetrics { get; set; }

        /// <summary>
        /// Active inference sessions
        /// </summary>
        public List<string>? ActiveSessions { get; set; }

        /// <summary>
        /// Model health score (0-100)
        /// </summary>
        public double HealthScore { get; set; }

        /// <summary>
        /// Last status update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for searching models
    /// </summary>
    public class SearchModelsResponse
    {
        /// <summary>
        /// Search results
        /// </summary>
        public List<ModelSearchResult> Results { get; set; } = new();

        /// <summary>
        /// Search query
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Total number of results
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Search execution time in milliseconds
        /// </summary>
        public double SearchTimeMs { get; set; }

        /// <summary>
        /// Search facets
        /// </summary>
        public Dictionary<string, Dictionary<string, int>> Facets { get; set; } = new();
    }

    /// <summary>
    /// Response for updating model metadata
    /// </summary>
    public class UpdateModelMetadataResponse
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Update success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Updated fields
        /// </summary>
        public List<string> UpdatedFields { get; set; } = new();

        /// <summary>
        /// Validation results
        /// </summary>
        public Dictionary<string, string> ValidationResults { get; set; } = new();

        /// <summary>
        /// Updated model information
        /// </summary>
        public ModelInfo? UpdatedModel { get; set; }

        /// <summary>
        /// Update message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Update timestamp
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

/// <summary>
/// Model compatibility information
/// </summary>
public class ModelCompatibilityInfo
{
    /// <summary>
    /// Compatible device types
    /// </summary>
    public List<DeviceType> CompatibleDevices { get; set; } = new();

    /// <summary>
    /// Required compute capability
    /// </summary>
    public string RequiredComputeCapability { get; set; } = string.Empty;

    /// <summary>
    /// Minimum driver version required
    /// </summary>
    public string MinDriverVersion { get; set; } = string.Empty;

    /// <summary>
    /// Supported frameworks
    /// </summary>
    public List<string> SupportedFrameworks { get; set; } = new();

    /// <summary>
    /// Supported optimizations
    /// </summary>
    public List<string> SupportedOptimizations { get; set; } = new();

    /// <summary>
    /// Platform compatibility
    /// </summary>
    public Dictionary<string, bool> PlatformCompatibility { get; set; } = new();
}

/// <summary>
/// Model usage statistics
/// </summary>
public class ModelUsageStats
{
    /// <summary>
    /// Total usage count
    /// </summary>
    public long TotalUsageCount { get; set; }

    /// <summary>
    /// Last used timestamp
    /// </summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>
    /// Average inference time in milliseconds
    /// </summary>
    public double AverageInferenceTime { get; set; }

    /// <summary>
    /// Usage frequency (uses per day)
    /// </summary>
    public double UsageFrequency { get; set; }

    /// <summary>
    /// Popular device types for this model
    /// </summary>
    public Dictionary<DeviceType, int> DeviceUsageDistribution { get; set; } = new();

    /// <summary>
    /// User satisfaction rating (0-5)
    /// </summary>
    public double? UserRating { get; set; }
}

/// <summary>
/// Model device status
/// </summary>
public class ModelDeviceStatus
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Device name
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Model status on this device
    /// </summary>
    public ModelStatus Status { get; set; }

    /// <summary>
    /// Load timestamp
    /// </summary>
    public DateTime? LoadedAt { get; set; }

    /// <summary>
    /// Memory usage on this device
    /// </summary>
    public ModelMemoryUsage? MemoryUsage { get; set; }

    /// <summary>
    /// Active sessions count
    /// </summary>
    public int ActiveSessionsCount { get; set; }
}

/// <summary>
/// Model memory usage
/// </summary>
public class ModelMemoryUsage
{
    /// <summary>
    /// Model memory usage in bytes
    /// </summary>
    public long ModelMemoryBytes { get; set; }

    /// <summary>
    /// Weights memory usage in bytes
    /// </summary>
    public long WeightsMemoryBytes { get; set; }

    /// <summary>
    /// Activations memory usage in bytes
    /// </summary>
    public long ActivationsMemoryBytes { get; set; }

    /// <summary>
    /// Cache memory usage in bytes
    /// </summary>
    public long CacheMemoryBytes { get; set; }

    /// <summary>
    /// Total memory usage in bytes
    /// </summary>
    public long TotalMemoryBytes { get; set; }

    /// <summary>
    /// Peak memory usage in bytes
    /// </summary>
    public long PeakMemoryBytes { get; set; }

    /// <summary>
    /// Memory efficiency score (0-100)
    /// </summary>
    public double MemoryEfficiency { get; set; }
}

/// <summary>
/// Model validation results
/// </summary>
public class ModelValidationResults
{
    /// <summary>
    /// Memory validation result
    /// </summary>
    public ValidationResult MemoryValidation { get; set; } = new();

    /// <summary>
    /// Compute validation result
    /// </summary>
    public ValidationResult ComputeValidation { get; set; } = new();

    /// <summary>
    /// Driver validation result
    /// </summary>
    public ValidationResult DriverValidation { get; set; } = new();

    /// <summary>
    /// Platform validation result
    /// </summary>
    public ValidationResult PlatformValidation { get; set; } = new();

    /// <summary>
    /// Feature validation results
    /// </summary>
    public Dictionary<string, ValidationResult> FeatureValidations { get; set; } = new();
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Validation passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Validation score (0-100)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Validation message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Validation details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Model performance estimation
/// </summary>
public class ModelPerformanceEstimation
{
    /// <summary>
    /// Estimated inference time in milliseconds
    /// </summary>
    public double EstimatedInferenceTime { get; set; }

    /// <summary>
    /// Estimated throughput (operations per second)
    /// </summary>
    public double EstimatedThroughput { get; set; }

    /// <summary>
    /// Estimated memory usage in bytes
    /// </summary>
    public long EstimatedMemoryUsage { get; set; }

    /// <summary>
    /// Estimated power consumption in watts
    /// </summary>
    public double EstimatedPowerConsumption { get; set; }

    /// <summary>
    /// Performance confidence score (0-100)
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Performance category
    /// </summary>
    public string PerformanceCategory { get; set; } = string.Empty;
}

/// <summary>
/// Model optimization results
/// </summary>
public class ModelOptimizationResults
{
    /// <summary>
    /// Optimizations applied
    /// </summary>
    public List<string> OptimizationsApplied { get; set; } = new();

    /// <summary>
    /// Model size before optimization in bytes
    /// </summary>
    public long OriginalSizeBytes { get; set; }

    /// <summary>
    /// Model size after optimization in bytes
    /// </summary>
    public long OptimizedSizeBytes { get; set; }

    /// <summary>
    /// Size reduction percentage
    /// </summary>
    public double SizeReductionPercentage { get; set; }

    /// <summary>
    /// Estimated performance improvement percentage
    /// </summary>
    public double EstimatedPerformanceImprovement { get; set; }

    /// <summary>
    /// Quality impact score (-100 to 100)
    /// </summary>
    public double QualityImpactScore { get; set; }

    /// <summary>
    /// Optimization efficiency score (0-100)
    /// </summary>
    public double OptimizationEfficiency { get; set; }

    /// <summary>
    /// Detailed optimization metrics
    /// </summary>
    public Dictionary<string, double> DetailedMetrics { get; set; } = new();
}

/// <summary>
/// Model benchmark results
/// </summary>
public class ModelBenchmarkResults
{
    /// <summary>
    /// Overall benchmark score
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// Inference time statistics
    /// </summary>
    public InferenceTimeStats InferenceTimeStats { get; set; } = new();

    /// <summary>
    /// Throughput statistics
    /// </summary>
    public ThroughputStats ThroughputStats { get; set; } = new();

    /// <summary>
    /// Quality metrics
    /// </summary>
    public Dictionary<string, double> QualityMetrics { get; set; } = new();

    /// <summary>
    /// Resource utilization metrics
    /// </summary>
    public Dictionary<string, double> ResourceUtilization { get; set; } = new();

    /// <summary>
    /// Benchmark configuration used
    /// </summary>
    public Dictionary<string, object> BenchmarkConfiguration { get; set; } = new();
}

/// <summary>
/// Inference time statistics
/// </summary>
public class InferenceTimeStats
{
    /// <summary>
    /// Average inference time in milliseconds
    /// </summary>
    public double Average { get; set; }

    /// <summary>
    /// Minimum inference time in milliseconds
    /// </summary>
    public double Minimum { get; set; }

    /// <summary>
    /// Maximum inference time in milliseconds
    /// </summary>
    public double Maximum { get; set; }

    /// <summary>
    /// Standard deviation
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    /// 95th percentile
    /// </summary>
    public double Percentile95 { get; set; }

    /// <summary>
    /// 99th percentile
    /// </summary>
    public double Percentile99 { get; set; }
}

/// <summary>
/// Throughput statistics
/// </summary>
public class ThroughputStats
{
    /// <summary>
    /// Average throughput (operations per second)
    /// </summary>
    public double Average { get; set; }

    /// <summary>
    /// Peak throughput
    /// </summary>
    public double Peak { get; set; }

    /// <summary>
    /// Sustained throughput
    /// </summary>
    public double Sustained { get; set; }

    /// <summary>
    /// Throughput efficiency (0-100)
    /// </summary>
    public double Efficiency { get; set; }
}

/// <summary>
/// Model search result
/// </summary>
public class ModelSearchResult
{
    /// <summary>
    /// Model information
    /// </summary>
    public ModelInfo Model { get; set; } = new();

    /// <summary>
    /// Search relevance score (0-100)
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Match highlights
    /// </summary>
    public Dictionary<string, string> Highlights { get; set; } = new();

    /// <summary>
    /// Compatibility with user's devices
    /// </summary>
    public Dictionary<string, bool> DeviceCompatibility { get; set; } = new();
}
