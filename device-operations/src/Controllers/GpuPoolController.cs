using DeviceOperations.Models.Requests;
using DeviceOperations.Services.Inference;
using Microsoft.AspNetCore.Mvc;

namespace DeviceOperations.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GpuPoolController : ControllerBase
{
    private readonly IGpuPoolService _gpuPoolService;
    private readonly IModelCacheService _modelCacheService;
    private readonly ILogger<GpuPoolController> _logger;

    public GpuPoolController(
        IGpuPoolService gpuPoolService,
        IModelCacheService modelCacheService,
        ILogger<GpuPoolController> logger)
    {
        _gpuPoolService = gpuPoolService;
        _modelCacheService = modelCacheService;
        _logger = logger;
    }

    /// <summary>
    /// Get the status of all GPUs in the pool
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetPoolStatus()
    {
        try
        {
            var status = await _gpuPoolService.GetPoolStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get GPU pool status");
            return StatusCode(500, new { error = "Failed to get pool status", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the status of a specific GPU
    /// </summary>
    /// <param name="gpuId">GPU ID (e.g., "gpu_0")</param>
    [HttpGet("gpu/{gpuId}/status")]
    public async Task<IActionResult> GetGpuStatus(string gpuId)
    {
        try
        {
            var status = await _gpuPoolService.GetGpuStatusAsync(gpuId);
            
            if (status == null)
            {
                return NotFound(new { error = $"GPU {gpuId} not found" });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get status for GPU {gpuId}");
            return StatusCode(500, new { error = "Failed to get GPU status", details = ex.Message });
        }
    }

    /// <summary>
    /// Load a model to a specific GPU's VRAM
    /// </summary>
    /// <param name="gpuId">Target GPU ID</param>
    /// <param name="request">Model loading request</param>
    [HttpPost("gpu/{gpuId}/load-model")]
    public async Task<IActionResult> LoadModelToGpu(string gpuId, [FromBody] LoadModelRequest request)
    {
        try
        {
            _logger.LogInformation($"Loading model to GPU {gpuId}: {request.ModelPath}");

            var result = await _gpuPoolService.LoadModelToGpuAsync(gpuId, request);
            
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
            _logger.LogError(ex, $"Failed to load model to GPU {gpuId}");
            return StatusCode(500, new { error = "Model loading failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Load a cached model to a specific GPU's VRAM
    /// </summary>
    /// <param name="gpuId">Target GPU ID</param>
    /// <param name="modelId">Cached model ID</param>
    [HttpPost("gpu/{gpuId}/load-cached-model/{modelId}")]
    public async Task<IActionResult> LoadCachedModelToGpu(string gpuId, string modelId)
    {
        try
        {
            _logger.LogInformation($"Loading cached model {modelId} to GPU {gpuId}");

            var result = await _modelCacheService.LoadCachedModelToGpuAsync(modelId, gpuId);
            
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
            _logger.LogError(ex, $"Failed to load cached model {modelId} to GPU {gpuId}");
            return StatusCode(500, new { error = "Cached model loading failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Unload model from a specific GPU's VRAM
    /// </summary>
    /// <param name="gpuId">Target GPU ID</param>
    /// <param name="modelId">Optional model ID to unload (if not specified, unloads current model)</param>
    [HttpDelete("gpu/{gpuId}/unload-model")]
    public async Task<IActionResult> UnloadModelFromGpu(string gpuId, [FromQuery] string? modelId = null)
    {
        try
        {
            _logger.LogInformation($"Unloading model from GPU {gpuId}");

            var success = await _gpuPoolService.UnloadModelFromGpuAsync(gpuId, modelId);
            
            if (success)
            {
                return Ok(new { message = $"Model unloaded successfully from GPU {gpuId}" });
            }
            else
            {
                return BadRequest(new { error = $"Failed to unload model from GPU {gpuId}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to unload model from GPU {gpuId}");
            return StatusCode(500, new { error = "Model unloading failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Run inference on a specific GPU
    /// </summary>
    /// <param name="gpuId">Target GPU ID</param>
    /// <param name="request">Inference request</param>
    [HttpPost("gpu/{gpuId}/inference")]
    public async Task<IActionResult> RunInferenceOnGpu(string gpuId, [FromBody] InferenceRequest request)
    {
        try
        {
            _logger.LogInformation($"Running inference on GPU {gpuId}");

            var result = await _gpuPoolService.RunInferenceOnGpuAsync(gpuId, request);
            
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
            _logger.LogError(ex, $"Failed to run inference on GPU {gpuId}");
            return StatusCode(500, new { error = "Inference failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Find the best available GPU for a model type
    /// </summary>
    /// <param name="modelType">Type of model to find GPU for</param>
    [HttpGet("find-best-gpu")]
    public async Task<IActionResult> FindBestGpu([FromQuery] string modelType = "SDXL")
    {
        try
        {
            if (!Enum.TryParse<ModelType>(modelType, true, out var parsedModelType))
            {
                return BadRequest(new { error = $"Invalid model type: {modelType}" });
            }

            var bestGpu = await _gpuPoolService.FindBestAvailableGpuAsync(parsedModelType);
            
            if (bestGpu == null)
            {
                return NotFound(new { error = "No available GPU found" });
            }

            return Ok(new { gpuId = bestGpu, message = $"Best available GPU for {modelType}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find best available GPU");
            return StatusCode(500, new { error = "Failed to find best GPU", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all GPUs that have a specific model loaded
    /// </summary>
    /// <param name="modelId">Model ID to search for</param>
    [HttpGet("models/{modelId}/gpus")]
    public async Task<IActionResult> GetGpusWithModel(string modelId)
    {
        try
        {
            var gpus = await _gpuPoolService.GetGpusWithModelAsync(modelId);
            
            return Ok(new 
            { 
                modelId,
                gpus = gpus.ToList(),
                count = gpus.Count()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get GPUs with model {modelId}");
            return StatusCode(500, new { error = "Failed to get GPUs with model", details = ex.Message });
        }
    }

    /// <summary>
    /// Load the same model to multiple GPUs (batch operation)
    /// </summary>
    /// <param name="request">Batch model loading request</param>
    [HttpPost("batch-load-model")]
    public async Task<IActionResult> BatchLoadModel([FromBody] BatchLoadModelRequest request)
    {
        try
        {
            _logger.LogInformation($"Batch loading model {request.ModelPath} to {request.GpuIds.Count} GPUs");

            var results = new List<object>();
            var tasks = new List<Task>();

            foreach (var gpuId in request.GpuIds)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var loadRequest = new LoadModelRequest
                    {
                        ModelPath = request.ModelPath,
                        ModelType = request.ModelType,
                        ModelId = request.ModelId,
                        ModelName = request.ModelName,
                        EnableMultiGpu = false // Single GPU per worker
                    };

                    var result = await _gpuPoolService.LoadModelToGpuAsync(gpuId, loadRequest);
                    
                    lock (results)
                    {
                        results.Add(new
                        {
                            gpuId,
                            success = result.Success,
                            modelId = result.ModelId,
                            message = result.Message
                        });
                    }
                }));
            }

            await Task.WhenAll(tasks);

            var successCount = results.Count(r => ((dynamic)r).success);
            
            return Ok(new
            {
                message = $"Batch operation completed: {successCount}/{request.GpuIds.Count} successful",
                results,
                totalRequested = request.GpuIds.Count,
                successful = successCount,
                failed = request.GpuIds.Count - successCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch model loading failed");
            return StatusCode(500, new { error = "Batch loading failed", details = ex.Message });
        }
    }
    
    /// <summary>
    /// Clean up memory on a specific GPU
    /// </summary>
    [HttpPost("gpu/{gpuId}/cleanup-memory")]
    public async Task<IActionResult> CleanupGpuMemory(string gpuId)
    {
        try
        {
            _logger.LogInformation($"Cleaning up memory on GPU {gpuId}");
            
            var result = await _gpuPoolService.CleanupGpuMemoryAsync(gpuId);
            
            if (result)
            {
                return Ok(new { message = $"Memory cleanup successful on {gpuId}", gpuId });
            }
            
            return BadRequest(new { message = $"Memory cleanup failed on {gpuId}", gpuId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Memory cleanup failed on GPU {gpuId}");
            return StatusCode(500, new { error = "Memory cleanup failed", gpuId, details = ex.Message });
        }
    }
    
    /// <summary>
    /// Clean up memory on all GPUs
    /// </summary>
    [HttpPost("cleanup-memory")]
    public async Task<IActionResult> CleanupAllGpuMemory()
    {
        try
        {
            _logger.LogInformation("Cleaning up memory on all GPUs");
            
            var status = await _gpuPoolService.GetPoolStatusAsync();
            var results = new List<object>();
            var tasks = new List<Task>();

            foreach (var gpu in status.Gpus)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var result = await _gpuPoolService.CleanupGpuMemoryAsync(gpu.GpuId);
                    
                    lock (results)
                    {
                        results.Add(new
                        {
                            gpuId = gpu.GpuId,
                            success = result,
                            message = result ? "Memory cleanup successful" : "Memory cleanup failed"
                        });
                    }
                }));
            }

            await Task.WhenAll(tasks);

            var successCount = results.Count(r => ((dynamic)r).success);
            
            return Ok(new
            {
                message = $"Memory cleanup completed: {successCount}/{status.Gpus.Count} successful",
                results,
                totalGpus = status.Gpus.Count,
                successful = successCount,
                failed = status.Gpus.Count - successCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory cleanup failed");
            return StatusCode(500, new { error = "Memory cleanup failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Run enhanced SDXL generation on the best available GPU
    /// </summary>
    /// <param name="request">Enhanced SDXL request</param>
    [HttpPost("generate-sdxl")]
    public async Task<IActionResult> GenerateSDXL([FromBody] EnhancedSDXLRequest request)
    {
        try
        {
            // Find best GPU for SDXL
            var targetGpu = await _gpuPoolService.FindBestAvailableGpuAsync(ModelType.SDXL);
            if (string.IsNullOrEmpty(targetGpu))
            {
                return StatusCode(503, new { error = "No available GPU for SDXL generation" });
            }

            _logger.LogInformation($"Running enhanced SDXL on GPU {targetGpu}");

            // Create inference request with enhanced SDXL format
            var inferenceRequest = new InferenceRequest
            {
                ModelId = request.Model.Base,
                Inputs = new Dictionary<string, object>
                {
                    ["enhanced_sdxl_request"] = request,
                    ["pipeline_type"] = "enhanced_sdxl"
                },
                InferenceOptions = new Dictionary<string, object>
                {
                    ["gpu_id"] = targetGpu,
                    ["return_detailed_metrics"] = true
                }
            };

            var result = await _gpuPoolService.RunInferenceOnGpuAsync(targetGpu, inferenceRequest);
            
            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    gpuUsed = targetGpu,
                    sessionId = result.SessionId,
                    message = "Enhanced SDXL generation started",
                    checkStatusUrl = $"/api/inference/status/{result.SessionId}"
                });
            }
            else
            {
                return BadRequest(new { error = result.Message, gpuId = targetGpu });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enhanced SDXL generation failed");
            return StatusCode(500, new { error = "SDXL generation failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get GPU capabilities for enhanced SDXL
    /// </summary>
    /// <param name="gpuId">GPU ID to check</param>
    [HttpGet("gpu/{gpuId}/sdxl-capabilities")]
    public async Task<IActionResult> GetSDXLCapabilities(string gpuId)
    {
        try
        {
            var status = await _gpuPoolService.GetGpuStatusAsync(gpuId);
            if (status == null)
            {
                return NotFound(new { error = $"GPU {gpuId} not found" });
            }

            // Check if GPU has enough memory for SDXL (using VramAvailableBytes)
            var availableMemoryMB = status.MemoryInfo.VramAvailableBytes / (1024 * 1024);
            var hasSDXLMemory = availableMemoryMB >= 6000;
            var recommendedBatchSize = availableMemoryMB >= 12000 ? 4 : 
                                      availableMemoryMB >= 8000 ? 2 : 1;

            return Ok(new
            {
                gpuId = gpuId,
                sdxlSupported = hasSDXLMemory,
                availableMemoryMB = availableMemoryMB,
                recommendedBatchSize = recommendedBatchSize,
                supportedFeatures = new
                {
                    text2img = true,
                    img2img = hasSDXLMemory,
                    inpainting = hasSDXLMemory,
                    controlnet = availableMemoryMB >= 8000,
                    lora = true,
                    refiner = availableMemoryMB >= 10000
                },
                performanceProfile = GetPerformanceProfile(availableMemoryMB)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get SDXL capabilities for GPU {gpuId}");
            return StatusCode(500, new { error = "Failed to get capabilities", details = ex.Message });
        }
    }

    /// <summary>
    /// Load an SDXL model suite (base + refiner + VAE) to a specific GPU
    /// </summary>
    /// <param name="gpuId">Target GPU ID</param>
    /// <param name="request">SDXL model suite request</param>
    [HttpPost("gpu/{gpuId}/load-sdxl-suite")]
    public async Task<IActionResult> LoadSDXLSuite(string gpuId, [FromBody] LoadSDXLSuiteRequest request)
    {
        try
        {
            _logger.LogInformation($"Loading SDXL suite to GPU {gpuId}: {request.ModelName}");

            var results = new List<object>();
            
            // Load base model (required)
            var baseRequest = new LoadModelRequest
            {
                ModelPath = request.BaseModelPath,
                ModelType = ModelType.SDXL,
                ModelId = $"sdxl_{request.ModelName}_base",
                ModelName = $"{request.ModelName}-base"
            };
            
            var baseResult = await _gpuPoolService.LoadModelToGpuAsync(gpuId, baseRequest);
            results.Add(new { component = "base", success = baseResult.Success, modelId = baseResult.ModelId, message = baseResult.Message });

            // Load refiner if provided
            if (!string.IsNullOrEmpty(request.RefinerModelPath))
            {
                var refinerRequest = new LoadModelRequest
                {
                    ModelPath = request.RefinerModelPath,
                    ModelType = ModelType.SDXL,
                    ModelId = $"sdxl_{request.ModelName}_refiner",
                    ModelName = $"{request.ModelName}-refiner"
                };
                
                var refinerResult = await _gpuPoolService.LoadModelToGpuAsync(gpuId, refinerRequest);
                results.Add(new { component = "refiner", success = refinerResult.Success, modelId = refinerResult.ModelId, message = refinerResult.Message });
            }

            // Load VAE if provided
            if (!string.IsNullOrEmpty(request.VaeModelPath))
            {
                var vaeRequest = new LoadModelRequest
                {
                    ModelPath = request.VaeModelPath,
                    ModelType = ModelType.VAE,
                    ModelId = $"sdxl_{request.ModelName}_vae",
                    ModelName = $"{request.ModelName}-vae"
                };
                
                var vaeResult = await _gpuPoolService.LoadModelToGpuAsync(gpuId, vaeRequest);
                results.Add(new { component = "vae", success = vaeResult.Success, modelId = vaeResult.ModelId, message = vaeResult.Message });
            }

            var allSuccess = results.All(r => ((dynamic)r).success);
            
            return Ok(new
            {
                success = allSuccess,
                message = allSuccess ? "SDXL suite loaded successfully" : "Some components failed to load",
                gpuId = gpuId,
                modelName = request.ModelName,
                components = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to load SDXL suite to GPU {gpuId}");
            return StatusCode(500, new { error = "SDXL suite loading failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all GPUs with their SDXL readiness status
    /// </summary>
    [HttpGet("sdxl-readiness")]
    public async Task<IActionResult> GetSDXLReadiness()
    {
        try
        {
            var poolStatus = await _gpuPoolService.GetPoolStatusAsync();
            
            var gpuReadiness = poolStatus.Gpus.Select(gpu => {
                var availableMemoryMB = gpu.MemoryInfo.VramAvailableBytes / (1024 * 1024);
                var totalMemoryMB = gpu.MemoryInfo.VramTotalBytes / (1024 * 1024);
                return new
                {
                    gpuId = gpu.GpuId,
                    name = gpu.DeviceName,
                    availableMemoryMB = availableMemoryMB,
                    totalMemoryMB = totalMemoryMB,
                    memoryUsagePercent = Math.Round((double)(totalMemoryMB - availableMemoryMB) / totalMemoryMB * 100, 2),
                    sdxlReady = availableMemoryMB >= 6000,
                    recommendedMaxBatchSize = availableMemoryMB >= 12000 ? 4 : 
                                             availableMemoryMB >= 8000 ? 2 : 1,
                    supportedFeatures = new
                    {
                        text2img = true,
                        img2img = availableMemoryMB >= 6000,
                        inpainting = availableMemoryMB >= 6000,
                        controlnet = availableMemoryMB >= 8000,
                        lora = true,
                        refiner = availableMemoryMB >= 10000
                    },
                    performanceProfile = GetPerformanceProfile(availableMemoryMB),
                    currentlyLoadedModels = gpu.HasModelLoaded ? 1 : 0,
                    isAvailable = gpu.IsAvailable
                };
            }).ToList();

            var readyGpuCount = gpuReadiness.Count(g => g.sdxlReady);
            
            return Ok(new
            {
                totalGpus = gpuReadiness.Count,
                sdxlReadyGpus = readyGpuCount,
                readinessPercent = gpuReadiness.Count > 0 ? Math.Round((double)readyGpuCount / gpuReadiness.Count * 100, 2) : 0,
                gpus = gpuReadiness
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SDXL readiness status");
            return StatusCode(500, new { error = "Failed to get SDXL readiness", details = ex.Message });
        }
    }

    // Helper method for performance profiling
    private static object GetPerformanceProfile(long availableMemoryMB)
    {
        if (availableMemoryMB >= 16000)
            return new { profile = "high", maxResolution = 2048, optimalSteps = 30, features = "all" };
        else if (availableMemoryMB >= 12000)
            return new { profile = "medium", maxResolution = 1536, optimalSteps = 25, features = "most" };
        else if (availableMemoryMB >= 8000)
            return new { profile = "balanced", maxResolution = 1024, optimalSteps = 20, features = "standard" };
        else
            return new { profile = "economy", maxResolution = 768, optimalSteps = 15, features = "basic" };
    }
}

/// <summary>
/// Request for batch loading models to multiple GPUs
/// </summary>
public class BatchLoadModelRequest
{
    public string ModelPath { get; set; } = string.Empty;
    public List<string> GpuIds { get; set; } = new();
    public ModelType ModelType { get; set; } = ModelType.SDXL;
    public string? ModelId { get; set; }
    public string? ModelName { get; set; }
}

/// <summary>
/// Request for loading SDXL model suite (base + refiner + VAE)
/// </summary>
public class LoadSDXLSuiteRequest
{
    /// <summary>
    /// Name identifier for this SDXL model suite
    /// </summary>
    public string ModelName { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the base SDXL model (required)
    /// </summary>
    public string BaseModelPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the refiner model (optional)
    /// </summary>
    public string? RefinerModelPath { get; set; }
    
    /// <summary>
    /// Path to the VAE model (optional)
    /// </summary>
    public string? VaeModelPath { get; set; }
}
