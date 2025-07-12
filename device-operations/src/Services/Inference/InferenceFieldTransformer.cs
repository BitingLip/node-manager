using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeviceOperations.Services.Inference
{
    /// <summary>
    /// Field name transformation service for seamless C# ↔ Python communication
    /// Handles automatic PascalCase ↔ snake_case transformation with complex field mapping
    /// </summary>
    public class InferenceFieldTransformer
    {
        private readonly ILogger<InferenceFieldTransformer> _logger;
        
        // Field mapping dictionaries for complex transformations
        private readonly Dictionary<string, string> _csharpToPythonMapping = new()
        {
            // Common API field mappings
            { "ModelId", "model_id" },
            { "DeviceId", "device_id" },
            { "SessionId", "session_id" },
            { "InferenceId", "inference_id" },
            { "InferenceType", "inference_type" },
            { "BatchSize", "batch_size" },
            { "MaxTokens", "max_tokens" },
            { "MaxLength", "max_length" },
            { "NumSteps", "num_steps" },
            { "GuidanceScale", "guidance_scale" },
            { "NegativePrompt", "negative_prompt" },
            { "SeedValue", "seed_value" },
            { "UseSeed", "use_seed" },
            { "ImageWidth", "image_width" },
            { "ImageHeight", "image_height" },
            { "ExecutionTime", "execution_time" },
            { "CompletedAt", "completed_at" },
            { "StartedAt", "started_at" },
            { "LastUpdated", "last_updated" },
            { "QueuePosition", "queue_position" },
            { "ProcessingSpeed", "processing_speed" },
            { "ErrorMessage", "error_message" },
            { "ErrorCode", "error_code" },
            { "RequestId", "request_id" },
            { "ResponseId", "response_id" },
            
            // Device and Model specific fields
            { "ComputeCapability", "compute_capability" },
            { "MemoryAvailable", "memory_available" },
            { "MemoryUsed", "memory_used" },
            { "VramAvailable", "vram_available" },
            { "VramUsed", "vram_used" },
            { "ModelPath", "model_path" },
            { "ModelSize", "model_size" },
            { "ModelType", "model_type" },
            { "ModelVersion", "model_version" },
            { "ConfigPath", "config_path" },
            { "TokenizerPath", "tokenizer_path" },
            
            // Inference specific fields
            { "ControlNetType", "controlnet_type" },
            { "ControlNetModel", "controlnet_model" },
            { "ControlNetWeight", "controlnet_weight" },
            { "LoRAPath", "lora_path" },
            { "LoRAWeight", "lora_weight" },
            { "LoRAAlpha", "lora_alpha" },
            { "InpaintingMask", "inpainting_mask" },
            { "InpaintingMode", "inpainting_mode" },
            { "SafetyChecker", "safety_checker" },
            { "ContentFilter", "content_filter" },
            
            // Batch processing fields
            { "BatchId", "batch_id" },
            { "BatchIndex", "batch_index" },
            { "BatchTotal", "batch_total" },
            { "BatchProgress", "batch_progress" },
            { "BatchStatus", "batch_status" },
            { "ProcessedItems", "processed_items" },
            { "FailedItems", "failed_items" },
            { "SuccessItems", "success_items" },
            
            // Performance metrics
            { "ThroughputMps", "throughput_mps" },
            { "LatencyMs", "latency_ms" },
            { "MemoryPeakMb", "memory_peak_mb" },
            { "GpuUtilization", "gpu_utilization" },
            { "CpuUtilization", "cpu_utilization" },
            { "PowerUsage", "power_usage" }
        };

        private readonly Dictionary<string, string> _pythonToCsharpMapping;

        public InferenceFieldTransformer(ILogger<InferenceFieldTransformer> logger)
        {
            _logger = logger;
            
            // Create reverse mapping for Python → C#
            _pythonToCsharpMapping = _csharpToPythonMapping.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }

        /// <summary>
        /// Transform C# object to Python-compatible format with snake_case field names
        /// </summary>
        /// <typeparam name="T">Type of the input object</typeparam>
        /// <param name="csharpObject">C# object to transform</param>
        /// <returns>Dictionary with Python-compatible field names</returns>
        public Dictionary<string, object> ToPythonFormat<T>(T csharpObject)
        {
            try
            {
                if (csharpObject == null)
                    return new Dictionary<string, object>();

                _logger.LogDebug($"Transforming C# object of type {typeof(T).Name} to Python format");

                // Serialize to JSON and parse to get field names
                var json = JsonSerializer.Serialize(csharpObject, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                
                var jsonDocument = JsonDocument.Parse(json);
                var result = new Dictionary<string, object>();

                foreach (var property in jsonDocument.RootElement.EnumerateObject())
                {
                    var csharpFieldName = ToPascalCase(property.Name);
                    var pythonFieldName = TransformFieldNameToPython(csharpFieldName);
                    var value = ExtractJsonValue(property.Value);

                    result[pythonFieldName] = value;
                }

                _logger.LogDebug($"Transformed {result.Count} fields to Python format");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to transform C# object to Python format");
                throw new InvalidOperationException($"Field transformation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Transform Python response to C#-compatible format with PascalCase field names
        /// </summary>
        /// <param name="pythonResponse">Python response object</param>
        /// <returns>Dictionary with C#-compatible field names</returns>
        public Dictionary<string, object> ToCSharpFormat(object pythonResponse)
        {
            try
            {
                if (pythonResponse == null)
                    return new Dictionary<string, object>();

                _logger.LogDebug("Transforming Python response to C# format");

                var result = new Dictionary<string, object>();

                if (pythonResponse is JsonElement jsonElement)
                {
                    ProcessJsonElement(jsonElement, result);
                }
                else if (pythonResponse is Dictionary<string, object> dict)
                {
                    foreach (var kvp in dict)
                    {
                        var csharpFieldName = TransformFieldNameToCSharp(kvp.Key);
                        result[csharpFieldName] = TransformValueToCSharp(kvp.Value);
                    }
                }
                else
                {
                    // Try to serialize and parse as JSON
                    var json = JsonSerializer.Serialize(pythonResponse);
                    var jsonDoc = JsonDocument.Parse(json);
                    ProcessJsonElement(jsonDoc.RootElement, result);
                }

                _logger.LogDebug($"Transformed {result.Count} fields to C# format");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transform Python response to C# format");
                throw new InvalidOperationException($"Response transformation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Transform individual field name from C# PascalCase to Python snake_case
        /// </summary>
        /// <param name="csharpFieldName">C# field name in PascalCase</param>
        /// <returns>Python field name in snake_case</returns>
        public string TransformFieldNameToPython(string csharpFieldName)
        {
            if (string.IsNullOrWhiteSpace(csharpFieldName))
                return csharpFieldName;

            // Check explicit mapping first
            if (_csharpToPythonMapping.TryGetValue(csharpFieldName, out var explicitMapping))
            {
                return explicitMapping;
            }

            // Convert PascalCase to snake_case
            return ToSnakeCase(csharpFieldName);
        }

        /// <summary>
        /// Transform individual field name from Python snake_case to C# PascalCase
        /// </summary>
        /// <param name="pythonFieldName">Python field name in snake_case</param>
        /// <returns>C# field name in PascalCase</returns>
        public string TransformFieldNameToCSharp(string pythonFieldName)
        {
            if (string.IsNullOrWhiteSpace(pythonFieldName))
                return pythonFieldName;

            // Check explicit mapping first
            if (_pythonToCsharpMapping.TryGetValue(pythonFieldName, out var explicitMapping))
            {
                return explicitMapping;
            }

            // Convert snake_case to PascalCase
            return ToPascalCase(pythonFieldName);
        }

        #region Private Helper Methods

        private void ProcessJsonElement(JsonElement element, Dictionary<string, object> result)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var csharpFieldName = TransformFieldNameToCSharp(property.Name);
                    result[csharpFieldName] = ExtractJsonValue(property.Value);
                }
            }
        }

        private object ExtractJsonValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var intValue))
                        return intValue;
                    if (element.TryGetInt64(out var longValue))
                        return longValue;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Object:
                    var objResult = new Dictionary<string, object>();
                    ProcessJsonElement(element, objResult);
                    return objResult;
                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(ExtractJsonValue).ToList();
                default:
                    return element.ToString();
            }
        }

        private object TransformValueToCSharp(object value)
        {
            if (value is Dictionary<string, object> dict)
            {
                return ToCSharpFormat(dict);
            }
            return value;
        }

        private string ToSnakeCase(string pascalCase)
        {
            if (string.IsNullOrWhiteSpace(pascalCase))
                return pascalCase;

            // Insert underscore before uppercase letters (except first character)
            var snakeCase = Regex.Replace(pascalCase, @"(?<!^)([A-Z])", "_$1").ToLowerInvariant();
            return snakeCase;
        }

        private string ToPascalCase(string snakeCase)
        {
            if (string.IsNullOrWhiteSpace(snakeCase))
                return snakeCase;

            // Split by underscore and capitalize each part
            var parts = snakeCase.Split('_');
            var pascalCase = string.Join("", parts.Select(part => 
                string.IsNullOrEmpty(part) ? part : char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant()));
            
            return pascalCase;
        }

        #endregion

        #region Performance Monitoring

        /// <summary>
        /// Test field transformation accuracy and performance
        /// </summary>
        /// <returns>Performance test results</returns>
        public async Task<Dictionary<string, object>> TestTransformationPerformanceAsync()
        {
            try
            {
                _logger.LogInformation("Running field transformation performance tests");

                var testData = new Dictionary<string, object>
                {
                    ["ModelId"] = "test-model-123",
                    ["DeviceId"] = "device-456",
                    ["InferenceType"] = "TextGeneration",
                    ["BatchSize"] = 4,
                    ["MaxTokens"] = 2048,
                    ["GuidanceScale"] = 7.5,
                    ["ComputeCapability"] = "8.9",
                    ["VramAvailable"] = 12884901888L
                };

                var startTime = DateTime.UtcNow;
                var iterations = 1000;

                // Test C# to Python transformation performance
                for (int i = 0; i < iterations; i++)
                {
                    var pythonFormat = ToPythonFormat(testData);
                }

                var csharpToPythonTime = DateTime.UtcNow - startTime;

                // Test Python to C# transformation performance
                var pythonData = ToPythonFormat(testData);
                startTime = DateTime.UtcNow;

                for (int i = 0; i < iterations; i++)
                {
                    var csharpFormat = ToCSharpFormat(pythonData);
                }

                var pythonToCsharpTime = DateTime.UtcNow - startTime;

                // Test transformation accuracy
                var pythonTransformed = ToPythonFormat(testData);
                var csharpTransformed = ToCSharpFormat(pythonTransformed);

                var accuracyScore = CalculateTransformationAccuracy(testData, csharpTransformed);

                var results = new Dictionary<string, object>
                {
                    ["test_timestamp"] = DateTime.UtcNow,
                    ["iterations"] = iterations,
                    ["csharp_to_python_ms"] = csharpToPythonTime.TotalMilliseconds,
                    ["python_to_csharp_ms"] = pythonToCsharpTime.TotalMilliseconds,
                    ["accuracy_score"] = accuracyScore,
                    ["field_mappings_count"] = _csharpToPythonMapping.Count,
                    ["avg_transformation_time_us"] = (csharpToPythonTime.TotalMicroseconds + pythonToCsharpTime.TotalMicroseconds) / (2 * iterations),
                    ["performance_rating"] = CalculatePerformanceRating(csharpToPythonTime, pythonToCsharpTime, accuracyScore)
                };

                _logger.LogInformation($"Transformation performance test completed - Accuracy: {accuracyScore:P2}");
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run transformation performance test");
                throw;
            }
        }

        private double CalculateTransformationAccuracy(Dictionary<string, object> original, Dictionary<string, object> transformed)
        {
            if (original.Count == 0) return 1.0;

            int matchCount = 0;
            foreach (var kvp in original)
            {
                if (transformed.ContainsKey(kvp.Key) && 
                    Equals(transformed[kvp.Key], kvp.Value))
                {
                    matchCount++;
                }
            }

            return (double)matchCount / original.Count;
        }

        private string CalculatePerformanceRating(TimeSpan csharpToPython, TimeSpan pythonToCsharp, double accuracy)
        {
            var avgTimeMs = (csharpToPython.TotalMilliseconds + pythonToCsharp.TotalMilliseconds) / 2;
            
            if (accuracy >= 0.95 && avgTimeMs < 1.0)
                return "EXCELLENT";
            else if (accuracy >= 0.90 && avgTimeMs < 5.0)
                return "GOOD";
            else if (accuracy >= 0.80 && avgTimeMs < 10.0)
                return "ACCEPTABLE";
            else
                return "NEEDS_IMPROVEMENT";
        }

        #endregion
    }
}
