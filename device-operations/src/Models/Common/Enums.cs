namespace DeviceOperations.Models.Common;

/// <summary>
/// Inference type enumeration
/// </summary>
public enum InferenceType
{
    Unknown = 0,
    TextToImage = 1,
    ImageToImage = 2,
    Inpainting = 3,
    ControlNet = 4,
    Upscaling = 5,
    Enhancement = 6,
    StyleTransfer = 7
}

/// <summary>
/// Inference status enumeration
/// </summary>
public enum InferenceStatus
{
    Unknown = 0,
    Pending = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

/// <summary>
/// Python worker types enumeration
/// </summary>
public static class PythonWorkerTypes
{
    public const string DEVICE = "device";
    public const string MEMORY = "memory";
    public const string MODEL = "model";
    public const string INFERENCE = "inference";
    public const string POSTPROCESSING = "postprocessing";
    public const string PROCESSING = "processing";
}

/// <summary>
/// Device reset type enumeration
/// </summary>
public enum DeviceResetType
{
    Soft = 0,
    Hard = 1,
    Driver = 2,
    Complete = 3
}

/// <summary>
/// Benchmark type enumeration
/// </summary>
public enum BenchmarkType
{
    Performance = 0,
    Memory = 1,
    Stability = 2,
    Power = 3,
    Compute = 4,
    Inference = 5,
    Mixed = 6,
    Stress = 7
}

/// <summary>
/// Optimization target enumeration
/// </summary>
public enum OptimizationTarget
{
    Performance = 0,
    Memory = 1,
    Power = 2,
    Balanced = 3,
    PowerEfficiency = 4,
    MemoryUsage = 5,
    Throughput = 6,
    Latency = 7
}

/// <summary>
/// Model precision enumeration
/// </summary>
public enum ModelPrecision
{
    FP32 = 0,
    FP16 = 1,
    INT8 = 2,
    AUTO = 3
}

/// <summary>
/// Session priority enumeration
/// </summary>
public enum SessionPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Workload type enumeration
/// </summary>
public enum WorkloadType
{
    ImageGeneration = 0,
    TextGeneration = 1,
    ImageToImage = 2,
    Upscaling = 3,
    BatchProcessing = 4,
    Interactive = 5
}
