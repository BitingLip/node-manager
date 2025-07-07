using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Training;

public interface ITrainingService
{
    /// <summary>
    /// Start a new training session
    /// </summary>
    Task<StartTrainingResponse> StartTrainingAsync(StartTrainingRequest request);
    
    /// <summary>
    /// Get the status of a training session
    /// </summary>
    Task<TrainingStatusResponse?> GetTrainingStatusAsync(string sessionId);
    
    /// <summary>
    /// Get training history for a session
    /// </summary>
    Task<TrainingHistoryResponse?> GetTrainingHistoryAsync(string sessionId);
    
    /// <summary>
    /// List all training sessions
    /// </summary>
    Task<TrainingSessionListResponse> GetTrainingSessionsAsync(TrainingState? stateFilter = null);
    
    /// <summary>
    /// Pause a running training session
    /// </summary>
    Task<PauseTrainingResponse> PauseTrainingAsync(string sessionId, bool saveCheckpoint = true);
    
    /// <summary>
    /// Resume a paused training session
    /// </summary>
    Task<ResumeTrainingResponse> ResumeTrainingAsync(string sessionId, string? checkpointPath = null);
    
    /// <summary>
    /// Stop a training session
    /// </summary>
    Task<StopTrainingResponse> StopTrainingAsync(string sessionId, bool saveFinalModel = true, string? finalModelPath = null);
    
    /// <summary>
    /// Get real-time training metrics
    /// </summary>
    Task<TrainingMetrics?> GetCurrentMetricsAsync(string sessionId);
    
    /// <summary>
    /// Cleanup completed or failed training sessions
    /// </summary>
    Task<bool> CleanupSessionAsync(string sessionId);
    
    /// <summary>
    /// Get training session by ID
    /// </summary>
    Task<TrainingSession?> GetSessionAsync(string sessionId);
    
    /// <summary>
    /// Check if a training session exists
    /// </summary>
    Task<bool> SessionExistsAsync(string sessionId);

    /// <summary>
    /// Start SDXL fine-tuning training with enhanced capabilities
    /// </summary>
    /// <param name="request">SDXL fine-tuning request</param>
    Task<StartSDXLTrainingResponse> StartSDXLFineTuningAsync(StartSDXLFineTuningRequest request);

    /// <summary>
    /// Get SDXL training capabilities and system requirements
    /// </summary>
    Task<SDXLTrainingCapabilities> GetSDXLTrainingCapabilitiesAsync();

    /// <summary>
    /// Get SDXL training templates and presets
    /// </summary>
    Task<List<SDXLTrainingTemplate>> GetSDXLTrainingTemplatesAsync();

    /// <summary>
    /// Validate SDXL training data and configuration
    /// </summary>
    /// <param name="request">SDXL data validation request</param>
    Task<SDXLDataValidationResult> ValidateSDXLTrainingDataAsync(ValidateSDXLDataRequest request);

    /// <summary>
    /// Get optimal SDXL training settings based on available resources
    /// </summary>
    /// <param name="request">Resource optimization request</param>
    Task<SDXLTrainingOptimization> GetOptimalSDXLSettingsAsync(OptimizeSDXLTrainingRequest request);

    /// <summary>
    /// Get enhanced training metrics with SDXL-specific information
    /// </summary>
    /// <param name="sessionId">Training session ID</param>
    Task<EnhancedTrainingMetrics?> GetEnhancedMetricsAsync(string sessionId);

    /// <summary>
    /// Estimate SDXL training duration and resource requirements
    /// </summary>
    /// <param name="request">Training estimation request</param>
    Task<SDXLTrainingEstimation> EstimateSDXLTrainingAsync(EstimateSDXLTrainingRequest request);

    /// <summary>
    /// Get training progress with detailed SDXL pipeline information
    /// </summary>
    /// <param name="sessionId">Training session ID</param>
    Task<SDXLTrainingProgress?> GetSDXLTrainingProgressAsync(string sessionId);

    /// <summary>
    /// Batch process multiple SDXL training sessions
    /// </summary>
    /// <param name="requests">List of SDXL training requests</param>
    Task<BatchSDXLTrainingResponse> BatchStartSDXLTrainingAsync(List<StartSDXLFineTuningRequest> requests);

    /// <summary>
    /// Get training resource utilization and optimization recommendations
    /// </summary>
    Task<TrainingResourceAnalysis> GetTrainingResourceAnalysisAsync();

    /// <summary>
    /// Auto-optimize training settings based on current system state
    /// </summary>
    /// <param name="sessionId">Training session ID</param>
    Task<TrainingOptimizationResult> AutoOptimizeTrainingAsync(string sessionId);
}

/// <summary>
/// Response for SDXL training startup
/// </summary>
public class StartSDXLTrainingResponse
{
    public bool Success { get; set; }
    public string? SessionId { get; set; }
    public string? Message { get; set; }
    public string TrainingName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public string EstimatedDuration { get; set; } = string.Empty;
    public string CheckStatusUrl { get; set; } = string.Empty;
    public SDXLTrainingConfiguration Configuration { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// SDXL training system capabilities
/// </summary>
public class SDXLTrainingCapabilities
{
    public List<string> SupportedTechniques { get; set; } = new();
    public SystemRequirements MinimumRequirements { get; set; } = new();
    public SystemRequirements RecommendedRequirements { get; set; } = new();
    public List<int> SupportedResolutions { get; set; } = new();
    public List<string> SupportedOptimizers { get; set; } = new();
    public List<string> SupportedSchedulers { get; set; } = new();
    public Dictionary<string, string> EstimatedTrainingTimes { get; set; } = new();
    public bool HasSDXLCapableGPU { get; set; }
    public List<string> AvailableGPUs { get; set; } = new();
    public long AvailableVRAM { get; set; }
    public long AvailableDiskSpace { get; set; }
}

/// <summary>
/// System requirements for SDXL training
/// </summary>
public class SystemRequirements
{
    public int VramGB { get; set; }
    public int DiskSpaceGB { get; set; }
    public int RamGB { get; set; }
    public string? RecommendedGPU { get; set; }
}

/// <summary>
/// SDXL training template/preset
/// </summary>
public class SDXLTrainingTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SDXLTrainingTechnique Technique { get; set; }
    public int RecommendedImages { get; set; }
    public SDXLTrainingSettings Settings { get; set; } = new();
    public List<string> SuitableFor { get; set; } = new();
    public string DifficultyLevel { get; set; } = string.Empty;
}

/// <summary>
/// SDXL training settings
/// </summary>
public class SDXLTrainingSettings
{
    public double LearningRate { get; set; }
    public int BatchSize { get; set; }
    public int MaxEpochs { get; set; }
    public int LoraRank { get; set; }
    public int LoraAlpha { get; set; }
    public int Resolution { get; set; }
    public bool EnableTextEncoderTraining { get; set; }
    public bool EnableUnetTraining { get; set; }
    public string Optimizer { get; set; } = string.Empty;
    public string Scheduler { get; set; } = string.Empty;
}

/// <summary>
/// SDXL data validation result
/// </summary>
public class SDXLDataValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public SDXLDataStatistics? Statistics { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public Dictionary<string, object> DetailedAnalysis { get; set; } = new();
}

/// <summary>
/// SDXL training data statistics
/// </summary>
public class SDXLDataStatistics
{
    public int ImageCount { get; set; }
    public int TextFileCount { get; set; }
    public long AverageFileSize { get; set; }
    public int RecommendedMinImages { get; set; }
    public List<string> ImageFormats { get; set; } = new();
    public List<int> ImageResolutions { get; set; } = new();
    public double CaptionCoverage { get; set; }
}

/// <summary>
/// Request for optimizing SDXL training settings
/// </summary>
public class OptimizeSDXLTrainingRequest
{
    public string? BaseModelPath { get; set; }
    public string TrainingDataPath { get; set; } = string.Empty;
    public SDXLTrainingTechnique PreferredTechnique { get; set; }
    public string? TargetGPU { get; set; }
    public int MaxTrainingTime { get; set; } // in hours
    public string Priority { get; set; } = "balanced"; // speed, quality, memory
}

/// <summary>
/// SDXL training optimization recommendations
/// </summary>
public class SDXLTrainingOptimization
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public SDXLTrainingSettings OptimalSettings { get; set; } = new();
    public string EstimatedDuration { get; set; } = string.Empty;
    public long EstimatedVRAMUsage { get; set; }
    public List<string> Optimizations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Enhanced training metrics with SDXL-specific information
/// </summary>
public class EnhancedTrainingMetrics
{
    public TrainingMetrics BaseMetrics { get; set; } = new();
    public SDXLSpecificMetrics SDXLMetrics { get; set; } = new();
    public ResourceUtilization ResourceUsage { get; set; } = new();
    public List<string> Alerts { get; set; } = new();
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// SDXL-specific training metrics
/// </summary>
public class SDXLSpecificMetrics
{
    public double? TextEncoderLoss { get; set; }
    public double? UNetLoss { get; set; }
    public double? VAELoss { get; set; }
    public double? LoRAMagnitude { get; set; }
    public double? ClipScore { get; set; }
    public double? FIDScore { get; set; }
    public int GeneratedSamples { get; set; }
    public List<string> SamplePaths { get; set; } = new();
}

/// <summary>
/// Resource utilization metrics
/// </summary>
public class ResourceUtilization
{
    public double GPUUtilization { get; set; }
    public long VRAMUsage { get; set; }
    public long VRAMTotal { get; set; }
    public double CPUUtilization { get; set; }
    public long RAMUsage { get; set; }
    public double DiskIORate { get; set; }
    public double NetworkIORate { get; set; }
    public double Temperature { get; set; }
    public double PowerUsage { get; set; }
}

/// <summary>
/// Request for estimating SDXL training
/// </summary>
public class EstimateSDXLTrainingRequest
{
    public string TrainingDataPath { get; set; } = string.Empty;
    public SDXLTrainingTechnique Technique { get; set; }
    public SDXLTrainingSettings Settings { get; set; } = new();
    public string? TargetGPU { get; set; }
}

/// <summary>
/// SDXL training estimation results
/// </summary>
public class SDXLTrainingEstimation
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public long EstimatedVRAMUsage { get; set; }
    public long EstimatedDiskUsage { get; set; }
    public double EstimatedCost { get; set; } // if applicable
    public List<string> Requirements { get; set; } = new();
    public Dictionary<string, TimeSpan> PhaseEstimates { get; set; } = new();
    public double ConfidenceLevel { get; set; }
}

/// <summary>
/// SDXL training progress with detailed pipeline information
/// </summary>
public class SDXLTrainingProgress
{
    public string SessionId { get; set; } = string.Empty;
    public SDXLTrainingConfiguration Configuration { get; set; } = new();
    public SDXLTrainingPhase CurrentPhase { get; set; }
    public double OverallProgress { get; set; }
    public Dictionary<string, double> PhaseProgress { get; set; } = new();
    public EnhancedTrainingMetrics CurrentMetrics { get; set; } = new();
    public List<string> RecentLogs { get; set; } = new();
    public DateTime LastUpdate { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
}

/// <summary>
/// SDXL training configuration
/// </summary>
public class SDXLTrainingConfiguration
{
    public string TrainingName { get; set; } = string.Empty;
    public SDXLTrainingTechnique Technique { get; set; }
    public SDXLTrainingSettings Settings { get; set; } = new();
    public List<string> ModelPaths { get; set; } = new();
    public string DataPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public Dictionary<string, object> AdvancedOptions { get; set; } = new();
}

/// <summary>
/// SDXL training phases
/// </summary>
public enum SDXLTrainingPhase
{
    Initialization,
    DataLoading,
    ModelLoading,
    Training,
    Validation,
    Checkpoint,
    Finalization,
    Completed,
    Failed
}

/// <summary>
/// Batch SDXL training response
/// </summary>
public class BatchSDXLTrainingResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<StartSDXLTrainingResponse> Results { get; set; } = new();
    public int TotalRequested { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public DateTime StartedAt { get; set; }
    public string EstimatedCompletionTime { get; set; } = string.Empty;
}

/// <summary>
/// Training resource analysis
/// </summary>
public class TrainingResourceAnalysis
{
    public DateTime AnalysisTime { get; set; }
    public List<ResourceAnalysisResult> GPUAnalysis { get; set; } = new();
    public SystemResourceSummary SystemSummary { get; set; } = new();
    public List<ResourceOptimizationRecommendation> Recommendations { get; set; } = new();
    public TrainingCapacityEstimate CapacityEstimate { get; set; } = new();
}

/// <summary>
/// Resource analysis result for individual GPU
/// </summary>
public class ResourceAnalysisResult
{
    public string GPUId { get; set; } = string.Empty;
    public string GPUName { get; set; } = string.Empty;
    public ResourceUtilization CurrentUtilization { get; set; } = new();
    public bool IsSDXLCapable { get; set; }
    public int MaxConcurrentTraining { get; set; }
    public List<string> OptimalTechniques { get; set; } = new();
    public string Status { get; set; } = string.Empty; // available, busy, error
}

/// <summary>
/// System resource summary
/// </summary>
public class SystemResourceSummary
{
    public int TotalGPUs { get; set; }
    public int AvailableGPUs { get; set; }
    public long TotalVRAM { get; set; }
    public long AvailableVRAM { get; set; }
    public long TotalRAM { get; set; }
    public long AvailableRAM { get; set; }
    public long TotalDiskSpace { get; set; }
    public long AvailableDiskSpace { get; set; }
    public int ActiveTrainingSessions { get; set; }
}

/// <summary>
/// Resource optimization recommendation
/// </summary>
public class ResourceOptimizationRecommendation
{
    public string Type { get; set; } = string.Empty; // memory, performance, efficiency
    public string Description { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public double ExpectedImprovement { get; set; }
    public int Priority { get; set; }
    public List<string> Requirements { get; set; } = new();
}

/// <summary>
/// Training capacity estimate
/// </summary>
public class TrainingCapacityEstimate
{
    public int MaxConcurrentLoRATraining { get; set; }
    public int MaxConcurrentDreamBoothTraining { get; set; }
    public int MaxConcurrentFullFineTune { get; set; }
    public string BottleneckComponent { get; set; } = string.Empty;
    public List<string> ScalingRecommendations { get; set; } = new();
}

/// <summary>
/// Training optimization result
/// </summary>
public class TrainingOptimizationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public List<string> AppliedOptimizations { get; set; } = new();
    public double ExpectedSpeedup { get; set; }
    public long MemorySavings { get; set; }
    public SDXLTrainingSettings OptimizedSettings { get; set; } = new();
    public DateTime OptimizedAt { get; set; }
}
