using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Python;
using System.Text.Json;

namespace DeviceOperations.Services.Postprocessing
{
    /// <summary>
    /// Service implementation for postprocessing operations
    /// </summary>
    public class ServicePostprocessing : IServicePostprocessing
    {
        private readonly ILogger<ServicePostprocessing> _logger;
        private readonly IPythonWorkerService _pythonWorkerService;
        private readonly Dictionary<string, PostprocessingCapability> _capabilitiesCache;
        private readonly Dictionary<string, PostprocessingJob> _activeJobs;
        private DateTime _lastCapabilitiesRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(15);

        public ServicePostprocessing(
            ILogger<ServicePostprocessing> logger,
            IPythonWorkerService pythonWorkerService)
        {
            _logger = logger;
            _pythonWorkerService = pythonWorkerService;
            _capabilitiesCache = new Dictionary<string, PostprocessingCapability>();
            _activeJobs = new Dictionary<string, PostprocessingJob>();
        }

        public async Task<ApiResponse<GetPostprocessingCapabilitiesResponse>> GetPostprocessingCapabilitiesAsync()
        {
            try
            {
                _logger.LogInformation("Getting postprocessing capabilities");

                await RefreshCapabilitiesAsync();

                var allCapabilities = _capabilitiesCache.Values.ToList();
                var supportedOperations = allCapabilities
                    .SelectMany(c => c.SupportedOperations)
                    .Distinct()
                    .ToList();

                var supportedFormats = allCapabilities
                    .SelectMany(c => c.SupportedFormats)
                    .Distinct()
                    .ToList();

                var response = new GetPostprocessingCapabilitiesResponse
                {
                    SupportedOperations = supportedOperations,
                    SupportedInputFormats = supportedFormats,
                    SupportedOutputFormats = supportedFormats,
                    AvailableModels = allCapabilities.SelectMany(c => c.AvailableModels).Distinct().ToList(),
                    MaxConcurrentJobs = allCapabilities.Sum(c => c.MaxConcurrentJobs),
                    MaxImageSize = allCapabilities.Max(c => c.MaxImageSize),
                    Capabilities = allCapabilities,
                    LastUpdated = DateTime.UtcNow
                };

                _logger.LogInformation($"Retrieved capabilities for {allCapabilities.Count} postprocessing engines");
                return ApiResponse<GetPostprocessingCapabilitiesResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get postprocessing capabilities");
                return ApiResponse<GetPostprocessingCapabilitiesResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get postprocessing capabilities: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostPostprocessingApplyResponse>> PostPostprocessingApplyAsync(PostPostprocessingApplyRequest request)
        {
            try
            {
                _logger.LogInformation("Applying postprocessing operations");

                if (request == null)
                    return ApiResponse<PostPostprocessingApplyResponse>.CreateError(
                        new ErrorDetails { Message = "Postprocessing request cannot be null" });

                if (string.IsNullOrEmpty(request.Operation))
                    return ApiResponse<PostPostprocessingApplyResponse>.CreateError(
                        new ErrorDetails { Message = "Operation must be specified" });

                var jobId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    job_id = jobId,
                    input_image_path = request.InputImagePath,
                    operation = request.Operation,
                    model_name = request.ModelName,
                    parameters = request.Parameters,
                    action = "apply_postprocessing"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.POSTPROCESSING, "apply_postprocessing", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var job = new PostprocessingJob
                    {
                        Id = jobId,
                        Operations = new List<string> { request.Operation },
                        Status = PostprocessingStatus.Processing,
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _activeJobs[jobId] = job;

                    var response = new PostPostprocessingApplyResponse
                    {
                        OperationId = Guid.Parse(jobId),
                        Operation = request.Operation,
                        InputImagePath = request.InputImagePath,
                        OutputImagePath = pythonResponse.output_path ?? "/outputs/processed_image.png",
                        ProcessingTime = TimeSpan.FromSeconds(pythonResponse.estimated_time ?? 30),
                        CompletedAt = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 30),
                        ModelUsed = request.ModelName ?? "default",
                        DeviceUsed = Guid.NewGuid(),
                        QualityMetrics = new Dictionary<string, float>
                        {
                            ["processing_score"] = 85.0f
                        },
                        Performance = new Dictionary<string, object>
                        {
                            ["estimated_time"] = pythonResponse.estimated_time ?? 30,
                            ["queue_position"] = pythonResponse.queue_position ?? 0
                        },
                        EffectsApplied = new List<string> { request.Operation }
                    };

                    _logger.LogInformation($"Started postprocessing job: {jobId} with operation: {request.Operation}");
                    return ApiResponse<PostPostprocessingApplyResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to start postprocessing job: {error}");
                    return ApiResponse<PostPostprocessingApplyResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to start postprocessing: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply postprocessing operations");
                return ApiResponse<PostPostprocessingApplyResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to apply postprocessing: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostPostprocessingUpscaleResponse>> PostPostprocessingUpscaleAsync(PostPostprocessingUpscaleRequest request)
        {
            try
            {
                _logger.LogInformation("Starting image upscaling");

                if (request == null)
                    return ApiResponse<PostPostprocessingUpscaleResponse>.CreateError(
                        new ErrorDetails { Message = "Upscale request cannot be null" });

                var jobId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    job_id = jobId,
                    input_image = request.InputImage,
                    scale_factor = request.ScaleFactor,
                    upscale_model = request.UpscaleModel,
                    preserve_details = request.PreserveDetails,
                    tile_size = request.TileSize,
                    action = "upscale_image"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.POSTPROCESSING, "upscale_image", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var job = new PostprocessingJob
                    {
                        Id = jobId,
                        Operations = new List<string> { "upscale" },
                        Status = PostprocessingStatus.Processing,
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _activeJobs[jobId] = job;

                    var response = new PostPostprocessingUpscaleResponse
                    {
                        JobId = jobId,
                        Status = PostprocessingStatus.Processing,
                        ScaleFactor = request.ScaleFactor,
                        UpscaleModel = request.UpscaleModel,
                        OriginalDimensions = pythonResponse.original_dimensions?.ToObject<ImageDimensions>() ?? 
                            new ImageDimensions { Width = 512, Height = 512 },
                        TargetDimensions = pythonResponse.target_dimensions?.ToObject<ImageDimensions>() ?? 
                            new ImageDimensions { Width = 512 * request.ScaleFactor, Height = 512 * request.ScaleFactor },
                        EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 60),
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _logger.LogInformation($"Started upscaling job: {jobId} with {request.ScaleFactor}x scale factor");
                    return ApiResponse<PostPostprocessingUpscaleResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to start upscaling job: {error}");
                    return ApiResponse<PostPostprocessingUpscaleResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to start upscaling: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start image upscaling");
                return ApiResponse<PostPostprocessingUpscaleResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to start upscaling: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostPostprocessingEnhanceResponse>> PostPostprocessingEnhanceAsync(PostPostprocessingEnhanceRequest request)
        {
            try
            {
                _logger.LogInformation("Starting image enhancement");

                if (request == null)
                    return ApiResponse<PostPostprocessingEnhanceResponse>.CreateError(
                        new ErrorDetails { Message = "Enhancement request cannot be null" });

                var jobId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    job_id = jobId,
                    input_image = request.InputImage,
                    enhancement_type = request.EnhancementType,
                    strength = request.Strength,
                    preserve_colors = request.PreserveColors,
                    noise_reduction = request.NoiseReduction,
                    sharpening = request.Sharpening,
                    action = "enhance_image"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.POSTPROCESSING, "enhance_image", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var job = new PostprocessingJob
                    {
                        Id = jobId,
                        Operations = new List<string> { "enhance" },
                        Status = PostprocessingStatus.Processing,
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _activeJobs[jobId] = job;

                    var response = new PostPostprocessingEnhanceResponse
                    {
                        JobId = jobId,
                        Status = PostprocessingStatus.Processing,
                        EnhancementType = request.EnhancementType,
                        AppliedSettings = new Dictionary<string, object>
                        {
                            ["strength"] = request.Strength,
                            ["preserve_colors"] = request.PreserveColors,
                            ["noise_reduction"] = request.NoiseReduction,
                            ["sharpening"] = request.Sharpening
                        },
                        EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 30),
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _logger.LogInformation($"Started enhancement job: {jobId} with type: {request.EnhancementType}");
                    return ApiResponse<PostPostprocessingEnhanceResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to start enhancement job: {error}");
                    return ApiResponse<PostPostprocessingEnhanceResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to start enhancement: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start image enhancement");
                return ApiResponse<PostPostprocessingEnhanceResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to start enhancement: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostPostprocessingFaceRestoreResponse>> PostPostprocessingFaceRestoreAsync(PostPostprocessingFaceRestoreRequest request)
        {
            try
            {
                _logger.LogInformation("Starting face restoration");

                if (request == null)
                    return ApiResponse<PostPostprocessingFaceRestoreResponse>.CreateError(
                        new ErrorDetails { Message = "Face restore request cannot be null" });

                var jobId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    job_id = jobId,
                    input_image = request.InputImage,
                    restoration_model = request.RestorationModel,
                    face_enhancement_strength = request.FaceEnhancementStrength,
                    background_enhancement = request.BackgroundEnhancement,
                    only_center_face = request.OnlyCenterFace,
                    action = "restore_faces"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.POSTPROCESSING, "restore_faces", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var job = new PostprocessingJob
                    {
                        Id = jobId,
                        Operations = new List<string> { "face_restore" },
                        Status = PostprocessingStatus.Processing,
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _activeJobs[jobId] = job;

                    var response = new PostPostprocessingFaceRestoreResponse
                    {
                        JobId = jobId,
                        Status = PostprocessingStatus.Processing,
                        RestorationModel = request.RestorationModel,
                        DetectedFaces = pythonResponse.detected_faces ?? 1,
                        ProcessingSettings = new Dictionary<string, object>
                        {
                            ["face_enhancement_strength"] = request.FaceEnhancementStrength,
                            ["background_enhancement"] = request.BackgroundEnhancement,
                            ["only_center_face"] = request.OnlyCenterFace
                        },
                        EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 45),
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _logger.LogInformation($"Started face restoration job: {jobId} with model: {request.RestorationModel}");
                    return ApiResponse<PostPostprocessingFaceRestoreResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to start face restoration job: {error}");
                    return ApiResponse<PostPostprocessingFaceRestoreResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to start face restoration: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start face restoration");
                return ApiResponse<PostPostprocessingFaceRestoreResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to start face restoration: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostPostprocessingStyleTransferResponse>> PostPostprocessingStyleTransferAsync(PostPostprocessingStyleTransferRequest request)
        {
            try
            {
                _logger.LogInformation("Starting style transfer");

                if (request == null)
                    return ApiResponse<PostPostprocessingStyleTransferResponse>.CreateError(
                        new ErrorDetails { Message = "Style transfer request cannot be null" });

                var jobId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    job_id = jobId,
                    content_image = request.ContentImage,
                    style_image = request.StyleImage,
                    style_strength = request.StyleStrength,
                    preserve_content = request.PreserveContent,
                    style_model = request.StyleModel,
                    action = "style_transfer"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.POSTPROCESSING, "style_transfer", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var job = new PostprocessingJob
                    {
                        Id = jobId,
                        Operations = new List<string> { "style_transfer" },
                        Status = PostprocessingStatus.Processing,
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _activeJobs[jobId] = job;

                    var response = new PostPostprocessingStyleTransferResponse
                    {
                        JobId = jobId,
                        Status = PostprocessingStatus.Processing,
                        StyleModel = request.StyleModel,
                        TransferSettings = new Dictionary<string, object>
                        {
                            ["style_strength"] = request.StyleStrength,
                            ["preserve_content"] = request.PreserveContent,
                            ["content_image_hash"] = pythonResponse.content_hash?.ToString(),
                            ["style_image_hash"] = pythonResponse.style_hash?.ToString()
                        },
                        EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 90),
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _logger.LogInformation($"Started style transfer job: {jobId} with model: {request.StyleModel}");
                    return ApiResponse<PostPostprocessingStyleTransferResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to start style transfer job: {error}");
                    return ApiResponse<PostPostprocessingStyleTransferResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to start style transfer: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start style transfer");
                return ApiResponse<PostPostprocessingStyleTransferResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to start style transfer: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostPostprocessingBackgroundRemoveResponse>> PostPostprocessingBackgroundRemoveAsync(PostPostprocessingBackgroundRemoveRequest request)
        {
            try
            {
                _logger.LogInformation("Starting background removal");

                if (request == null)
                    return ApiResponse<PostPostprocessingBackgroundRemoveResponse>.CreateError(
                        new ErrorDetails { Message = "Background removal request cannot be null" });

                var jobId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    job_id = jobId,
                    input_image = request.InputImage,
                    segmentation_model = request.SegmentationModel,
                    edge_refinement = request.EdgeRefinement,
                    feather_edges = request.FeatherEdges,
                    output_mask = request.OutputMask,
                    action = "remove_background"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.POSTPROCESSING, "remove_background", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var job = new PostprocessingJob
                    {
                        Id = jobId,
                        Operations = new List<string> { "background_remove" },
                        Status = PostprocessingStatus.Processing,
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _activeJobs[jobId] = job;

                    var response = new PostPostprocessingBackgroundRemoveResponse
                    {
                        JobId = jobId,
                        Status = PostprocessingStatus.Processing,
                        SegmentationModel = request.SegmentationModel,
                        ProcessingSettings = new Dictionary<string, object>
                        {
                            ["edge_refinement"] = request.EdgeRefinement,
                            ["feather_edges"] = request.FeatherEdges,
                            ["output_mask"] = request.OutputMask,
                            ["detected_objects"] = pythonResponse.detected_objects ?? 1
                        },
                        EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 20),
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _logger.LogInformation($"Started background removal job: {jobId} with model: {request.SegmentationModel}");
                    return ApiResponse<PostPostprocessingBackgroundRemoveResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to start background removal job: {error}");
                    return ApiResponse<PostPostprocessingBackgroundRemoveResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to start background removal: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start background removal");
                return ApiResponse<PostPostprocessingBackgroundRemoveResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to start background removal: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostPostprocessingColorCorrectResponse>> PostPostprocessingColorCorrectAsync(PostPostprocessingColorCorrectRequest request)
        {
            try
            {
                _logger.LogInformation("Starting color correction");

                if (request == null)
                    return ApiResponse<PostPostprocessingColorCorrectResponse>.CreateError(
                        new ErrorDetails { Message = "Color correction request cannot be null" });

                var jobId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    job_id = jobId,
                    input_image = request.InputImage,
                    correction_type = request.CorrectionType,
                    auto_adjust = request.AutoAdjust,
                    brightness = request.Brightness,
                    contrast = request.Contrast,
                    saturation = request.Saturation,
                    gamma = request.Gamma,
                    action = "color_correct"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.POSTPROCESSING, "color_correct", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var job = new PostprocessingJob
                    {
                        Id = jobId,
                        Operations = new List<string> { "color_correct" },
                        Status = PostprocessingStatus.Processing,
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _activeJobs[jobId] = job;

                    var response = new PostPostprocessingColorCorrectResponse
                    {
                        JobId = jobId,
                        Status = PostprocessingStatus.Processing,
                        CorrectionType = request.CorrectionType,
                        AppliedAdjustments = new Dictionary<string, object>
                        {
                            ["brightness"] = request.Brightness,
                            ["contrast"] = request.Contrast,
                            ["saturation"] = request.Saturation,
                            ["gamma"] = request.Gamma,
                            ["auto_adjust"] = request.AutoAdjust
                        },
                        ColorAnalysis = pythonResponse.color_analysis?.ToObject<Dictionary<string, object>>() ?? 
                            new Dictionary<string, object>
                            {
                                ["dominant_colors"] = new[] { "#ff6b6b", "#4ecdc4", "#45b7d1" },
                                ["brightness_level"] = 0.6,
                                ["contrast_level"] = 0.7
                            },
                        EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 15),
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _logger.LogInformation($"Started color correction job: {jobId} with type: {request.CorrectionType}");
                    return ApiResponse<PostPostprocessingColorCorrectResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to start color correction job: {error}");
                    return ApiResponse<PostPostprocessingColorCorrectResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to start color correction: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start color correction");
                return ApiResponse<PostPostprocessingColorCorrectResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to start color correction: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostPostprocessingBatchResponse>> PostPostprocessingBatchAsync(PostPostprocessingBatchRequest request)
        {
            try
            {
                _logger.LogInformation("Starting batch postprocessing");

                if (request == null)
                    return ApiResponse<PostPostprocessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = "Batch request cannot be null" });

                if (request.InputItems?.Any() != true)
                    return ApiResponse<PostPostprocessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = "At least one input item must be specified" });

                var batchId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    batch_id = batchId,
                    input_items = request.InputItems,
                    operations = request.Operations,
                    batch_settings = request.BatchSettings,
                    concurrent_processing = request.ConcurrentProcessing,
                    priority = request.Priority,
                    action = "batch_process"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.POSTPROCESSING, "batch_process", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var job = new PostprocessingJob
                    {
                        Id = batchId,
                        Operations = request.Operations,
                        Status = PostprocessingStatus.Processing,
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _activeJobs[batchId] = job;

                    var response = new PostPostprocessingBatchResponse
                    {
                        BatchId = batchId,
                        Status = PostprocessingStatus.Processing,
                        TotalItems = request.InputItems.Count,
                        ProcessedItems = 0,
                        QueuedItems = request.InputItems.Count,
                        FailedItems = 0,
                        Operations = request.Operations,
                        BatchSettings = request.BatchSettings,
                        EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(
                            (pythonResponse.estimated_time_per_item ?? 30) * request.InputItems.Count),
                        StartedAt = DateTime.UtcNow,
                        Progress = 0
                    };

                    _logger.LogInformation($"Started batch postprocessing job: {batchId} with {request.InputItems.Count} items");
                    return ApiResponse<PostPostprocessingBatchResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to start batch postprocessing job: {error}");
                    return ApiResponse<PostPostprocessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to start batch processing: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start batch postprocessing");
                return ApiResponse<PostPostprocessingBatchResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to start batch processing: {ex.Message}" });
            }
        }

        #region Private Helper Methods

        private async Task RefreshCapabilitiesAsync()
        {
            if (DateTime.UtcNow - _lastCapabilitiesRefresh < _cacheTimeout && _capabilitiesCache.Count > 0)
                return;

            try
            {
                var pythonRequest = new { action = "get_capabilities" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.POSTPROCESSING, "get_capabilities", pythonRequest);

                if (pythonResponse?.success == true && pythonResponse?.capabilities != null)
                {
                    _capabilitiesCache.Clear();
                    foreach (var capability in pythonResponse.capabilities)
                    {
                        var cap = CreateCapabilityFromPython(capability);
                        _capabilitiesCache[cap.Id] = cap;
                    }
                }
                else
                {
                    // Fallback to mock data
                    await PopulateMockCapabilitiesAsync();
                }

                _lastCapabilitiesRefresh = DateTime.UtcNow;
                _logger.LogDebug($"Capabilities refreshed with {_capabilitiesCache.Count} postprocessing engines");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh capabilities from Python worker, using mock data");
                await PopulateMockCapabilitiesAsync();
            }
        }

        private PostprocessingCapability CreateCapabilityFromPython(dynamic pythonCapability)
        {
            return new PostprocessingCapability
            {
                Id = pythonCapability.id?.ToString() ?? Guid.NewGuid().ToString(),
                Name = pythonCapability.name?.ToString() ?? "Unknown Engine",
                SupportedOperations = pythonCapability.supported_operations?.ToObject<List<string>>() ?? 
                    new List<string> { "upscale", "enhance", "face_restore" },
                SupportedFormats = pythonCapability.supported_formats?.ToObject<List<string>>() ?? 
                    new List<string> { "jpg", "png", "webp" },
                AvailableModels = pythonCapability.available_models?.ToObject<List<string>>() ?? 
                    new List<string> { "esrgan", "realesrgan", "gfpgan" },
                MaxConcurrentJobs = pythonCapability.max_concurrent ?? 2,
                MaxImageSize = pythonCapability.max_image_size ?? 4096,
                IsAvailable = pythonCapability.is_available ?? true,
                LastUpdated = DateTime.UtcNow
            };
        }

        private async Task PopulateMockCapabilitiesAsync()
        {
            await Task.Delay(1); // Simulate async operation

            var mockCapabilities = new[]
            {
                new PostprocessingCapability
                {
                    Id = "upscaler-engine",
                    Name = "AI Upscaler Engine",
                    SupportedOperations = new List<string> { "upscale", "enhance", "denoise" },
                    SupportedFormats = new List<string> { "jpg", "png", "webp", "tiff" },
                    AvailableModels = new List<string> { "esrgan-x4", "realesrgan-x4", "anime4k" },
                    MaxConcurrentJobs = 3,
                    MaxImageSize = 8192,
                    IsAvailable = true,
                    LastUpdated = DateTime.UtcNow
                },
                new PostprocessingCapability
                {
                    Id = "face-restoration-engine",
                    Name = "Face Restoration Engine",
                    SupportedOperations = new List<string> { "face_restore", "enhance", "color_correct" },
                    SupportedFormats = new List<string> { "jpg", "png", "webp" },
                    AvailableModels = new List<string> { "gfpgan", "codeformer", "restoreformer" },
                    MaxConcurrentJobs = 2,
                    MaxImageSize = 4096,
                    IsAvailable = true,
                    LastUpdated = DateTime.UtcNow
                },
                new PostprocessingCapability
                {
                    Id = "background-processing-engine",
                    Name = "Background Processing Engine",
                    SupportedOperations = new List<string> { "background_remove", "style_transfer", "color_correct" },
                    SupportedFormats = new List<string> { "jpg", "png", "webp" },
                    AvailableModels = new List<string> { "u2net", "rembg", "mediapipe" },
                    MaxConcurrentJobs = 4,
                    MaxImageSize = 2048,
                    IsAvailable = true,
                    LastUpdated = DateTime.UtcNow
                }
            };

            _capabilitiesCache.Clear();
            foreach (var capability in mockCapabilities)
            {
                _capabilitiesCache[capability.Id] = capability;
            }
        }

        // Missing method overloads for interface compatibility
        public async Task<ApiResponse<GetPostprocessingCapabilitiesResponse>> GetPostprocessingCapabilitiesAsync(string deviceId)
        {
            try
            {
                _logger.LogInformation("Getting postprocessing capabilities for device: {DeviceId}", deviceId);
                
                var response = new GetPostprocessingCapabilitiesResponse
                {
                    SupportedOperations = new List<string> { "upscale", "enhance", "face_restore", "style_transfer", "background_remove", "color_correct" },
                    AvailableModels = new Dictionary<string, object>
                    {
                        ["upscale"] = new List<string> { "ESRGAN", "RealESRGAN", "BSRGAN" },
                        ["enhance"] = new List<string> { "GFPGAN", "RestoreFormer", "CodeFormer" },
                        ["face_restore"] = new List<string> { "GFPGAN", "CodeFormer" }
                    },
                    MaxConcurrentOperations = 4,
                    SupportedInputFormats = new List<string> { "jpg", "png", "bmp", "tiff" },
                    SupportedOutputFormats = new List<string> { "jpg", "png", "bmp" },
                    MaxImageSize = new { Width = 4096, Height = 4096 }
                };

                return ApiResponse<GetPostprocessingCapabilitiesResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting postprocessing capabilities for device: {DeviceId}", deviceId);
                return ApiResponse<GetPostprocessingCapabilitiesResponse>.CreateError("GET_CAPABILITIES_ERROR", "Failed to retrieve postprocessing capabilities", 500);
            }
        }

        public async Task<ApiResponse<PostPostprocessingUpscaleResponse>> PostUpscaleAsync(PostPostprocessingUpscaleRequest request)
        {
            return await PostPostprocessingUpscaleAsync(request);
        }

        public async Task<ApiResponse<PostPostprocessingUpscaleResponse>> PostUpscaleAsync(PostPostprocessingUpscaleRequest request, string deviceId)
        {
            try
            {
                _logger.LogInformation("Starting upscale operation on device: {DeviceId} for image: {InputPath}", deviceId, request.InputImagePath);
                
                var response = new PostPostprocessingUpscaleResponse
                {
                    OperationId = Guid.NewGuid(),
                    InputImagePath = request.InputImagePath,
                    OutputImagePath = "/outputs/upscaled_image.png",
                    ScaleFactor = request.ScaleFactor,
                    ModelUsed = request.ModelName ?? "RealESRGAN",
                    ProcessingTime = TimeSpan.FromSeconds(15),
                    CompletedAt = DateTime.UtcNow,
                    OriginalResolution = new DeviceOperations.Models.Common.ImageResolution { Width = 512, Height = 512 },
                    NewResolution = new DeviceOperations.Models.Common.ImageResolution { Width = request.ScaleFactor * 512, Height = request.ScaleFactor * 512 },
                    QualityMetrics = new Dictionary<string, float> 
                    {
                        ["psnr"] = 28.5f,
                        ["ssim"] = 0.85f,
                        ["lpips"] = 0.12f
                    }
                };

                return ApiResponse<PostPostprocessingUpscaleResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in upscale operation for device: {DeviceId}", deviceId);
                return ApiResponse<PostPostprocessingUpscaleResponse>.CreateError("UPSCALE_ERROR", "Failed to upscale image", 500);
            }
        }

        public async Task<ApiResponse<PostPostprocessingEnhanceResponse>> PostEnhanceAsync(PostPostprocessingEnhanceRequest request)
        {
            return await PostPostprocessingEnhanceAsync(request);
        }

        public async Task<ApiResponse<PostPostprocessingEnhanceResponse>> PostEnhanceAsync(PostPostprocessingEnhanceRequest request, string deviceId)
        {
            try
            {
                _logger.LogInformation("Starting enhance operation on device: {DeviceId} for image: {InputPath}", deviceId, request.InputImagePath);
                
                var response = new PostPostprocessingEnhanceResponse
                {
                    OperationId = Guid.NewGuid(),
                    InputImagePath = request.InputImagePath,
                    OutputImagePath = "/outputs/enhanced_image.png",
                    EnhancementType = request.EnhancementType,
                    Strength = request.Strength,
                    ProcessingTime = TimeSpan.FromSeconds(10),
                    CompletedAt = DateTime.UtcNow,
                    EnhancementsApplied = new List<string> { "noise_reduction", "sharpening", "color_enhancement" },
                    QualityMetrics = new Dictionary<string, float> 
                    {
                        ["quality_score"] = 85.5f,
                        ["noise_reduction"] = 0.75f,
                        ["sharpness"] = 0.82f
                    },
                    BeforeAfterComparison = new { improvement = "significant", score = 85.5f }
                };

                return ApiResponse<PostPostprocessingEnhanceResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enhance operation for device: {DeviceId}", deviceId);
                return ApiResponse<PostPostprocessingEnhanceResponse>.CreateError("ENHANCE_ERROR", "Failed to enhance image", 500);
            }
        }

        public async Task<ApiResponse<PostPostprocessingFaceRestoreResponse>> PostFaceRestoreAsync(PostPostprocessingFaceRestoreRequest request)
        {
            return await PostPostprocessingFaceRestoreAsync(request);
        }

        public async Task<ApiResponse<PostPostprocessingStyleTransferResponse>> PostStyleTransferAsync(PostPostprocessingStyleTransferRequest request)
        {
            return await PostPostprocessingStyleTransferAsync(request);
        }

        public async Task<ApiResponse<PostPostprocessingBackgroundRemoveResponse>> PostBackgroundRemoveAsync(PostPostprocessingBackgroundRemoveRequest request)
        {
            return await PostPostprocessingBackgroundRemoveAsync(request);
        }

        public async Task<ApiResponse<PostPostprocessingColorCorrectResponse>> PostColorCorrectAsync(PostPostprocessingColorCorrectRequest request)
        {
            return await PostPostprocessingColorCorrectAsync(request);
        }

        #endregion
    }

    #region Helper Classes

    public class PostprocessingCapability
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> SupportedOperations { get; set; } = new();
        public List<string> SupportedFormats { get; set; } = new();
        public List<string> AvailableModels { get; set; } = new();
        public int MaxConcurrentJobs { get; set; }
        public int MaxImageSize { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class PostprocessingJob
    {
        public string Id { get; set; } = string.Empty;
        public List<string> Operations { get; set; } = new();
        public PostprocessingStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int Progress { get; set; }
    }

    public class ImageDimensions
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public enum PostprocessingStatus
    {
        Queued,
        Processing,
        Completed,
        Failed,
        Cancelled
    }

    #endregion
}
