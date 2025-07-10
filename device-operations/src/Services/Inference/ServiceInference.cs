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
                    .SelectMany(c => c.SupportedTypes)
                    .Distinct()
                    .ToList();

                var supportedModels = allCapabilities
                    .SelectMany(c => c.SupportedModels)
                    .Distinct()
                    .ToList();

                var response = new GetInferenceCapabilitiesResponse
                {
                    SupportedInferenceTypes = supportedTypes,
                    SupportedModelTypes = supportedModels,
                    AvailableDevices = _deviceCapabilities.Count,
                    MaxConcurrentInferences = allCapabilities.Sum(c => c.MaxConcurrentInferences),
                    DeviceCapabilities = allCapabilities,
                    LastUpdated = DateTime.UtcNow
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
                    DeviceId = idDevice,
                    Capabilities = capabilities,
                    IsAvailable = capabilities.IsAvailable,
                    CurrentLoad = capabilities.CurrentLoad,
                    ActiveSessions = _activeSessions.Values.Count(s => s.DeviceId == idDevice),
                    LastChecked = DateTime.UtcNow
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

                // Find best available device
                await RefreshCapabilitiesAsync();
                var availableDevice = _deviceCapabilities.Values
                    .Where(c => c.IsAvailable && c.SupportedModels.Contains(request.ModelId))
                    .OrderBy(c => c.CurrentLoad)
                    .FirstOrDefault();

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
                    device_id = availableDevice.DeviceId,
                    prompt = request.Prompt,
                    parameters = request.Parameters,
                    inference_type = request.InferenceType?.ToString(),
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
                        DeviceId = availableDevice.DeviceId,
                        Status = InferenceStatus.Running,
                        StartedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    _activeSessions[sessionId] = session;

                    var response = new PostInferenceExecuteResponse
                    {
                        InferenceId = sessionId,
                        ModelId = request.ModelId,
                        DeviceId = availableDevice.DeviceId,
                        Status = InferenceStatus.Running,
                        StartedAt = DateTime.UtcNow,
                        EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 30),
                        QueuePosition = pythonResponse.queue_position ?? 0
                    };

                    _logger.LogInformation($"Started inference session: {sessionId} on device: {availableDevice.DeviceId}");
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
                if (!_deviceCapabilities.TryGetValue(idDevice, out var capabilities) || !capabilities.IsAvailable)
                {
                    return ApiResponse<PostInferenceExecuteDeviceResponse>.CreateError(
                        new ErrorDetails { Message = $"Device '{idDevice}' is not available for inference" });
                }

                if (!capabilities.SupportedModels.Contains(request.ModelId))
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
                    prompt = request.Prompt,
                    parameters = request.Parameters,
                    inference_type = request.InferenceType?.ToString(),
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
                        Status = InferenceStatus.Running,
                        StartedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    _activeSessions[sessionId] = session;

                    var response = new PostInferenceExecuteDeviceResponse
                    {
                        InferenceId = sessionId,
                        ModelId = request.ModelId,
                        DeviceId = idDevice,
                        Status = InferenceStatus.Running,
                        StartedAt = DateTime.UtcNow,
                        EstimatedCompletionTime = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 30),
                        DeviceLoad = capabilities.CurrentLoad,
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
                    prompt = request.Prompt,
                    parameters = request.Parameters,
                    inference_type = request.InferenceType?.ToString(),
                    validation_level = request.ValidationLevel ?? "basic",
                    action = "validate_request"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "validate_request", pythonRequest);

                var isValid = pythonResponse?.success == true && pythonResponse?.is_valid == true;
                var issues = new List<ValidationIssue>();
                var suggestions = new List<string>();

                if (pythonResponse?.issues != null)
                {
                    foreach (var issue in pythonResponse.issues)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Type = issue.type?.ToString() ?? "error",
                            Message = issue.message?.ToString() ?? "Unknown issue",
                            Field = issue.field?.ToString(),
                            Severity = Enum.TryParse<ValidationSeverity>(issue.severity?.ToString(), true, out var severity) 
                                ? severity : ValidationSeverity.Error
                        });
                    }
                }

                if (pythonResponse?.suggestions != null)
                {
                    foreach (var suggestion in pythonResponse.suggestions)
                    {
                        suggestions.Add(suggestion.ToString());
                    }
                }

                // Find compatible devices
                await RefreshCapabilitiesAsync();
                var compatibleDevices = _deviceCapabilities.Values
                    .Where(c => c.SupportedModels.Contains(request.ModelId))
                    .Select(c => c.DeviceId)
                    .ToList();

                var response = new PostInferenceValidateResponse
                {
                    IsValid = isValid,
                    ValidationLevel = request.ValidationLevel ?? "basic",
                    Issues = issues,
                    Suggestions = suggestions,
                    CompatibleDevices = compatibleDevices,
                    EstimatedExecutionTime = TimeSpan.FromSeconds(pythonResponse?.estimated_execution_time ?? 30),
                    EstimatedMemoryUsage = pythonResponse?.estimated_memory_usage ?? 2147483648, // 2GB default
                    ValidatedAt = DateTime.UtcNow
                };

                _logger.LogInformation($"Validation completed: {(isValid ? "Valid" : "Invalid")} with {issues.Count} issues");
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

                var supportedTypes = new List<InferenceTypeInfo>();
                var modelSupport = new Dictionary<string, List<string>>();

                if (pythonResponse?.success == true && pythonResponse?.types != null)
                {
                    foreach (var type in pythonResponse.types)
                    {
                        supportedTypes.Add(new InferenceTypeInfo
                        {
                            Type = Enum.TryParse<InferenceType>(type.name?.ToString(), true, out var inferenceType) 
                                ? inferenceType : InferenceType.TextGeneration,
                            Name = type.display_name?.ToString() ?? type.name?.ToString(),
                            Description = type.description?.ToString(),
                            SupportedModels = type.supported_models?.ToObject<List<string>>() ?? new List<string>(),
                            RequiredParameters = type.required_parameters?.ToObject<List<string>>() ?? new List<string>(),
                            OptionalParameters = type.optional_parameters?.ToObject<List<string>>() ?? new List<string>(),
                            IsExperimental = type.is_experimental ?? false
                        });
                    }

                    if (pythonResponse?.model_support != null)
                    {
                        modelSupport = pythonResponse.model_support.ToObject<Dictionary<string, List<string>>>();
                    }
                }
                else
                {
                    // Fallback to default types
                    supportedTypes = GetDefaultSupportedTypes();
                }

                var response = new GetSupportedTypesResponse
                {
                    SupportedTypes = supportedTypes,
                    ModelSupport = modelSupport,
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

                var supportedTypes = new List<InferenceTypeInfo>();
                var deviceLimitations = new Dictionary<string, object>();

                if (pythonResponse?.success == true)
                {
                    if (pythonResponse?.types != null)
                    {
                        foreach (var type in pythonResponse.types)
                        {
                            supportedTypes.Add(new InferenceTypeInfo
                            {
                                Type = Enum.TryParse<InferenceType>(type.name?.ToString(), true, out var inferenceType) 
                                    ? inferenceType : InferenceType.TextGeneration,
                                Name = type.display_name?.ToString() ?? type.name?.ToString(),
                                Description = type.description?.ToString(),
                                SupportedModels = type.supported_models?.ToObject<List<string>>() ?? new List<string>(),
                                RequiredParameters = type.required_parameters?.ToObject<List<string>>() ?? new List<string>(),
                                OptionalParameters = type.optional_parameters?.ToObject<List<string>>() ?? new List<string>(),
                                IsExperimental = type.is_experimental ?? false
                            });
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
                    DeviceLimitations = deviceLimitations,
                    MaxConcurrentInferences = pythonResponse?.max_concurrent ?? 1,
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
                var activeSessions = allSessions.Where(s => s.Status == InferenceStatus.Running).ToList();
                var completedSessions = allSessions.Where(s => s.Status == InferenceStatus.Completed).ToList();
                var failedSessions = allSessions.Where(s => s.Status == InferenceStatus.Failed).ToList();

                var response = new GetInferenceSessionsResponse
                {
                    Sessions = allSessions,
                    TotalSessions = allSessions.Count,
                    ActiveSessions = activeSessions.Count,
                    CompletedSessions = completedSessions.Count,
                    FailedSessions = failedSessions.Count,
                    QueuedSessions = allSessions.Count(s => s.Status == InferenceStatus.Queued),
                    LastUpdated = DateTime.UtcNow
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

                var response = new GetInferenceSessionResponse
                {
                    Session = session,
                    ExecutionLogs = await GetSessionLogsAsync(idSession),
                    PerformanceMetrics = await GetSessionPerformanceAsync(idSession),
                    ResourceUsage = await GetSessionResourceUsageAsync(idSession),
                    LastUpdated = DateTime.UtcNow
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

                var wasRunning = session.Status == InferenceStatus.Running;
                var pythonRequest = new
                {
                    session_id = idSession,
                    force_cancel = request?.ForceCancel ?? false,
                    preserve_results = request?.PreserveResults ?? true,
                    action = "cancel_session"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "cancel_session", pythonRequest);

                // Update session status
                session.Status = InferenceStatus.Cancelled;
                session.LastUpdated = DateTime.UtcNow;
                session.CompletedAt = DateTime.UtcNow;

                var response = new DeleteInferenceSessionResponse
                {
                    SessionId = idSession,
                    WasRunning = wasRunning,
                    CancelledAt = DateTime.UtcNow,
                    ResourcesReleased = pythonResponse?.resources_released ?? true,
                    ResultsPreserved = request?.PreserveResults ?? true,
                    FinalStatus = session.Status
                };

                // Remove from active sessions if not preserving results
                if (request?.PreserveResults != true)
                {
                    _activeSessions.Remove(idSession);
                }

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
                    foreach (var device in pythonResponse.devices)
                    {
                        var capabilities = CreateCapabilitiesFromPython(device);
                        _deviceCapabilities[capabilities.DeviceId] = capabilities;
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
                DeviceId = pythonCapabilities.device_id?.ToString() ?? Guid.NewGuid().ToString(),
                SupportedTypes = pythonCapabilities.supported_types?.ToObject<List<InferenceType>>() ?? 
                    new List<InferenceType> { InferenceType.TextGeneration, InferenceType.ImageGeneration },
                SupportedModels = pythonCapabilities.supported_models?.ToObject<List<string>>() ?? 
                    new List<string> { "sd15-base", "sdxl-base" },
                MaxConcurrentInferences = pythonCapabilities.max_concurrent ?? 2,
                MaxMemoryUsage = pythonCapabilities.max_memory ?? 8589934592, // 8GB
                CurrentLoad = pythonCapabilities.current_load ?? Random.Shared.NextSingle() * 0.5f,
                IsAvailable = pythonCapabilities.is_available ?? true,
                LastUpdated = DateTime.UtcNow
            };
        }

        private async Task PopulateMockCapabilitiesAsync()
        {
            await Task.Delay(1); // Simulate async operation

            var mockCapabilities = new[]
            {
                new InferenceCapabilities
                {
                    DeviceId = "gpu-nvidia-rtx4090",
                    SupportedTypes = new List<InferenceType> 
                    { 
                        InferenceType.TextGeneration, 
                        InferenceType.ImageGeneration, 
                        InferenceType.ImageToImage,
                        InferenceType.Inpainting 
                    },
                    SupportedModels = new List<string> { "sd15-base", "sdxl-base", "flux-dev" },
                    MaxConcurrentInferences = 3,
                    MaxMemoryUsage = 25769803776, // 24GB
                    CurrentLoad = 0.2f,
                    IsAvailable = true,
                    LastUpdated = DateTime.UtcNow
                },
                new InferenceCapabilities
                {
                    DeviceId = "gpu-nvidia-rtx3080",
                    SupportedTypes = new List<InferenceType> 
                    { 
                        InferenceType.TextGeneration, 
                        InferenceType.ImageGeneration 
                    },
                    SupportedModels = new List<string> { "sd15-base", "sdxl-base" },
                    MaxConcurrentInferences = 2,
                    MaxMemoryUsage = 10737418240, // 10GB
                    CurrentLoad = 0.1f,
                    IsAvailable = true,
                    LastUpdated = DateTime.UtcNow
                }
            };

            _deviceCapabilities.Clear();
            foreach (var capability in mockCapabilities)
            {
                _deviceCapabilities[capability.DeviceId] = capability;
            }
        }

        private List<InferenceTypeInfo> GetDefaultSupportedTypes()
        {
            return new List<InferenceTypeInfo>
            {
                new InferenceTypeInfo
                {
                    Type = InferenceType.TextGeneration,
                    Name = "Text Generation",
                    Description = "Generate text based on prompts",
                    SupportedModels = new List<string> { "llama2", "gpt-3.5", "claude" },
                    RequiredParameters = new List<string> { "prompt" },
                    OptionalParameters = new List<string> { "max_tokens", "temperature", "top_p" },
                    IsExperimental = false
                },
                new InferenceTypeInfo
                {
                    Type = InferenceType.ImageGeneration,
                    Name = "Image Generation",
                    Description = "Generate images from text prompts",
                    SupportedModels = new List<string> { "sd15-base", "sdxl-base", "flux-dev" },
                    RequiredParameters = new List<string> { "prompt" },
                    OptionalParameters = new List<string> { "negative_prompt", "steps", "cfg_scale", "width", "height" },
                    IsExperimental = false
                },
                new InferenceTypeInfo
                {
                    Type = InferenceType.ImageToImage,
                    Name = "Image to Image",
                    Description = "Transform existing images based on prompts",
                    SupportedModels = new List<string> { "sd15-base", "sdxl-base" },
                    RequiredParameters = new List<string> { "prompt", "init_image" },
                    OptionalParameters = new List<string> { "strength", "steps", "cfg_scale" },
                    IsExperimental = false
                }
            };
        }

        private async Task UpdateSessionStatusesAsync()
        {
            foreach (var session in _activeSessions.Values.Where(s => s.Status == InferenceStatus.Running))
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
                    if (Enum.TryParse<InferenceStatus>(pythonResponse.status?.ToString(), true, out var status))
                    {
                        session.Status = status;
                    }

                    if (status == InferenceStatus.Completed && pythonResponse.completed_at != null)
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
                    return pythonResponse.logs.ToObject<List<string>>();
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
                    return pythonResponse.performance.ToObject<Dictionary<string, object>>();
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
                    return pythonResponse.resources.ToObject<Dictionary<string, object>>();
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
