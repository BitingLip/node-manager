using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Core;
using DeviceOperations.Services.Memory;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DeviceOperations.Services.Inference;

public class InferenceService : IInferenceService, IDisposable
{
    private readonly IDeviceService _deviceService;
    private readonly IMemoryOperationsService _memoryService;
    private readonly PyTorchDirectMLService _pytorchService;
    private readonly ILogger<InferenceService> _logger;
    private readonly ConcurrentDictionary<string, LoadedModelInfo> _loadedModels = new();
    private readonly ConcurrentDictionary<string, InferenceSession> _activeSessions = new();
    private bool _initialized = false;

    public InferenceService(
        IDeviceService deviceService,
        IMemoryOperationsService memoryService,
        PyTorchDirectMLService pytorchService,
        ILogger<InferenceService> logger)
    {
        _deviceService = deviceService;
        _memoryService = memoryService;
        _pytorchService = pytorchService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing Inference Service...");

        // Ensure dependent services are initialized
        await _deviceService.InitializeAsync();
        await _memoryService.InitializeAsync();

        // Initialize PyTorch DirectML service
        if (!await _pytorchService.InitializeAsync())
        {
            throw new InvalidOperationException("Failed to initialize PyTorch DirectML service");
        }

        _initialized = true;
        _logger.LogInformation("Inference Service initialized successfully");
    }

    public async Task<LoadModelResponse> LoadModelAsync(LoadModelRequest request)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            // Validate device exists and is available
            var device = await _deviceService.GetDeviceAsync(request.DeviceId);
            if (device == null || !device.IsAvailable)
            {
                return new LoadModelResponse
                {
                    Success = false,
                    Message = $"Device {request.DeviceId} is not available"
                };
            }

            // Check if model file exists
            if (!File.Exists(request.ModelPath))
            {
                return new LoadModelResponse
                {
                    Success = false,
                    Message = $"Model file not found: {request.ModelPath}"
                };
            }

            // Generate model ID if not provided
            var modelId = request.ModelId ?? Guid.NewGuid().ToString("N")[..16];

            // Check if model is already loaded
            if (_loadedModels.ContainsKey(modelId))
            {
                return new LoadModelResponse
                {
                    Success = false,
                    Message = $"Model {modelId} is already loaded"
                };
            }

            // Initialize DirectML on the specified device
            var deviceIdInt = int.TryParse(request.DeviceId, out var parsedId) ? parsedId : 1;
            var initResult = await _pytorchService.InitializeDirectMLAsync(deviceIdInt, request.EnableMultiGpu);
            if (!initResult.Success)
            {
                return new LoadModelResponse
                {
                    Success = false,
                    Message = $"Failed to initialize DirectML: {initResult.Error}"
                };
            }

            // Load model to VRAM using PyTorch DirectML
            var loadResult = await _pytorchService.LoadModelAsync(request.ModelPath, request.ModelType.ToString());
            if (!loadResult.Success)
            {
                return new LoadModelResponse
                {
                    Success = false,
                    Message = $"Failed to load model: {loadResult.Error}"
                };
            }

            // Create model metadata
            var metadata = new ModelMetadata
            {
                ModelId = modelId,
                ModelName = request.ModelName ?? Path.GetFileNameWithoutExtension(request.ModelPath),
                ModelPath = request.ModelPath,
                ModelType = request.ModelType,
                Description = $"PyTorch DirectML model loaded from {request.ModelPath}",
                InputTensors = GenerateMockTensorInfo("input", request.ModelType),
                OutputTensors = GenerateMockTensorInfo("output", request.ModelType)
            };

            // Create loaded model info
            var loadedModel = new LoadedModelInfo
            {
                ModelId = modelId,
                ModelName = metadata.ModelName,
                ModelPath = request.ModelPath,
                DeviceId = request.DeviceId,
                ModelType = request.ModelType,
                ModelSizeBytes = loadResult.Data?.ModelSizeBytes ?? 0,
                LoadedAt = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow,
                ActiveSessions = 0,
                Metadata = metadata
            };

            _loadedModels[modelId] = loadedModel;

            _logger.LogInformation($"Loaded PyTorch DirectML model {modelId} ({metadata.ModelName}) on device {request.DeviceId}, size: {loadedModel.ModelSizeBytes:N0} bytes, load time: {loadResult.Data?.LoadTimeSeconds:F2}s");

            return new LoadModelResponse
            {
                Success = true,
                ModelId = modelId,
                Message = $"Successfully loaded PyTorch DirectML model {metadata.ModelName}",
                ModelSizeBytes = loadedModel.ModelSizeBytes,
                DeviceId = request.DeviceId,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to load model from {request.ModelPath}");
            return new LoadModelResponse
            {
                Success = false,
                Message = $"Model loading failed: {ex.Message}"
            };
        }
    }

    public async Task<InferenceResponse> RunInferenceAsync(InferenceRequest request)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            // Validate model exists
            if (!_loadedModels.TryGetValue(request.ModelId, out var modelInfo))
            {
                return new InferenceResponse
                {
                    Success = false,
                    Message = $"Model {request.ModelId} is not loaded"
                };
            }

            // Generate session ID
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString("N")[..16];

            // Create inference session
            var session = new InferenceSession
            {
                SessionId = sessionId,
                ModelId = request.ModelId,
                DeviceId = modelInfo.DeviceId,
                State = InferenceState.Pending,
                StartedAt = DateTime.UtcNow,
                ProgressPercentage = 0
            };

            _activeSessions[sessionId] = session;
            modelInfo.ActiveSessions++;
            modelInfo.LastUsed = DateTime.UtcNow;

            // Start inference in background
            _ = Task.Run(async () => await ExecuteInferenceAsync(session, request));

            return new InferenceResponse
            {
                Success = true,
                SessionId = sessionId,
                Message = "Inference started successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to start inference for model {request.ModelId}");
            return new InferenceResponse
            {
                Success = false,
                Message = $"Inference failed: {ex.Message}"
            };
        }
    }

    private async Task ExecuteInferenceAsync(InferenceSession session, InferenceRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var statistics = new InferenceStatistics();

        try
        {
            // Preprocessing phase
            session.State = InferenceState.Preprocessing;
            session.ProgressPercentage = 10;
            statistics.PreprocessingTimeMs = 10;

            // Memory cleanup before inference
            var cleanupResult = await _pytorchService.CleanupMemoryAsync();
            if (!cleanupResult.Success)
            {
                _logger.LogWarning($"Memory cleanup warning: {cleanupResult.Error}");
            }

            // Inference phase using PyTorch DirectML
            session.State = InferenceState.Running;
            session.ProgressPercentage = 50;

            // Extract generation parameters from request inputs
            var generateRequest = new GenerateImageRequest
            {
                Prompt = request.Inputs.GetValueOrDefault("prompt", "a beautiful sunset").ToString() ?? "",
                NegativePrompt = request.Inputs.GetValueOrDefault("negative_prompt", "").ToString(),
                Width = int.TryParse(request.Inputs.GetValueOrDefault("width", "512").ToString(), out var w) ? w : 512,
                Height = int.TryParse(request.Inputs.GetValueOrDefault("height", "512").ToString(), out var h) ? h : 512,
                Steps = int.TryParse(request.Inputs.GetValueOrDefault("steps", "20").ToString(), out var s) ? s : 20,
                GuidanceScale = double.TryParse(request.Inputs.GetValueOrDefault("guidance_scale", "7.5").ToString(), out var g) ? g : 7.5,
                Seed = int.TryParse(request.Inputs.GetValueOrDefault("seed", "-1").ToString(), out var seed) && seed != -1 ? seed : Random.Shared.Next()
            };

            // Run PyTorch DirectML inference
            var inferenceResult = await _pytorchService.GenerateImageAsync(generateRequest);
            
            if (!inferenceResult.Success)
            {
                throw new InvalidOperationException($"PyTorch inference failed: {inferenceResult.Error}");
            }

            statistics.InferenceTimeMs = (inferenceResult.Data?.GenerationTimeSeconds ?? 0) * 1000;

            // Postprocessing phase
            session.State = InferenceState.Postprocessing;
            session.ProgressPercentage = 90;
            await Task.Delay(25); // Simulate postprocessing
            statistics.PostprocessingTimeMs = 25;

            // Complete
            session.State = InferenceState.Completed;
            session.ProgressPercentage = 100;
            session.CompletedAt = DateTime.UtcNow;
            
            stopwatch.Stop();
            statistics.TotalTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            statistics.InputTensorCount = request.Inputs.Count;
            statistics.OutputTensorCount = 1; // Generated image
            statistics.MemoryUsageBytes = 1024 * 1024 * 500; // Estimate 500MB

            session.Statistics = statistics;
            session.OutputPath = inferenceResult.Data?.OutputPath;

            _logger.LogInformation($"PyTorch DirectML inference completed for session {session.SessionId}, model {session.ModelId} in {statistics.TotalTimeMs:F2}ms");
        }
        catch (Exception ex)
        {
            session.State = InferenceState.Failed;
            session.ErrorMessage = ex.Message;
            session.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, $"PyTorch DirectML inference failed for session {session.SessionId}");
        }
        finally
        {
            // Update model active sessions count
            if (_loadedModels.TryGetValue(session.ModelId, out var modelInfo))
            {
                modelInfo.ActiveSessions = Math.Max(0, modelInfo.ActiveSessions - 1);
            }

            // Post-inference memory cleanup
            _ = Task.Run(async () =>
            {
                var cleanupResult = await _pytorchService.CleanupMemoryAsync();
                if (!cleanupResult.Success)
                {
                    _logger.LogWarning($"Post-inference memory cleanup warning: {cleanupResult.Error}");
                }
            });
        }
    }

    public async Task<bool> UnloadModelAsync(string modelId)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            if (!_loadedModels.TryRemove(modelId, out var modelInfo))
            {
                return false;
            }

            // Cancel any active sessions for this model
            var sessionsToCancel = _activeSessions.Values
                .Where(s => s.ModelId == modelId && s.State != InferenceState.Completed && s.State != InferenceState.Failed)
                .ToList();

            foreach (var session in sessionsToCancel)
            {
                await CancelInferenceAsync(session.SessionId);
            }

            // Unload model from PyTorch DirectML
            var unloadResult = await _pytorchService.UnloadModelAsync();
            if (!unloadResult.Success)
            {
                _logger.LogWarning($"PyTorch model unload warning: {unloadResult.Error}");
            }

            _logger.LogInformation($"Unloaded PyTorch DirectML model {modelId} ({modelInfo.ModelName})");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to unload model {modelId}");
            return false;
        }
    }

    public async Task<ModelListResponse> GetLoadedModelsAsync()
    {
        if (!_initialized) await InitializeAsync();

        var models = _loadedModels.Values.ToList();
        var totalMemoryUsage = models.Sum(m => m.ModelSizeBytes);

        return new ModelListResponse
        {
            Models = models,
            TotalCount = models.Count,
            TotalMemoryUsageBytes = totalMemoryUsage
        };
    }

    public async Task<LoadedModelInfo?> GetModelInfoAsync(string modelId)
    {
        if (!_initialized) await InitializeAsync();

        _loadedModels.TryGetValue(modelId, out var modelInfo);
        return modelInfo;
    }

    public async Task<InferenceStatusResponse?> GetInferenceStatusAsync(string sessionId)
    {
        if (!_initialized) await InitializeAsync();

        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return null;
        }

        return new InferenceStatusResponse
        {
            SessionId = session.SessionId,
            ModelId = session.ModelId,
            State = session.State,
            ProgressPercentage = session.ProgressPercentage,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt,
            ErrorMessage = session.ErrorMessage,
            Statistics = session.Statistics
        };
    }

    public async Task<bool> CancelInferenceAsync(string sessionId)
    {
        if (!_initialized) await InitializeAsync();

        if (!_activeSessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }

        try
        {
            session.CancellationTokenSource?.Cancel();
            session.State = InferenceState.Cancelled;
            session.CompletedAt = DateTime.UtcNow;
            session.ErrorMessage = "Inference cancelled by user";

            _logger.LogInformation($"Cancelled inference session {sessionId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to cancel inference session {sessionId}");
            return false;
        }
    }

    public async Task<IEnumerable<InferenceSession>> GetActiveSessionsAsync(string? modelId = null)
    {
        if (!_initialized) await InitializeAsync();

        var query = _activeSessions.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(modelId))
        {
            query = query.Where(s => s.ModelId == modelId);
        }

        return query.Where(s => s.State != InferenceState.Completed && s.State != InferenceState.Failed).ToList();
    }

    private List<TensorInfo> GenerateMockTensorInfo(string prefix, ModelType modelType)
    {
        return modelType switch
        {
            ModelType.SDXL => new List<TensorInfo>
            {
                new() { Name = $"{prefix}_ids", DataType = TensorDataType.Int64, Shape = new List<long> { 1, 77 } },
                new() { Name = $"{prefix}_embeddings", DataType = TensorDataType.Float32, Shape = new List<long> { 1, 512 } }
            },
            ModelType.VAE => new List<TensorInfo>
            {
                new() { Name = $"{prefix}_latent", DataType = TensorDataType.Float32, Shape = new List<long> { 1, 4, 64, 64 } }
            },
            _ => new List<TensorInfo>
            {
                new() { Name = $"{prefix}_data", DataType = TensorDataType.Float32, Shape = new List<long> { 1, 3, 224, 224 } }
            }
        };
    }

    private Dictionary<string, object> GenerateMockOutputs(string modelId)
    {
        return new Dictionary<string, object>
        {
            ["output_tensor"] = "Mock output data",
            ["confidence"] = 0.95f,
            ["processing_time_ms"] = 150.0
        };
    }

    private int GetSimulatedInferenceTime(string modelId)
    {
        // Simulate different inference times based on model
        return Random.Shared.Next(100, 500);
    }

    public void Dispose()
    {
        // Cancel all active sessions
        foreach (var session in _activeSessions.Values)
        {
            session.CancellationTokenSource?.Cancel();
            session.CancellationTokenSource?.Dispose();
        }

        _activeSessions.Clear();
        _loadedModels.Clear();
        _initialized = false;

        _logger.LogInformation("Inference Service disposed");
    }

    // Enhanced SDXL support methods
    public async Task<InferenceResponse> RunStructuredInferenceAsync(StructuredPromptRequest request)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            _logger.LogInformation($"Running structured inference for model {request.ModelName}");
            
            // Convert structured request to enhanced SDXL request
            var enhancedRequest = ConvertToEnhancedSDXLRequest(request);
            
            // Run enhanced SDXL inference
            return await RunEnhancedSDXLAsync(enhancedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Structured inference failed");
            return new InferenceResponse
            {
                Success = false,
                Message = $"Structured inference failed: {ex.Message}"
            };
        }
    }

    public async Task<InferenceResponse> RunEnhancedSDXLAsync(EnhancedSDXLRequest request)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            _logger.LogInformation($"Running enhanced SDXL inference with scheduler {request.Scheduler.Type}");
            
            // Create inference request with enhanced format
            var inferenceRequest = new InferenceRequest
            {
                ModelId = request.Model.Base,
                Inputs = new Dictionary<string, object>
                {
                    ["enhanced_sdxl_request"] = request,
                    ["pipeline_type"] = "enhanced_sdxl",
                    ["prompt"] = request.Conditioning.Prompt,
                    ["negative_prompt"] = request.Hyperparameters.NegativePrompt ?? "",
                    ["width"] = request.Hyperparameters.Width,
                    ["height"] = request.Hyperparameters.Height,
                    ["steps"] = request.Scheduler.Steps,
                    ["guidance_scale"] = request.Hyperparameters.GuidanceScale,
                    ["seed"] = request.Hyperparameters.Seed ?? Random.Shared.Next(),
                    ["scheduler"] = request.Scheduler.Type,
                    ["batch_size"] = request.Hyperparameters.BatchSize
                },
                InferenceOptions = new Dictionary<string, object>
                {
                    ["return_images"] = true,
                    ["return_metrics"] = true,
                    ["enhanced_mode"] = true
                }
            };
            
            // Run standard inference with enhanced parameters
            var result = await RunInferenceAsync(inferenceRequest);
            
            // Enhance response with SDXL-specific information
            if (result.Success && result.Outputs != null)
            {
                result.Outputs["enhanced_sdxl_mode"] = true;
                result.Outputs["scheduler_used"] = request.Scheduler.Type;
                result.Outputs["guidance_scale"] = request.Hyperparameters.GuidanceScale;
                
                if (request.Conditioning.Loras?.Any() == true)
                {
                    result.Outputs["loras_applied"] = request.Conditioning.Loras.Select(l => l.Name).ToArray();
                }
                
                if (request.Conditioning.ControlNets?.Any() == true)
                {
                    result.Outputs["controlnets_applied"] = request.Conditioning.ControlNets.Select(c => c.Type).ToArray();
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enhanced SDXL inference failed");
            return new InferenceResponse
            {
                Success = false,
                Message = $"Enhanced SDXL inference failed: {ex.Message}"
            };
        }
    }

    private EnhancedSDXLRequest ConvertToEnhancedSDXLRequest(StructuredPromptRequest request)
    {
        return new EnhancedSDXLRequest
        {
            Model = new ModelConfig
            {
                Base = request.ModelPath ?? $"./models/{request.ModelName}",
                Refiner = string.Empty,
                Vae = string.Empty
            },
            Scheduler = new SchedulerConfig
            {
                Type = request.Scheduler ?? "DPMSolverMultistep",
                Steps = request.Hyperparameters?.NumInferenceSteps ?? 20
            },
            Hyperparameters = new HyperParametersConfig
            {
                GuidanceScale = (float)(request.Hyperparameters?.GuidanceScale ?? 7.5),
                Seed = request.Hyperparameters?.Seed,
                BatchSize = request.Batch?.Size ?? 1,
                Height = request.Dimensions?.Height ?? 1024,
                Width = request.Dimensions?.Width ?? 1024,
                NegativePrompt = request.NegativePrompt ?? string.Empty
            },
            Conditioning = new ConditioningConfig
            {
                Prompt = request.Prompt,
                Loras = request.Lora?.Enabled == true && request.Lora.Models != null
                    ? request.Lora.Models.Select(l => new LoRAConfig { Name = l.Name, Scale = (float)l.Weight }).ToList()
                    : new List<LoRAConfig>(),
                ControlNets = request.Controlnet?.Enabled == true && !string.IsNullOrEmpty(request.Controlnet.Type)
                    ? new List<ControlNetConfig> { new() { Type = request.Controlnet.Type, Weight = (float)request.Controlnet.ConditioningScale } }
                    : new List<ControlNetConfig>()
            },
            Performance = new PerformanceConfig
            {
                Dtype = request.Precision?.Dtype ?? "fp16",
                Xformers = request.Precision?.AttentionSlicing ?? true,
                Device = request.Metadata?.DeviceId ?? string.Empty
            }
        };
    }
}
