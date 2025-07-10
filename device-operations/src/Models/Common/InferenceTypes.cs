namespace DeviceOperations.Models.Common;

/// <summary>
/// Inference capabilities model
/// </summary>
public class InferenceCapabilities
{
    public List<string> SupportedInferenceTypes { get; set; } = new();
    public List<string> SupportedPrecisions { get; set; } = new();
    public int MaxBatchSize { get; set; }
    public int MaxConcurrentInferences { get; set; }
    public ImageResolution MaxResolution { get; set; } = new();
}

/// <summary>
/// Inference type information model
/// </summary>
public class InferenceTypeInfo
{
    public string TypeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> RequiredCapabilities { get; set; } = new();
    public Dictionary<string, object> DefaultParameters { get; set; } = new();
}
