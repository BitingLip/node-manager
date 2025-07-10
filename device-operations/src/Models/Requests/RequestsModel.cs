namespace DeviceOperations.Models.Requests;

using DeviceOperations.Models.Common;

/// <summary>
/// Model operation requests
/// </summary>
public static class RequestsModel
{
    /// <summary>
    /// Request to list available models
    /// </summary>
    public class ListModelsRequest
    {
        /// <summary>
        /// Filter by model type
        /// </summary>
        public ModelType? ModelType { get; set; }

        /// <summary>
        /// Filter by model status
        /// </summary>
        public ModelStatus? Status { get; set; }

        /// <summary>
        /// Filter by device compatibility
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Include model details
        /// </summary>
        public bool IncludeDetails { get; set; } = false;

        /// <summary>
        /// Include performance metrics
        /// </summary>
        public bool IncludeMetrics { get; set; } = false;

        /// <summary>
        /// Search query for model name/description
        /// </summary>
        public string? SearchQuery { get; set; }

        /// <summary>
        /// Sort by criteria
        /// </summary>
        public ModelSortBy SortBy { get; set; } = ModelSortBy.Name;

        /// <summary>
        /// Sort direction
        /// </summary>
        public SortDirection SortDirection { get; set; } = SortDirection.Ascending;

        /// <summary>
        /// Page number for pagination
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Items per page
        /// </summary>
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Request to get model information by ID
    /// </summary>
    public class GetModelRequest
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Include detailed metadata
        /// </summary>
        public bool IncludeMetadata { get; set; } = true;

        /// <summary>
        /// Include component information
        /// </summary>
        public bool IncludeComponents { get; set; } = true;

        /// <summary>
        /// Include requirements
        /// </summary>
        public bool IncludeRequirements { get; set; } = true;

        /// <summary>
        /// Include performance metrics
        /// </summary>
        public bool IncludePerformance { get; set; } = false;
    }

    /// <summary>
    /// Request to load a model onto a device
    /// </summary>
    public class LoadModelRequest
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Target device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Load configuration
        /// </summary>
        public ModelLoadConfiguration Configuration { get; set; } = new();

        /// <summary>
        /// Load priority
        /// </summary>
        public LoadPriority Priority { get; set; } = LoadPriority.Normal;

        /// <summary>
        /// Wait for load completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;

        /// <summary>
        /// Load timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Force load even if device memory is limited
        /// </summary>
        public bool Force { get; set; } = false;
    }

    /// <summary>
    /// Request to unload a model from a device
    /// </summary>
    public class UnloadModelRequest
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
        /// Force unload even if model is in use
        /// </summary>
        public bool Force { get; set; } = false;

        /// <summary>
        /// Wait for unload completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;

        /// <summary>
        /// Unload timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Clear model cache after unload
        /// </summary>
        public bool ClearCache { get; set; } = false;
    }

    /// <summary>
    /// Request to validate model compatibility with a device
    /// </summary>
    public class ValidateModelRequest
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
        /// Check memory requirements
        /// </summary>
        public bool CheckMemory { get; set; } = true;

        /// <summary>
        /// Check compute capability
        /// </summary>
        public bool CheckCompute { get; set; } = true;

        /// <summary>
        /// Check driver compatibility
        /// </summary>
        public bool CheckDriver { get; set; } = true;

        /// <summary>
        /// Include performance estimation
        /// </summary>
        public bool EstimatePerformance { get; set; } = false;
    }

    /// <summary>
    /// Request to optimize a model for a specific device
    /// </summary>
    public class OptimizeModelRequest
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Target device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Optimization target
        /// </summary>
        public ModelOptimizationTarget Target { get; set; }

        /// <summary>
        /// Optimization level
        /// </summary>
        public OptimizationLevel Level { get; set; } = OptimizationLevel.Balanced;

        /// <summary>
        /// Custom optimization parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// Save optimized model
        /// </summary>
        public bool SaveOptimized { get; set; } = true;

        /// <summary>
        /// Optimized model name
        /// </summary>
        public string? OptimizedModelName { get; set; }

        /// <summary>
        /// Optimization timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 1800;
    }

    /// <summary>
    /// Request to benchmark a model on a device
    /// </summary>
    public class BenchmarkModelRequest
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
        /// Benchmark configuration
        /// </summary>
        public ModelBenchmarkConfiguration Configuration { get; set; } = new();

        /// <summary>
        /// Number of warmup runs
        /// </summary>
        public int WarmupRuns { get; set; } = 3;

        /// <summary>
        /// Number of benchmark runs
        /// </summary>
        public int BenchmarkRuns { get; set; } = 10;

        /// <summary>
        /// Save detailed results
        /// </summary>
        public bool SaveResults { get; set; } = true;

        /// <summary>
        /// Include memory usage tracking
        /// </summary>
        public bool TrackMemoryUsage { get; set; } = true;
    }

    /// <summary>
    /// Request to get model status on a device
    /// </summary>
    public class GetModelStatusRequest
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
        /// Include memory usage
        /// </summary>
        public bool IncludeMemoryUsage { get; set; } = true;

        /// <summary>
        /// Include performance metrics
        /// </summary>
        public bool IncludePerformance { get; set; } = true;

        /// <summary>
        /// Include active sessions
        /// </summary>
        public bool IncludeSessions { get; set; } = false;
    }

    /// <summary>
    /// Request to search for models
    /// </summary>
    public class SearchModelsRequest
    {
        /// <summary>
        /// Search query
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Search fields
        /// </summary>
        public List<ModelSearchField> SearchFields { get; set; } = new() { ModelSearchField.Name, ModelSearchField.Description };

        /// <summary>
        /// Filter by model type
        /// </summary>
        public List<ModelType> ModelTypes { get; set; } = new();

        /// <summary>
        /// Filter by tags
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Minimum model rating
        /// </summary>
        public double? MinRating { get; set; }

        /// <summary>
        /// Maximum memory requirement in bytes
        /// </summary>
        public long? MaxMemoryBytes { get; set; }

        /// <summary>
        /// Compatible device types
        /// </summary>
        public List<DeviceType> CompatibleDevices { get; set; } = new();

        /// <summary>
        /// Sort by criteria
        /// </summary>
        public ModelSortBy SortBy { get; set; } = ModelSortBy.Relevance;

        /// <summary>
        /// Page number
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Items per page
        /// </summary>
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Request to update model metadata
    /// </summary>
    public class UpdateModelMetadataRequest
    {
        /// <summary>
        /// Model identifier
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Updated metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Updated tags
        /// </summary>
        public List<string>? Tags { get; set; }

        /// <summary>
        /// Updated description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Validate metadata before updating
        /// </summary>
        public bool Validate { get; set; } = true;
    }
}

/// <summary>
/// Model load configuration
/// </summary>
public class ModelLoadConfiguration
{
    /// <summary>
    /// Precision to load model with
    /// </summary>
    public ModelPrecision Precision { get; set; } = ModelPrecision.Auto;

    /// <summary>
    /// Memory optimization settings
    /// </summary>
    public ModelMemoryOptimization MemoryOptimization { get; set; } = new();

    /// <summary>
    /// Performance optimization settings
    /// </summary>
    public ModelPerformanceOptimization PerformanceOptimization { get; set; } = new();

    /// <summary>
    /// Custom parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Model benchmark configuration
/// </summary>
public class ModelBenchmarkConfiguration
{
    /// <summary>
    /// Input resolution for benchmarking
    /// </summary>
    public int InputWidth { get; set; } = 512;

    /// <summary>
    /// Input height for benchmarking
    /// </summary>
    public int InputHeight { get; set; } = 512;

    /// <summary>
    /// Batch size for benchmarking
    /// </summary>
    public int BatchSize { get; set; } = 1;

    /// <summary>
    /// Number of inference steps
    /// </summary>
    public int InferenceSteps { get; set; } = 20;

    /// <summary>
    /// Test various batch sizes
    /// </summary>
    public bool TestBatchSizes { get; set; } = false;

    /// <summary>
    /// Test various resolutions
    /// </summary>
    public bool TestResolutions { get; set; } = false;

    /// <summary>
    /// Custom benchmark parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Model memory optimization settings
/// </summary>
public class ModelMemoryOptimization
{
    /// <summary>
    /// Enable attention slicing
    /// </summary>
    public bool EnableAttentionSlicing { get; set; } = false;

    /// <summary>
    /// Enable CPU offloading
    /// </summary>
    public bool EnableCpuOffloading { get; set; } = false;

    /// <summary>
    /// Enable model splitting
    /// </summary>
    public bool EnableModelSplitting { get; set; } = false;

    /// <summary>
    /// Memory fraction to use
    /// </summary>
    public double MemoryFraction { get; set; } = 1.0;
}

/// <summary>
/// Model performance optimization settings
/// </summary>
public class ModelPerformanceOptimization
{
    /// <summary>
    /// Enable TensorRT optimization
    /// </summary>
    public bool EnableTensorRT { get; set; } = false;

    /// <summary>
    /// Enable ONNX optimization
    /// </summary>
    public bool EnableONNX { get; set; } = false;

    /// <summary>
    /// Enable compilation optimization
    /// </summary>
    public bool EnableCompilation { get; set; } = false;

    /// <summary>
    /// Enable flash attention
    /// </summary>
    public bool EnableFlashAttention { get; set; } = false;
}

/// <summary>
/// Load priority enumeration
/// </summary>
public enum LoadPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Model optimization target enumeration
/// </summary>
public enum ModelOptimizationTarget
{
    Speed = 0,
    Memory = 1,
    Quality = 2,
    Balanced = 3
}

/// <summary>
/// Optimization level enumeration
/// </summary>
public enum OptimizationLevel
{
    None = 0,
    Basic = 1,
    Balanced = 2,
    Aggressive = 3,
    Maximum = 4
}

/// <summary>
/// Model precision enumeration
/// </summary>
public enum ModelPrecision
{
    Auto = 0,
    Float32 = 1,
    Float16 = 2,
    Int8 = 3,
    Int4 = 4
}

/// <summary>
/// Model sort criteria enumeration
/// </summary>
public enum ModelSortBy
{
    Name = 0,
    Type = 1,
    Size = 2,
    Rating = 3,
    CreatedDate = 4,
    LastUsed = 5,
    Performance = 6,
    Relevance = 7
}

/// <summary>
/// Model search field enumeration
/// </summary>
public enum ModelSearchField
{
    Name = 0,
    Description = 1,
    Tags = 2,
    Author = 3,
    Version = 4
}

/// <summary>
/// Sort direction enumeration
/// </summary>
public enum SortDirection
{
    Ascending = 0,
    Descending = 1
}
