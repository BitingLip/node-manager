using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using static DeviceOperations.Models.Requests.RequestsDevice;

namespace DeviceOperations.Services.Device;

/// <summary>
/// Interface for device management operations
/// Core Responsibilities: Hardware discovery, monitoring, control, and optimization
/// </summary>
public interface IServiceDevice
{
    /// <summary>
    /// Device Discovery and Enumeration
    /// Enumerate all available CPU and GPU devices in the system
    /// </summary>
    /// <returns>List of all discovered devices</returns>
    Task<ApiResponse<GetDeviceListResponse>> GetDeviceListAsync();

    /// <summary>
    /// Get specific device information by ID
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <returns>Device information and details</returns>
    Task<ApiResponse<GetDeviceResponse>> GetDeviceAsync(string deviceId);

    /// <summary>
    /// Get device capabilities and feature support
    /// Retrieve device capabilities and feature support
    /// </summary>
    /// <param name="deviceId">Device identifier (optional)</param>
    /// <returns>Device capabilities information</returns>
    Task<ApiResponse<DeviceCapabilities>> GetDeviceCapabilitiesAsync(string? deviceId = null);

    /// <summary>
    /// Get current operational status of devices
    /// Monitor device availability and responsiveness
    /// </summary>
    /// <param name="deviceId">Device identifier (optional)</param>
    /// <returns>Device status information</returns>
    Task<ApiResponse<GetDeviceStatusResponse>> GetDeviceStatusAsync(string? deviceId = null);

    /// <summary>
    /// Perform device health check operation
    /// Run comprehensive health diagnostics
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="request">Health check parameters</param>
    /// <returns>Health check results</returns>
    Task<ApiResponse<PostDeviceHealthResponse>> PostDeviceHealthAsync(string deviceId, PostDeviceHealthRequest request);

    /// <summary>
    /// Run device benchmarks for performance testing
    /// Execute performance benchmarks and collect metrics
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="request">Benchmark parameters</param>
    /// <returns>Benchmark results</returns>
    Task<ApiResponse<PostDeviceBenchmarkResponse>> PostDeviceBenchmarkAsync(string deviceId, PostDeviceBenchmarkRequest request);

    /// <summary>
    /// Optimize device performance settings
    /// Tune device parameters for optimal operation
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="request">Optimization parameters</param>
    /// <returns>Optimization results</returns>
    Task<ApiResponse<PostDeviceOptimizeResponse>> PostDeviceOptimizeAsync(string deviceId, PostDeviceOptimizeRequest request);
    Task<ApiResponse<PostDeviceOptimizeResponse>> PostDeviceOptimizeAsync(PostDeviceOptimizeRequest request);
    Task<ApiResponse<PostDeviceOptimizeResponse>> PostDeviceOptimizeAsync(PostDeviceOptimizeRequest request, string deviceId);

    /// <summary>
    /// Get device configuration settings
    /// Retrieve current device configuration parameters
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <returns>Device configuration</returns>
    Task<ApiResponse<GetDeviceConfigResponse>> GetDeviceConfigAsync(string deviceId);

    /// <summary>
    /// Update device configuration settings
    /// Apply configuration changes to device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="request">Configuration update parameters</param>
    /// <returns>Configuration update result</returns>
    Task<ApiResponse<PutDeviceConfigResponse>> PutDeviceConfigAsync(string deviceId, PutDeviceConfigRequest request);

    /// <summary>
    /// Get detailed device information and specifications
    /// Offer detailed device metadata for system analysis
    /// </summary>
    /// <param name="deviceId">Device identifier (optional)</param>
    /// <returns>Detailed device information</returns>
    Task<ApiResponse<DeviceInfo>> GetDeviceDetailsAsync(string? deviceId = null);

    /// <summary>
    /// Get device driver information and versions
    /// Monitor driver health and installation status
    /// </summary>
    /// <param name="deviceId">Device identifier (optional)</param>
    /// <returns>Driver information</returns>
    Task<ApiResponse<DriverInfo>> GetDeviceDriversAsync(string? deviceId = null);

    /// <summary>
    /// Create or update a device set for coordinated operations
    /// Configure multiple devices to work together as a set
    /// </summary>
    /// <param name="request">Device set configuration parameters</param>
    /// <returns>Device set configuration result</returns>
    Task<ApiResponse<SetDeviceSetResponse>> PostDeviceSetAsync(SetDeviceSetRequest request);

    /// <summary>
    /// Get device memory information and allocation status
    /// Retrieve current memory usage and allocation information for a device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <returns>Device memory information</returns>
    Task<ApiResponse<GetDeviceMemoryResponse>> GetDeviceMemoryAsync(string deviceId);
}
