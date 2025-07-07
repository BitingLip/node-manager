using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Core;
using DeviceOperations.Services.Workers;
using System.Collections.Concurrent;

namespace DeviceOperations.Services.Inference;

/// <summary>
/// Service for managing a pool of GPU workers for inference operations
/// </summary>
public class GpuPoolService : IGpuPoolService, IDisposable
{
    private readonly IDeviceService _deviceService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GpuPoolService> _logger;
    private readonly ConcurrentDictionary<string, IPyTorchWorkerService> _gpuWorkers = new();
    private bool _initialized = false;

    public GpuPoolService(
        IDeviceService deviceService,
        IServiceProvider serviceProvider,
        ILogger<GpuPoolService> logger)
    {
        _deviceService = deviceService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing GPU Pool Service...");

        try
        {
            // Ensure device service is initialized
            await _deviceService.InitializeAsync();
            
            // Get available devices
            var devices = await _deviceService.GetAvailableDevicesAsync();
            
            // Create worker for each GPU
            var workerTasks = new List<Task>();
            
            foreach (var device in devices)
            {
                workerTasks.Add(CreateWorkerForGpu(device.DeviceId));
            }
            
            // Wait for all workers to initialize
            await Task.WhenAll(workerTasks);
            
            _initialized = true;
            _logger.LogInformation($"GPU Pool Service initialized with {_gpuWorkers.Count} workers");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize GPU Pool Service");
            throw;
        }
    }

    public async Task<LoadModelResponse> LoadModelToGpuAsync(string gpuId, LoadModelRequest request)
    {
        if (!_initialized) await InitializeAsync();

        if (!_gpuWorkers.TryGetValue(gpuId, out var worker))
        {
            return new LoadModelResponse
            {
                Success = false,
                Message = $"GPU {gpuId} not found in pool"
            };
        }

        try
        {
            _logger.LogInformation($"Loading model to GPU {gpuId}: {request.ModelPath}");
            
            // Check if GPU already has a model loaded
            var currentStatus = await worker.GetStatusAsync();
            if (currentStatus?.HasModelLoaded == true)
            {
                _logger.LogInformation($"GPU {gpuId} already has model '{currentStatus.CurrentModelId}' loaded. Automatically unloading before loading new model.");
                
                var unloadResult = await worker.UnloadModelAsync();
                if (!unloadResult)
                {
                    _logger.LogWarning($"Failed to unload existing model from GPU {gpuId}. Proceeding with new model load anyway.");
                }
                else
                {
                    _logger.LogInformation($"Successfully unloaded existing model from GPU {gpuId}");
                }
            }
            
            var result = await worker.LoadModelAsync(request.ModelPath, request.ModelType, request.ModelId);
            
            if (result.Success)
            {
                _logger.LogInformation($"Model loaded successfully to GPU {gpuId}: {result.ModelId}");
            }
            else
            {
                _logger.LogWarning($"Failed to load model to GPU {gpuId}: {result.Message}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading model to GPU {gpuId}");
            return new LoadModelResponse
            {
                Success = false,
                Message = $"Error loading model: {ex.Message}"
            };
        }
    }

    public async Task<bool> UnloadModelFromGpuAsync(string gpuId, string? modelId = null)
    {
        if (!_gpuWorkers.TryGetValue(gpuId, out var worker))
        {
            _logger.LogWarning($"GPU {gpuId} not found in pool");
            return false;
        }

        try
        {
            _logger.LogInformation($"Unloading model from GPU {gpuId}");
            
            var result = await worker.UnloadModelAsync();
            
            if (result)
            {
                _logger.LogInformation($"Model unloaded successfully from GPU {gpuId}");
            }
            else
            {
                _logger.LogWarning($"Failed to unload model from GPU {gpuId}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error unloading model from GPU {gpuId}");
            return false;
        }
    }

    public async Task<GpuPoolStatusResponse> GetPoolStatusAsync()
    {
        if (!_initialized) await InitializeAsync();

        var gpuStatuses = new List<GpuStatusResponse>();
        var totalVram = 0L;
        var usedVram = 0L;
        var availableGpus = 0;
        var gpusWithModels = 0;

        foreach (var kvp in _gpuWorkers)
        {
            try
            {
                var workerStatus = await kvp.Value.GetStatusAsync();
                var deviceInfo = await _deviceService.GetDeviceAsync(kvp.Key);

                var gpuStatus = new GpuStatusResponse
                {
                    GpuId = kvp.Key,
                    DeviceName = deviceInfo?.Name ?? "Unknown",
                    IsAvailable = deviceInfo?.IsAvailable ?? false,
                    IsInitialized = workerStatus.IsInitialized,
                    WorkerState = workerStatus.State,
                    HasModelLoaded = workerStatus.HasModelLoaded,
                    CurrentModelId = workerStatus.CurrentModelId,
                    CurrentModelName = workerStatus.CurrentModelName,
                    ModelLoadedAt = workerStatus.ModelLoadedAt,
                    ModelSizeBytes = workerStatus.ModelSizeBytes,
                    ActiveInferenceSessions = workerStatus.ActiveInferenceSessions,
                    LastActivity = workerStatus.LastActivity,
                    ErrorMessage = workerStatus.ErrorMessage
                };

                // Calculate VRAM info
                if (deviceInfo != null)
                {
                    totalVram += deviceInfo.TotalMemory;
                    // Estimate used VRAM based on model size
                    if (workerStatus.HasModelLoaded && workerStatus.ModelSizeBytes.HasValue)
                    {
                        usedVram += workerStatus.ModelSizeBytes.Value;
                    }

                    gpuStatus.MemoryInfo = new GpuMemoryInfo
                    {
                        VramTotalBytes = deviceInfo.TotalMemory,
                        VramUsedBytes = workerStatus.ModelSizeBytes ?? 0,
                        VramAvailableBytes = deviceInfo.AvailableMemory,
                        VramUsagePercentage = deviceInfo.TotalMemory > 0 
                            ? (double)(workerStatus.ModelSizeBytes ?? 0) / deviceInfo.TotalMemory * 100 
                            : 0
                    };
                }

                if (workerStatus.IsInitialized && workerStatus.State != WorkerState.Error)
                {
                    availableGpus++;
                }

                if (workerStatus.HasModelLoaded)
                {
                    gpusWithModels++;
                }

                gpuStatuses.Add(gpuStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting status for GPU {kvp.Key}");
                
                gpuStatuses.Add(new GpuStatusResponse
                {
                    GpuId = kvp.Key,
                    IsAvailable = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return new GpuPoolStatusResponse
        {
            Gpus = gpuStatuses,
            TotalGpus = _gpuWorkers.Count,
            AvailableGpus = availableGpus,
            GpusWithModelsLoaded = gpusWithModels,
            TotalVramBytes = totalVram,
            UsedVramBytes = usedVram,
            VramUsagePercentage = totalVram > 0 ? (double)usedVram / totalVram * 100 : 0
        };
    }

    public async Task<GpuStatusResponse?> GetGpuStatusAsync(string gpuId)
    {
        if (!_gpuWorkers.TryGetValue(gpuId, out var worker))
        {
            return null;
        }

        try
        {
            var workerStatus = await worker.GetStatusAsync();
            var deviceInfo = await _deviceService.GetDeviceAsync(gpuId);

            return new GpuStatusResponse
            {
                GpuId = gpuId,
                DeviceName = deviceInfo?.Name ?? "Unknown",
                IsAvailable = deviceInfo?.IsAvailable ?? false,
                IsInitialized = workerStatus.IsInitialized,
                WorkerState = workerStatus.State,
                HasModelLoaded = workerStatus.HasModelLoaded,
                CurrentModelId = workerStatus.CurrentModelId,
                CurrentModelName = workerStatus.CurrentModelName,
                ModelLoadedAt = workerStatus.ModelLoadedAt,
                ModelSizeBytes = workerStatus.ModelSizeBytes,
                ActiveInferenceSessions = workerStatus.ActiveInferenceSessions,
                LastActivity = workerStatus.LastActivity,
                MemoryInfo = deviceInfo != null ? new GpuMemoryInfo
                {
                    VramTotalBytes = deviceInfo.TotalMemory,
                    VramUsedBytes = workerStatus.ModelSizeBytes ?? 0,                        VramAvailableBytes = deviceInfo.AvailableMemory,
                    VramUsagePercentage = deviceInfo.TotalMemory > 0 
                        ? (double)(workerStatus.ModelSizeBytes ?? 0) / deviceInfo.TotalMemory * 100 
                        : 0
                } : new GpuMemoryInfo(),
                ErrorMessage = workerStatus.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting status for GPU {gpuId}");
            return new GpuStatusResponse
            {
                GpuId = gpuId,
                IsAvailable = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<InferenceResponse> RunInferenceOnGpuAsync(string gpuId, InferenceRequest request)
    {
        if (!_gpuWorkers.TryGetValue(gpuId, out var worker))
        {
            return new InferenceResponse
            {
                Success = false,
                Message = $"GPU {gpuId} not found in pool"
            };
        }

        try
        {
            _logger.LogInformation($"Running inference on GPU {gpuId}");
            
            var result = await worker.RunInferenceAsync(request);
            
            if (result.Success)
            {
                _logger.LogInformation($"Inference completed successfully on GPU {gpuId}");
            }
            else
            {
                _logger.LogWarning($"Inference failed on GPU {gpuId}: {result.Message}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error running inference on GPU {gpuId}");
            return new InferenceResponse
            {
                Success = false,
                Message = $"Error running inference: {ex.Message}"
            };
        }
    }

    public async Task<string?> FindBestAvailableGpuAsync(ModelType modelType)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            // Simple strategy: find GPU with least memory usage that's ready
            string? bestGpu = null;
            double lowestUsage = double.MaxValue;

            foreach (var kvp in _gpuWorkers)
            {
                var worker = kvp.Value;
                
                if (!worker.IsInitialized || worker.HasModelLoaded)
                    continue;

                var status = await worker.GetStatusAsync();
                if (status.State == WorkerState.Ready)
                {
                    var deviceInfo = await _deviceService.GetDeviceAsync(kvp.Key);
                    if (deviceInfo?.IsAvailable == true)
                    {
                        var usage = deviceInfo.TotalMemory > 0 
                            ? (double)(deviceInfo.TotalMemory - deviceInfo.AvailableMemory) / deviceInfo.TotalMemory 
                            : 0;

                        if (usage < lowestUsage)
                        {
                            lowestUsage = usage;
                            bestGpu = kvp.Key;
                        }
                    }
                }
            }

            return bestGpu;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding best available GPU");
            return null;
        }
    }

    public async Task<IEnumerable<string>> GetGpusWithModelAsync(string modelId)
    {
        var gpusWithModel = new List<string>();

        foreach (var kvp in _gpuWorkers)
        {
            try
            {
                var status = await kvp.Value.GetStatusAsync();
                if (status.HasModelLoaded && status.CurrentModelId == modelId)
                {
                    gpusWithModel.Add(kvp.Key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking model on GPU {kvp.Key}");
            }
        }

        return gpusWithModel;
    }

    private async Task CreateWorkerForGpu(string gpuId)
    {
        try
        {
            _logger.LogInformation($"Creating worker for GPU {gpuId}");
            
            // Create logger for this specific worker
            var workerLogger = _serviceProvider.GetRequiredService<ILogger<PyTorchWorkerService>>();
            
            // Create worker instance
            var worker = new PyTorchWorkerService(gpuId, workerLogger);
            
            // Initialize the worker
            var success = await worker.InitializeAsync();
            
            if (success)
            {
                _gpuWorkers[gpuId] = worker;
                _logger.LogInformation($"Worker for GPU {gpuId} created and initialized successfully");
            }
            else
            {
                _logger.LogError($"Failed to initialize worker for GPU {gpuId}");
                worker.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating worker for GPU {gpuId}");
        }
    }

    public async Task<bool> CleanupGpuMemoryAsync(string gpuId)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            if (!_gpuWorkers.TryGetValue(gpuId, out var worker))
            {
                _logger.LogWarning($"GPU worker {gpuId} not found for memory cleanup");
                return false;
            }

            if (!worker.IsInitialized)
            {
                _logger.LogWarning($"GPU worker {gpuId} not initialized for memory cleanup");
                return false;
            }

            var result = await worker.CleanupMemoryAsync();
            
            if (result)
            {
                _logger.LogInformation($"Memory cleanup successful on GPU {gpuId}");
            }
            else
            {
                _logger.LogWarning($"Memory cleanup failed on GPU {gpuId}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error cleaning up memory on GPU {gpuId}");
            return false;
        }
    }

    public async Task<SDXLCapabilitiesResponse> GetSDXLCapabilitiesAsync(string gpuId)
    {
        try
        {
            var status = await GetGpuStatusAsync(gpuId);
            if (status == null)
            {
                return new SDXLCapabilitiesResponse
                {
                    GpuId = gpuId,
                    SDXLSupported = false
                };
            }

            var availableMemoryMB = status.MemoryInfo.VramAvailableBytes / (1024 * 1024);
            var hasSDXLMemory = availableMemoryMB >= 6000;
            var recommendedBatchSize = availableMemoryMB >= 12000 ? 4 : 
                                      availableMemoryMB >= 8000 ? 2 : 1;

            return new SDXLCapabilitiesResponse
            {
                GpuId = gpuId,
                SDXLSupported = hasSDXLMemory,
                AvailableMemoryMB = availableMemoryMB,
                RecommendedBatchSize = recommendedBatchSize,
                SupportedFeatures = new SDXLSupportedFeatures
                {
                    Text2Img = true,
                    Img2Img = hasSDXLMemory,
                    Inpainting = hasSDXLMemory,
                    ControlNet = availableMemoryMB >= 8000,
                    LoRA = true,
                    Refiner = availableMemoryMB >= 10000,
                    HighResolutionGeneration = availableMemoryMB >= 12000,
                    BatchProcessing = availableMemoryMB >= 8000
                },
                PerformanceProfile = GetPerformanceProfileForMemory(availableMemoryMB),
                OptimalModelFormats = new List<string> { "safetensors", "onnx", "ckpt" },
                TechnicalSpecs = new Dictionary<string, object>
                {
                    ["recommendedPrecision"] = availableMemoryMB >= 12000 ? "fp16" : "fp32",
                    ["maxConcurrentInferences"] = Math.Max(1, (int)(availableMemoryMB / 6000)),
                    ["optimalChunkSize"] = availableMemoryMB >= 12000 ? 8 : 4
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get SDXL capabilities for GPU {gpuId}");
            return new SDXLCapabilitiesResponse
            {
                GpuId = gpuId,
                SDXLSupported = false
            };
        }
    }

    public async Task<SDXLSuiteLoadResponse> LoadSDXLModelSuiteAsync(string gpuId, LoadSDXLModelSuiteRequest request)
    {
        try
        {
            _logger.LogInformation($"Loading SDXL suite to GPU {gpuId}: {request.ModelName}");

            var response = new SDXLSuiteLoadResponse
            {
                GpuId = gpuId,
                ModelName = request.ModelName,
                LoadedAt = DateTime.UtcNow,
                ComponentResults = new List<ComponentLoadResult>()
            };

            var totalLoadTime = 0.0;
            var totalMemoryUsage = 0L;
            var allSuccess = true;

            // Load base model (required)
            if (!string.IsNullOrEmpty(request.BaseModelPath))
            {
                var baseResult = await LoadModelComponentAsync(gpuId, request.BaseModelPath, 
                    $"sdxl_{request.ModelName}_base", ModelType.SDXL, "base", request.ForceReload);
                
                response.ComponentResults.Add(baseResult);
                totalLoadTime += baseResult.LoadTimeSeconds;
                totalMemoryUsage += baseResult.MemoryUsageBytes;
                allSuccess &= baseResult.Success;
            }

            // Load other components if provided and if base was successful
            if (allSuccess && !string.IsNullOrEmpty(request.RefinerModelPath))
            {
                var refinerResult = await LoadModelComponentAsync(gpuId, request.RefinerModelPath,
                    $"sdxl_{request.ModelName}_refiner", ModelType.SDXL, "refiner", request.ForceReload);
                
                response.ComponentResults.Add(refinerResult);
                totalLoadTime += refinerResult.LoadTimeSeconds;
                totalMemoryUsage += refinerResult.MemoryUsageBytes;
                allSuccess &= refinerResult.Success;
            }

            if (allSuccess && !string.IsNullOrEmpty(request.VaeModelPath))
            {
                var vaeResult = await LoadModelComponentAsync(gpuId, request.VaeModelPath,
                    $"sdxl_{request.ModelName}_vae", ModelType.VAE, "vae", request.ForceReload);
                
                response.ComponentResults.Add(vaeResult);
                totalLoadTime += vaeResult.LoadTimeSeconds;
                totalMemoryUsage += vaeResult.MemoryUsageBytes;
                allSuccess &= vaeResult.Success;
            }

            if (allSuccess && !string.IsNullOrEmpty(request.ControlNetPath))
            {
                var controlNetResult = await LoadModelComponentAsync(gpuId, request.ControlNetPath,
                    $"sdxl_{request.ModelName}_controlnet", ModelType.Generic, "controlnet", request.ForceReload);
                
                response.ComponentResults.Add(controlNetResult);
                totalLoadTime += controlNetResult.LoadTimeSeconds;
                totalMemoryUsage += controlNetResult.MemoryUsageBytes;
                allSuccess &= controlNetResult.Success;
            }

            if (allSuccess && !string.IsNullOrEmpty(request.LoraPath))
            {
                var loraResult = await LoadModelComponentAsync(gpuId, request.LoraPath,
                    $"sdxl_{request.ModelName}_lora", ModelType.Custom, "lora", request.ForceReload);
                
                response.ComponentResults.Add(loraResult);
                totalLoadTime += loraResult.LoadTimeSeconds;
                totalMemoryUsage += loraResult.MemoryUsageBytes;
                allSuccess &= loraResult.Success;
            }

            response.Success = allSuccess;
            response.TotalMemoryUsageBytes = totalMemoryUsage;
            response.TotalLoadTimeSeconds = totalLoadTime;
            response.Message = allSuccess 
                ? $"SDXL suite '{request.ModelName}' loaded successfully with {response.ComponentResults.Count} components"
                : $"SDXL suite '{request.ModelName}' partially loaded - some components failed";

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to load SDXL suite to GPU {gpuId}");
            return new SDXLSuiteLoadResponse
            {
                Success = false,
                GpuId = gpuId,
                ModelName = request.ModelName,
                Message = $"Failed to load SDXL suite: {ex.Message}"
            };
        }
    }

    public async Task<EnhancedInferenceResponse> RunEnhancedSDXLAsync(EnhancedSDXLRequest request)
    {
        try
        {
            // Find best available GPU for SDXL
            var targetGpu = await FindBestAvailableGpuAsync(ModelType.SDXL);
            if (string.IsNullOrEmpty(targetGpu))
            {
                return new EnhancedInferenceResponse
                {
                    Success = false,
                    Message = "No available GPU found for SDXL generation"
                };
            }

            _logger.LogInformation($"Running enhanced SDXL on GPU {targetGpu}");

            // Create enhanced inference request
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

            var startTime = DateTime.UtcNow;
            var result = await RunInferenceOnGpuAsync(targetGpu, inferenceRequest);
            var endTime = DateTime.UtcNow;

            return new EnhancedInferenceResponse
            {
                Success = result.Success,
                SessionId = result.SessionId,
                Message = result.Message,
                GpuUsed = targetGpu,
                OutputPaths = result.Outputs.ContainsKey("output_paths") ? 
                    (List<string>)(result.Outputs["output_paths"]) : null,
                Metrics = new InferenceMetrics
                {
                    TotalTimeSeconds = (endTime - startTime).TotalSeconds,
                    InferenceTimeSeconds = result.InferenceTimeMs / 1000.0,
                    MemoryUsedBytes = result.Statistics?.MemoryUsageBytes ?? 0,
                    StepsCompleted = 25, // Default value since steps not exposed in HyperParametersConfig
                    PerformanceScore = CalculatePerformanceScore(result)
                },
                AdditionalData = result.Outputs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enhanced SDXL inference failed");
            return new EnhancedInferenceResponse
            {
                Success = false,
                Message = $"Enhanced SDXL inference failed: {ex.Message}"
            };
        }
    }

    public async Task<SDXLReadinessResponse> GetSDXLReadinessAsync()
    {
        try
        {
            var poolStatus = await GetPoolStatusAsync();
            
            var gpuReadiness = poolStatus.Gpus.Select(gpu => {
                var availableMemoryMB = gpu.MemoryInfo.VramAvailableBytes / (1024 * 1024);
                var totalMemoryMB = gpu.MemoryInfo.VramTotalBytes / (1024 * 1024);
                var memoryUsagePercent = totalMemoryMB > 0 ? 
                    Math.Round((double)(totalMemoryMB - availableMemoryMB) / totalMemoryMB * 100, 2) : 0;

                return new GpuReadinessInfo
                {
                    GpuId = gpu.GpuId,
                    Name = gpu.DeviceName,
                    AvailableMemoryMB = availableMemoryMB,
                    TotalMemoryMB = totalMemoryMB,
                    MemoryUsagePercent = memoryUsagePercent,
                    SDXLReady = availableMemoryMB >= 6000,
                    RecommendedMaxBatchSize = availableMemoryMB >= 12000 ? 4 : 
                                             availableMemoryMB >= 8000 ? 2 : 1,
                    SupportedFeatures = new SDXLSupportedFeatures
                    {
                        Text2Img = true,
                        Img2Img = availableMemoryMB >= 6000,
                        Inpainting = availableMemoryMB >= 6000,
                        ControlNet = availableMemoryMB >= 8000,
                        LoRA = true,
                        Refiner = availableMemoryMB >= 10000,
                        HighResolutionGeneration = availableMemoryMB >= 12000,
                        BatchProcessing = availableMemoryMB >= 8000
                    },
                    PerformanceProfile = GetPerformanceProfileForMemory(availableMemoryMB),
                    CurrentlyLoadedModels = gpu.HasModelLoaded ? 1 : 0,
                    IsAvailable = gpu.IsAvailable
                };
            }).ToList();

            var readyGpuCount = gpuReadiness.Count(g => g.SDXLReady);
            var readinessPercent = gpuReadiness.Count > 0 ? 
                Math.Round((double)readyGpuCount / gpuReadiness.Count * 100, 2) : 0;

            return new SDXLReadinessResponse
            {
                TotalGpus = gpuReadiness.Count,
                SDXLReadyGpus = readyGpuCount,
                ReadinessPercent = readinessPercent,
                Gpus = gpuReadiness,
                OptimalDistribution = GenerateOptimalDistribution(gpuReadiness)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SDXL readiness status");
            return new SDXLReadinessResponse();
        }
    }

    public async Task<BatchLoadResponse> BatchLoadModelAsync(BatchLoadModelRequest request)
    {
        try
        {
            _logger.LogInformation($"Batch loading model {request.ModelPath} to {request.GpuIds.Count} GPUs");

            var response = new BatchLoadResponse
            {
                TotalRequested = request.GpuIds.Count,
                Results = new List<GpuLoadResult>()
            };

            var startTime = DateTime.UtcNow;
            var tasks = new List<Task>();

            foreach (var gpuId in request.GpuIds)
            {
                if (request.EnableParallelLoading)
                {
                    tasks.Add(LoadModelToGpuBatch(gpuId, request, response.Results));
                }
                else
                {
                    await LoadModelToGpuBatch(gpuId, request, response.Results);
                }
            }

            if (request.EnableParallelLoading)
            {
                await Task.WhenAll(tasks);
            }

            var endTime = DateTime.UtcNow;
            response.TotalLoadTimeSeconds = (endTime - startTime).TotalSeconds;
            response.Successful = response.Results.Count(r => r.Success);
            response.Failed = response.Results.Count(r => !r.Success);
            response.Success = response.Successful > 0;
            response.Message = $"Batch operation completed: {response.Successful}/{response.TotalRequested} successful";

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch model loading failed");
            return new BatchLoadResponse
            {
                Success = false,
                Message = $"Batch loading failed: {ex.Message}",
                TotalRequested = request.GpuIds.Count
            };
        }
    }

    public async Task<BatchOperationResponse> CleanupAllGpuMemoryAsync()
    {
        try
        {
            _logger.LogInformation("Cleaning up memory on all GPUs");

            var status = await GetPoolStatusAsync();
            var response = new BatchOperationResponse
            {
                TotalGpus = status.Gpus.Count,
                Results = new List<GpuOperationResult>()
            };

            var startTime = DateTime.UtcNow;
            var tasks = new List<Task>();

            foreach (var gpu in status.Gpus)
            {
                tasks.Add(CleanupGpuMemoryBatch(gpu.GpuId, response.Results));
            }

            await Task.WhenAll(tasks);

            var endTime = DateTime.UtcNow;
            response.TotalOperationTimeSeconds = (endTime - startTime).TotalSeconds;
            response.Successful = response.Results.Count(r => r.Success);
            response.Failed = response.Results.Count(r => !r.Success);
            response.Success = response.Successful > 0;
            response.Message = $"Memory cleanup completed: {response.Successful}/{response.TotalGpus} successful";

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory cleanup failed");
            return new BatchOperationResponse
            {
                Success = false,
                Message = $"Memory cleanup failed: {ex.Message}"
            };
        }
    }

    public async Task<LoadBalancingResponse> AutoBalanceWorkloadsAsync(string workloadType = "sdxl")
    {
        try
        {
            _logger.LogInformation($"Auto-balancing workloads for type: {workloadType}");

            var poolStatus = await GetPoolStatusAsync();
            var recommendations = new List<LoadBalancingRecommendation>();

            // Simple load balancing strategy
            var availableGpus = poolStatus.Gpus
                .Where(g => g.IsAvailable && g.IsInitialized)
                .OrderBy(g => g.MemoryInfo.VramUsagePercentage)
                .ToList();

            var overloadedGpus = poolStatus.Gpus
                .Where(g => g.MemoryInfo.VramUsagePercentage > 85)
                .ToList();

            // Generate migration recommendations
            foreach (var overloaded in overloadedGpus)
            {
                var targetGpu = availableGpus
                    .FirstOrDefault(g => g.GpuId != overloaded.GpuId && 
                                       g.MemoryInfo.VramUsagePercentage < 50);

                if (targetGpu != null)
                {
                    recommendations.Add(new LoadBalancingRecommendation
                    {
                        Action = "migrate",
                        GpuId = overloaded.GpuId,
                        TargetGpuId = targetGpu.GpuId,
                        ModelId = overloaded.CurrentModelId,
                        Description = $"Migrate model from overloaded GPU {overloaded.GpuId} to underutilized GPU {targetGpu.GpuId}",
                        ExpectedBenefit = 0.8,
                        Priority = 1
                    });
                }
            }

            // Generate cleanup recommendations
            var underutilizedGpus = poolStatus.Gpus
                .Where(g => g.IsAvailable && g.MemoryInfo.VramUsagePercentage < 10 && g.HasModelLoaded)
                .ToList();

            foreach (var underutilized in underutilizedGpus)
            {
                recommendations.Add(new LoadBalancingRecommendation
                {
                    Action = "cleanup",
                    GpuId = underutilized.GpuId,
                    Description = $"Clean up memory on underutilized GPU {underutilized.GpuId}",
                    ExpectedBenefit = 0.5,
                    Priority = 2
                });
            }

            var strategy = recommendations.Any() ? "optimize" : "maintain";
            var expectedImprovement = recommendations.Sum(r => r.ExpectedBenefit) / Math.Max(1, recommendations.Count);

            return new LoadBalancingResponse
            {
                Success = true,
                WorkloadType = workloadType,
                Strategy = strategy,
                Recommendations = recommendations,
                ExpectedImprovement = expectedImprovement,
                GeneratedAt = DateTime.UtcNow,
                Message = $"Generated {recommendations.Count} optimization recommendations"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-balancing failed");
            return new LoadBalancingResponse
            {
                Success = false,
                WorkloadType = workloadType,
                Message = $"Auto-balancing failed: {ex.Message}",
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<GpuPerformanceMetrics> GetPerformanceMetricsAsync()
    {
        try
        {
            await Task.CompletedTask;
            var poolStatus = await GetPoolStatusAsync();
            var gpuMetrics = new List<GpuMetrics>();
            var alerts = new List<PerformanceAlert>();

            foreach (var gpu in poolStatus.Gpus)
            {
                var metrics = new GpuMetrics
                {
                    GpuId = gpu.GpuId,
                    UtilizationPercent = gpu.HasModelLoaded ? 70.0 : 5.0, // Simulated
                    MemoryUsagePercent = gpu.MemoryInfo.VramUsagePercentage,
                    TemperatureCelsius = 65.0, // Simulated
                    PowerUsageWatts = gpu.HasModelLoaded ? 250.0 : 50.0, // Simulated
                    ActiveInferenceSessions = gpu.ActiveInferenceSessions,
                    AverageInferenceTimeSeconds = 2.5, // Simulated
                    CompletedInferences = 100, // Simulated
                    LastActivity = gpu.LastActivity != DateTime.MinValue ? gpu.LastActivity : DateTime.UtcNow.AddMinutes(-5),
                    LoadedModels = gpu.HasModelLoaded ? new List<string> { gpu.CurrentModelId ?? "unknown" } : new List<string>()
                };

                // Generate alerts
                if (metrics.MemoryUsagePercent > 90)
                {
                    alerts.Add(new PerformanceAlert
                    {
                        Level = "warning",
                        Message = $"High memory usage on GPU {gpu.GpuId}",
                        GpuId = gpu.GpuId,
                        MetricType = "memory",
                        CurrentValue = metrics.MemoryUsagePercent,
                        ThresholdValue = 90,
                        DetectedAt = DateTime.UtcNow,
                        RecommendedAction = "Consider unloading unused models or migrating workload"
                    });
                }

                gpuMetrics.Add(metrics);
            }

            var poolMetrics = new PoolPerformanceMetrics
            {
                AverageUtilization = gpuMetrics.Average(m => m.UtilizationPercent),
                TotalThroughput = gpuMetrics.Sum(m => m.CompletedInferences),
                AverageLatency = gpuMetrics.Average(m => m.AverageInferenceTimeSeconds),
                TotalActiveInferences = gpuMetrics.Sum(m => m.ActiveInferenceSessions),
                MemoryEfficiency = Math.Max(0, 100 - gpuMetrics.Average(m => m.MemoryUsagePercent)),
                PowerEfficiency = gpuMetrics.Average(m => m.CompletedInferences / Math.Max(1, m.PowerUsageWatts)),
                BottleneckAnalysis = alerts.Any() ? "Memory pressure detected" : "No bottlenecks detected"
            };

            return new GpuPerformanceMetrics
            {
                MetricsGeneratedAt = DateTime.UtcNow,
                GpuMetrics = gpuMetrics,
                PoolMetrics = poolMetrics,
                Alerts = alerts,
                RawMetrics = new Dictionary<string, object>
                {
                    ["totalGpus"] = poolStatus.Gpus.Count,
                    ["activeGpus"] = poolStatus.AvailableGpus,
                    ["totalVramGB"] = poolStatus.TotalVramBytes / (1024.0 * 1024.0 * 1024.0),
                    ["usedVramGB"] = poolStatus.UsedVramBytes / (1024.0 * 1024.0 * 1024.0)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get performance metrics");
            return new GpuPerformanceMetrics
            {
                MetricsGeneratedAt = DateTime.UtcNow,
                GpuMetrics = new List<GpuMetrics>(),
                PoolMetrics = new PoolPerformanceMetrics(),
                Alerts = new List<PerformanceAlert>()
            };
        }
    }

    // Helper methods for enhanced GPU pool functionality
    private async Task<ComponentLoadResult> LoadModelComponentAsync(
        string gpuId, 
        string modelPath, 
        string modelId, 
        ModelType modelType, 
        string componentType,
        bool forceReload)
    {
        try
        {
            var loadRequest = new LoadModelRequest
            {
                ModelPath = modelPath,
                ModelId = modelId,
                ModelType = modelType
            };

            var startTime = DateTime.UtcNow;
            var result = await LoadModelToGpuAsync(gpuId, loadRequest);
            var endTime = DateTime.UtcNow;

            return new ComponentLoadResult
            {
                ComponentType = componentType,
                ModelId = result.ModelId,
                Success = result.Success,
                Message = result.Message,
                MemoryUsageBytes = result.ModelSizeBytes,
                LoadTimeSeconds = (endTime - startTime).TotalSeconds
            };
        }
        catch (Exception ex)
        {
            return new ComponentLoadResult
            {
                ComponentType = componentType,
                Success = false,
                Message = $"Failed to load {componentType}: {ex.Message}"
            };
        }
    }

    private async Task LoadModelToGpuBatch(string gpuId, BatchLoadModelRequest request, List<GpuLoadResult> results)
    {
        try
        {
            var loadRequest = new LoadModelRequest
            {
                ModelPath = request.ModelPath,
                ModelType = request.ModelType,
                ModelId = request.ModelId,
                ModelName = request.ModelName
            };

            var startTime = DateTime.UtcNow;
            var result = await LoadModelToGpuAsync(gpuId, loadRequest);
            var endTime = DateTime.UtcNow;

            lock (results)
            {
                results.Add(new GpuLoadResult
                {
                    GpuId = gpuId,
                    Success = result.Success,
                    ModelId = result.ModelId,
                    Message = result.Message,
                    LoadTimeSeconds = (endTime - startTime).TotalSeconds,
                    MemoryUsageBytes = result.ModelSizeBytes
                });
            }
        }
        catch (Exception ex)
        {
            lock (results)
            {
                results.Add(new GpuLoadResult
                {
                    GpuId = gpuId,
                    Success = false,
                    Message = $"Failed to load model: {ex.Message}"
                });
            }
        }
    }

    private async Task CleanupGpuMemoryBatch(string gpuId, List<GpuOperationResult> results)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var success = await CleanupGpuMemoryAsync(gpuId);
            var endTime = DateTime.UtcNow;

            lock (results)
            {
                results.Add(new GpuOperationResult
                {
                    GpuId = gpuId,
                    Success = success,
                    Message = success ? "Memory cleanup successful" : "Memory cleanup failed",
                    OperationTimeSeconds = (endTime - startTime).TotalSeconds
                });
            }
        }
        catch (Exception ex)
        {
            lock (results)
            {
                results.Add(new GpuOperationResult
                {
                    GpuId = gpuId,
                    Success = false,
                    Message = $"Memory cleanup failed: {ex.Message}"
                });
            }
        }
    }

    private static GpuPerformanceProfile GetPerformanceProfileForMemory(long availableMemoryMB)
    {
        if (availableMemoryMB >= 16000)
            return new GpuPerformanceProfile 
            { 
                Profile = "high", 
                MaxResolution = 2048, 
                OptimalSteps = 30, 
                MaxBatchSize = 4,
                EstimatedSecondsPerImage = 2.0,
                Features = "all" 
            };
        else if (availableMemoryMB >= 12000)
            return new GpuPerformanceProfile 
            { 
                Profile = "medium", 
                MaxResolution = 1536, 
                OptimalSteps = 25, 
                MaxBatchSize = 2,
                EstimatedSecondsPerImage = 3.0,
                Features = "most" 
            };
        else if (availableMemoryMB >= 8000)
            return new GpuPerformanceProfile 
            { 
                Profile = "balanced", 
                MaxResolution = 1024, 
                OptimalSteps = 20, 
                MaxBatchSize = 1,
                EstimatedSecondsPerImage = 4.0,
                Features = "standard" 
            };
        else
            return new GpuPerformanceProfile 
            { 
                Profile = "economy", 
                MaxResolution = 768, 
                OptimalSteps = 15, 
                MaxBatchSize = 1,
                EstimatedSecondsPerImage = 6.0,
                Features = "basic" 
            };
    }

    private static OptimalWorkloadDistribution GenerateOptimalDistribution(List<GpuReadinessInfo> gpus)
    {
        var recommendations = new List<WorkloadRecommendation>();
        var strategy = "balanced";
        var totalGpus = gpus.Count;
        var readyGpus = gpus.Count(g => g.SDXLReady);

        if (readyGpus > 0)
        {
            var gpusPerWorkload = Math.Max(1, readyGpus / 3); // Distribute across 3 workload types
            
            var highPerfGpus = gpus.Where(g => g.AvailableMemoryMB >= 12000).Take(gpusPerWorkload);
            var mediumPerfGpus = gpus.Where(g => g.AvailableMemoryMB >= 8000 && g.AvailableMemoryMB < 12000).Take(gpusPerWorkload);
            var standardGpus = gpus.Where(g => g.AvailableMemoryMB >= 6000 && g.AvailableMemoryMB < 8000).Take(gpusPerWorkload);

            foreach (var gpu in highPerfGpus)
            {
                recommendations.Add(new WorkloadRecommendation
                {
                    GpuId = gpu.GpuId,
                    RecommendedWorkload = "high-resolution",
                    RecommendedBatchSize = 4,
                    ExpectedThroughput = 1.5,
                    Reasoning = "High memory GPU optimal for batch processing"
                });
            }

            foreach (var gpu in mediumPerfGpus)
            {
                recommendations.Add(new WorkloadRecommendation
                {
                    GpuId = gpu.GpuId,
                    RecommendedWorkload = "standard-resolution",
                    RecommendedBatchSize = 2,
                    ExpectedThroughput = 1.0,
                    Reasoning = "Medium memory GPU suitable for standard workloads"
                });
            }

            foreach (var gpu in standardGpus)
            {
                recommendations.Add(new WorkloadRecommendation
                {
                    GpuId = gpu.GpuId,
                    RecommendedWorkload = "single-image",
                    RecommendedBatchSize = 1,
                    ExpectedThroughput = 0.5,
                    Reasoning = "Standard memory GPU for single image generation"
                });
            }
        }

        var efficiency = readyGpus > 0 ? (double)readyGpus / totalGpus : 0.0;

        return new OptimalWorkloadDistribution
        {
            Recommendations = recommendations,
            Strategy = strategy,
            EfficiencyScore = efficiency
        };
    }

    private static double CalculatePerformanceScore(InferenceResponse result)
    {
        if (!result.Success) return 0.0;
        
        // Simple performance scoring based on execution time
        var executionTimeSeconds = result.InferenceTimeMs / 1000.0;
        
        return executionTimeSeconds switch
        {
            <= 1.0 => 100.0,
            <= 2.0 => 90.0,
            <= 3.0 => 80.0,
            <= 5.0 => 70.0,
            <= 10.0 => 60.0,
            _ => 50.0
        };
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing GPU Pool Service...");
        
        foreach (var kvp in _gpuWorkers)
        {
            try
            {
                kvp.Value.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error disposing worker for GPU {kvp.Key}");
            }
        }
        
        _gpuWorkers.Clear();
        _initialized = false;
        
        _logger.LogInformation("GPU Pool Service disposed");
    }
}
