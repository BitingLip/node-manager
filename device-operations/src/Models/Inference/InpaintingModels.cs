namespace DeviceOperations.Models.Inference
{
    /// <summary>
    /// Request model for image inpainting operations
    /// </summary>
    public class PostInferenceInpaintingRequest
    {
        /// <summary>
        /// Session identifier for tracking
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Base image for inpainting (base64 encoded)
        /// </summary>
        public string BaseImage { get; set; } = string.Empty;

        /// <summary>
        /// Inpainting mask (base64 encoded, white = inpaint, black = preserve)
        /// </summary>
        public string InpaintingMask { get; set; } = string.Empty;

        /// <summary>
        /// Text prompt for inpainting guidance
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Negative prompt to avoid certain features
        /// </summary>
        public string? NegativePrompt { get; set; }

        /// <summary>
        /// Model to use for inpainting
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Device to use for processing
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Inpainting strength (0.0 to 1.0)
        /// </summary>
        public double InpaintingStrength { get; set; } = 0.75;

        /// <summary>
        /// Number of inference steps
        /// </summary>
        public int Steps { get; set; } = 20;

        /// <summary>
        /// Guidance scale for prompt adherence
        /// </summary>
        public double GuidanceScale { get; set; } = 7.5;

        /// <summary>
        /// Random seed for reproducibility
        /// </summary>
        public long? Seed { get; set; }

        /// <summary>
        /// Output image width
        /// </summary>
        public int Width { get; set; } = 512;

        /// <summary>
        /// Output image height
        /// </summary>
        public int Height { get; set; } = 512;

        /// <summary>
        /// Inpainting method to use
        /// </summary>
        public InpaintingMethod Method { get; set; } = InpaintingMethod.StableDiffusion;

        /// <summary>
        /// Quality level for processing
        /// </summary>
        public InpaintingQuality Quality { get; set; } = InpaintingQuality.Standard;

        /// <summary>
        /// Edge blending mode for seamless integration
        /// </summary>
        public EdgeBlendingMode EdgeBlending { get; set; } = EdgeBlendingMode.Feather;

        /// <summary>
        /// Enable advanced mask processing
        /// </summary>
        public bool EnableAdvancedMaskProcessing { get; set; } = true;

        /// <summary>
        /// Mask blur radius for soft edges
        /// </summary>
        public int MaskBlurRadius { get; set; } = 4;

        /// <summary>
        /// Mask dilation for extending inpaint area
        /// </summary>
        public int MaskDilation { get; set; } = 0;

        /// <summary>
        /// Content-aware fill before inpainting
        /// </summary>
        public bool EnableContentAwareFill { get; set; } = false;

        /// <summary>
        /// Additional parameters for specific models
        /// </summary>
        public Dictionary<string, object> AdditionalParameters { get; set; } = new();
    }

    /// <summary>
    /// Response model for inpainting operations
    /// </summary>
    public class PostInferenceInpaintingResponse
    {
        /// <summary>
        /// Operation success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Generated inpainted image (base64 encoded)
        /// </summary>
        public string? InpaintedImage { get; set; }

        /// <summary>
        /// Processing time in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Quality metrics for the inpainting result
        /// </summary>
        public InpaintingQualityMetrics QualityMetrics { get; set; } = new();

        /// <summary>
        /// Performance metrics for the operation
        /// </summary>
        public InpaintingPerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Used inpainting parameters
        /// </summary>
        public Dictionary<string, object> UsedParameters { get; set; } = new();

        /// <summary>
        /// Error information if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Detailed error code if applicable
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Metadata about the inpainting process
        /// </summary>
        public InpaintingMetadata Metadata { get; set; } = new();
    }

    /// <summary>
    /// Inpainting method enumeration
    /// </summary>
    public enum InpaintingMethod
    {
        StableDiffusion,
        ControlNet,
        LDSR,
        RealESRGAN,
        ESRGAN,
        CodeFormer,
        GFPGAN,
        Custom
    }

    /// <summary>
    /// Inpainting quality level enumeration
    /// </summary>
    public enum InpaintingQuality
    {
        Draft,
        Standard,
        High,
        Ultra,
        Production
    }

    /// <summary>
    /// Edge blending mode for seamless integration
    /// </summary>
    public enum EdgeBlendingMode
    {
        None,
        Feather,
        Gaussian,
        Linear,
        Advanced
    }

    /// <summary>
    /// Quality metrics for inpainting results
    /// </summary>
    public class InpaintingQualityMetrics
    {
        /// <summary>
        /// Seamlessness score (0.0 to 1.0)
        /// </summary>
        public double SeamlessnessScore { get; set; }

        /// <summary>
        /// Color consistency score (0.0 to 1.0)
        /// </summary>
        public double ColorConsistencyScore { get; set; }

        /// <summary>
        /// Texture consistency score (0.0 to 1.0)
        /// </summary>
        public double TextureConsistencyScore { get; set; }

        /// <summary>
        /// Overall quality score (0.0 to 1.0)
        /// </summary>
        public double OverallQualityScore { get; set; }

        /// <summary>
        /// Edge quality score (0.0 to 1.0)
        /// </summary>
        public double EdgeQualityScore { get; set; }

        /// <summary>
        /// Content coherence score (0.0 to 1.0)
        /// </summary>
        public double ContentCoherenceScore { get; set; }
    }

    /// <summary>
    /// Performance metrics for inpainting operations
    /// </summary>
    public class InpaintingPerformanceMetrics
    {
        /// <summary>
        /// Image preprocessing time in milliseconds
        /// </summary>
        public long PreprocessingTimeMs { get; set; }

        /// <summary>
        /// Mask processing time in milliseconds
        /// </summary>
        public long MaskProcessingTimeMs { get; set; }

        /// <summary>
        /// Model inference time in milliseconds
        /// </summary>
        public long InferenceTimeMs { get; set; }

        /// <summary>
        /// Post-processing time in milliseconds
        /// </summary>
        public long PostprocessingTimeMs { get; set; }

        /// <summary>
        /// Total memory usage in MB
        /// </summary>
        public long MemoryUsageMB { get; set; }

        /// <summary>
        /// Peak VRAM usage in MB
        /// </summary>
        public long PeakVRAMUsageMB { get; set; }

        /// <summary>
        /// GPU utilization percentage
        /// </summary>
        public double GPUUtilizationPercent { get; set; }

        /// <summary>
        /// Throughput in pixels per second
        /// </summary>
        public long ThroughputPixelsPerSecond { get; set; }
    }

    /// <summary>
    /// Metadata about the inpainting process
    /// </summary>
    public class InpaintingMetadata
    {
        /// <summary>
        /// Inpainting algorithm used
        /// </summary>
        public string Algorithm { get; set; } = string.Empty;

        /// <summary>
        /// Model version information
        /// </summary>
        public string ModelVersion { get; set; } = string.Empty;

        /// <summary>
        /// Processing device information
        /// </summary>
        public string ProcessingDevice { get; set; } = string.Empty;

        /// <summary>
        /// Mask analysis results
        /// </summary>
        public MaskAnalysisResult MaskAnalysis { get; set; } = new();

        /// <summary>
        /// Applied preprocessing steps
        /// </summary>
        public List<string> PreprocessingSteps { get; set; } = new();

        /// <summary>
        /// Applied postprocessing steps
        /// </summary>
        public List<string> PostprocessingSteps { get; set; } = new();

        /// <summary>
        /// Optimization suggestions
        /// </summary>
        public List<string> OptimizationSuggestions { get; set; } = new();
    }

    /// <summary>
    /// Mask analysis result information
    /// </summary>
    public class MaskAnalysisResult
    {
        /// <summary>
        /// Percentage of image area to inpaint
        /// </summary>
        public double InpaintAreaPercentage { get; set; }

        /// <summary>
        /// Mask complexity score (0.0 to 1.0)
        /// </summary>
        public double ComplexityScore { get; set; }

        /// <summary>
        /// Number of distinct regions in mask
        /// </summary>
        public int RegionCount { get; set; }

        /// <summary>
        /// Average region size in pixels
        /// </summary>
        public int AverageRegionSize { get; set; }

        /// <summary>
        /// Edge density score (0.0 to 1.0)
        /// </summary>
        public double EdgeDensityScore { get; set; }

        /// <summary>
        /// Recommended processing mode
        /// </summary>
        public string RecommendedMode { get; set; } = string.Empty;
    }
}
