using DeviceOperations.Services.Inference;
using DeviceOperations.Models.Requests;
using Microsoft.AspNetCore.Mvc;

namespace DeviceOperations.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelCacheController : ControllerBase
{
    private readonly IModelCacheService _modelCacheService;
    private readonly ILogger<ModelCacheController> _logger;

    public ModelCacheController(
        IModelCacheService modelCacheService,
        ILogger<ModelCacheController> logger)
    {
        _modelCacheService = modelCacheService;
        _logger = logger;
    }

    /// <summary>
    /// Load a model into shared RAM cache
    /// </summary>
    /// <param name="request">Model cache request</param>
    [HttpPost("load")]
    public async Task<IActionResult> CacheModel([FromBody] CacheModelRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ModelPath))
            {
                return BadRequest(new { error = "ModelPath is required" });
            }

            _logger.LogInformation($"Caching model: {request.ModelPath}");

            var result = await _modelCacheService.CacheModelAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to cache model: {request.ModelPath}");
            return StatusCode(500, new { error = "Model caching failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Remove a model from shared RAM cache
    /// </summary>
    /// <param name="modelId">Model ID to remove</param>
    [HttpDelete("{modelId}")]
    public async Task<IActionResult> UncacheModel(string modelId)
    {
        try
        {
            _logger.LogInformation($"Removing model from cache: {modelId}");

            var success = await _modelCacheService.UncacheModelAsync(modelId);
            
            if (success)
            {
                return Ok(new { message = $"Model {modelId} removed from cache successfully" });
            }
            else
            {
                return NotFound(new { error = $"Model {modelId} not found in cache" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to uncache model: {modelId}");
            return StatusCode(500, new { error = "Model uncaching failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the status of the model cache
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetCacheStatus()
    {
        try
        {
            var status = await _modelCacheService.GetCacheStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache status");
            return StatusCode(500, new { error = "Failed to get cache status", details = ex.Message });
        }
    }

    /// <summary>
    /// Get information about a specific cached model
    /// </summary>
    /// <param name="modelId">Model ID to query</param>
    [HttpGet("{modelId}")]
    public async Task<IActionResult> GetCachedModelInfo(string modelId)
    {
        try
        {
            var modelInfo = await _modelCacheService.GetCachedModelInfoAsync(modelId);
            
            if (modelInfo == null)
            {
                return NotFound(new { error = $"Model {modelId} not found in cache" });
            }

            return Ok(modelInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get cached model info: {modelId}");
            return StatusCode(500, new { error = "Failed to get model info", details = ex.Message });
        }
    }

    /// <summary>
    /// Check if a model is cached
    /// </summary>
    /// <param name="modelId">Model ID to check</param>
    [HttpGet("{modelId}/exists")]
    public async Task<IActionResult> IsModelCached(string modelId)
    {
        try
        {
            var isCached = await _modelCacheService.IsModelCachedAsync(modelId);
            
            return Ok(new 
            { 
                modelId,
                cached = isCached,
                message = isCached ? "Model is cached" : "Model is not cached"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to check if model is cached: {modelId}");
            return StatusCode(500, new { error = "Failed to check cache status", details = ex.Message });
        }
    }

    /// <summary>
    /// Get memory usage statistics for the cache
    /// </summary>
    [HttpGet("memory-stats")]
    public async Task<IActionResult> GetMemoryStats()
    {
        try
        {
            var stats = await _modelCacheService.GetMemoryStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache memory stats");
            return StatusCode(500, new { error = "Failed to get memory stats", details = ex.Message });
        }
    }

    /// <summary>
    /// Clean up unused cache entries
    /// </summary>
    [HttpPost("cleanup")]
    public async Task<IActionResult> CleanupCache()
    {
        try
        {
            _logger.LogInformation("Starting cache cleanup");

            await _modelCacheService.CleanupCacheAsync();
            
            return Ok(new { message = "Cache cleanup completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache cleanup failed");
            return StatusCode(500, new { error = "Cache cleanup failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Batch cache multiple models
    /// </summary>
    /// <param name="requests">List of models to cache</param>
    [HttpPost("batch-load")]
    public async Task<IActionResult> BatchCacheModels([FromBody] List<CacheModelRequest> requests)
    {
        try
        {
            _logger.LogInformation($"Batch caching {requests.Count} models");

            var results = new List<object>();
            var tasks = new List<Task>();

            foreach (var request in requests)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var result = await _modelCacheService.CacheModelAsync(request);
                    
                    lock (results)
                    {
                        results.Add(new
                        {
                            modelPath = request.ModelPath,
                            modelId = result.ModelId,
                            success = result.Success,
                            message = result.Message,
                            sizeBytes = result.ModelSizeBytes
                        });
                    }
                }));
            }

            await Task.WhenAll(tasks);

            var successCount = results.Count(r => ((dynamic)r).success);
            
            return Ok(new
            {
                message = $"Batch caching completed: {successCount}/{requests.Count} successful",
                results,
                totalRequested = requests.Count,
                successful = successCount,
                failed = requests.Count - successCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch model caching failed");
            return StatusCode(500, new { error = "Batch caching failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Preload common models to cache for faster GPU deployment
    /// </summary>
    [HttpPost("preload-common")]
    public async Task<IActionResult> PreloadCommonModels()
    {
        try
        {
            _logger.LogInformation("Preloading common models to cache");

            // Define common models to preload (this could come from configuration)
            var commonModels = new List<CacheModelRequest>
            {
                new()
                {
                    ModelPath = "C:\\Users\\admin\\Desktop\\device-manager\\models\\cyberrealistic_pony_v110.safetensors",
                    ModelType = ModelType.SDXL,
                    ModelName = "CyberRealistic Pony v1.1.0"
                }
                // Add more common models as needed
            };

            var results = new List<object>();
            
            foreach (var model in commonModels)
            {
                // Check if model file exists before caching
                if (System.IO.File.Exists(model.ModelPath))
                {
                    var result = await _modelCacheService.CacheModelAsync(model);
                    results.Add(new
                    {
                        modelPath = model.ModelPath,
                        modelId = result.ModelId,
                        success = result.Success,
                        message = result.Message
                    });
                }
                else
                {
                    results.Add(new
                    {
                        modelPath = model.ModelPath,
                        success = false,
                        message = "Model file not found"
                    });
                }
            }

            var successCount = results.Count(r => ((dynamic)r).success);
            
            return Ok(new
            {
                message = $"Preload completed: {successCount}/{commonModels.Count} models cached",
                results,
                totalAttempted = commonModels.Count,
                successful = successCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preload common models");
            return StatusCode(500, new { error = "Preload failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Cache an enhanced SDXL model with all components
    /// </summary>
    /// <param name="request">Enhanced SDXL cache request</param>
    [HttpPost("cache-sdxl")]
    public async Task<IActionResult> CacheSDXLModel([FromBody] CacheSDXLModelRequest request)
    {
        try
        {
            _logger.LogInformation($"Caching SDXL model components: base={request.BaseModel}, refiner={request.RefinerModel}, vae={request.VaeModel}");

            var results = new List<object>();
            
            // Cache base model
            if (!string.IsNullOrEmpty(request.BaseModel))
            {
                var baseResult = await _modelCacheService.CacheModelAsync(new CacheModelRequest
                {
                    ModelPath = request.BaseModel,
                    ModelType = ModelType.SDXL,
                    ModelName = $"{request.ModelName}-base",
                    ModelId = $"sdxl_{request.ModelName}_base"
                });
                results.Add(new { component = "base", success = baseResult.Success, modelId = baseResult.ModelId });
            }

            // Cache refiner if provided
            if (!string.IsNullOrEmpty(request.RefinerModel))
            {
                var refinerResult = await _modelCacheService.CacheModelAsync(new CacheModelRequest
                {
                    ModelPath = request.RefinerModel,
                    ModelType = ModelType.SDXL,
                    ModelName = $"{request.ModelName}-refiner",
                    ModelId = $"sdxl_{request.ModelName}_refiner"
                });
                results.Add(new { component = "refiner", success = refinerResult.Success, modelId = refinerResult.ModelId });
            }

            // Cache VAE if provided
            if (!string.IsNullOrEmpty(request.VaeModel))
            {
                var vaeResult = await _modelCacheService.CacheModelAsync(new CacheModelRequest
                {
                    ModelPath = request.VaeModel,
                    ModelType = ModelType.VAE,
                    ModelName = $"{request.ModelName}-vae",
                    ModelId = $"sdxl_{request.ModelName}_vae"
                });
                results.Add(new { component = "vae", success = vaeResult.Success, modelId = vaeResult.ModelId });
            }

            var allSuccess = results.All(r => ((dynamic)r).success);
            
            return Ok(new
            {
                success = allSuccess,
                message = allSuccess ? "All SDXL components cached successfully" : "Some components failed to cache",
                modelName = request.ModelName,
                components = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache SDXL model components");
            return StatusCode(500, new { error = "SDXL model caching failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get cached SDXL models with component status
    /// </summary>
    [HttpGet("sdxl-models")]
    public async Task<IActionResult> GetCachedSDXLModels()
    {
        try
        {
            var status = await _modelCacheService.GetCacheStatusAsync();
            
            // Group SDXL models by base name (extract from ModelId pattern)
            var sdxlModels = status.CachedModels
                .Where(m => m.ModelId.StartsWith("sdxl_") || m.ModelType == ModelType.SDXL)
                .GroupBy(m => ExtractSDXLModelName(m.ModelId, m.ModelName))
                .Select(g => new
                {
                    modelName = g.Key,
                    components = g.Select(m => new
                    {
                        type = ExtractComponentType(m.ModelId, m.ModelName),
                        modelId = m.ModelId,
                        sizeBytes = m.ModelSizeBytes,
                        cachedAt = m.CachedAt
                    }).ToList()
                })
                .ToList();

            return Ok(new
            {
                count = sdxlModels.Count,
                models = sdxlModels,
                totalSizeMB = status.CachedModels
                    .Where(m => m.ModelId.StartsWith("sdxl_") || m.ModelType == ModelType.SDXL)
                    .Sum(m => m.ModelSizeBytes) / (1024.0 * 1024.0)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cached SDXL models");
            return StatusCode(500, new { error = "Failed to retrieve SDXL models", details = ex.Message });
        }
    }

    // Helper methods for SDXL model grouping
    private static string ExtractSDXLModelName(string modelId, string modelName)
    {
        if (modelId.StartsWith("sdxl_"))
        {
            var parts = modelId.Split('_');
            if (parts.Length >= 3)
            {
                return string.Join("_", parts.Skip(1).Take(parts.Length - 2));
            }
        }
        
        // Fallback: extract from model name
        if (modelName.EndsWith("-base") || modelName.EndsWith("-refiner") || modelName.EndsWith("-vae"))
        {
            return modelName.Substring(0, modelName.LastIndexOf('-'));
        }
        
        return modelName;
    }

    private static string ExtractComponentType(string modelId, string modelName)
    {
        if (modelId.Contains("_base")) return "base";
        if (modelId.Contains("_refiner")) return "refiner";
        if (modelId.Contains("_vae")) return "vae";
        
        // Fallback: check model name
        if (modelName.EndsWith("-base")) return "base";
        if (modelName.EndsWith("-refiner")) return "refiner";
        if (modelName.EndsWith("-vae")) return "vae";
        
        return "unknown";
    }
}

public class CacheSDXLModelRequest
{
    public string ModelName { get; set; } = string.Empty;
    public string BaseModel { get; set; } = string.Empty;
    public string? RefinerModel { get; set; }
    public string? VaeModel { get; set; }
}
