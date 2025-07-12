namespace DeviceOperations.Models.Common;

/// <summary>
/// Device information and capabilities model
/// </summary>
public class DeviceInfo
{
    /// <summary>
    /// Unique device identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable device name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Device type (CPU, GPU, etc.)
    /// </summary>
    public DeviceType Type { get; set; }

    /// <summary>
    /// Device vendor (Intel, NVIDIA, AMD, etc.)
    /// </summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>
    /// Device architecture (x64, ARM, etc.)
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Current device status
    /// </summary>
    public DeviceStatus Status { get; set; }

    /// <summary>
    /// Device availability for operations
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Driver version information
    /// </summary>
    public string DriverVersion { get; set; } = string.Empty;

    /// <summary>
    /// Device creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Device information last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Device utilization information
    /// </summary>
    public DeviceUtilization Utilization { get; set; } = new();

    /// <summary>
    /// Device specifications
    /// </summary>
    public DeviceSpecifications Specifications { get; set; } = new();
}

/// <summary>
/// Device capabilities and supported features
/// </summary>
public class DeviceCapabilities
{
    /// <summary>
    /// Supported inference types
    /// </summary>
    public List<string> SupportedInferenceTypes { get; set; } = new();

    /// <summary>
    /// Supported model types
    /// </summary>
    public List<ModelType> SupportedModelTypes { get; set; } = new();

    /// <summary>
    /// Supports batch processing
    /// </summary>
    public bool SupportsBatchProcessing { get; set; }

    /// <summary>
    /// Maximum batch size
    /// </summary>
    public int MaxBatchSize { get; set; }

    /// <summary>
    /// Supports concurrent inferences
    /// </summary>
    public bool SupportsConcurrentInference { get; set; }

    /// <summary>
    /// Maximum concurrent inferences
    /// </summary>
    public int MaxConcurrentInferences { get; set; }

    /// <summary>
    /// Supported precision modes
    /// </summary>
    public List<string> SupportedPrecisions { get; set; } = new();

    /// <summary>
    /// Memory allocation capabilities
    /// </summary>
    public MemoryAllocationInfo MemoryCapabilities { get; set; } = new();

    /// <summary>
    /// Performance benchmarks
    /// </summary>
    public Dictionary<string, double> PerformanceBenchmarks { get; set; } = new();
}

/// <summary>
/// Memory allocation information
/// </summary>
public class MemoryAllocationInfo
{
    /// <summary>
    /// Total available memory in bytes
    /// </summary>
    public long TotalMemoryBytes { get; set; }

    /// <summary>
    /// Currently allocated memory in bytes
    /// </summary>
    public long AllocatedMemoryBytes { get; set; }

    /// <summary>
    /// Available memory in bytes
    /// </summary>
    public long AvailableMemoryBytes => TotalMemoryBytes - AllocatedMemoryBytes;

    /// <summary>
    /// Supports memory pooling
    /// </summary>
    public bool SupportsMemoryPooling { get; set; }

    /// <summary>
    /// Memory allocation alignment requirements
    /// </summary>
    public int AllocationAlignment { get; set; } = 1;
}

/// <summary>
/// Device type enumeration
/// </summary>
public enum DeviceType
{
    Unknown = 0,
    CPU = 1,
    GPU = 2,
    NPU = 3,
    TPU = 4
}

/// <summary>
/// Device status enumeration
/// </summary>
public enum DeviceStatus
{
    Unknown = 0,
    Offline = 1,
    Available = 2,
    Busy = 3,
    Error = 4,
    Maintenance = 5
}

/// <summary>
/// Model type enumeration
/// </summary>
public enum ModelType
{
    Unknown = 0,
    SD15 = 1,
    SDXL = 2,
    Flux = 3,
    Diffusion = 4,
    ControlNet = 5,
    LoRA = 6,
    VAE = 7,
    TextEncoder = 8,
    UNet = 9,
    Scheduler = 10
}

/// <summary>
/// Image resolution class
/// </summary>
public class ImageResolution
{
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// Device health information
/// </summary>
public class DeviceHealth
{
    /// <summary>
    /// Overall health status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Temperature in Celsius
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Power consumption in watts
    /// </summary>
    public double PowerConsumption { get; set; }

    /// <summary>
    /// Health metrics
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// Driver information
/// </summary>
public class DriverInfo
{
    /// <summary>
    /// Driver version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Driver name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Driver date
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Driver status
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Device utilization information
/// </summary>
public class DeviceUtilization
{
    /// <summary>
    /// CPU utilization percentage
    /// </summary>
    public double CpuUtilization { get; set; }

    /// <summary>
    /// Memory utilization percentage
    /// </summary>
    public double MemoryUtilization { get; set; }

    /// <summary>
    /// GPU utilization percentage (if applicable)
    /// </summary>
    public double GpuUtilization { get; set; }
}

/// <summary>
/// Device specifications
/// </summary>
public class DeviceSpecifications
{
    /// <summary>
    /// Total memory in bytes
    /// </summary>
    public long TotalMemoryBytes { get; set; }

    /// <summary>
    /// Available memory in bytes
    /// </summary>
    public long AvailableMemoryBytes { get; set; }

    /// <summary>
    /// Compute units/cores
    /// </summary>
    public int ComputeUnits { get; set; }

    /// <summary>
    /// Clock speed in MHz
    /// </summary>
    public int ClockSpeedMHz { get; set; }

    /// <summary>
    /// Supported model types
    /// </summary>
    public List<ModelType> SupportedModelTypes { get; set; } = new();

    /// <summary>
    /// Supported precision modes
    /// </summary>
    public List<string> SupportedPrecisions { get; set; } = new();
}

/// <summary>
/// Device benchmark results
/// </summary>
public class DeviceBenchmarkResults
{
    /// <summary>
    /// Benchmark metrics
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// Inference performance metrics
/// </summary>
public class InferencePerformanceMetrics
{
    public double InferenceTimeMs { get; set; }
    public double ThroughputTokensPerSecond { get; set; }
    public double MemoryUsageMB { get; set; }
}
