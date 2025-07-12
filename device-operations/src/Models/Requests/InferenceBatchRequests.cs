using DeviceOperations.Models.Common;
using System.ComponentModel.DataAnnotations;

namespace DeviceOperations.Models.Requests
{
    /// <summary>
    /// Request model for batch inference processing
    /// </summary>
    public class PostInferenceBatchRequest
    {
        /// <summary>
        /// Unique identifier for the batch
        /// </summary>
        public string? BatchId { get; set; }

        /// <summary>
        /// Model to use for batch inference
        /// </summary>
        [Required]
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Type of inference to perform
        /// </summary>
        [Required]
        public InferenceType InferenceType { get; set; }

        /// <summary>
        /// List of inference items to process in batch
        /// </summary>
        [Required]
        public List<BatchInferenceItem> Items { get; set; } = new();

        /// <summary>
        /// Batch processing options
        /// </summary>
        public BatchProcessingOptions Options { get; set; } = new();

        /// <summary>
        /// Priority level for the batch (1-10, 10 = highest)
        /// </summary>
        [Range(1, 10)]
        public int Priority { get; set; } = 5;

        /// <summary>
        /// Optional callback URL for batch completion notification
        /// </summary>
        public string? CallbackUrl { get; set; }

        /// <summary>
        /// Maximum execution time for the entire batch (in seconds)
        /// </summary>
        [Range(1, 3600)]
        public int MaxExecutionTimeSeconds { get; set; } = 1800; // 30 minutes default
    }

    /// <summary>
    /// Individual inference item within a batch
    /// </summary>
    public class BatchInferenceItem
    {
        /// <summary>
        /// Unique identifier for this item within the batch
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Parameters specific to this inference item
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// Optional metadata for this item
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Input data for this inference item
        /// </summary>
        public Dictionary<string, object> InputData { get; set; } = new();
    }

    /// <summary>
    /// Batch processing configuration options
    /// </summary>
    public class BatchProcessingOptions
    {
        /// <summary>
        /// Number of items to process concurrently
        /// </summary>
        [Range(1, 32)]
        public int ConcurrencyLevel { get; set; } = 4;

        /// <summary>
        /// Maximum batch size (will split larger batches automatically)
        /// </summary>
        [Range(1, 100)]
        public int MaxBatchSize { get; set; } = 16;

        /// <summary>
        /// Whether to fail the entire batch if one item fails
        /// </summary>
        public bool FailOnFirstError { get; set; } = false;

        /// <summary>
        /// Whether to optimize memory usage during batch processing
        /// </summary>
        public bool OptimizeMemoryUsage { get; set; } = true;

        /// <summary>
        /// Whether to enable progress tracking and updates
        /// </summary>
        public bool EnableProgressTracking { get; set; } = true;

        /// <summary>
        /// Minimum delay between processing items (in milliseconds)
        /// </summary>
        [Range(0, 5000)]
        public int MinItemDelayMs { get; set; } = 0;

        /// <summary>
        /// Whether to automatically retry failed items
        /// </summary>
        public bool AutoRetryFailedItems { get; set; } = true;

        /// <summary>
        /// Maximum number of retry attempts per item
        /// </summary>
        [Range(0, 5)]
        public int MaxRetryAttempts { get; set; } = 2;
    }

    /// <summary>
    /// Request model for ControlNet inference
    /// </summary>
    public class PostInferenceControlNetRequest
    {
        /// <summary>
        /// Model to use for ControlNet inference
        /// </summary>
        [Required]
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Type of ControlNet to use
        /// </summary>
        [Required]
        public ControlNetType ControlNetType { get; set; }

        /// <summary>
        /// ControlNet model identifier
        /// </summary>
        [Required]
        public string ControlNetModel { get; set; } = string.Empty;

        /// <summary>
        /// Control image for guidance
        /// </summary>
        [Required]
        public string ControlImageBase64 { get; set; } = string.Empty;

        /// <summary>
        /// Text prompt for generation
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Negative prompt to avoid certain features
        /// </summary>
        public string NegativePrompt { get; set; } = string.Empty;

        /// <summary>
        /// ControlNet conditioning strength (0.0 to 2.0)
        /// </summary>
        [Range(0.0, 2.0)]
        public double ControlNetWeight { get; set; } = 1.0;

        /// <summary>
        /// Guidance scale for text adherence (1.0 to 30.0)
        /// </summary>
        [Range(1.0, 30.0)]
        public double GuidanceScale { get; set; } = 7.5;

        /// <summary>
        /// Number of inference steps
        /// </summary>
        [Range(1, 150)]
        public int NumSteps { get; set; } = 20;

        /// <summary>
        /// Output image dimensions
        /// </summary>
        public ImageResolution Resolution { get; set; } = new() { Width = 512, Height = 512 };

        /// <summary>
        /// Random seed for reproducibility
        /// </summary>
        public long? Seed { get; set; }

        /// <summary>
        /// Additional inference parameters
        /// </summary>
        public Dictionary<string, object> AdditionalParameters { get; set; } = new();
    }

    /// <summary>
    /// Request model for LoRA inference
    /// </summary>
    public class PostInferenceLoRARequest
    {
        /// <summary>
        /// Base model to use for LoRA inference
        /// </summary>
        [Required]
        public string BaseModelId { get; set; } = string.Empty;

        /// <summary>
        /// LoRA model path or identifier
        /// </summary>
        [Required]
        public string LoRAPath { get; set; } = string.Empty;

        /// <summary>
        /// LoRA conditioning strength (0.0 to 1.0)
        /// </summary>
        [Range(0.0, 1.0)]
        public double LoRAWeight { get; set; } = 0.8;

        /// <summary>
        /// LoRA alpha parameter for scaling
        /// </summary>
        [Range(1, 128)]
        public int LoRAAlpha { get; set; } = 32;

        /// <summary>
        /// Text prompt for generation
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Negative prompt to avoid certain features
        /// </summary>
        public string NegativePrompt { get; set; } = string.Empty;

        /// <summary>
        /// Type of inference to perform
        /// </summary>
        public InferenceType InferenceType { get; set; } = InferenceType.TextToImage;

        /// <summary>
        /// Number of inference steps
        /// </summary>
        [Range(1, 150)]
        public int NumSteps { get; set; } = 20;

        /// <summary>
        /// Guidance scale for text adherence
        /// </summary>
        [Range(1.0, 30.0)]
        public double GuidanceScale { get; set; } = 7.5;

        /// <summary>
        /// Output image dimensions
        /// </summary>
        public ImageResolution Resolution { get; set; } = new() { Width = 512, Height = 512 };

        /// <summary>
        /// Random seed for reproducibility
        /// </summary>
        public long? Seed { get; set; }

        /// <summary>
        /// Additional LoRA-specific parameters
        /// </summary>
        public Dictionary<string, object> LoRAParameters { get; set; } = new();
    }

    /// <summary>
    /// ControlNet type enumeration
    /// </summary>
    public enum ControlNetType
    {
        Unknown = 0,
        Pose = 1,
        Depth = 2,
        Canny = 3,
        Edge = 4,
        Normal = 5,
        Segmentation = 6,
        Scribble = 7,
        Lineart = 8,
        SoftEdge = 9,
        OpenposeHand = 10
    }
}
