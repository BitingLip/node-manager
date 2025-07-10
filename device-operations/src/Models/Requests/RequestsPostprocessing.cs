namespace DeviceOperations.Models.Requests;

using DeviceOperations.Models.Common;

/// <summary>
/// Postprocessing operation requests
/// </summary>
public static class RequestsPostprocessing
{
    /// <summary>
    /// Request to apply postprocessing to an image
    /// </summary>
    public class ApplyPostprocessingRequest
    {
        /// <summary>
        /// Input image data (base64 encoded)
        /// </summary>
        public string ImageData { get; set; } = string.Empty;

        /// <summary>
        /// Input image URL
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Postprocessing operations to apply
        /// </summary>
        public List<PostprocessingOperation> Operations { get; set; } = new();

        /// <summary>
        /// Device to use for processing
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Processing priority
        /// </summary>
        public ProcessingPriority Priority { get; set; } = ProcessingPriority.Normal;

        /// <summary>
        /// Output format preferences
        /// </summary>
        public OutputFormat OutputFormat { get; set; } = new();

        /// <summary>
        /// Wait for processing completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;

        /// <summary>
        /// Processing timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;
    }

    /// <summary>
    /// Request to upscale an image
    /// </summary>
    public class UpscaleImageRequest
    {
        /// <summary>
        /// Input image data (base64 encoded)
        /// </summary>
        public string ImageData { get; set; } = string.Empty;

        /// <summary>
        /// Input image URL
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Upscale factor (2x, 4x, 8x)
        /// </summary>
        public UpscaleFactor ScaleFactor { get; set; } = UpscaleFactor.TwoX;

        /// <summary>
        /// Upscaler model to use
        /// </summary>
        public string? UpscalerModel { get; set; }

        /// <summary>
        /// Device to use for upscaling
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Upscaling algorithm
        /// </summary>
        public UpscaleAlgorithm Algorithm { get; set; } = UpscaleAlgorithm.ESRGAN;

        /// <summary>
        /// Quality enhancement settings
        /// </summary>
        public QualityEnhancement QualitySettings { get; set; } = new();

        /// <summary>
        /// Output format preferences
        /// </summary>
        public OutputFormat OutputFormat { get; set; } = new();

        /// <summary>
        /// Processing timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 600;
    }

    /// <summary>
    /// Request to enhance image quality
    /// </summary>
    public class EnhanceImageRequest
    {
        /// <summary>
        /// Input image data (base64 encoded)
        /// </summary>
        public string ImageData { get; set; } = string.Empty;

        /// <summary>
        /// Input image URL
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Enhancement operations
        /// </summary>
        public List<EnhancementOperation> Operations { get; set; } = new();

        /// <summary>
        /// Device to use for enhancement
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Enhancement model to use
        /// </summary>
        public string? EnhancementModel { get; set; }

        /// <summary>
        /// Enhancement strength (0.0-1.0)
        /// </summary>
        public double Strength { get; set; } = 0.5;

        /// <summary>
        /// Preserve original aspect ratio
        /// </summary>
        public bool PreserveAspectRatio { get; set; } = true;

        /// <summary>
        /// Output format preferences
        /// </summary>
        public OutputFormat OutputFormat { get; set; } = new();

        /// <summary>
        /// Processing timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;
    }

    /// <summary>
    /// Request to apply face restoration
    /// </summary>
    public class RestoreFacesRequest
    {
        /// <summary>
        /// Input image data (base64 encoded)
        /// </summary>
        public string ImageData { get; set; } = string.Empty;

        /// <summary>
        /// Input image URL
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Face restoration model
        /// </summary>
        public FaceRestorationModel Model { get; set; } = FaceRestorationModel.CodeFormer;

        /// <summary>
        /// Device to use for restoration
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Restoration strength (0.0-1.0)
        /// </summary>
        public double Strength { get; set; } = 0.8;

        /// <summary>
        /// Background enhancement
        /// </summary>
        public bool EnhanceBackground { get; set; } = false;

        /// <summary>
        /// Upscale after restoration
        /// </summary>
        public UpscaleFactor? PostUpscale { get; set; }

        /// <summary>
        /// Output format preferences
        /// </summary>
        public OutputFormat OutputFormat { get; set; } = new();

        /// <summary>
        /// Processing timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;
    }

    /// <summary>
    /// Request to apply style transfer
    /// </summary>
    public class StyleTransferRequest
    {
        /// <summary>
        /// Content image data (base64 encoded)
        /// </summary>
        public string ContentImageData { get; set; } = string.Empty;

        /// <summary>
        /// Style image data (base64 encoded)
        /// </summary>
        public string StyleImageData { get; set; } = string.Empty;

        /// <summary>
        /// Content image URL
        /// </summary>
        public string? ContentImageUrl { get; set; }

        /// <summary>
        /// Style image URL
        /// </summary>
        public string? StyleImageUrl { get; set; }

        /// <summary>
        /// Style transfer model
        /// </summary>
        public string? StyleModel { get; set; }

        /// <summary>
        /// Device to use for style transfer
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Style strength (0.0-1.0)
        /// </summary>
        public double StyleStrength { get; set; } = 0.7;

        /// <summary>
        /// Content preservation (0.0-1.0)
        /// </summary>
        public double ContentPreservation { get; set; } = 0.3;

        /// <summary>
        /// Output resolution
        /// </summary>
        public ImageResolution OutputResolution { get; set; } = new();

        /// <summary>
        /// Output format preferences
        /// </summary>
        public OutputFormat OutputFormat { get; set; } = new();

        /// <summary>
        /// Processing timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 600;
    }

    /// <summary>
    /// Request to remove background from image
    /// </summary>
    public class RemoveBackgroundRequest
    {
        /// <summary>
        /// Input image data (base64 encoded)
        /// </summary>
        public string ImageData { get; set; } = string.Empty;

        /// <summary>
        /// Input image URL
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Background removal model
        /// </summary>
        public BackgroundRemovalModel Model { get; set; } = BackgroundRemovalModel.U2Net;

        /// <summary>
        /// Device to use for processing
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Alpha matting for smooth edges
        /// </summary>
        public bool UseAlphaMatting { get; set; } = false;

        /// <summary>
        /// Background color for non-transparent formats
        /// </summary>
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// Output format preferences
        /// </summary>
        public OutputFormat OutputFormat { get; set; } = new();

        /// <summary>
        /// Processing timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 180;
    }

    /// <summary>
    /// Request to apply color correction
    /// </summary>
    public class ColorCorrectionRequest
    {
        /// <summary>
        /// Input image data (base64 encoded)
        /// </summary>
        public string ImageData { get; set; } = string.Empty;

        /// <summary>
        /// Input image URL
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Color correction operations
        /// </summary>
        public List<ColorOperation> Operations { get; set; } = new();

        /// <summary>
        /// Auto color correction
        /// </summary>
        public bool AutoCorrection { get; set; } = false;

        /// <summary>
        /// Reference image for color matching
        /// </summary>
        public string? ReferenceImageData { get; set; }

        /// <summary>
        /// Color space for processing
        /// </summary>
        public ColorSpace ColorSpace { get; set; } = ColorSpace.RGB;

        /// <summary>
        /// Output format preferences
        /// </summary>
        public OutputFormat OutputFormat { get; set; } = new();

        /// <summary>
        /// Processing timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 120;
    }

    /// <summary>
    /// Request to apply batch postprocessing
    /// </summary>
    public class BatchPostprocessingRequest
    {
        /// <summary>
        /// List of input images
        /// </summary>
        public List<BatchImageInput> Images { get; set; } = new();

        /// <summary>
        /// Postprocessing operations to apply to all images
        /// </summary>
        public List<PostprocessingOperation> Operations { get; set; } = new();

        /// <summary>
        /// Device to use for processing
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Batch processing configuration
        /// </summary>
        public BatchConfiguration BatchConfig { get; set; } = new();

        /// <summary>
        /// Output format preferences
        /// </summary>
        public OutputFormat OutputFormat { get; set; } = new();

        /// <summary>
        /// Wait for batch completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;

        /// <summary>
        /// Batch timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 1800;
    }

    /// <summary>
    /// Request to get postprocessing operation status
    /// </summary>
    public class GetPostprocessingStatusRequest
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Include processing details
        /// </summary>
        public bool IncludeDetails { get; set; } = true;

        /// <summary>
        /// Include resource usage
        /// </summary>
        public bool IncludeResourceUsage { get; set; } = false;
    }
}

/// <summary>
/// Postprocessing operation
/// </summary>
public class PostprocessingOperation
{
    /// <summary>
    /// Operation type
    /// </summary>
    public PostprocessingType Type { get; set; }

    /// <summary>
    /// Operation parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Operation order/priority
    /// </summary>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Operation enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Enhancement operation
/// </summary>
public class EnhancementOperation
{
    /// <summary>
    /// Enhancement type
    /// </summary>
    public EnhancementType Type { get; set; }

    /// <summary>
    /// Enhancement strength (0.0-1.0)
    /// </summary>
    public double Strength { get; set; } = 0.5;

    /// <summary>
    /// Enhancement parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Color operation
/// </summary>
public class ColorOperation
{
    /// <summary>
    /// Color operation type
    /// </summary>
    public ColorOperationType Type { get; set; }

    /// <summary>
    /// Operation value
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Additional parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Quality enhancement settings
/// </summary>
public class QualityEnhancement
{
    /// <summary>
    /// Noise reduction level (0.0-1.0)
    /// </summary>
    public double NoiseReduction { get; set; } = 0.0;

    /// <summary>
    /// Sharpening level (0.0-1.0)
    /// </summary>
    public double Sharpening { get; set; } = 0.0;

    /// <summary>
    /// Artifact reduction
    /// </summary>
    public bool ReduceArtifacts { get; set; } = false;

    /// <summary>
    /// Color enhancement
    /// </summary>
    public bool EnhanceColors { get; set; } = false;
}

/// <summary>
/// Image resolution specification
/// </summary>
public class ImageResolution
{
    /// <summary>
    /// Image width
    /// </summary>
    public int Width { get; set; } = 512;

    /// <summary>
    /// Image height
    /// </summary>
    public int Height { get; set; } = 512;

    /// <summary>
    /// Maintain aspect ratio
    /// </summary>
    public bool MaintainAspectRatio { get; set; } = true;
}

/// <summary>
/// Batch image input
/// </summary>
public class BatchImageInput
{
    /// <summary>
    /// Image identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Image data (base64 encoded)
    /// </summary>
    public string? ImageData { get; set; }

    /// <summary>
    /// Image URL
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Custom operations for this image
    /// </summary>
    public List<PostprocessingOperation>? CustomOperations { get; set; }
}

/// <summary>
/// Processing priority enumeration
/// </summary>
public enum ProcessingPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Upscale factor enumeration
/// </summary>
public enum UpscaleFactor
{
    OnePointFiveX = 0,
    TwoX = 1,
    ThreeX = 2,
    FourX = 3,
    SixX = 4,
    EightX = 5
}

/// <summary>
/// Upscale algorithm enumeration
/// </summary>
public enum UpscaleAlgorithm
{
    ESRGAN = 0,
    RealESRGAN = 1,
    SwinIR = 2,
    LDSR = 3,
    Waifu2x = 4,
    Bicubic = 5,
    Lanczos = 6
}

/// <summary>
/// Face restoration model enumeration
/// </summary>
public enum FaceRestorationModel
{
    CodeFormer = 0,
    GFPGAN = 1,
    RestoreFormer = 2
}

/// <summary>
/// Background removal model enumeration
/// </summary>
public enum BackgroundRemovalModel
{
    U2Net = 0,
    SINet = 1,
    MODNet = 2,
    BGMv2 = 3
}

/// <summary>
/// Postprocessing type enumeration
/// </summary>
public enum PostprocessingType
{
    Upscale = 0,
    FaceRestore = 1,
    ColorCorrection = 2,
    NoiseReduction = 3,
    Sharpening = 4,
    StyleTransfer = 5,
    BackgroundRemoval = 6,
    Enhancement = 7
}

/// <summary>
/// Enhancement type enumeration
/// </summary>
public enum EnhancementType
{
    Brightness = 0,
    Contrast = 1,
    Saturation = 2,
    Sharpness = 3,
    NoiseReduction = 4,
    ColorBalance = 5,
    Gamma = 6,
    Exposure = 7
}

/// <summary>
/// Color operation type enumeration
/// </summary>
public enum ColorOperationType
{
    Brightness = 0,
    Contrast = 1,
    Saturation = 2,
    Hue = 3,
    Gamma = 4,
    Exposure = 5,
    Temperature = 6,
    Tint = 7
}

/// <summary>
/// Color space enumeration
/// </summary>
public enum ColorSpace
{
    RGB = 0,
    HSV = 1,
    LAB = 2,
    XYZ = 3,
    YUV = 4
}

/// <summary>
/// Simple request models matching controller expectations
/// </summary>

/// <summary>
/// Request for background removal (controller compatible)
/// </summary>
public class PostPostprocessingBackgroundRemovalRequest
{
    public string ImagePath { get; set; } = string.Empty;
}

/// <summary>
/// Request for color correction (controller compatible)
/// </summary>
public class PostPostprocessingColorCorrectionRequest
{
    public string ImagePath { get; set; } = string.Empty;
    public Dictionary<string, double> Adjustments { get; set; } = new();
}
