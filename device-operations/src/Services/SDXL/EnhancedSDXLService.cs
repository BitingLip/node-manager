using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Models.Common;
using DeviceOperations.Services.Inference;
using DeviceOperations.Services.Core;
using System.Collections.Concurrent;

namespace DeviceOperations.Services.SDXL;

/// <summary>
/// Enhanced SDXL service implementation providing advanced generation capabilities
/// </summary>
public class EnhancedSDXLService : IEnhancedSDXLService
{
    private readonly IInferenceService _inferenceService;
    private readonly IDeviceService _deviceService;
    private readonly ILogger<EnhancedSDXLService> _logger;
    private readonly ConcurrentDictionary<string, QueuedRequestInfo> _generationQueue;
    private readonly ConcurrentDictionary<string, BatchSDXLResponse> _batchRequests;

    public EnhancedSDXLService(
        IInferenceService inferenceService,
        IDeviceService deviceService,
        ILogger<EnhancedSDXLService> logger)
    {
        _inferenceService = inferenceService;
        _deviceService = deviceService;
        _logger = logger;
        _generationQueue = new ConcurrentDictionary<string, QueuedRequestInfo>();
        _batchRequests = new ConcurrentDictionary<string, BatchSDXLResponse>();
        
        _logger.LogInformation("Enhanced SDXL service initialized");
    }

    public async Task<EnhancedSDXLResponse> GenerateEnhancedSDXLAsync(EnhancedSDXLRequest request)
    {
        try
        {
            _logger.LogInformation("Starting enhanced SDXL generation with scheduler {Scheduler}", request.Scheduler.Type);

            // Validate the request first
            var validationResult = await ValidateSDXLRequestAsync(request);
            if (!validationResult.Valid)
            {
                return new EnhancedSDXLResponse
                {
                    Success = false,
                    Message = "Request validation failed",
                    Error = validationResult.Error
                };
            }

            // Log validation warnings if any
            if (validationResult.Warnings?.Any() == true)
            {
                _logger.LogWarning("SDXL request has warnings: {Warnings}", string.Join(", ", validationResult.Warnings));
            }

            // Determine target device
            var targetDevice = request.Performance?.Device;
            if (string.IsNullOrEmpty(targetDevice))
            {
                var devices = await _deviceService.GetAvailableDevicesAsync();
                var bestDevice = devices.Where(d => d.IsAvailable && d.TotalMemory >= 6L * 1024 * 1024 * 1024)
                                       .OrderByDescending(d => d.AvailableMemory)
                                       .FirstOrDefault();
                targetDevice = bestDevice?.DeviceId ?? "gpu_0";
                _logger.LogInformation("Auto-selected device {Device} for SDXL generation", targetDevice);
            }

            // Create enhanced inference request
            var inferenceRequest = new InferenceRequest
            {
                ModelId = request.Model.Base,
                Inputs = new Dictionary<string, object>
                {
                    ["enhanced_sdxl_request"] = request,
                    ["pipeline_type"] = "enhanced_sdxl",
                    ["prompt"] = request.Conditioning.Prompt,
                    ["negative_prompt"] = request.Hyperparameters.NegativePrompt ?? "",
                    ["width"] = request.Hyperparameters.Width,
                    ["height"] = request.Hyperparameters.Height,
                    ["num_inference_steps"] = request.Scheduler.Steps,
                    ["guidance_scale"] = request.Hyperparameters.GuidanceScale,
                    ["num_images_per_prompt"] = request.Hyperparameters.BatchSize,
                    ["scheduler"] = request.Scheduler.Type
                },
                InferenceOptions = new Dictionary<string, object>
                {
                    ["return_images"] = true,
                    ["return_metrics"] = true,
                    ["target_device"] = targetDevice,
                    ["enable_memory_efficient_attention"] = request.Performance?.Xformers ?? true,
                    ["use_fp16"] = request.Performance?.Dtype == "fp16"
                }
            };

            // Add advanced features
            AddAdvancedFeatures(inferenceRequest, request);

            // Run the enhanced inference
            var result = await _inferenceService.RunInferenceAsync(inferenceRequest);

            if (result.Success)
            {
                // Extract enhanced response
                var response = new EnhancedSDXLResponse
                {
                    Success = true,
                    Message = result.Message ?? "Generation completed successfully",
                    Images = ConvertToGeneratedImages(ExtractImages(result.Outputs), request),
                    Metrics = ExtractEnhancedMetrics(result, request),
                    FeaturesUsed = ExtractFeaturesUsed(result.Outputs)
                };

                _logger.LogInformation("Enhanced SDXL generation completed successfully in {Time}s", response.Metrics.GenerationTimeSeconds);
                return response;
            }
            else
            {
                _logger.LogError("Enhanced SDXL generation failed: {Message}", result.Message);
                return new EnhancedSDXLResponse
                {
                    Success = false,
                    Message = result.Message ?? "Generation failed",
                    Error = "Inference service returned error"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enhanced SDXL generation failed with exception");
            return new EnhancedSDXLResponse
            {
                Success = false,
                Error = "Internal server error",
                Message = ex.Message
            };
        }
    }

    public async Task<PromptValidationResponse> ValidateSDXLRequestAsync(EnhancedSDXLRequest request)
    {
        try
        {
            await Task.CompletedTask;
            
            var response = new PromptValidationResponse { Valid = true };
            var warnings = new List<string>();

            // Validate resolution
            if (request.Hyperparameters.Width % 8 != 0 || request.Hyperparameters.Height % 8 != 0)
            {
                response.Valid = false;
                response.Error = "Width and height must be multiples of 8";
                return response;
            }

            // Validate resolution limits
            var totalPixels = request.Hyperparameters.Width * request.Hyperparameters.Height;
            if (totalPixels > 2048 * 2048)
            {
                response.Valid = false;
                response.Error = "Maximum resolution is 2048x2048";
                return response;
            }

            // Validate scheduler
            var validSchedulers = GetSchedulerList();
            if (!validSchedulers.Contains(request.Scheduler.Type))
            {
                response.Valid = false;
                response.Error = $"Invalid scheduler type: {request.Scheduler.Type}. Valid types: {string.Join(", ", validSchedulers)}";
                return response;
            }

            // Performance warnings
            if (request.Hyperparameters.GuidanceScale < 1.0 || request.Hyperparameters.GuidanceScale > 20.0)
                warnings.Add("Guidance scale outside recommended range [1.0, 20.0] may produce poor results");

            if (request.Hyperparameters.BatchSize > 4)
                warnings.Add("Batch size > 4 may cause memory issues on some GPUs");

            if (request.Scheduler.Steps > 50)
                warnings.Add("High step count will significantly increase generation time");

            // Validate LoRA settings
            if (request.Conditioning?.Loras != null)
            {
                foreach (var lora in request.Conditioning.Loras)
                {
                    if (Math.Abs(lora.Scale) > 2.0f)
                        warnings.Add($"LoRA '{lora.Name}' scale {lora.Scale} is outside recommended range [-2.0, 2.0]");
                }
            }

            response.Warnings = warnings;
            response.ValidationDetails = new Dictionary<string, object>
            {
                ["estimated_vram_mb"] = EstimateVRAMUsage(request),
                ["estimated_time_seconds"] = EstimateGenerationTime(request),
                ["pixel_count"] = totalPixels,
                ["aspect_ratio"] = (double)request.Hyperparameters.Width / request.Hyperparameters.Height
            };

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request validation failed");
            return new PromptValidationResponse
            {
                Valid = false,
                Error = $"Validation error: {ex.Message}"
            };
        }
    }

    public async Task<SDXLSchedulersResponse> GetAvailableSchedulersAsync()
    {
        try
        {
            await Task.CompletedTask;
            
            var schedulers = new List<SchedulerInfo>
            {
                new() { Name = "DDIM", Description = "Denoising Diffusion Implicit Models - Fast, deterministic", RecommendedSteps = 50, QualityScore = 0.8, SpeedScore = 0.9, BestFor = new List<string> { "fast_generation", "reproducible_results" } },
                new() { Name = "PNDM", Description = "Pseudo Numerical Methods for Diffusion Models", RecommendedSteps = 50, QualityScore = 0.75, SpeedScore = 0.85, BestFor = new List<string> { "balanced_quality_speed" } },
                new() { Name = "LMS", Description = "Linear Multi-Step scheduler", RecommendedSteps = 50, QualityScore = 0.7, SpeedScore = 0.8, BestFor = new List<string> { "stable_generation" } },
                new() { Name = "EulerA", Description = "Euler Ancestral - High quality, slower", RecommendedSteps = 30, QualityScore = 0.95, SpeedScore = 0.6, BestFor = new List<string> { "high_quality", "artistic_generation" } },
                new() { Name = "EulerC", Description = "Euler - Fast, good quality", RecommendedSteps = 20, QualityScore = 0.85, SpeedScore = 0.9, BestFor = new List<string> { "balanced_quality_speed", "general_purpose" } },
                new() { Name = "Heun", Description = "Heun scheduler - High quality", RecommendedSteps = 20, QualityScore = 0.9, SpeedScore = 0.7, BestFor = new List<string> { "high_quality", "detailed_images" } },
                new() { Name = "DPM++", Description = "DPM++ variants - Excellent quality", RecommendedSteps = 20, QualityScore = 0.98, SpeedScore = 0.75, BestFor = new List<string> { "best_quality", "professional_use" } },
                new() { Name = "DPMSolverMultistep", Description = "DPM Solver Multistep", RecommendedSteps = 25, QualityScore = 0.92, SpeedScore = 0.8, BestFor = new List<string> { "high_quality", "efficient_sampling" } },
                new() { Name = "DPMSolverSinglestep", Description = "DPM Solver Singlestep", RecommendedSteps = 25, QualityScore = 0.88, SpeedScore = 0.85, BestFor = new List<string> { "fast_high_quality" } }
            };

            return new SDXLSchedulersResponse
            {
                Success = true,
                Message = "Schedulers retrieved successfully",
                Schedulers = schedulers,
                RecommendedScheduler = "DPM++",
                DefaultSteps = schedulers.ToDictionary(s => s.Name, s => s.RecommendedSteps)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available schedulers");
            return new SDXLSchedulersResponse
            {
                Success = false,
                Message = $"Failed to get schedulers: {ex.Message}"
            };
        }
    }

    public async Task<ControlNetTypesResponse> GetControlNetTypesAsync()
    {
        try
        {
            await Task.CompletedTask;
            
            var types = new List<ControlNetTypeInfo>
            {
                new() { Name = "canny", Description = "Edge detection using Canny algorithm", RecommendedWeight = 1.0, SupportedFormats = new List<string> { "PNG", "JPEG", "WEBP" }, PreprocessorModel = "canny_processor" },
                new() { Name = "depth", Description = "Depth map conditioning", RecommendedWeight = 0.8, SupportedFormats = new List<string> { "PNG", "JPEG" }, PreprocessorModel = "depth_estimator" },
                new() { Name = "pose", Description = "Human pose estimation", RecommendedWeight = 0.9, SupportedFormats = new List<string> { "PNG", "JPEG" }, PreprocessorModel = "openpose" },
                new() { Name = "scribble", Description = "Scribble/sketch conditioning", RecommendedWeight = 0.7, SupportedFormats = new List<string> { "PNG", "JPEG" }, PreprocessorModel = "scribble_processor" },
                new() { Name = "seg", Description = "Semantic segmentation", RecommendedWeight = 0.8, SupportedFormats = new List<string> { "PNG", "JPEG" }, PreprocessorModel = "segmentation_model" },
                new() { Name = "normal", Description = "Normal map conditioning", RecommendedWeight = 0.7, SupportedFormats = new List<string> { "PNG", "JPEG" }, PreprocessorModel = "normal_estimator" },
                new() { Name = "mlsd", Description = "Line segment detection", RecommendedWeight = 0.8, SupportedFormats = new List<string> { "PNG", "JPEG" }, PreprocessorModel = "mlsd_processor" },
                new() { Name = "lineart", Description = "Line art conditioning", RecommendedWeight = 0.9, SupportedFormats = new List<string> { "PNG", "JPEG" }, PreprocessorModel = "lineart_processor" }
            };

            return new ControlNetTypesResponse
            {
                Success = true,
                Message = "ControlNet types retrieved successfully",
                Types = types,
                RecommendedWeights = types.ToDictionary(t => t.Name, t => t.RecommendedWeight)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ControlNet types");
            return new ControlNetTypesResponse
            {
                Success = false,
                Message = $"Failed to get ControlNet types: {ex.Message}"
            };
        }
    }

    public async Task<SDXLCapabilitiesResponse> GetSDXLCapabilitiesAsync()
    {
        try
        {
            // Get available models and device information
            var loadedModels = await _inferenceService.GetLoadedModelsAsync();
            var devices = await _deviceService.GetAvailableDevicesAsync();
            
            var sdxlCapableGpus = devices.Where(d => d.TotalMemory >= 6L * 1024 * 1024 * 1024).ToList();

            return new SDXLCapabilitiesResponse
            {
                Success = true,
                Message = "Capabilities retrieved successfully",
                SDXLSupport = sdxlCapableGpus.Any(),
                Features = new Dictionary<string, bool>
                {
                    ["text2img"] = true,
                    ["img2img"] = true,
                    ["inpainting"] = true,
                    ["controlnet"] = true,
                    ["lora"] = true,
                    ["upscaling"] = true,
                    ["safety_checker"] = true,
                    ["batch_generation"] = true,
                    ["advanced_scheduling"] = true,
                    ["memory_optimization"] = true
                },
                SupportedSchedulers = GetSchedulerList(),
                SupportedFormats = new List<string> { "PNG", "JPEG", "WEBP" },
                MaxResolution = 2048,
                MaxBatchSize = 8,
                MemoryOptimizations = new List<string> { "xformers", "cpu_offload", "attention_slicing", "vae_slicing", "fp16" },
                PostprocessingOptions = new List<string> { "Real-ESRGAN", "GFPGAN", "auto_contrast", "color_correction" },
                LoadedModels = new List<LoadedModelInfo>(), // loadedModels would need to be properly structured
                Resources = new SystemResourceInfo
                {
                    TotalVRAM = devices.Sum(d => d.TotalMemory),
                    AvailableVRAM = devices.Sum(d => d.AvailableMemory),
                    GPUUtilization = devices.Any() ? devices.Average(d => 0.0) : 0.0, // Would be populated by monitoring
                    AvailableGPUs = sdxlCapableGpus.Count,
                    GPUNames = sdxlCapableGpus.Select(g => g.Name).ToList()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SDXL capabilities");
            return new SDXLCapabilitiesResponse
            {
                Success = false,
                Message = $"Failed to get capabilities: {ex.Message}"
            };
        }
    }

    public async Task<SDXLPerformanceEstimate> GetPerformanceEstimateAsync(EnhancedSDXLRequest request)
    {
        try
        {
            await Task.CompletedTask;
            
            var vramUsage = EstimateVRAMUsage(request);
            var timeEstimate = EstimateGenerationTime(request);
            var costEstimate = EstimateCost(request);

            var recommendations = GetPerformanceRecommendations(request);

            return new SDXLPerformanceEstimate
            {
                Success = true,
                Message = "Performance estimate calculated successfully",
                EstimatedVRAMUsageMB = vramUsage,
                EstimatedTimeSeconds = timeEstimate,
                EstimatedCostCredits = costEstimate,
                PerformanceRecommendations = recommendations,
                DetailedBreakdown = new Dictionary<string, object>
                {
                    ["base_vram_mb"] = 6000,
                    ["resolution_impact"] = (request.Hyperparameters.Width * request.Hyperparameters.Height) / 1_000_000.0,
                    ["batch_multiplier"] = request.Hyperparameters.BatchSize,
                    ["scheduler_impact"] = request.Scheduler.Steps * 0.02,
                    ["features_overhead"] = CalculateFeaturesOverhead(request)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to estimate performance");
            return new SDXLPerformanceEstimate
            {
                Success = false,
                Message = $"Performance estimation failed: {ex.Message}"
            };
        }
    }

    // Simplified implementations for remaining interface methods
    public async Task<SDXLOptimizationResult> OptimizeSDXLRequestAsync(EnhancedSDXLRequest request, SDXLOptimizationPreferences optimization)
    {
        await Task.CompletedTask;
        return new SDXLOptimizationResult { Success = true, Message = "Feature coming soon", OptimizedRequest = request };
    }

    public async Task<SDXLModelsResponse> GetAvailableSDXLModelsAsync()
    {
        await Task.CompletedTask;
        return new SDXLModelsResponse { Success = true, Message = "Feature coming soon" };
    }

    public async Task<ImageAnalysisResult> AnalyzeImageAsync(string imagePath)
    {
        await Task.CompletedTask;
        return new ImageAnalysisResult { Success = true, Message = "Feature coming soon", ImagePath = imagePath };
    }

    public async Task<ControlNetPreprocessResult> GenerateControlNetConditioningAsync(ControlNetPreprocessRequest request)
    {
        await Task.CompletedTask;
        return new ControlNetPreprocessResult { Success = true, Message = "Feature coming soon" };
    }

    public async Task<BatchSDXLResponse> BatchGenerateSDXLAsync(List<EnhancedSDXLRequest> requests)
    {
        await Task.CompletedTask;
        return new BatchSDXLResponse { Success = true, Message = "Feature coming soon", BatchId = Guid.NewGuid().ToString("N")[..12] };
    }

    public async Task<SDXLQueueStatus> GetGenerationQueueStatusAsync()
    {
        await Task.CompletedTask;
        return new SDXLQueueStatus();
    }

    // Private helper methods
    private void AddAdvancedFeatures(InferenceRequest inferenceRequest, EnhancedSDXLRequest request)
    {
        // Add ControlNet if specified
        if (request.Conditioning?.ControlNets?.Any() == true)
        {
            inferenceRequest.Inputs["controlnet_conditioning"] = request.Conditioning.ControlNets;
        }

        // Add LoRA if specified
        if (request.Conditioning?.Loras?.Any() == true)
        {
            inferenceRequest.Inputs["lora_conditioning"] = request.Conditioning.Loras;
        }

        // Add img2img if specified (using the InitImage property)
        if (!string.IsNullOrEmpty(request.Conditioning?.InitImage))
        {
            inferenceRequest.Inputs["init_image"] = request.Conditioning.InitImage;
            inferenceRequest.Inputs["strength"] = request.Conditioning.Img2ImgStrength ?? 0.8f;
        }

        // Add inpainting if specified (using the InpaintMask property)
        if (!string.IsNullOrEmpty(request.Conditioning?.InpaintMask))
        {
            inferenceRequest.Inputs["mask_image"] = request.Conditioning.InpaintMask;
            inferenceRequest.Inputs["mask_blur"] = request.Conditioning.MaskBlur ?? 0;
        }

        // Add seed if specified
        if (request.Hyperparameters.Seed.HasValue)
        {
            inferenceRequest.Inputs["seed"] = request.Hyperparameters.Seed.Value;
        }
    }

    private List<GeneratedImage> ConvertToGeneratedImages(List<string> imagePaths, EnhancedSDXLRequest request)
    {
        return imagePaths.Select((path, index) => new GeneratedImage
        {
            Path = path,
            Filename = Path.GetFileName(path),
            Seed = request.Hyperparameters.Seed ?? 0,
            Width = request.Hyperparameters.Width,
            Height = request.Hyperparameters.Height,
            Metadata = new Dictionary<string, object>
            {
                ["prompt"] = request.Conditioning.Prompt,
                ["scheduler"] = request.Scheduler.Type,
                ["steps"] = request.Scheduler.Steps,
                ["guidance_scale"] = request.Hyperparameters.GuidanceScale,
                ["resolution"] = $"{request.Hyperparameters.Width}x{request.Hyperparameters.Height}"
            }
        }).ToList();
    }

    private GenerationMetrics ExtractEnhancedMetrics(InferenceResponse result, EnhancedSDXLRequest request)
    {
        var metrics = new GenerationMetrics();

        if (result.Statistics != null)
        {
            metrics.GenerationTimeSeconds = result.Statistics.InferenceTimeMs / 1000.0;
            metrics.PreprocessingTimeSeconds = result.Statistics.PreprocessingTimeMs / 1000.0;
            metrics.PostprocessingTimeSeconds = result.Statistics.PostprocessingTimeMs / 1000.0;
            metrics.InferenceSteps = request.Scheduler.Steps;
            metrics.PipelineType = "enhanced_sdxl";
            
            // Set memory usage using the MemoryUsage property structure
            metrics.MemoryUsage = new MemoryUsage
            {
                GpuMemoryMB = result.Statistics.MemoryUsageBytes / (1024.0 * 1024.0),
                PeakMemoryMB = result.Statistics.MemoryUsageBytes / (1024.0 * 1024.0) * 1.2, // Estimated
                SystemMemoryMB = 0 // Not tracked
            };
        }

        return metrics;
    }

    private Dictionary<string, object> ExtractFeaturesUsed(Dictionary<string, object>? outputs)
    {
        if (outputs?.TryGetValue("features_used", out var featuresObj) == true &&
            featuresObj is Dictionary<string, object> features)
        {
            return features;
        }

        return new Dictionary<string, object>
        {
            ["sdxl_base"] = true,
            ["memory_optimization"] = true,
            ["advanced_scheduling"] = true
        };
    }

    private List<string> ExtractImages(Dictionary<string, object>? outputs)
    {
        if (outputs?.TryGetValue("images", out var imagesObj) == true)
        {
            if (imagesObj is List<string> imagesList)
                return imagesList;
            if (imagesObj is string[] imagesArray)
                return imagesArray.ToList();
        }
        return new List<string>();
    }

    private List<string> GetSchedulerList()
    {
        return new List<string>
        {
            "DDIM", "PNDM", "LMS", "EulerA", "EulerC", "Heun", "DPM++",
            "DPMSolverMultistep", "DPMSolverSinglestep"
        };
    }

    private int EstimateVRAMUsage(EnhancedSDXLRequest request)
    {
        var baseVRAM = 6000; // Base SDXL VRAM in MB

        // Add for resolution
        var pixels = request.Hyperparameters.Width * request.Hyperparameters.Height;
        var resolutionVRAM = (pixels / 1_000_000.0) * 200; // ~200MB per megapixel

        // Add for batch size
        var batchVRAM = (request.Hyperparameters.BatchSize - 1) * 1500;

        // Add for features
        if (request.Conditioning?.ControlNets?.Any() == true)
            baseVRAM += 1500 * request.Conditioning.ControlNets.Count;

        if (request.Conditioning?.Loras?.Any() == true)
            baseVRAM += 300 * request.Conditioning.Loras.Count;

        return (int)(baseVRAM + resolutionVRAM + batchVRAM);
    }

    private double EstimateGenerationTime(EnhancedSDXLRequest request)
    {
        var baseTime = 3.0; // Base time in seconds
        var timePerStep = 0.08; // Time per step
        var batchMultiplier = Math.Pow(request.Hyperparameters.BatchSize, 0.8); // Diminishing returns

        // Resolution impact
        var pixels = request.Hyperparameters.Width * request.Hyperparameters.Height;
        var resolutionMultiplier = Math.Sqrt(pixels / (1024.0 * 1024.0));

        return baseTime + (request.Scheduler.Steps * timePerStep * batchMultiplier * resolutionMultiplier);
    }

    private double EstimateCost(EnhancedSDXLRequest request)
    {
        // Simple cost estimation based on compute resources
        var baseCost = 1.0;
        var stepCost = request.Scheduler.Steps * 0.02;
        var batchCost = request.Hyperparameters.BatchSize * 0.5;
        var resolutionCost = (request.Hyperparameters.Width * request.Hyperparameters.Height) / 1_000_000.0;

        return baseCost + stepCost + batchCost + resolutionCost;
    }

    private List<string> GetPerformanceRecommendations(EnhancedSDXLRequest request)
    {
        var recommendations = new List<string>();

        if (request.Scheduler.Steps > 30)
            recommendations.Add("Consider reducing steps to 20-30 for faster generation");

        if (request.Hyperparameters.BatchSize > 2)
            recommendations.Add("Reduce batch size if encountering memory issues");

        if (request.Performance?.Dtype != "fp16")
            recommendations.Add("Use fp16 for better memory efficiency");

        if (request.Performance?.Xformers != true)
            recommendations.Add("Enable xformers for memory optimization");

        return recommendations;
    }

    private double CalculateFeaturesOverhead(EnhancedSDXLRequest request)
    {
        var overhead = 0.0;

        if (request.Conditioning?.ControlNets?.Any() == true)
            overhead += request.Conditioning.ControlNets.Count * 0.15;

        if (request.Conditioning?.Loras?.Any() == true)
            overhead += request.Conditioning.Loras.Count * 0.05;

        if (!string.IsNullOrEmpty(request.Conditioning?.InitImage))
            overhead += 0.1;

        if (!string.IsNullOrEmpty(request.Conditioning?.InpaintMask))
            overhead += 0.08;

        return overhead;
    }
}
