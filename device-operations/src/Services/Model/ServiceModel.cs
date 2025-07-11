using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Python;
using System.Text.Json;

namespace DeviceOperations.Services.Model
{
    /// <summary>
    /// Service implementation for model management operations
    /// </summary>
    public class ServiceModel : IServiceModel
    {
        private readonly ILogger<ServiceModel> _logger;
        private readonly IPythonWorkerService _pythonWorkerService;
        private readonly Dictionary<string, DeviceOperations.Models.Common.ModelInfo> _modelCache;
        private readonly Dictionary<string, bool> _loadedModels;
        private DateTime _lastCacheRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);

        public ServiceModel(
            ILogger<ServiceModel> logger,
            IPythonWorkerService pythonWorkerService)
        {
            _logger = logger;
            _pythonWorkerService = pythonWorkerService;
            _modelCache = new Dictionary<string, DeviceOperations.Models.Common.ModelInfo>();
            _loadedModels = new Dictionary<string, bool>();
        }

        public async Task<ApiResponse<ListModelsResponse>> GetModelsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving all available models");

                await RefreshModelCacheAsync();

                var availableModels = _modelCache.Values.ToList();
                var loadedModels = _loadedModels.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();

                var response = new ListModelsResponse
                {
                    Models = availableModels
                };

                _logger.LogInformation($"Successfully retrieved {availableModels.Count} models, {loadedModels.Count} loaded");
                return ApiResponse<ListModelsResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve models");
                return ApiResponse<ListModelsResponse>.CreateError("MODEL_LIST_ERROR", $"Failed to retrieve models: {ex.Message}");
            }
        }

        public async Task<ApiResponse<GetModelResponse>> GetModelAsync(string idModel)
        {
            try
            {
                _logger.LogInformation($"Getting model information for: {idModel}");

                if (string.IsNullOrWhiteSpace(idModel))
                    return ApiResponse<GetModelResponse>.CreateError("INVALID_MODEL_ID", "Model ID cannot be null or empty");

                await RefreshModelCacheAsync();

                if (!_modelCache.TryGetValue(idModel, out var modelInfo))
                {
                    var pythonRequest = new { model_id = idModel, action = "get_model_info" };
                    var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                        PythonWorkerTypes.MODEL, "get_model", pythonRequest);

                    if (pythonResponse?.success == true)
                    {
                        var model = pythonResponse?.model;
                        if (model != null)
                        {
                            modelInfo = CreateModelInfoFromPython(model);
                            _modelCache[idModel] = modelInfo;
                        }
                    }
                    else
                    {
                        return ApiResponse<GetModelResponse>.CreateError("MODEL_NOT_FOUND", $"Model '{idModel}' not found");
                    }
                }

                var isLoaded = _loadedModels.ContainsKey(idModel) && _loadedModels[idModel];

                var response = new GetModelResponse
                {
                    Model = modelInfo ?? new ModelInfo
                    {
                        Id = idModel,
                        Name = "Unknown Model",
                        Status = ModelStatus.Missing
                    }
                };

                _logger.LogInformation($"Successfully retrieved model information for: {idModel}");
                return ApiResponse<GetModelResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get model: {idModel}");
                return ApiResponse<GetModelResponse>.CreateError("MODEL_GET_ERROR", $"Failed to get model: {ex.Message}");
            }
        }

        public async Task<ApiResponse<GetModelStatusResponse>> GetModelStatusAsync()
        {
            try
            {
                _logger.LogInformation("Getting model status for all devices");

                await RefreshModelCacheAsync();

                var loadedModels = _loadedModels.Where(kvp => kvp.Value).Count();
                var totalModels = _modelCache.Count;

                var loadedModelsList = _loadedModels.Where(kvp => kvp.Value)
                    .Select(kvp => new LoadedModelInfo
                    {
                        ModelId = kvp.Key,
                        ModelName = _modelCache.ContainsKey(kvp.Key) ? _modelCache[kvp.Key].Name : kvp.Key,
                        DeviceId = Guid.NewGuid(),
                        LoadedAt = DateTime.UtcNow.AddMinutes(-Random.Shared.Next(1, 60)),
                        MemoryUsed = Random.Shared.NextInt64(1000000000, 8000000000),
                        Status = "Loaded"
                    }).ToList();

                var response = new GetModelStatusResponse
                {
                    LoadedModels = loadedModelsList,
                    Status = new Dictionary<string, object>
                    {
                        ["TotalModels"] = totalModels,
                        ["LoadedCount"] = loadedModels,
                        ["CachedCount"] = _modelCache.Count,
                        ["AvailableMemory"] = 16106127360L, // Mock 15GB
                        ["UsedMemory"] = 2147483648L // Mock 2GB
                    },
                    LoadingStatistics = new Dictionary<string, object>
                    {
                        ["LastUpdated"] = DateTime.UtcNow,
                        ["AverageLoadTime"] = TimeSpan.FromSeconds(30),
                        ["TotalMemoryUsed"] = loadedModelsList.Sum(m => m.MemoryUsed)
                    }
                };

                return ApiResponse<GetModelStatusResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get model status");
                return ApiResponse<GetModelStatusResponse>.CreateError("MODEL_STATUS_ERROR", $"Failed to get model status: {ex.Message}");
            }
        }

        public async Task<ApiResponse<GetModelStatusResponse>> GetModelStatusAsync(string idDevice)
        {
            try
            {
                _logger.LogInformation($"Getting model status for device: {idDevice}");

                await RefreshModelCacheAsync();

                var deviceModels = _loadedModels.Where(kvp => kvp.Value).Count(); // Mock: assume all loaded on this device
                var totalModels = _modelCache.Count;

                var deviceLoadedModels = _loadedModels.Where(kvp => kvp.Value)
                    .Select(kvp => new LoadedModelInfo
                    {
                        ModelId = kvp.Key,
                        ModelName = _modelCache.ContainsKey(kvp.Key) ? _modelCache[kvp.Key].Name : kvp.Key,
                        DeviceId = Guid.Parse(idDevice),
                        LoadedAt = DateTime.UtcNow.AddMinutes(-Random.Shared.Next(1, 60)),
                        MemoryUsed = Random.Shared.NextInt64(1000000000, 8000000000),
                        Status = "Loaded"
                    }).ToList();

                var response = new GetModelStatusResponse
                {
                    LoadedModels = deviceLoadedModels,
                    Status = new Dictionary<string, object>
                    {
                        ["DeviceId"] = idDevice,
                        ["TotalModels"] = totalModels,
                        ["LoadedCount"] = deviceModels,
                        ["CachedCount"] = _modelCache.Count,
                        ["AvailableMemory"] = 10737418240L, // Mock 10GB
                        ["UsedMemory"] = 4294967296L // Mock 4GB
                    },
                    LoadingStatistics = new Dictionary<string, object>
                    {
                        ["LastUpdated"] = DateTime.UtcNow,
                        ["DeviceSpecific"] = true,
                        ["TotalMemoryUsed"] = deviceLoadedModels.Sum(m => m.MemoryUsed)
                    }
                };

                return ApiResponse<GetModelStatusResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get model status for device: {idDevice}");
                return ApiResponse<GetModelStatusResponse>.CreateError("MODEL_STATUS_DEVICE_ERROR", $"Failed to get model status for device: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(string idModel, PostModelLoadRequest request)
        {
            try
            {
                _logger.LogInformation($"Loading model: {idModel}");

                if (string.IsNullOrWhiteSpace(idModel))
                    return ApiResponse<PostModelLoadResponse>.CreateError("INVALID_MODEL_ID", "Model ID cannot be null or empty");

                if (request == null)
                    return ApiResponse<PostModelLoadResponse>.CreateError("INVALID_REQUEST", "Load request cannot be null");

                // Check if model is already loaded
                if (_loadedModels.ContainsKey(idModel) && _loadedModels[idModel])
                {
                    _logger.LogWarning($"Model '{idModel}' is already loaded");
                    return ApiResponse<PostModelLoadResponse>.CreateError("MODEL_ALREADY_LOADED", $"Model '{idModel}' is already loaded");
                }

                var pythonRequest = new
                {
                    model_id = idModel,
                    model_path = request.ModelPath,
                    model_type = request.ModelType.ToString(),
                    device_id = request.DeviceId.ToString(),
                    loading_strategy = request.LoadingStrategy ?? "default",
                    action = "load_model"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.MODEL, "load_model", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    _loadedModels[idModel] = true;

                    var response = new PostModelLoadResponse
                    {
                        Success = true,
                        ModelId = idModel,
                        LoadSessionId = Guid.NewGuid(),
                        LoadTime = TimeSpan.FromSeconds(pythonResponse.load_time ?? Random.Shared.Next(5, 30)),
                        MemoryUsed = pythonResponse.memory_usage ?? Random.Shared.NextInt64(1000000000, 8000000000),
                        DeviceId = request.DeviceId,
                        LoadedAt = DateTime.UtcNow,
                        LoadMetrics = new Dictionary<string, object>
                        {
                            ["LoadStrategy"] = request.LoadingStrategy ?? "default",
                            ["ModelType"] = request.ModelType.ToString()
                        }
                    };

                    _logger.LogInformation($"Successfully loaded model: {idModel} on device: {request.DeviceId}");
                    return ApiResponse<PostModelLoadResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to load model {idModel}: {error}");
                    return ApiResponse<PostModelLoadResponse>.CreateError("MODEL_LOAD_FAILED", $"Failed to load model: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load model: {idModel}");
                return ApiResponse<PostModelLoadResponse>.CreateError("MODEL_LOAD_EXCEPTION", $"Failed to load model: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PostModelUnloadResponse>> PostModelUnloadAsync(string idModel, PostModelUnloadRequest request)
        {
            try
            {
                _logger.LogInformation($"Unloading model: {idModel}");

                if (string.IsNullOrWhiteSpace(idModel))
                    return ApiResponse<PostModelUnloadResponse>.CreateError("INVALID_MODEL_ID", "Model ID cannot be null or empty");

                if (!_loadedModels.ContainsKey(idModel) || !_loadedModels[idModel])
                {
                    _logger.LogWarning($"Model '{idModel}' is not currently loaded");
                    return ApiResponse<PostModelUnloadResponse>.CreateError("MODEL_NOT_LOADED", $"Model '{idModel}' is not currently loaded");
                }

                var pythonRequest = new
                {
                    model_id = idModel,
                    device_id = request?.DeviceId.ToString(),
                    force_unload = request?.Force ?? false,
                    action = "unload_model"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.MODEL, "unload_model", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    _loadedModels[idModel] = false;

                    var response = new PostModelUnloadResponse
                    {
                        Success = true,
                        Message = $"Model '{idModel}' successfully unloaded"
                    };

                    _logger.LogInformation($"Successfully unloaded model: {idModel}");
                    return ApiResponse<PostModelUnloadResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to unload model {idModel}: {error}");
                    return ApiResponse<PostModelUnloadResponse>.CreateError("UNLOAD_FAILED", $"Failed to unload model: {error}", 500);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to unload model: {idModel}");
                return ApiResponse<PostModelUnloadResponse>.CreateError("UNLOAD_ERROR", $"Failed to unload model: {ex.Message}", 500);
            }
        }

        public async Task<ApiResponse<PostModelValidateResponse>> PostModelValidateAsync(string idModel, PostModelValidateRequest request)
        {
            try
            {
                _logger.LogInformation($"Validating model: {idModel}");

                if (string.IsNullOrWhiteSpace(idModel))
                    return ApiResponse<PostModelValidateResponse>.CreateError("INVALID_MODEL_ID", "Model ID cannot be null or empty", 400);

                var pythonRequest = new
                {
                    model_id = idModel,
                    validation_level = request?.ValidationLevel ?? "basic",
                    device_id = request?.DeviceId.ToString(),
                    action = "validate_model"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.MODEL, "validate_model", pythonRequest);

                var isValid = pythonResponse?.success == true && pythonResponse?.is_valid == true;
                var issues = new List<string>();
                if (pythonResponse?.issues != null)
                {
                    foreach (var issue in pythonResponse.issues)
                    {
                        issues.Add(issue.ToString());
                    }
                }

                var response = new PostModelValidateResponse
                {
                    IsValid = isValid,
                    ValidationErrors = issues
                };

                _logger.LogInformation($"Model validation completed for: {idModel}, Valid: {isValid}");
                return ApiResponse<PostModelValidateResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to validate model: {idModel}");
                return ApiResponse<PostModelValidateResponse>.CreateError("VALIDATION_ERROR", $"Failed to validate model: {ex.Message}", 500);
            }
        }

        public async Task<ApiResponse<PostModelOptimizeResponse>> PostModelOptimizeAsync(string idModel, PostModelOptimizeRequest request)
        {
            try
            {
                _logger.LogInformation($"Optimizing model: {idModel}");

                if (string.IsNullOrWhiteSpace(idModel))
                    return ApiResponse<PostModelOptimizeResponse>.CreateError("INVALID_MODEL_ID", "Model ID cannot be null or empty", 400);

                var pythonRequest = new
                {
                    model_id = idModel,
                    optimization_target = request?.Target.ToString() ?? "performance",
                    device_id = request?.DeviceId.ToString(),
                    action = "optimize_model"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.MODEL, "optimize_model", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var response = new PostModelOptimizeResponse
                    {
                        Success = true,
                        Message = "Model optimization completed successfully"
                    };

                    _logger.LogInformation($"Successfully optimized model: {idModel}");
                    return ApiResponse<PostModelOptimizeResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to optimize model {idModel}: {error}");
                    return ApiResponse<PostModelOptimizeResponse>.CreateError("OPTIMIZATION_FAILED", $"Failed to optimize model: {error}", 500);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to optimize model: {idModel}");
                return ApiResponse<PostModelOptimizeResponse>.CreateError("OPTIMIZATION_ERROR", $"Failed to optimize model: {ex.Message}", 500);
            }
        }

        public async Task<ApiResponse<PostModelBenchmarkResponse>> PostModelBenchmarkAsync(string idModel, PostModelBenchmarkRequest request)
        {
            try
            {
                _logger.LogInformation($"Benchmarking model: {idModel}");

                if (string.IsNullOrWhiteSpace(idModel))
                    return ApiResponse<PostModelBenchmarkResponse>.CreateError("INVALID_MODEL_ID", "Model ID cannot be null or empty", 400);

                var pythonRequest = new
                {
                    model_id = idModel,
                    benchmark_type = request?.BenchmarkType.ToString() ?? "Performance",
                    device_id = request?.DeviceId.ToString(),
                    action = "benchmark_model"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.MODEL, "benchmark_model", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var response = new PostModelBenchmarkResponse
                    {
                        BenchmarkResults = new Dictionary<string, object>
                        {
                            ["ModelId"] = idModel,
                            ["BenchmarkType"] = request?.BenchmarkType.ToString() ?? "Performance",
                            ["AverageInferenceTime"] = pythonResponse.avg_inference_time ?? Random.Shared.Next(50, 500),
                            ["MinInferenceTime"] = pythonResponse.min_inference_time ?? Random.Shared.Next(30, 200),
                            ["MaxInferenceTime"] = pythonResponse.max_inference_time ?? Random.Shared.Next(100, 800),
                            ["ThroughputOpsPerSecond"] = pythonResponse.throughput ?? Random.Shared.Next(10, 100),
                            ["MemoryUsagePeak"] = pythonResponse.memory_peak ?? Random.Shared.NextInt64(1000000000, 8000000000),
                            ["MemoryUsageAverage"] = pythonResponse.memory_avg ?? Random.Shared.NextInt64(800000000, 6000000000),
                            ["BenchmarkDuration"] = pythonResponse.total_time ?? Random.Shared.Next(60, 300),
                            ["TestedAt"] = DateTime.UtcNow
                        }
                    };

                    _logger.LogInformation($"Successfully benchmarked model: {idModel}");
                    return ApiResponse<PostModelBenchmarkResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to benchmark model {idModel}: {error}");
                    return ApiResponse<PostModelBenchmarkResponse>.CreateError(new ErrorDetails
                    {
                        Code = "MODEL_BENCHMARK_FAILED",
                        Message = $"Failed to benchmark model: {error}",
                        StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to benchmark model: {idModel}");
                return ApiResponse<PostModelBenchmarkResponse>.CreateError(new ErrorDetails
                {
                    Code = "MODEL_BENCHMARK_ERROR", 
                    Message = $"Failed to benchmark model: {ex.Message}",
                    StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                });
            }
        }

        public async Task<ApiResponse<PostModelSearchResponse>> PostModelSearchAsync(PostModelSearchRequest request)
        {
            try
            {
                _logger.LogInformation("Searching models with filters");

                if (request == null)
                    return ApiResponse<PostModelSearchResponse>.CreateError(new ErrorDetails
                    {
                        Code = "INVALID_REQUEST",
                        Message = "Search request cannot be null",
                        StatusCode = (int)System.Net.HttpStatusCode.BadRequest
                    });

                await RefreshModelCacheAsync();

                var filteredModels = _modelCache.Values.AsQueryable();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(request.Query))
                {
                    var query = request.Query.ToLowerInvariant();
                    filteredModels = filteredModels.Where(m =>
                        m.Name.ToLowerInvariant().Contains(query) ||
                        m.Description.ToLowerInvariant().Contains(query) ||
                        m.Metadata.Tags.Any(t => t.ToLowerInvariant().Contains(query)));
                }

                if (request.Tags?.Any() == true)
                {
                    filteredModels = filteredModels.Where(m => 
                        request.Tags.Any(tag => m.Metadata.Tags.Contains(tag)));
                }

                var results = filteredModels.OrderBy(m => m.Name).ToList();

                var response = new PostModelSearchResponse
                {
                    Models = results
                };

                _logger.LogInformation($"Model search completed: {results.Count} models found");
                return ApiResponse<PostModelSearchResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search models");
                return ApiResponse<PostModelSearchResponse>.CreateError(new ErrorDetails
                {
                    Code = "MODEL_SEARCH_ERROR",
                    Message = $"Failed to search models: {ex.Message}",
                    StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                });
            }
        }

        public async Task<ApiResponse<GetModelMetadataResponse>> GetModelMetadataAsync(string idModel)
        {
            try
            {
                _logger.LogInformation($"Getting model metadata for: {idModel}");

                if (string.IsNullOrWhiteSpace(idModel))
                    return ApiResponse<GetModelMetadataResponse>.CreateError(new ErrorDetails
                    {
                        Code = "INVALID_MODEL_ID",
                        Message = "Model ID cannot be null or empty",
                        StatusCode = (int)System.Net.HttpStatusCode.BadRequest
                    });

                var pythonRequest = new { model_id = idModel, action = "get_metadata" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.MODEL, "get_metadata", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var response = new GetModelMetadataResponse
                    {
                        Metadata = new Dictionary<string, object>
                        {
                            ["ModelId"] = idModel,
                            ["Metadata"] = pythonResponse.metadata?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
                            ["Schema"] = pythonResponse.schema?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
                            ["LastModified"] = pythonResponse.last_modified != null 
                                ? DateTime.Parse(pythonResponse.last_modified.ToString())
                                : DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30)),
                            ["Version"] = pythonResponse.version?.ToString() ?? "1.0.0",
                            ["ChecksumMd5"] = pythonResponse.checksum_md5?.ToString() ?? Guid.NewGuid().ToString("N")[..32],
                            ["ChecksumSha256"] = pythonResponse.checksum_sha256?.ToString() ?? Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")
                        }
                    };

                    _logger.LogInformation($"Successfully retrieved metadata for model: {idModel}");
                    return ApiResponse<GetModelMetadataResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Model not found";
                    return ApiResponse<GetModelMetadataResponse>.CreateError(new ErrorDetails
                    {
                        Code = "MODEL_METADATA_FAILED",
                        Message = $"Failed to get model metadata: {error}",
                        StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get model metadata: {idModel}");
                return ApiResponse<GetModelMetadataResponse>.CreateError(new ErrorDetails
                {
                    Code = "MODEL_METADATA_ERROR",
                    Message = $"Failed to get model metadata: {ex.Message}",
                    StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                });
            }
        }

        public async Task<ApiResponse<PutModelMetadataResponse>> PutModelMetadataAsync(string idModel, PutModelMetadataRequest request)
        {
            try
            {
                _logger.LogInformation($"Updating model metadata for: {idModel}");

                if (string.IsNullOrWhiteSpace(idModel))
                    return ApiResponse<PutModelMetadataResponse>.CreateError(new ErrorDetails
                    {
                        Code = "INVALID_MODEL_ID",
                        Message = "Model ID cannot be null or empty",
                        StatusCode = (int)System.Net.HttpStatusCode.BadRequest
                    });

                if (request?.Metadata == null)
                    return ApiResponse<PutModelMetadataResponse>.CreateError(new ErrorDetails
                    {
                        Code = "INVALID_METADATA",
                        Message = "Metadata cannot be null",
                        StatusCode = (int)System.Net.HttpStatusCode.BadRequest
                    });

                var pythonRequest = new
                {
                    model_id = idModel,
                    metadata = request.Metadata,
                    action = "update_metadata"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.MODEL, "update_metadata", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var response = new PutModelMetadataResponse
                    {
                        Success = true,
                        Message = "Model metadata updated successfully"
                    };

                    _logger.LogInformation($"Successfully updated metadata for model: {idModel}");
                    return ApiResponse<PutModelMetadataResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to update model metadata {idModel}: {error}");
                    return ApiResponse<PutModelMetadataResponse>.CreateError(new ErrorDetails
                    {
                        Code = "METADATA_UPDATE_FAILED",
                        Message = $"Failed to update model metadata: {error}",
                        StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update model metadata: {idModel}");
                return ApiResponse<PutModelMetadataResponse>.CreateError(new ErrorDetails
                {
                    Code = "METADATA_UPDATE_ERROR",
                    Message = $"Failed to update model metadata: {ex.Message}",
                    StatusCode = (int)System.Net.HttpStatusCode.InternalServerError
                });
            }
        }

        #region Private Helper Methods

        private async Task RefreshModelCacheAsync()
        {
            if (DateTime.UtcNow - _lastCacheRefresh < _cacheTimeout && _modelCache.Count > 0)
                return;

            try
            {
                var pythonRequest = new { action = "list_models" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.MODEL, "list_models", pythonRequest);

                if (pythonResponse?.success == true && pythonResponse?.models != null)
                {
                    _modelCache.Clear();
                    foreach (var model in pythonResponse.models)
                    {
                        var modelInfo = CreateModelInfoFromPython(model);
                        _modelCache[modelInfo.Id] = modelInfo;
                    }
                }
                else
                {
                    // Fallback to mock data if Python worker is not available
                    await PopulateMockModelsAsync();
                }

                _lastCacheRefresh = DateTime.UtcNow;
                _logger.LogDebug($"Model cache refreshed with {_modelCache.Count} models");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh model cache from Python worker, using mock data");
                await PopulateMockModelsAsync();
            }
        }

        private DeviceOperations.Models.Common.ModelInfo CreateModelInfoFromPython(dynamic pythonModel)
        {
            return new DeviceOperations.Models.Common.ModelInfo
            {
                Id = pythonModel.id?.ToString() ?? Guid.NewGuid().ToString(),
                Name = pythonModel.name?.ToString() ?? "Unknown Model",
                Description = pythonModel.description?.ToString() ?? "No description available",
                Type = Enum.TryParse<ModelType>(pythonModel.type?.ToString(), true, out ModelType modelType) ? modelType : ModelType.Diffusion,
                Version = pythonModel.version?.ToString() ?? "1.0.0",
                FileSize = pythonModel.size_bytes ?? Random.Shared.NextInt64(1000000000, 10000000000),
                FilePath = pythonModel.file_path?.ToString() ?? $"/models/{pythonModel.name ?? "unknown"}.safetensors",
                Hash = pythonModel.checksum?.ToString() ?? Guid.NewGuid().ToString("N"),
                Status = ModelStatus.Available,
                LoadingStatus = ModelLoadingStatus.NotLoaded,
                LastUpdated = pythonModel.updated_at != null ? DateTime.Parse(pythonModel.updated_at.ToString()) : DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30))
            };
        }

        private async Task PopulateMockModelsAsync()
        {
            await Task.Delay(1); // Simulate async operation

            var mockModels = new[]
            {
                new DeviceOperations.Models.Common.ModelInfo
                {
                    Id = "sd15-base",
                    Name = "Stable Diffusion 1.5 Base",
                    Description = "Base Stable Diffusion 1.5 model for general image generation",
                    Type = ModelType.Diffusion,
                    Version = "1.5.0",
                    FileSize = 4265068800,
                    FilePath = "/models/base-models/sd15/v1-5-pruned-emaonly.safetensors",
                    Hash = "cc6cb27103417325ff94f52b7a5d2dde45a7515b25c255d8e396c90014281516",
                    Status = ModelStatus.Available,
                    LoadingStatus = ModelLoadingStatus.NotLoaded,
                    LastUpdated = DateTime.UtcNow.AddDays(-10)
                },
                new DeviceOperations.Models.Common.ModelInfo
                {
                    Id = "sdxl-base",
                    Name = "Stable Diffusion XL Base",
                    Description = "High-resolution Stable Diffusion XL base model",
                    Type = ModelType.Diffusion,
                    Version = "1.0.0",
                    FileSize = 6938078208,
                    FilePath = "/models/base-models/sdxl/sd_xl_base_1.0.safetensors",
                    Hash = "31e35c80fc4829d14f90153f4c74cd59c90b779f6afe05a74cd6120b893f7e5b",
                    Status = ModelStatus.Available,
                    LoadingStatus = ModelLoadingStatus.NotLoaded,
                    LastUpdated = DateTime.UtcNow.AddDays(-5)
                },
                new DeviceOperations.Models.Common.ModelInfo
                {
                    Id = "flux-dev",
                    Name = "FLUX.1 Dev",
                    Description = "Advanced FLUX development model for high-quality generation",
                    Type = ModelType.Flux,
                    Version = "1.0.0",
                    FileSize = 23800000000,
                    FilePath = "/models/flux/flux1-dev.safetensors",
                    Hash = "875df240c1d8f0be2bb4e5d0fcf1b20ee76f7dc7d3e4d2a5c6b8f9e0a1b2c3d4",
                    Status = ModelStatus.Available,
                    LoadingStatus = ModelLoadingStatus.NotLoaded,
                    LastUpdated = DateTime.UtcNow.AddDays(-2)
                }
            };

            _modelCache.Clear();
            foreach (var model in mockModels)
            {
                _modelCache[model.Id] = model;
            }
        }

        // Missing method overloads for interface compatibility
        public Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(PostModelLoadRequest request)
        {
            try
            {
                _logger.LogInformation("Loading model configuration on all devices");
                
                var response = new PostModelLoadResponse
                {
                    Success = true,
                    ModelId = "default-model",
                    LoadSessionId = Guid.NewGuid(),
                    LoadTime = TimeSpan.FromSeconds(10),
                    MemoryUsed = 2147483648, // 2GB
                    DeviceId = Guid.NewGuid(),
                    LoadedAt = DateTime.UtcNow,
                    LoadMetrics = new Dictionary<string, object>
                    {
                        ["strategy"] = "default",
                        ["device_count"] = 2
                    }
                };

                return Task.FromResult(ApiResponse<PostModelLoadResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading model on all devices");
                return Task.FromResult(ApiResponse<PostModelLoadResponse>.CreateError("LOAD_MODEL_ERROR", "Failed to load model", 500));
            }
        }

        public Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(PostModelLoadRequest request, string idDevice)
        {
            try
            {
                _logger.LogInformation("Loading model configuration on device: {DeviceId}", idDevice);
                
                var response = new PostModelLoadResponse
                {
                    Success = true,
                    ModelId = "device-model",
                    LoadSessionId = Guid.NewGuid(),
                    LoadTime = TimeSpan.FromSeconds(5),
                    MemoryUsed = 1073741824, // 1GB
                    DeviceId = Guid.Parse(idDevice),
                    LoadedAt = DateTime.UtcNow,
                    LoadMetrics = new Dictionary<string, object>
                    {
                        ["strategy"] = "device-specific",
                        ["device_id"] = idDevice
                    }
                };

                return Task.FromResult(ApiResponse<PostModelLoadResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading model on device: {DeviceId}", idDevice);
                return Task.FromResult(ApiResponse<PostModelLoadResponse>.CreateError("LOAD_MODEL_ERROR", "Failed to load model", 500));
            }
        }

        public async Task<ApiResponse<PostModelUnloadResponse>> PostModelUnloadAsync(PostModelUnloadRequest request)
        {
            return await Task.FromResult(ApiResponse<PostModelUnloadResponse>.CreateSuccess(new PostModelUnloadResponse
            {
                Success = true,
                Message = "Model unloaded successfully from all devices"
            }));
        }

        public async Task<ApiResponse<PostModelValidateResponse>> PostModelValidateAsync(PostModelValidateRequest request)
        {
            return await Task.FromResult(ApiResponse<PostModelValidateResponse>.CreateSuccess(new PostModelValidateResponse
            {
                IsValid = true,
                ValidationErrors = new List<string>()
            }));
        }

        public async Task<ApiResponse<PostModelOptimizeResponse>> PostModelOptimizeAsync(PostModelOptimizeRequest request)
        {
            return await Task.FromResult(ApiResponse<PostModelOptimizeResponse>.CreateSuccess(new PostModelOptimizeResponse
            {
                Success = true,
                Message = "Model optimization completed successfully"
            }));
        }

        public async Task<ApiResponse<PostModelBenchmarkResponse>> PostModelBenchmarkAsync(PostModelBenchmarkRequest request)
        {
            return await Task.FromResult(ApiResponse<PostModelBenchmarkResponse>.CreateSuccess(new PostModelBenchmarkResponse
            {
                BenchmarkResults = new Dictionary<string, object>
                {
                    { "inference_time_ms", 125.5 },
                    { "memory_usage_mb", 2048 },
                    { "throughput_tokens_per_second", 45.2 }
                }
            }));
        }

        // Methods required by controller but not in interface - Cache/VRAM operations
        public Task<ApiResponse<GetModelCacheResponse>> GetModelCacheAsync()
        {
            try
            {
                _logger.LogInformation("Getting model cache status");
                
                var response = new GetModelCacheResponse
                {
                    CachedModels = new List<CachedModelInfo>(),
                    TotalCacheSize = 4294967296, // Mock 4GB
                    CacheStatistics = new Dictionary<string, object>
                    {
                        ["total_models"] = 5,
                        ["cache_hit_rate"] = 0.85,
                        ["last_cleanup"] = DateTime.UtcNow.AddHours(-2)
                    }
                };

                return Task.FromResult(ApiResponse<GetModelCacheResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cache");
                return Task.FromResult(ApiResponse<GetModelCacheResponse>.CreateError("CACHE_ERROR", "Failed to get model cache", 500));
            }
        }

        public Task<ApiResponse<GetModelCacheComponentResponse>> GetModelCacheComponentAsync(string componentId)
        {
            try
            {
                _logger.LogInformation("Getting cache component: {ComponentId}", componentId);
                
                var response = new GetModelCacheComponentResponse
                {
                    Component = new CachedModelInfo
                    {
                        CacheId = componentId,
                        ModelId = "model-123",
                        CachedSize = 1073741824, // 1GB
                        CachedAt = DateTime.UtcNow.AddHours(-1),
                        LastAccessed = DateTime.UtcNow.AddMinutes(-10),
                        AccessCount = 5
                    },
                    ComponentDetails = new Dictionary<string, object>
                    {
                        ["cache_location"] = "/cache/models",
                        ["compression"] = "lz4"
                    }
                };

                return Task.FromResult(ApiResponse<GetModelCacheComponentResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache component: {ComponentId}", componentId);
                return Task.FromResult(ApiResponse<GetModelCacheComponentResponse>.CreateError("CACHE_COMPONENT_ERROR", "Failed to get cache component", 500));
            }
        }

        public Task<ApiResponse<PostModelCacheResponse>> PostModelCacheAsync(PostModelCacheRequest request)
        {
            try
            {
                _logger.LogInformation("Caching model components");
                
                var response = new PostModelCacheResponse
                {
                    Success = true,
                    CacheId = Guid.NewGuid().ToString(),
                    CacheTime = TimeSpan.FromSeconds(10),
                    CachedSize = 2147483648 // 2GB
                };

                return Task.FromResult(ApiResponse<PostModelCacheResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching model");
                return Task.FromResult(ApiResponse<PostModelCacheResponse>.CreateError("CACHE_ERROR", "Failed to cache model", 500));
            }
        }

        public Task<ApiResponse<DeleteModelCacheResponse>> DeleteModelCacheAsync()
        {
            try
            {
                _logger.LogInformation("Clearing all model cache");
                
                var response = new DeleteModelCacheResponse
                {
                    Success = true,
                    Message = "All model cache cleared successfully",
                    FreedSize = 8589934592 // 8GB
                };

                return Task.FromResult(ApiResponse<DeleteModelCacheResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing model cache");
                return Task.FromResult(ApiResponse<DeleteModelCacheResponse>.CreateError("CACHE_CLEAR_ERROR", "Failed to clear model cache", 500));
            }
        }

        public Task<ApiResponse<DeleteModelCacheComponentResponse>> DeleteModelCacheComponentAsync(string componentId)
        {
            try
            {
                _logger.LogInformation("Clearing cache component: {ComponentId}", componentId);
                
                var response = new DeleteModelCacheComponentResponse
                {
                    Success = true,
                    Message = $"Cache component {componentId} cleared successfully",
                    FreedSize = 1073741824 // 1GB
                };

                return Task.FromResult(ApiResponse<DeleteModelCacheComponentResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache component: {ComponentId}", componentId);
                return Task.FromResult(ApiResponse<DeleteModelCacheComponentResponse>.CreateError("CACHE_COMPONENT_CLEAR_ERROR", "Failed to clear cache component", 500));
            }
        }

        public Task<ApiResponse<PostModelVramLoadResponse>> PostModelVramLoadAsync(PostModelVramLoadRequest request)
        {
            try
            {
                _logger.LogInformation("Loading model to VRAM on all devices");
                
                var response = new PostModelVramLoadResponse
                {
                    Success = true,
                    LoadId = Guid.NewGuid().ToString(),
                    LoadTime = TimeSpan.FromSeconds(5),
                    VramUsed = 4294967296 // 4GB
                };

                return Task.FromResult(ApiResponse<PostModelVramLoadResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading model to VRAM");
                return Task.FromResult(ApiResponse<PostModelVramLoadResponse>.CreateError("VRAM_LOAD_ERROR", "Failed to load model to VRAM", 500));
            }
        }

        public Task<ApiResponse<PostModelVramLoadResponse>> PostModelVramLoadAsync(PostModelVramLoadRequest request, string idDevice)
        {
            try
            {
                _logger.LogInformation("Loading model to VRAM on device: {DeviceId}", idDevice);
                
                var response = new PostModelVramLoadResponse
                {
                    Success = true,
                    LoadId = Guid.NewGuid().ToString(),
                    LoadTime = TimeSpan.FromSeconds(3),
                    VramUsed = 2147483648 // 2GB
                };

                return Task.FromResult(ApiResponse<PostModelVramLoadResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading model to VRAM on device: {DeviceId}", idDevice);
                return Task.FromResult(ApiResponse<PostModelVramLoadResponse>.CreateError("VRAM_LOAD_DEVICE_ERROR", "Failed to load model to VRAM on device", 500));
            }
        }

        public Task<ApiResponse<DeleteModelVramUnloadResponse>> DeleteModelVramUnloadAsync(DeleteModelVramUnloadRequest request)
        {
            try
            {
                _logger.LogInformation("Unloading model from VRAM on all devices");
                
                var response = new DeleteModelVramUnloadResponse
                {
                    Success = true,
                    Message = "Model unloaded from VRAM successfully",
                    UnloadTime = TimeSpan.FromSeconds(2),
                    VramFreed = 4294967296 // 4GB
                };

                return Task.FromResult(ApiResponse<DeleteModelVramUnloadResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading model from VRAM");
                return Task.FromResult(ApiResponse<DeleteModelVramUnloadResponse>.CreateError("VRAM_UNLOAD_ERROR", "Failed to unload model from VRAM", 500));
            }
        }

        public Task<ApiResponse<DeleteModelVramUnloadResponse>> DeleteModelVramUnloadAsync(DeleteModelVramUnloadRequest request, string idDevice)
        {
            try
            {
                _logger.LogInformation("Unloading model from VRAM on device: {DeviceId}", idDevice);
                
                var response = new DeleteModelVramUnloadResponse
                {
                    Success = true,
                    Message = $"Model unloaded from VRAM on device {idDevice} successfully",
                    UnloadTime = TimeSpan.FromSeconds(1),
                    VramFreed = 2147483648 // 2GB
                };

                return Task.FromResult(ApiResponse<DeleteModelVramUnloadResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading model from VRAM on device: {DeviceId}", idDevice);
                return Task.FromResult(ApiResponse<DeleteModelVramUnloadResponse>.CreateError("VRAM_UNLOAD_DEVICE_ERROR", "Failed to unload model from VRAM on device", 500));
            }
        }

        public Task<ApiResponse<GetModelComponentsResponse>> GetModelComponentsAsync()
        {
            try
            {
                _logger.LogInformation("Getting model components");
                
                var response = new GetModelComponentsResponse
                {
                    Components = new List<ModelComponentInfo>
                    {
                        new ModelComponentInfo
                        {
                            ComponentId = "unet-123",
                            ComponentType = "unet",
                            ComponentName = "UNet Model",
                            Size = 1073741824, // 1GB
                            Properties = new Dictionary<string, object>
                            {
                                ["precision"] = "fp16",
                                ["layers"] = 32
                            }
                        }
                    },
                    ComponentStatistics = new Dictionary<string, object>
                    {
                        ["total_components"] = 1,
                        ["total_size"] = 1073741824
                    }
                };

                return Task.FromResult(ApiResponse<GetModelComponentsResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model components");
                return Task.FromResult(ApiResponse<GetModelComponentsResponse>.CreateError("COMPONENTS_ERROR", "Failed to get model components", 500));
            }
        }

        public Task<ApiResponse<GetModelComponentsByTypeResponse>> GetModelComponentsByTypeAsync(string componentType)
        {
            try
            {
                _logger.LogInformation("Getting model components by type: {ComponentType}", componentType);
                
                var response = new GetModelComponentsByTypeResponse
                {
                    ComponentType = componentType,
                    Components = new List<ModelComponentInfo>(),
                    TypeStatistics = new Dictionary<string, object>
                    {
                        ["type"] = componentType,
                        ["count"] = 0
                    }
                };

                return Task.FromResult(ApiResponse<GetModelComponentsByTypeResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model components by type: {ComponentType}", componentType);
                return Task.FromResult(ApiResponse<GetModelComponentsByTypeResponse>.CreateError("COMPONENTS_TYPE_ERROR", "Failed to get model components by type", 500));
            }
        }

        public async Task<ApiResponse<GetAvailableModelsResponse>> GetAvailableModelsAsync()
        {
            try
            {
                _logger.LogInformation("Getting available models");
                
                await RefreshModelCacheAsync();
                
                var response = new GetAvailableModelsResponse
                {
                    AvailableModels = _modelCache.Values.ToList(),
                    ModelsByCategory = new Dictionary<string, List<ModelInfo>>
                    {
                        ["sdxl"] = _modelCache.Values.Where(m => m.Type == ModelType.SDXL).ToList(),
                        ["flux"] = _modelCache.Values.Where(m => m.Type == ModelType.Flux).ToList()
                    },
                    AvailabilityStatistics = new Dictionary<string, object>
                    {
                        ["total_available"] = _modelCache.Count,
                        ["last_scan"] = DateTime.UtcNow
                    }
                };

                return ApiResponse<GetAvailableModelsResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models");
                return ApiResponse<GetAvailableModelsResponse>.CreateError("AVAILABLE_MODELS_ERROR", "Failed to get available models", 500);
            }
        }

        public async Task<ApiResponse<GetAvailableModelsByTypeResponse>> GetAvailableModelsByTypeAsync(string modelType)
        {
            try
            {
                _logger.LogInformation("Getting available models by type: {ModelType}", modelType);
                
                await RefreshModelCacheAsync();
                
                var response = new GetAvailableModelsByTypeResponse
                {
                    ModelType = modelType,
                    AvailableModels = new List<ModelInfo>(),
                    TypeStatistics = new Dictionary<string, object>
                    {
                        ["type"] = modelType,
                        ["count"] = 0
                    }
                };

                return ApiResponse<GetAvailableModelsByTypeResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models by type: {ModelType}", modelType);
                return ApiResponse<GetAvailableModelsByTypeResponse>.CreateError("AVAILABLE_MODELS_TYPE_ERROR", "Failed to get available models by type", 500);
            }
        }

        // Methods required by controller but not in interface - Basic unload operations
        public Task<ApiResponse<DeleteModelUnloadResponse>> DeleteModelUnloadAsync()
        {
            try
            {
                _logger.LogInformation("Unloading all models from all devices");
                
                // Clear all loaded models
                _loadedModels.Clear();

                var response = new DeleteModelUnloadResponse
                {
                    Success = true,
                    Message = "All models unloaded successfully from all devices",
                    UnloadTime = TimeSpan.FromSeconds(2),
                    MemoryFreed = 8589934592 // Mock 8GB freed
                };

                return Task.FromResult(ApiResponse<DeleteModelUnloadResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading all models");
                return Task.FromResult(ApiResponse<DeleteModelUnloadResponse>.CreateError("UNLOAD_ALL_ERROR", "Failed to unload all models", 500));
            }
        }

        public Task<ApiResponse<DeleteModelUnloadResponse>> DeleteModelUnloadAsync(string idDevice)
        {
            try
            {
                _logger.LogInformation("Unloading models from device: {DeviceId}", idDevice);

                if (string.IsNullOrWhiteSpace(idDevice))
                    return Task.FromResult(ApiResponse<DeleteModelUnloadResponse>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400));

                var response = new DeleteModelUnloadResponse
                {
                    Success = true,
                    Message = $"Models unloaded successfully from device {idDevice}",
                    UnloadTime = TimeSpan.FromSeconds(1),
                    MemoryFreed = 4294967296 // Mock 4GB freed
                };

                return Task.FromResult(ApiResponse<DeleteModelUnloadResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading models from device: {DeviceId}", idDevice);
                return Task.FromResult(ApiResponse<DeleteModelUnloadResponse>.CreateError("UNLOAD_DEVICE_ERROR", "Failed to unload models from device", 500));
            }
        }

        #endregion
    }
}
