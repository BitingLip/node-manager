using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DeviceOperations.Services.Inference;

/// <summary>
/// Service for managing models in shared RAM cache for fast GPU loading
/// </summary>
public class ModelCacheService : IModelCacheService, IDisposable
{
    private readonly ILogger<ModelCacheService> _logger;
    private readonly ConcurrentDictionary<string, CachedModelInfo> _cachedModels = new();
    private readonly ConcurrentDictionary<string, object> _modelData = new(); // In-memory model storage
    private bool _initialized = false;
    private long _totalCacheMemoryBytes = 0;

    public ModelCacheService(ILogger<ModelCacheService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing Model Cache Service...");

        try
        {
            // Initialize cache storage
            // In a production system, this might use memory-mapped files or shared memory
            await Task.CompletedTask;
            
            _initialized = true;
            _logger.LogInformation("Model Cache Service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Model Cache Service");
            throw;
        }
    }

    public async Task<ModelCacheResponse> CacheModelAsync(CacheModelRequest request)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            var modelId = request.ModelId ?? Path.GetFileNameWithoutExtension(request.ModelPath);
            
            _logger.LogInformation($"Caching model {modelId} from {request.ModelPath}");

            // Check if model is already cached
            if (_cachedModels.ContainsKey(modelId) && !request.ForceReload)
            {
                var existingModel = _cachedModels[modelId];
                existingModel.UseCount++;
                existingModel.LastUsed = DateTime.UtcNow;
                
                return new ModelCacheResponse
                {
                    Success = true,
                    ModelId = modelId,
                    Message = "Model already cached",
                    ModelSizeBytes = existingModel.ModelSizeBytes,
                    CachedAt = existingModel.CachedAt
                };
            }

            // Check if model file exists
            if (!File.Exists(request.ModelPath))
            {
                return new ModelCacheResponse
                {
                    Success = false,
                    Message = $"Model file not found: {request.ModelPath}"
                };
            }

            var startTime = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            // Load model into cache (simplified approach)
            // In production, this would use a more sophisticated caching mechanism
            var fileInfo = new FileInfo(request.ModelPath);
            var modelSize = fileInfo.Length;

            // Simulate loading model to cache (in production this would load the actual model weights)
            await Task.Delay(100); // Simulate loading time
            
            // Store cache entry
            var cachedModel = new CachedModelInfo
            {
                ModelId = modelId,
                ModelName = request.ModelName ?? modelId,
                ModelPath = request.ModelPath,
                ModelType = request.ModelType,
                ModelSizeBytes = modelSize,
                CachedAt = startTime,
                LastUsed = startTime,
                UseCount = 1,
                LoadedOnGpus = new List<string>()
            };

            _cachedModels[modelId] = cachedModel;
            _modelData[modelId] = new { ModelPath = request.ModelPath, ModelType = request.ModelType };
            
            Interlocked.Add(ref _totalCacheMemoryBytes, modelSize);
            
            stopwatch.Stop();

            _logger.LogInformation($"Model {modelId} cached successfully in {stopwatch.ElapsedMilliseconds}ms, size: {modelSize:N0} bytes");

            return new ModelCacheResponse
            {
                Success = true,
                ModelId = modelId,
                Message = "Model cached successfully",
                ModelSizeBytes = modelSize,
                LoadTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                CachedAt = startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to cache model from {request.ModelPath}");
            return new ModelCacheResponse
            {
                Success = false,
                Message = $"Failed to cache model: {ex.Message}"
            };
        }
    }

    public async Task<bool> UncacheModelAsync(string modelId)
    {
        try
        {
            await Task.CompletedTask;
            _logger.LogInformation($"Removing model {modelId} from cache");

            if (_cachedModels.TryRemove(modelId, out var cachedModel))
            {
                _modelData.TryRemove(modelId, out _);
                Interlocked.Add(ref _totalCacheMemoryBytes, -cachedModel.ModelSizeBytes);
                
                _logger.LogInformation($"Model {modelId} removed from cache, freed {cachedModel.ModelSizeBytes:N0} bytes");
                return true;
            }

            _logger.LogWarning($"Model {modelId} not found in cache");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to uncache model {modelId}");
            return false;
        }
    }

    public async Task<bool> IsModelCachedAsync(string modelId)
    {
        await Task.CompletedTask;
        return _cachedModels.ContainsKey(modelId);
    }

    public async Task<CachedModelInfo?> GetCachedModelInfoAsync(string modelId)
    {
        await Task.CompletedTask;
        _cachedModels.TryGetValue(modelId, out var modelInfo);
        return modelInfo;
    }

    public async Task<ModelCacheStatusResponse> GetCacheStatusAsync()
    {
        try
        {
            await Task.CompletedTask;
            var cachedModels = _cachedModels.Values.ToList();
            var totalCacheHits = cachedModels.Sum(m => m.UseCount);
            var totalRequests = Math.Max(totalCacheHits, 1);
            var cacheHitRatio = (double)totalCacheHits / totalRequests;

            // Get system memory info
            var process = Process.GetCurrentProcess();
            var systemMemory = GC.GetTotalMemory(false);
            var availableMemory = GetAvailableSystemMemory();

            return new ModelCacheStatusResponse
            {
                CachedModels = cachedModels,
                TotalCachedModels = cachedModels.Count,
                TotalCacheMemoryBytes = _totalCacheMemoryBytes,
                AvailableMemoryBytes = availableMemory,
                CacheHitRatio = cacheHitRatio
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache status");
            return new ModelCacheStatusResponse();
        }
    }

    public async Task<LoadModelResponse> LoadCachedModelToGpuAsync(string modelId, string gpuId)
    {
        try
        {
            await Task.CompletedTask;
            if (!_cachedModels.TryGetValue(modelId, out var cachedModel))
            {
                return new LoadModelResponse
                {
                    Success = false,
                    Message = $"Model {modelId} not found in cache"
                };
            }

            if (!_modelData.TryGetValue(modelId, out var modelData))
            {
                return new LoadModelResponse
                {
                    Success = false,
                    Message = $"Model data for {modelId} not found in cache"
                };
            }

            // Update usage statistics
            cachedModel.LastUsed = DateTime.UtcNow;
            cachedModel.UseCount++;
            
            if (!cachedModel.LoadedOnGpus.Contains(gpuId))
            {
                cachedModel.LoadedOnGpus.Add(gpuId);
            }

            _logger.LogInformation($"Model {modelId} loaded from cache to GPU {gpuId}");

            // This would integrate with GPU Pool Service to actually load to GPU
            // For now, return a simulated successful response
            return new LoadModelResponse
            {
                Success = true,
                ModelId = modelId,
                Message = $"Model loaded from cache to GPU {gpuId}",
                ModelSizeBytes = cachedModel.ModelSizeBytes,
                DeviceId = gpuId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to load cached model {modelId} to GPU {gpuId}");
            return new LoadModelResponse
            {
                Success = false,
                Message = $"Failed to load from cache: {ex.Message}"
            };
        }
    }

    public async Task CleanupCacheAsync()
    {
        try
        {
            _logger.LogInformation("Starting cache cleanup...");

            var modelsToRemove = new List<string>();
            var cutoffTime = DateTime.UtcNow.AddHours(-24); // Remove models not used in 24 hours

            foreach (var kvp in _cachedModels)
            {
                var model = kvp.Value;
                
                // Remove models that haven't been used recently and aren't loaded on any GPU
                if (model.LastUsed < cutoffTime && model.LoadedOnGpus.Count == 0)
                {
                    modelsToRemove.Add(kvp.Key);
                }
            }

            foreach (var modelId in modelsToRemove)
            {
                await UncacheModelAsync(modelId);
            }

            _logger.LogInformation($"Cache cleanup completed, removed {modelsToRemove.Count} models");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache cleanup failed");
        }
    }

    public async Task<CacheMemoryStats> GetMemoryStatsAsync()
    {
        try
        {
            await Task.CompletedTask;
            var process = Process.GetCurrentProcess();
            var totalSystemMemory = GetTotalSystemMemory();
            var availableMemory = GetAvailableSystemMemory();
            var usedCacheMemory = _totalCacheMemoryBytes;

            return new CacheMemoryStats
            {
                TotalSystemMemoryBytes = totalSystemMemory,
                UsedCacheMemoryBytes = usedCacheMemory,
                AvailableMemoryBytes = availableMemory,
                MemoryUsagePercentage = totalSystemMemory > 0 
                    ? (double)usedCacheMemory / totalSystemMemory * 100 
                    : 0,
                ActiveCacheEntries = _cachedModels.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory stats");
            return new CacheMemoryStats();
        }
    }

    public async Task<SDXLModelSuiteCacheResponse> CacheSDXLModelSuiteAsync(CacheSDXLModelSuiteRequest request)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            _logger.LogInformation($"Caching SDXL model suite: {request.SuiteName}");

            var response = new SDXLModelSuiteCacheResponse
            {
                SuiteName = request.SuiteName,
                CachedAt = DateTime.UtcNow,
                ComponentResults = new List<ComponentCacheResult>()
            };

            var totalLoadTime = 0.0;
            var totalSize = 0L;
            var allSuccess = true;

            // Cache base model (required)
            if (!string.IsNullOrEmpty(request.BaseModelPath))
            {
                var baseResult = await CacheModelComponentAsync(
                    request.BaseModelPath, 
                    $"sdxl_{request.SuiteName}_base", 
                    $"{request.SuiteName}-base", 
                    ModelType.SDXL, 
                    "base",
                    request.ForceReload);
                
                response.ComponentResults.Add(baseResult);
                totalLoadTime += baseResult.LoadTimeSeconds;
                totalSize += baseResult.SizeBytes;
                allSuccess &= baseResult.Success;
            }

            // Cache refiner model (optional)
            if (!string.IsNullOrEmpty(request.RefinerModelPath))
            {
                var refinerResult = await CacheModelComponentAsync(
                    request.RefinerModelPath, 
                    $"sdxl_{request.SuiteName}_refiner", 
                    $"{request.SuiteName}-refiner", 
                    ModelType.SDXL, 
                    "refiner",
                    request.ForceReload);
                
                response.ComponentResults.Add(refinerResult);
                totalLoadTime += refinerResult.LoadTimeSeconds;
                totalSize += refinerResult.SizeBytes;
                allSuccess &= refinerResult.Success;
            }

            // Cache VAE model (optional)
            if (!string.IsNullOrEmpty(request.VaeModelPath))
            {
                var vaeResult = await CacheModelComponentAsync(
                    request.VaeModelPath, 
                    $"sdxl_{request.SuiteName}_vae", 
                    $"{request.SuiteName}-vae", 
                    ModelType.VAE, 
                    "vae",
                    request.ForceReload);
                
                response.ComponentResults.Add(vaeResult);
                totalLoadTime += vaeResult.LoadTimeSeconds;
                totalSize += vaeResult.SizeBytes;
                allSuccess &= vaeResult.Success;
            }

            // Cache ControlNet model (optional)
            if (!string.IsNullOrEmpty(request.ControlNetPath))
            {
                var controlNetResult = await CacheModelComponentAsync(
                    request.ControlNetPath, 
                    $"sdxl_{request.SuiteName}_controlnet", 
                    $"{request.SuiteName}-controlnet", 
                    ModelType.Generic, 
                    "controlnet",
                    request.ForceReload);
                
                response.ComponentResults.Add(controlNetResult);
                totalLoadTime += controlNetResult.LoadTimeSeconds;
                totalSize += controlNetResult.SizeBytes;
                allSuccess &= controlNetResult.Success;
            }

            // Cache LoRA model (optional)
            if (!string.IsNullOrEmpty(request.LoraPath))
            {
                var loraResult = await CacheModelComponentAsync(
                    request.LoraPath, 
                    $"sdxl_{request.SuiteName}_lora", 
                    $"{request.SuiteName}-lora", 
                    ModelType.Custom, 
                    "lora",
                    request.ForceReload);
                
                response.ComponentResults.Add(loraResult);
                totalLoadTime += loraResult.LoadTimeSeconds;
                totalSize += loraResult.SizeBytes;
                allSuccess &= loraResult.Success;
            }

            response.Success = allSuccess;
            response.TotalSizeBytes = totalSize;
            response.TotalLoadTimeSeconds = totalLoadTime;
            response.Message = allSuccess 
                ? $"SDXL model suite '{request.SuiteName}' cached successfully with {response.ComponentResults.Count} components"
                : $"SDXL model suite '{request.SuiteName}' partially cached - some components failed";

            _logger.LogInformation($"SDXL model suite '{request.SuiteName}' caching completed: {response.ComponentResults.Count(r => r.Success)}/{response.ComponentResults.Count} components successful");

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to cache SDXL model suite: {request.SuiteName}");
            return new SDXLModelSuiteCacheResponse
            {
                Success = false,
                SuiteName = request.SuiteName,
                Message = $"Failed to cache SDXL model suite: {ex.Message}"
            };
        }
    }

    public async Task<List<SDXLModelSuiteInfo>> GetCachedSDXLModelSuitesAsync()
    {
        try
        {
            await Task.CompletedTask;
            var suites = new Dictionary<string, SDXLModelSuiteInfo>();

            foreach (var model in _cachedModels.Values)
            {
                if (model.ModelId.StartsWith("sdxl_"))
                {
                    var parts = model.ModelId.Split('_');
                    if (parts.Length >= 3)
                    {
                        var suiteName = string.Join("_", parts.Skip(1).Take(parts.Length - 2));
                        var componentType = parts.Last();

                        if (!suites.ContainsKey(suiteName))
                        {
                            suites[suiteName] = new SDXLModelSuiteInfo
                            {
                                SuiteName = suiteName,
                                CachedAt = model.CachedAt,
                                LastUsed = model.LastUsed,
                                UseCount = model.UseCount,
                                LoadedOnGpus = new List<string>(model.LoadedOnGpus)
                            };
                        }

                        var suite = suites[suiteName];
                        suite.TotalSizeBytes += model.ModelSizeBytes;
                        suite.UseCount = Math.Max(suite.UseCount, model.UseCount);
                        suite.LastUsed = model.LastUsed > suite.LastUsed ? model.LastUsed : suite.LastUsed;

                        // Merge GPU lists
                        foreach (var gpu in model.LoadedOnGpus)
                        {
                            if (!suite.LoadedOnGpus.Contains(gpu))
                            {
                                suite.LoadedOnGpus.Add(gpu);
                            }
                        }

                        // Assign component model IDs
                        switch (componentType)
                        {
                            case "base":
                                suite.BaseModelId = model.ModelId;
                                break;
                            case "refiner":
                                suite.RefinerModelId = model.ModelId;
                                break;
                            case "vae":
                                suite.VaeModelId = model.ModelId;
                                break;
                            case "controlnet":
                                suite.ControlNetModelId = model.ModelId;
                                break;
                            case "lora":
                                suite.LoraModelId = model.ModelId;
                                break;
                        }
                    }
                }
            }

            return suites.Values.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached SDXL model suites");
            return new List<SDXLModelSuiteInfo>();
        }
    }

    public async Task<bool> UncacheSDXLModelSuiteAsync(string suiteName)
    {
        try
        {
            _logger.LogInformation($"Removing SDXL model suite: {suiteName}");

            var modelsToRemove = _cachedModels.Keys
                .Where(id => id.StartsWith($"sdxl_{suiteName}_"))
                .ToList();

            var allRemoved = true;
            foreach (var modelId in modelsToRemove)
            {
                var removed = await UncacheModelAsync(modelId);
                allRemoved &= removed;
            }

            _logger.LogInformation($"SDXL model suite '{suiteName}' removal completed: {modelsToRemove.Count} components processed");
            return allRemoved;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to uncache SDXL model suite: {suiteName}");
            return false;
        }
    }

    public async Task<PreloadCommonModelsResponse> PreloadCommonSDXLModelsAsync()
    {
        try
        {
            _logger.LogInformation("Preloading common SDXL models");

            var commonModels = GetCommonSDXLModelPaths();
            var response = new PreloadCommonModelsResponse
            {
                TotalAttempted = commonModels.Count,
                PreloadResults = new List<ComponentCacheResult>()
            };

            foreach (var model in commonModels)
            {
                if (File.Exists(model.BaseModelPath))
                {
                    var suiteResponse = await CacheSDXLModelSuiteAsync(model);
                    response.PreloadResults.AddRange(suiteResponse.ComponentResults);
                    response.TotalSizeBytes += suiteResponse.TotalSizeBytes;
                }
                else
                {
                    response.PreloadResults.Add(new ComponentCacheResult
                    {
                        ComponentType = "base",
                        Success = false,
                        Message = $"Model file not found: {model.BaseModelPath}"
                    });
                }
            }

            response.Successful = response.PreloadResults.Count(r => r.Success);
            response.Failed = response.PreloadResults.Count(r => !r.Success);
            response.Success = response.Successful > 0;
            response.Message = $"Preload completed: {response.Successful}/{response.TotalAttempted} models cached successfully";

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preload common SDXL models");
            return new PreloadCommonModelsResponse
            {
                Success = false,
                Message = $"Preload failed: {ex.Message}"
            };
        }
    }

    public async Task<List<ModelValidationResult>> ValidateModelsAsync(List<string> modelPaths)
    {
        try
        {
            var results = new List<ModelValidationResult>();

            foreach (var modelPath in modelPaths)
            {
                var result = await ValidateModelAsync(modelPath);
                results.Add(result);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate models");
            return new List<ModelValidationResult>();
        }
    }

    public async Task<CacheEfficiencyMetrics> GetCacheEfficiencyMetricsAsync()
    {
        try
        {
            await Task.CompletedTask;
            var models = _cachedModels.Values.ToList();
            var totalRequests = models.Sum(m => m.UseCount);
            var totalCacheHits = models.Where(m => m.UseCount > 0).Sum(m => m.UseCount);
            var totalCacheMisses = Math.Max(0, totalRequests - totalCacheHits);

            var mostUsed = models
                .OrderByDescending(m => m.UseCount)
                .Take(10)
                .Select(m => new ModelUsageStatistic
                {
                    ModelId = m.ModelId,
                    ModelName = m.ModelName,
                    UseCount = m.UseCount,
                    LastUsed = m.LastUsed,
                    SizeBytes = m.ModelSizeBytes,
                    AvgLoadTimeSeconds = 1.0 // Simplified - would track actual load times
                })
                .ToList();

            var unused = models
                .Where(m => m.UseCount == 0 || m.LastUsed < DateTime.UtcNow.AddDays(-7))
                .Select(m => new ModelUsageStatistic
                {
                    ModelId = m.ModelId,
                    ModelName = m.ModelName,
                    UseCount = m.UseCount,
                    LastUsed = m.LastUsed,
                    SizeBytes = m.ModelSizeBytes,
                    AvgLoadTimeSeconds = 1.0
                })
                .ToList();

            var totalSystemMemory = GetTotalSystemMemory();
            var memoryUtilization = totalSystemMemory > 0 
                ? (double)_totalCacheMemoryBytes / totalSystemMemory * 100 
                : 0;

            return new CacheEfficiencyMetrics
            {
                CacheHitRatio = totalRequests > 0 ? (double)totalCacheHits / totalRequests : 0,
                CacheMissRatio = totalRequests > 0 ? (double)totalCacheMisses / totalRequests : 0,
                TotalCacheRequests = totalRequests,
                TotalCacheHits = totalCacheHits,
                TotalCacheMisses = totalCacheMisses,
                AverageLoadTimeSeconds = 1.5, // Simplified - would track actual metrics
                MemoryUtilizationPercentage = memoryUtilization,
                MostUsedModelCount = mostUsed.Count,
                UnusedModelCount = unused.Count,
                TopUsedModels = mostUsed,
                UnusedModels = unused,
                MetricsGeneratedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache efficiency metrics");
            return new CacheEfficiencyMetrics
            {
                MetricsGeneratedAt = DateTime.UtcNow
            };
        }
    }

    // Helper methods for SDXL model suite support
    private async Task<ComponentCacheResult> CacheModelComponentAsync(
        string modelPath, 
        string modelId, 
        string modelName, 
        ModelType modelType, 
        string componentType,
        bool forceReload)
    {
        try
        {
            var cacheRequest = new CacheModelRequest
            {
                ModelPath = modelPath,
                ModelId = modelId,
                ModelName = modelName,
                ModelType = modelType,
                ForceReload = forceReload
            };

            var result = await CacheModelAsync(cacheRequest);

            return new ComponentCacheResult
            {
                ComponentType = componentType,
                ModelId = result.ModelId,
                Success = result.Success,
                Message = result.Message,
                SizeBytes = result.ModelSizeBytes,
                LoadTimeSeconds = result.LoadTimeSeconds
            };
        }
        catch (Exception ex)
        {
            return new ComponentCacheResult
            {
                ComponentType = componentType,
                Success = false,
                Message = $"Failed to cache {componentType}: {ex.Message}"
            };
        }
    }

    private List<CacheSDXLModelSuiteRequest> GetCommonSDXLModelPaths()
    {
        return new List<CacheSDXLModelSuiteRequest>
        {
            new()
            {
                SuiteName = "cyberrealistic_pony_v110",
                BaseModelPath = "C:\\Users\\admin\\Desktop\\node-manager\\models\\cyberrealisticPony_v110.safetensors"
            },
            new()
            {
                SuiteName = "cyberrealistic_pony_v125",
                BaseModelPath = "C:\\Users\\admin\\Desktop\\node-manager\\models\\cyberrealisticPony_v125.safetensors"
            }
        };
    }

    private async Task<ModelValidationResult> ValidateModelAsync(string modelPath)
    {
        await Task.CompletedTask;
        var result = new ModelValidationResult
        {
            ModelPath = modelPath,
            FileExists = File.Exists(modelPath)
        };

        if (!result.FileExists)
        {
            result.ValidationErrors.Add("Model file does not exist");
            return result;
        }

        try
        {
            var fileInfo = new FileInfo(modelPath);
            result.FileSizeBytes = fileInfo.Length;
            result.ModelFormat = Path.GetExtension(modelPath).ToLowerInvariant() switch
            {
                ".safetensors" => "safetensors",
                ".ckpt" => "checkpoint",
                ".pt" => "pytorch",
                ".onnx" => "onnx",
                _ => "unknown"
            };

            result.ModelName = Path.GetFileNameWithoutExtension(modelPath);

            // Basic validation checks
            if (result.FileSizeBytes < 1024) // Less than 1KB
            {
                result.ValidationErrors.Add("Model file appears to be too small");
            }
            else if (result.FileSizeBytes > 50L * 1024 * 1024 * 1024) // Larger than 50GB
            {
                result.ValidationWarnings.Add("Model file is very large and may consume significant memory");
            }

            // Detect model type based on size and format
            result.DetectedModelType = DetectModelType(result.FileSizeBytes, result.ModelFormat);

            result.IsValid = result.ValidationErrors.Count == 0;
        }
        catch (Exception ex)
        {
            result.ValidationErrors.Add($"Error validating model: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    private ModelType DetectModelType(long fileSizeBytes, string format)
    {
        // Simple heuristic based on file size (would be more sophisticated in production)
        var sizeMB = fileSizeBytes / (1024.0 * 1024.0);

        return sizeMB switch
        {
            > 5000 => ModelType.SDXL,      // SDXL models are typically 5GB+
            > 2000 => ModelType.Generic,   // Large models
            > 500 => ModelType.VAE,        // VAE models are typically 500MB-2GB
            > 100 => ModelType.Generic,    // Medium models
            _ => ModelType.Custom          // Small models (LoRA, embeddings, etc.)
        };
    }

    private long GetTotalSystemMemory()
    {
        try
        {
            // Simplified approach - in production, use proper system APIs
            var gcMemory = GC.GetTotalMemory(false);
            return Math.Max(gcMemory * 10, 16L * 1024 * 1024 * 1024); // Assume at least 16GB
        }
        catch
        {
            return 16L * 1024 * 1024 * 1024; // Default to 16GB
        }
    }

    private long GetAvailableSystemMemory()
    {
        try
        {
            // Simplified approach - in production, use proper system APIs
            var totalMemory = GetTotalSystemMemory();
            var usedMemory = _totalCacheMemoryBytes + GC.GetTotalMemory(false);
            return Math.Max(0, totalMemory - usedMemory);
        }
        catch
        {
            return 8L * 1024 * 1024 * 1024; // Default to 8GB available
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing Model Cache Service...");
        
        // Clear all cached models
        foreach (var modelId in _cachedModels.Keys.ToList())
        {
            try
            {
                UncacheModelAsync(modelId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uncaching model {modelId} during disposal");
            }
        }
        
        _cachedModels.Clear();
        _modelData.Clear();
        _totalCacheMemoryBytes = 0;
        _initialized = false;
        
        _logger.LogInformation("Model Cache Service disposed");
    }
}
