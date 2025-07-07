using System.Linq;
using Microsoft.Extensions.Logging;
using DeviceOperations.Models.Requests;

namespace DeviceOperations.Services.Enhanced
{
    /// <summary>
    /// Resolves the appropriate worker type based on request features
    /// </summary>
    public class WorkerTypeResolver
    {
        private readonly ILogger<WorkerTypeResolver> _logger;

        public WorkerTypeResolver(ILogger<WorkerTypeResolver> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Determine the appropriate worker type based on request complexity
        /// </summary>
        /// <param name="request">The enhanced SDXL request</param>
        /// <returns>Worker type string for routing</returns>
        public string DetermineWorkerType(EnhancedSDXLRequest request)
        {
            // Analyze request complexity and route to appropriate worker
            var hasAdvancedFeatures = HasAdvancedFeatures(request);
            
            if (hasAdvancedFeatures)
            {
                _logger.LogDebug("Request requires enhanced worker due to advanced features");
                return "enhanced_sdxl_worker";
            }
            
            _logger.LogDebug("Request can be handled by basic worker");
            return "sdxl_worker";
        }

        /// <summary>
        /// Check if request contains advanced features requiring enhanced worker
        /// </summary>
        /// <param name="request">The enhanced SDXL request</param>
        /// <returns>True if advanced features are present</returns>
        private bool HasAdvancedFeatures(EnhancedSDXLRequest request)
        {
            // Check for LoRA usage
            if (request.Conditioning?.Loras?.Any() == true)
            {
                _logger.LogDebug("Advanced feature detected: LoRA adapters");
                return true;
            }
            
            // Check for ControlNet usage
            if (request.Conditioning?.ControlNets?.Any() == true)
            {
                _logger.LogDebug("Advanced feature detected: ControlNet conditioning");
                return true;
            }
            
            // Check for refiner model
            if (!string.IsNullOrEmpty(request.Model?.Refiner))
            {
                _logger.LogDebug("Advanced feature detected: SDXL refiner model");
                return true;
            }
            
            // Check for custom VAE
            if (!string.IsNullOrEmpty(request.Model?.Vae))
            {
                _logger.LogDebug("Advanced feature detected: Custom VAE model");
                return true;
            }
            
            // Check for post-processing
            if (request.Postprocessing?.Upscaler != "none" && !string.IsNullOrEmpty(request.Postprocessing?.Upscaler))
            {
                _logger.LogDebug("Advanced feature detected: Post-processing upscaling");
                return true;
            }
            
            // Check for batch generation
            if (request.Hyperparameters?.BatchSize > 1)
            {
                _logger.LogDebug("Advanced feature detected: Batch generation");
                return true;
            }
            
            // Check for textual inversions
            if (request.Conditioning?.TextualInversions?.Any() == true)
            {
                _logger.LogDebug("Advanced feature detected: Textual inversions");
                return true;
            }
            
            // Check for img2img or inpainting
            if (!string.IsNullOrEmpty(request.Conditioning?.InitImage) || 
                !string.IsNullOrEmpty(request.Conditioning?.InpaintMask))
            {
                _logger.LogDebug("Advanced feature detected: Img2img or inpainting");
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Get feature summary for logging and debugging
        /// </summary>
        /// <param name="request">The enhanced SDXL request</param>
        /// <returns>List of detected features</returns>
        public string[] GetFeatureSummary(EnhancedSDXLRequest request)
        {
            var features = new List<string>();
            
            if (request.Conditioning?.Loras?.Any() == true)
                features.Add($"LoRA ({request.Conditioning.Loras.Count} adapters)");
            
            if (request.Conditioning?.ControlNets?.Any() == true)
                features.Add($"ControlNet ({request.Conditioning.ControlNets.Count} controls)");
            
            if (!string.IsNullOrEmpty(request.Model?.Refiner))
                features.Add("SDXL Refiner");
            
            if (!string.IsNullOrEmpty(request.Model?.Vae))
                features.Add("Custom VAE");
            
            if (request.Postprocessing?.Upscaler != "none" && !string.IsNullOrEmpty(request.Postprocessing?.Upscaler))
                features.Add($"Upscaling ({request.Postprocessing.Upscaler})");
            
            if (request.Hyperparameters?.BatchSize > 1)
                features.Add($"Batch ({request.Hyperparameters.BatchSize} images)");
            
            if (request.Conditioning?.TextualInversions?.Any() == true)
                features.Add($"Textual Inversions ({request.Conditioning.TextualInversions.Count})");
            
            if (!string.IsNullOrEmpty(request.Conditioning?.InitImage))
                features.Add("Img2Img");
            
            if (!string.IsNullOrEmpty(request.Conditioning?.InpaintMask))
                features.Add("Inpainting");
            
            return features.ToArray();
        }
    }
}
