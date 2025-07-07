using System.Collections.Concurrent;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Inference;

namespace DeviceOperations.Services.Inference;

/// <summary>
/// Inference service that uses HTTP communication with the workers bridge
/// </summary>
public class HttpInferenceService : IInferenceService, IDisposable
{
    private readonly ILogger<HttpInferenceService> _logger;
    private readonly PyTorchDirectMLHttpService _httpService;
    private readonly ConcurrentDictionary<string, LoadedModelInfo> _loadedModels = new();
    private readonly ConcurrentDictionary<string, InferenceSession> _activeSessions = new();
    private bool _isInitialized = false;

    public HttpInferenceService(ILogger<HttpInferenceService> logger)
    {
        _logger = logger;
        _httpService = new PyTorchDirectMLHttpService(
            new LoggerFactory().CreateLogger<PyTorchDirectMLHttpService>()
        );
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            _logger.LogInformation("Initializing HTTP Inference Service...");

            // Initialize the HTTP service
            if (!await _httpService.InitializeAsync())
            {
                _logger.LogError("Failed to initialize HTTP PyTorch service");
                throw new InvalidOperationException("HTTP PyTorch service initialization failed");
            }

            // Initialize DirectML
            var initResult = await _httpService.InitializeDirectMLAsync(1, true);
            if (!initResult.Success)
            {
                _logger.LogError("Failed to initialize DirectML: {Error}", initResult.Error);
                throw new InvalidOperationException($"DirectML initialization failed: {initResult.Error}");
            }

            _logger.LogInformation("DirectML initialized successfully");

            // Load the default model
            var modelPath = "models/cyberrealisticPony_v125.safetensors";
            var loadRequest = new LoadModelRequest
            {
                ModelPath = modelPath,
                DeviceId = "gpu_0",
                ModelId = "cyberrealisticPony_v125",
                ModelType = ModelType.SDXL
            };

            var loadResult = await LoadModelAsync(loadRequest);
            
            if (loadResult.Success)
            {
                _logger.LogInformation("Default model loaded: {ModelPath}", modelPath);
            }
            else
            {
                _logger.LogWarning("Failed to load default model: {Error}", loadResult.Message);
            }

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize HTTP Inference Service");
            throw;
        }
    }

    public async Task<LoadModelResponse> LoadModelAsync(LoadModelRequest request)
    {
        try
        {
            _logger.LogInformation("Loading model: {ModelPath}", request.ModelPath);

            var result = await _httpService.LoadModelAsync(request.ModelPath, request.ModelType.ToString());
            
            if (result.Success && result.Data != null)
            {
                var modelId = request.ModelId ?? Path.GetFileNameWithoutExtension(request.ModelPath);
                var modelInfo = new LoadedModelInfo
                {
                    ModelId = modelId,
                    ModelPath = request.ModelPath,
                    DeviceId = request.DeviceId,
                    ModelType = request.ModelType,
                    ModelSizeBytes = result.Data.ModelSizeBytes,
                    LoadedAt = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow,
                    ActiveSessions = 0
                };

                _loadedModels[modelId] = modelInfo;
                
                _logger.LogInformation("Model loaded successfully: {ModelId}", modelId);
                
                return new LoadModelResponse
                {
                    Success = true,
                    ModelId = modelId,
                    Message = "Model loaded successfully",
                    ModelSizeBytes = result.Data.ModelSizeBytes,
                    DeviceId = request.DeviceId,
                    LoadedAt = DateTime.UtcNow
                };
            }
            else
            {
                _logger.LogError("Failed to load model: {Error}", result.Error);
                return new LoadModelResponse
                {
                    Success = false,
                    Message = result.Error ?? "Unknown error"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception loading model");
            return new LoadModelResponse
            {
                Success = false,
                Message = $"Exception: {ex.Message}"
            };
        }
    }

    public async Task<InferenceResponse> RunInferenceAsync(InferenceRequest request)
    {
        try
        {
            _logger.LogInformation("Running inference for model: {ModelId}", request.ModelId);

            // Create session
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString("N")[..12];
            var session = new InferenceSession
            {
                SessionId = sessionId,
                ModelId = request.ModelId,
                DeviceId = "gpu_0",
                State = InferenceState.Running,
                StartedAt = DateTime.UtcNow
            };

            _activeSessions[sessionId] = session;

            // Update model usage
            if (_loadedModels.TryGetValue(request.ModelId, out var modelInfo))
            {
                modelInfo.LastUsed = DateTime.UtcNow;
                modelInfo.ActiveSessions++;
            }

            // Extract generation parameters from inputs
            var prompt = request.Inputs.TryGetValue("prompt", out var promptObj) ? promptObj?.ToString() ?? "" : "";
            var negativePrompt = request.Inputs.TryGetValue("negative_prompt", out var negObj) ? negObj?.ToString() : "";
            var width = ExtractInt(request.Inputs, "width", 1024);
            var height = ExtractInt(request.Inputs, "height", 1024);
            var steps = ExtractInt(request.Inputs, "steps", 30);
            var guidanceScale = ExtractDouble(request.Inputs, "guidance_scale", 7.5);
            var seed = ExtractInt(request.Inputs, "seed", Random.Shared.Next());

            // Create the generation request
            var generateRequest = new GenerateImageRequest
            {
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                Width = width,
                Height = height,
                Steps = steps,
                GuidanceScale = guidanceScale,
                Seed = seed
            };

            var result = await _httpService.GenerateImageAsync(generateRequest);

            // Update session
            session.CompletedAt = DateTime.UtcNow;
            session.State = result.Success ? InferenceState.Completed : InferenceState.Failed;
            
            if (modelInfo != null)
            {
                modelInfo.ActiveSessions--;
            }

            if (result.Success && result.Data != null)
            {
                session.OutputPath = result.Data.OutputPath;
                session.Statistics = new InferenceStatistics
                {
                    TotalTimeMs = result.Data.GenerationTimeSeconds * 1000,
                    InferenceTimeMs = result.Data.GenerationTimeSeconds * 1000
                };

                _logger.LogInformation("Inference completed successfully. Output: {OutputPath}", result.Data.OutputPath);

                return new InferenceResponse
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = "Inference completed successfully",
                    Outputs = new Dictionary<string, object>
                    {
                        ["output_path"] = result.Data.OutputPath,
                        ["seed"] = result.Data.Seed,
                        ["width"] = result.Data.Width,
                        ["height"] = result.Data.Height
                    },
                    InferenceTimeMs = result.Data.GenerationTimeSeconds * 1000,
                    Statistics = session.Statistics
                };
            }
            else
            {
                var error = result.Error ?? "Unknown error occurred during inference";
                session.ErrorMessage = error;
                _logger.LogError("Inference failed: {Error}", error);
                
                return new InferenceResponse
                {
                    Success = false,
                    SessionId = sessionId,
                    Message = error
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during inference");
            return new InferenceResponse
            {
                Success = false,
                Message = $"Inference exception: {ex.Message}"
            };
        }
    }

    public async Task<bool> UnloadModelAsync(string modelId)
    {
        try
        {
            var result = await _httpService.UnloadModelAsync();
            
            if (result.Success)
            {
                _loadedModels.TryRemove(modelId, out _);
                _logger.LogInformation("Model unloaded successfully: {ModelId}", modelId);
                return true;
            }
            else
            {
                _logger.LogError("Failed to unload model: {Error}", result.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception unloading model");
            return false;
        }
    }

    public async Task<ModelListResponse> GetLoadedModelsAsync()
    {
        try
        {
            var models = _loadedModels.Values.ToList();
            var totalMemory = models.Sum(m => m.ModelSizeBytes);

            return new ModelListResponse
            {
                Models = models,
                TotalCount = models.Count,
                TotalMemoryUsageBytes = totalMemory
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting loaded models");
            return new ModelListResponse();
        }
    }

    public async Task<LoadedModelInfo?> GetModelInfoAsync(string modelId)
    {
        try
        {
            if (_loadedModels.TryGetValue(modelId, out var modelInfo))
            {
                return modelInfo;
            }

            var result = await _httpService.GetModelInfoAsync();
            
            if (result.Success && result.Data != null)
            {
                // Convert from PyTorch response to our model
                return new LoadedModelInfo
                {
                    ModelId = modelId,
                    ModelPath = result.Data.ModelInfo.TryGetValue("path", out var pathObj) ? pathObj?.ToString() ?? "" : "",
                    DeviceId = "gpu_0",
                    ModelType = ModelType.SDXL,
                    LoadedAt = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting model info");
            return null;
        }
    }

    public async Task<InferenceStatusResponse?> GetInferenceStatusAsync(string sessionId)
    {
        try
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                return new InferenceStatusResponse
                {
                    SessionId = sessionId,
                    ModelId = session.ModelId,
                    State = session.State,
                    ProgressPercentage = session.ProgressPercentage,
                    StartedAt = session.StartedAt,
                    CompletedAt = session.CompletedAt,
                    ErrorMessage = session.ErrorMessage,
                    Statistics = session.Statistics
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting inference status");
            return null;
        }
    }

    public async Task<bool> CancelInferenceAsync(string sessionId)
    {
        try
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                session.State = InferenceState.Cancelled;
                session.CompletedAt = DateTime.UtcNow;
                session.ErrorMessage = "Cancelled by user";
                
                // Cancel the token source if available
                session.CancellationTokenSource?.Cancel();
                
                _logger.LogInformation("Inference cancelled: {SessionId}", sessionId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception cancelling inference");
            return false;
        }
    }

    public async Task<IEnumerable<InferenceSession>> GetActiveSessionsAsync(string? modelId = null)
    {
        try
        {
            var sessions = _activeSessions.Values.AsEnumerable();
            
            if (!string.IsNullOrEmpty(modelId))
            {
                sessions = sessions.Where(s => s.ModelId == modelId);
            }

            return sessions.Where(s => s.State == InferenceState.Running || s.State == InferenceState.Pending).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting active sessions");
            return new List<InferenceSession>();
        }
    }

    public async Task<InferenceResponse> RunStructuredInferenceAsync(StructuredPromptRequest request)
    {
        try
        {
            // Convert structured prompt to standard inference request
            var inferenceRequest = new InferenceRequest
            {
                ModelId = request.ModelName ?? "cyberrealisticPony_v125",
                Inputs = new Dictionary<string, object>
                {
                    ["prompt"] = request.Prompt,
                    ["negative_prompt"] = request.NegativePrompt ?? "",
                    ["width"] = request.Dimensions?.Width ?? 1024,
                    ["height"] = request.Dimensions?.Height ?? 1024,
                    ["steps"] = request.Hyperparameters?.NumInferenceSteps ?? 30,
                    ["guidance_scale"] = request.Hyperparameters?.GuidanceScale ?? 7.5,
                    ["seed"] = request.Hyperparameters?.Seed ?? Random.Shared.Next()
                }
            };

            return await RunInferenceAsync(inferenceRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during structured inference");
            return new InferenceResponse
            {
                Success = false,
                Message = $"Structured inference exception: {ex.Message}"
            };
        }
    }

    public async Task<InferenceResponse> RunEnhancedSDXLAsync(EnhancedSDXLRequest request)
    {
        try
        {
            // Convert enhanced SDXL request to standard inference request
            var inferenceRequest = new InferenceRequest
            {
                ModelId = request.Model?.Base ?? "cyberrealisticPony_v125",
                Inputs = new Dictionary<string, object>
                {
                    ["prompt"] = request.Conditioning?.Prompt ?? "",
                    ["negative_prompt"] = request.Hyperparameters?.NegativePrompt ?? "",
                    ["width"] = request.Hyperparameters?.Width ?? 1024,
                    ["height"] = request.Hyperparameters?.Height ?? 1024,
                    ["steps"] = request.Scheduler?.Steps ?? 30,
                    ["guidance_scale"] = request.Hyperparameters?.GuidanceScale ?? 7.5,
                    ["seed"] = request.Hyperparameters?.Seed ?? Random.Shared.Next()
                }
            };

            return await RunInferenceAsync(inferenceRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during enhanced SDXL inference");
            return new InferenceResponse
            {
                Success = false,
                Message = $"Enhanced SDXL inference exception: {ex.Message}"
            };
        }
    }

    private int ExtractInt(Dictionary<string, object> inputs, string key, int defaultValue)
    {
        if (inputs.TryGetValue(key, out var value))
        {
            if (value is int intValue) return intValue;
            if (int.TryParse(value?.ToString(), out var parsedInt)) return parsedInt;
        }
        return defaultValue;
    }

    private double ExtractDouble(Dictionary<string, object> inputs, string key, double defaultValue)
    {
        if (inputs.TryGetValue(key, out var value))
        {
            if (value is double doubleValue) return doubleValue;
            if (value is float floatValue) return floatValue;
            if (double.TryParse(value?.ToString(), out var parsedDouble)) return parsedDouble;
        }
        return defaultValue;
    }

    public void Dispose()
    {
        try
        {
            _httpService?.Dispose();
            _loadedModels?.Clear();
            _activeSessions?.Clear();
            _logger.LogInformation("HTTP Inference Service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing HTTP Inference Service");
        }

        _isInitialized = false;
    }
}
