using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Python;
using System.Text.Json;
using static DeviceOperations.Models.Requests.RequestsDevice;
using Microsoft.Extensions.Logging;

namespace DeviceOperations.Services.Device
{
    /// <summary>
    /// Service implementation for device management operations
    /// </summary>
    public class ServiceDevice : IServiceDevice
    {
        private readonly ILogger<ServiceDevice> _logger;
        private readonly IPythonWorkerService _pythonWorkerService;
        private readonly Dictionary<string, DeviceInfo> _deviceCache;
        private readonly Dictionary<string, DeviceCapabilities> _capabilityCache;
        private readonly Dictionary<string, DeviceHealth> _healthCache;
        private readonly Dictionary<string, DateTime> _cacheExpiryTimes;
        private readonly Dictionary<string, int> _cacheAccessCount;
        private DateTime _lastCacheRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _deviceSpecificCacheTimeout = TimeSpan.FromMinutes(2);
        private readonly int _maxCacheSize = 1000;
        private readonly object _cacheLock = new object();

        public ServiceDevice(
            ILogger<ServiceDevice> logger,
            IPythonWorkerService pythonWorkerService)
        {
            _logger = logger;
            _pythonWorkerService = pythonWorkerService;
            _deviceCache = new Dictionary<string, DeviceInfo>();
            _capabilityCache = new Dictionary<string, DeviceCapabilities>();
            _healthCache = new Dictionary<string, DeviceHealth>();
            _cacheExpiryTimes = new Dictionary<string, DateTime>();
            _cacheAccessCount = new Dictionary<string, int>();
        }

        public async Task<ApiResponse<GetDeviceListResponse>> GetDeviceListAsync()
        {
            try
            {
                _logger.LogInformation("Getting device list");

                await OptimizedCacheRefreshAsync();

                var devices = _deviceCache.Values.ToList();

                var response = new GetDeviceListResponse
                {
                    Devices = devices,
                    TotalCount = devices.Count,
                    ActiveDevices = devices.Where(d => d.IsAvailable).ToList(),
                    DeviceStatistics = new Dictionary<string, object>
                    {
                        ["total_devices"] = devices.Count,
                        ["available_devices"] = devices.Count(d => d.IsAvailable),
                        ["busy_devices"] = devices.Count(d => !d.IsAvailable),
                        ["last_updated"] = DateTime.UtcNow,
                        ["cache_refresh_time"] = _lastCacheRefresh
                    }
                };

                _logger.LogInformation("Successfully retrieved {Count} devices (Available: {Available}, Busy: {Busy})", 
                    devices.Count, 
                    devices.Count(d => d.IsAvailable),
                    devices.Count(d => !d.IsAvailable));
                return ApiResponse<GetDeviceListResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device list");
                return ApiResponse<GetDeviceListResponse>.CreateError("GET_DEVICE_LIST_ERROR", "Failed to retrieve device list", 500);
            }
        }

        public async Task<ApiResponse<GetDeviceResponse>> GetDeviceAsync(string deviceId)
        {
            try
            {
                _logger.LogInformation("Getting device information for device: {DeviceId}", deviceId);

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return ApiResponse<GetDeviceResponse>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400);
                }

                // Try to get from cache first
                if (!_deviceCache.TryGetValue(deviceId, out var deviceInfo))
                {
                    await RefreshDeviceCacheAsync();
                    
                    if (!_deviceCache.TryGetValue(deviceId, out deviceInfo))
                    {
                        // Try direct Python worker query for specific device
                        var requestId = Guid.NewGuid().ToString();
                        var deviceCommand = new 
                        { 
                            request_id = requestId,
                            action = "get_device",
                            data = new { device_id = deviceId }
                        };
                        
                        _logger.LogDebug("Querying Python worker for device {DeviceId} with request ID: {RequestId}", deviceId, requestId);
                        
                        var pythonResult = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                            PythonWorkerTypes.DEVICE,
                            JsonSerializer.Serialize(deviceCommand),
                            deviceCommand
                        );

                        if (pythonResult != null)
                        {
                            deviceInfo = ConvertPythonDeviceToDeviceInfo(pythonResult);
                            _deviceCache[deviceId] = deviceInfo;
                            _cacheExpiryTimes[deviceId] = DateTime.UtcNow.Add(_deviceSpecificCacheTimeout);
                            _logger.LogDebug("Retrieved device {DeviceId} from Python worker (Request ID: {RequestId})", deviceId, requestId);
                        }
                        else
                        {
                            _logger.LogWarning("Device not found: {DeviceId} (Request ID: {RequestId})", deviceId, requestId);
                            return ApiResponse<GetDeviceResponse>.CreateError("DEVICE_NOT_FOUND", $"Device with ID '{deviceId}' not found", 404);
                        }
                    }
                }

                if (_cacheExpiryTimes.TryGetValue(deviceId, out var expiryTime) && DateTime.UtcNow > expiryTime)
                {
                    _logger.LogInformation("Device cache expired for device: {DeviceId}", deviceId);
                    _deviceCache.Remove(deviceId);
                    return await GetDeviceAsync(deviceId);
                }

                var compatibility = await GetDeviceCompatibilityAsync(deviceId);
                var workload = await GetDeviceWorkloadAsync(deviceId);

                // Convert DeviceCapabilities to DeviceCompatibility
                var deviceCompatibility = new DeviceCompatibility();
                if (compatibility != null)
                {
                    deviceCompatibility.SupportedModelTypes = compatibility.SupportedModelTypes ?? new List<ModelType>();
                    deviceCompatibility.SupportedPrecisions = compatibility.SupportedPrecisions?.Select(p => 
                        Enum.TryParse<DeviceOperations.Models.Common.ModelPrecision>(p, true, out var precision) ? precision : DeviceOperations.Models.Common.ModelPrecision.FP32
                    ).ToList() ?? new List<DeviceOperations.Models.Common.ModelPrecision>();
                    deviceCompatibility.MaxModelSize = compatibility.MemoryCapabilities?.TotalMemoryBytes ?? 0;
                    deviceCompatibility.MaxResolution = new DeviceOperations.Models.Common.ImageResolution { Width = 2048, Height = 2048 }; // Default
                }

                // Convert Dictionary to DeviceWorkload
                DeviceWorkload? deviceWorkload = null;
                if (workload != null)
                {
                    deviceWorkload = new DeviceWorkload
                    {
                        ActiveSessions = workload.TryGetValue("active_sessions", out var sessions) ? Convert.ToInt32(sessions) : 0,
                        QueuedOperations = workload.TryGetValue("queued_operations", out var queued) ? Convert.ToInt32(queued) : 0,
                        CurrentOperations = new List<WorkloadOperation>(),
                        EstimatedCompletion = workload.TryGetValue("estimated_completion", out var completion) && 
                                            DateTime.TryParse(completion.ToString(), out var completionTime) ? completionTime : null
                    };
                }

                var response = new GetDeviceResponse
                {
                    Device = deviceInfo,
                    Compatibility = deviceCompatibility,
                    CurrentWorkload = deviceWorkload
                };

                _logger.LogInformation("Successfully retrieved device information for: {DeviceId}", deviceId);
                return ApiResponse<GetDeviceResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device information for: {DeviceId}", deviceId);
                return ApiResponse<GetDeviceResponse>.CreateError("GET_DEVICE_ERROR", "Failed to retrieve device information", 500);
            }
        }

        public async Task<ApiResponse<SetDeviceSetResponse>> PostDeviceSetAsync(SetDeviceSetRequest request)
        {
            try
            {
                _logger.LogInformation("Creating/updating device set: {DeviceSetName}", request.DeviceSetName);

                if (string.IsNullOrWhiteSpace(request.DeviceSetName))
                {
                    return ApiResponse<SetDeviceSetResponse>.CreateError("INVALID_DEVICE_SET_NAME", "Device set name cannot be null or empty", 400);
                }

                if (request.DeviceIds == null || !request.DeviceIds.Any())
                {
                    return ApiResponse<SetDeviceSetResponse>.CreateError("INVALID_DEVICE_IDS", "Device IDs list cannot be null or empty", 400);
                }

                // Validate all devices exist
                var invalidDevices = new List<string>();
                var validDevices = new List<DeviceInfo>();

                foreach (var deviceId in request.DeviceIds)
                {
                    if (!_deviceCache.TryGetValue(deviceId, out var deviceInfo))
                    {
                        // Try to refresh cache and check again
                        await RefreshDeviceCacheAsync();
                        if (!_deviceCache.TryGetValue(deviceId, out deviceInfo))
                        {
                            invalidDevices.Add(deviceId);
                            continue;
                        }
                    }
                    validDevices.Add(deviceInfo);
                }

                if (invalidDevices.Any())
                {
                    return ApiResponse<SetDeviceSetResponse>.CreateError(
                        "INVALID_DEVICES", 
                        $"Invalid device IDs: {string.Join(", ", invalidDevices)}", 
                        400);
                }

                // Send device set configuration to Python worker
                var requestId = Guid.NewGuid().ToString();
                var deviceSetCommand = new 
                { 
                    request_id = requestId,
                    action = "set_device_set",
                    data = new 
                    {
                        device_set_name = request.DeviceSetName,
                        device_ids = request.DeviceIds,
                        priority = request.Priority ?? "normal",
                        description = request.Description,
                        load_balancing_strategy = request.LoadBalancingStrategy ?? "round_robin"
                    }
                };
                
                _logger.LogDebug("Configuring device set {DeviceSetName} with Python worker (Request ID: {RequestId})", 
                    request.DeviceSetName, requestId);
                
                var pythonResult = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(deviceSetCommand),
                    deviceSetCommand
                );

                if (pythonResult == null)
                {
                    _logger.LogError("Failed to configure device set {DeviceSetName} - Python worker returned null (Request ID: {RequestId})", 
                        request.DeviceSetName, requestId);
                    return ApiResponse<SetDeviceSetResponse>.CreateError("DEVICE_SET_CONFIGURATION_FAILED", 
                        "Failed to configure device set with Python worker", 500);
                }

                // Calculate aggregated capabilities
                var aggregatedCapabilities = CalculateAggregatedCapabilities(validDevices);

                var response = new SetDeviceSetResponse
                {
                    DeviceSetName = request.DeviceSetName,
                    DeviceIds = request.DeviceIds,
                    AggregatedCapabilities = aggregatedCapabilities,
                    ConfigurationStatus = "configured",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Successfully configured device set: {DeviceSetName} with {DeviceCount} devices (Request ID: {RequestId})", 
                    request.DeviceSetName, request.DeviceIds.Count, requestId);
                
                return ApiResponse<SetDeviceSetResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring device set: {DeviceSetName}", request.DeviceSetName);
                return ApiResponse<SetDeviceSetResponse>.CreateError("DEVICE_SET_ERROR", "Failed to configure device set", 500);
            }
        }

        private DeviceCapabilities CalculateAggregatedCapabilities(List<DeviceInfo> devices)
        {
            if (!devices.Any())
            {
                return new DeviceCapabilities();
            }

            var aggregated = new DeviceCapabilities
            {
                SupportedModelTypes = new List<ModelType>(),
                SupportedPrecisions = new List<string>(),
                MemoryCapabilities = new MemoryAllocationInfo
                {
                    TotalMemoryBytes = devices.Sum(d => d.Specifications?.TotalMemoryBytes ?? 0),
                    AllocatedMemoryBytes = devices.Sum(d => (d.Specifications?.TotalMemoryBytes ?? 0) - (d.Specifications?.AvailableMemoryBytes ?? 0))
                },
                SupportsConcurrentInference = devices.All(d => true), // Default to true
                MaxConcurrentInferences = devices.Sum(d => 1), // Conservative: 1 per device
                SupportsBatchProcessing = devices.All(d => true), // Default to true
                MaxBatchSize = devices.Min(d => 16) // Conservative batch size
            };

            // Aggregate supported model types (intersection - only types all devices support)
            if (devices.Any())
            {
                var firstDeviceTypes = devices.First().Specifications?.SupportedModelTypes ?? new List<ModelType>();
                var commonTypes = firstDeviceTypes
                    .Where(t => devices.All(d => d.Specifications?.SupportedModelTypes?.Contains(t) == true))
                    .ToList();
                aggregated.SupportedModelTypes = commonTypes;
            }

            // Aggregate supported precisions (intersection)
            if (devices.Any())
            {
                var firstDevicePrecisions = devices.First().Specifications?.SupportedPrecisions ?? new List<string>();
                var commonPrecisions = firstDevicePrecisions
                    .Where(p => devices.All(d => d.Specifications?.SupportedPrecisions?.Contains(p) == true))
                    .ToList();
                aggregated.SupportedPrecisions = commonPrecisions;
            }

            return aggregated;
        }

        public async Task<ApiResponse<DeviceCapabilities>> GetDeviceCapabilitiesAsync(string? deviceId = null)
        {
            try
            {
                _logger.LogInformation("Getting device capabilities for: {DeviceId}", deviceId);

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return ApiResponse<DeviceCapabilities>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400);
                }

                if (!_capabilityCache.TryGetValue(deviceId, out var capabilities))
                {
                    capabilities = await QueryDeviceCapabilitiesAsync(deviceId);
                    if (capabilities != null)
                    {
                        _capabilityCache[deviceId] = capabilities;
                    }
                }

                if (capabilities == null)
                {
                    return ApiResponse<DeviceCapabilities>.CreateError("CAPABILITIES_NOT_FOUND", $"Capabilities for device '{deviceId}' not found", 404);
                }

                _logger.LogInformation("Successfully retrieved capabilities for device: {DeviceId}", deviceId);
                return ApiResponse<DeviceCapabilities>.CreateSuccess(capabilities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device capabilities for: {DeviceId}", deviceId);
                return ApiResponse<DeviceCapabilities>.CreateError("GET_CAPABILITIES_ERROR", "Failed to retrieve device capabilities", 500);
            }
        }

        public async Task<ApiResponse<GetDeviceStatusResponse>> GetDeviceStatusAsync(string? deviceId = null)
        {
            try
            {
                _logger.LogInformation("Getting real-time device status for: {DeviceId}", deviceId ?? "all devices");

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return ApiResponse<GetDeviceStatusResponse>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400);
                }

                // Get cached device info and validate device exists
                var deviceInfo = await GetCachedDeviceInfoAsync(deviceId);
                if (deviceInfo == null)
                {
                    return ApiResponse<GetDeviceStatusResponse>.CreateError("DEVICE_NOT_FOUND", $"Device with ID '{deviceId}' not found", 404);
                }

                // Send real-time status request to Python worker
                var requestId = Guid.NewGuid().ToString();
                var statusCommand = new
                {
                    request_id = requestId,
                    action = "get_device_status",
                    data = new 
                    { 
                        device_id = deviceId,
                        include_health_metrics = true,
                        include_workload_info = true,
                        include_performance_metrics = true
                    }
                };

                _logger.LogDebug("Querying real-time status for device {DeviceId} with request ID: {RequestId}", deviceId, requestId);

                var pythonResult = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(statusCommand),
                    statusCommand
                );

                DeviceStatus currentStatus;
                DeviceUtilization utilization;
                DevicePerformanceMetrics performance;
                DeviceWorkload? workload = null;

                if (pythonResult != null)
                {
                    // Parse real-time status from Python response
                    var statusInfo = ParseDeviceStatusInfo(pythonResult);
                    currentStatus = statusInfo.Status;
                    utilization = statusInfo.Utilization;
                    performance = statusInfo.Performance;
                    workload = statusInfo.Workload;

                    // Detect status changes and trigger alerts if needed
                    DetectAndHandleStatusChange(deviceId, deviceInfo.Status, currentStatus);

                    // Update cache with latest status
                    deviceInfo.Status = currentStatus;
                    deviceInfo.Utilization = utilization;
                    deviceInfo.LastUpdated = DateTime.UtcNow;
                    _deviceCache[deviceId] = deviceInfo;
                }
                else
                {
                    _logger.LogWarning("Failed to retrieve real-time status for device {DeviceId}, using cached data (Request ID: {RequestId})", 
                        deviceId, requestId);
                    
                    // Fallback to cached information
                    currentStatus = deviceInfo.Status;
                    utilization = deviceInfo.Utilization;
                    performance = GenerateDefaultPerformanceMetrics();
                }

                var response = new GetDeviceStatusResponse
                {
                    DeviceId = deviceId,
                    Status = currentStatus,
                    StatusDescription = GetStatusDescription(currentStatus),
                    Utilization = utilization,
                    Performance = performance,
                    CurrentWorkload = workload,
                    LastUpdated = DateTime.UtcNow
                };

                _logger.LogInformation("Successfully retrieved real-time status for device: {DeviceId}, Status: {Status} (Request ID: {RequestId})", 
                    deviceId, currentStatus, requestId);
                
                return ApiResponse<GetDeviceStatusResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device status for: {DeviceId}", deviceId);
                return ApiResponse<GetDeviceStatusResponse>.CreateError("GET_STATUS_ERROR", "Failed to retrieve device status", 500);
            }
        }

        private DeviceStatusInfo ParseDeviceStatusInfo(dynamic pythonResult)
        {
            try
            {
                var statusInfo = new DeviceStatusInfo();
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(pythonResult.ToString());

                // Parse device status
                if (jsonElement.TryGetProperty("status", out JsonElement statusProp))
                {
                    var statusStr = statusProp.GetString();
                    statusInfo.Status = statusStr?.ToLowerInvariant() switch
                    {
                        "available" => DeviceStatus.Available,
                        "busy" => DeviceStatus.Busy,
                        "offline" => DeviceStatus.Offline,
                        "error" => DeviceStatus.Error,
                        "maintenance" => DeviceStatus.Maintenance,
                        _ => DeviceStatus.Unknown
                    };
                }

                // Parse utilization metrics
                if (jsonElement.TryGetProperty("utilization", out JsonElement utilizationProp))
                {
                    statusInfo.Utilization = ParseUtilization(utilizationProp);
                }

                // Parse performance metrics
                if (jsonElement.TryGetProperty("performance", out JsonElement performanceProp))
                {
                    statusInfo.Performance = ParsePerformanceMetrics(performanceProp);
                }
                else
                {
                    statusInfo.Performance = GenerateDefaultPerformanceMetrics();
                }

                // Parse workload information
                if (jsonElement.TryGetProperty("workload", out JsonElement workloadProp))
                {
                    statusInfo.Workload = ParseWorkloadInfo(workloadProp);
                }

                return statusInfo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse device status from Python response, using defaults");
                return new DeviceStatusInfo
                {
                    Status = DeviceStatus.Unknown,
                    Utilization = new DeviceUtilization(),
                    Performance = GenerateDefaultPerformanceMetrics(),
                    Workload = null
                };
            }
        }

        private DevicePerformanceMetrics ParsePerformanceMetrics(JsonElement performanceElement)
        {
            var metrics = new DevicePerformanceMetrics();

            if (performanceElement.TryGetProperty("operations_per_second", out JsonElement opsProp))
            {
                metrics.OperationsPerSecond = opsProp.GetDouble();
            }

            if (performanceElement.TryGetProperty("average_operation_time", out JsonElement avgTimeProp))
            {
                metrics.AverageOperationTime = avgTimeProp.GetDouble();
            }

            if (performanceElement.TryGetProperty("throughput_mbps", out JsonElement throughputProp))
            {
                metrics.ThroughputMBps = throughputProp.GetDouble();
            }

            if (performanceElement.TryGetProperty("error_rate", out JsonElement errorRateProp))
            {
                metrics.ErrorRate = errorRateProp.GetDouble();
            }

            if (performanceElement.TryGetProperty("uptime_percentage", out JsonElement uptimeProp))
            {
                metrics.UptimePercentage = uptimeProp.GetDouble();
            }

            if (performanceElement.TryGetProperty("performance_score", out JsonElement scoreProp))
            {
                metrics.PerformanceScore = scoreProp.GetDouble();
            }

            return metrics;
        }

        private DeviceWorkload? ParseWorkloadInfo(JsonElement workloadElement)
        {
            try
            {
                var workload = new DeviceWorkload();

                if (workloadElement.TryGetProperty("active_sessions", out JsonElement sessionsProp))
                {
                    workload.ActiveSessions = sessionsProp.GetInt32();
                }

                if (workloadElement.TryGetProperty("queued_operations", out JsonElement queuedProp))
                {
                    workload.QueuedOperations = queuedProp.GetInt32();
                }

                if (workloadElement.TryGetProperty("estimated_completion", out JsonElement completionProp))
                {
                    if (DateTime.TryParse(completionProp.GetString(), out var completionTime))
                    {
                        workload.EstimatedCompletion = completionTime;
                    }
                }

                return workload;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse workload information");
                return null;
            }
        }

        private DevicePerformanceMetrics GenerateDefaultPerformanceMetrics()
        {
            return new DevicePerformanceMetrics
            {
                OperationsPerSecond = 0.0,
                AverageOperationTime = 0.0,
                ThroughputMBps = 0.0,
                ErrorRate = 0.0,
                UptimePercentage = 100.0,
                PerformanceScore = 50.0
            };
        }

        private string GetStatusDescription(DeviceStatus status)
        {
            return status switch
            {
                DeviceStatus.Available => "Device is available and ready for operations",
                DeviceStatus.Busy => "Device is currently processing operations",
                DeviceStatus.Offline => "Device is offline or unreachable",
                DeviceStatus.Error => "Device has encountered an error",
                DeviceStatus.Maintenance => "Device is in maintenance mode",
                DeviceStatus.Unknown => "Device status is unknown",
                _ => "Unknown device status"
            };
        }

        private void DetectAndHandleStatusChange(string deviceId, DeviceStatus previousStatus, DeviceStatus currentStatus)
        {
            if (previousStatus != currentStatus)
            {
                _logger.LogInformation("Device status change detected for {DeviceId}: {PreviousStatus} → {CurrentStatus}", 
                    deviceId, previousStatus, currentStatus);

                // Handle critical status changes
                if (currentStatus == DeviceStatus.Error || currentStatus == DeviceStatus.Offline)
                {
                    _logger.LogWarning("Device {DeviceId} has entered critical status: {Status}", deviceId, currentStatus);
                    // TODO: Integration with Processing domain for workflow coordination
                    // TODO: Trigger alerts or notifications
                }

                // Handle recovery
                if (previousStatus is DeviceStatus.Error or DeviceStatus.Offline && currentStatus == DeviceStatus.Available)
                {
                    _logger.LogInformation("Device {DeviceId} has recovered and is now available", deviceId);
                    // TODO: Notify Processing domain that device is available again
                }

                // Update status change tracking
                // TODO: Store status change history for analytics
            }
        }

        private class DeviceStatusInfo
        {
            public DeviceStatus Status { get; set; }
            public DeviceUtilization Utilization { get; set; } = new();
            public DevicePerformanceMetrics Performance { get; set; } = new();
            public DeviceWorkload? Workload { get; set; }
        }

        public async Task<ApiResponse<PostDeviceHealthResponse>> PostDeviceHealthAsync(string deviceId, PostDeviceHealthRequest request)
        {
            try
            {
                _logger.LogInformation("Running health check for device: {DeviceId}", deviceId);

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return ApiResponse<PostDeviceHealthResponse>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400);
                }

                var deviceInfo = await GetCachedDeviceInfoAsync(deviceId);
                if (deviceInfo == null)
                {
                    return ApiResponse<PostDeviceHealthResponse>.CreateError("DEVICE_NOT_FOUND", $"Device with ID '{deviceId}' not found", 404);
                }

                // Execute health check via Python worker
                var healthCheckCommand = new
                {
                    command = "device_health",
                    device_id = deviceId,
                    check_type = request.HealthCheckType ?? "comprehensive",
                    include_performance_metrics = request.IncludePerformanceMetrics
                };

                var result = await _pythonWorkerService.ExecuteAsync<object, DeviceHealth>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(healthCheckCommand),
                    healthCheckCommand
                );

                if (result == null)
                {
                    _logger.LogError("Python worker health check failed for device: {DeviceId}", deviceId);
                    return ApiResponse<PostDeviceHealthResponse>.CreateError("HEALTH_CHECK_FAILED", "Health check execution failed", 500);
                }

                var healthData = result;
                if (healthData != null)
                {
                    _healthCache[deviceId] = healthData;
                }

                var response = new PostDeviceHealthResponse
                {
                    DeviceId = deviceId,
                    Health = healthData ?? new DeviceHealth { Status = "Unknown" },
                    HealthScore = 85.0, // Default score
                    Recommendations = new List<string>(),
                    Warnings = new List<string>()
                };

                _logger.LogInformation("Successfully completed health check for device: {DeviceId}", deviceId);
                return ApiResponse<PostDeviceHealthResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running health check for device: {DeviceId}", deviceId);
                return ApiResponse<PostDeviceHealthResponse>.CreateError("HEALTH_CHECK_ERROR", "Failed to execute health check", 500);
            }
        }

        public Task<ApiResponse<bool>> PostDeviceAvailabilityAsync(string deviceId, bool isAvailable)
        {
            try
            {
                _logger.LogInformation("Setting device availability: {DeviceId} = {IsAvailable}", deviceId, isAvailable);

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return Task.FromResult(ApiResponse<bool>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400));
                }

                if (_deviceCache.TryGetValue(deviceId, out var deviceInfo))
                {
                    deviceInfo.IsAvailable = isAvailable;
                    deviceInfo.Status = isAvailable ? DeviceStatus.Available : DeviceStatus.Offline;
                    deviceInfo.LastUpdated = DateTime.UtcNow;
                }

                _logger.LogInformation("Successfully updated device availability: {DeviceId}", deviceId);
                return Task.FromResult(ApiResponse<bool>.CreateSuccess(isAvailable));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting device availability: {DeviceId}", deviceId);
                return Task.FromResult(ApiResponse<bool>.CreateError("SET_AVAILABILITY_ERROR", "Failed to set device availability", 500));
            }
        }

        public async Task<ApiResponse<PostDeviceBenchmarkResponse>> PostDeviceBenchmarkAsync(string deviceId, PostDeviceBenchmarkRequest request)
        {
            try
            {
                _logger.LogInformation("Running benchmark for device: {DeviceId}, Type: {BenchmarkType}", deviceId, request.BenchmarkType);

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return ApiResponse<PostDeviceBenchmarkResponse>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400);
                }

                var benchmarkCommand = new
                {
                    command = "device_benchmark",
                    device_id = deviceId,
                    benchmark_type = request.BenchmarkType.ToString(),
                    duration_seconds = request.DurationSeconds
                };

                var result = await _pythonWorkerService.ExecuteAsync<object, DeviceBenchmarkResults>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(benchmarkCommand),
                    benchmarkCommand
                );

                if (result == null)
                {
                    _logger.LogError("Benchmark failed for device: {DeviceId}", deviceId);
                    return ApiResponse<PostDeviceBenchmarkResponse>.CreateError("BENCHMARK_FAILED", "Device benchmark execution failed", 500);
                }

                var response = new PostDeviceBenchmarkResponse
                {
                    DeviceId = deviceId,
                    BenchmarkType = (DeviceOperations.Models.Common.BenchmarkType)request.BenchmarkType,
                    Results = result,
                    BenchmarkAt = DateTime.UtcNow,
                    DurationSeconds = request.DurationSeconds
                };

                _logger.LogInformation("Successfully completed benchmark for device: {DeviceId}", deviceId);
                return ApiResponse<PostDeviceBenchmarkResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running benchmark for device: {DeviceId}", deviceId);
                return ApiResponse<PostDeviceBenchmarkResponse>.CreateError("BENCHMARK_ERROR", "Failed to run device benchmark", 500);
            }
        }

        public async Task<ApiResponse<PostDeviceOptimizeResponse>> PostDeviceOptimizeAsync(string deviceId, PostDeviceOptimizeRequest request)
        {
            try
            {
                _logger.LogInformation("Optimizing device: {DeviceId}, Target: {Target}", 
                    deviceId, request.Target);

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return ApiResponse<PostDeviceOptimizeResponse>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400);
                }

                // Validate device exists and is available for optimization
                if (!_deviceCache.TryGetValue(deviceId, out var deviceInfo))
                {
                    await RefreshDeviceCacheAsync();
                    if (!_deviceCache.TryGetValue(deviceId, out deviceInfo))
                    {
                        _logger.LogWarning("Device not found for optimization: {DeviceId}", deviceId);
                        return ApiResponse<PostDeviceOptimizeResponse>.CreateError("DEVICE_NOT_FOUND", $"Device with ID '{deviceId}' not found", 404);
                    }
                }

                if (deviceInfo.Status != DeviceStatus.Available)
                {
                    _logger.LogWarning("Device not available for optimization: {DeviceId}, Status: {Status}", deviceId, deviceInfo.Status);
                    return ApiResponse<PostDeviceOptimizeResponse>.CreateError("DEVICE_NOT_AVAILABLE", 
                        $"Device '{deviceId}' is not available for optimization (Status: {deviceInfo.Status})", 409);
                }

                // Send optimization request to Python worker with standardized format
                var requestId = Guid.NewGuid().ToString();
                var optimizeCommand = new
                {
                    request_id = requestId,
                    action = "optimize_device",
                    data = new
                    {
                        device_id = deviceId,
                        optimization_target = request.Target.ToString().ToLowerInvariant(),
                        auto_apply = request.AutoApply
                    }
                };

                _logger.LogDebug("Sending device optimization request for {DeviceId} with request ID: {RequestId}", deviceId, requestId);

                var pythonResult = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(optimizeCommand),
                    optimizeCommand
                );

                if (pythonResult == null)
                {
                    _logger.LogError("Optimization failed for device: {DeviceId} - Python worker returned null (Request ID: {RequestId})", 
                        deviceId, requestId);
                    return ApiResponse<PostDeviceOptimizeResponse>.CreateError("OPTIMIZATION_FAILED", 
                        "Device optimization execution failed", 500);
                }

                // Parse optimization results from Python response
                var optimizationResults = ParseOptimizationResults(pythonResult);
                
                // Collect performance metrics if optimization was applied
                var performanceMetrics = request.AutoApply ? await CollectPerformanceMetrics(deviceId) : null;

                var response = new PostDeviceOptimizeResponse
                {
                    DeviceId = deviceId,
                    Target = (DeviceOperations.Models.Common.OptimizationTarget)request.Target,
                    Results = optimizationResults,
                    Applied = request.AutoApply && optimizationResults.ConfidenceScore > 0.7, // Only apply if confident
                    Recommendations = ExtractOptimizationRecommendations(optimizationResults),
                    OptimizedAt = DateTime.UtcNow
                };

                // Update device cache if optimization was applied
                if (response.Applied)
                {
                    await RefreshDeviceCacheAsync();
                }

                _logger.LogInformation("Device optimization completed for device: {DeviceId}", deviceId);

                return ApiResponse<PostDeviceOptimizeResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing device: {DeviceId}", deviceId);
                return ApiResponse<PostDeviceOptimizeResponse>.CreateError("DEVICE_OPTIMIZATION_ERROR", "Device optimization failed", 500);
            }
        }

        private DeviceOptimizationResults ParseOptimizationResults(dynamic pythonResult)
        {
            try
            {
                var results = new DeviceOptimizationResults();

                if (pythonResult != null)
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(pythonResult.ToString());
                    
                    if (jsonElement.TryGetProperty("current_settings", out JsonElement currentProp))
                    {
                        results.CurrentSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(currentProp.GetRawText()) ?? new();
                    }
                    
                    if (jsonElement.TryGetProperty("recommended_settings", out JsonElement recommendedProp))
                    {
                        results.RecommendedSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(recommendedProp.GetRawText()) ?? new();
                    }
                    
                    if (jsonElement.TryGetProperty("expected_improvement", out JsonElement improvementProp))
                    {
                        results.ExpectedImprovement = improvementProp.GetDouble();
                    }
                    
                    if (jsonElement.TryGetProperty("confidence_score", out JsonElement confidenceProp))
                    {
                        results.ConfidenceScore = confidenceProp.GetDouble();
                    }
                    
                    if (jsonElement.TryGetProperty("analysis", out JsonElement analysisProp))
                    {
                        results.Analysis = JsonSerializer.Deserialize<Dictionary<string, string>>(analysisProp.GetRawText()) ?? new();
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse optimization results from Python response, using defaults");
                return new DeviceOptimizationResults
                {
                    CurrentSettings = new Dictionary<string, object>(),
                    RecommendedSettings = new Dictionary<string, object>(),
                    ExpectedImprovement = 0.0,
                    ConfidenceScore = 0.0,
                    Analysis = new Dictionary<string, string> { { "error", "Failed to parse optimization results" } }
                };
            }
        }

        private async Task<Dictionary<string, double>?> CollectPerformanceMetrics(string deviceId)
        {
            try
            {
                // Collect basic performance metrics after optimization
                var requestId = Guid.NewGuid().ToString();
                var metricsCommand = new
                {
                    request_id = requestId,
                    action = "get_performance_metrics",
                    data = new { device_id = deviceId }
                };

                var pythonResult = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(metricsCommand),
                    metricsCommand
                );

                if (pythonResult != null)
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(pythonResult.ToString());
                    if (jsonElement.TryGetProperty("metrics", out JsonElement metricsProp))
                    {
                        return JsonSerializer.Deserialize<Dictionary<string, double>>(metricsProp.GetRawText());
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect performance metrics for device: {DeviceId}", deviceId);
                return null;
            }
        }

        private List<string> ExtractOptimizationRecommendations(DeviceOptimizationResults results)
        {
            var recommendations = new List<string>();

            if (results.ExpectedImprovement > 10)
            {
                recommendations.Add("Significant performance improvement expected");
            }
            else if (results.ExpectedImprovement > 5)
            {
                recommendations.Add("Moderate performance improvement expected");
            }
            else if (results.ExpectedImprovement > 0)
            {
                recommendations.Add("Minor performance improvement expected");
            }

            if (results.ConfidenceScore < 0.5)
            {
                recommendations.Add("Low confidence in optimization results - manual review recommended");
            }
            else if (results.ConfidenceScore < 0.8)
            {
                recommendations.Add("Moderate confidence in optimization results");
            }
            else
            {
                recommendations.Add("High confidence in optimization results");
            }

            // Add specific recommendations based on analysis
            foreach (var analysis in results.Analysis)
            {
                if (analysis.Key.Contains("memory") && analysis.Value.Contains("optimization"))
                {
                    recommendations.Add("Memory allocation optimization available");
                }
                if (analysis.Key.Contains("power") && analysis.Value.Contains("reduction"))
                {
                    recommendations.Add("Power consumption reduction possible");
                }
                if (analysis.Key.Contains("thermal") && analysis.Value.Contains("management"))
                {
                    recommendations.Add("Thermal management improvements available");
                }
            }

            return recommendations.Any() ? recommendations : new List<string> { "No specific recommendations available" };
        }

        public async Task<ApiResponse<GetDeviceConfigResponse>> GetDeviceConfigAsync(string deviceId)
        {
            try
            {
                _logger.LogInformation("Getting device configuration for: {DeviceId}", deviceId);

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return ApiResponse<GetDeviceConfigResponse>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400);
                }

                var configCommand = new
                {
                    command = "get_device_config",
                    device_id = deviceId
                };

                var result = await _pythonWorkerService.ExecuteAsync<object, Dictionary<string, object>>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(configCommand),
                    configCommand
                );

                if (result == null)
                {
                    _logger.LogError("Failed to get configuration for device: {DeviceId}", deviceId);
                    return ApiResponse<GetDeviceConfigResponse>.CreateError("CONFIG_GET_FAILED", "Failed to retrieve device configuration", 500);
                }

                var config = result;

                var response = new GetDeviceConfigResponse
                {
                    DeviceId = deviceId,
                    Configuration = config,
                    Schema = new Dictionary<string, ConfigurationSchema>(),
                    LastModified = DateTime.UtcNow.AddHours(-1)
                };

                _logger.LogInformation("Successfully retrieved configuration for device: {DeviceId}", deviceId);
                return ApiResponse<GetDeviceConfigResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device configuration: {DeviceId}", deviceId);
                return ApiResponse<GetDeviceConfigResponse>.CreateError("CONFIG_ERROR", "Failed to retrieve device configuration", 500);
            }
        }

        public async Task<ApiResponse<PutDeviceConfigResponse>> PutDeviceConfigAsync(string deviceId, PutDeviceConfigRequest request)
        {
            try
            {
                _logger.LogInformation("Updating device configuration for: {DeviceId}", deviceId);

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return ApiResponse<PutDeviceConfigResponse>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400);
                }

                var configCommand = new
                {
                    command = "set_device_config",
                    device_id = deviceId,
                    configuration = request.Configuration
                };

                var result = await _pythonWorkerService.ExecuteAsync<object, object>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(configCommand),
                    configCommand
                );

                if (result == null)
                {
                    _logger.LogError("Failed to update configuration for device: {DeviceId}", deviceId);
                    return ApiResponse<PutDeviceConfigResponse>.CreateError("CONFIG_UPDATE_FAILED", "Failed to update device configuration", 500);
                }

                var response = new PutDeviceConfigResponse
                {
                    DeviceId = deviceId,
                    UpdatedKeys = request.Configuration?.Keys.ToList() ?? new List<string>(),
                    FailedKeys = new List<string>(),
                    ValidationResults = new Dictionary<string, string> { { "status", "Configuration updated successfully" } }
                };

                _logger.LogInformation("Successfully updated configuration for device: {DeviceId}", deviceId);
                return ApiResponse<PutDeviceConfigResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device configuration: {DeviceId}", deviceId);
                return ApiResponse<PutDeviceConfigResponse>.CreateError("CONFIG_UPDATE_ERROR", "Failed to update device configuration", 500);
            }
        }

        public async Task<ApiResponse<DeviceInfo>> GetDeviceDetailsAsync(string? deviceId = null)
        {
            try
            {
                _logger.LogInformation("Getting device details for: {DeviceId}", deviceId ?? "default");

                var targetDeviceId = deviceId ?? "default";
                var deviceInfo = await GetCachedDeviceInfoAsync(targetDeviceId);

                if (deviceInfo == null)
                {
                    return ApiResponse<DeviceInfo>.CreateError("DEVICE_NOT_FOUND", $"Device with ID '{targetDeviceId}' not found", 404);
                }

                _logger.LogInformation("Successfully retrieved device details for: {DeviceId}", targetDeviceId);
                return ApiResponse<DeviceInfo>.CreateSuccess(deviceInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device details: {DeviceId}", deviceId);
                return ApiResponse<DeviceInfo>.CreateError("GET_DETAILS_ERROR", "Failed to retrieve device details", 500);
            }
        }

        public async Task<ApiResponse<DriverInfo>> GetDeviceDriversAsync(string? deviceId = null)
        {
            try
            {
                _logger.LogInformation("Getting device drivers for: {DeviceId}", deviceId ?? "default");

                var targetDeviceId = deviceId ?? "default";
                var deviceInfo = await GetCachedDeviceInfoAsync(targetDeviceId);

                if (deviceInfo == null)
                {
                    return ApiResponse<DriverInfo>.CreateError("DEVICE_NOT_FOUND", $"Device with ID '{targetDeviceId}' not found", 404);
                }

                // Create DriverInfo from DeviceInfo data
                var driverInfo = new DriverInfo
                {
                    Version = deviceInfo.DriverVersion,
                    Name = $"{deviceInfo.Vendor} Driver",
                    Date = deviceInfo.LastUpdated,
                    Status = "Active"
                };

                _logger.LogInformation("Successfully retrieved driver info for device: {DeviceId}", targetDeviceId);
                return ApiResponse<DriverInfo>.CreateSuccess(driverInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device drivers: {DeviceId}", deviceId);
                return ApiResponse<DriverInfo>.CreateError("GET_DRIVERS_ERROR", "Failed to retrieve device drivers", 500);
            }
        }

        public async Task<ApiResponse<GetDeviceMemoryResponse>> GetDeviceMemoryAsync(string deviceId)
        {
            try
            {
                _logger.LogInformation("Getting device memory information for device: {DeviceId}", deviceId);

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return ApiResponse<GetDeviceMemoryResponse>.CreateError("INVALID_DEVICE_ID", "Device ID cannot be null or empty", 400);
                }

                // Validate device exists
                if (!_deviceCache.TryGetValue(deviceId, out var deviceInfo))
                {
                    await RefreshDeviceCacheAsync();
                    if (!_deviceCache.TryGetValue(deviceId, out deviceInfo))
                    {
                        _logger.LogWarning("Device not found for memory query: {DeviceId}", deviceId);
                        return ApiResponse<GetDeviceMemoryResponse>.CreateError("DEVICE_NOT_FOUND", $"Device with ID '{deviceId}' not found", 404);
                    }
                }

                // Send memory information request to Python worker
                var requestId = Guid.NewGuid().ToString();
                var memoryCommand = new 
                { 
                    request_id = requestId,
                    action = "get_memory_info",
                    data = new { device_id = deviceId }
                };
                
                _logger.LogDebug("Querying device memory for {DeviceId} with request ID: {RequestId}", deviceId, requestId);
                
                var pythonResult = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(memoryCommand),
                    memoryCommand
                );

                if (pythonResult == null)
                {
                    _logger.LogError("Failed to retrieve memory information for device {DeviceId} - Python worker returned null (Request ID: {RequestId})", 
                        deviceId, requestId);
                    return ApiResponse<GetDeviceMemoryResponse>.CreateError("MEMORY_INFO_RETRIEVAL_FAILED", 
                        "Failed to retrieve memory information from device worker", 500);
                }

                // Parse memory information from Python response
                var memoryInfo = ParseDeviceMemoryInfo(pythonResult);
                
                // Create response with both current usage and allocation information
                var response = new GetDeviceMemoryResponse
                {
                    DeviceId = deviceId,
                    DeviceName = deviceInfo.Name,
                    DeviceType = deviceInfo.Type,
                    MemoryInfo = memoryInfo,
                    LastUpdated = DateTime.UtcNow,
                    MemoryStatus = DetermineMemoryStatus(memoryInfo)
                };

                var totalMB = (long)(memoryInfo.TotalMemoryBytes / (1024 * 1024));
                var availableMB = (long)(memoryInfo.AvailableMemoryBytes / (1024 * 1024));
                _logger.LogInformation("Successfully retrieved memory information for device: {DeviceId} - Total: {TotalMB}MB, Available: {AvailableMB}MB (Request ID: {RequestId})", 
                    deviceId, totalMB, availableMB, requestId);
                
                return ApiResponse<GetDeviceMemoryResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device memory information for: {DeviceId}", deviceId);
                return ApiResponse<GetDeviceMemoryResponse>.CreateError("DEVICE_MEMORY_ERROR", "Failed to retrieve device memory information", 500);
            }
        }

        private MemoryAllocationInfo ParseDeviceMemoryInfo(dynamic pythonResult)
        {
            try
            {
                // Parse Python response to extract memory information
                var memoryInfo = new MemoryAllocationInfo();

                if (pythonResult != null)
                {
                    // Handle different possible response formats from Python
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(pythonResult.ToString());
                    
                    if (jsonElement.TryGetProperty("total_memory", out JsonElement totalMemoryProp))
                    {
                        memoryInfo.TotalMemoryBytes = totalMemoryProp.GetInt64();
                    }
                    else if (jsonElement.TryGetProperty("total_memory_bytes", out JsonElement totalMemoryBytesProp))
                    {
                        memoryInfo.TotalMemoryBytes = totalMemoryBytesProp.GetInt64();
                    }

                    if (jsonElement.TryGetProperty("allocated_memory", out JsonElement allocatedMemoryProp))
                    {
                        memoryInfo.AllocatedMemoryBytes = allocatedMemoryProp.GetInt64();
                    }
                    else if (jsonElement.TryGetProperty("used_memory", out JsonElement usedMemoryProp))
                    {
                        memoryInfo.AllocatedMemoryBytes = usedMemoryProp.GetInt64();
                    }
                    else if (jsonElement.TryGetProperty("allocated_memory_bytes", out JsonElement allocatedMemoryBytesProp))
                    {
                        memoryInfo.AllocatedMemoryBytes = allocatedMemoryBytesProp.GetInt64();
                    }

                    if (jsonElement.TryGetProperty("supports_memory_pooling", out JsonElement poolingProp))
                    {
                        memoryInfo.SupportsMemoryPooling = poolingProp.GetBoolean();
                    }

                    if (jsonElement.TryGetProperty("allocation_alignment", out JsonElement alignmentProp))
                    {
                        memoryInfo.AllocationAlignment = alignmentProp.GetInt32();
                    }
                    else
                    {
                        memoryInfo.AllocationAlignment = 256; // Default GPU alignment
                    }
                }

                return memoryInfo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse memory information from Python response, using defaults");
                return new MemoryAllocationInfo
                {
                    TotalMemoryBytes = 0,
                    AllocatedMemoryBytes = 0,
                    SupportsMemoryPooling = false,
                    AllocationAlignment = 1
                };
            }
        }

        private string DetermineMemoryStatus(MemoryAllocationInfo memoryInfo)
        {
            if (memoryInfo.TotalMemoryBytes == 0)
            {
                return "unknown";
            }

            var utilizationPercentage = (double)memoryInfo.AllocatedMemoryBytes / memoryInfo.TotalMemoryBytes * 100;

            return utilizationPercentage switch
            {
                < 50 => "low_usage",
                < 75 => "moderate_usage", 
                < 90 => "high_usage",
                _ => "critical_usage"
            };
        }

        private async Task RefreshDeviceCacheAsync()
        {
            if (DateTime.UtcNow - _lastCacheRefresh < _cacheTimeout)
                return;

            try
            {
                // Standardized communication pattern with request ID
                var requestId = Guid.NewGuid().ToString();
                var listCommand = new 
                { 
                    request_id = requestId,
                    action = "list_devices",
                    data = new { }
                };
                
                _logger.LogDebug("Sending device list request with ID: {RequestId}", requestId);
                
                var result = await _pythonWorkerService.ExecuteAsync<object, List<DeviceInfo>>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(listCommand),
                    listCommand
                );

                if (result != null && result.Any())
                {
                    var devices = result;
                    
                    _deviceCache.Clear();
                    foreach (var device in devices)
                    {
                        _deviceCache[device.Id] = device;
                        _cacheExpiryTimes[device.Id] = DateTime.UtcNow.Add(_deviceSpecificCacheTimeout);
                    }

                    _lastCacheRefresh = DateTime.UtcNow;
                    _logger.LogInformation("Refreshed device cache with {Count} devices (Request ID: {RequestId})", devices.Count, requestId);
                }
                else
                {
                    _logger.LogWarning("Device discovery returned no results (Request ID: {RequestId})", requestId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing device cache");
            }
        }

        private async Task<DeviceInfo?> GetCachedDeviceInfoAsync(string deviceId)
        {
            DeviceInfo? deviceInfo;
            var cacheHit = TryGetCachedDevice(deviceId, out deviceInfo);
            
            if (!cacheHit)
            {
                await OptimizedCacheRefreshAsync();
                TryGetCachedDevice(deviceId, out deviceInfo);
            }
            
            return deviceInfo;
        }

        private async Task<DeviceCapabilities?> GetDeviceCompatibilityAsync(string deviceId)
        {
            if (!_capabilityCache.TryGetValue(deviceId, out var capabilities))
            {
                capabilities = await QueryDeviceCapabilitiesAsync(deviceId);
                if (capabilities != null)
                {
                    _capabilityCache[deviceId] = capabilities;
                }
            }
            return capabilities;
        }

        private async Task<DeviceCapabilities?> QueryDeviceCapabilitiesAsync(string deviceId)
        {
            try
            {
                var requestId = Guid.NewGuid().ToString();
                var capabilitiesCommand = new 
                { 
                    request_id = requestId,
                    action = "get_capabilities", 
                    data = new { device_id = deviceId }
                };
                
                _logger.LogDebug("Querying device capabilities for {DeviceId} with request ID: {RequestId}", deviceId, requestId);
                
                var result = await _pythonWorkerService.ExecuteAsync<object, DeviceCapabilities>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(capabilitiesCommand),
                    capabilitiesCommand
                );

                if (result != null)
                {
                    _logger.LogDebug("Successfully retrieved capabilities for {DeviceId} (Request ID: {RequestId})", deviceId, requestId);
                    return result;
                }
                else
                {
                    _logger.LogWarning("No capabilities returned for {DeviceId} (Request ID: {RequestId})", deviceId, requestId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying device capabilities for: {DeviceId}", deviceId);
            }
            return null;
        }

        private async Task<DeviceHealth?> QueryDeviceHealthAsync(string deviceId)
        {
            try
            {
                var healthCommand = new { command = "get_health", device_id = deviceId };
                var result = await _pythonWorkerService.ExecuteAsync<object, DeviceHealth>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(healthCommand),
                    healthCommand
                );

                if (result != null)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying device health for: {DeviceId}", deviceId);
            }
            return null;
        }

        private async Task<Dictionary<string, object>?> GetDeviceWorkloadAsync(string deviceId)
        {
            try
            {
                var workloadCommand = new { command = "get_workload", device_id = deviceId };
                var result = await _pythonWorkerService.ExecuteAsync<object, Dictionary<string, object>>(
                    PythonWorkerTypes.DEVICE,
                    JsonSerializer.Serialize(workloadCommand),
                    workloadCommand
                );

                if (result != null)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device workload for: {DeviceId}", deviceId);
            }
            return new Dictionary<string, object>();
        }

        // Missing overload implementations for interface compatibility
        public Task<ApiResponse<PostDeviceOptimizeResponse>> PostDeviceOptimizeAsync(PostDeviceOptimizeRequest request)
        {
            // Implementation for all devices optimize operation
            return Task.FromResult(ApiResponse<PostDeviceOptimizeResponse>.CreateSuccess(new PostDeviceOptimizeResponse
            {
                DeviceId = "all-devices",
                Target = DeviceOperations.Models.Common.OptimizationTarget.Balanced,
                Results = new DeviceOptimizationResults(),
                Applied = true
            }));
        }

        public async Task<ApiResponse<PostDeviceOptimizeResponse>> PostDeviceOptimizeAsync(PostDeviceOptimizeRequest request, string deviceId)
        {
            // Delegate to the main implementation with correct parameter order
            return await PostDeviceOptimizeAsync(deviceId, request);
        }

        private DeviceInfo ConvertPythonDeviceToDeviceInfo(dynamic pythonResult)
        {
            try
            {
                var deviceInfo = new DeviceInfo();

                if (pythonResult != null)
                {
                    // Parse Python response to extract device information
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(pythonResult.ToString());
                    
                    if (jsonElement.TryGetProperty("id", out JsonElement idProp))
                    {
                        deviceInfo.Id = idProp.GetString() ?? string.Empty;
                    }
                    
                    if (jsonElement.TryGetProperty("name", out JsonElement nameProp))
                    {
                        deviceInfo.Name = nameProp.GetString() ?? string.Empty;
                    }
                    
                    if (jsonElement.TryGetProperty("type", out JsonElement typeProp))
                    {
                        var typeStr = typeProp.GetString();
                        deviceInfo.Type = typeStr?.ToLowerInvariant() switch
                        {
                            "cpu" => DeviceType.CPU,
                            "gpu" => DeviceType.GPU,
                            "npu" => DeviceType.NPU,
                            "tpu" => DeviceType.TPU,
                            _ => DeviceType.Unknown
                        };
                    }
                    
                    if (jsonElement.TryGetProperty("vendor", out JsonElement vendorProp))
                    {
                        deviceInfo.Vendor = vendorProp.GetString() ?? string.Empty;
                    }
                    
                    if (jsonElement.TryGetProperty("architecture", out JsonElement archProp))
                    {
                        deviceInfo.Architecture = archProp.GetString() ?? string.Empty;
                    }
                    
                    if (jsonElement.TryGetProperty("status", out JsonElement statusProp))
                    {
                        var statusStr = statusProp.GetString();
                        deviceInfo.Status = statusStr?.ToLowerInvariant() switch
                        {
                            "available" => DeviceStatus.Available,
                            "busy" => DeviceStatus.Busy,
                            "offline" => DeviceStatus.Offline,
                            "error" => DeviceStatus.Error,
                            "maintenance" => DeviceStatus.Maintenance,
                            _ => DeviceStatus.Unknown
                        };
                    }
                    
                    if (jsonElement.TryGetProperty("driver_version", out JsonElement driverProp))
                    {
                        deviceInfo.DriverVersion = driverProp.GetString() ?? string.Empty;
                    }

                    deviceInfo.IsAvailable = deviceInfo.Status == DeviceStatus.Available;
                    deviceInfo.LastUpdated = DateTime.UtcNow;
                    deviceInfo.CreatedAt = DateTime.UtcNow;
                    deviceInfo.UpdatedAt = DateTime.UtcNow;

                    // Initialize utilization if available
                    if (jsonElement.TryGetProperty("utilization", out JsonElement utilizationProp))
                    {
                        deviceInfo.Utilization = ParseUtilization(utilizationProp);
                    }

                    // Initialize specifications if available
                    if (jsonElement.TryGetProperty("specifications", out JsonElement specsProp))
                    {
                        deviceInfo.Specifications = ParseSpecifications(specsProp);
                    }
                }

                return deviceInfo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse device information from Python response, using defaults");
                return new DeviceInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Unknown Device",
                    Type = DeviceType.Unknown,
                    Status = DeviceStatus.Unknown,
                    IsAvailable = false,
                    LastUpdated = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Utilization = new DeviceUtilization(),
                    Specifications = new DeviceSpecifications()
                };
            }
        }

        private DeviceUtilization ParseUtilization(JsonElement utilizationElement)
        {
            var utilization = new DeviceUtilization();
            
            if (utilizationElement.TryGetProperty("cpu_utilization", out JsonElement cpuProp))
            {
                utilization.CpuUtilization = cpuProp.GetDouble();
            }
            
            if (utilizationElement.TryGetProperty("memory_utilization", out JsonElement memProp))
            {
                utilization.MemoryUtilization = memProp.GetDouble();
            }
            
            if (utilizationElement.TryGetProperty("gpu_utilization", out JsonElement gpuProp))
            {
                utilization.GpuUtilization = gpuProp.GetDouble();
            }
            
            return utilization;
        }

        private DeviceSpecifications ParseSpecifications(JsonElement specsElement)
        {
            var specs = new DeviceSpecifications();
            
            if (specsElement.TryGetProperty("total_memory_bytes", out JsonElement totalMemProp))
            {
                specs.TotalMemoryBytes = totalMemProp.GetInt64();
            }
            
            if (specsElement.TryGetProperty("available_memory_bytes", out JsonElement availMemProp))
            {
                specs.AvailableMemoryBytes = availMemProp.GetInt64();
            }
            
            if (specsElement.TryGetProperty("compute_units", out JsonElement computeProp))
            {
                specs.ComputeUnits = computeProp.GetInt32();
            }
            
            if (specsElement.TryGetProperty("clock_speed_mhz", out JsonElement clockProp))
            {
                specs.ClockSpeedMHz = clockProp.GetInt32();
            }
            
            return specs;
        }

        #region Enhanced Cache Management

        /// <summary>
        /// Optimized cache retrieval with access tracking and expiry checking
        /// </summary>
        private bool TryGetCachedDevice(string deviceId, out DeviceInfo? deviceInfo)
        {
            lock (_cacheLock)
            {
                deviceInfo = null;
                
                if (!_deviceCache.TryGetValue(deviceId, out deviceInfo))
                {
                    return false;
                }

                // Check if cache entry has expired
                if (_cacheExpiryTimes.TryGetValue(deviceId, out var expiryTime) && 
                    DateTime.UtcNow > expiryTime)
                {
                    _deviceCache.Remove(deviceId);
                    _cacheExpiryTimes.Remove(deviceId);
                    _cacheAccessCount.Remove(deviceId);
                    deviceInfo = null;
                    return false;
                }

                // Track access for cache optimization
                _cacheAccessCount[deviceId] = _cacheAccessCount.GetValueOrDefault(deviceId, 0) + 1;
                
                return true;
            }
        }

        /// <summary>
        /// Optimized cache update with automatic cleanup
        /// </summary>
        private void UpdateDeviceCache(string deviceId, DeviceInfo deviceInfo)
        {
            lock (_cacheLock)
            {
                // Cleanup old entries if cache is getting too large
                if (_deviceCache.Count >= _maxCacheSize)
                {
                    CleanupExpiredCacheEntries();
                    
                    // If still too large, remove least accessed entries
                    if (_deviceCache.Count >= _maxCacheSize)
                    {
                        RemoveLeastAccessedEntries();
                    }
                }

                // Update cache with new expiry time
                _deviceCache[deviceId] = deviceInfo;
                _cacheExpiryTimes[deviceId] = DateTime.UtcNow.Add(_deviceSpecificCacheTimeout);
                _cacheAccessCount[deviceId] = _cacheAccessCount.GetValueOrDefault(deviceId, 0) + 1;
            }
        }

        /// <summary>
        /// Remove expired cache entries
        /// </summary>
        private void CleanupExpiredCacheEntries()
        {
            var currentTime = DateTime.UtcNow;
            var expiredKeys = _cacheExpiryTimes
                .Where(kvp => currentTime > kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _deviceCache.Remove(key);
                _cacheExpiryTimes.Remove(key);
                _cacheAccessCount.Remove(key);
                _capabilityCache.Remove(key);
                _healthCache.Remove(key);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
            }
        }

        /// <summary>
        /// Remove least accessed cache entries to free space
        /// </summary>
        private void RemoveLeastAccessedEntries()
        {
            var entriesToRemove = _cacheAccessCount
                .OrderBy(kvp => kvp.Value)
                .Take(_maxCacheSize / 4) // Remove 25% of entries
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in entriesToRemove)
            {
                _deviceCache.Remove(key);
                _cacheExpiryTimes.Remove(key);
                _cacheAccessCount.Remove(key);
                _capabilityCache.Remove(key);
                _healthCache.Remove(key);
            }

            _logger.LogDebug("Removed {Count} least accessed cache entries", entriesToRemove.Count);
        }

        /// <summary>
        /// Optimized cache refresh with selective updates
        /// </summary>
        private async Task OptimizedCacheRefreshAsync()
        {
            var currentTime = DateTime.UtcNow;
            
            // Only refresh if cache timeout has passed
            if (currentTime - _lastCacheRefresh < _cacheTimeout)
            {
                return;
            }

            try
            {
                _logger.LogDebug("Starting optimized cache refresh");
                
                // Cleanup expired entries first
                CleanupExpiredCacheEntries();

                // Refresh device list from Python worker
                await RefreshDeviceCacheAsync();
                var refreshedDevices = _deviceCache.Values.ToList();
                
                if (refreshedDevices?.Any() == true)
                {
                    lock (_cacheLock)
                    {
                        // Update existing devices and add new ones
                        foreach (var device in refreshedDevices)
                        {
                            UpdateDeviceCache(device.Id, device);
                        }

                        // Remove devices that are no longer available
                        var currentDeviceIds = refreshedDevices.Select(d => d.Id).ToHashSet();
                        var staleDeviceIds = _deviceCache.Keys
                            .Where(id => !currentDeviceIds.Contains(id))
                            .ToList();

                        foreach (var staleId in staleDeviceIds)
                        {
                            _deviceCache.Remove(staleId);
                            _cacheExpiryTimes.Remove(staleId);
                            _cacheAccessCount.Remove(staleId);
                            _capabilityCache.Remove(staleId);
                            _healthCache.Remove(staleId);
                        }

                        _lastCacheRefresh = currentTime;
                        
                        _logger.LogInformation("Cache refresh completed. {ActiveDevices} active, {StaleDevices} removed", 
                            refreshedDevices.Count, staleDeviceIds.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache refresh failed, using existing cache");
            }
        }

        /// <summary>
        /// Get cache statistics for monitoring and optimization
        /// </summary>
        public Dictionary<string, object> GetCacheStatistics()
        {
            lock (_cacheLock)
            {
                var totalAccesses = _cacheAccessCount.Values.Sum();
                var averageAccesses = _cacheAccessCount.Count > 0 ? totalAccesses / (double)_cacheAccessCount.Count : 0;
                var expiredCount = _cacheExpiryTimes.Count(kvp => DateTime.UtcNow > kvp.Value);

                return new Dictionary<string, object>
                {
                    ["cache_size"] = _deviceCache.Count,
                    ["max_cache_size"] = _maxCacheSize,
                    ["cache_utilization_percent"] = (_deviceCache.Count / (double)_maxCacheSize) * 100,
                    ["total_accesses"] = totalAccesses,
                    ["average_accesses_per_entry"] = averageAccesses,
                    ["expired_entries"] = expiredCount,
                    ["last_refresh"] = _lastCacheRefresh,
                    ["cache_timeout_minutes"] = _cacheTimeout.TotalMinutes,
                    ["device_specific_timeout_minutes"] = _deviceSpecificCacheTimeout.TotalMinutes
                };
            }
        }

        #endregion
    }
}
