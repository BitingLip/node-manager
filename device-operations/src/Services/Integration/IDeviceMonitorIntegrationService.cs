using DeviceOperations.Models.Common;

namespace DeviceOperations.Services.Integration;

public interface IDeviceMonitorIntegrationService
{
    /// <summary>
    /// Get real-time device health from the device-monitor service
    /// </summary>
    Task<IEnumerable<DeviceHealthInfo>> GetDeviceHealthAsync();
    
    /// <summary>
    /// Get health info for a specific device
    /// </summary>
    Task<DeviceHealthInfo?> GetDeviceHealthAsync(string deviceId);
    
    /// <summary>
    /// Get device status history from the monitoring database
    /// </summary>
    Task<IEnumerable<DeviceHealthHistory>> GetDeviceHealthHistoryAsync(string deviceId, DateTime from, DateTime to);
    
    /// <summary>
    /// Check if a device is available for operations based on health metrics
    /// </summary>
    Task<bool> IsDeviceAvailableForOperationsAsync(string deviceId);
    
    /// <summary>
    /// Get aggregated device usage statistics
    /// </summary>
    Task<DeviceUsageStatistics?> GetDeviceUsageStatisticsAsync(string deviceId, TimeSpan period);
}

public class DeviceHealthInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceVendor { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public long? MemoryCapacity { get; set; }
    public double? MemoryUsage { get; set; }
    public double? ProcessingUsage { get; set; }
    public double? Temperature { get; set; }
    public double? PowerUsage { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }
    public bool IsAvailableForOperations { get; set; }
}

public class DeviceHealthHistory
{
    public string DeviceId { get; set; } = string.Empty;
    public double ProcessingUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double? Temperature { get; set; }
    public double? PowerUsage { get; set; }
    public DateTime Timestamp { get; set; }
}

public class DeviceUsageStatistics
{
    public string DeviceId { get; set; } = string.Empty;
    public double AverageProcessingUsage { get; set; }
    public double AverageMemoryUsage { get; set; }
    public double? AverageTemperature { get; set; }
    public double? AveragePowerUsage { get; set; }
    public double MaxProcessingUsage { get; set; }
    public double MaxMemoryUsage { get; set; }
    public double? MaxTemperature { get; set; }
    public double? MaxPowerUsage { get; set; }
    public int TotalSamples { get; set; }
    public TimeSpan Period { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
