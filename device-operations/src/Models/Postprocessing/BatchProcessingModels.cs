namespace DeviceOperations.Models.Postprocessing
{
    /// <summary>
    /// Advanced batch processing request for postprocessing operations
    /// </summary>
    public class PostPostprocessingBatchAdvancedRequest
    {
        /// <summary>
        /// Unique batch identifier
        /// </summary>
        public string BatchId { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// List of images to process in batch
        /// </summary>
        public List<BatchImageItem> Images { get; set; } = new();

        /// <summary>
        /// Batch processing operation type
        /// </summary>
        public BatchOperationType OperationType { get; set; } = BatchOperationType.Enhance;

        /// <summary>
        /// Processing parameters applied to all images
        /// </summary>
        public Dictionary<string, object> GlobalParameters { get; set; } = new();

        /// <summary>
        /// Maximum concurrent processing items
        /// </summary>
        public int MaxConcurrency { get; set; } = 4;

        /// <summary>
        /// Memory optimization mode
        /// </summary>
        public BatchMemoryMode MemoryMode { get; set; } = BatchMemoryMode.Balanced;

        /// <summary>
        /// Quality vs speed preference
        /// </summary>
        public BatchProcessingMode ProcessingMode { get; set; } = BatchProcessingMode.Quality;

        /// <summary>
        /// Progress callback frequency in milliseconds
        /// </summary>
        public int ProgressUpdateInterval { get; set; } = 1000;

        /// <summary>
        /// Error handling strategy
        /// </summary>
        public BatchErrorHandling ErrorHandling { get; set; } = BatchErrorHandling.ContinueOnError;

        /// <summary>
        /// Output format settings
        /// </summary>
        public BatchOutputSettings OutputSettings { get; set; } = new();

        /// <summary>
        /// Priority level for batch processing
        /// </summary>
        public BatchPriority Priority { get; set; } = BatchPriority.Normal;
    }

    /// <summary>
    /// Advanced batch processing response
    /// </summary>
    public class PostPostprocessingBatchAdvancedResponse
    {
        /// <summary>
        /// Batch processing success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Batch identifier
        /// </summary>
        public string BatchId { get; set; } = string.Empty;

        /// <summary>
        /// Current batch status
        /// </summary>
        public BatchStatus Status { get; set; } = BatchStatus.Queued;

        /// <summary>
        /// Processed image results
        /// </summary>
        public List<BatchImageResult> Results { get; set; } = new();

        /// <summary>
        /// Overall batch progress (0.0 to 1.0)
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// Processing performance metrics
        /// </summary>
        public BatchPerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Error information for failed items
        /// </summary>
        public List<BatchErrorInfo> Errors { get; set; } = new();

        /// <summary>
        /// Estimated time remaining in milliseconds
        /// </summary>
        public long EstimatedTimeRemainingMs { get; set; }

        /// <summary>
        /// Batch processing statistics
        /// </summary>
        public BatchStatistics Statistics { get; set; } = new();
    }

    /// <summary>
    /// Individual image item for batch processing
    /// </summary>
    public class BatchImageItem
    {
        /// <summary>
        /// Unique item identifier
        /// </summary>
        public string ItemId { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// Image data (base64 encoded)
        /// </summary>
        public string ImageData { get; set; } = string.Empty;

        /// <summary>
        /// Item-specific processing parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// Priority for this specific item
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Metadata for the image item
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Batch processing result for individual image
    /// </summary>
    public class BatchImageResult
    {
        /// <summary>
        /// Original item identifier
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Processing success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Processed image data (base64 encoded)
        /// </summary>
        public string? ProcessedImageData { get; set; }

        /// <summary>
        /// Processing time in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Quality score (0.0 to 1.0)
        /// </summary>
        public double QualityScore { get; set; }

        /// <summary>
        /// Error information if processing failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Processing metadata and metrics
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Batch performance metrics
    /// </summary>
    public class BatchPerformanceMetrics
    {
        /// <summary>
        /// Total processing time in milliseconds
        /// </summary>
        public long TotalProcessingTimeMs { get; set; }

        /// <summary>
        /// Average processing time per image
        /// </summary>
        public double AverageProcessingTimeMs { get; set; }

        /// <summary>
        /// Images processed per second
        /// </summary>
        public double ThroughputImagesPerSecond { get; set; }

        /// <summary>
        /// Peak memory usage in MB
        /// </summary>
        public long PeakMemoryUsageMB { get; set; }

        /// <summary>
        /// Average quality score across all processed images
        /// </summary>
        public double AverageQualityScore { get; set; }

        /// <summary>
        /// Resource utilization metrics
        /// </summary>
        public Dictionary<string, double> ResourceUtilization { get; set; } = new();
    }

    /// <summary>
    /// Batch processing statistics
    /// </summary>
    public class BatchStatistics
    {
        /// <summary>
        /// Total number of items in batch
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Number of successfully processed items
        /// </summary>
        public int SuccessfulItems { get; set; }

        /// <summary>
        /// Number of failed items
        /// </summary>
        public int FailedItems { get; set; }

        /// <summary>
        /// Number of items currently being processed
        /// </summary>
        public int ProcessingItems { get; set; }

        /// <summary>
        /// Number of items in queue
        /// </summary>
        public int QueuedItems { get; set; }

        /// <summary>
        /// Success rate (0.0 to 1.0)
        /// </summary>
        public double SuccessRate => TotalItems > 0 ? (double)SuccessfulItems / TotalItems : 0.0;
    }

    /// <summary>
    /// Batch output settings
    /// </summary>
    public class BatchOutputSettings
    {
        /// <summary>
        /// Output image format
        /// </summary>
        public string Format { get; set; } = "PNG";

        /// <summary>
        /// Compression quality (0.0 to 1.0)
        /// </summary>
        public double Quality { get; set; } = 0.95;

        /// <summary>
        /// Whether to preserve original metadata
        /// </summary>
        public bool PreserveMetadata { get; set; } = true;

        /// <summary>
        /// Custom output parameters
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; } = new();
    }

    /// <summary>
    /// Batch error information
    /// </summary>
    public class BatchErrorInfo
    {
        /// <summary>
        /// Item identifier that failed
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Error code
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Error timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the error is retryable
        /// </summary>
        public bool IsRetryable { get; set; }
    }

    #region Enumerations

    /// <summary>
    /// Batch operation types
    /// </summary>
    public enum BatchOperationType
    {
        Enhance,
        Upscale,
        FaceRestore,
        StyleTransfer,
        BackgroundRemove,
        ColorCorrect,
        NoiseReduce,
        Sharpen,
        Custom
    }

    /// <summary>
    /// Batch processing status
    /// </summary>
    public enum BatchStatus
    {
        Queued,
        Initializing,
        Processing,
        Completing,
        Completed,
        Failed,
        Cancelled,
        Paused
    }

    /// <summary>
    /// Memory optimization modes
    /// </summary>
    public enum BatchMemoryMode
    {
        Aggressive,  // Minimize memory usage
        Balanced,    // Balance memory and performance
        Performance  // Maximize performance
    }

    /// <summary>
    /// Processing mode preferences
    /// </summary>
    public enum BatchProcessingMode
    {
        Speed,       // Prioritize processing speed
        Balanced,    // Balance speed and quality
        Quality      // Prioritize output quality
    }

    /// <summary>
    /// Error handling strategies
    /// </summary>
    public enum BatchErrorHandling
    {
        StopOnFirstError,  // Stop batch on first error
        ContinueOnError,   // Continue processing remaining items
        RetryAndContinue   // Retry failed items then continue
    }

    /// <summary>
    /// Batch priority levels
    /// </summary>
    public enum BatchPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    /// <summary>
    /// Types of model management actions
    /// </summary>
    public enum ModelManagementAction
    {
        Load,
        Unload,
        Benchmark,
        Optimize,
        Validate,
        GetInfo,
        ClearCache
    }

    /// <summary>
    /// Types of postprocessing models
    /// </summary>
    public enum PostprocessingModelType
    {
        Enhancement,
        Upscaling,
        Denoising,
        ColorCorrection,
        StyleTransfer,
        Restoration,
        Custom
    }

    /// <summary>
    /// Model optimization levels
    /// </summary>
    public enum ModelOptimizationLevel
    {
        None,
        Basic,
        Balanced,
        Aggressive,
        Maximum
    }

    #endregion

    // ============================================================================
    // Model Management Models
    // ============================================================================

    /// <summary>
    /// Request for managing postprocessing models (load, unload, benchmark, optimize)
    /// </summary>
    public class PostPostprocessingModelManagementRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public ModelManagementAction Action { get; set; }
        public PostprocessingModelType? ModelType { get; set; }
        public ModelOptimizationLevel OptimizationLevel { get; set; } = ModelOptimizationLevel.Balanced;
        public int BenchmarkIterations { get; set; } = 10;
        public int MemoryLimitMB { get; set; } = 4096;
        public bool EnableGpuAcceleration { get; set; } = true;
        public Dictionary<string, object>? Configuration { get; set; }
    }

    /// <summary>
    /// Response for model management operations
    /// </summary>
    public class PostPostprocessingModelManagementResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public ModelManagementAction Action { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PostprocessingModelInfo? ModelInfo { get; set; }
        public ModelBenchmarkResults? BenchmarkResults { get; set; }
    }

    /// <summary>
    /// Information about a postprocessing model
    /// </summary>
    public class PostprocessingModelInfo
    {
        public string ModelName { get; set; } = string.Empty;
        public PostprocessingModelType ModelType { get; set; }
        public string Version { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool IsLoaded { get; set; }
        public bool SupportsGpu { get; set; }
        public ModelOptimizationLevel OptimizationLevel { get; set; }
        public DateTime LoadedAt { get; set; }
        public DateTime LastUsed { get; set; }
        public long MemoryUsageMB { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// Model benchmark test results
    /// </summary>
    public class ModelBenchmarkResults
    {
        public double AverageProcessingTimeMs { get; set; }
        public double MinProcessingTimeMs { get; set; }
        public double MaxProcessingTimeMs { get; set; }
        public double ThroughputImagesPerSecond { get; set; }
        public long MemoryPeakUsageMB { get; set; }
        public double GpuUtilizationPercent { get; set; }
        public double QualityScore { get; set; }
        public int IterationsCompleted { get; set; }
        public int TestImages { get; set; }
        public DateTime BenchmarkDate { get; set; }
        public List<string> OptimizationRecommendations { get; set; } = new();
    }

    // ============================================================================
    // Content Policy Management Models
    // ============================================================================

    /// <summary>
    /// Request for managing content policies
    /// </summary>
    public class PostPostprocessingContentPolicyRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string PolicyName { get; set; } = string.Empty;
        public ContentPolicyAction PolicyAction { get; set; }
        public ContentPolicyType? PolicyType { get; set; }
        public ContentPolicyEnforcementLevel EnforcementLevel { get; set; } = ContentPolicyEnforcementLevel.Moderate;
        public bool EnableLogging { get; set; } = true;
        public Dictionary<string, object>? Configuration { get; set; }
        public List<string>? ApplyToOperations { get; set; }
    }

    /// <summary>
    /// Response for content policy management operations
    /// </summary>
    public class PostPostprocessingContentPolicyResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public string PolicyName { get; set; } = string.Empty;
        public ContentPolicyAction PolicyAction { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ContentPolicyInfo? PolicyInfo { get; set; }
        public ContentPolicyValidationResults? ValidationResults { get; set; }
    }

    /// <summary>
    /// Information about a content policy
    /// </summary>
    public class ContentPolicyInfo
    {
        public string PolicyName { get; set; } = string.Empty;
        public ContentPolicyType PolicyType { get; set; }
        public string Version { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public ContentPolicyEnforcementLevel EnforcementLevel { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
        public List<string> AppliedOperations { get; set; } = new();
    }

    /// <summary>
    /// Content policy validation test results
    /// </summary>
    public class ContentPolicyValidationResults
    {
        public bool IsValid { get; set; }
        public DateTime ValidationDate { get; set; }
        public int TestsRun { get; set; }
        public int TestsPassed { get; set; }
        public int TestsFailed { get; set; }
        public double ValidationScore { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Types of content policy actions
    /// </summary>
    public enum ContentPolicyAction
    {
        Create,
        Update,
        Delete,
        Activate,
        Deactivate,
        Validate,
        GetInfo,
        Test
    }

    /// <summary>
    /// Types of content policies
    /// </summary>
    public enum ContentPolicyType
    {
        ContentFilter,
        SafetyCheck,
        QualityValidation,
        ComplianceCheck,
        Custom
    }

    /// <summary>
    /// Content policy enforcement levels
    /// </summary>
    public enum ContentPolicyEnforcementLevel
    {
        Disabled,
        Permissive,
        Moderate,
        Strict,
        Maximum
    }

    // ============================================================================
    // Performance Optimization Models (Week 19)
    // ============================================================================

    /// <summary>
    /// Connection configuration for optimized execution
    /// </summary>
    public class ConnectionConfig
    {
        public int PoolSize { get; set; } = 5;
        public int MaxRetries { get; set; } = 3;
        public int TimeoutMs { get; set; } = 30000;
        public string OptimizationLevel { get; set; } = "Balanced";
        public bool EnableCompression { get; set; } = true;
        public bool EnableKeepAlive { get; set; } = true;
        public Dictionary<string, object> AdditionalSettings { get; set; } = new();
    }

    /// <summary>
    /// Enhanced performance metrics for optimized operations
    /// </summary>
    public class PostprocessingPerformanceMetrics
    {
        public long TotalProcessingTimeMs { get; set; }
        public long ConnectionOptimizationMs { get; set; }
        public long TransformationMs { get; set; }
        public long ExecutionMs { get; set; }
        public long ParsingMs { get; set; }
        public long MemoryUsageMB { get; set; }
        public int ConnectionPoolSize { get; set; }
        public string OptimizationLevel { get; set; } = string.Empty;
        public double ThroughputOperationsPerSecond { get; set; }
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }

    /// <summary>
    /// Progress streaming configuration
    /// </summary>
    public class ProgressStreamingConfig
    {
        public int UpdateIntervalMs { get; set; } = 500;
        public bool EnablePreviewData { get; set; } = false;
        public bool EnableMetricsStreaming { get; set; } = true;
        public bool EnableCompression { get; set; } = false;
        public string CompressionLevel { get; set; } = "Fast";
        public int MaxPayloadSizeKB { get; set; } = 1024;
    }

    // ============================================================================
    // Basic Postprocessing Request/Response Models
    // ============================================================================

    /// <summary>
    /// Basic postprocessing request
    /// </summary>
    public class PostPostprocessingRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string ImageData { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public Dictionary<string, object>? Parameters { get; set; }
        public Dictionary<string, object>? QualitySettings { get; set; }
        public bool EnablePerformanceTracking { get; set; } = true;
    }

    /// <summary>
    /// Basic postprocessing response
    /// </summary>
    public class PostPostprocessingResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ProcessedImageData { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public PostprocessingPerformanceMetrics? PerformanceMetrics { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }

    // ============================================================================
    // Progress Streaming Models (Week 19)
    // ============================================================================

    /// <summary>
    /// Progress update for real-time streaming operations
    /// </summary>
    public record PostprocessingProgressUpdate
    {
        public string RequestId { get; init; } = string.Empty;
        public string Stage { get; init; } = string.Empty;
        public double Progress { get; init; } = 0.0;
        public string Message { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public bool IsCompleted { get; init; } = false;
        public bool HasError { get; init; } = false;
        public Dictionary<string, object>? PreviewData { get; init; }
        public Dictionary<string, object>? Metrics { get; init; }
        public PostPostprocessingResponse? Result { get; init; }
    }
}
