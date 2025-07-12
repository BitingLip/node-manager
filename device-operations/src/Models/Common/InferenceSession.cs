namespace DeviceOperations.Models.Common;

/// <summary>
/// Inference session tracking model
/// </summary>
public class InferenceSession
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Session name/description
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Session type
    /// </summary>
    public SessionType Type { get; set; }

    /// <summary>
    /// Current session status
    /// </summary>
    public SessionStatus Status { get; set; }

    /// <summary>
    /// Session priority
    /// </summary>
    public SessionPriority Priority { get; set; }

    /// <summary>
    /// Device ID where session is running
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Model ID being used in session
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Session configuration
    /// </summary>
    public SessionConfiguration Configuration { get; set; } = new();

    /// <summary>
    /// Session progress information
    /// </summary>
    public SessionProgress Progress { get; set; } = new();

    /// <summary>
    /// Session resource usage
    /// </summary>
    public SessionResources Resources { get; set; } = new();

    /// <summary>
    /// Session metrics
    /// </summary>
    public SessionMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Session results
    /// </summary>
    public List<InferenceResult> Results { get; set; } = new();

    /// <summary>
    /// Session errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Session warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Session creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Session start timestamp
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Session completion timestamp
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Session duration in milliseconds
    /// </summary>
    public long DurationMs => CompletedAt.HasValue && StartedAt.HasValue 
        ? (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds 
        : StartedAt.HasValue 
            ? (long)(DateTime.UtcNow - StartedAt.Value).TotalMilliseconds 
            : 0;
}

/// <summary>
/// Session configuration
/// </summary>
public class SessionConfiguration
{
    /// <summary>
    /// Maximum number of inference operations
    /// </summary>
    public int MaxOperations { get; set; } = 1;

    /// <summary>
    /// Session timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to save intermediate results
    /// </summary>
    public bool SaveIntermediateResults { get; set; } = false;

    /// <summary>
    /// Auto-cleanup after completion
    /// </summary>
    public bool AutoCleanup { get; set; } = true;

    /// <summary>
    /// Session-specific parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Memory optimization settings
    /// </summary>
    public MemoryOptimization MemoryOptimization { get; set; } = new();
}

/// <summary>
/// Session progress information
/// </summary>
public class SessionProgress
{
    /// <summary>
    /// Current step number
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Total number of steps
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage => TotalSteps > 0 ? (double)CurrentStep / TotalSteps * 100 : 0;

    /// <summary>
    /// Current operation description
    /// </summary>
    public string CurrentOperation { get; set; } = string.Empty;

    /// <summary>
    /// Estimated time remaining in seconds
    /// </summary>
    public double? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// Operations per second
    /// </summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>
    /// Completed operations count
    /// </summary>
    public int CompletedOperations { get; set; }

    /// <summary>
    /// Failed operations count
    /// </summary>
    public int FailedOperations { get; set; }
}

/// <summary>
/// Session resource usage
/// </summary>
public class SessionResources
{
    /// <summary>
    /// Memory allocations for this session
    /// </summary>
    public List<string> MemoryAllocations { get; set; } = new();

    /// <summary>
    /// Total memory usage in bytes
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Peak memory usage in bytes
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// GPU utilization percentage
    /// </summary>
    public double GpuUtilization { get; set; }

    /// <summary>
    /// CPU utilization percentage
    /// </summary>
    public double CpuUtilization { get; set; }

    /// <summary>
    /// Power consumption in watts
    /// </summary>
    public double PowerConsumption { get; set; }

    /// <summary>
    /// Temporary files created
    /// </summary>
    public List<string> TemporaryFiles { get; set; } = new();
}

/// <summary>
/// Session metrics
/// </summary>
public class SessionMetrics
{
    /// <summary>
    /// Average inference time per operation in milliseconds
    /// </summary>
    public double AverageInferenceTime { get; set; }

    /// <summary>
    /// Minimum inference time in milliseconds
    /// </summary>
    public double MinInferenceTime { get; set; }

    /// <summary>
    /// Maximum inference time in milliseconds
    /// </summary>
    public double MaxInferenceTime { get; set; }

    /// <summary>
    /// Total processing time in milliseconds
    /// </summary>
    public double TotalProcessingTime { get; set; }

    /// <summary>
    /// Throughput (operations per second)
    /// </summary>
    public double Throughput { get; set; }

    /// <summary>
    /// Quality metrics
    /// </summary>
    public Dictionary<string, double> QualityMetrics { get; set; } = new();

    /// <summary>
    /// Performance metrics
    /// </summary>
    public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
}

/// <summary>
/// Inference result
/// </summary>
public class InferenceResult
{
    /// <summary>
    /// Result identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Operation type that produced this result
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Result status
    /// </summary>
    public ResultStatus Status { get; set; }

    /// <summary>
    /// Result data (base64 encoded image, JSON, etc.)
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Result metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// Result file path (if saved to disk)
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Result creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Error message if result failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Memory optimization settings
/// </summary>
public class MemoryOptimization
{
    /// <summary>
    /// Enable attention slicing
    /// </summary>
    public bool EnableAttentionSlicing { get; set; }

    /// <summary>
    /// Enable CPU offloading
    /// </summary>
    public bool EnableCpuOffloading { get; set; }

    /// <summary>
    /// Enable mixed precision
    /// </summary>
    public bool EnableMixedPrecision { get; set; }

    /// <summary>
    /// Memory fraction to use (0.0-1.0)
    /// </summary>
    public double MemoryFraction { get; set; } = 1.0;

    /// <summary>
    /// Enable garbage collection
    /// </summary>
    public bool EnableGarbageCollection { get; set; } = true;
}

/// <summary>
/// Session type enumeration
/// </summary>
public enum SessionType
{
    Unknown = 0,
    SingleInference = 1,
    BatchInference = 2,
    InteractiveSession = 3,
    WorkflowExecution = 4,
    ModelTraining = 5,
    ModelValidation = 6
}

/// <summary>
/// Session status enumeration
/// </summary>
public enum SessionStatus
{
    Unknown = 0,
    Created = 1,
    Initializing = 2,
    Running = 3,
    Paused = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7,
    TimedOut = 8
}

/// <summary>
/// Result status enumeration
/// </summary>
public enum ResultStatus
{
    Unknown = 0,
    Success = 1,
    Failed = 2,
    Cancelled = 3,
    Timeout = 4,
    PartialSuccess = 5
}
