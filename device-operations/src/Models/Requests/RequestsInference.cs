namespace DeviceOperations.Models.Requests;

using DeviceOperations.Models.Common;

/// <summary>
/// Inference operation requests
/// </summary>
public static class RequestsInference
{
    /// <summary>
    /// Request to create a new inference session
    /// </summary>
    public class CreateInferenceSessionRequest
    {
        /// <summary>
        /// Session name/description
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Session type
        /// </summary>
        public SessionType Type { get; set; }

        /// <summary>
        /// Device identifier to run inference on
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Model identifier to use for inference
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Session configuration
        /// </summary>
        public SessionConfiguration Configuration { get; set; } = new();

        /// <summary>
        /// Session priority
        /// </summary>
        public SessionPriority Priority { get; set; } = SessionPriority.Normal;

        /// <summary>
        /// Auto-start session after creation
        /// </summary>
        public bool AutoStart { get; set; } = true;
    }

    /// <summary>
    /// Request to run inference on a session
    /// </summary>
    public class RunInferenceRequest
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Inference input parameters
        /// </summary>
        public InferenceInput Input { get; set; } = new();

        /// <summary>
        /// Inference generation parameters
        /// </summary>
        public InferenceParameters Parameters { get; set; } = new();

        /// <summary>
        /// Wait for inference completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;

        /// <summary>
        /// Inference timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Save results to disk
        /// </summary>
        public bool SaveResults { get; set; } = true;

        /// <summary>
        /// Output format preferences
        /// </summary>
        public OutputFormat OutputFormat { get; set; } = new();
    }

    /// <summary>
    /// Request to run batch inference
    /// </summary>
    public class RunBatchInferenceRequest
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Batch of inference inputs
        /// </summary>
        public List<InferenceInput> Inputs { get; set; } = new();

        /// <summary>
        /// Shared inference parameters
        /// </summary>
        public InferenceParameters Parameters { get; set; } = new();

        /// <summary>
        /// Batch processing configuration
        /// </summary>
        public BatchConfiguration BatchConfig { get; set; } = new();

        /// <summary>
        /// Wait for batch completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;

        /// <summary>
        /// Batch timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 1800;

        /// <summary>
        /// Progress callback interval in seconds
        /// </summary>
        public int ProgressCallbackInterval { get; set; } = 5;
    }

    /// <summary>
    /// Request to get inference session status
    /// </summary>
    public class GetInferenceSessionStatusRequest
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Include detailed progress information
        /// </summary>
        public bool IncludeProgress { get; set; } = true;

        /// <summary>
        /// Include resource usage
        /// </summary>
        public bool IncludeResources { get; set; } = true;

        /// <summary>
        /// Include results summary
        /// </summary>
        public bool IncludeResults { get; set; } = false;

        /// <summary>
        /// Include metrics
        /// </summary>
        public bool IncludeMetrics { get; set; } = false;
    }

    /// <summary>
    /// Request to list active inference sessions
    /// </summary>
    public class ListInferenceSessionsRequest
    {
        /// <summary>
        /// Filter by device ID
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Filter by model ID
        /// </summary>
        public string? ModelId { get; set; }

        /// <summary>
        /// Filter by session status
        /// </summary>
        public SessionStatus? Status { get; set; }

        /// <summary>
        /// Filter by session type
        /// </summary>
        public SessionType? Type { get; set; }

        /// <summary>
        /// Include session details
        /// </summary>
        public bool IncludeDetails { get; set; } = false;

        /// <summary>
        /// Include progress information
        /// </summary>
        public bool IncludeProgress { get; set; } = true;

        /// <summary>
        /// Sort by criteria
        /// </summary>
        public SessionSortBy SortBy { get; set; } = SessionSortBy.CreatedAt;

        /// <summary>
        /// Sort direction
        /// </summary>
        public SortDirection SortDirection { get; set; } = SortDirection.Descending;
    }

    /// <summary>
    /// Request to pause an inference session
    /// </summary>
    public class PauseInferenceSessionRequest
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Save session state
        /// </summary>
        public bool SaveState { get; set; } = true;

        /// <summary>
        /// Wait for pause completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;
    }

    /// <summary>
    /// Request to resume an inference session
    /// </summary>
    public class ResumeInferenceSessionRequest
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Restore session state
        /// </summary>
        public bool RestoreState { get; set; } = true;

        /// <summary>
        /// Wait for resume completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;
    }

    /// <summary>
    /// Request to cancel an inference session
    /// </summary>
    public class CancelInferenceSessionRequest
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Cancel reason
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Force cancel even if session is not responsive
        /// </summary>
        public bool Force { get; set; } = false;

        /// <summary>
        /// Cleanup session resources
        /// </summary>
        public bool CleanupResources { get; set; } = true;

        /// <summary>
        /// Wait for cancellation completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;
    }

    /// <summary>
    /// Request to get inference results
    /// </summary>
    public class GetInferenceResultsRequest
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Specific result ID (optional)
        /// </summary>
        public string? ResultId { get; set; }

        /// <summary>
        /// Result status filter
        /// </summary>
        public ResultStatus? Status { get; set; }

        /// <summary>
        /// Include result data
        /// </summary>
        public bool IncludeData { get; set; } = true;

        /// <summary>
        /// Include result metadata
        /// </summary>
        public bool IncludeMetadata { get; set; } = true;

        /// <summary>
        /// Page number for pagination
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Items per page
        /// </summary>
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// Request to update inference session configuration
    /// </summary>
    public class UpdateInferenceSessionRequest
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Updated configuration
        /// </summary>
        public SessionConfiguration? Configuration { get; set; }

        /// <summary>
        /// Updated priority
        /// </summary>
        public SessionPriority? Priority { get; set; }

        /// <summary>
        /// Apply updates immediately
        /// </summary>
        public bool ApplyImmediately { get; set; } = true;
    }
}

/// <summary>
/// Inference input parameters
/// </summary>
public class InferenceInput
{
    /// <summary>
    /// Input type
    /// </summary>
    public InputType Type { get; set; }

    /// <summary>
    /// Text prompt (for text-to-image, text generation)
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Negative prompt (for diffusion models)
    /// </summary>
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// Input image data (base64 encoded for image-to-image)
    /// </summary>
    public string? ImageData { get; set; }

    /// <summary>
    /// Input image URL
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Mask image data (for inpainting)
    /// </summary>
    public string? MaskData { get; set; }

    /// <summary>
    /// ControlNet input images
    /// </summary>
    public Dictionary<string, string> ControlNetInputs { get; set; } = new();

    /// <summary>
    /// Additional input parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Inference generation parameters
/// </summary>
public class InferenceParameters
{
    /// <summary>
    /// Number of inference steps
    /// </summary>
    public int Steps { get; set; } = 20;

    /// <summary>
    /// Guidance scale
    /// </summary>
    public double GuidanceScale { get; set; } = 7.5;

    /// <summary>
    /// Random seed (-1 for random)
    /// </summary>
    public long Seed { get; set; } = -1;

    /// <summary>
    /// Output width
    /// </summary>
    public int Width { get; set; } = 512;

    /// <summary>
    /// Output height
    /// </summary>
    public int Height { get; set; } = 512;

    /// <summary>
    /// Number of images to generate
    /// </summary>
    public int NumImages { get; set; } = 1;

    /// <summary>
    /// Scheduler type
    /// </summary>
    public string Scheduler { get; set; } = "DPMSolverMultistepScheduler";

    /// <summary>
    /// Strength for image-to-image (0.0-1.0)
    /// </summary>
    public double Strength { get; set; } = 0.8;

    /// <summary>
    /// ControlNet conditioning scale
    /// </summary>
    public Dictionary<string, double> ControlNetScales { get; set; } = new();

    /// <summary>
    /// LoRA weights
    /// </summary>
    public Dictionary<string, double> LoRAWeights { get; set; } = new();

    /// <summary>
    /// Additional custom parameters
    /// </summary>
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Output format preferences
/// </summary>
public class OutputFormat
{
    /// <summary>
    /// Image format (PNG, JPEG, WebP)
    /// </summary>
    public string ImageFormat { get; set; } = "PNG";

    /// <summary>
    /// Image quality (1-100 for JPEG)
    /// </summary>
    public int Quality { get; set; } = 95;

    /// <summary>
    /// Whether to return base64 encoded data
    /// </summary>
    public bool ReturnBase64 { get; set; } = false;

    /// <summary>
    /// Whether to save to file
    /// </summary>
    public bool SaveToFile { get; set; } = true;

    /// <summary>
    /// Custom output path
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Include metadata in output
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;
}

/// <summary>
/// Batch processing configuration
/// </summary>
public class BatchConfiguration
{
    /// <summary>
    /// Maximum concurrent operations
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Batch size for processing
    /// </summary>
    public int BatchSize { get; set; } = 1;

    /// <summary>
    /// Continue processing on error
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Shuffle input order
    /// </summary>
    public bool ShuffleInputs { get; set; } = false;

    /// <summary>
    /// Progress reporting interval
    /// </summary>
    public int ProgressInterval { get; set; } = 1;
}

/// <summary>
/// Input type enumeration
/// </summary>
public enum InputType
{
    TextToImage = 0,
    ImageToImage = 1,
    Inpainting = 2,
    ControlNet = 3,
    Upscaling = 4,
    Text = 5,
    Custom = 6
}

/// <summary>
/// Session sort criteria enumeration
/// </summary>
public enum SessionSortBy
{
    CreatedAt = 0,
    StartedAt = 1,
    Priority = 2,
    Status = 3,
    Name = 4,
    Progress = 5
}
