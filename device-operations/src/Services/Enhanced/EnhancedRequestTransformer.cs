using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using DeviceOperations.Models.Requests;
using DeviceOperations.Services.Interfaces;

namespace DeviceOperations.Services.Enhanced
{
    /// <summary>
    /// Transforms C# Enhanced SDXL requests to Python worker format
    /// Fixes critical protocol mismatch: "action" → "message_type"
    /// </summary>
    public class EnhancedRequestTransformer : IEnhancedRequestTransformer
    {
        private readonly ILogger<EnhancedRequestTransformer> _logger;
        private readonly WorkerTypeResolver _workerTypeResolver;

        public EnhancedRequestTransformer(
            ILogger<EnhancedRequestTransformer> logger,
            WorkerTypeResolver workerTypeResolver)
        {
            _logger = logger;
            _workerTypeResolver = workerTypeResolver;
        }

        /// <summary>
        /// Transform C# EnhancedSDXLRequest to Python worker format
        /// CRITICAL FIX: Changes "action" to "message_type" for protocol compatibility
        /// </summary>
        public object TransformEnhancedSDXLRequest(EnhancedSDXLRequest request, string requestId)
        {
            try
            {
                _logger.LogDebug("Transforming EnhancedSDXLRequest {RequestId}", requestId);

                var transformedRequest = new
                {
                    message_type = "inference_request",           // ← CRITICAL FIX: Protocol compatibility
                    request_id = requestId,
                    data = new
                    {
                        // Smart worker routing based on features
                        worker_type = _workerTypeResolver.DetermineWorkerType(request),
                        
                        // Core SDXL parameters (✅ Direct mapping)
                        prompt = request.Conditioning?.Prompt ?? string.Empty,
                        negative_prompt = request.Hyperparameters?.NegativePrompt ?? string.Empty,
                        model_name = request.Model?.Base ?? "cyberrealisticPony_v125",
                        
                        // Hyperparameters (✅ Direct mapping with defaults)
                        hyperparameters = new
                        {
                            num_inference_steps = request.Scheduler?.Steps ?? 25,
                            guidance_scale = request.Hyperparameters?.GuidanceScale ?? 7.5f,
                            seed = request.Hyperparameters?.Seed ?? -1,
                            strength = request.Conditioning?.Img2ImgStrength ?? 0.8f
                        },
                        
                        // Dimensions (✅ Direct mapping with validation)
                        dimensions = new
                        {
                            width = ValidateAndClampDimension(request.Hyperparameters?.Width ?? 1024),
                            height = ValidateAndClampDimension(request.Hyperparameters?.Height ?? 1024)
                        },
                        
                        // Scheduler (✅ Schema supports, map C# enum to Python string)
                        scheduler = MapSchedulerType(request.Scheduler?.Type ?? "DPMSolverMultistepScheduler"),
                        
                        // Enhanced model configuration (⚠️ Schema extension required)
                        models = new
                        {
                            @base = request.Model?.Base ?? "cyberrealisticPony_v125",
                            refiner = request.Model?.Refiner,     // ← Will be added to schema
                            vae = request.Model?.Vae              // ← Will be added to schema
                        },
                        
                        // LoRA configuration (✅ Schema ready)
                        lora = TransformLoRAConfiguration(request.Conditioning?.Loras),
                        
                        // ControlNet configuration (✅ Schema ready)
                        controlnet = TransformControlNetConfiguration(request.Conditioning?.ControlNets),
                        
                        // Output configuration (✅ Direct mapping)
                        output = new
                        {
                            format = request.Postprocessing?.OutputFormat?.ToLowerInvariant() ?? "png",
                            quality = Math.Clamp(request.Postprocessing?.Quality ?? 95, 1, 100),
                            save_path = "outputs/",
                            upscale = TransformUpscaleConfiguration(request.Postprocessing)
                        },
                        
                        // Batch configuration (✅ Schema ready)
                        batch = new
                        {
                            size = Math.Clamp(request.Hyperparameters?.BatchSize ?? 1, 1, 9),
                            parallel = false // Default for memory management
                        },
                        
                        // Precision controls (✅ Schema ready, Python enhancement)
                        precision = new
                        {
                            dtype = request.Performance?.Dtype ?? "fp16",
                            cpu_offload = request.Performance?.CpuOffload ?? false,
                            sequential_cpu_offload = request.Performance?.SequentialCpuOffload ?? false,
                            attention_slicing = request.Performance?.AttentionSlicing ?? true,
                            vae_slicing = true,          // Always enable for memory efficiency
                            xformers = request.Performance?.Xformers ?? false
                        },
                        
                        // Metadata (✅ Schema supports)
                        metadata = new
                        {
                            request_id = requestId,
                            priority = "normal",
                            timeout = 300,
                            tags = GenerateRequestTags(request)
                        }
                    }
                };

                _logger.LogDebug("Successfully transformed request {RequestId} with features: {Features}", 
                    requestId, string.Join(", ", _workerTypeResolver.GetFeatureSummary(request)));
                
                return transformedRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transform EnhancedSDXLRequest {RequestId}", requestId);
                throw new InvalidOperationException($"Request transformation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validate enhanced SDXL request before transformation
        /// </summary>
        public bool ValidateRequest(EnhancedSDXLRequest request, out List<string> errors)
        {
            errors = new List<string>();

            // Required fields validation
            if (request?.Conditioning?.Prompt == null || string.IsNullOrWhiteSpace(request.Conditioning.Prompt))
            {
                errors.Add("Prompt is required and cannot be empty");
            }

            if (request?.Model?.Base == null || string.IsNullOrWhiteSpace(request.Model.Base))
            {
                errors.Add("Base model is required");
            }

            // Dimension validation
            if (request?.Hyperparameters?.Width != null && !IsValidDimension(request.Hyperparameters.Width))
            {
                errors.Add("Width must be between 256 and 2048 and divisible by 8");
            }

            if (request?.Hyperparameters?.Height != null && !IsValidDimension(request.Hyperparameters.Height))
            {
                errors.Add("Height must be between 256 and 2048 and divisible by 8");
            }

            // LoRA validation
            if (request?.Conditioning?.Loras != null)
            {
                foreach (var lora in request.Conditioning.Loras)
                {
                    if (string.IsNullOrWhiteSpace(lora.Name))
                    {
                        errors.Add("LoRA Name cannot be empty");
                    }
                    if (lora.Scale < -2.0f || lora.Scale > 2.0f)
                    {
                        errors.Add($"LoRA scale {lora.Scale} must be between -2.0 and 2.0");
                    }
                }
            }

            // ControlNet validation
            if (request?.Conditioning?.ControlNets != null)
            {
                foreach (var controlnet in request.Conditioning.ControlNets)
                {
                    if (string.IsNullOrWhiteSpace(controlnet.Type))
                    {
                        errors.Add("ControlNet Type cannot be empty");
                    }
                    if (controlnet.Weight < 0.0f || controlnet.Weight > 2.0f)
                    {
                        errors.Add($"ControlNet weight {controlnet.Weight} must be between 0.0 and 2.0");
                    }
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Determine the appropriate worker type based on request features
        /// </summary>
        public string DetermineWorkerType(EnhancedSDXLRequest request)
        {
            return _workerTypeResolver.DetermineWorkerType(request);
        }

        #region Private Helper Methods

        private object TransformLoRAConfiguration(List<LoRAConfig>? loras)
        {
            if (loras == null || !loras.Any())
            {
                return new { enabled = false, models = Array.Empty<object>() };
            }

            return new
            {
                enabled = true,
                models = loras.Select(lora => new
                {
                    name = ExtractModelName(lora.Name),
                    weight = Math.Clamp(lora.Scale, -2.0f, 2.0f),
                    adapter_name = lora.AdapterName
                }).ToArray()
            };
        }

        private object TransformControlNetConfiguration(List<ControlNetConfig>? controlnets)
        {
            if (controlnets == null || !controlnets.Any())
            {
                return new { enabled = false };
            }

            var primaryControlNet = controlnets.First(); // Use first ControlNet for now
            return new
            {
                enabled = true,
                type = primaryControlNet.Type.ToLowerInvariant(),
                conditioning_scale = Math.Clamp(primaryControlNet.Weight, 0.0f, 2.0f),
                control_image = primaryControlNet.Image,
                guidance_start = primaryControlNet.GuidanceStart,
                guidance_end = primaryControlNet.GuidanceEnd,
                preprocessor = new
                {
                    enabled = true,
                    resolution = 512
                }
            };
        }

        private object TransformUpscaleConfiguration(PostProcessingConfig? postProcessing)
        {
            if (postProcessing?.Upscaler == null || postProcessing.Upscaler == "none")
            {
                return new { enabled = false };
            }

            return new
            {
                enabled = true,
                factor = Math.Clamp(postProcessing.UpscaleFactor, 1.0f, 8.0f),
                method = postProcessing.Upscaler.ToLowerInvariant()
            };
        }

        private string MapSchedulerType(string schedulerType)
        {
            return schedulerType switch
            {
                "DPMSolverMultistep" => "DPMSolverMultistepScheduler",
                "DDIM" => "DDIMScheduler",
                "EulerDiscrete" => "EulerDiscreteScheduler",
                "EulerAncestral" => "EulerAncestralDiscreteScheduler",
                "DPMSolverSinglestep" => "DPMSolverSinglestepScheduler",
                "KDPM2Discrete" => "KDPM2DiscreteScheduler",
                "KDPM2Ancestral" => "KDPM2AncestralDiscreteScheduler",
                "HeunDiscrete" => "HeunDiscreteScheduler",
                "LMSDiscrete" => "LMSDiscreteScheduler",
                "UniPCMultistep" => "UniPCMultistepScheduler",
                _ => "DPMSolverMultistepScheduler" // Default fallback
            };
        }

        private int ValidateAndClampDimension(int dimension)
        {
            // Ensure dimension is within valid range and divisible by 8
            var clamped = Math.Clamp(dimension, 256, 2048);
            return (clamped / 8) * 8; // Round down to nearest multiple of 8
        }

        private bool IsValidDimension(int dimension)
        {
            return dimension >= 256 && dimension <= 2048 && dimension % 8 == 0;
        }

        private string ExtractModelName(string modelPath)
        {
            // Extract model name from path (e.g., "path/to/model.safetensors" → "model")
            var fileName = Path.GetFileNameWithoutExtension(modelPath);
            return fileName ?? modelPath;
        }

        private string[] GenerateRequestTags(EnhancedSDXLRequest request)
        {
            var tags = new List<string> { "enhanced_sdxl", "c_sharp_service" };
            
            if (request.Conditioning?.Loras?.Any() == true)
                tags.Add("lora_enhanced");
            
            if (request.Conditioning?.ControlNets?.Any() == true)
                tags.Add("controlnet_guided");
            
            if (!string.IsNullOrEmpty(request.Model?.Refiner))
                tags.Add("refiner_enhanced");
            
            if (request.Postprocessing?.Upscaler != "none" && !string.IsNullOrEmpty(request.Postprocessing?.Upscaler))
                tags.Add("post_processed");
            
            if (request.Hyperparameters?.BatchSize > 1)
                tags.Add("batch_generation");
                
            if (request.Conditioning?.TextualInversions?.Any() == true)
                tags.Add("textual_inversions");
            
            return tags.ToArray();
        }

        #endregion
    }
}
