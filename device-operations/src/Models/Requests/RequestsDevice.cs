namespace DeviceOperations.Models.Requests;

using DeviceOperations.Models.Common;

/// <summary>
/// Device operation requests
/// </summary>
public static class RequestsDevice
{
    /// <summary>
    /// Request to list all available devices
    /// </summary>
    public class ListDevicesRequest
    {
        /// <summary>
        /// Filter by device type
        /// </summary>
        public DeviceType? DeviceType { get; set; }

        /// <summary>
        /// Filter by device status
        /// </summary>
        public DeviceStatus? Status { get; set; }

        /// <summary>
        /// Include detailed information
        /// </summary>
        public bool IncludeDetails { get; set; } = false;

        /// <summary>
        /// Include health information
        /// </summary>
        public bool IncludeHealth { get; set; } = false;

        /// <summary>
        /// Include utilization metrics
        /// </summary>
        public bool IncludeUtilization { get; set; } = false;
    }

    /// <summary>
    /// Request to get device information by ID
    /// </summary>
    public class GetDeviceRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Include detailed specifications
        /// </summary>
        public bool IncludeSpecifications { get; set; } = true;

        /// <summary>
        /// Include current health status
        /// </summary>
        public bool IncludeHealth { get; set; } = true;

        /// <summary>
        /// Include current utilization
        /// </summary>
        public bool IncludeUtilization { get; set; } = true;

        /// <summary>
        /// Include driver information
        /// </summary>
        public bool IncludeDriverInfo { get; set; } = false;
    }

    /// <summary>
    /// Request to get device status
    /// </summary>
    public class GetDeviceStatusRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Include performance metrics
        /// </summary>
        public bool IncludeMetrics { get; set; } = true;

        /// <summary>
        /// Include current workload
        /// </summary>
        public bool IncludeWorkload { get; set; } = true;
    }

    /// <summary>
    /// Request to get device health information
    /// </summary>
    public class GetDeviceHealthRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Include historical health data
        /// </summary>
        public bool IncludeHistory { get; set; } = false;

        /// <summary>
        /// Time range for historical data (hours)
        /// </summary>
        public int HistoryHours { get; set; } = 1;
    }

    /// <summary>
    /// Request to enable/disable a device
    /// </summary>
    public class SetDeviceStatusRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Desired device status
        /// </summary>
        public DeviceStatus Status { get; set; }

        /// <summary>
        /// Reason for status change
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Force status change even if device is busy
        /// </summary>
        public bool Force { get; set; } = false;
    }

    /// <summary>
    /// Request to reset a device
    /// </summary>
    public class ResetDeviceRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Reset type
        /// </summary>
        public DeviceResetType ResetType { get; set; }

        /// <summary>
        /// Force reset even if device has active sessions
        /// </summary>
        public bool Force { get; set; } = false;

        /// <summary>
        /// Wait for reset completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;

        /// <summary>
        /// Timeout for reset operation in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Request to benchmark a device
    /// </summary>
    public class BenchmarkDeviceRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Benchmark type to run
        /// </summary>
        public BenchmarkType BenchmarkType { get; set; }

        /// <summary>
        /// Benchmark duration in seconds
        /// </summary>
        public int DurationSeconds { get; set; } = 60;

        /// <summary>
        /// Test model to use for benchmarking
        /// </summary>
        public string? TestModelId { get; set; }

        /// <summary>
        /// Number of concurrent operations
        /// </summary>
        public int Concurrency { get; set; } = 1;

        /// <summary>
        /// Save detailed results
        /// </summary>
        public bool SaveResults { get; set; } = true;
    }

    /// <summary>
    /// Request to optimize device settings
    /// </summary>
    public class OptimizeDeviceRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Optimization target
        /// </summary>
        public OptimizationTarget Target { get; set; }

        /// <summary>
        /// Workload type to optimize for
        /// </summary>
        public WorkloadType WorkloadType { get; set; }

        /// <summary>
        /// Apply optimization automatically
        /// </summary>
        public bool AutoApply { get; set; } = false;

        /// <summary>
        /// Custom optimization parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Request to update device configuration
    /// </summary>
    public class UpdateDeviceConfigRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Configuration updates
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();

        /// <summary>
        /// Validate configuration before applying
        /// </summary>
        public bool Validate { get; set; } = true;

        /// <summary>
        /// Apply configuration immediately
        /// </summary>
        public bool ApplyImmediately { get; set; } = true;
    }
}

/// <summary>
/// Workload type enumeration
/// </summary>
public enum WorkloadType
{
    ImageGeneration = 0,
    TextGeneration = 1,
    ImageToImage = 2,
    Upscaling = 3,
    BatchProcessing = 4,
    Interactive = 5
}
