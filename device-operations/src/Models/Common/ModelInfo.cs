namespace DeviceOperations.Models.Common;

/// <summary>
/// Model metadata and status models
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// Unique model identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Model name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Model type (SDXL, SD1.5, Flux, etc.)
    /// </summary>
    public ModelType Type { get; set; }

    /// <summary>
    /// Model version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Model description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Model file path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Model file size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Model hash/checksum
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Current model status
    /// </summary>
    public ModelStatus Status { get; set; }

    /// <summary>
    /// Model loading status
    /// </summary>
    public ModelLoadingStatus LoadingStatus { get; set; }

    /// <summary>
    /// Model components
    /// </summary>
    public List<ModelComponent> Components { get; set; } = new();

    /// <summary>
    /// Model capabilities
    /// </summary>
    public ModelCapabilities Capabilities { get; set; } = new();

    /// <summary>
    /// Model requirements
    /// </summary>
    public ModelRequirements Requirements { get; set; } = new();

    /// <summary>
    /// Model metadata
    /// </summary>
    public ModelMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Currently loaded on devices
    /// </summary>
    public List<string> LoadedOnDevices { get; set; } = new();

    /// <summary>
    /// Memory usage information
    /// </summary>
    public ModelMemoryUsage MemoryUsage { get; set; } = new();

    /// <summary>
    /// Model performance metrics
    /// </summary>
    public ModelPerformance Performance { get; set; } = new();

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model component information
/// </summary>
public class ModelComponent
{
    /// <summary>
    /// Component identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Component name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Component type (UNet, VAE, Encoder, etc.)
    /// </summary>
    public ComponentType Type { get; set; }

    /// <summary>
    /// Component file path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Component size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Component loading status
    /// </summary>
    public ComponentStatus Status { get; set; }

    /// <summary>
    /// Whether component is cached in RAM
    /// </summary>
    public bool IsCachedInRAM { get; set; }

    /// <summary>
    /// Whether component is loaded in VRAM
    /// </summary>
    public bool IsLoadedInVRAM { get; set; }

    /// <summary>
    /// Devices where component is loaded
    /// </summary>
    public List<string> LoadedOnDevices { get; set; } = new();

    /// <summary>
    /// Component dependencies
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Component memory usage
    /// </summary>
    public long MemoryUsage { get; set; }
}

/// <summary>
/// Model capabilities
/// </summary>
public class ModelCapabilities
{
    /// <summary>
    /// Supported inference types
    /// </summary>
    public List<string> SupportedInferenceTypes { get; set; } = new();

    /// <summary>
    /// Maximum image resolution
    /// </summary>
    public ImageResolution MaxResolution { get; set; } = new();

    /// <summary>
    /// Supported precision modes
    /// </summary>
    public List<PrecisionMode> SupportedPrecisions { get; set; } = new();

    /// <summary>
    /// Supports LoRA adapters
    /// </summary>
    public bool SupportsLoRA { get; set; }

    /// <summary>
    /// Supports ControlNet
    /// </summary>
    public bool SupportsControlNet { get; set; }

    /// <summary>
    /// Supports inpainting
    /// </summary>
    public bool SupportsInpainting { get; set; }

    /// <summary>
    /// Supports image-to-image
    /// </summary>
    public bool SupportsImage2Image { get; set; }

    /// <summary>
    /// Batch size limits
    /// </summary>
    public BatchSizeLimits BatchLimits { get; set; } = new();
}

/// <summary>
/// Model requirements
/// </summary>
public class ModelRequirements
{
    /// <summary>
    /// Minimum VRAM required in bytes
    /// </summary>
    public long MinVRAM { get; set; }

    /// <summary>
    /// Recommended VRAM in bytes
    /// </summary>
    public long RecommendedVRAM { get; set; }

    /// <summary>
    /// Minimum RAM required in bytes
    /// </summary>
    public long MinRAM { get; set; }

    /// <summary>
    /// Supported device types
    /// </summary>
    public List<DeviceType> SupportedDeviceTypes { get; set; } = new();

    /// <summary>
    /// Required compute capability
    /// </summary>
    public double MinComputeCapability { get; set; }

    /// <summary>
    /// Required driver versions
    /// </summary>
    public Dictionary<string, string> RequiredDriverVersions { get; set; } = new();
}

/// <summary>
/// Model metadata
/// </summary>
public class ModelMetadata
{
    /// <summary>
    /// Model author/creator
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Model license
    /// </summary>
    public string License { get; set; } = string.Empty;

    /// <summary>
    /// Model tags
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Model creation date
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// Model source URL
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Training parameters
    /// </summary>
    public Dictionary<string, object> TrainingParameters { get; set; } = new();

    /// <summary>
    /// Model configuration
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Model memory usage information
/// </summary>
public class ModelMemoryUsage
{
    /// <summary>
    /// Total RAM usage in bytes
    /// </summary>
    public long RAMUsage { get; set; }

    /// <summary>
    /// Total VRAM usage in bytes
    /// </summary>
    public long VRAMUsage { get; set; }

    /// <summary>
    /// Peak memory usage in bytes
    /// </summary>
    public long PeakUsage { get; set; }

    /// <summary>
    /// Memory usage by component
    /// </summary>
    public Dictionary<string, long> ComponentUsage { get; set; } = new();

    /// <summary>
    /// Memory usage by device
    /// </summary>
    public Dictionary<string, long> DeviceUsage { get; set; } = new();
}

/// <summary>
/// Model performance metrics
/// </summary>
public class ModelPerformance
{
    /// <summary>
    /// Average loading time in milliseconds
    /// </summary>
    public double AverageLoadingTime { get; set; }

    /// <summary>
    /// Average inference time in milliseconds
    /// </summary>
    public double AverageInferenceTime { get; set; }

    /// <summary>
    /// Inference throughput (images per second)
    /// </summary>
    public double InferenceThroughput { get; set; }

    /// <summary>
    /// Total inference count
    /// </summary>
    public int TotalInferences { get; set; }

    /// <summary>
    /// Performance by device
    /// </summary>
    public Dictionary<string, DevicePerformance> DevicePerformance { get; set; } = new();
}

/// <summary>
/// Device-specific performance metrics
/// </summary>
public class DevicePerformance
{
    /// <summary>
    /// Average inference time on this device
    /// </summary>
    public double AverageInferenceTime { get; set; }

    /// <summary>
    /// Throughput on this device
    /// </summary>
    public double Throughput { get; set; }

    /// <summary>
    /// Inference count on this device
    /// </summary>
    public int InferenceCount { get; set; }
}

/// <summary>
/// Batch size limits
/// </summary>
public class BatchSizeLimits
{
    /// <summary>
    /// Minimum batch size
    /// </summary>
    public int Min { get; set; } = 1;

    /// <summary>
    /// Maximum batch size
    /// </summary>
    public int Max { get; set; } = 1;

    /// <summary>
    /// Recommended batch size
    /// </summary>
    public int Recommended { get; set; } = 1;
}

/// <summary>
/// Model status enumeration
/// </summary>
public enum ModelStatus
{
    Unknown = 0,
    Available = 1,
    Loading = 2,
    Loaded = 3,
    Unloading = 4,
    Error = 5,
    Missing = 6,
    Corrupted = 7
}

/// <summary>
/// Model loading status enumeration
/// </summary>
public enum ModelLoadingStatus
{
    NotLoaded = 0,
    Loading = 1,
    Loaded = 2,
    Unloading = 3,
    Failed = 4,
    Cached = 5
}

/// <summary>
/// Component type enumeration
/// </summary>
public enum ComponentType
{
    Unknown = 0,
    UNet = 1,
    VAE = 2,
    TextEncoder = 3,
    TextEncoder2 = 4,
    Tokenizer = 5,
    Scheduler = 6,
    ControlNet = 7,
    LoRA = 8,
    SafetyChecker = 9
}

/// <summary>
/// Component status enumeration
/// </summary>
public enum ComponentStatus
{
    Unknown = 0,
    Available = 1,
    Loading = 2,
    Loaded = 3,
    Cached = 4,
    Unloading = 5,
    Error = 6
}

/// <summary>
/// Precision mode enumeration
/// </summary>
public enum PrecisionMode
{
    Float32 = 0,
    Float16 = 1,
    Int8 = 2
}
