using DeviceOperations.Models.Common;

namespace DeviceOperations.Models.Requests
{
    /// <summary>
    /// Request model for device health check
    /// </summary>
    public class PostDeviceHealthRequest
    {
        /// <summary>
        /// Health check type
        /// </summary>
        public string HealthCheckType { get; set; } = "comprehensive";

        /// <summary>
        /// Include performance metrics
        /// </summary>
        public bool IncludePerformanceMetrics { get; set; } = true;
    }

    /// <summary>
    /// Request model for device reset
    /// </summary>
    public class PostDeviceResetRequest
    {
        /// <summary>
        /// Reset type
        /// </summary>
        public DeviceResetType ResetType { get; set; }

        /// <summary>
        /// Force reset even if busy
        /// </summary>
        public bool Force { get; set; } = false;
    }

    /// <summary>
    /// Request model for device benchmark
    /// </summary>
    public class PostDeviceBenchmarkRequest
    {
        /// <summary>
        /// Benchmark type
        /// </summary>
        public BenchmarkType BenchmarkType { get; set; }

        /// <summary>
        /// Duration for benchmark in seconds
        /// </summary>
        public int DurationSeconds { get; set; } = 60;
    }

    /// <summary>
    /// Request model for device optimization
    /// </summary>
    public class PostDeviceOptimizeRequest
    {
        /// <summary>
        /// Optimization target
        /// </summary>
        public OptimizationTarget Target { get; set; }

        /// <summary>
        /// Apply optimizations automatically
        /// </summary>
        public bool AutoApply { get; set; } = false;
    }

    /// <summary>
    /// Request model for updating device configuration
    /// </summary>
    public class PutDeviceConfigRequest
    {
        /// <summary>
        /// Configuration updates
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();

        /// <summary>
        /// Validate before applying
        /// </summary>
        public bool ValidateOnly { get; set; } = false;
    }

    // Memory Request Models
    public class PostMemoryAllocateRequest
    {
        public long SizeBytes { get; set; }
        public string MemoryType { get; set; } = "GPU";
    }

    public class DeleteMemoryDeallocateRequest
    {
        public string AllocationId { get; set; } = string.Empty;
        public bool Force { get; set; } = false;
    }

    public class PostMemoryTransferRequest
    {
        public string SourceDeviceId { get; set; } = string.Empty;
        public string TargetDeviceId { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    public class PostMemoryCopyRequest
    {
        public string SourceId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
    }

    public class PostMemoryClearRequest
    {
        public string MemoryType { get; set; } = "all";
        public bool Force { get; set; } = false;
    }

    public class PostMemoryOptimizeRequest
    {
        public OptimizationTarget Target { get; set; }
    }

    public class PostMemoryDefragmentRequest
    {
        public string MemoryType { get; set; } = "GPU";
    }

    // Model Request Models
    public class PostModelLoadRequest
    {
        public string ModelPath { get; set; } = string.Empty;
        public ModelType ModelType { get; set; }
        public Guid DeviceId { get; set; }
        public string? LoadingStrategy { get; set; }
    }

    public class PostModelUnloadRequest
    {
        public bool Force { get; set; } = false;
        public Guid DeviceId { get; set; }
    }

    public class PostModelValidateRequest
    {
        public string ValidationLevel { get; set; } = "basic";
        public Guid DeviceId { get; set; }
    }

    public class PostModelOptimizeRequest
    {
        public OptimizationTarget Target { get; set; }
        public Guid DeviceId { get; set; }
        public OptimizationTarget OptimizationTarget { get; set; }
    }

    public class PostModelBenchmarkRequest
    {
        public BenchmarkType BenchmarkType { get; set; }
        public Guid DeviceId { get; set; }
    }

    public class PostModelSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    public class PutModelMetadataRequest
    {
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    // Inference Request Models
    public class PostInferenceExecuteRequest
    {
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string ModelId { get; set; } = string.Empty;
        public InferenceType InferenceType { get; set; }
    }

    public class PostInferenceExecuteDeviceRequest
    {
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string ModelId { get; set; } = string.Empty;
        public InferenceType InferenceType { get; set; }
    }

    public class PostInferenceValidateRequest
    {
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string ModelId { get; set; } = string.Empty;
        public InferenceType InferenceType { get; set; }
    }

    public class DeleteInferenceSessionRequest
    {
        public bool Force { get; set; } = false;
    }

    // Postprocessing Request Models - Updated with controller-expected properties
    public class PostPostprocessingApplyRequest
    {
        public string InputImagePath { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string? ModelName { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }

    public class PostPostprocessingUpscaleRequest
    {
        public string InputImagePath { get; set; } = string.Empty;
        public int ScaleFactor { get; set; } = 2;
        public string? ModelName { get; set; }
    }

    public class PostPostprocessingEnhanceRequest
    {
        public string InputImagePath { get; set; } = string.Empty;
        public string EnhancementType { get; set; } = "general";
        public float Strength { get; set; } = 0.8f;
    }

    public class PostPostprocessingFaceRestoreRequest
    {
        public string InputImagePath { get; set; } = string.Empty;
        public string? ModelName { get; set; }
        public float Strength { get; set; } = 0.8f;
    }

    public class PostPostprocessingStyleTransferRequest
    {
        public string InputImagePath { get; set; } = string.Empty;
        public string StyleImagePath { get; set; } = string.Empty;
        public string? ModelName { get; set; }
        public float StyleStrength { get; set; } = 0.8f;
    }

    public class PostPostprocessingBackgroundRemoveRequest
    {
        public string InputImagePath { get; set; } = string.Empty;
        public string? ModelName { get; set; }
    }

    public class PostPostprocessingColorCorrectRequest
    {
        public string InputImagePath { get; set; } = string.Empty;
        public string CorrectionType { get; set; } = "auto";
        public float Intensity { get; set; } = 0.5f;
    }

    public class PostPostprocessingBatchRequest
    {
        public List<string> InputImagePaths { get; set; } = new();
        public string Operation { get; set; } = string.Empty;
        public int? MaxConcurrency { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }
}
