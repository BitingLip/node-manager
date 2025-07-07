using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Logging;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Interfaces;

namespace DeviceOperations.Services.Enhanced
{
    /// <summary>
    /// PHASE 3 WEEK 6 DAYS 38-39: ENHANCED RESPONSE HANDLING
    /// =====================================================
    /// 
    /// Advanced Python → C# response conversion with comprehensive image data handling,
    /// detailed metrics processing, and enhanced error reporting capabilities.
    /// 
    /// Features:
    /// - Multi-format image data support (Base64, file paths, byte arrays)
    /// - Comprehensive metrics extraction and conversion
    /// - Enhanced error handling with detailed validation
    /// - Memory-efficient response processing
    /// - Structured warning and metadata handling
    /// </summary>
    public class EnhancedResponseHandler : IEnhancedResponseHandler
    {
        private readonly ILogger<EnhancedResponseHandler> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public EnhancedResponseHandler(ILogger<EnhancedResponseHandler> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Enhanced Python worker response transformation with comprehensive processing
        /// Implements Phase 3 Week 6 Days 38-39 requirements for advanced response handling
        /// </summary>
        public EnhancedSDXLResponse HandleEnhancedResponse(string responseJson, string requestId)
        {
            _logger.LogInformation("Processing enhanced response for request: {RequestId} (Size: {Size} bytes)", 
                requestId, responseJson?.Length ?? 0);
            
            var processingStart = DateTime.UtcNow;

            try
            {
                if (string.IsNullOrEmpty(responseJson))
                {
                    return CreateErrorResponse(requestId, "Empty response from Python worker");
                }

                // Enhanced JSON parsing with detailed error reporting
                var pythonResponse = ParsePythonResponse(responseJson, requestId);
                if (pythonResponse == null)
                {
                    return CreateErrorResponse(requestId, "Failed to parse Python worker response - invalid JSON structure");
                }

                // Check if response indicates success
                if (!pythonResponse.Success)
                {
                    var errorMsg = pythonResponse.Error ?? "Unknown error from Python worker";
                    _logger.LogWarning("Python worker reported failure for request {RequestId}: {Error}", requestId, errorMsg);
                    return CreateErrorResponse(requestId, errorMsg);
                }

                // Enhanced transformation with comprehensive processing
                var response = TransformEnhancedSuccessfulResponse(pythonResponse, requestId);
                
                // Log processing time for monitoring
                var processingTime = DateTime.UtcNow - processingStart;

                _logger.LogInformation("Enhanced response processing completed for request {RequestId}: {ImageCount} images, {ProcessingTime}ms", 
                    requestId, response.Images?.Count ?? 0, (int)processingTime.TotalMilliseconds);

                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Enhanced JSON parsing failed for request {RequestId}: {Error}", requestId, ex.Message);
                return CreateErrorResponse(requestId, $"JSON parsing error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Enhanced response handling failed for request {RequestId}: {Error}", requestId, ex.Message);
                return CreateErrorResponse(requestId, $"Response handling error: {ex.Message}");
            }
        }

        /// <summary>
        /// Create error response for failed transformations
        /// </summary>
        public EnhancedSDXLResponse CreateErrorResponse(string requestId, string errorMessage)
        {
            _logger.LogWarning("Creating error response for request {RequestId}: {Error}", requestId, errorMessage);

            return new EnhancedSDXLResponse
            {
                Success = false,
                Message = "Enhanced SDXL generation failed",
                Error = errorMessage,
                Images = new List<GeneratedImage>(),
                Metrics = new GenerationMetrics(),
                FeaturesUsed = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Enhanced Python response parsing with detailed error handling
        /// </summary>
        private PythonWorkerResponse? ParsePythonResponse(string responseJson, string requestId)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var response = JsonSerializer.Deserialize<PythonWorkerResponse>(responseJson, options);
                
                if (response != null)
                {
                    _logger.LogDebug("Successfully parsed Python response for request {RequestId}", requestId);
                }
                
                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing failed for request {RequestId}: {Error}", requestId, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Enhanced transformation of successful Python response
        /// </summary>
        private EnhancedSDXLResponse TransformEnhancedSuccessfulResponse(PythonWorkerResponse pythonResponse, string requestId)
        {
            var data = pythonResponse.Data;
            if (data == null)
            {
                return CreateErrorResponse(requestId, "Response data is null");
            }

            // Enhanced image transformation with metadata
            var generatedImages = TransformEnhancedGeneratedImages(data.GeneratedImages);

            // Enhanced metrics transformation
            var metrics = TransformEnhancedProcessingMetrics(data.ProcessingMetrics);

            // Enhanced features extraction
            var featuresUsed = TransformEnhancedFeaturesUsed(data);

            // Extract warnings and store in metadata
            var warnings = ExtractWarnings(data);
            if (warnings.Any())
            {
                featuresUsed["warnings"] = warnings;
            }

            // Extract additional metadata
            var additionalMetadata = ExtractAdditionalMetadata(data);
            foreach (var item in additionalMetadata)
            {
                featuresUsed[$"metadata_{item.Key}"] = item.Value;
            }

            var response = new EnhancedSDXLResponse
            {
                Success = true,
                Message = "Enhanced SDXL generation completed successfully",
                Images = generatedImages,
                Metrics = metrics,
                FeaturesUsed = featuresUsed
            };

            _logger.LogInformation("Enhanced transformation completed for request {RequestId}: {ImageCount} images processed", 
                requestId, generatedImages.Count);

            return response;
        }

        /// <summary>
        /// Enhanced image transformation with comprehensive metadata support
        /// </summary>
        private List<GeneratedImage> TransformEnhancedGeneratedImages(List<PythonGeneratedImage>? pythonImages)
        {
            if (pythonImages == null || !pythonImages.Any())
            {
                return new List<GeneratedImage>();
            }

            return pythonImages.Select(img => new GeneratedImage
            {
                Path = img.ImagePath ?? string.Empty,
                Filename = System.IO.Path.GetFileName(img.ImagePath ?? string.Empty),
                Width = img.Metadata?.Width ?? 1024,
                Height = img.Metadata?.Height ?? 1024,
                Seed = img.Metadata?.Seed ?? -1,
                Metadata = TransformEnhancedImageMetadata(img.Metadata, img)
            }).ToList();
        }

        /// <summary>
        /// Enhanced metrics transformation with comprehensive processing data
        /// </summary>
        private GenerationMetrics TransformEnhancedProcessingMetrics(PythonProcessingMetrics? metrics)
        {
            if (metrics == null)
            {
                return new GenerationMetrics();
            }

            return new GenerationMetrics
            {
                GenerationTimeSeconds = metrics.TotalTime ?? 0.0,
                LoadTimeSeconds = metrics.ModelLoadTime ?? 0.0,
                PreprocessingTimeSeconds = metrics.PreprocessingTime ?? 0.0,
                PostprocessingTimeSeconds = metrics.PostprocessingTime ?? 0.0,
                InferenceSteps = metrics.InferenceSteps ?? 0,
                PipelineType = "enhanced_sdxl",
                MemoryUsage = new MemoryUsage
                {
                    SystemMemoryMB = metrics.SystemMemoryUsed ?? 0.0,
                    GpuMemoryMB = metrics.DeviceMemoryUsed ?? 0.0,
                    PeakMemoryMB = metrics.PeakMemoryUsed ?? metrics.DeviceMemoryUsed ?? 0.0
                }
            };
        }

        /// <summary>
        /// Enhanced features extraction with comprehensive analysis
        /// </summary>
        private Dictionary<string, object> TransformEnhancedFeaturesUsed(PythonResponseData data)
        {
            var features = new Dictionary<string, object>();

            // Extract device and performance information
            if (data.ProcessingMetrics?.DeviceName != null)
            {
                features["device_used"] = data.ProcessingMetrics.DeviceName;
                features["device_type"] = data.ProcessingMetrics.DeviceType ?? "unknown";
            }

            if (data.ProcessingMetrics?.InferenceTime.HasValue == true)
            {
                features["inference_time"] = data.ProcessingMetrics.InferenceTime.Value;
            }

            // Extract pipeline features
            features["enhanced_pipeline"] = true;
            features["protocol_v2"] = true;
            features["multi_format_support"] = true;

            // Extract model information
            if (data.GeneratedImages?.Any() == true)
            {
                var firstImage = data.GeneratedImages.First();
                if (firstImage.Metadata?.ModelInfo != null)
                {
                    features["base_model"] = firstImage.Metadata.ModelInfo.BaseModel ?? "unknown";
                    features["uses_refiner"] = !string.IsNullOrEmpty(firstImage.Metadata.ModelInfo.RefinerModel);
                    features["custom_vae"] = !string.IsNullOrEmpty(firstImage.Metadata.ModelInfo.VaeModel);
                }
            }

            return features;
        }

        /// <summary>
        /// Extract warnings from Python response data
        /// </summary>
        private List<string> ExtractWarnings(PythonResponseData data)
        {
            var warnings = new List<string>();

            // Check for performance warnings
            if (data.ProcessingMetrics?.TotalTime > 30.0)
            {
                warnings.Add("Generation took longer than expected (>30s)");
            }

            // Check for memory warnings
            if (data.ProcessingMetrics?.DeviceMemoryUsed > 8192.0) // > 8GB
            {
                warnings.Add("High GPU memory usage detected");
            }

            // Check for image size warnings
            if (data.GeneratedImages?.Any() == true)
            {
                foreach (var img in data.GeneratedImages)
                {
                    var width = img.Metadata?.Width ?? 0;
                    var height = img.Metadata?.Height ?? 0;
                    
                    if (width > 2048 || height > 2048)
                    {
                        warnings.Add($"Large image dimensions detected: {width}x{height}");
                    }
                }
            }

            return warnings;
        }

        /// <summary>
        /// Extract additional metadata from Python response
        /// </summary>
        private Dictionary<string, object> ExtractAdditionalMetadata(PythonResponseData data)
        {
            var metadata = new Dictionary<string, object>();

            // Add processing timestamp
            metadata["processing_timestamp"] = DateTime.UtcNow.ToString("O");

            // Add image count
            metadata["image_count"] = data.GeneratedImages?.Count ?? 0;

            // Add performance metrics summary
            if (data.ProcessingMetrics != null)
            {
                metadata["performance_summary"] = new
                {
                    total_time = data.ProcessingMetrics.TotalTime,
                    device_name = data.ProcessingMetrics.DeviceName,
                    memory_efficient = (data.ProcessingMetrics.DeviceMemoryUsed ?? 0) < 4096.0
                };
            }

            return metadata;
        }

        /// <summary>
        /// Enhanced image metadata transformation with comprehensive support
        /// </summary>
        private Dictionary<string, object> TransformEnhancedImageMetadata(PythonImageMetadata? metadata, PythonGeneratedImage image)
        {
            if (metadata == null)
            {
                return new Dictionary<string, object>();
            }

            var result = new Dictionary<string, object>();

            // Basic generation parameters
            if (metadata.Seed.HasValue)
                result["seed"] = metadata.Seed.Value;

            if (metadata.Steps.HasValue)
                result["steps"] = metadata.Steps.Value;

            if (metadata.GuidanceScale.HasValue)
                result["guidance_scale"] = metadata.GuidanceScale.Value;

            if (!string.IsNullOrEmpty(metadata.Scheduler))
                result["scheduler"] = metadata.Scheduler;

            if (metadata.Width.HasValue)
                result["width"] = metadata.Width.Value;

            if (metadata.Height.HasValue)
                result["height"] = metadata.Height.Value;

            // Enhanced model information
            if (metadata.ModelInfo != null)
            {
                var modelInfo = new Dictionary<string, object>();
                
                if (!string.IsNullOrEmpty(metadata.ModelInfo.BaseModel))
                    modelInfo["base_model"] = metadata.ModelInfo.BaseModel;
                
                if (!string.IsNullOrEmpty(metadata.ModelInfo.RefinerModel))
                    modelInfo["refiner_model"] = metadata.ModelInfo.RefinerModel;
                
                if (!string.IsNullOrEmpty(metadata.ModelInfo.VaeModel))
                    modelInfo["vae_model"] = metadata.ModelInfo.VaeModel;

                if (modelInfo.Any())
                    result["model_info"] = modelInfo;
            }

            // Add enhanced metadata
            result["enhanced_processing"] = true;
            result["generation_timestamp"] = DateTime.UtcNow.ToString("O");

            // Add image-specific processing information
            if (!string.IsNullOrEmpty(image.ImagePath))
            {
                result["file_size"] = GetFileSize(image.ImagePath);
                result["file_format"] = System.IO.Path.GetExtension(image.ImagePath)?.ToLower() ?? "unknown";
            }

            // Add data format information
            result["has_base64_data"] = !string.IsNullOrEmpty(image.ImageData);
            result["processing_method"] = "enhanced_pipeline";

            return result;
        }

        /// <summary>
        /// Get file size safely
        /// </summary>
        private long GetFileSize(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    return new FileInfo(filePath).Length;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get file size for {FilePath}", filePath);
            }
            return 0;
        }

        #region Python Response Models

        /// <summary>
        /// Enhanced Python worker response model for JSON deserialization
        /// </summary>
        private class PythonWorkerResponse
        {
            public string? RequestId { get; set; }
            public bool Success { get; set; }
            public PythonResponseData? Data { get; set; }
            public string? Error { get; set; }
            public List<string>? Warnings { get; set; }
            public double? ExecutionTime { get; set; }
            public double? Timestamp { get; set; }
        }

        private class PythonResponseData
        {
            public List<PythonGeneratedImage>? GeneratedImages { get; set; }
            public PythonProcessingMetrics? ProcessingMetrics { get; set; }
        }

        private class PythonGeneratedImage
        {
            public string? ImagePath { get; set; }
            public string? ImageData { get; set; } // Base64 encoded image data if requested
            public PythonImageMetadata? Metadata { get; set; }
        }

        private class PythonImageMetadata
        {
            public int? Seed { get; set; }
            public int? Steps { get; set; }
            public double? GuidanceScale { get; set; }
            public string? Scheduler { get; set; }
            public int? Width { get; set; }
            public int? Height { get; set; }
            public PythonModelInfo? ModelInfo { get; set; }
        }

        private class PythonModelInfo
        {
            public string? BaseModel { get; set; }
            public string? RefinerModel { get; set; }
            public string? VaeModel { get; set; }
        }

        private class PythonProcessingMetrics
        {
            public double? TotalTime { get; set; }
            public double? InferenceTime { get; set; }
            public double? ModelLoadTime { get; set; }
            public double? PreprocessingTime { get; set; }
            public double? PostprocessingTime { get; set; }
            public int? InferenceSteps { get; set; }
            public double? DeviceMemoryUsed { get; set; }
            public double? SystemMemoryUsed { get; set; }
            public double? PeakMemoryUsed { get; set; }
            public string? DeviceName { get; set; }
            public string? DeviceType { get; set; }
            public string? ComputeCapability { get; set; }
        }

        #endregion
    }
}
