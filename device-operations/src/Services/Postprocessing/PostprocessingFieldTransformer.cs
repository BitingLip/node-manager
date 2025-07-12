using System.Text.Json;

namespace DeviceOperations.Services.Postprocessing
{
    /// <summary>
    /// Field transformation service for postprocessing operations
    /// Handles PascalCase ↔ snake_case conversion and field mapping
    /// </summary>
    public class PostprocessingFieldTransformer
    {
        private readonly ILogger<PostprocessingFieldTransformer> _logger;
        private readonly Dictionary<string, string> _pascalToSnakeMapping;
        private readonly Dictionary<string, string> _snakeToPascalMapping;

        public PostprocessingFieldTransformer(ILogger<PostprocessingFieldTransformer> logger)
        {
            _logger = logger;
            _pascalToSnakeMapping = InitializePascalToSnakeMapping();
            _snakeToPascalMapping = InitializeSnakeToPascalMapping();
        }

        /// <summary>
        /// Transform C# request to Python format
        /// </summary>
        public Dictionary<string, object> ToPythonFormat(Dictionary<string, object> csharpRequest)
        {
            try
            {
                var pythonRequest = new Dictionary<string, object>();

                foreach (var kvp in csharpRequest)
                {
                    var pythonKey = ConvertToPythonFieldName(kvp.Key);
                    var pythonValue = ConvertToPythonValue(kvp.Value);
                    pythonRequest[pythonKey] = pythonValue;
                }

                _logger.LogDebug($"Transformed {csharpRequest.Count} fields to Python format");
                return pythonRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transform request to Python format");
                throw;
            }
        }

        /// <summary>
        /// Transform Python response to C# format
        /// </summary>
        public Dictionary<string, object> ToCSharpFormat(Dictionary<string, object> pythonResponse)
        {
            try
            {
                var csharpResponse = new Dictionary<string, object>();

                foreach (var kvp in pythonResponse)
                {
                    var csharpKey = ConvertToCSharpFieldName(kvp.Key);
                    var csharpValue = ConvertToCSharpValue(kvp.Value);
                    csharpResponse[csharpKey] = csharpValue;
                }

                _logger.LogDebug($"Transformed {pythonResponse.Count} fields to C# format");
                return csharpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transform response to C# format");
                throw;
            }
        }

        private Dictionary<string, string> InitializePascalToSnakeMapping()
        {
            return new Dictionary<string, string>
            {
                // Request field mappings
                ["DeviceId"] = "device_id",
                ["ModelId"] = "model_id",
                ["SessionId"] = "session_id",
                ["RequestId"] = "request_id",
                ["ImageData"] = "image_data",
                ["TargetWidth"] = "target_width",
                ["TargetHeight"] = "target_height",
                ["UpscaleFactor"] = "upscale_factor",
                ["EnhancementType"] = "enhancement_type",
                ["QualityLevel"] = "quality_level",
                ["StyleImage"] = "style_image",
                ["StyleStrength"] = "style_strength",
                ["FaceDetectionThreshold"] = "face_detection_threshold",
                ["FaceRestoreStrength"] = "face_restore_strength",
                ["BackgroundThreshold"] = "background_threshold",
                ["ColorBalance"] = "color_balance",
                ["Brightness"] = "brightness",
                ["Contrast"] = "contrast",
                ["Saturation"] = "saturation",
                ["BatchSize"] = "batch_size",
                ["ProcessingMode"] = "processing_mode",
                ["PreserveFaces"] = "preserve_faces",
                ["PreserveDetails"] = "preserve_details",
                ["NoiseReduction"] = "noise_reduction",
                ["EdgeEnhancement"] = "edge_enhancement",
                ["PerformanceMode"] = "performance_mode",
                ["OutputFormat"] = "output_format",
                ["CompressionQuality"] = "compression_quality",
                ["MetadataPreservation"] = "metadata_preservation",

                // Response field mappings
                ["ProcessedImage"] = "processed_image",
                ["ProcessingTimeMs"] = "processing_time_ms",
                ["QualityScore"] = "quality_score",
                ["ModelUsed"] = "model_used",
                ["PerformanceMetrics"] = "performance_metrics",
                ["ErrorMessage"] = "error_message",
                ["ErrorCode"] = "error_code",
                ["Warning"] = "warning",
                ["ProcessingSteps"] = "processing_steps",
                ["ResourceUsage"] = "resource_usage",
                ["OptimizationSuggestions"] = "optimization_suggestions",

                // Capability mappings
                ["SupportedOperations"] = "supported_operations",
                ["SupportedFormats"] = "supported_formats",
                ["MaxResolution"] = "max_resolution",
                ["MinResolution"] = "min_resolution",
                ["MemoryRequirement"] = "memory_requirement",
                ["ProcessingSpeed"] = "processing_speed",
                ["ModelSize"] = "model_size",
                ["IsAvailable"] = "is_available",
                ["LoadTime"] = "load_time",
                ["Precision"] = "precision",

                // Batch processing mappings
                ["BatchId"] = "batch_id",
                ["BatchStatus"] = "batch_status",
                ["TotalItems"] = "total_items",
                ["ProcessedItems"] = "processed_items",
                ["FailedItems"] = "failed_items",
                ["EstimatedTimeRemaining"] = "estimated_time_remaining",
                ["AverageProcessingTime"] = "average_processing_time",
                ["BatchProgress"] = "batch_progress",
                ["CurrentItem"] = "current_item",
                ["BatchErrors"] = "batch_errors",

                // Performance and analytics mappings
                ["MemoryUsageMB"] = "memory_usage_mb",
                ["VramUsageMB"] = "vram_usage_mb",
                ["CpuUsagePercent"] = "cpu_usage_percent",
                ["GpuUsagePercent"] = "gpu_usage_percent",
                ["ThroughputImagesPerSecond"] = "throughput_images_per_second",
                ["AverageQualityScore"] = "average_quality_score",
                ["SuccessRate"] = "success_rate",
                ["OptimizationLevel"] = "optimization_level",
                ["CacheHitRate"] = "cache_hit_rate",
                ["ModelLoadTime"] = "model_load_time"
            };
        }

        private Dictionary<string, string> InitializeSnakeToPascalMapping()
        {
            return _pascalToSnakeMapping.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }

        private string ConvertToPythonFieldName(string csharpFieldName)
        {
            if (_pascalToSnakeMapping.TryGetValue(csharpFieldName, out var mappedName))
            {
                return mappedName;
            }

            // Convert PascalCase to snake_case automatically
            return ConvertPascalCaseToSnakeCase(csharpFieldName);
        }

        private string ConvertToCSharpFieldName(string pythonFieldName)
        {
            if (_snakeToPascalMapping.TryGetValue(pythonFieldName, out var mappedName))
            {
                return mappedName;
            }

            // Convert snake_case to PascalCase automatically
            return ConvertSnakeCaseToPascalCase(pythonFieldName);
        }

        private string ConvertPascalCaseToSnakeCase(string pascalCase)
        {
            if (string.IsNullOrEmpty(pascalCase))
                return pascalCase;

            var result = new List<char>();
            for (int i = 0; i < pascalCase.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascalCase[i]))
                {
                    result.Add('_');
                }
                result.Add(char.ToLowerInvariant(pascalCase[i]));
            }

            return new string(result.ToArray());
        }

        private string ConvertSnakeCaseToPascalCase(string snakeCase)
        {
            if (string.IsNullOrEmpty(snakeCase))
                return snakeCase;

            var parts = snakeCase.Split('_');
            var result = string.Join("", parts.Select(part =>
                string.IsNullOrEmpty(part) ? part :
                char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant()));

            return result;
        }

        private object ConvertToPythonValue(object value)
        {
            if (value == null) return null;

            // Handle specific type conversions if needed
            return value switch
            {
                bool boolValue => boolValue,
                string stringValue => stringValue,
                int intValue => intValue,
                long longValue => longValue,
                float floatValue => floatValue,
                double doubleValue => doubleValue,
                decimal decimalValue => (double)decimalValue,
                DateTime dateTimeValue => dateTimeValue.ToString("O"), // ISO 8601 format
                Enum enumValue => enumValue.ToString().ToLowerInvariant(),
                IDictionary<string, object> dictValue => dictValue.ToDictionary(
                    kvp => ConvertToPythonFieldName(kvp.Key),
                    kvp => ConvertToPythonValue(kvp.Value)),
                IEnumerable<object> listValue => listValue.Select(ConvertToPythonValue).ToList(),
                _ => value
            };
        }

        private object ConvertToCSharpValue(object value)
        {
            if (value == null) return null;

            // Handle specific type conversions if needed
            return value switch
            {
                JsonElement jsonElement => ConvertJsonElementToCSharpValue(jsonElement),
                IDictionary<string, object> dictValue => dictValue.ToDictionary(
                    kvp => ConvertToCSharpFieldName(kvp.Key),
                    kvp => ConvertToCSharpValue(kvp.Value)),
                IEnumerable<object> listValue => listValue.Select(ConvertToCSharpValue).ToList(),
                _ => value
            };
        }

        private object ConvertJsonElementToCSharpValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                    prop => ConvertToCSharpFieldName(prop.Name),
                    prop => ConvertJsonElementToCSharpValue(prop.Value)),
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToCSharpValue).ToList(),
                _ => element.ToString()
            };
        }

        /// <summary>
        /// Test transformation performance and accuracy
        /// </summary>
        public async Task<(TimeSpan transformTime, int fieldsProcessed, bool success)> TestTransformationPerformanceAsync()
        {
            var testData = CreateTestTransformationData();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var pythonFormat = ToPythonFormat(testData);
                var csharpFormat = ToCSharpFormat(pythonFormat);
                
                stopwatch.Stop();
                
                var fieldsProcessed = testData.Count * 2; // To Python and back to C#
                var success = ValidateRoundTripTransformation(testData, csharpFormat);
                
                _logger.LogInformation($"Transformation test: {fieldsProcessed} fields in {stopwatch.Elapsed.TotalMilliseconds:F2}ms, Success: {success}");
                
                return (stopwatch.Elapsed, fieldsProcessed, success);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Transformation test failed");
                return (stopwatch.Elapsed, 0, false);
            }
        }

        private Dictionary<string, object> CreateTestTransformationData()
        {
            return new Dictionary<string, object>
            {
                ["DeviceId"] = "device-123",
                ["ModelId"] = "model-456",
                ["ImageData"] = "base64-encoded-image-data",
                ["TargetWidth"] = 1920,
                ["TargetHeight"] = 1080,
                ["UpscaleFactor"] = 2.0,
                ["QualityLevel"] = "high",
                ["ProcessingMode"] = "standard",
                ["PreserveFaces"] = true,
                ["NoiseReduction"] = 0.8,
                ["PerformanceMode"] = "quality",
                ["MetadataPreservation"] = true
            };
        }

        private bool ValidateRoundTripTransformation(Dictionary<string, object> original, Dictionary<string, object> transformed)
        {
            try
            {
                foreach (var kvp in original)
                {
                    if (!transformed.ContainsKey(kvp.Key))
                    {
                        _logger.LogWarning($"Missing key after round-trip transformation: {kvp.Key}");
                        return false;
                    }

                    // Basic value comparison (could be enhanced for complex types)
                    if (!Equals(kvp.Value, transformed[kvp.Key]))
                    {
                        _logger.LogWarning($"Value mismatch for key {kvp.Key}: {kvp.Value} != {transformed[kvp.Key]}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate round-trip transformation");
                return false;
            }
        }
    }
}
