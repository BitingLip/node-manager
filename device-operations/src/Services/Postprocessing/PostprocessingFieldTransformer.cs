using System.Text.Json;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Postprocessing;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;

namespace DeviceOperations.Services.Postprocessing
{
    /// <summary>
    /// Enhanced field transformation service for postprocessing operations
    /// Handles PascalCase ↔ snake_case conversion, field mapping, and complex object transformation
    /// Phase 4 Enhancement: Supports complex nested objects and cross-domain compatibility
    /// </summary>
    public class PostprocessingFieldTransformer
    {
        private readonly ILogger<PostprocessingFieldTransformer> _logger;
        private readonly Dictionary<string, string> _pascalToSnakeMapping;
        private readonly Dictionary<string, string> _snakeToPascalMapping;
        private readonly IMemoryCache _transformationCache;
        private readonly Dictionary<Type, Func<object, Dictionary<string, object>>> _customTypeTransformers;
        
        // Phase 4 Enhancement: Large payload optimization threshold
        private const int LARGE_PAYLOAD_THRESHOLD = 1024 * 1024; // 1MB
        private const int MAX_TRANSFORMATION_CACHE_SIZE = 1000;

        public PostprocessingFieldTransformer(
            ILogger<PostprocessingFieldTransformer> logger,
            IMemoryCache memoryCache)
        {
            _logger = logger;
            _transformationCache = memoryCache;
            _pascalToSnakeMapping = InitializePascalToSnakeMapping();
            _snakeToPascalMapping = InitializeSnakeToPascalMapping();
            _customTypeTransformers = InitializeCustomTypeTransformers();
        }

        /// <summary>
        /// Phase 4 Enhancement: Estimate payload size for optimization decisions
        /// </summary>
        private long EstimatePayloadSize(Dictionary<string, object> payload)
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(payload);
                return System.Text.Encoding.UTF8.GetByteCount(jsonString);
            }
            catch
            {
                // Fallback estimation based on field count
                return payload.Count * 100; // Rough estimate of 100 bytes per field
            }
        }

        /// <summary>
        /// Phase 4 Enhancement: Generate cache key for transformation patterns
        /// </summary>
        private string GenerateTransformationCacheKey(Dictionary<string, object> payload)
        {
            var keyElements = new List<string>();
            
            // Use payload structure for cache key
            foreach (var kvp in payload.Take(5)) // Use first 5 fields for cache key
            {
                var valueType = kvp.Value?.GetType().Name ?? "null";
                keyElements.Add($"{kvp.Key}:{valueType}");
            }
            
            keyElements.Add($"count:{payload.Count}");
            return string.Join("|", keyElements);
        }

        /// <summary>
        /// Phase 4 Enhancement: Adapt cached transformation to current payload
        /// </summary>
        private Dictionary<string, object>? AdaptCachedTransformation(object cachedPattern, Dictionary<string, object> payload)
        {
            try
            {
                if (cachedPattern is not Dictionary<string, object> pattern)
                    return null;
                
                var result = new Dictionary<string, object>();
                
                foreach (var kvp in payload)
                {
                    var pythonKey = ConvertToPythonFieldName(kvp.Key);
                    var pythonValue = ConvertToPythonValue(kvp.Value);
                    result[pythonKey] = pythonValue;
                }
                
                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Phase 4 Enhancement: Cache transformation pattern for reuse
        /// </summary>
        private void CacheTransformationPattern(string cacheKey, Dictionary<string, object> result, Dictionary<string, object> original)
        {
            try
            {
                var pattern = ExtractTransformationPattern(original, result);
                
                _transformationCache.Set(cacheKey, pattern, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30),
                    Priority = CacheItemPriority.High,
                    Size = EstimatePatternSize(pattern)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache transformation pattern");
            }
        }

        /// <summary>
        /// Phase 4 Enhancement: Extract transformation pattern for caching
        /// </summary>
        private Dictionary<string, object> ExtractTransformationPattern(Dictionary<string, object> original, Dictionary<string, object> result)
        {
            var pattern = new Dictionary<string, object>();
            
            foreach (var kvp in original)
            {
                var originalType = kvp.Value?.GetType().Name ?? "null";
                var pythonKey = ConvertToPythonFieldName(kvp.Key);
                
                pattern[kvp.Key] = new { pythonKey, originalType };
            }
            
            return pattern;
        }

        /// <summary>
        /// Phase 4 Enhancement: Estimate pattern size for cache management
        /// </summary>
        private long EstimatePatternSize(Dictionary<string, object> pattern)
        {
            return pattern.Count * 50; // Rough estimate of 50 bytes per pattern entry
        }

        /// <summary>
        /// Phase 4 Enhancement: Check if object is complex (nested)
        /// </summary>
        private bool IsComplexObject(object? value)
        {
            if (value == null) return false;
            
            var type = value.GetType();
            
            // Check for collection types
            if (value is IEnumerable<object> || value is IDictionary<string, object>)
                return true;
                
            // Check for custom objects (not primitives or strings)
            return !type.IsPrimitive && type != typeof(string) && type != typeof(DateTime) && 
                   type != typeof(decimal) && type != typeof(double) && type != typeof(float);
        }

        /// <summary>
        /// Phase 4 Enhancement: Transform complex objects with nested support
        /// </summary>
        private object TransformComplexObject(object value)
        {
            try
            {
                if (value is IDictionary<string, object> dict)
                {
                    var result = new Dictionary<string, object>();
                    foreach (var kvp in dict)
                    {
                        var pythonKey = ConvertToPythonFieldName(kvp.Key);
                        var pythonValue = ConvertToPythonValue(kvp.Value);
                        result[pythonKey] = pythonValue;
                    }
                    return result;
                }
                
                if (value is IEnumerable<object> enumerable)
                {
                    return enumerable.Select(ConvertToPythonValue).ToList();
                }
                
                // Use reflection for other complex objects
                return TransformObjectUsingReflection(value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to transform complex object of type {value?.GetType()}");
                return value ?? new object(); // Return safe object if value is null
            }
        }

        /// <summary>
        /// Phase 4 Enhancement: Optimize transformation for large payloads
        /// </summary>
        private Dictionary<string, object> OptimizeTransformationForLargePayload(Dictionary<string, object> payload)
        {
            _logger.LogInformation($"Optimizing transformation for large payload ({EstimatePayloadSize(payload)} bytes)");
            
            var result = new Dictionary<string, object>();
            
            // Process in batches for memory efficiency
            const int batchSize = 100;
            var keys = payload.Keys.ToList();
            
            for (int i = 0; i < keys.Count; i += batchSize)
            {
                var batch = keys.Skip(i).Take(batchSize);
                
                foreach (var key in batch)
                {
                    try
                    {
                        var pythonKey = ConvertToPythonFieldName(key);
                        var pythonValue = ConvertToPythonValue(payload[key]);
                        result[pythonKey] = pythonValue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to transform field {key} in large payload");
                    }
                }
                
                // Allow garbage collection between batches
                if (i % (batchSize * 10) == 0)
                {
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Transform C# request to Python format with enhanced capabilities
        /// Phase 4 Enhancement: Supports large payloads and complex objects
        /// </summary>
        public Dictionary<string, object> ToPythonFormat(Dictionary<string, object> csharpRequest)
        {
            try
            {
                var payloadSize = EstimatePayloadSize(csharpRequest);
                
                // Phase 4 Enhancement: Optimize for large payloads
                if (payloadSize > LARGE_PAYLOAD_THRESHOLD)
                {
                    return OptimizeTransformationForLargePayload(csharpRequest);
                }
                
                // Check cache for repeated patterns
                var cacheKey = GenerateTransformationCacheKey(csharpRequest);
                if (_transformationCache.TryGetValue(cacheKey, out var cachedPattern) && cachedPattern != null)
                {
                    var adaptedResult = AdaptCachedTransformation(cachedPattern, csharpRequest);
                    if (adaptedResult != null)
                    {
                        _logger.LogDebug($"Used cached transformation pattern for {csharpRequest.Count} fields");
                        return adaptedResult;
                    }
                }

                var pythonRequest = new Dictionary<string, object>();

                foreach (var kvp in csharpRequest)
                {
                    var pythonKey = ConvertToPythonFieldName(kvp.Key);
                    var pythonValue = ConvertToPythonValue(kvp.Value);
                    pythonRequest[pythonKey] = pythonValue;
                }

                // Cache transformation pattern for reuse
                CacheTransformationPattern(cacheKey, pythonRequest, csharpRequest);

                _logger.LogDebug($"Transformed {csharpRequest.Count} fields to Python format (Size: {payloadSize} bytes)");
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

            // Phase 4 Enhancement: Enhanced type conversion with complex object support
            return value switch
            {
                // Basic types (existing)
                bool boolValue => boolValue,
                string stringValue => stringValue,
                int intValue => intValue,
                long longValue => longValue,
                float floatValue => floatValue,
                double doubleValue => doubleValue,
                decimal decimalValue => (double)decimalValue,
                DateTime dateTimeValue => dateTimeValue.ToString("O"), // ISO 8601 format
                Enum enumValue => enumValue.ToString().ToLowerInvariant(),
                
                // Phase 4 NEW: Complex postprocessing objects
                IPostprocessingModel modelValue => TransformPostprocessingModel(modelValue),
                IPostprocessingConfiguration configValue => TransformConfigurationObject(configValue),
                IPostprocessingMetrics metricsValue => TransformMetricsObject(metricsValue),
                
                // Phase 4 NEW: Custom type handlers
                var customValue when _customTypeTransformers.ContainsKey(customValue.GetType()) =>
                    _customTypeTransformers[customValue.GetType()](customValue),
                
                // Enhanced nested objects (existing but improved)
                IDictionary<string, object> dictValue => dictValue.ToDictionary(
                    kvp => ConvertToPythonFieldName(kvp.Key),
                    kvp => ConvertToPythonValue(kvp.Value)),
                
                // Phase 4 NEW: Enhanced array processing with type hints
                IEnumerable<object> listValue => TransformArrayWithTypeHints(listValue),
                
                // Phase 4 NEW: Reflection-based complex object transformation
                _ when IsComplexObject(value) => TransformComplexObject(value),
                
                // Fallback (existing)
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
        public Task<(TimeSpan transformTime, int fieldsProcessed, bool success)> TestTransformationPerformanceAsync()
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
                
                return Task.FromResult((stopwatch.Elapsed, fieldsProcessed, success));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Transformation test failed");
                return Task.FromResult((stopwatch.Elapsed, 0, false));
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

        #region Phase 4 Enhancement Methods

        /// <summary>
        /// Initialize custom type transformers for complex objects
        /// </summary>
        private Dictionary<Type, Func<object, Dictionary<string, object>>> InitializeCustomTypeTransformers()
        {
            return new Dictionary<Type, Func<object, Dictionary<string, object>>>
            {
                // Add custom transformers as needed for specific types
                // Example: [typeof(SomeCustomType)] = obj => TransformSomeCustomType((SomeCustomType)obj)
            };
        }

        /// <summary>
        /// Transform postprocessing model objects
        /// </summary>
        private Dictionary<string, object> TransformPostprocessingModel(IPostprocessingModel model)
        {
            return new Dictionary<string, object>
            {
                ["model_id"] = model.ModelId,
                ["model_type"] = model.ModelType?.ToString().ToLowerInvariant(),
                ["capabilities"] = TransformModelCapabilities(model.Capabilities),
                ["optimization_level"] = model.OptimizationLevel?.ToString().ToLowerInvariant(),
                ["memory_requirements"] = TransformMemoryRequirements(model.MemoryRequirements)
            };
        }

        /// <summary>
        /// Transform configuration objects
        /// </summary>
        private Dictionary<string, object> TransformConfigurationObject(IPostprocessingConfiguration config)
        {
            var result = new Dictionary<string, object>();
            
            // Use reflection to transform all public properties
            var properties = config.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var pythonKey = ConvertToPythonFieldName(prop.Name);
                var value = prop.GetValue(config);
                result[pythonKey] = ConvertToPythonValue(value);
            }
            
            return result;
        }

        /// <summary>
        /// Transform metrics objects
        /// </summary>
        private Dictionary<string, object> TransformMetricsObject(IPostprocessingMetrics metrics)
        {
            var result = new Dictionary<string, object>();
            
            // Use reflection to transform all public properties
            var properties = metrics.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var pythonKey = ConvertToPythonFieldName(prop.Name);
                var value = prop.GetValue(metrics);
                result[pythonKey] = ConvertToPythonValue(value);
            }
            
            return result;
        }

        /// <summary>
        /// Transform model capabilities
        /// </summary>
        private Dictionary<string, object> TransformModelCapabilities(object capabilities)
        {
            if (capabilities == null) return new Dictionary<string, object>();
            
            var result = new Dictionary<string, object>();
            var properties = capabilities.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var prop in properties)
            {
                var pythonKey = ConvertToPythonFieldName(prop.Name);
                var value = prop.GetValue(capabilities);
                result[pythonKey] = ConvertToPythonValue(value);
            }
            
            return result;
        }

        /// <summary>
        /// Transform memory requirements
        /// </summary>
        private Dictionary<string, object> TransformMemoryRequirements(object memoryRequirements)
        {
            if (memoryRequirements == null) return new Dictionary<string, object>();
            
            var result = new Dictionary<string, object>();
            var properties = memoryRequirements.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var prop in properties)
            {
                var pythonKey = ConvertToPythonFieldName(prop.Name);
                var value = prop.GetValue(memoryRequirements);
                result[pythonKey] = ConvertToPythonValue(value);
            }
            
            return result;
        }

        /// <summary>
        /// Transform arrays with type hints for better performance
        /// </summary>
        private List<object> TransformArrayWithTypeHints(IEnumerable<object> listValue)
        {
            var list = listValue.ToList();
            if (!list.Any()) return new List<object>();
            
            // Detect common type for optimization
            var firstItemType = list.First()?.GetType();
            var isHomogeneous = list.All(item => item?.GetType() == firstItemType);
            
            if (isHomogeneous && firstItemType != null)
            {
                // Optimized transformation for homogeneous arrays
                return TransformHomogeneousArray(list, firstItemType);
            }
            
            // Standard transformation for heterogeneous arrays
            return list.Select(ConvertToPythonValue).ToList();
        }

        /// <summary>
        /// Phase 4 Enhancement: Optimized transformation for homogeneous arrays
        /// </summary>
        private List<object> TransformHomogeneousArray(List<object> items, Type itemType)
        {
            // Use specialized transformation for known types
            if (typeof(IPostprocessingModel).IsAssignableFrom(itemType))
            {
                return items.Cast<IPostprocessingModel>()
                           .Select(TransformPostprocessingModel)
                           .Cast<object>()
                           .ToList();
            }
            
            if (typeof(IPostprocessingConfiguration).IsAssignableFrom(itemType))
            {
                return items.Cast<IPostprocessingConfiguration>()
                           .Select(TransformConfigurationObject)
                           .Cast<object>()
                           .ToList();
            }
            
            // Fallback to standard transformation
            return items.Select(ConvertToPythonValue).ToList();
        }

        /// <summary>
        /// Phase 4 Enhancement: Comprehensive transformation testing
        /// </summary>
        public async Task<TransformationTestResult> TestComplexTransformationAsync()
        {
            try
            {
                var complexTestData = CreateComplexTestData();
                var accuracyTests = new List<TransformationAccuracyTest>();
                
                foreach (var testCase in complexTestData)
                {
                    var result = await ExecuteTransformationTest(testCase);
                    accuracyTests.Add(result);
                }
                
                return new TransformationTestResult
                {
                    TotalTests = accuracyTests.Count,
                    PassedTests = accuracyTests.Count(t => t.Success),
                    AccuracyScore = accuracyTests.Average(t => t.AccuracyScore),
                    ComplexObjectSupport = AnalyzeComplexObjectSupport(accuracyTests),
                    EdgeCaseHandling = AnalyzeEdgeCaseHandling(accuracyTests)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during complex transformation testing");
                return new TransformationTestResult
                {
                    TotalTests = 0,
                    PassedTests = 0,
                    AccuracyScore = 0.0,
                    ComplexObjectSupport = false,
                    EdgeCaseHandling = false
                };
            }
        }

        /// <summary>
        /// Phase 4 Enhancement: Transform large payloads in chunks
        /// </summary>
        private Dictionary<string, object> TransformInChunks(Dictionary<string, object> payload)
        {
            var result = new Dictionary<string, object>();
            var chunkSize = 100; // Process 100 fields at a time
            var chunks = payload.Keys.Select((key, index) => new { key, index })
                                   .GroupBy(x => x.index / chunkSize)
                                   .Select(g => g.Select(x => x.key).ToList());

            foreach (var chunk in chunks)
            {
                var chunkDict = chunk.ToDictionary(key => key, key => payload[key]);
                var transformedChunk = TransformChunk(chunkDict);
                
                foreach (var kvp in transformedChunk)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Phase 4 Enhancement: Transform a chunk of the payload
        /// </summary>
        private Dictionary<string, object> TransformChunk(Dictionary<string, object> chunk)
        {
            var result = new Dictionary<string, object>();
            
            foreach (var kvp in chunk)
            {
                var pythonKey = ConvertToPythonFieldName(kvp.Key);
                var pythonValue = ConvertToPythonValue(kvp.Value);
                result[pythonKey] = pythonValue;
            }
            
            return result;
        }

        /// <summary>
        /// Phase 4 Enhancement: Transform model capabilities
        /// </summary>
        private Dictionary<string, object> TransformModelCapabilities(Dictionary<string, object> capabilities)
        {
            var result = new Dictionary<string, object>();
            
            foreach (var kvp in capabilities)
            {
                var pythonKey = ConvertToPythonFieldName(kvp.Key);
                var pythonValue = ConvertToPythonValue(kvp.Value);
                result[pythonKey] = pythonValue;
            }
            
            return result;
        }

        /// <summary>
        /// Phase 4 Enhancement: Transform memory requirements
        /// </summary>
        private Dictionary<string, object> TransformMemoryRequirements(MemoryRequirements requirements)
        {
            return new Dictionary<string, object>
            {
                ["minimum_memory_mb"] = requirements.MinimumMemoryMB,
                ["recommended_memory_mb"] = requirements.RecommendedMemoryMB,
                ["maximum_memory_mb"] = requirements.MaximumMemoryMB,
                ["requires_gpu"] = requirements.RequiresGPU,
                ["minimum_vram_mb"] = requirements.MinimumVRAMMB
            };
        }

        /// <summary>
        /// Phase 4 Enhancement: Transform performance metrics
        /// </summary>
        private Dictionary<string, object> TransformPerformanceMetrics(PostprocessingPerformanceMetrics metrics)
        {
            var result = new Dictionary<string, object>();
            
            // Use reflection to safely access available properties
            var metricsType = metrics.GetType();
            var properties = metricsType.GetProperties();
            
            foreach (var property in properties)
            {
                try
                {
                    var value = property.GetValue(metrics);
                    if (value != null)
                    {
                        var pythonKey = ConvertToPythonFieldName(property.Name);
                        result[pythonKey] = ConvertToPythonValue(value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to transform metrics property {property.Name}");
                }
            }
            
            return result;
        }

        /// <summary>
        /// Phase 4 Enhancement: Transform object using reflection as fallback
        /// </summary>
        private Dictionary<string, object> TransformObjectUsingReflection(object obj)
        {
            var result = new Dictionary<string, object>();
            
            try
            {
                var properties = obj.GetType().GetProperties();
                
                foreach (var property in properties)
                {
                    try
                    {
                        var value = property.GetValue(obj);
                        if (value != null)
                        {
                            var pythonKey = ConvertToPythonFieldName(property.Name);
                            var pythonValue = ConvertToPythonValue(value);
                            result[pythonKey] = pythonValue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to transform property {property.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transform object using reflection");
            }
            
            return result;
        }

        /// <summary>
        /// Phase 4 Enhancement: Create complex test data for transformation testing
        /// </summary>
        private List<TransformationTestCase> CreateComplexTestData()
        {
            return new List<TransformationTestCase>
            {
                // Simple object test
                new TransformationTestCase
                {
                    TestName = "SimpleObject",
                    InputData = new Dictionary<string, object>
                    {
                        ["DeviceId"] = "device-123",
                        ["ModelType"] = "Upscaler",
                        ["QualityLevel"] = 85.5
                    },
                    IsComplexObject = false,
                    IsLargePayload = false
                },
                
                // Complex nested object test
                new TransformationTestCase
                {
                    TestName = "ComplexNestedObject",
                    InputData = new Dictionary<string, object>
                    {
                        ["RequestId"] = "req-456",
                        ["Configuration"] = new Dictionary<string, object>
                        {
                            ["UpscaleFactor"] = 2.0,
                            ["PreserveFaces"] = true,
                            ["NoiseReduction"] = 0.7
                        },
                        ["ModelSettings"] = new Dictionary<string, object>
                        {
                            ["ModelType"] = "ESRGAN",
                            ["MemoryUsage"] = 1024
                        }
                    },
                    IsComplexObject = true,
                    IsLargePayload = false
                },
                
                // Array handling test
                new TransformationTestCase
                {
                    TestName = "ArrayHandling",
                    InputData = new Dictionary<string, object>
                    {
                        ["BatchItems"] = new List<object>
                        {
                            new Dictionary<string, object> { ["ImageId"] = "img1", ["Size"] = 512 },
                            new Dictionary<string, object> { ["ImageId"] = "img2", ["Size"] = 1024 }
                        },
                        ["SupportedFormats"] = new List<string> { "PNG", "JPEG", "WEBP" }
                    },
                    IsComplexObject = true,
                    IsLargePayload = false
                }
            };
        }

        /// <summary>
        /// Phase 4 Enhancement: Execute transformation test
        /// </summary>
        private Task<TransformationAccuracyTest> ExecuteTransformationTest(TransformationTestCase testCase)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var result = ToPythonFormat(testCase.InputData);
                stopwatch.Stop();
                
                var success = ValidateTransformationResult(testCase, result);
                var accuracy = CalculateTransformationAccuracy(testCase, result);
                
                return Task.FromResult(new TransformationAccuracyTest
                {
                    TestName = testCase.TestName,
                    Success = success,
                    AccuracyScore = accuracy,
                    TestDuration = stopwatch.Elapsed,
                    TestData = result
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                return Task.FromResult(new TransformationAccuracyTest
                {
                    TestName = testCase.TestName,
                    Success = false,
                    AccuracyScore = 0.0,
                    ErrorMessage = ex.Message,
                    TestDuration = stopwatch.Elapsed
                });
            }
        }

        /// <summary>
        /// Phase 4 Enhancement: Validate transformation result
        /// </summary>
        private bool ValidateTransformationResult(TransformationTestCase testCase, Dictionary<string, object> result)
        {
            try
            {
                // Check that all input keys are transformed
                foreach (var inputKey in testCase.InputData.Keys)
                {
                    var expectedPythonKey = ConvertToPythonFieldName(inputKey);
                    if (!result.ContainsKey(expectedPythonKey))
                    {
                        return false;
                    }
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Phase 4 Enhancement: Calculate transformation accuracy
        /// </summary>
        private double CalculateTransformationAccuracy(TransformationTestCase testCase, Dictionary<string, object> result)
        {
            try
            {
                var correctTransformations = 0;
                var totalTransformations = testCase.InputData.Count;
                
                foreach (var inputKvp in testCase.InputData)
                {
                    var expectedPythonKey = ConvertToPythonFieldName(inputKvp.Key);
                    if (result.ContainsKey(expectedPythonKey))
                    {
                        correctTransformations++;
                    }
                }
                
                return totalTransformations > 0 ? (double)correctTransformations / totalTransformations : 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Phase 4 Enhancement: Analyze complex object support
        /// </summary>
        private bool AnalyzeComplexObjectSupport(List<TransformationAccuracyTest> tests)
        {
            var complexObjectTests = tests.Where(t => t.TestName.Contains("Complex") || t.TestName.Contains("Nested"));
            return complexObjectTests.Any() && complexObjectTests.All(t => t.Success && t.AccuracyScore > 0.9);
        }

        /// <summary>
        /// Phase 4 Enhancement: Analyze edge case handling
        /// </summary>
        private bool AnalyzeEdgeCaseHandling(List<TransformationAccuracyTest> tests)
        {
            var edgeCaseTests = tests.Where(t => t.TestName.Contains("Array") || t.TestName.Contains("Edge"));
            return edgeCaseTests.Any() && edgeCaseTests.All(t => t.Success && t.AccuracyScore > 0.8);
        }

        #endregion

        #region Helper Classes for Phase 4 Enhancements

        public class TransformationTestResult
        {
            public int TotalTests { get; set; }
            public int PassedTests { get; set; }
            public double AccuracyScore { get; set; }
            public bool ComplexObjectSupport { get; set; }
            public bool EdgeCaseHandling { get; set; }
        }

        public class TransformationAccuracyTest
        {
            public bool Success { get; set; }
            public double AccuracyScore { get; set; }
            public TimeSpan ProcessingTime { get; set; }
            public int FieldCount { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public string TestName { get; set; } = string.Empty;
            public Dictionary<string, object> TestData { get; set; } = new Dictionary<string, object>();
            public TimeSpan TestDuration { get; set; }
        }

        #endregion
    }

    #region Interface Definitions for Phase 4 Enhancements

    public interface IPostprocessingModel
    {
        string ModelId { get; }
        object ModelType { get; }
        object Capabilities { get; }
        object OptimizationLevel { get; }
        object MemoryRequirements { get; }
    }

    public interface IPostprocessingConfiguration
    {
        // Marker interface for configuration objects
    }

    public interface IPostprocessingMetrics
    {
        // Marker interface for metrics objects
    }

    #endregion
}
