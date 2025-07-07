using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Workers;

/// <summary>
/// Service for managing a single PyTorch DirectML worker process for one GPU
/// </summary>
public interface IPyTorchWorkerService : IDisposable
{
    /// <summary>
    /// GPU ID this worker is assigned to
    /// </summary>
    string GpuId { get; }

    /// <summary>
    /// Whether the worker is initialized and ready
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Whether the worker has a model loaded
    /// </summary>
    bool HasModelLoaded { get; }

    /// <summary>
    /// Current model ID if loaded
    /// </summary>
    string? CurrentModelId { get; }

    /// <summary>
    /// Initialize the worker for the assigned GPU
    /// </summary>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Load a model from file path to this GPU's VRAM
    /// </summary>
    /// <param name="modelPath">Path to model file</param>
    /// <param name="modelType">Type of model</param>
    /// <param name="modelId">Optional model ID</param>
    Task<LoadModelResponse> LoadModelAsync(string modelPath, ModelType modelType, string? modelId = null);

    /// <summary>
    /// Load a model from shared cache to this GPU's VRAM
    /// </summary>
    /// <param name="modelId">Cached model ID</param>
    Task<LoadModelResponse> LoadModelFromCacheAsync(string modelId);

    /// <summary>
    /// Unload the current model from VRAM
    /// </summary>
    Task<bool> UnloadModelAsync();

    /// <summary>
    /// Run inference on the loaded model
    /// </summary>
    /// <param name="request">Inference parameters</param>
    Task<InferenceResponse> RunInferenceAsync(InferenceRequest request);

    /// <summary>
    /// Get worker status and statistics
    /// </summary>
    Task<WorkerStatusResponse> GetStatusAsync();

    /// <summary>
    /// Get information about the currently loaded model
    /// </summary>
    Task<LoadedModelInfo?> GetModelInfoAsync();

    /// <summary>
    /// Cleanup memory without unloading model
    /// </summary>
    Task<bool> CleanupMemoryAsync();

    /// <summary>
    /// Check if worker is healthy and responsive
    /// </summary>
    Task<bool> IsHealthyAsync();

    /// <summary>
    /// Generate images using enhanced SDXL pipeline with full control
    /// </summary>
    Task<EnhancedSDXLResponse> GenerateEnhancedSDXLAsync(EnhancedSDXLRequest request);

    /// <summary>
    /// Get worker capabilities and supported features
    /// </summary>
    Task<WorkerCapabilitiesResponse> GetCapabilitiesAsync();

    /// <summary>
    /// Validate an enhanced SDXL request
    /// </summary>
    Task<PromptValidationResponse> ValidateRequestAsync(EnhancedSDXLRequest request);
}

/// <summary>
/// Status response for a worker
/// </summary>
public class WorkerStatusResponse
{
    public string GpuId { get; set; } = string.Empty;
    public bool IsInitialized { get; set; }
    public bool HasModelLoaded { get; set; }
    public string? CurrentModelId { get; set; }
    public string? CurrentModelName { get; set; }
    public DateTime? ModelLoadedAt { get; set; }
    public long? ModelSizeBytes { get; set; }
    public WorkerState State { get; set; }
    public int ActiveInferenceSessions { get; set; }
    public DateTime LastActivity { get; set; }
    public WorkerMemoryStats? MemoryStats { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Worker state enumeration
/// </summary>
public enum WorkerState
{
    Uninitialized,
    Initializing,
    Ready,
    Loading,
    Loaded,
    Running,
    Error,
    Disposed
}

/// <summary>
/// Memory statistics for a worker
/// </summary>
public class WorkerMemoryStats
{
    public long VramUsedBytes { get; set; }
    public long VramTotalBytes { get; set; }
    public long VramAvailableBytes { get; set; }
    public double VramUsagePercentage { get; set; }
    public long RamUsedBytes { get; set; }
}
