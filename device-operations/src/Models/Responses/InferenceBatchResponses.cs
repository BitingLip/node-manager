using DeviceOperations.Models.Common;

namespace DeviceOperations.Models.Responses
{
    /// <summary>
    /// Response model for batch inference processing
    /// </summary>
    public class PostInferenceBatchResponse
    {
        /// <summary>
        /// Unique identifier for the batch
        /// </summary>
        public string BatchId { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the batch
        /// </summary>
        public BatchStatus Status { get; set; }

        /// <summary>
        /// Total number of items in the batch
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Number of items completed successfully
        /// </summary>
        public int CompletedItems { get; set; }

        /// <summary>
        /// Number of items that failed
        /// </summary>
        public int FailedItems { get; set; }

        /// <summary>
        /// Number of items currently processing
        /// </summary>
        public int ProcessingItems { get; set; }

        /// <summary>
        /// Overall batch progress (0.0 to 1.0)
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// Estimated time to completion in seconds
        /// </summary>
        public int? EstimatedTimeToCompletionSeconds { get; set; }

        /// <summary>
        /// When the batch was started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// When the batch was completed (if applicable)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Results for completed items
        /// </summary>
        public List<BatchInferenceResult> Results { get; set; } = new();

        /// <summary>
        /// Performance metrics for the batch
        /// </summary>
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Error details for failed items
        /// </summary>
        public List<BatchInferenceError> Errors { get; set; } = new();

        /// <summary>
        /// Additional batch statistics
        /// </summary>
        public Dictionary<string, object> Statistics { get; set; } = new();
    }

    /// <summary>
    /// Result for an individual batch inference item
    /// </summary>
    public class BatchInferenceResult
    {
        /// <summary>
        /// Item identifier within the batch
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Status of this specific item
        /// </summary>
        public BatchItemStatus Status { get; set; }

        /// <summary>
        /// Inference results for this item
        /// </summary>
        public Dictionary<string, object> Results { get; set; } = new();

        /// <summary>
        /// Processing time for this item in milliseconds
        /// </summary>
        public double ProcessingTimeMs { get; set; }

        /// <summary>
        /// Quality metrics for this inference result
        /// </summary>
        public Dictionary<string, object> QualityMetrics { get; set; } = new();

        /// <summary>
        /// When this item was started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// When this item was completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Device used for processing this item
        /// </summary>
        public string? DeviceId { get; set; }
    }

    /// <summary>
    /// Error information for a failed batch item
    /// </summary>
    public class BatchInferenceError
    {
        /// <summary>
        /// Item identifier that failed
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Standardized error code
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Error category for classification
        /// </summary>
        public string ErrorCategory { get; set; } = string.Empty;

        /// <summary>
        /// When the error occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Number of retry attempts made
        /// </summary>
        public int RetryAttempts { get; set; }

        /// <summary>
        /// Additional error details
        /// </summary>
        public Dictionary<string, object> ErrorDetails { get; set; } = new();
    }

    /// <summary>
    /// Response model for ControlNet inference
    /// </summary>
    public class PostInferenceControlNetResponse
    {
        /// <summary>
        /// Unique identifier for this inference
        /// </summary>
        public Guid InferenceId { get; set; }

        /// <summary>
        /// Success status of the inference
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Generated image results
        /// </summary>
        public List<string> GeneratedImages { get; set; } = new();

        /// <summary>
        /// ControlNet processing results
        /// </summary>
        public Dictionary<string, object> ControlNetResults { get; set; } = new();

        /// <summary>
        /// Inference parameters used
        /// </summary>
        public Dictionary<string, object> UsedParameters { get; set; } = new();

        /// <summary>
        /// Processing time in milliseconds
        /// </summary>
        public double ProcessingTimeMs { get; set; }

        /// <summary>
        /// Quality metrics for the generated content
        /// </summary>
        public Dictionary<string, object> QualityMetrics { get; set; } = new();

        /// <summary>
        /// Performance metrics
        /// </summary>
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Device used for processing
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// When the inference was completed
        /// </summary>
        public DateTime CompletedAt { get; set; }
    }

    /// <summary>
    /// Response model for LoRA inference
    /// </summary>
    public class PostInferenceLoRAResponse
    {
        /// <summary>
        /// Unique identifier for this inference
        /// </summary>
        public Guid InferenceId { get; set; }

        /// <summary>
        /// Success status of the inference
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Generated content results
        /// </summary>
        public Dictionary<string, object> Results { get; set; } = new();

        /// <summary>
        /// LoRA adaptation information
        /// </summary>
        public Dictionary<string, object> LoRAInfo { get; set; } = new();

        /// <summary>
        /// Base model information
        /// </summary>
        public Dictionary<string, object> BaseModelInfo { get; set; } = new();

        /// <summary>
        /// Processing time in milliseconds
        /// </summary>
        public double ProcessingTimeMs { get; set; }

        /// <summary>
        /// Quality metrics for the generated content
        /// </summary>
        public Dictionary<string, object> QualityMetrics { get; set; } = new();

        /// <summary>
        /// Performance metrics including LoRA overhead
        /// </summary>
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Device used for processing
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// When the inference was completed
        /// </summary>
        public DateTime CompletedAt { get; set; }

        /// <summary>
        /// Memory usage statistics
        /// </summary>
        public Dictionary<string, object> MemoryUsage { get; set; } = new();
    }

    /// <summary>
    /// Batch processing status enumeration
    /// </summary>
    public enum BatchStatus
    {
        Unknown = 0,
        Queued = 1,
        Processing = 2,
        Completed = 3,
        Failed = 4,
        Cancelled = 5,
        PartiallyCompleted = 6
    }

    /// <summary>
    /// Individual batch item status enumeration
    /// </summary>
    public enum BatchItemStatus
    {
        Unknown = 0,
        Pending = 1,
        Processing = 2,
        Completed = 3,
        Failed = 4,
        Skipped = 5,
        Retrying = 6
    }
}
