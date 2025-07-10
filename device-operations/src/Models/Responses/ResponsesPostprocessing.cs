namespace DeviceOperations.Models.Responses;

using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;

/// <summary>
/// Postprocessing operation responses
/// </summary>
public static class ResponsesPostprocessing
{
    /// <summary>
    /// Response for applying postprocessing
    /// </summary>
    public class ApplyPostprocessingResponse
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Processing success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Processed image results
        /// </summary>
        public List<PostprocessingResult> Results { get; set; } = new();

        /// <summary>
        /// Total processing time in milliseconds
        /// </summary>
        public double ProcessingTimeMs { get; set; }

        /// <summary>
        /// Operations applied
        /// </summary>
        public List<string> OperationsApplied { get; set; } = new();

        /// <summary>
        /// Processing performance metrics
        /// </summary>
        public PostprocessingPerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Quality assessment
        /// </summary>
        public ImageQualityAssessment? QualityAssessment { get; set; }

        /// <summary>
        /// Processing warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Processing message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Processing completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for upscaling an image
    /// </summary>
    public class UpscaleImageResponse
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Upscaling success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Upscaled image result
        /// </summary>
        public UpscaleResult Result { get; set; } = new();

        /// <summary>
        /// Upscaling time in milliseconds
        /// </summary>
        public double UpscalingTimeMs { get; set; }

        /// <summary>
        /// Upscaling performance metrics
        /// </summary>
        public UpscalePerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Quality improvement assessment
        /// </summary>
        public QualityImprovementAssessment QualityImprovement { get; set; } = new();

        /// <summary>
        /// Resource usage during upscaling
        /// </summary>
        public ResourceUsageMetrics ResourceUsage { get; set; } = new();

        /// <summary>
        /// Upscaling message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Upscaling completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for enhancing an image
    /// </summary>
    public class EnhanceImageResponse
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Enhancement success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Enhanced image result
        /// </summary>
        public EnhancementResult Result { get; set; } = new();

        /// <summary>
        /// Enhancement time in milliseconds
        /// </summary>
        public double EnhancementTimeMs { get; set; }

        /// <summary>
        /// Enhancement operations applied
        /// </summary>
        public List<string> OperationsApplied { get; set; } = new();

        /// <summary>
        /// Enhancement performance metrics
        /// </summary>
        public EnhancementPerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Before/after quality comparison
        /// </summary>
        public QualityComparison QualityComparison { get; set; } = new();

        /// <summary>
        /// Enhancement message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Enhancement completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for face restoration
    /// </summary>
    public class RestoreFacesResponse
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Face restoration success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Face restoration result
        /// </summary>
        public FaceRestorationResult Result { get; set; } = new();

        /// <summary>
        /// Restoration time in milliseconds
        /// </summary>
        public double RestorationTimeMs { get; set; }

        /// <summary>
        /// Number of faces detected
        /// </summary>
        public int FacesDetected { get; set; }

        /// <summary>
        /// Number of faces restored
        /// </summary>
        public int FacesRestored { get; set; }

        /// <summary>
        /// Face restoration performance metrics
        /// </summary>
        public FaceRestorationPerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Face quality improvements
        /// </summary>
        public List<FaceQualityImprovement> FaceImprovements { get; set; } = new();

        /// <summary>
        /// Restoration message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Restoration completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for style transfer
    /// </summary>
    public class StyleTransferResponse
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Style transfer success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Style transfer result
        /// </summary>
        public StyleTransferResult Result { get; set; } = new();

        /// <summary>
        /// Style transfer time in milliseconds
        /// </summary>
        public double StyleTransferTimeMs { get; set; }

        /// <summary>
        /// Style transfer performance metrics
        /// </summary>
        public StyleTransferPerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Style similarity assessment
        /// </summary>
        public StyleSimilarityAssessment StyleSimilarity { get; set; } = new();

        /// <summary>
        /// Content preservation assessment
        /// </summary>
        public ContentPreservationAssessment ContentPreservation { get; set; } = new();

        /// <summary>
        /// Style transfer message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Style transfer completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for background removal
    /// </summary>
    public class RemoveBackgroundResponse
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Background removal success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Background removal result
        /// </summary>
        public BackgroundRemovalResult Result { get; set; } = new();

        /// <summary>
        /// Background removal time in milliseconds
        /// </summary>
        public double RemovalTimeMs { get; set; }

        /// <summary>
        /// Background removal performance metrics
        /// </summary>
        public BackgroundRemovalPerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Mask quality assessment
        /// </summary>
        public MaskQualityAssessment MaskQuality { get; set; } = new();

        /// <summary>
        /// Edge quality assessment
        /// </summary>
        public EdgeQualityAssessment EdgeQuality { get; set; } = new();

        /// <summary>
        /// Background removal message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Background removal completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for color correction
    /// </summary>
    public class ColorCorrectionResponse
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Color correction success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Color correction result
        /// </summary>
        public ColorCorrectionResult Result { get; set; } = new();

        /// <summary>
        /// Color correction time in milliseconds
        /// </summary>
        public double CorrectionTimeMs { get; set; }

        /// <summary>
        /// Color operations applied
        /// </summary>
        public List<string> OperationsApplied { get; set; } = new();

        /// <summary>
        /// Color correction performance metrics
        /// </summary>
        public ColorCorrectionPerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Color accuracy assessment
        /// </summary>
        public ColorAccuracyAssessment ColorAccuracy { get; set; } = new();

        /// <summary>
        /// Color correction message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Color correction completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for batch postprocessing
    /// </summary>
    public class BatchPostprocessingResponse
    {
        /// <summary>
        /// Batch operation identifier
        /// </summary>
        public string BatchOperationId { get; set; } = string.Empty;

        /// <summary>
        /// Batch processing success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Batch processing results
        /// </summary>
        public BatchPostprocessingResults Results { get; set; } = new();

        /// <summary>
        /// Total batch processing time in milliseconds
        /// </summary>
        public double TotalProcessingTimeMs { get; set; }

        /// <summary>
        /// Batch performance metrics
        /// </summary>
        public BatchPostprocessingPerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Batch processing summary
        /// </summary>
        public BatchProcessingSummary ProcessingSummary { get; set; } = new();

        /// <summary>
        /// Batch processing message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Batch completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for getting postprocessing operation status
    /// </summary>
    public class GetPostprocessingStatusResponse
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Current operation status
        /// </summary>
        public PostprocessingStatus Status { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double ProgressPercentage { get; set; }

        /// <summary>
        /// Current processing step
        /// </summary>
        public string CurrentStep { get; set; } = string.Empty;

        /// <summary>
        /// Estimated time remaining in seconds
        /// </summary>
        public double? EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Operation details
        /// </summary>
        public PostprocessingOperationDetails? Details { get; set; }

        /// <summary>
        /// Resource usage
        /// </summary>
        public ResourceUsageMetrics? ResourceUsage { get; set; }

        /// <summary>
        /// Last status update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}

/// <summary>
/// Postprocessing result base class
/// </summary>
public class PostprocessingResult
{
    /// <summary>
    /// Result identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Output image data (base64 encoded)
    /// </summary>
    public string? ImageData { get; set; }

    /// <summary>
    /// Output image file path
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Output image metadata
    /// </summary>
    public ImageMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Processing quality score (0-100)
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// Result creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Upscale result
/// </summary>
public class UpscaleResult : PostprocessingResult
{
    /// <summary>
    /// Original image dimensions
    /// </summary>
    public ImageDimensions OriginalDimensions { get; set; } = new();

    /// <summary>
    /// Upscaled image dimensions
    /// </summary>
    public ImageDimensions UpscaledDimensions { get; set; } = new();

    /// <summary>
    /// Actual scale factor achieved
    /// </summary>
    public double ActualScaleFactor { get; set; }

    /// <summary>
    /// Upscaling algorithm used
    /// </summary>
    public string AlgorithmUsed { get; set; } = string.Empty;

    /// <summary>
    /// Model used for upscaling
    /// </summary>
    public string? ModelUsed { get; set; }
}

/// <summary>
/// Enhancement result
/// </summary>
public class EnhancementResult : PostprocessingResult
{
    /// <summary>
    /// Enhancement operations applied
    /// </summary>
    public List<AppliedEnhancement> EnhancementsApplied { get; set; } = new();

    /// <summary>
    /// Overall enhancement strength
    /// </summary>
    public double OverallStrength { get; set; }

    /// <summary>
    /// Quality improvement score
    /// </summary>
    public double QualityImprovement { get; set; }
}

/// <summary>
/// Face restoration result
/// </summary>
public class FaceRestorationResult : PostprocessingResult
{
    /// <summary>
    /// Face restoration details
    /// </summary>
    public List<RestoredFace> RestoredFaces { get; set; } = new();

    /// <summary>
    /// Restoration model used
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>
    /// Background enhancement applied
    /// </summary>
    public bool BackgroundEnhanced { get; set; }

    /// <summary>
    /// Post-upscaling applied
    /// </summary>
    public UpscaleFactor? PostUpscaleApplied { get; set; }
}

/// <summary>
/// Style transfer result
/// </summary>
public class StyleTransferResult : PostprocessingResult
{
    /// <summary>
    /// Style image metadata
    /// </summary>
    public ImageMetadata StyleImageMetadata { get; set; } = new();

    /// <summary>
    /// Style strength applied
    /// </summary>
    public double StyleStrengthApplied { get; set; }

    /// <summary>
    /// Content preservation level
    /// </summary>
    public double ContentPreservationLevel { get; set; }

    /// <summary>
    /// Style transfer model used
    /// </summary>
    public string? ModelUsed { get; set; }
}

/// <summary>
/// Background removal result
/// </summary>
public class BackgroundRemovalResult : PostprocessingResult
{
    /// <summary>
    /// Mask image data (base64 encoded)
    /// </summary>
    public string? MaskData { get; set; }

    /// <summary>
    /// Confidence mask data
    /// </summary>
    public string? ConfidenceMask { get; set; }

    /// <summary>
    /// Alpha matting applied
    /// </summary>
    public bool AlphaMattingApplied { get; set; }

    /// <summary>
    /// Background removal model used
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>
    /// Background color applied
    /// </summary>
    public string? BackgroundColorApplied { get; set; }
}

/// <summary>
/// Color correction result
/// </summary>
public class ColorCorrectionResult : PostprocessingResult
{
    /// <summary>
    /// Color corrections applied
    /// </summary>
    public List<AppliedColorCorrection> CorrectionsApplied { get; set; } = new();

    /// <summary>
    /// Auto correction applied
    /// </summary>
    public bool AutoCorrectionApplied { get; set; }

    /// <summary>
    /// Color space used
    /// </summary>
    public string ColorSpaceUsed { get; set; } = string.Empty;

    /// <summary>
    /// Reference image used
    /// </summary>
    public bool ReferenceImageUsed { get; set; }
}

/// <summary>
/// Batch postprocessing results
/// </summary>
public class BatchPostprocessingResults
{
    /// <summary>
    /// Individual processing results
    /// </summary>
    public List<PostprocessingResult> Results { get; set; } = new();

    /// <summary>
    /// Total images processed
    /// </summary>
    public int TotalImagesProcessed { get; set; }

    /// <summary>
    /// Successful processes count
    /// </summary>
    public int SuccessfulProcesses { get; set; }

    /// <summary>
    /// Failed processes count
    /// </summary>
    public int FailedProcesses { get; set; }

    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Batch processing efficiency
    /// </summary>
    public double ProcessingEfficiency { get; set; }
}

/// <summary>
/// Performance metrics base class
/// </summary>
public abstract class PerformanceMetricsBase
{
    /// <summary>
    /// Processing throughput (images per second)
    /// </summary>
    public double Throughput { get; set; }

    /// <summary>
    /// Resource efficiency score (0-100)
    /// </summary>
    public double ResourceEfficiency { get; set; }

    /// <summary>
    /// Memory efficiency score (0-100)
    /// </summary>
    public double MemoryEfficiency { get; set; }

    /// <summary>
    /// Overall performance score (0-100)
    /// </summary>
    public double OverallScore { get; set; }
}

/// <summary>
/// Postprocessing performance metrics
/// </summary>
public class PostprocessingPerformanceMetrics : PerformanceMetricsBase
{
    /// <summary>
    /// Per-operation performance breakdown
    /// </summary>
    public Dictionary<string, double> OperationPerformance { get; set; } = new();

    /// <summary>
    /// Pipeline efficiency
    /// </summary>
    public double PipelineEfficiency { get; set; }
}

/// <summary>
/// Resource usage metrics
/// </summary>
public class ResourceUsageMetrics
{
    /// <summary>
    /// Peak memory usage in bytes
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Average memory usage in bytes
    /// </summary>
    public long AverageMemoryUsage { get; set; }

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
    /// I/O operations per second
    /// </summary>
    public double IOOperationsPerSecond { get; set; }
}

/// <summary>
/// Image quality assessment
/// </summary>
public class ImageQualityAssessment
{
    /// <summary>
    /// Overall quality score (0-100)
    /// </summary>
    public double OverallQuality { get; set; }

    /// <summary>
    /// Sharpness score (0-100)
    /// </summary>
    public double Sharpness { get; set; }

    /// <summary>
    /// Noise level (0-100, lower is better)
    /// </summary>
    public double NoiseLevel { get; set; }

    /// <summary>
    /// Contrast score (0-100)
    /// </summary>
    public double Contrast { get; set; }

    /// <summary>
    /// Color accuracy score (0-100)
    /// </summary>
    public double ColorAccuracy { get; set; }

    /// <summary>
    /// Artifact level (0-100, lower is better)
    /// </summary>
    public double ArtifactLevel { get; set; }
}

/// <summary>
/// Quality improvement assessment
/// </summary>
public class QualityImprovementAssessment
{
    /// <summary>
    /// Improvement percentage
    /// </summary>
    public double ImprovementPercentage { get; set; }

    /// <summary>
    /// Before quality score
    /// </summary>
    public double BeforeQuality { get; set; }

    /// <summary>
    /// After quality score
    /// </summary>
    public double AfterQuality { get; set; }

    /// <summary>
    /// Improvement areas
    /// </summary>
    public Dictionary<string, double> ImprovementAreas { get; set; } = new();
}

// Additional specialized performance metrics classes
public class UpscalePerformanceMetrics : PerformanceMetricsBase { }
public class EnhancementPerformanceMetrics : PerformanceMetricsBase { }
public class FaceRestorationPerformanceMetrics : PerformanceMetricsBase { }
public class StyleTransferPerformanceMetrics : PerformanceMetricsBase { }
public class BackgroundRemovalPerformanceMetrics : PerformanceMetricsBase { }
public class ColorCorrectionPerformanceMetrics : PerformanceMetricsBase { }
public class BatchPostprocessingPerformanceMetrics : PerformanceMetricsBase { }

// Additional assessment classes (simplified for brevity)
public class QualityComparison { }
public class StyleSimilarityAssessment { }
public class ContentPreservationAssessment { }
public class MaskQualityAssessment { }
public class EdgeQualityAssessment { }
public class ColorAccuracyAssessment { }
public class BatchProcessingSummary { }
public class PostprocessingOperationDetails { }
public class ImageMetadata { }
public class ImageDimensions { public int Width { get; set; } public int Height { get; set; } }
public class AppliedEnhancement { }
public class RestoredFace { }
public class AppliedColorCorrection { }
public class FaceQualityImprovement { }

/// <summary>
/// Postprocessing status enumeration
/// </summary>
public enum PostprocessingStatus
{
    Unknown = 0,
    Queued = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}
