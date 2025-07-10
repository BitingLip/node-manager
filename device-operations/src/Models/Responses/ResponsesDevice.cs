namespace DeviceOperations.Models.Responses;

using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;

/// <summary>
/// Response for listing devices
/// </summary>
public class ListDevicesResponse
{
    /// <summary>
    /// List of devices
    /// </summary>
    public List<DeviceInfo> Devices { get; set; } = new();

    /// <summary>
    /// Total number of devices
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Available devices count
    /// </summary>
    public int AvailableCount { get; set; }

    /// <summary>
    /// Busy devices count
    /// </summary>
    public int BusyCount { get; set; }

    /// <summary>
    /// Offline devices count
    /// </summary>
    public int OfflineCount { get; set; }
}

/// <summary>
/// Response for getting device information
/// </summary>
public class GetDeviceResponse
{
    /// <summary>
    /// Device information
    /// </summary>
    public DeviceInfo Device { get; set; } = new();

    /// <summary>
    /// Device compatibility information
    /// </summary>
    public DeviceCompatibility Compatibility { get; set; } = new();

    /// <summary>
    /// Current workload information
    /// </summary>
    public DeviceWorkload? CurrentWorkload { get; set; }
}

/// <summary>
/// Response for getting device status
/// </summary>
public class GetDeviceStatusResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Current status
    /// </summary>
    public DeviceStatus Status { get; set; }

    /// <summary>
    /// Status description
    /// </summary>
    public string StatusDescription { get; set; } = string.Empty;

    /// <summary>
    /// Utilization metrics
    /// </summary>
    public DeviceUtilization Utilization { get; set; } = new();

    /// <summary>
    /// Performance metrics
    /// </summary>
    public DevicePerformanceMetrics Performance { get; set; } = new();

    /// <summary>
    /// Current workload
    /// </summary>
    public DeviceWorkload? CurrentWorkload { get; set; }

    /// <summary>
    /// Last status update timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response for device health check
/// </summary>
public class PostDeviceHealthResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Current health status
    /// </summary>
    public DeviceHealth Health { get; set; } = new();

    /// <summary>
    /// Health score (0-100)
    /// </summary>
    public double HealthScore { get; set; }

    /// <summary>
    /// Health recommendations
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Warning messages
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Response for resetting a device
/// </summary>
public class PostDeviceResetResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Reset type performed
    /// </summary>
    public Common.DeviceResetType ResetType { get; set; }

    /// <summary>
    /// Reset duration in milliseconds
    /// </summary>
    public long ResetDurationMs { get; set; }

    /// <summary>
    /// Whether the reset was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Reset message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Device status after reset
    /// </summary>
    public DeviceStatus PostResetStatus { get; set; }

    /// <summary>
    /// Reset timestamp
    /// </summary>
    public DateTime ResetAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response for device benchmark
/// </summary>
public class PostDeviceBenchmarkResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Benchmark type
    /// </summary>
    public Common.BenchmarkType BenchmarkType { get; set; }

    /// <summary>
    /// Benchmark results
    /// </summary>
    public DeviceBenchmarkResults Results { get; set; } = new();

    /// <summary>
    /// Benchmark duration in seconds
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Benchmark timestamp
    /// </summary>
    public DateTime BenchmarkAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the benchmark completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Benchmark message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response for device optimization
/// </summary>
public class PostDeviceOptimizeResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Optimization target
    /// </summary>
    public Common.OptimizationTarget Target { get; set; }

    /// <summary>
    /// Optimization results
    /// </summary>
    public DeviceOptimizationResults Results { get; set; } = new();

    /// <summary>
    /// Whether optimization was applied
    /// </summary>
    public bool Applied { get; set; }

    /// <summary>
    /// Optimization recommendations
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Optimization timestamp
    /// </summary>
    public DateTime OptimizedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response for getting device configuration
/// </summary>
public class GetDeviceConfigResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Current configuration
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Configuration schema
    /// </summary>
    public Dictionary<string, ConfigurationSchema> Schema { get; set; } = new();

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response for updating device configuration
/// </summary>
public class PutDeviceConfigResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Updated configuration keys
    /// </summary>
    public List<string> UpdatedKeys { get; set; } = new();

    /// <summary>
    /// Failed configuration keys
    /// </summary>
    public List<string> FailedKeys { get; set; } = new();

    /// <summary>
    /// Configuration validation results
    /// </summary>
    public Dictionary<string, string> ValidationResults { get; set; } = new();

    /// <summary>
    /// Whether configuration was applied
    /// </summary>
    public bool Applied { get; set; }

    /// <summary>
    /// Update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Device compatibility information
/// </summary>
public class DeviceCompatibility
{
    /// <summary>
    /// Supported model types
    /// </summary>
    public List<ModelType> SupportedModelTypes { get; set; } = new();

    /// <summary>
    /// Supported precisions
    /// </summary>
    public List<Common.ModelPrecision> SupportedPrecisions { get; set; } = new();

    /// <summary>
    /// Maximum supported model size in bytes
    /// </summary>
    public long MaxModelSize { get; set; }

    /// <summary>
    /// Maximum supported resolution
    /// </summary>
    public Common.ImageResolution MaxResolution { get; set; } = new();

    /// <summary>
    /// Supported features
    /// </summary>
    public List<string> SupportedFeatures { get; set; } = new();
}

/// <summary>
/// Device workload information
/// </summary>
public class DeviceWorkload
{
    /// <summary>
    /// Active sessions count
    /// </summary>
    public int ActiveSessions { get; set; }

    /// <summary>
    /// Queued operations count
    /// </summary>
    public int QueuedOperations { get; set; }

    /// <summary>
    /// Current operation details
    /// </summary>
    public List<WorkloadOperation> CurrentOperations { get; set; } = new();

    /// <summary>
    /// Estimated completion time
    /// </summary>
    public DateTime? EstimatedCompletion { get; set; }

    /// <summary>
    /// Workload priority distribution
    /// </summary>
    public Dictionary<SessionPriority, int> PriorityDistribution { get; set; } = new();
}

/// <summary>
/// Workload operation details
/// </summary>
public class WorkloadOperation
{
    /// <summary>
    /// Operation identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Operation type
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Session identifier
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Operation priority
    /// </summary>
    public SessionPriority Priority { get; set; }

    /// <summary>
    /// Operation progress (0-100)
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// Estimated completion time
    /// </summary>
    public DateTime? EstimatedCompletion { get; set; }

    /// <summary>
    /// Operation start time
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Device performance metrics
/// </summary>
public class DevicePerformanceMetrics
{
    /// <summary>
    /// Operations per second
    /// </summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>
    /// Average operation time in milliseconds
    /// </summary>
    public double AverageOperationTime { get; set; }

    /// <summary>
    /// Throughput in MB/s
    /// </summary>
    public double ThroughputMBps { get; set; }

    /// <summary>
    /// Error rate percentage
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Uptime percentage
    /// </summary>
    public double UptimePercentage { get; set; }

    /// <summary>
    /// Performance score (0-100)
    /// </summary>
    public double PerformanceScore { get; set; }
}

/// <summary>
/// Device optimization results
/// </summary>
public class DeviceOptimizationResults
{
    /// <summary>
    /// Current settings before optimization
    /// </summary>
    public Dictionary<string, object> CurrentSettings { get; set; } = new();

    /// <summary>
    /// Recommended settings
    /// </summary>
    public Dictionary<string, object> RecommendedSettings { get; set; } = new();

    /// <summary>
    /// Expected performance improvement percentage
    /// </summary>
    public double ExpectedImprovement { get; set; }

    /// <summary>
    /// Optimization confidence score (0-100)
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Detailed optimization analysis
    /// </summary>
    public Dictionary<string, string> Analysis { get; set; } = new();
}

/// <summary>
/// Configuration schema definition
/// </summary>
public class ConfigurationSchema
{
    /// <summary>
    /// Parameter type
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Parameter description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Default value
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Minimum value (for numeric types)
    /// </summary>
    public object? MinValue { get; set; }

    /// <summary>
    /// Maximum value (for numeric types)
    /// </summary>
    public object? MaxValue { get; set; }

    /// <summary>
    /// Valid options (for enum types)
    /// </summary>
    public List<object> ValidOptions { get; set; } = new();

    /// <summary>
    /// Whether this parameter is required
    /// </summary>
    public bool Required { get; set; }
}
