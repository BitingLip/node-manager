using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Python;
using System.Text.Json;

namespace DeviceOperations.Services.Inference
{
    /// <summary>
    /// Service implementation for inference operations
    /// </summary>
    public class ServiceInference : IServiceInference
    {
        private readonly ILogger<ServiceInference> _logger;
        private readonly IPythonWorkerService _pythonWorkerService;
        private readonly Dictionary<string, InferenceSession> _activeSessions;
        private readonly Dictionary<string, InferenceCapabilities> _deviceCapabilities;
        private DateTime _lastCapabilitiesRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(10);

        public ServiceInference(
            ILogger<ServiceInference> logger,
            IPythonWorkerService pythonWorkerService)
        {
            _logger = logger;
            _pythonWorkerService = pythonWorkerService;
            _activeSessions = new Dictionary<string, InferenceSession>();
            _deviceCapabilities = new Dictionary<string, InferenceCapabilities>();
        }

        public async Task<ApiResponse<GetInferenceCapabilitiesResponse>> GetInferenceCapabilitiesAsync()
        {
            try
            {
                _logger.LogInformation("Getting overall inference capabilities");

                await RefreshCapabilitiesAsync();

                var allCapabilities = _deviceCapabilities.Values.ToList();
                var supportedTypes = allCapabilities
                    .SelectMany(c => c.SupportedInferenceTypes)
                    .Distinct()
                    .ToList();

                var supportedModels = new List<string> { "SDXL", "SD15", "Flux" }; // Mock supported models

                var response = new GetInferenceCapabilitiesResponse
                {
                    SupportedInferenceTypes = supportedTypes,
                    SupportedModels = new List<ModelInfo>(), // Will be populated from mock data
                    AvailableDevices = new List<DeviceInfo>(), // Will be populated from mock data
                    MaxConcurrentInferences = allCapabilities.Sum(c => c.MaxConcurrentInferences),
                    SupportedPrecisions = new List<string> { "FP32", "FP16", "INT8" },
                    MaxBatchSize = allCapabilities.Any() ? allCapabilities.Max(c => c.MaxBatchSize) : 8,
                    MaxResolution = allCapabilities.Any() ? allCapabilities.First().MaxResolution : new Models.Common.ImageResolution { Width = 2048, Height = 2048 }
                };

                _logger.LogInformation($"Retrieved capabilities for {allCapabilities.Count} devices");
                return ApiResponse<GetInferenceCapabilitiesResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get inference capabilities");
                return ApiResponse<GetInferenceCapabilitiesResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get inference capabilities: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetInferenceCapabilitiesDeviceResponse>> GetInferenceCapabilitiesAsync(string idDevice)
        {
            try
            {
                _logger.LogInformation($"Getting inference capabilities for device: {idDevice}");

                if (string.IsNullOrWhiteSpace(idDevice))
                    return ApiResponse<GetInferenceCapabilitiesDeviceResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" });

                await RefreshCapabilitiesAsync();

                if (!_deviceCapabilities.TryGetValue(idDevice, out var capabilities))
                {
                    // Try to get from Python worker
                    var pythonRequest = new { device_id = idDevice, action = "get_capabilities" };
                    var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                        PythonWorkerTypes.INFERENCE, "get_device_capabilities", pythonRequest);

                    if (pythonResponse?.success == true)
                    {
                        capabilities = CreateCapabilitiesFromPython(pythonResponse.capabilities);
                        _deviceCapabilities[idDevice] = capabilities;
                    }
                    else
                    {
                        return ApiResponse<GetInferenceCapabilitiesDeviceResponse>.CreateError(
                            new ErrorDetails { Message = $"Device '{idDevice}' not found or not available for inference" });
                    }
                }

                var response = new GetInferenceCapabilitiesDeviceResponse
                {
                    DeviceId = Guid.TryParse(idDevice, out var deviceGuid) ? deviceGuid : Guid.NewGuid(),
                    DeviceName = $"Device {idDevice}",
                    SupportedInferenceTypes = capabilities.SupportedInferenceTypes,
                    LoadedModels = new List<ModelInfo>(), // Mock empty list
                    MaxConcurrentInferences = capabilities.MaxConcurrentInferences,
                    SupportedPrecisions = capabilities.SupportedPrecisions,
                    MaxBatchSize = capabilities.MaxBatchSize,
                    MaxResolution = capabilities.MaxResolution,
                    MemoryAvailable = 8589934592, // Mock 8GB
                    ComputeCapability = "8.9", // Mock compute capability
                    OptimalInferenceTypes = new List<string> { "TextGeneration", "ImageGeneration" }
                };

                _logger.LogInformation($"Retrieved capabilities for device: {idDevice}");
                return ApiResponse<GetInferenceCapabilitiesDeviceResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get inference capabilities for device: {idDevice}");
                return ApiResponse<GetInferenceCapabilitiesDeviceResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get device capabilities: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostInferenceExecuteResponse>> PostInferenceExecuteAsync(PostInferenceExecuteRequest request)
        {
            try
            {
                _logger.LogInformation("Executing inference request");

                if (request == null)
                    return ApiResponse<PostInferenceExecuteResponse>.CreateError(
                        new ErrorDetails { Message = "Inference request cannot be null" });

                // Find best available device - simplified approach
                await RefreshCapabilitiesAsync();
                var availableDeviceKvp = _deviceCapabilities.FirstOrDefault();
                
                if (availableDeviceKvp.Key == null)
                {
                    return ApiResponse<PostInferenceExecuteResponse>.CreateError(
                        new ErrorDetails { Message = "No available device supports the requested model" });
                }
                
                var deviceId = availableDeviceKvp.Key;
                var availableDevice = availableDeviceKvp.Value;

                if (availableDevice == null)
                {
                    return ApiResponse<PostInferenceExecuteResponse>.CreateError(
                        new ErrorDetails { Message = "No available device supports the requested model" });
                }

                var sessionId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    session_id = sessionId,
                    model_id = request.ModelId,
                    device_id = deviceId,
                    prompt = request.Parameters.TryGetValue("prompt", out var promptValue) ? promptValue?.ToString() : "",
                    parameters = request.Parameters,
                    inference_type = request.InferenceType.ToString(),
                    action = "execute_inference"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "execute_inference", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var session = new InferenceSession
                    {
                        Id = sessionId,
                        ModelId = request.ModelId,
                        DeviceId = deviceId,
                        Status = (SessionStatus)InferenceStatus.Running,
                        StartedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    _activeSessions[sessionId] = session;

                    var response = new PostInferenceExecuteResponse
                    {
                        Results = pythonResponse.results?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
                        Success = true,
                        InferenceId = Guid.Parse(sessionId),
                        ModelId = request.ModelId,
                        DeviceId = deviceId,
                        InferenceType = request.InferenceType,
                        Status = InferenceStatus.Running,
                        ExecutionTime = TimeSpan.FromSeconds(pythonResponse.estimated_time ?? 30),
                        CompletedAt = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 30),
                        Performance = new Dictionary<string, object>
                        {
                            ["estimated_time"] = pythonResponse.estimated_time ?? 30,
                            ["queue_position"] = pythonResponse.queue_position ?? 0
                        },
                        QualityMetrics = new Dictionary<string, object>
                        {
                            ["confidence"] = 0.85f,
                            ["processing_speed"] = 1.2f
                        }
                    };

                    _logger.LogInformation($"Started inference session: {sessionId} on device: {deviceId}");
                    return ApiResponse<PostInferenceExecuteResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to execute inference: {error}");
                    return ApiResponse<PostInferenceExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to execute inference: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute inference");
                return ApiResponse<PostInferenceExecuteResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to execute inference: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostInferenceExecuteDeviceResponse>> PostInferenceExecuteAsync(PostInferenceExecuteDeviceRequest request, string idDevice)
        {
            try
            {
                _logger.LogInformation($"Executing inference request on specific device: {idDevice}");

                if (request == null)
                    return ApiResponse<PostInferenceExecuteDeviceResponse>.CreateError(
                        new ErrorDetails { Message = "Inference request cannot be null" });

                if (string.IsNullOrWhiteSpace(idDevice))
                    return ApiResponse<PostInferenceExecuteDeviceResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" });

                // Check device availability
                await RefreshCapabilitiesAsync();
                if (!_deviceCapabilities.TryGetValue(idDevice, out var capabilities))
                {
                    return ApiResponse<PostInferenceExecuteDeviceResponse>.CreateError(
                        new ErrorDetails { Message = $"Device '{idDevice}' is not available for inference" });
                }

                // Mock: All devices support all models (for now)
                // TODO: Implement actual model support checking based on device capabilities
                var supportedModels = new List<string> { "all", request.ModelId, "mock-model-1", "mock-model-2" };
                if (!supportedModels.Contains(request.ModelId) && !supportedModels.Contains("all"))
                {
                    return ApiResponse<PostInferenceExecuteDeviceResponse>.CreateError(
                        new ErrorDetails { Message = $"Device '{idDevice}' does not support model '{request.ModelId}'" });
                }

                var sessionId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    session_id = sessionId,
                    model_id = request.ModelId,
                    device_id = idDevice,
                    prompt = request.Parameters.TryGetValue("prompt", out var promptValue) ? promptValue?.ToString() : "",
                    parameters = request.Parameters,
                    inference_type = request.InferenceType.ToString(),
                    force_device = true,
                    action = "execute_inference"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "execute_inference", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var session = new InferenceSession
                    {
                        Id = sessionId,
                        ModelId = request.ModelId,
                        DeviceId = idDevice,
                        Status = (SessionStatus)InferenceStatus.Running,
                        StartedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    _activeSessions[sessionId] = session;

                    var response = new PostInferenceExecuteDeviceResponse
                    {
                        Results = pythonResponse.results?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
                        Success = true,
                        InferenceId = Guid.Parse(sessionId),
                        ModelId = request.ModelId,
                        DeviceId = idDevice,
                        InferenceType = request.InferenceType,
                        Status = InferenceStatus.Running,
                        ExecutionTime = TimeSpan.FromSeconds(pythonResponse.estimated_time ?? 30),
                        CompletedAt = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 30),
                        DevicePerformance = new Dictionary<string, object>
                        {
                            ["device_load"] = 0.65f,
                            ["estimated_time"] = pythonResponse.estimated_time ?? 30
                        },
                        QualityMetrics = new Dictionary<string, object>
                        {
                            ["confidence"] = 0.85f,
                            ["processing_speed"] = 1.2f
                        },
                        OptimizationsApplied = pythonResponse.optimizations?.ToObject<List<string>>() ?? new List<string>()
                    };

                    _logger.LogInformation($"Started inference session: {sessionId} on device: {idDevice}");
                    return ApiResponse<PostInferenceExecuteDeviceResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to execute inference on device {idDevice}: {error}");
                    return ApiResponse<PostInferenceExecuteDeviceResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to execute inference: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to execute inference on device: {idDevice}");
                return ApiResponse<PostInferenceExecuteDeviceResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to execute inference: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostInferenceValidateResponse>> PostInferenceValidateAsync(PostInferenceValidateRequest request)
        {
            try
            {
                _logger.LogInformation("Validating inference request");

                if (request == null)
                    return ApiResponse<PostInferenceValidateResponse>.CreateError(
                        new ErrorDetails { Message = "Validation request cannot be null" });

                var pythonRequest = new
                {
                    model_id = request.ModelId,
                    prompt = request.Parameters.TryGetValue("prompt", out var promptValue) ? promptValue?.ToString() : "",
                    parameters = request.Parameters,
                    inference_type = request.InferenceType.ToString(),
                    validation_level = "basic",
                    action = "validate_request"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "validate_request", pythonRequest);

                var isValid = pythonResponse?.success == true && pythonResponse?.is_valid == true;
                var validationErrors = new List<string>();
                var warnings = new List<string>();
                var recommendations = new List<string>();

                if (pythonResponse?.issues != null)
                {
                    foreach (var issue in pythonResponse.issues)
                    {
                        var issueMessage = issue.message?.ToString() ?? "Unknown issue";
                        var severity = issue.severity?.ToString()?.ToLower();
                        
                        if (severity == "error")
                            validationErrors.Add(issueMessage);
                        else if (severity == "warning")
                            warnings.Add(issueMessage);
                        else
                            recommendations.Add(issueMessage);
                    }
                }

                if (pythonResponse?.suggestions != null)
                {
                    foreach (var suggestion in pythonResponse.suggestions)
                    {
                        recommendations.Add(suggestion.ToString());
                    }
                }

                // Find compatible devices - simplified
                await RefreshCapabilitiesAsync();
                var compatibleDevices = _deviceCapabilities.Keys.ToList();

                var response = new PostInferenceValidateResponse
                {
                    IsValid = isValid,
                    ValidationErrors = validationErrors,
                    ValidationTime = TimeSpan.FromMilliseconds(pythonResponse?.validation_time ?? 100),
                    ValidatedAt = DateTime.UtcNow,
                    ValidationResults = new Dictionary<string, object>
                    {
                        ["model_compatibility"] = isValid,
                        ["parameter_validation"] = validationErrors.Count == 0
                    },
                    Issues = validationErrors,
                    Warnings = warnings,
                    Recommendations = recommendations,
                    EstimatedExecutionTime = TimeSpan.FromSeconds(pythonResponse?.estimated_execution_time ?? 30),
                    EstimatedMemoryUsage = pythonResponse?.estimated_memory_usage ?? 2147483648,
                    OptimalDevice = compatibleDevices.FirstOrDefault() ?? "gpu-0",
                    SuggestedOptimizations = recommendations
                };

                _logger.LogInformation($"Validation completed: {(isValid ? "Valid" : "Invalid")} with {validationErrors.Count} errors");
                return ApiResponse<PostInferenceValidateResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate inference request");
                return ApiResponse<PostInferenceValidateResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to validate inference request: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetSupportedTypesResponse>> GetSupportedTypesAsync()
        {
            try
            {
                _logger.LogInformation("Getting supported inference types");

                var pythonRequest = new { action = "get_supported_types" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_supported_types", pythonRequest);

                var supportedTypes = new List<string>();
                var modelSupport = new Dictionary<string, List<string>>();

                if (pythonResponse?.success == true && pythonResponse?.types != null)
                {
                    var types = pythonResponse!.types;
                    if (types != null)
                    {
                        foreach (var type in types)
                        {
                            var typeName = type?.name?.ToString() ?? "Unknown";
                            supportedTypes.Add(typeName);
                        }
                    }

                    if (pythonResponse?.model_support != null)
                    {
                        modelSupport = pythonResponse.model_support.ToObject<Dictionary<string, List<string>>>();
                    }
                }
                else
                {
                    // Fallback to default types
                    supportedTypes = new List<string> { "text-generation", "text-to-image", "image-to-image" };
                }

                var response = new GetSupportedTypesResponse
                {
                    SupportedTypes = supportedTypes,
                    TotalTypes = supportedTypes.Count,
                    LastUpdated = DateTime.UtcNow
                };

                _logger.LogInformation($"Retrieved {supportedTypes.Count} supported inference types");
                return ApiResponse<GetSupportedTypesResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get supported types");
                return ApiResponse<GetSupportedTypesResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get supported types: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetSupportedTypesDeviceResponse>> GetSupportedTypesAsync(string idDevice)
        {
            try
            {
                _logger.LogInformation($"Getting supported inference types for device: {idDevice}");

                if (string.IsNullOrWhiteSpace(idDevice))
                    return ApiResponse<GetSupportedTypesDeviceResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" });

                var pythonRequest = new { device_id = idDevice, action = "get_device_supported_types" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_device_supported_types", pythonRequest);

                var supportedTypes = new List<string>();
                var deviceLimitations = new Dictionary<string, object>();

                if (pythonResponse?.success == true)
                {
                    if (pythonResponse?.types != null)
                    {
                        foreach (var type in pythonResponse.types)
                        {
                            var typeName = type.name?.ToString() ?? "Unknown";
                            supportedTypes.Add(typeName);
                        }
                    }

                    if (pythonResponse?.limitations != null)
                    {
                        deviceLimitations = pythonResponse.limitations.ToObject<Dictionary<string, object>>();
                    }
                }
                else
                {
                    return ApiResponse<GetSupportedTypesDeviceResponse>.CreateError(
                        new ErrorDetails { Message = $"Device '{idDevice}' not found or not available" });
                }

                var response = new GetSupportedTypesDeviceResponse
                {
                    DeviceId = idDevice,
                    SupportedTypes = supportedTypes,
                    TotalTypes = supportedTypes.Count,
                    LastUpdated = DateTime.UtcNow
                };

                _logger.LogInformation($"Retrieved {supportedTypes.Count} supported types for device: {idDevice}");
                return ApiResponse<GetSupportedTypesDeviceResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get supported types for device: {idDevice}");
                return ApiResponse<GetSupportedTypesDeviceResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get device supported types: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetInferenceSessionsResponse>> GetInferenceSessionsAsync()
        {
            try
            {
                _logger.LogInformation("Getting all inference sessions");

                // Update session statuses
                await UpdateSessionStatusesAsync();

                var allSessions = _activeSessions.Values.ToList();
                var activeSessions = allSessions.Where(s => s.Status == SessionStatus.Running).ToList();
                var completedSessions = allSessions.Where(s => s.Status == SessionStatus.Completed).ToList();
                var failedSessions = allSessions.Where(s => s.Status == SessionStatus.Failed).ToList();

                var sessionInfos = allSessions.Select(s => new SessionInfo
                {
                    SessionId = Guid.Parse(s.Id),
                    Status = s.Status.ToString(),
                    StartedAt = s.StartedAt ?? s.CreatedAt
                }).ToList();

                var response = new GetInferenceSessionsResponse
                {
                    Sessions = sessionInfos
                };

                _logger.LogInformation($"Retrieved {allSessions.Count} inference sessions");
                return ApiResponse<GetInferenceSessionsResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get inference sessions");
                return ApiResponse<GetInferenceSessionsResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get inference sessions: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetInferenceSessionResponse>> GetInferenceSessionAsync(string idSession)
        {
            try
            {
                _logger.LogInformation($"Getting inference session: {idSession}");

                if (string.IsNullOrWhiteSpace(idSession))
                    return ApiResponse<GetInferenceSessionResponse>.CreateError(
                        new ErrorDetails { Message = "Session ID cannot be null or empty" });

                if (!_activeSessions.TryGetValue(idSession, out var session))
                {
                    return ApiResponse<GetInferenceSessionResponse>.CreateError(
                        new ErrorDetails { Message = $"Session '{idSession}' not found" });
                }

                // Update session status
                await UpdateSessionStatusAsync(session);

                var sessionInfo = new SessionInfo
                {
                    SessionId = Guid.Parse(session.Id),
                    Status = session.Status.ToString(),
                    StartedAt = session.StartedAt ?? session.CreatedAt
                };

                var response = new GetInferenceSessionResponse
                {
                    Session = sessionInfo
                };

                _logger.LogInformation($"Retrieved session details for: {idSession}");
                return ApiResponse<GetInferenceSessionResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get inference session: {idSession}");
                return ApiResponse<GetInferenceSessionResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get inference session: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<DeleteInferenceSessionResponse>> DeleteInferenceSessionAsync(string idSession, DeleteInferenceSessionRequest request)
        {
            try
            {
                _logger.LogInformation($"Deleting inference session: {idSession}");

                if (string.IsNullOrWhiteSpace(idSession))
                    return ApiResponse<DeleteInferenceSessionResponse>.CreateError(
                        new ErrorDetails { Message = "Session ID cannot be null or empty" });

                if (!_activeSessions.TryGetValue(idSession, out var session))
                {
                    return ApiResponse<DeleteInferenceSessionResponse>.CreateError(
                        new ErrorDetails { Message = $"Session '{idSession}' not found" });
                }

                var wasRunning = session.Status == SessionStatus.Running;
                var pythonRequest = new
                {
                    session_id = idSession,
                    force_cancel = false,
                    preserve_results = true,
                    action = "cancel_session"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "cancel_session", pythonRequest);

                // Update session status
                session.Status = (SessionStatus)InferenceStatus.Cancelled;
                session.LastUpdated = DateTime.UtcNow;
                session.CompletedAt = DateTime.UtcNow;

                var response = new DeleteInferenceSessionResponse
                {
                    Success = true
                };

                // Remove from active sessions
                _activeSessions.Remove(idSession);

                _logger.LogInformation($"Successfully deleted session: {idSession}");
                return ApiResponse<DeleteInferenceSessionResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete inference session: {idSession}");
                return ApiResponse<DeleteInferenceSessionResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to delete inference session: {ex.Message}" });
            }
        }

        #region Private Helper Methods

        private async Task RefreshCapabilitiesAsync()
        {
            if (DateTime.UtcNow - _lastCapabilitiesRefresh < _cacheTimeout && _deviceCapabilities.Count > 0)
                return;

            try
            {
                var pythonRequest = new { action = "get_all_capabilities" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_all_capabilities", pythonRequest);

                if (pythonResponse?.success == true && pythonResponse?.devices != null)
                {
                    _deviceCapabilities.Clear();
                    var devices = pythonResponse!.devices;
                    if (devices != null)
                    {
                        foreach (var device in devices)
                        {
                            if (device != null)
                            {
                                var capabilities = CreateCapabilitiesFromPython(device);
                                _deviceCapabilities[capabilities.DeviceId] = capabilities;
                            }
                        }
                    }
                }
                else
                {
                    // Fallback to mock data
                    await PopulateMockCapabilitiesAsync();
                }

                _lastCapabilitiesRefresh = DateTime.UtcNow;
                _logger.LogDebug($"Capabilities refreshed for {_deviceCapabilities.Count} devices");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh capabilities from Python worker, using mock data");
                await PopulateMockCapabilitiesAsync();
            }
        }

        private InferenceCapabilities CreateCapabilitiesFromPython(dynamic pythonCapabilities)
        {
            return new InferenceCapabilities
            {
                SupportedInferenceTypes = pythonCapabilities.supported_types?.ToObject<List<string>>() ?? 
                    new List<string> { "TextGeneration", "ImageGeneration" },
                SupportedPrecisions = pythonCapabilities.supported_precisions?.ToObject<List<string>>() ?? 
                    new List<string> { "FP32", "FP16" },
                MaxConcurrentInferences = pythonCapabilities.max_concurrent ?? 2,
                MaxBatchSize = pythonCapabilities.max_batch_size ?? 8,
                MaxResolution = new Models.Common.ImageResolution
                {
                    Width = pythonCapabilities.max_width ?? 2048,
                    Height = pythonCapabilities.max_height ?? 2048
                }
            };
        }

        private async Task PopulateMockCapabilitiesAsync()
        {
            await Task.Delay(1); // Simulate async operation

            var mockCapabilities = new[]
            {
                new InferenceCapabilities
                {
                    SupportedInferenceTypes = new List<string> 
                    { 
                        "TextGeneration", 
                        "ImageGeneration", 
                        "ImageToImage",
                        "Inpainting" 
                    },
                    SupportedPrecisions = new List<string> { "FP32", "FP16", "INT8" },
                    MaxBatchSize = 8,
                    MaxConcurrentInferences = 3,
                    MaxResolution = new Models.Common.ImageResolution { Width = 2048, Height = 2048 }
                },
                new InferenceCapabilities
                {
                    SupportedInferenceTypes = new List<string> 
                    { 
                        "TextGeneration", 
                        "ImageGeneration" 
                    },
                    SupportedPrecisions = new List<string> { "FP32", "FP16" },
                    MaxBatchSize = 4,
                    MaxConcurrentInferences = 2,
                    MaxResolution = new Models.Common.ImageResolution { Width = 1024, Height = 1024 }
                }
            };

            _deviceCapabilities.Clear();
            int deviceIndex = 0;
            foreach (var capability in mockCapabilities)
            {
                _deviceCapabilities[$"device-{deviceIndex++}"] = capability;
            }
        }

        private async Task UpdateSessionStatusesAsync()
        {
            foreach (var session in _activeSessions.Values.Where(s => s.Status == SessionStatus.Running))
            {
                await UpdateSessionStatusAsync(session);
            }
        }

        private async Task UpdateSessionStatusAsync(InferenceSession session)
        {
            try
            {
                var pythonRequest = new { session_id = session.Id, action = "get_status" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_session_status", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    if (Enum.TryParse<SessionStatus>(pythonResponse.status?.ToString(), true, out SessionStatus status))
                    {
                        session.Status = status;
                    }

                    if (session.Status == SessionStatus.Completed && pythonResponse.completed_at != null)
                    {
                        session.CompletedAt = DateTime.Parse(pythonResponse.completed_at.ToString());
                    }

                    session.LastUpdated = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to update status for session: {session.Id}");
            }
        }

        private async Task<List<string>> GetSessionLogsAsync(string sessionId)
        {
            try
            {
                var pythonRequest = new { session_id = sessionId, action = "get_logs" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_session_logs", pythonRequest);

                if (pythonResponse?.success == true && pythonResponse?.logs != null)
                {
                    return pythonResponse!.logs!.ToObject<List<string>>() ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get logs for session: {sessionId}");
            }

            return new List<string> { "No logs available" };
        }

        private async Task<Dictionary<string, object>> GetSessionPerformanceAsync(string sessionId)
        {
            try
            {
                var pythonRequest = new { session_id = sessionId, action = "get_performance" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_session_performance", pythonRequest);

                if (pythonResponse?.success == true && pythonResponse?.performance != null)
                {
                    return pythonResponse!.performance!.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get performance for session: {sessionId}");
            }

            return new Dictionary<string, object>
            {
                ["avg_tokens_per_second"] = 15.5,
                ["peak_memory_usage"] = 4294967296, // 4GB
                ["gpu_utilization"] = 0.85
            };
        }

        private async Task<Dictionary<string, object>> GetSessionResourceUsageAsync(string sessionId)
        {
            try
            {
                var pythonRequest = new { session_id = sessionId, action = "get_resources" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_session_resources", pythonRequest);

                if (pythonResponse?.success == true && pythonResponse?.resources != null)
                {
                    return pythonResponse!.resources!.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get resource usage for session: {sessionId}");
            }

            return new Dictionary<string, object>
            {
                ["memory_used"] = 2147483648, // 2GB
                ["cpu_usage"] = 0.25,
                ["gpu_memory_used"] = 6442450944 // 6GB
            };
        }

        #endregion
    }
}
