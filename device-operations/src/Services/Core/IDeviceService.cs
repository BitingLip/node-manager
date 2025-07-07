using DeviceOperations.Models.Common;

namespace DeviceOperations.Services.Core;

public interface IDeviceService
{
    Task<IEnumerable<DeviceInfo>> GetAvailableDevicesAsync();
    Task<DeviceInfo?> GetDeviceAsync(string deviceId);
    Task<bool> IsDeviceAvailableAsync(string deviceId);
    Task InitializeAsync();
}
