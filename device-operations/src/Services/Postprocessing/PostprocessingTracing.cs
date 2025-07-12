namespace DeviceOperations.Services.Postprocessing
{
    /// <summary>
    /// Request tracing for postprocessing operations
    /// </summary>
    public class PostprocessingRequestTrace
    {
        public string RequestId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public PostprocessingRequestStatus Status { get; set; } = PostprocessingRequestStatus.Pending;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration => EndTime?.Subtract(StartTime);
        public List<PostprocessingError> Errors { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
        public string? DeviceId { get; set; }
        public string? ModelId { get; set; }
        public int? ImageWidth { get; set; }
        public int? ImageHeight { get; set; }
        public string? ProcessingType { get; set; }
        public double? QualityScore { get; set; }
        public long? MemoryUsageMB { get; set; }
        public long? ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Standardized error format for postprocessing operations
    /// </summary>
    public class PostprocessingError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Details { get; set; } = new();
        public string? StackTrace { get; set; }
        public string? InnerError { get; set; }
        public PostprocessingErrorSeverity Severity { get; set; } = PostprocessingErrorSeverity.Error;
        public bool IsRetryable { get; set; } = false;
        public string? SuggestedAction { get; set; }
    }

    /// <summary>
    /// Request status enumeration
    /// </summary>
    public enum PostprocessingRequestStatus
    {
        Pending,
        Validating,
        Processing,
        Enhancing,
        Finalizing,
        Completed,
        Failed,
        Cancelled,
        Timeout
    }

    /// <summary>
    /// Error severity levels
    /// </summary>
    public enum PostprocessingErrorSeverity
    {
        Info,
        Warning,
        Error,
        Critical,
        Fatal
    }

    /// <summary>
    /// Postprocessing error codes
    /// </summary>
    public static class PostprocessingErrorCodes
    {
        // Validation errors
        public const string VALIDATION_ERROR = "POST_VALIDATION_ERROR";
        public const string INVALID_IMAGE_FORMAT = "POST_INVALID_IMAGE_FORMAT";
        public const string INVALID_PARAMETERS = "POST_INVALID_PARAMETERS";
        public const string UNSUPPORTED_OPERATION = "POST_UNSUPPORTED_OPERATION";
        public const string IMAGE_TOO_LARGE = "POST_IMAGE_TOO_LARGE";
        public const string IMAGE_TOO_SMALL = "POST_IMAGE_TOO_SMALL";
        public const string INVALID_QUALITY_LEVEL = "POST_INVALID_QUALITY_LEVEL";
        public const string INVALID_DEVICE_ID = "POST_INVALID_DEVICE_ID";
        public const string INVALID_MODEL_ID = "POST_INVALID_MODEL_ID";

        // Processing errors
        public const string PROCESSING_ERROR = "POST_PROCESSING_ERROR";
        public const string MODEL_NOT_LOADED = "POST_MODEL_NOT_LOADED";
        public const string MODEL_LOADING_FAILED = "POST_MODEL_LOADING_FAILED";
        public const string DEVICE_NOT_AVAILABLE = "POST_DEVICE_NOT_AVAILABLE";
        public const string INSUFFICIENT_MEMORY = "POST_INSUFFICIENT_MEMORY";
        public const string INSUFFICIENT_VRAM = "POST_INSUFFICIENT_VRAM";
        public const string PROCESSING_TIMEOUT = "POST_PROCESSING_TIMEOUT";
        public const string ENHANCEMENT_FAILED = "POST_ENHANCEMENT_FAILED";
        public const string UPSCALING_FAILED = "POST_UPSCALING_FAILED";
        public const string FACE_RESTORATION_FAILED = "POST_FACE_RESTORATION_FAILED";
        public const string STYLE_TRANSFER_FAILED = "POST_STYLE_TRANSFER_FAILED";
        public const string BACKGROUND_REMOVAL_FAILED = "POST_BACKGROUND_REMOVAL_FAILED";
        public const string COLOR_CORRECTION_FAILED = "POST_COLOR_CORRECTION_FAILED";

        // System errors
        public const string SYSTEM_ERROR = "POST_SYSTEM_ERROR";
        public const string PYTHON_WORKER_ERROR = "POST_PYTHON_WORKER_ERROR";
        public const string COMMUNICATION_ERROR = "POST_COMMUNICATION_ERROR";
        public const string SERIALIZATION_ERROR = "POST_SERIALIZATION_ERROR";
        public const string FILE_IO_ERROR = "POST_FILE_IO_ERROR";
        public const string NETWORK_ERROR = "POST_NETWORK_ERROR";
        public const string RESOURCE_ALLOCATION_ERROR = "POST_RESOURCE_ALLOCATION_ERROR";
        public const string CONCURRENT_PROCESSING_ERROR = "POST_CONCURRENT_PROCESSING_ERROR";

        // Batch processing errors
        public const string BATCH_PROCESSING_ERROR = "POST_BATCH_PROCESSING_ERROR";
        public const string BATCH_SIZE_TOO_LARGE = "POST_BATCH_SIZE_TOO_LARGE";
        public const string BATCH_ITEM_FAILED = "POST_BATCH_ITEM_FAILED";
        public const string BATCH_TIMEOUT = "POST_BATCH_TIMEOUT";
        public const string BATCH_MEMORY_ERROR = "POST_BATCH_MEMORY_ERROR";
        public const string BATCH_QUEUE_FULL = "POST_BATCH_QUEUE_FULL";

        // Performance and optimization errors
        public const string PERFORMANCE_DEGRADATION = "POST_PERFORMANCE_DEGRADATION";
        public const string OPTIMIZATION_FAILED = "POST_OPTIMIZATION_FAILED";
        public const string QUALITY_THRESHOLD_NOT_MET = "POST_QUALITY_THRESHOLD_NOT_MET";
        public const string CACHE_ERROR = "POST_CACHE_ERROR";
        public const string CONFIGURATION_ERROR = "POST_CONFIGURATION_ERROR";
    }

    /// <summary>
    /// Error categories for classification
    /// </summary>
    public static class PostprocessingErrorCategories
    {
        public const string VALIDATION = "Validation";
        public const string PROCESSING = "Processing";
        public const string SYSTEM = "System";
        public const string PERFORMANCE = "Performance";
        public const string RESOURCE = "Resource";
        public const string COMMUNICATION = "Communication";
        public const string CONFIGURATION = "Configuration";
        public const string BATCH = "Batch";
        public const string MODEL = "Model";
        public const string DEVICE = "Device";
    }
}
