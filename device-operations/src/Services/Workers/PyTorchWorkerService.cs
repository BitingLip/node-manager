using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Workers;
using DeviceOperations.Services.Interfaces;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace DeviceOperations.Services.Workers;

/// <summary>
/// Service for managing a single PyTorch DirectML worker process for one GPU
/// </summary>
public class PyTorchWorkerService : IPyTorchWorkerService
{
    private readonly ILogger<PyTorchWorkerService> _logger;
    private readonly IEnhancedRequestTransformer? _requestTransformer;
    private readonly IEnhancedResponseHandler? _responseHandler;
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    
    private Process? _workerProcess;
    private StreamWriter? _workerInput;
    private StreamReader? _workerOutput;
    
    public string GpuId { get; }
    public bool IsInitialized { get; private set; }
    public bool HasModelLoaded { get; private set; }
    public string? CurrentModelId { get; private set; }
    
    private WorkerState _state = WorkerState.Uninitialized;
    private DateTime _lastActivity = DateTime.UtcNow;
    private readonly ConcurrentDictionary<string, object> _activeSessions = new();

    public PyTorchWorkerService(
        string gpuId, 
        ILogger<PyTorchWorkerService> logger,
        IEnhancedRequestTransformer? requestTransformer = null,
        IEnhancedResponseHandler? responseHandler = null)
    {
        GpuId = gpuId;
        _logger = logger;
        _requestTransformer = requestTransformer;
        _responseHandler = responseHandler;
    }

    public async Task<bool> InitializeAsync()
    {
        if (IsInitialized) return true;

        await _initializationSemaphore.WaitAsync();
        try
        {
            if (IsInitialized) return true;

            try
            {
                _state = WorkerState.Initializing;
                _logger.LogInformation($"Initializing worker for GPU {GpuId}");

                // Path to enhanced SDXL worker entry point
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var workerPath = Path.Combine(baseDir, "src", "Workers", "main.py");
                
                if (!File.Exists(workerPath))
                {
                    workerPath = Path.Combine(baseDir, "Workers", "main.py");
                    
                    if (!File.Exists(workerPath))
                    {
                        _logger.LogError($"Enhanced SDXL worker entry point not found at: {workerPath}");
                        _state = WorkerState.Error;
                        return false;
                    }
                }

                // Start Python worker process with enhanced configuration
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{workerPath}\" --worker pipeline_manager --log-level INFO",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(workerPath)
                };

                // Set environment variables for GPU selection
                startInfo.Environment["CUDA_VISIBLE_DEVICES"] = GpuId;
                startInfo.Environment["GPU_ID"] = GpuId;

                _workerProcess = Process.Start(startInfo);
                if (_workerProcess == null)
                {
                    _logger.LogError($"Failed to start worker process for GPU {GpuId}");
                    _state = WorkerState.Error;
                    return false;
                }

                _workerInput = _workerProcess.StandardInput;
                _workerOutput = _workerProcess.StandardOutput;

                // Redirect stderr for logging
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!_workerProcess.HasExited)
                        {
                            var errorLine = await _workerProcess.StandardError.ReadLineAsync();
                            if (!string.IsNullOrEmpty(errorLine))
                            {
                                _logger.LogInformation($"GPU Worker {GpuId}: {errorLine}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error reading worker stderr for {GpuId}: {ex.Message}");
                    }
                });

                // Initialize the worker
                var deviceIndex = ExtractDeviceIndex(GpuId);
                var initResult = await SendCommandAsync<object>(new
                {
                    action = "initialize",
                    device_index = deviceIndex
                });

                if (initResult?.Success == true)
                {
                    IsInitialized = true;
                    _state = WorkerState.Ready;
                    _lastActivity = DateTime.UtcNow;
                    _logger.LogInformation($"GPU worker {GpuId} initialized successfully");
                    return true;
                }
                else
                {
                    _logger.LogError($"Failed to initialize GPU worker {GpuId}: {initResult?.Error}");
                    _state = WorkerState.Error;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to initialize GPU worker {GpuId}");
                _state = WorkerState.Error;
                return false;
            }
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    public async Task<LoadModelResponse> LoadModelAsync(string modelPath, ModelType modelType, string? modelId = null)
    {
        if (!IsInitialized)
        {
            return new LoadModelResponse
            {
                Success = false,
                Message = "Worker not initialized"
            };
        }

        try
        {
            _state = WorkerState.Loading;
            
            var command = new
            {
                action = "load_model",
                model_path = modelPath,
                model_type = modelType.ToString(),
                model_id = modelId
            };

            var result = await SendCommandAsync<LoadModelResult>(command);
            
            if (result?.Success == true && result.Data != null)
            {
                HasModelLoaded = true;
                CurrentModelId = result.Data.ModelId;
                _state = WorkerState.Loaded;
                _lastActivity = DateTime.UtcNow;

                return new LoadModelResponse
                {
                    Success = true,
                    ModelId = result.Data.ModelId,
                    Message = $"Model loaded successfully on {GpuId}",
                    ModelSizeBytes = result.Data.ModelSizeBytes,
                    DeviceId = GpuId
                };
            }
            else
            {
                _state = WorkerState.Error;
                return new LoadModelResponse
                {
                    Success = false,
                    Message = $"Failed to load model: {result?.Error}"
                };
            }
        }
        catch (Exception ex)
        {
            _state = WorkerState.Error;
            _logger.LogError(ex, $"Failed to load model on GPU {GpuId}");
            return new LoadModelResponse
            {
                Success = false,
                Message = $"Model loading failed: {ex.Message}"
            };
        }
    }

    public async Task<LoadModelResponse> LoadModelFromCacheAsync(string modelId)
    {
        // This would integrate with the ModelCacheService
        // For now, return not implemented
        await Task.CompletedTask; // Add await to fix warning
        return new LoadModelResponse
        {
            Success = false,
            Message = "Cache loading not yet implemented"
        };
    }

    public async Task<bool> UnloadModelAsync()
    {
        if (!HasModelLoaded) return true;

        try
        {
            var command = new { action = "unload_model" };
            var result = await SendCommandAsync<object>(command);

            if (result?.Success == true)
            {
                HasModelLoaded = false;
                CurrentModelId = null;
                _state = WorkerState.Ready;
                _lastActivity = DateTime.UtcNow;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to unload model from GPU {GpuId}");
            return false;
        }
    }

    public async Task<InferenceResponse> RunInferenceAsync(InferenceRequest request)
    {
        if (!HasModelLoaded)
        {
            return new InferenceResponse
            {
                Success = false,
                Message = "No model loaded"
            };
        }

        try
        {
            _state = WorkerState.Running;
            
            var sessionId = Guid.NewGuid().ToString("N")[..16];
            _activeSessions[sessionId] = new object();

            var command = new
            {
                action = "generate_image",
                parameters = request.Inputs
            };

            var result = await SendCommandAsync<GenerateImageResult>(command);

            _activeSessions.TryRemove(sessionId, out _);

            if (result?.Success == true && result.Data != null)
            {
                _state = WorkerState.Loaded;
                _lastActivity = DateTime.UtcNow;

                return new InferenceResponse
                {
                    Success = true,
                    SessionId = sessionId,
                    Message = "Inference completed successfully"
                };
            }
            else
            {
                _state = WorkerState.Error;
                return new InferenceResponse
                {
                    Success = false,
                    Message = $"Inference failed: {result?.Error}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Inference failed on GPU {GpuId}");
            return new InferenceResponse
            {
                Success = false,
                Message = $"Inference failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate images using enhanced SDXL pipeline with full control
    /// CRITICAL FIX: Uses enhanced request transformer for protocol compatibility
    /// </summary>
    public async Task<EnhancedSDXLResponse> GenerateEnhancedSDXLAsync(EnhancedSDXLRequest request)
    {
        if (!HasModelLoaded)
        {
            return new EnhancedSDXLResponse
            {
                Success = false,
                Message = "No model loaded",
                Error = "No model loaded on this worker"
            };
        }

        var requestId = Guid.NewGuid().ToString("N")[..16];

        try
        {
            _state = WorkerState.Running;
            _activeSessions[requestId] = new object();

            _logger.LogInformation("Starting enhanced SDXL generation for request {RequestId} on GPU {GpuId}", 
                requestId, GpuId);

            // Use enhanced request transformer if available
            object command;
            if (_requestTransformer != null)
            {
                // CRITICAL FIX: Validate request first
                if (!_requestTransformer.ValidateRequest(request, out var errors))
                {
                    return new EnhancedSDXLResponse
                    {
                        Success = false,
                        Message = "Request validation failed",
                        Error = $"Validation errors: {string.Join(", ", errors)}"
                    };
                }

                // Transform C# request to Python worker format
                // This fixes the protocol mismatch: "action" → "message_type"
                command = _requestTransformer.TransformEnhancedSDXLRequest(request, requestId);
                
                _logger.LogDebug("Using enhanced request transformer for protocol v2 compatibility");
            }
            else
            {
                // Fallback to legacy protocol for backward compatibility
                command = new
                {
                    action = "generate_sdxl_enhanced",  // Legacy protocol
                    request = request,
                    session_id = requestId
                };
                
                _logger.LogWarning("Enhanced request transformer not available, using legacy protocol");
            }

            // Send command to Python worker
            var result = await SendCommandAsync<EnhancedSDXLResult>(command);

            _activeSessions.TryRemove(requestId, out _);

            if (result?.Success == true && result.Data != null)
            {
                _state = WorkerState.Loaded;
                _lastActivity = DateTime.UtcNow;

                // Use enhanced response handler if available
                if (_responseHandler != null && _requestTransformer != null)
                {
                    // For enhanced protocol, we need to handle the response differently
                    // This is a bridge until we implement full Python response handling
                    return CreateEnhancedResponseFromLegacyResult(result.Data, requestId);
                }
                else
                {
                    // Legacy response handling
                    return new EnhancedSDXLResponse
                    {
                        Success = true,
                        Images = ConvertToGeneratedImages(result.Data.Images ?? new List<string>()),
                        Message = "Enhanced SDXL generation completed successfully",
                        Metrics = new GenerationMetrics
                        {
                            GenerationTimeSeconds = result.Data.GenerationTimeSeconds,
                            PreprocessingTimeSeconds = result.Data.PreprocessingTimeSeconds,
                            PostprocessingTimeSeconds = result.Data.PostprocessingTimeSeconds,
                            MemoryUsage = new MemoryUsage 
                            { 
                                GpuMemoryMB = result.Data.MemoryUsedMB 
                            }
                        },
                        FeaturesUsed = ConvertToObjectDictionary(result.Data.FeaturesUsed ?? new Dictionary<string, bool>())
                    };
                }
            }
            else
            {
                _state = WorkerState.Error;
                return new EnhancedSDXLResponse
                {
                    Success = false,
                    Message = $"Enhanced SDXL generation failed: {result?.Error}",
                    Error = result?.Error ?? "Unknown error"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Enhanced SDXL generation failed on GPU {GpuId} for request {requestId}");
            _state = WorkerState.Error;
            _activeSessions.TryRemove(requestId, out _);
            
            return new EnhancedSDXLResponse
            {
                Success = false,
                Message = "Enhanced SDXL generation failed",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Get worker capabilities and supported features
    /// </summary>
    public async Task<WorkerCapabilitiesResponse> GetCapabilitiesAsync()
    {
        try
        {
            var command = new { action = "get_capabilities" };
            var result = await SendCommandAsync<WorkerCapabilitiesResult>(command);
            
            if (result?.Success == true && result.Data != null)
            {
                return new WorkerCapabilitiesResponse
                {
                    Success = true,
                    Features = result.Data.Features ?? new Dictionary<string, bool>(),
                    SupportedSchedulers = result.Data.SupportedSchedulers ?? new List<string>(),
                    SupportedFormats = result.Data.SupportedFormats ?? new List<string>(),
                    MaxResolution = result.Data.MaxResolution,
                    MaxBatchSize = result.Data.MaxBatchSize,
                    MemoryOptimizations = result.Data.MemoryOptimizations ?? new List<string>(),
                    PostprocessingOptions = result.Data.PostprocessingOptions ?? new List<string>()
                };
            }
            else
            {
                return new WorkerCapabilitiesResponse
                {
                    Success = false,
                    Error = result?.Error ?? "Failed to get capabilities"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get capabilities for GPU {GpuId}");
            return new WorkerCapabilitiesResponse
            {
                Success = false,
                Error = $"Failed to get capabilities: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validate an enhanced SDXL request
    /// </summary>
    public async Task<PromptValidationResponse> ValidateRequestAsync(EnhancedSDXLRequest request)
    {
        try
        {
            var command = new
            {
                action = "validate_request",
                request = request
            };

            var result = await SendCommandAsync<PromptValidationResult>(command);
            
            if (result?.Success == true && result.Data != null)
            {
                return new PromptValidationResponse
                {
                    Valid = result.Data.Valid,
                    Error = result.Data.Error ?? string.Empty,
                    Warnings = result.Data.Warnings ?? new List<string>(),
                    ValidationDetails = result.Data.ValidationDetails ?? new Dictionary<string, object>()
                };
            }
            else
            {
                return new PromptValidationResponse
                {
                    Valid = false,
                    Error = result?.Error ?? "Validation failed"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Request validation failed for GPU {GpuId}");
            return new PromptValidationResponse
            {
                Valid = false,
                Error = $"Validation error: {ex.Message}"
            };
        }
    }

    public async Task<WorkerStatusResponse> GetStatusAsync()
    {
        try
        {
            var command = new { action = "get_status" };
            var result = await SendCommandAsync<object>(command);

            return new WorkerStatusResponse
            {
                GpuId = GpuId,
                IsInitialized = IsInitialized,
                HasModelLoaded = HasModelLoaded,
                CurrentModelId = CurrentModelId,
                State = _state,
                ActiveInferenceSessions = _activeSessions.Count,
                LastActivity = _lastActivity
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get status for GPU {GpuId}");
            return new WorkerStatusResponse
            {
                GpuId = GpuId,
                IsInitialized = false,
                State = WorkerState.Error,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<LoadedModelInfo?> GetModelInfoAsync()
    {
        if (!HasModelLoaded) return null;

        try
        {
            var command = new { action = "get_model_info" };
            var result = await SendCommandAsync<object>(command);

            // Convert result to LoadedModelInfo
            // This is a simplified implementation
            return new LoadedModelInfo
            {
                ModelId = CurrentModelId ?? "",
                DeviceId = GpuId,
                LoadedAt = _lastActivity
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get model info for GPU {GpuId}");
            return null;
        }
    }

    public async Task<bool> CleanupMemoryAsync()
    {
        try
        {
            var command = new { action = "cleanup_memory" };
            var result = await SendCommandAsync<object>(command);
            return result?.Success == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Memory cleanup failed for GPU {GpuId}");
            return false;
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var status = await GetStatusAsync();
            return status.IsInitialized && status.State != WorkerState.Error;
        }
        catch
        {
            return false;
        }
    }

    private async Task<PyTorchResponse<T>?> SendCommandAsync<T>(object command, CancellationToken cancellationToken = default)
    {
        if (_workerInput == null || _workerOutput == null || _workerProcess?.HasExited == true)
        {
            return new PyTorchResponse<T>
            {
                Success = false,
                Error = "Worker process not available"
            };
        }

        try
        {
            var commandJson = JsonSerializer.Serialize(command);
            await _workerInput.WriteLineAsync(commandJson);
            await _workerInput.FlushAsync();

            var responseJson = await _workerOutput.ReadLineAsync();
            if (string.IsNullOrEmpty(responseJson))
            {
                return new PyTorchResponse<T>
                {
                    Success = false,
                    Error = "No response from worker"
                };
            }

            var response = JsonSerializer.Deserialize<PyTorchResponse<T>>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to communicate with worker {GpuId}");
            return new PyTorchResponse<T>
            {
                Success = false,
                Error = $"Communication error: {ex.Message}"
            };
        }
    }

    private static int ExtractDeviceIndex(string gpuId)
    {
        // Extract device index from gpuId like "gpu_0" -> 0
        if (gpuId.StartsWith("gpu_") && int.TryParse(gpuId[4..], out var index))
        {
            return index;
        }
        return 0;
    }

    public void Dispose()
    {
        try
        {
            _state = WorkerState.Disposed;
            
            if (_workerProcess != null && !_workerProcess.HasExited)
            {
                _workerInput?.Close();
                _workerOutput?.Close();
                
                if (!_workerProcess.WaitForExit(5000))
                {
                    _workerProcess.Kill();
                }
                
                _workerProcess.Dispose();
            }

            _workerInput?.Dispose();
            _workerOutput?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Error disposing worker {GpuId}");
        }

        _logger.LogInformation($"GPU worker {GpuId} disposed");
    }

    /// <summary>
    /// Bridge method to create enhanced response from legacy result
    /// TODO: Remove once full Python enhanced response handling is implemented
    /// </summary>
    private EnhancedSDXLResponse CreateEnhancedResponseFromLegacyResult(EnhancedSDXLResult data, string requestId)
    {
        return new EnhancedSDXLResponse
        {
            Success = true,
            Images = ConvertToGeneratedImages(data.Images ?? new List<string>()),
            Message = "Enhanced SDXL generation completed successfully",
            Metrics = new GenerationMetrics
            {
                GenerationTimeSeconds = data.GenerationTimeSeconds,
                PreprocessingTimeSeconds = data.PreprocessingTimeSeconds,
                PostprocessingTimeSeconds = data.PostprocessingTimeSeconds,
                MemoryUsage = new MemoryUsage 
                { 
                    GpuMemoryMB = data.MemoryUsedMB 
                }
            },
            FeaturesUsed = ConvertToObjectDictionary(data.FeaturesUsed ?? new Dictionary<string, bool>())
        };
    }

    // Helper methods for type conversion
    private static List<GeneratedImage> ConvertToGeneratedImages(List<string> imagePaths)
    {
        return imagePaths.Select(path => new GeneratedImage
        {
            Path = path,
            Filename = Path.GetFileName(path)
        }).ToList();
    }

    private static Dictionary<string, object> ConvertToObjectDictionary(Dictionary<string, bool> boolDict)
    {
        return boolDict.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
    }
}

// Response models for worker communication
public class PyTorchResponse<T>
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public T? Data { get; set; }
}

public class LoadModelResult
{
    public string? ModelId { get; set; }
    public double LoadTimeSeconds { get; set; }
    public long ModelSizeBytes { get; set; }
    public string? GpuId { get; set; }
}

public class GenerateImageResult
{
    public string? OutputPath { get; set; }
    public double GenerationTimeSeconds { get; set; }
    public int Seed { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? GpuId { get; set; }
}

// Enhanced SDXL result models
public class EnhancedSDXLResult
{
    public List<string>? Images { get; set; }
    public double GenerationTimeSeconds { get; set; }
    public double PreprocessingTimeSeconds { get; set; }
    public double PostprocessingTimeSeconds { get; set; }
    public double MemoryUsedMB { get; set; }
    public int SeedUsed { get; set; }
    public Dictionary<string, bool>? FeaturesUsed { get; set; }
    public string? GpuId { get; set; }
}

public class WorkerCapabilitiesResult
{
    public Dictionary<string, bool>? Features { get; set; }
    public List<string>? SupportedSchedulers { get; set; }
    public List<string>? SupportedFormats { get; set; }
    public int MaxResolution { get; set; }
    public int MaxBatchSize { get; set; }
    public List<string>? MemoryOptimizations { get; set; }
    public List<string>? PostprocessingOptions { get; set; }
}

public class PromptValidationResult
{
    public bool Valid { get; set; }
    public string? Error { get; set; }
    public List<string>? Warnings { get; set; }
    public Dictionary<string, object>? ValidationDetails { get; set; }
}
