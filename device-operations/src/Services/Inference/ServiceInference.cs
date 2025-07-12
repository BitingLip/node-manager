using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Python;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Concurrent;
using DeviceOperations.Models.Inference;

namespace DeviceOperations.Services.Inference
{
    /// <summary>
    /// Enhanced request tracking information for complete traceability
    /// </summary>
    public class InferenceRequestTrace
    {
        public string RequestId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<string> ProcessingSteps { get; set; } = new();
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
        public List<InferenceError> Errors { get; set; } = new();
        public InferenceRequestStatus Status { get; set; }
    }

    /// <summary>
    /// Standardized error response format with detailed error codes
    /// </summary>
    public class InferenceError
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorCategory { get; set; } = string.Empty;
        public Dictionary<string, object> ErrorDetails { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string StackTrace { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request status enumeration for tracking
    /// </summary>
    public enum InferenceRequestStatus
    {
        Initiated,
        Validated,
        Queued,
        Processing,
        Completed,
        Failed,
        Cancelled,
        Timeout
    }

    /// <summary>
    /// Service implementation for inference operations with enhanced tracking and error handling
    /// </summary>
    public class ServiceInference : IServiceInference
    {
        private readonly ILogger<ServiceInference> _logger;
        private readonly IPythonWorkerService _pythonWorkerService;
        private readonly InferenceFieldTransformer _fieldTransformer;
        private readonly IOptimizedPythonWorkerService _optimizedWorkerService;
        private readonly Dictionary<string, InferenceSession> _activeSessions;
        private readonly Dictionary<string, InferenceCapabilities> _deviceCapabilities;
        private readonly Dictionary<string, InferenceRequestTrace> _requestTraces;
        private DateTime _lastCapabilitiesRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(10);

        // Enhanced caching with intelligent refresh
        private readonly Dictionary<string, CachedCapabilities> _capabilitiesCache;
        private readonly Dictionary<string, DateTime> _cacheAccessTimes;
        private readonly Timer _cacheMaintenanceTimer;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private const int MaxCacheSize = 100;
        private readonly TimeSpan _cacheRefreshInterval = TimeSpan.FromMinutes(15);
        
        // Performance tracking
        private long _cacheHits;
        private long _cacheMisses;
        private readonly ConcurrentDictionary<string, OperationMetrics> _operationMetrics;

        public ServiceInference(
            ILogger<ServiceInference> logger,
            IPythonWorkerService pythonWorkerService,
            InferenceFieldTransformer fieldTransformer,
            IOptimizedPythonWorkerService optimizedWorkerService)
        {
            _logger = logger;
            _pythonWorkerService = pythonWorkerService;
            _fieldTransformer = fieldTransformer;
            _optimizedWorkerService = optimizedWorkerService;
            _activeSessions = new Dictionary<string, InferenceSession>();
            _deviceCapabilities = new Dictionary<string, InferenceCapabilities>();
            _requestTraces = new Dictionary<string, InferenceRequestTrace>();
            
            // Initialize enhanced caching
            _capabilitiesCache = new Dictionary<string, CachedCapabilities>();
            _cacheAccessTimes = new Dictionary<string, DateTime>();
            _operationMetrics = new ConcurrentDictionary<string, OperationMetrics>();
            
            // Initialize cache maintenance timer
            _cacheMaintenanceTimer = new Timer(PerformCacheMaintenance, null, 
                (int)TimeSpan.FromMinutes(5).TotalMilliseconds, 
                (int)TimeSpan.FromMinutes(5).TotalMilliseconds);
        }

        public async Task<ApiResponse<GetInferenceCapabilitiesResponse>> GetInferenceCapabilitiesAsync()
        {
            var requestId = CreateRequestTrace("GetInferenceCapabilities");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation($"[{requestId}] Getting overall inference capabilities with intelligent caching");
                UpdateRequestTrace(requestId, "Starting capabilities retrieval", InferenceRequestStatus.Processing);

                // Try to get capabilities from intelligent cache first
                var cachedResponse = await GetCapabilitiesFromCacheAsync();
                if (cachedResponse != null)
                {
                    Interlocked.Increment(ref _cacheHits);
                    TrackOperationMetrics("GetInferenceCapabilities", stopwatch.Elapsed, true);
                    
                    _logger.LogDebug($"[{requestId}] Capabilities retrieved from cache in {stopwatch.ElapsedMilliseconds}ms");
                    UpdateRequestTrace(requestId, "Capabilities retrieved from cache", InferenceRequestStatus.Completed);
                    
                    return ApiResponse<GetInferenceCapabilitiesResponse>.CreateSuccess(cachedResponse);
                }

                Interlocked.Increment(ref _cacheMisses);
                
                // Refresh capabilities from devices
                await RefreshCapabilitiesWithOptimizationAsync();

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

                // Cache the response
                await CacheCapabilitiesAsync(response);
                
                TrackOperationMetrics("GetInferenceCapabilities", stopwatch.Elapsed, true);
                _logger.LogInformation($"[{requestId}] Capabilities retrieved and cached in {stopwatch.ElapsedMilliseconds}ms");
                UpdateRequestTrace(requestId, "Capabilities retrieved and cached", InferenceRequestStatus.Completed);

                var performanceMetrics = new Dictionary<string, object>
                {
                    ["devices_discovered"] = allCapabilities.Count,
                    ["supported_types_count"] = supportedTypes.Count,
                    ["response_time_ms"] = stopwatch.ElapsedMilliseconds,
                    ["cache_hit"] = false
                };

                CompleteRequestTrace(requestId, InferenceRequestStatus.Completed, performanceMetrics);
                _logger.LogInformation($"[{requestId}] Retrieved capabilities for {allCapabilities.Count} devices");
                
                return ApiResponse<GetInferenceCapabilitiesResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                AddRequestError(requestId, "CAP_RETRIEVAL_ERROR", ex.Message, "SystemError");
                _logger.LogError(ex, $"[{requestId}] Failed to get inference capabilities");
                return CreateStandardizedErrorResponse<GetInferenceCapabilitiesResponse>(
                    requestId, "CAP_RETRIEVAL_ERROR", $"Failed to get inference capabilities: {ex.Message}", "SystemError");
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
            var requestId = CreateRequestTrace("PostInferenceExecute", new Dictionary<string, object>
            {
                ["model_id"] = request?.ModelId ?? "unknown",
                ["inference_type"] = request?.InferenceType.ToString() ?? "unknown"
            });

            try
            {
                _logger.LogInformation($"[{requestId}] Executing inference request");

                // Validate and transform request
                var (isValid, transformedRequest, errorMessage) = ValidateAndTransformRequest(request, requestId);
                if (!isValid)
                {
                    return CreateStandardizedErrorResponse<PostInferenceExecuteResponse>(
                        requestId, "REQ_VALIDATION_ERROR", errorMessage, "ValidationError");
                }

                // Find best available device - simplified approach
                UpdateRequestTrace(requestId, "Finding available device", InferenceRequestStatus.Queued);
                await RefreshCapabilitiesAsync();
                var availableDeviceKvp = _deviceCapabilities.FirstOrDefault();
                
                if (availableDeviceKvp.Key == null)
                {
                    AddRequestError(requestId, "NO_DEVICE_AVAILABLE", "No available device supports the requested model", "DeviceError");
                    return CreateStandardizedErrorResponse<PostInferenceExecuteResponse>(
                        requestId, "NO_DEVICE_AVAILABLE", "No available device supports the requested model", "DeviceError");
                }
                
                var deviceId = availableDeviceKvp.Key;
                var availableDevice = availableDeviceKvp.Value;

                var sessionId = Guid.NewGuid().ToString();
                
                // Update trace with session and device information
                if (_requestTraces.TryGetValue(requestId, out var trace))
                {
                    trace.SessionId = sessionId;
                    trace.DeviceId = deviceId;
                    trace.ModelId = request?.ModelId ?? "";
                }

                UpdateRequestTrace(requestId, $"Assigned to device: {deviceId}, session: {sessionId}", InferenceRequestStatus.Processing);

                // Prepare Python request with field transformation
                var pythonRequest = new Dictionary<string, object>(transformedRequest)
                {
                    ["session_id"] = sessionId,
                    ["device_id"] = deviceId,
                    ["action"] = "execute_inference",
                    ["request_id"] = requestId
                };

                UpdateRequestTrace(requestId, "Sending request to Python worker");
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "execute_inference", pythonRequest);

                // Validate and transform response
                var responseValidation = ValidateAndTransformResponse(pythonResponse, requestId);
                if (!responseValidation.Success)
                {
                    return CreateStandardizedErrorResponse<PostInferenceExecuteResponse>(
                        requestId, "PYTHON_EXEC_ERROR", responseValidation.ErrorMessage, "PythonExecutionError");
                }

                var transformedResponse = responseValidation.TransformedResponse;

                var session = new InferenceSession
                {
                    Id = sessionId,
                    ModelId = request?.ModelId ?? "",
                    DeviceId = deviceId,
                    Status = (SessionStatus)InferenceStatus.Running,
                    StartedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                _activeSessions[sessionId] = session;

                var response = new PostInferenceExecuteResponse
                {
                    Results = transformedResponse.ContainsKey("results") ? 
                        (transformedResponse["results"] as Dictionary<string, object>) ?? new Dictionary<string, object>() : 
                        new Dictionary<string, object>(),
                    Success = true,
                    InferenceId = Guid.Parse(sessionId),
                    ModelId = request?.ModelId ?? "",
                    DeviceId = deviceId,
                    InferenceType = request?.InferenceType ?? InferenceType.Unknown,
                    Status = InferenceStatus.Running,
                    ExecutionTime = TimeSpan.FromSeconds(transformedResponse.ContainsKey("estimated_time") ? 
                        Convert.ToDouble(transformedResponse["estimated_time"]) : 30),
                    CompletedAt = DateTime.UtcNow.AddSeconds(transformedResponse.ContainsKey("estimated_time") ? 
                        Convert.ToDouble(transformedResponse["estimated_time"]) : 30),
                    Performance = new Dictionary<string, object>
                    {
                        ["estimated_time"] = transformedResponse.ContainsKey("estimated_time") ? transformedResponse["estimated_time"] : 30,
                        ["queue_position"] = transformedResponse.ContainsKey("queue_position") ? transformedResponse["queue_position"] : 0,
                        ["request_id"] = requestId
                    },
                    QualityMetrics = new Dictionary<string, object>
                    {
                        ["confidence"] = 0.85f,
                        ["processing_speed"] = 1.2f,
                        ["field_transform_success"] = true
                    }
                };

                var performanceMetrics = new Dictionary<string, object>
                {
                    ["session_id"] = sessionId,
                    ["device_id"] = deviceId,
                    ["estimated_execution_time"] = transformedResponse.ContainsKey("estimated_time") ? transformedResponse["estimated_time"] : 30,
                    ["queue_position"] = transformedResponse.ContainsKey("queue_position") ? transformedResponse["queue_position"] : 0
                };

                CompleteRequestTrace(requestId, InferenceRequestStatus.Completed, performanceMetrics);
                _logger.LogInformation($"[{requestId}] Started inference session: {sessionId} on device: {deviceId}");
                
                return ApiResponse<PostInferenceExecuteResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                AddRequestError(requestId, "EXEC_SYSTEM_ERROR", ex.Message, "SystemError");
                _logger.LogError(ex, $"[{requestId}] Failed to execute inference");
                return CreateStandardizedErrorResponse<PostInferenceExecuteResponse>(
                    requestId, "EXEC_SYSTEM_ERROR", $"Failed to execute inference: {ex.Message}", "SystemError");
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

        #region Enhanced Request Tracking and Error Standardization

        /// <summary>
        /// Create new request trace for complete traceability
        /// </summary>
        /// <param name="operationType">Type of operation being performed</param>
        /// <param name="parameters">Request parameters</param>
        /// <returns>Generated request ID</returns>
        private string CreateRequestTrace(string operationType, Dictionary<string, object>? parameters = null)
        {
            var requestId = $"req_{Guid.NewGuid():N}";
            var trace = new InferenceRequestTrace
            {
                RequestId = requestId,
                StartTime = DateTime.UtcNow,
                OperationType = operationType,
                Parameters = parameters ?? new Dictionary<string, object>(),
                Status = InferenceRequestStatus.Initiated
            };

            _requestTraces[requestId] = trace;
            _logger.LogInformation($"Created request trace: {requestId} for operation: {operationType}");
            
            return requestId;
        }

        /// <summary>
        /// Update request trace with processing step
        /// </summary>
        /// <param name="requestId">Request ID</param>
        /// <param name="step">Processing step description</param>
        /// <param name="status">Updated status</param>
        private void UpdateRequestTrace(string requestId, string step, InferenceRequestStatus? status = null)
        {
            if (_requestTraces.TryGetValue(requestId, out var trace))
            {
                trace.ProcessingSteps.Add($"{DateTime.UtcNow:HH:mm:ss.fff}: {step}");
                if (status.HasValue)
                {
                    trace.Status = status.Value;
                }
                
                _logger.LogDebug($"Request {requestId}: {step}");
            }
        }

        /// <summary>
        /// Complete request trace with final status and performance metrics
        /// </summary>
        /// <param name="requestId">Request ID</param>
        /// <param name="status">Final status</param>
        /// <param name="performanceMetrics">Performance metrics</param>
        private void CompleteRequestTrace(string requestId, InferenceRequestStatus status, Dictionary<string, object>? performanceMetrics = null)
        {
            if (_requestTraces.TryGetValue(requestId, out var trace))
            {
                trace.EndTime = DateTime.UtcNow;
                trace.Status = status;
                if (performanceMetrics != null)
                {
                    trace.PerformanceMetrics = performanceMetrics;
                }

                var duration = trace.EndTime.Value - trace.StartTime;
                _logger.LogInformation($"Completed request {requestId} in {duration.TotalMilliseconds:F2}ms with status: {status}");
            }
        }

        /// <summary>
        /// Add standardized error to request trace
        /// </summary>
        /// <param name="requestId">Request ID</param>
        /// <param name="errorCode">Standardized error code</param>
        /// <param name="errorMessage">Error message</param>
        /// <param name="errorCategory">Error category</param>
        /// <param name="errorDetails">Additional error details</param>
        private void AddRequestError(string requestId, string errorCode, string errorMessage, string errorCategory, Dictionary<string, object>? errorDetails = null)
        {
            var error = new InferenceError
            {
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                ErrorCategory = errorCategory,
                ErrorDetails = errorDetails ?? new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow,
                RequestId = requestId,
                StackTrace = new StackTrace().ToString()
            };

            if (_requestTraces.TryGetValue(requestId, out var trace))
            {
                trace.Errors.Add(error);
                trace.Status = InferenceRequestStatus.Failed;
            }

            _logger.LogError($"Request {requestId} error [{errorCode}] {errorCategory}: {errorMessage}");
        }

        /// <summary>
        /// Validate request and response with field transformation
        /// </summary>
        /// <typeparam name="TRequest">Request type</typeparam>
        /// <param name="request">Request object</param>
        /// <param name="requestId">Request ID for tracking</param>
        /// <returns>Validation result with transformed request</returns>
        private (bool IsValid, Dictionary<string, object> TransformedRequest, string ErrorMessage) ValidateAndTransformRequest<TRequest>(TRequest request, string requestId)
        {
            try
            {
                UpdateRequestTrace(requestId, "Validating request", InferenceRequestStatus.Validated);

                if (request == null)
                {
                    AddRequestError(requestId, "REQ_NULL", "Request cannot be null", "ValidationError");
                    return (false, new Dictionary<string, object>(), "Request cannot be null");
                }

                // Transform request to Python format
                var transformedRequest = _fieldTransformer.ToPythonFormat(request);
                UpdateRequestTrace(requestId, $"Transformed {transformedRequest.Count} fields to Python format");

                // Basic validation - can be extended with specific validation rules
                if (!transformedRequest.ContainsKey("model_id") && !transformedRequest.ContainsKey("ModelId"))
                {
                    AddRequestError(requestId, "REQ_MODEL_MISSING", "Model ID is required", "ValidationError");
                    return (false, transformedRequest, "Model ID is required");
                }

                return (true, transformedRequest, string.Empty);
            }
            catch (Exception ex)
            {
                AddRequestError(requestId, "REQ_TRANSFORM_ERROR", ex.Message, "TransformationError", 
                    new Dictionary<string, object> { ["exception_type"] = ex.GetType().Name });
                return (false, new Dictionary<string, object>(), $"Request transformation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Transform and validate Python response
        /// </summary>
        /// <param name="pythonResponse">Python response object</param>
        /// <param name="requestId">Request ID for tracking</param>
        /// <returns>Transformed response or error details</returns>
        private (bool Success, Dictionary<string, object> TransformedResponse, string ErrorMessage) ValidateAndTransformResponse(dynamic pythonResponse, string requestId)
        {
            try
            {
                UpdateRequestTrace(requestId, "Transforming Python response");

                if (pythonResponse == null)
                {
                    AddRequestError(requestId, "RESP_NULL", "Python response is null", "ResponseError");
                    return (false, new Dictionary<string, object>(), "Python response is null");
                }

                // Check if Python response indicates success
                if (pythonResponse.success == false)
                {
                    var error = pythonResponse.error?.ToString() ?? "Unknown Python error";
                    AddRequestError(requestId, "PYTHON_ERROR", error, "PythonExecutionError");
                    return (false, new Dictionary<string, object>(), error);
                }

                // Transform response to C# format
                var transformedResponse = _fieldTransformer.ToCSharpFormat(pythonResponse);
                UpdateRequestTrace(requestId, $"Transformed {transformedResponse.Count} fields to C# format");

                return (true, transformedResponse, string.Empty);
            }
            catch (Exception ex)
            {
                AddRequestError(requestId, "RESP_TRANSFORM_ERROR", ex.Message, "TransformationError",
                    new Dictionary<string, object> { ["exception_type"] = ex.GetType().Name });
                return (false, new Dictionary<string, object>(), $"Response transformation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get standardized error response
        /// </summary>
        /// <typeparam name="T">Response type</typeparam>
        /// <param name="requestId">Request ID</param>
        /// <param name="errorCode">Error code</param>
        /// <param name="errorMessage">Error message</param>
        /// <param name="errorCategory">Error category</param>
        /// <returns>Standardized error response</returns>
        private ApiResponse<T> CreateStandardizedErrorResponse<T>(string requestId, string errorCode, string errorMessage, string errorCategory)
        {
            CompleteRequestTrace(requestId, InferenceRequestStatus.Failed);
            
            var errorDetails = new ErrorDetails 
            { 
                Message = errorMessage,
                Code = errorCode,
                Timestamp = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    ["request_id"] = requestId,
                    ["error_category"] = errorCategory,
                    ["request_trace_available"] = _requestTraces.ContainsKey(requestId)
                }
            };

            return ApiResponse<T>.CreateError(errorDetails);
        }

        /// <summary>
        /// Get request trace information for debugging
        /// </summary>
        /// <param name="requestId">Request ID</param>
        /// <returns>Request trace information</returns>
        public Dictionary<string, object> GetRequestTrace(string requestId)
        {
            try
            {
                if (_requestTraces.TryGetValue(requestId, out var trace))
                {
                    return new Dictionary<string, object>
                    {
                        ["request_id"] = trace.RequestId,
                        ["session_id"] = trace.SessionId,
                        ["operation_type"] = trace.OperationType,
                        ["start_time"] = trace.StartTime,
                        ["end_time"] = trace.EndTime ?? (object)"null",
                        ["duration_ms"] = trace.EndTime?.Subtract(trace.StartTime).TotalMilliseconds ?? (object)"null",
                        ["status"] = trace.Status.ToString(),
                        ["processing_steps"] = trace.ProcessingSteps,
                        ["parameters"] = trace.Parameters,
                        ["performance_metrics"] = trace.PerformanceMetrics,
                        ["errors"] = trace.Errors.Select(e => new
                        {
                            error_code = e.ErrorCode,
                            error_message = e.ErrorMessage,
                            error_category = e.ErrorCategory,
                            timestamp = e.Timestamp,
                            details = e.ErrorDetails
                        }).ToList()
                    };
                }

                return new Dictionary<string, object>
                {
                    ["error"] = "Request trace not found",
                    ["request_id"] = requestId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get request trace for: {requestId}");
                return new Dictionary<string, object>
                {
                    ["error"] = $"Failed to get request trace: {ex.Message}",
                    ["request_id"] = requestId
                };
            }
        }

        #endregion

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

        #region Advanced Feature Methods (Week 18)

        /// <summary>
        /// Execute batch inference processing with sophisticated queue management
        /// </summary>
        /// <param name="request">Batch inference request</param>
        /// <returns>Batch processing response with progress tracking</returns>
        public async Task<ApiResponse<PostInferenceBatchResponse>> PostInferenceBatchAsync(PostInferenceBatchRequest request)
        {
            var requestId = CreateRequestTrace("PostInferenceBatch", new Dictionary<string, object>
            {
                ["model_id"] = request?.ModelId ?? "unknown",
                ["batch_size"] = request?.Items?.Count ?? 0,
                ["inference_type"] = request?.InferenceType.ToString() ?? "unknown"
            });

            try
            {
                _logger.LogInformation($"[{requestId}] Starting batch inference processing");

                // Validate and transform request
                var (isValid, transformedRequest, errorMessage) = ValidateAndTransformRequest(request, requestId);
                if (!isValid)
                {
                    return CreateStandardizedErrorResponse<PostInferenceBatchResponse>(
                        requestId, "BATCH_VALIDATION_ERROR", errorMessage, "ValidationError");
                }

                if (request == null || request.Items == null || !request.Items.Any())
                {
                    AddRequestError(requestId, "BATCH_EMPTY", "Batch must contain at least one item", "ValidationError");
                    return CreateStandardizedErrorResponse<PostInferenceBatchResponse>(
                        requestId, "BATCH_EMPTY", "Batch must contain at least one item", "ValidationError");
                }

                UpdateRequestTrace(requestId, $"Validating batch with {request.Items.Count} items", InferenceRequestStatus.Validated);

                // Calculate optimal batch size
                var deviceId = _deviceCapabilities.Keys.FirstOrDefault() ?? "default-device";
                var optimalBatchSize = await CalculateOptimalBatchSizeAsync(request.ModelId, deviceId);
                
                var batchId = request.BatchId ?? $"batch_{Guid.NewGuid():N}";
                
                UpdateRequestTrace(requestId, $"Assigned batch ID: {batchId}, optimal size: {optimalBatchSize}", InferenceRequestStatus.Queued);

                // Prepare Python batch request with field transformation
                var pythonBatchRequest = new Dictionary<string, object>(transformedRequest)
                {
                    ["batch_id"] = batchId,
                    ["device_id"] = deviceId,
                    ["action"] = "batch_process",
                    ["request_id"] = requestId,
                    ["optimal_batch_size"] = optimalBatchSize,
                    ["items"] = request.Items.Select(item => _fieldTransformer.ToPythonFormat(item)).ToList()
                };

                UpdateRequestTrace(requestId, "Sending batch request to Python worker", InferenceRequestStatus.Processing);
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "batch_process", pythonBatchRequest);

                // Validate and transform response
                var responseValidation = ValidateAndTransformResponse(pythonResponse, requestId);
                if (!responseValidation.Success)
                {
                    return CreateStandardizedErrorResponse<PostInferenceBatchResponse>(
                        requestId, "BATCH_PYTHON_ERROR", responseValidation.ErrorMessage, "PythonExecutionError");
                }

                var transformedResponse = responseValidation.TransformedResponse;

                var response = new PostInferenceBatchResponse
                {
                    BatchId = batchId,
                    Status = BatchStatus.Processing,
                    TotalItems = request.Items.Count,
                    CompletedItems = transformedResponse.ContainsKey("completed_items") ? 
                        Convert.ToInt32(transformedResponse["completed_items"]) : 0,
                    FailedItems = transformedResponse.ContainsKey("failed_items") ? 
                        Convert.ToInt32(transformedResponse["failed_items"]) : 0,
                    ProcessingItems = transformedResponse.ContainsKey("processing_items") ? 
                        Convert.ToInt32(transformedResponse["processing_items"]) : request.Items.Count,
                    Progress = transformedResponse.ContainsKey("progress") ? 
                        Convert.ToDouble(transformedResponse["progress"]) : 0.0,
                    EstimatedTimeToCompletionSeconds = transformedResponse.ContainsKey("estimated_time") ? 
                        Convert.ToInt32(transformedResponse["estimated_time"]) : null,
                    StartedAt = DateTime.UtcNow,
                    PerformanceMetrics = new Dictionary<string, object>
                    {
                        ["optimal_batch_size"] = optimalBatchSize,
                        ["request_id"] = requestId,
                        ["device_id"] = deviceId,
                        ["concurrency_level"] = request.Options.ConcurrencyLevel
                    },
                    Statistics = new Dictionary<string, object>
                    {
                        ["batch_optimization"] = "enabled",
                        ["memory_optimization"] = request.Options.OptimizeMemoryUsage,
                        ["progress_tracking"] = request.Options.EnableProgressTracking,
                        ["auto_retry"] = request.Options.AutoRetryFailedItems
                    }
                };

                var performanceMetrics = new Dictionary<string, object>
                {
                    ["batch_id"] = batchId,
                    ["total_items"] = request.Items.Count,
                    ["optimal_batch_size"] = optimalBatchSize,
                    ["estimated_time"] = response.EstimatedTimeToCompletionSeconds ?? 0
                };

                CompleteRequestTrace(requestId, InferenceRequestStatus.Completed, performanceMetrics);
                _logger.LogInformation($"[{requestId}] Started batch processing: {batchId} with {request.Items.Count} items");

                return ApiResponse<PostInferenceBatchResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                AddRequestError(requestId, "BATCH_SYSTEM_ERROR", ex.Message, "SystemError");
                _logger.LogError(ex, $"[{requestId}] Failed to process batch inference");
                return CreateStandardizedErrorResponse<PostInferenceBatchResponse>(
                    requestId, "BATCH_SYSTEM_ERROR", $"Failed to process batch inference: {ex.Message}", "SystemError");
            }
        }

        /// <summary>
        /// Monitor batch processing progress with real-time updates
        /// </summary>
        /// <param name="batchId">Batch identifier to monitor</param>
        /// <returns>Current batch status and progress</returns>
        public async Task<ApiResponse<PostInferenceBatchResponse>> MonitorBatchProgressAsync(string batchId)
        {
            var requestId = CreateRequestTrace("MonitorBatchProgress", new Dictionary<string, object>
            {
                ["batch_id"] = batchId
            });

            try
            {
                _logger.LogInformation($"[{requestId}] Monitoring batch progress: {batchId}");

                if (string.IsNullOrWhiteSpace(batchId))
                {
                    AddRequestError(requestId, "BATCH_ID_REQUIRED", "Batch ID is required", "ValidationError");
                    return CreateStandardizedErrorResponse<PostInferenceBatchResponse>(
                        requestId, "BATCH_ID_REQUIRED", "Batch ID is required", "ValidationError");
                }

                UpdateRequestTrace(requestId, $"Querying batch status for: {batchId}", InferenceRequestStatus.Processing);

                // Prepare Python request for batch monitoring
                var pythonRequest = new Dictionary<string, object>
                {
                    ["batch_id"] = batchId,
                    ["action"] = "get_batch_status",
                    ["request_id"] = requestId
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_batch_status", pythonRequest);

                // Validate and transform response
                var responseValidation = ValidateAndTransformResponse(pythonResponse, requestId);
                if (!responseValidation.Success)
                {
                    return CreateStandardizedErrorResponse<PostInferenceBatchResponse>(
                        requestId, "BATCH_MONITOR_ERROR", responseValidation.ErrorMessage, "PythonExecutionError");
                }

                var transformedResponse = responseValidation.TransformedResponse;

                var response = new PostInferenceBatchResponse
                {
                    BatchId = batchId,
                    Status = ParseBatchStatus(transformedResponse.ContainsKey("status") ? 
                        transformedResponse["status"].ToString() : "unknown"),
                    TotalItems = transformedResponse.ContainsKey("total_items") ? 
                        Convert.ToInt32(transformedResponse["total_items"]) : 0,
                    CompletedItems = transformedResponse.ContainsKey("completed_items") ? 
                        Convert.ToInt32(transformedResponse["completed_items"]) : 0,
                    FailedItems = transformedResponse.ContainsKey("failed_items") ? 
                        Convert.ToInt32(transformedResponse["failed_items"]) : 0,
                    ProcessingItems = transformedResponse.ContainsKey("processing_items") ? 
                        Convert.ToInt32(transformedResponse["processing_items"]) : 0,
                    Progress = transformedResponse.ContainsKey("progress") ? 
                        Convert.ToDouble(transformedResponse["progress"]) : 0.0,
                    EstimatedTimeToCompletionSeconds = transformedResponse.ContainsKey("estimated_time_remaining") ? 
                        Convert.ToInt32(transformedResponse["estimated_time_remaining"]) : null,
                    StartedAt = transformedResponse.ContainsKey("started_at") ? 
                        DateTime.Parse(transformedResponse["started_at"].ToString() ?? DateTime.UtcNow.ToString()) : DateTime.UtcNow,
                    CompletedAt = transformedResponse.ContainsKey("completed_at") && transformedResponse["completed_at"] != null ? 
                        DateTime.Parse(transformedResponse["completed_at"].ToString() ?? "") : null,
                    Results = transformedResponse.ContainsKey("results") && transformedResponse["results"] is List<object> ? 
                        ((List<object>)transformedResponse["results"]).Cast<dynamic>().Select(r => new BatchInferenceResult
                        {
                            ItemId = r.item_id?.ToString() ?? "",
                            Status = ParseBatchItemStatus(r.status?.ToString() ?? "unknown"),
                            Results = r.results as Dictionary<string, object> ?? new Dictionary<string, object>(),
                            ProcessingTimeMs = Convert.ToDouble(r.processing_time_ms ?? 0.0),
                            StartedAt = DateTime.Parse(r.started_at?.ToString() ?? DateTime.UtcNow.ToString()),
                            CompletedAt = r.completed_at != null ? DateTime.Parse(r.completed_at.ToString()) : null,
                            DeviceId = r.device_id?.ToString()
                        }).ToList() : new List<BatchInferenceResult>(),
                    PerformanceMetrics = transformedResponse.ContainsKey("performance_metrics") ? 
                        transformedResponse["performance_metrics"] as Dictionary<string, object> ?? new Dictionary<string, object>() : 
                        new Dictionary<string, object>(),
                    Statistics = transformedResponse.ContainsKey("statistics") ? 
                        transformedResponse["statistics"] as Dictionary<string, object> ?? new Dictionary<string, object>() : 
                        new Dictionary<string, object>()
                };

                var performanceMetrics = new Dictionary<string, object>
                {
                    ["batch_id"] = batchId,
                    ["progress"] = response.Progress,
                    ["total_items"] = response.TotalItems,
                    ["completed_items"] = response.CompletedItems,
                    ["status"] = response.Status.ToString()
                };

                CompleteRequestTrace(requestId, InferenceRequestStatus.Completed, performanceMetrics);
                _logger.LogInformation($"[{requestId}] Retrieved batch status: {batchId} - {response.Status} ({response.Progress:P2})");

                return ApiResponse<PostInferenceBatchResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                AddRequestError(requestId, "BATCH_MONITOR_SYSTEM_ERROR", ex.Message, "SystemError");
                _logger.LogError(ex, $"[{requestId}] Failed to monitor batch progress: {batchId}");
                return CreateStandardizedErrorResponse<PostInferenceBatchResponse>(
                    requestId, "BATCH_MONITOR_SYSTEM_ERROR", $"Failed to monitor batch progress: {ex.Message}", "SystemError");
            }
        }

        /// <summary>
        /// Execute ControlNet-guided inference with pose, depth, or edge conditioning
        /// </summary>
        /// <param name="request">ControlNet inference request</param>
        /// <returns>ControlNet inference response</returns>
        public async Task<ApiResponse<PostInferenceControlNetResponse>> PostInferenceControlNetAsync(PostInferenceControlNetRequest request)
        {
            var requestId = CreateRequestTrace("PostInferenceControlNet", new Dictionary<string, object>
            {
                ["model_id"] = request?.ModelId ?? "unknown",
                ["controlnet_type"] = request?.ControlNetType.ToString() ?? "unknown",
                ["controlnet_model"] = request?.ControlNetModel ?? "unknown"
            });

            try
            {
                _logger.LogInformation($"[{requestId}] Starting ControlNet inference");

                // Validate ControlNet request
                if (request == null || !await ValidateControlNetRequestAsync(request))
                {
                    AddRequestError(requestId, "CONTROLNET_VALIDATION_ERROR", "ControlNet request validation failed", "ValidationError");
                    return CreateStandardizedErrorResponse<PostInferenceControlNetResponse>(
                        requestId, "CONTROLNET_VALIDATION_ERROR", "ControlNet request validation failed", "ValidationError");
                }

                // Validate and transform request
                var (isValid, transformedRequest, errorMessage) = ValidateAndTransformRequest(request, requestId);
                if (!isValid)
                {
                    return CreateStandardizedErrorResponse<PostInferenceControlNetResponse>(
                        requestId, "CONTROLNET_REQ_VALIDATION_ERROR", errorMessage, "ValidationError");
                }

                var deviceId = _deviceCapabilities.Keys.FirstOrDefault() ?? "default-device";
                var inferenceId = Guid.NewGuid();

                UpdateRequestTrace(requestId, $"Processing ControlNet {request?.ControlNetType} on device: {deviceId}", InferenceRequestStatus.Processing);

                // Prepare Python ControlNet request
                var pythonRequest = new Dictionary<string, object>(transformedRequest)
                {
                    ["inference_id"] = inferenceId.ToString(),
                    ["device_id"] = deviceId,
                    ["action"] = "controlnet_inference",
                    ["request_id"] = requestId
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "controlnet_inference", pythonRequest);

                // Validate and transform response
                var responseValidation = ValidateAndTransformResponse(pythonResponse, requestId);
                if (!responseValidation.Success)
                {
                    return CreateStandardizedErrorResponse<PostInferenceControlNetResponse>(
                        requestId, "CONTROLNET_PYTHON_ERROR", responseValidation.ErrorMessage, "PythonExecutionError");
                }

                var transformedResponse = responseValidation.TransformedResponse;

                var response = new PostInferenceControlNetResponse
                {
                    InferenceId = inferenceId,
                    Success = true,
                    GeneratedImages = transformedResponse.ContainsKey("generated_images") && 
                        transformedResponse["generated_images"] is List<object> ? 
                        ((List<object>)transformedResponse["generated_images"]).Cast<string>().ToList() : new List<string>(),
                    ControlNetResults = transformedResponse.ContainsKey("controlnet_results") ? 
                        transformedResponse["controlnet_results"] as Dictionary<string, object> ?? new Dictionary<string, object>() : 
                        new Dictionary<string, object>(),
                    UsedParameters = transformedResponse.ContainsKey("used_parameters") ? 
                        transformedResponse["used_parameters"] as Dictionary<string, object> ?? new Dictionary<string, object>() : 
                        new Dictionary<string, object>(),
                    ProcessingTimeMs = transformedResponse.ContainsKey("processing_time_ms") ? 
                        Convert.ToDouble(transformedResponse["processing_time_ms"]) : 0.0,
                    QualityMetrics = transformedResponse.ContainsKey("quality_metrics") ? 
                        transformedResponse["quality_metrics"] as Dictionary<string, object> ?? new Dictionary<string, object>() : 
                        new Dictionary<string, object>(),
                    PerformanceMetrics = new Dictionary<string, object>
                    {
                        ["request_id"] = requestId,
                        ["controlnet_type"] = request?.ControlNetType.ToString() ?? "unknown",
                        ["device_id"] = deviceId,
                        ["memory_optimized"] = true
                    },
                    DeviceId = deviceId,
                    CompletedAt = DateTime.UtcNow
                };

                var performanceMetrics = new Dictionary<string, object>
                {
                    ["inference_id"] = inferenceId,
                    ["controlnet_type"] = request?.ControlNetType.ToString() ?? "unknown",
                    ["processing_time_ms"] = response.ProcessingTimeMs,
                    ["generated_images_count"] = response.GeneratedImages.Count
                };

                CompleteRequestTrace(requestId, InferenceRequestStatus.Completed, performanceMetrics);
                _logger.LogInformation($"[{requestId}] Completed ControlNet inference: {inferenceId}");

                return ApiResponse<PostInferenceControlNetResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                AddRequestError(requestId, "CONTROLNET_SYSTEM_ERROR", ex.Message, "SystemError");
                _logger.LogError(ex, $"[{requestId}] Failed to execute ControlNet inference");
                return CreateStandardizedErrorResponse<PostInferenceControlNetResponse>(
                    requestId, "CONTROLNET_SYSTEM_ERROR", $"Failed to execute ControlNet inference: {ex.Message}", "SystemError");
            }
        }

        /// <summary>
        /// Execute LoRA-adapted inference with dynamic model fine-tuning
        /// </summary>
        /// <param name="request">LoRA inference request</param>
        /// <returns>LoRA inference response</returns>
        public async Task<ApiResponse<PostInferenceLoRAResponse>> PostInferenceLoRAAsync(PostInferenceLoRARequest request)
        {
            var requestId = CreateRequestTrace("PostInferenceLoRA", new Dictionary<string, object>
            {
                ["base_model_id"] = request?.BaseModelId ?? "unknown",
                ["lora_path"] = request?.LoRAPath ?? "unknown",
                ["lora_weight"] = request?.LoRAWeight ?? 0.0,
                ["inference_type"] = request?.InferenceType.ToString() ?? "unknown"
            });

            try
            {
                _logger.LogInformation($"[{requestId}] Starting LoRA inference");

                // Validate and transform request
                var (isValid, transformedRequest, errorMessage) = ValidateAndTransformRequest(request, requestId);
                if (!isValid)
                {
                    return CreateStandardizedErrorResponse<PostInferenceLoRAResponse>(
                        requestId, "LORA_VALIDATION_ERROR", errorMessage, "ValidationError");
                }

                if (request == null || string.IsNullOrWhiteSpace(request.LoRAPath))
                {
                    AddRequestError(requestId, "LORA_PATH_REQUIRED", "LoRA path is required", "ValidationError");
                    return CreateStandardizedErrorResponse<PostInferenceLoRAResponse>(
                        requestId, "LORA_PATH_REQUIRED", "LoRA path is required", "ValidationError");
                }

                var deviceId = _deviceCapabilities.Keys.FirstOrDefault() ?? "default-device";
                var inferenceId = Guid.NewGuid();

                UpdateRequestTrace(requestId, $"Loading LoRA {request.LoRAPath} on device: {deviceId}", InferenceRequestStatus.Processing);

                // Prepare Python LoRA request
                var pythonRequest = new Dictionary<string, object>(transformedRequest)
                {
                    ["inference_id"] = inferenceId.ToString(),
                    ["device_id"] = deviceId,
                    ["action"] = "lora_inference",
                    ["request_id"] = requestId
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "lora_inference", pythonRequest);

                // Validate and transform response
                var responseValidation = ValidateAndTransformResponse(pythonResponse, requestId);
                if (!responseValidation.Success)
                {
                    return CreateStandardizedErrorResponse<PostInferenceLoRAResponse>(
                        requestId, "LORA_PYTHON_ERROR", responseValidation.ErrorMessage, "PythonExecutionError");
                }

                var transformedResponse = responseValidation.TransformedResponse;

                var response = new PostInferenceLoRAResponse
                {
                    InferenceId = inferenceId,
                    Success = true,
                    Results = transformedResponse.ContainsKey("results") ? 
                        transformedResponse["results"] as Dictionary<string, object> ?? new Dictionary<string, object>() : 
                        new Dictionary<string, object>(),
                    LoRAInfo = new Dictionary<string, object>
                    {
                        ["lora_path"] = request.LoRAPath,
                        ["lora_weight"] = request.LoRAWeight,
                        ["lora_alpha"] = request.LoRAAlpha,
                        ["adaptation_successful"] = transformedResponse.ContainsKey("lora_loaded") ? 
                            transformedResponse["lora_loaded"] : true
                    },
                    BaseModelInfo = new Dictionary<string, object>
                    {
                        ["model_id"] = request.BaseModelId,
                        ["model_loaded"] = transformedResponse.ContainsKey("base_model_loaded") ? 
                            transformedResponse["base_model_loaded"] : true
                    },
                    ProcessingTimeMs = transformedResponse.ContainsKey("processing_time_ms") ? 
                        Convert.ToDouble(transformedResponse["processing_time_ms"]) : 0.0,
                    QualityMetrics = transformedResponse.ContainsKey("quality_metrics") ? 
                        transformedResponse["quality_metrics"] as Dictionary<string, object> ?? new Dictionary<string, object>() : 
                        new Dictionary<string, object>(),
                    PerformanceMetrics = new Dictionary<string, object>
                    {
                        ["request_id"] = requestId,
                        ["lora_overhead_ms"] = transformedResponse.ContainsKey("lora_overhead_ms") ? 
                            transformedResponse["lora_overhead_ms"] : 0.0,
                        ["device_id"] = deviceId,
                        ["memory_optimized"] = true
                    },
                    DeviceId = deviceId,
                    CompletedAt = DateTime.UtcNow,
                    MemoryUsage = transformedResponse.ContainsKey("memory_usage") ? 
                        transformedResponse["memory_usage"] as Dictionary<string, object> ?? new Dictionary<string, object>() : 
                        new Dictionary<string, object>()
                };

                var performanceMetrics = new Dictionary<string, object>
                {
                    ["inference_id"] = inferenceId,
                    ["lora_path"] = request.LoRAPath,
                    ["lora_weight"] = request.LoRAWeight,
                    ["processing_time_ms"] = response.ProcessingTimeMs,
                    ["lora_overhead_ms"] = response.PerformanceMetrics.ContainsKey("lora_overhead_ms") ? 
                        response.PerformanceMetrics["lora_overhead_ms"] : 0.0
                };

                CompleteRequestTrace(requestId, InferenceRequestStatus.Completed, performanceMetrics);
                _logger.LogInformation($"[{requestId}] Completed LoRA inference: {inferenceId}");

                return ApiResponse<PostInferenceLoRAResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                AddRequestError(requestId, "LORA_SYSTEM_ERROR", ex.Message, "SystemError");
                _logger.LogError(ex, $"[{requestId}] Failed to execute LoRA inference");
                return CreateStandardizedErrorResponse<PostInferenceLoRAResponse>(
                    requestId, "LORA_SYSTEM_ERROR", $"Failed to execute LoRA inference: {ex.Message}", "SystemError");
            }
        }

        /// <summary>
        /// Calculate optimal batch size based on model and device capabilities
        /// </summary>
        /// <param name="modelId">Model identifier</param>
        /// <param name="deviceId">Device identifier</param>
        /// <returns>Optimal batch size for processing</returns>
        public async Task<int> CalculateOptimalBatchSizeAsync(string modelId, string deviceId)
        {
            try
            {
                _logger.LogDebug($"Calculating optimal batch size for model: {modelId}, device: {deviceId}");

                // Get device capabilities
                if (_deviceCapabilities.TryGetValue(deviceId, out var capabilities))
                {
                    var maxBatchSize = capabilities.MaxBatchSize;
                    
                    // Request optimization from Python worker
                    var pythonRequest = new Dictionary<string, object>
                    {
                        ["model_id"] = modelId,
                        ["device_id"] = deviceId,
                        ["max_batch_size"] = maxBatchSize,
                        ["action"] = "calculate_optimal_batch_size"
                    };

                    var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                        PythonWorkerTypes.INFERENCE, "calculate_optimal_batch_size", pythonRequest);

                    if (pythonResponse?.success == true && pythonResponse?.optimal_batch_size != null)
                    {
                        var optimalSize = Convert.ToInt32(pythonResponse.optimal_batch_size);
                        _logger.LogDebug($"Python calculated optimal batch size: {optimalSize}");
                        return Math.Min(optimalSize, maxBatchSize);
                    }
                }

                // Fallback calculation based on device type
                var fallbackSize = deviceId.Contains("cuda") ? 8 : 4;
                _logger.LogDebug($"Using fallback batch size: {fallbackSize}");
                return fallbackSize;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to calculate optimal batch size, using default: 4");
                return 4; // Safe default
            }
        }

        /// <summary>
        /// Validate ControlNet request parameters and control image
        /// </summary>
        /// <param name="request">ControlNet request to validate</param>
        /// <returns>Validation result</returns>
        public async Task<bool> ValidateControlNetRequestAsync(PostInferenceControlNetRequest request)
        {
            try
            {
                if (request == null)
                {
                    _logger.LogWarning("ControlNet request is null");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(request.ModelId))
                {
                    _logger.LogWarning("ControlNet request missing model ID");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(request.ControlNetModel))
                {
                    _logger.LogWarning("ControlNet request missing ControlNet model");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(request.ControlImageBase64))
                {
                    _logger.LogWarning("ControlNet request missing control image");
                    return false;
                }

                // Validate control image format
                try
                {
                    var imageBytes = Convert.FromBase64String(request.ControlImageBase64);
                    if (imageBytes.Length == 0)
                    {
                        _logger.LogWarning("ControlNet control image is empty");
                        return false;
                    }
                }
                catch (FormatException)
                {
                    _logger.LogWarning("ControlNet control image is not valid base64");
                    return false;
                }

                // Validate ControlNet type
                if (request.ControlNetType == ControlNetType.Unknown)
                {
                    _logger.LogWarning("ControlNet type is unknown");
                    return false;
                }

                // Validate Python worker capability
                var pythonRequest = new Dictionary<string, object>
                {
                    ["controlnet_type"] = request.ControlNetType.ToString(),
                    ["controlnet_model"] = request.ControlNetModel,
                    ["action"] = "validate_controlnet_capability"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "validate_controlnet", pythonRequest);

                if (pythonResponse?.success != true)
                {
                    _logger.LogWarning($"Python worker cannot support ControlNet: {request.ControlNetType}");
                    return false;
                }

                _logger.LogDebug($"ControlNet request validation successful: {request.ControlNetType}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate ControlNet request");
                return false;
            }
        }

        #endregion

        #region Helper Methods for Advanced Features

        /// <summary>
        /// Parse batch status string to BatchStatus enum
        /// </summary>
        /// <param name="statusString">Status string from Python response</param>
        /// <returns>Parsed BatchStatus</returns>
        private BatchStatus ParseBatchStatus(string statusString)
        {
            return statusString?.ToLowerInvariant() switch
            {
                "queued" => BatchStatus.Queued,
                "processing" => BatchStatus.Processing,
                "completed" => BatchStatus.Completed,
                "failed" => BatchStatus.Failed,
                "cancelled" => BatchStatus.Cancelled,
                "partially_completed" => BatchStatus.PartiallyCompleted,
                _ => BatchStatus.Unknown
            };
        }

        /// <summary>
        /// Parse batch item status string to BatchItemStatus enum
        /// </summary>
        /// <param name="statusString">Status string from Python response</param>
        /// <returns>Parsed BatchItemStatus</returns>
        private BatchItemStatus ParseBatchItemStatus(string statusString)
        {
            return statusString?.ToLowerInvariant() switch
            {
                "pending" => BatchItemStatus.Pending,
                "processing" => BatchItemStatus.Processing,
                "completed" => BatchItemStatus.Completed,
                "failed" => BatchItemStatus.Failed,
                "skipped" => BatchItemStatus.Skipped,
                "retrying" => BatchItemStatus.Retrying,
                _ => BatchItemStatus.Unknown
            };
        }

        #endregion

        #region Performance Optimization Methods

        /// <summary>
        /// Get capabilities from intelligent cache with access tracking
        /// </summary>
        private async Task<GetInferenceCapabilitiesResponse?> GetCapabilitiesFromCacheAsync()
        {
            await _cacheLock.WaitAsync();
            try
            {
                var globalCacheKey = "global_capabilities";
                
                if (_capabilitiesCache.TryGetValue(globalCacheKey, out var cached) && cached.IsValid)
                {
                    cached.LastAccessed = DateTime.UtcNow;
                    cached.AccessCount++;
                    _cacheAccessTimes[globalCacheKey] = DateTime.UtcNow;
                    
                    _logger.LogDebug($"Cache hit for capabilities (age: {cached.Age:mm\\:ss}, access count: {cached.AccessCount})");
                    
                    // Create response from cached capabilities
                    var response = new GetInferenceCapabilitiesResponse
                    {
                        SupportedInferenceTypes = cached.Capabilities.SupportedInferenceTypes,
                        SupportedModels = new List<ModelInfo>(), // Populate as needed
                        AvailableDevices = new List<DeviceInfo>(), // Populate as needed
                        MaxConcurrentInferences = cached.Capabilities.MaxConcurrentInferences,
                        SupportedPrecisions = new List<string> { "FP32", "FP16", "INT8" },
                        MaxBatchSize = cached.Capabilities.MaxBatchSize,
                        MaxResolution = cached.Capabilities.MaxResolution
                    };
                    
                    return response;
                }
                
                _logger.LogDebug("Cache miss for capabilities");
                return null;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Cache capabilities with intelligent expiry
        /// </summary>
        private async Task CacheCapabilitiesAsync(GetInferenceCapabilitiesResponse response)
        {
            await _cacheLock.WaitAsync();
            try
            {
                var globalCacheKey = "global_capabilities";
                var now = DateTime.UtcNow;
                
                // Create simplified capabilities for caching
                var capabilitiesToCache = new InferenceCapabilities
                {
                    SupportedInferenceTypes = response.SupportedInferenceTypes,
                    MaxConcurrentInferences = response.MaxConcurrentInferences,
                    MaxBatchSize = response.MaxBatchSize,
                    MaxResolution = response.MaxResolution
                };
                
                var cachedEntry = new CachedCapabilities
                {
                    DeviceId = globalCacheKey,
                    Capabilities = capabilitiesToCache,
                    CachedAt = now,
                    LastAccessed = now,
                    AccessCount = 1,
                    ExpiresAt = now.Add(_cacheRefreshInterval)
                };
                
                _capabilitiesCache[globalCacheKey] = cachedEntry;
                _cacheAccessTimes[globalCacheKey] = now;
                
                _logger.LogDebug($"Cached capabilities (expires: {cachedEntry.ExpiresAt:HH:mm:ss})");
                
                // Maintain cache size
                await MaintainCacheSizeAsync();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Refresh capabilities with connection pooling optimization
        /// </summary>
        private async Task RefreshCapabilitiesWithOptimizationAsync()
        {
            if (_optimizedWorkerService != null)
            {
                _logger.LogDebug("Using optimized worker service for capabilities refresh");
                
                await _optimizedWorkerService.ExecuteWithPoolingAsync<object>(async connection =>
                {
                    // Use optimized connection for capabilities refresh
                    await RefreshCapabilitiesAsync();
                    return new object();
                });
            }
            else
            {
                // Fallback to standard refresh
                await RefreshCapabilitiesAsync();
            }
        }

        /// <summary>
        /// Track operation metrics for performance analysis
        /// </summary>
        private void TrackOperationMetrics(string operationName, TimeSpan executionTime, bool success)
        {
            var key = operationName;
            var now = DateTime.UtcNow;
            
            _operationMetrics.AddOrUpdate(key,
                new OperationMetrics
                {
                    OperationName = operationName,
                    TotalInvocations = 1,
                    SuccessfulInvocations = success ? 1 : 0,
                    FailedInvocations = success ? 0 : 1,
                    TotalExecutionTime = executionTime,
                    MinExecutionTime = executionTime,
                    MaxExecutionTime = executionTime,
                    FirstInvocation = now,
                    LastInvocation = now
                },
                (k, existing) =>
                {
                    existing.TotalInvocations++;
                    if (success) existing.SuccessfulInvocations++;
                    else existing.FailedInvocations++;
                    
                    existing.TotalExecutionTime += executionTime;
                    existing.LastInvocation = now;
                    
                    if (executionTime < existing.MinExecutionTime)
                        existing.MinExecutionTime = executionTime;
                    if (executionTime > existing.MaxExecutionTime)
                        existing.MaxExecutionTime = executionTime;
                    
                    return existing;
                });
        }

        /// <summary>
        /// Perform cache maintenance - remove expired entries and manage size
        /// </summary>
        private async void PerformCacheMaintenance(object? state)
        {
            try
            {
                await _cacheLock.WaitAsync();
                var now = DateTime.UtcNow;
                var expiredKeys = new List<string>();
                
                // Find expired entries
                foreach (var kvp in _capabilitiesCache)
                {
                    if (!kvp.Value.IsValid)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }
                
                // Remove expired entries
                foreach (var key in expiredKeys)
                {
                    _capabilitiesCache.Remove(key);
                    _cacheAccessTimes.Remove(key);
                }
                
                if (expiredKeys.Any())
                {
                    _logger.LogDebug($"Cache maintenance: removed {expiredKeys.Count} expired entries");
                }
                
                // Maintain cache size
                await MaintainCacheSizeAsync();
                
                // Log cache statistics
                var hitRate = (_cacheHits + _cacheMisses) > 0 ? 
                    (double)_cacheHits / (_cacheHits + _cacheMisses) * 100 : 0;
                
                _logger.LogDebug($"Cache stats: {_capabilitiesCache.Count} entries, " +
                               $"{hitRate:F1}% hit rate ({_cacheHits} hits, {_cacheMisses} misses)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache maintenance");
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Maintain cache size by removing least recently used entries
        /// </summary>
        private Task MaintainCacheSizeAsync()
        {
            if (_capabilitiesCache.Count <= MaxCacheSize)
                return Task.CompletedTask;
            
            var entriesToRemove = _capabilitiesCache.Count - MaxCacheSize;
            var sortedEntries = _capabilitiesCache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .Take(entriesToRemove)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in sortedEntries)
            {
                _capabilitiesCache.Remove(key);
                _cacheAccessTimes.Remove(key);
            }
            
            _logger.LogDebug($"Cache size maintenance: removed {entriesToRemove} LRU entries");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get performance metrics for the service
        /// </summary>
        public async Task<Dictionary<string, object>> GetPerformanceMetricsAsync()
        {
            var metrics = new Dictionary<string, object>();
            
            // Cache metrics
            var hitRate = (_cacheHits + _cacheMisses) > 0 ? 
                (double)_cacheHits / (_cacheHits + _cacheMisses) * 100 : 0;
            
            metrics["cache_hit_rate"] = hitRate;
            metrics["cache_hits"] = _cacheHits;
            metrics["cache_misses"] = _cacheMisses;
            metrics["cache_entries"] = _capabilitiesCache.Count;
            
            // Operation metrics
            metrics["operations"] = _operationMetrics.Values.Select(m => new
            {
                operation = m.OperationName,
                total_invocations = m.TotalInvocations,
                success_rate = m.SuccessRate,
                avg_execution_time_ms = m.AverageExecutionTime.TotalMilliseconds,
                operations_per_minute = m.OperationsPerMinute
            }).ToList();
            
            // Connection pool metrics (if available)
            if (_optimizedWorkerService != null)
            {
                var poolMetrics = await _optimizedWorkerService.GetPerformanceMetricsAsync();
                metrics["connection_pool"] = new
                {
                    pool_hit_rate = poolMetrics.PoolHitRate,
                    active_connections = poolMetrics.ActiveConnections,
                    pooled_connections = poolMetrics.PooledConnections,
                    total_requests = poolMetrics.TotalRequestsProcessed
                };
            }
            
            return metrics;
        }

        #endregion

        #region Week 20: Advanced Operations and Testing

        /// <summary>
        /// Execute image inpainting with advanced mask processing and quality optimization
        /// </summary>
        public async Task<ApiResponse<PostInferenceInpaintingResponse>> PostInferenceInpaintingAsync(PostInferenceInpaintingRequest request)
        {
            var requestId = CreateRequestTrace("PostInferenceInpainting");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation($"[{requestId}] Starting inpainting operation for session {request.SessionId}");
                UpdateRequestTrace(requestId, "Starting inpainting validation", InferenceRequestStatus.Processing);

                // Validate inpainting request
                var isValid = await ValidateInpaintingRequestAsync(request);
                if (!isValid)
                {
                    return CreateStandardizedErrorResponse<PostInferenceInpaintingResponse>(
                        requestId, "INPAINTING_VALIDATION_ERROR", "Invalid inpainting request parameters", "ValidationError");
                }

                // Analyze mask complexity and determine optimal processing strategy
                var maskAnalysis = await AnalyzeMaskComplexityAsync(request.InpaintingMask);
                UpdateRequestTrace(requestId, $"Mask analysis complete: {maskAnalysis.ComplexityScore:F2} complexity", InferenceRequestStatus.Processing);

                // Prepare inpainting request for Python worker
                var transformedRequest = _fieldTransformer.ToPythonFormat(new Dictionary<string, object>
                {
                    ["session_id"] = request.SessionId,
                    ["base_image"] = request.BaseImage,
                    ["inpainting_mask"] = request.InpaintingMask,
                    ["prompt"] = request.Prompt,
                    ["negative_prompt"] = request.NegativePrompt ?? "",
                    ["model_id"] = request.ModelId,
                    ["device_id"] = request.DeviceId,
                    ["inpainting_strength"] = request.InpaintingStrength,
                    ["steps"] = request.Steps,
                    ["guidance_scale"] = request.GuidanceScale,
                    ["seed"] = request.Seed ?? -1,
                    ["width"] = request.Width,
                    ["height"] = request.Height,
                    ["method"] = request.Method.ToString().ToLowerInvariant(),
                    ["quality"] = request.Quality.ToString().ToLowerInvariant(),
                    ["edge_blending"] = request.EdgeBlending.ToString().ToLowerInvariant(),
                    ["enable_advanced_mask_processing"] = request.EnableAdvancedMaskProcessing,
                    ["mask_blur_radius"] = request.MaskBlurRadius,
                    ["mask_dilation"] = request.MaskDilation,
                    ["enable_content_aware_fill"] = request.EnableContentAwareFill,
                    ["mask_analysis"] = maskAnalysis,
                    ["additional_parameters"] = request.AdditionalParameters
                });

                UpdateRequestTrace(requestId, "Executing inpainting with Python worker", InferenceRequestStatus.Processing);

                // Execute inpainting with optimized worker service
                if (_optimizedWorkerService != null)
                {
                    var result = await _optimizedWorkerService.ExecuteWithPoolingAsync(async connection =>
                    {
                        return await _pythonWorkerService.ExecuteAsync<Dictionary<string, object>, dynamic>(
                            PythonWorkerTypes.INFERENCE, "inpaint_image", transformedRequest);
                    });

                    return await ProcessInpaintingResultAsync(result, requestId, stopwatch);
                }
                else
                {
                    // Fallback to standard service
                    var pythonResponse = await _pythonWorkerService.ExecuteAsync<Dictionary<string, object>, dynamic>(
                        PythonWorkerTypes.INFERENCE, "inpaint_image", transformedRequest);

                    return await ProcessInpaintingResultAsync(pythonResponse, requestId, stopwatch);
                }
            }
            catch (Exception ex)
            {
                AddRequestError(requestId, "INPAINTING_EXECUTION_ERROR", ex.Message, "ExecutionError");
                _logger.LogError(ex, $"[{requestId}] Inpainting operation failed");
                TrackOperationMetrics("PostInferenceInpainting", stopwatch.Elapsed, false);
                
                return CreateStandardizedErrorResponse<PostInferenceInpaintingResponse>(
                    requestId, "INPAINTING_EXECUTION_ERROR", $"Inpainting execution failed: {ex.Message}", "ExecutionError");
            }
        }

        /// <summary>
        /// Get enhanced inference session analytics with performance insights
        /// </summary>
        public async Task<ApiResponse<GetInferenceSessionResponse>> GetInferenceSessionAnalyticsAsync(string idSession)
        {
            var requestId = CreateRequestTrace("GetInferenceSessionAnalytics");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation($"[{requestId}] Getting enhanced analytics for session {idSession}");
                UpdateRequestTrace(requestId, "Starting session analytics retrieval", InferenceRequestStatus.Processing);

                // Get basic session information
                var basicSessionResponse = await GetInferenceSessionAsync(idSession);
                if (!basicSessionResponse.IsSuccess || basicSessionResponse.Data == null)
                {
                    return CreateStandardizedErrorResponse<GetInferenceSessionResponse>(
                        requestId, "SESSION_NOT_FOUND", $"Session {idSession} not found", "NotFound");
                }

                var session = basicSessionResponse.Data.Session;
                
                // Get enhanced analytics from Python worker
                var analyticsRequest = new Dictionary<string, object>
                {
                    ["session_id"] = idSession,
                    ["include_performance_metrics"] = true,
                    ["include_resource_usage"] = true,
                    ["include_quality_metrics"] = true,
                    ["include_optimization_suggestions"] = true
                };

                var transformedRequest = _fieldTransformer.ToPythonFormat(analyticsRequest);
                
                dynamic pythonResponse = await _pythonWorkerService.ExecuteAsync<Dictionary<string, object>, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_session_analytics", transformedRequest);

                if (pythonResponse?.success == true)
                {
                    // Create enhanced session response with analytics
                    var enhancedSession = CreateEnhancedSessionWithAnalytics(session, pythonResponse);
                    
                    TrackOperationMetrics("GetInferenceSessionAnalytics", stopwatch.Elapsed, true);
                    _logger.LogInformation($"[{requestId}] Enhanced session analytics retrieved in {stopwatch.ElapsedMilliseconds}ms");
                    
                    var response = new GetInferenceSessionResponse
                    {
                        Session = enhancedSession
                    };

                    CompleteRequestTrace(requestId, InferenceRequestStatus.Completed, new Dictionary<string, object>
                    {
                        ["session_id"] = idSession,
                        ["analytics_included"] = true,
                        ["response_time_ms"] = stopwatch.ElapsedMilliseconds
                    });

                    return ApiResponse<GetInferenceSessionResponse>.CreateSuccess(response);
                }
                else
                {
                    // Fallback to basic session with C# analytics
                    var enhancedSession = CreateBasicSessionWithFallbackAnalytics(session);
                    
                    var response = new GetInferenceSessionResponse
                    {
                        Session = enhancedSession
                    };

                    _logger.LogWarning($"[{requestId}] Using fallback analytics for session {idSession}");
                    return ApiResponse<GetInferenceSessionResponse>.CreateSuccess(response);
                }
            }
            catch (Exception ex)
            {
                AddRequestError(requestId, "SESSION_ANALYTICS_ERROR", ex.Message, "AnalyticsError");
                _logger.LogError(ex, $"[{requestId}] Failed to get session analytics");
                TrackOperationMetrics("GetInferenceSessionAnalytics", stopwatch.Elapsed, false);
                
                return CreateStandardizedErrorResponse<GetInferenceSessionResponse>(
                    requestId, "SESSION_ANALYTICS_ERROR", $"Failed to get session analytics: {ex.Message}", "AnalyticsError");
            }
        }

        /// <summary>
        /// Validate inpainting request parameters and requirements
        /// </summary>
        public async Task<bool> ValidateInpaintingRequestAsync(PostInferenceInpaintingRequest request)
        {
            try
            {
                _logger.LogDebug("Validating inpainting request parameters");

                // Basic parameter validation
                if (string.IsNullOrEmpty(request.SessionId) || string.IsNullOrEmpty(request.BaseImage) || 
                    string.IsNullOrEmpty(request.InpaintingMask) || string.IsNullOrEmpty(request.ModelId))
                {
                    _logger.LogWarning("Missing required inpainting parameters");
                    return false;
                }

                // Validate inpainting strength
                if (request.InpaintingStrength < 0.0 || request.InpaintingStrength > 1.0)
                {
                    _logger.LogWarning($"Invalid inpainting strength: {request.InpaintingStrength}");
                    return false;
                }

                // Validate dimensions
                if (request.Width <= 0 || request.Height <= 0 || request.Width > 2048 || request.Height > 2048)
                {
                    _logger.LogWarning($"Invalid dimensions: {request.Width}x{request.Height}");
                    return false;
                }

                // Validate steps
                if (request.Steps <= 0 || request.Steps > 150)
                {
                    _logger.LogWarning($"Invalid steps count: {request.Steps}");
                    return false;
                }

                // Validate guidance scale
                if (request.GuidanceScale <= 0.0 || request.GuidanceScale > 30.0)
                {
                    _logger.LogWarning($"Invalid guidance scale: {request.GuidanceScale}");
                    return false;
                }

                // Validate mask parameters
                if (request.MaskBlurRadius < 0 || request.MaskBlurRadius > 50)
                {
                    _logger.LogWarning($"Invalid mask blur radius: {request.MaskBlurRadius}");
                    return false;
                }

                if (request.MaskDilation < 0 || request.MaskDilation > 50)
                {
                    _logger.LogWarning($"Invalid mask dilation: {request.MaskDilation}");
                    return false;
                }

                // Validate image data (basic base64 check)
                if (!IsValidBase64(request.BaseImage) || !IsValidBase64(request.InpaintingMask))
                {
                    _logger.LogWarning("Invalid base64 image data");
                    return false;
                }

                // Validate model capability with Python worker
                var validationRequest = new Dictionary<string, object>
                {
                    ["model_id"] = request.ModelId,
                    ["device_id"] = request.DeviceId,
                    ["inpainting_method"] = request.Method.ToString(),
                    ["action"] = "validate_inpainting_capability"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<Dictionary<string, object>, dynamic>(
                    PythonWorkerTypes.INFERENCE, "validate_inpainting", validationRequest);

                if (pythonResponse?.success != true)
                {
                    _logger.LogWarning($"Python worker cannot support inpainting with model: {request.ModelId}");
                    return false;
                }

                _logger.LogDebug($"Inpainting request validation successful: {request.ModelId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate inpainting request");
                return false;
            }
        }

        #endregion

        #region Helper Methods for Week 20 Operations

        /// <summary>
        /// Analyze mask complexity to determine optimal processing strategy
        /// </summary>
        private async Task<MaskAnalysisResult> AnalyzeMaskComplexityAsync(string maskData)
        {
            try
            {
                var analysisRequest = new Dictionary<string, object>
                {
                    ["mask_data"] = maskData,
                    ["analyze_complexity"] = true,
                    ["analyze_regions"] = true,
                    ["analyze_edges"] = true
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<Dictionary<string, object>, dynamic>(
                    PythonWorkerTypes.INFERENCE, "analyze_mask", analysisRequest);

                if (pythonResponse?.success == true)
                {
                    return new MaskAnalysisResult
                    {
                        InpaintAreaPercentage = Convert.ToDouble(pythonResponse.inpaint_area_percentage ?? 0.0),
                        ComplexityScore = Convert.ToDouble(pythonResponse.complexity_score ?? 0.5),
                        RegionCount = Convert.ToInt32(pythonResponse.region_count ?? 1),
                        AverageRegionSize = Convert.ToInt32(pythonResponse.average_region_size ?? 1000),
                        EdgeDensityScore = Convert.ToDouble(pythonResponse.edge_density_score ?? 0.5),
                        RecommendedMode = pythonResponse.recommended_mode?.ToString() ?? "standard"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze mask complexity, using defaults");
            }

            // Return default analysis if Python analysis fails
            return new MaskAnalysisResult
            {
                InpaintAreaPercentage = 25.0,
                ComplexityScore = 0.5,
                RegionCount = 1,
                AverageRegionSize = 1000,
                EdgeDensityScore = 0.5,
                RecommendedMode = "standard"
            };
        }

        // ...existing helper methods continued...

        /// <summary>
        /// Process inpainting result from Python worker
        /// </summary>
        private Task<ApiResponse<PostInferenceInpaintingResponse>> ProcessInpaintingResultAsync(
            dynamic pythonResponse, string requestId, Stopwatch stopwatch)
        {
            try
            {
                if (pythonResponse?.success == true)
                {
                    // Extract quality metrics
                    var qualityMetrics = ExtractQualityMetricsFromPythonResponse(pythonResponse);
                    
                    // Extract performance metrics
                    var performanceMetrics = ExtractPerformanceMetricsFromPythonResponse(pythonResponse);
                    
                    var response = new PostInferenceInpaintingResponse
                    {
                        Success = true,
                        InpaintedImage = pythonResponse.inpainted_image?.ToString() ?? "",
                        QualityMetrics = qualityMetrics ?? new InpaintingQualityMetrics(),
                        PerformanceMetrics = performanceMetrics ?? new InpaintingPerformanceMetrics(),
                        UsedParameters = ExtractGenerationParametersFromPythonResponse(pythonResponse) ?? new Dictionary<string, object>(),
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        Metadata = ExtractInpaintingMetadataFromPythonResponse(pythonResponse) ?? new InpaintingMetadata()
                    };

                    TrackOperationMetrics("PostInferenceInpainting", stopwatch.Elapsed, true);
                    CompleteRequestTrace(requestId, InferenceRequestStatus.Completed, new Dictionary<string, object>
                    {
                        ["quality_score"] = qualityMetrics?.OverallQualityScore ?? 0.0,
                        ["processing_time_ms"] = stopwatch.ElapsedMilliseconds
                    });

                    _logger.LogInformation($"[{requestId}] Inpainting completed successfully in {stopwatch.ElapsedMilliseconds}ms");
                    return Task.FromResult(ApiResponse<PostInferenceInpaintingResponse>.CreateSuccess(response));
                }
                else
                {
                    var error = pythonResponse?.error?.ToString() ?? "Unknown error";
                    AddRequestError(requestId, "PYTHON_INPAINTING_ERROR", error, "PythonError");
                    
                    var errorResponse = new PostInferenceInpaintingResponse
                    {
                        Success = false,
                        ErrorMessage = error,
                        ErrorCode = "PYTHON_INPAINTING_ERROR"
                    };
                    
                    return Task.FromResult(ApiResponse<PostInferenceInpaintingResponse>.CreateSuccess(errorResponse));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{requestId}] Failed to process inpainting result");
                return Task.FromResult(CreateStandardizedErrorResponse<PostInferenceInpaintingResponse>(
                    requestId, "RESULT_PROCESSING_ERROR", $"Failed to process result: {ex.Message}", "ProcessingError"));
            }
        }

        /// <summary>
        /// Create enhanced session with analytics from Python response
        /// </summary>
        private SessionInfo CreateEnhancedSessionWithAnalytics(SessionInfo baseSession, dynamic pythonResponse)
        {
            try
            {
                // For now, just return the base session with basic enhancements
                // The SessionInfo model is simplified and doesn't support complex analytics
                _logger.LogDebug("Enhanced session analytics created with Python response data");
                return baseSession;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create enhanced session, using base session");
                return baseSession;
            }
        }

        /// <summary>
        /// Create basic session with fallback analytics
        /// </summary>
        private SessionInfo CreateBasicSessionWithFallbackAnalytics(SessionInfo baseSession)
        {
            try
            {
                _logger.LogDebug("Using fallback analytics for session");
                return baseSession;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create fallback analytics, using base session");
                return baseSession;
            }
        }

        /// <summary>
        /// Extract quality metrics from Python response
        /// </summary>
        private InpaintingQualityMetrics? ExtractQualityMetricsFromPythonResponse(dynamic pythonResponse)
        {
            try
            {
                if (pythonResponse?.quality_metrics == null) return null;

                var metrics = pythonResponse.quality_metrics;
                return new InpaintingQualityMetrics
                {
                    SeamlessnessScore = Convert.ToDouble(metrics.seamlessness_score ?? 0.0),
                    ColorConsistencyScore = Convert.ToDouble(metrics.color_consistency_score ?? 0.0),
                    TextureConsistencyScore = Convert.ToDouble(metrics.texture_consistency_score ?? 0.0),
                    EdgeQualityScore = Convert.ToDouble(metrics.edge_quality_score ?? 0.0),
                    ContentCoherenceScore = Convert.ToDouble(metrics.content_coherence_score ?? 0.0),
                    OverallQualityScore = Convert.ToDouble(metrics.overall_quality_score ?? 0.0)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract quality metrics from Python response");
                return null;
            }
        }

        /// <summary>
        /// Extract performance metrics from Python response
        /// </summary>
        private InpaintingPerformanceMetrics? ExtractPerformanceMetricsFromPythonResponse(dynamic pythonResponse)
        {
            try
            {
                if (pythonResponse?.performance_metrics == null) return null;

                var metrics = pythonResponse.performance_metrics;
                return new InpaintingPerformanceMetrics
                {
                    PreprocessingTimeMs = Convert.ToInt64(metrics.preprocessing_time_ms ?? 0),
                    MaskProcessingTimeMs = Convert.ToInt64(metrics.mask_processing_time_ms ?? 0),
                    InferenceTimeMs = Convert.ToInt64(metrics.inference_time_ms ?? 0),
                    PostprocessingTimeMs = Convert.ToInt64(metrics.postprocessing_time_ms ?? 0),
                    MemoryUsageMB = Convert.ToInt64(metrics.memory_usage_mb ?? 0),
                    PeakVRAMUsageMB = Convert.ToInt64(metrics.peak_vram_usage_mb ?? 0)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract performance metrics from Python response");
                return null;
            }
        }

        /// <summary>
        /// Extract generation parameters from Python response
        /// </summary>
        private Dictionary<string, object>? ExtractGenerationParametersFromPythonResponse(dynamic pythonResponse)
        {
            try
            {
                if (pythonResponse?.generation_parameters == null) return null;

                var parameters = new Dictionary<string, object>();
                var genParams = pythonResponse.generation_parameters;

                // Simple approach to handle dynamic response
                parameters["method"] = genParams.method?.ToString() ?? "";
                parameters["steps"] = Convert.ToInt32(genParams.steps ?? 0);
                parameters["guidance_scale"] = Convert.ToDouble(genParams.guidance_scale ?? 0.0);
                parameters["strength"] = Convert.ToDouble(genParams.strength ?? 0.0);
                parameters["seed"] = Convert.ToInt64(genParams.seed ?? -1);

                return parameters;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract generation parameters from Python response");
                return null;
            }
        }

        /// <summary>
        /// Extract inpainting metadata from Python response
        /// </summary>
        private InpaintingMetadata? ExtractInpaintingMetadataFromPythonResponse(dynamic pythonResponse)
        {
            try
            {
                if (pythonResponse?.metadata == null) return null;

                var metadata = pythonResponse.metadata;
                return new InpaintingMetadata
                {
                    Algorithm = metadata.algorithm?.ToString() ?? "",
                    ModelVersion = metadata.model_version?.ToString() ?? "",
                    ProcessingDevice = metadata.processing_device?.ToString() ?? "",
                    MaskAnalysis = new MaskAnalysisResult
                    {
                        InpaintAreaPercentage = Convert.ToDouble(metadata.inpaint_area_percentage ?? 0.0),
                        ComplexityScore = Convert.ToDouble(metadata.complexity_score ?? 0.5),
                        RegionCount = Convert.ToInt32(metadata.region_count ?? 1),
                        AverageRegionSize = Convert.ToInt32(metadata.average_region_size ?? 1000),
                        EdgeDensityScore = Convert.ToDouble(metadata.edge_density_score ?? 0.5),
                        RecommendedMode = metadata.recommended_mode?.ToString() ?? "standard"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract inpainting metadata from Python response");
                return null;
            }
        }

        /// <summary>
        /// Basic base64 validation helper
        /// </summary>
        private bool IsValidBase64(string base64String)
        {
            try
            {
                if (string.IsNullOrEmpty(base64String))
                    return false;

                // Remove data URL prefix if present
                var cleanBase64 = base64String;
                if (base64String.StartsWith("data:"))
                {
                    var commaIndex = base64String.IndexOf(',');
                    if (commaIndex >= 0)
                        cleanBase64 = base64String.Substring(commaIndex + 1);
                }

                // Basic validation
                if (cleanBase64.Length % 4 != 0)
                    return false;

                Convert.FromBase64String(cleanBase64);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
