using DeviceOperations.Models.Common;
using Vortice.DXGI;
using Vortice.Direct3D12;
using Vortice.DirectML;
using static Vortice.DirectML.DML;

namespace DeviceOperations.Services.Core;

public class DirectMLService : IDeviceService, IDisposable
{
    private readonly ILogger<DirectMLService> _logger;
    private readonly Dictionary<string, DeviceInfo> _devices = new();
    private readonly Dictionary<string, IDMLDevice> _dmlDevices = new();
    private readonly Dictionary<string, ID3D12Device> _d3d12Devices = new();
    private IDXGIFactory4? _dxgiFactory;
    private bool _initialized = false;

    public DirectMLService(ILogger<DirectMLService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Initializing DirectML devices...");

                // Create DXGI factory for device enumeration
                _dxgiFactory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();

                int adapterIndex = 0;
                while (true)
                {
                    // Enumerate DXGI adapters
                    var result = _dxgiFactory.EnumAdapters1(adapterIndex, out IDXGIAdapter1? adapter);
                    if (result == Vortice.DXGI.ResultCode.NotFound || adapter == null)
                        break;

                    try
                    {
                        var adapterDesc = adapter.Description1;
                        
                        // Skip software adapters
                        if ((adapterDesc.Flags & AdapterFlags.Software) != 0)
                        {
                            adapter.Dispose();
                            adapterIndex++;
                            continue;
                        }

                        _logger.LogInformation($"Found adapter {adapterIndex}: {adapterDesc.Description}");

                        // Try to create D3D12 device
                        var d3d12Result = D3D12.D3D12CreateDevice(adapter, Vortice.Direct3D.FeatureLevel.Level_11_0, out ID3D12Device? d3d12Device);
                        
                        if (d3d12Result.Success && d3d12Device != null)
                        {                        // Try to create DirectML device
                        var dmlResult = DMLCreateDevice(d3d12Device, CreateDeviceFlags.None, out IDMLDevice? dmlDevice);
                            
                            if (dmlResult.Success && dmlDevice != null)
                            {
                                var deviceId = $"gpu_{adapterIndex}";
                                
                                // Get memory information from adapter description
                                var totalMemory = (long)adapterDesc.DedicatedVideoMemory;
                                var availableMemory = totalMemory; // Assume available for now

                                var deviceInfo = new DeviceInfo
                                {
                                    DeviceId = deviceId,
                                    Name = adapterDesc.Description.Trim(),
                                    VendorId = adapterDesc.VendorId,
                                    DeviceType = GetVendorName(adapterDesc.VendorId),
                                    TotalMemory = totalMemory,
                                    AvailableMemory = availableMemory,
                                    IsAvailable = true
                                };

                                _devices[deviceId] = deviceInfo;
                                _d3d12Devices[deviceId] = d3d12Device;
                                _dmlDevices[deviceId] = dmlDevice;

                                _logger.LogInformation($"Initialized DirectML device: {deviceId} - {deviceInfo.Name} " +
                                    $"(Memory: {totalMemory / (1024 * 1024 * 1024)}GB total, {availableMemory / (1024 * 1024 * 1024)}GB available)");
                            }
                            else
                            {
                                _logger.LogWarning($"Failed to create DirectML device for adapter {adapterIndex}: {adapterDesc.Description}");
                                d3d12Device.Dispose();
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Failed to create D3D12 device for adapter {adapterIndex}: {adapterDesc.Description}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing adapter {adapterIndex}");
                    }
                    finally
                    {
                        adapter.Dispose();
                    }

                    adapterIndex++;
                }

                // If no DirectML devices found, create a fallback CPU device
                if (_devices.Count == 0)
                {
                    _logger.LogWarning("No DirectML-compatible devices found. Creating CPU fallback device.");
                    
                    var cpuDevice = new DeviceInfo
                    {
                        DeviceId = "cpu_0",
                        Name = "CPU Fallback Device",
                        VendorId = 0x0000,
                        DeviceType = "CPU",
                        TotalMemory = GetSystemMemory(),
                        AvailableMemory = GetAvailableSystemMemory(),
                        IsAvailable = true
                    };
                    
                    _devices["cpu_0"] = cpuDevice;
                    _logger.LogInformation($"Created CPU fallback device with {cpuDevice.TotalMemory / (1024 * 1024 * 1024)}GB memory");
                }

                _initialized = true;
                _logger.LogInformation($"DirectML initialization complete. Found {_devices.Count} devices.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize DirectML");
                
                // Create emergency fallback device
                var fallbackDevice = new DeviceInfo
                {
                    DeviceId = "fallback_0",
                    Name = "Emergency Fallback Device",
                    VendorId = 0x0000,
                    DeviceType = "Fallback",
                    TotalMemory = 8L * 1024 * 1024 * 1024, // 8GB
                    AvailableMemory = 8L * 1024 * 1024 * 1024,
                    IsAvailable = true
                };
                
                _devices["fallback_0"] = fallbackDevice;
                _initialized = true;
                
                _logger.LogWarning("Created emergency fallback device due to initialization failure");
            }
        });
    }

    public async Task<IEnumerable<DeviceInfo>> GetAvailableDevicesAsync()
    {
        if (!_initialized) await InitializeAsync();
        return _devices.Values.Where(d => d.IsAvailable);
    }

    public async Task<DeviceInfo?> GetDeviceAsync(string deviceId)
    {
        if (!_initialized) await InitializeAsync();
        return _devices.GetValueOrDefault(deviceId);
    }

    public async Task<bool> IsDeviceAvailableAsync(string deviceId)
    {
        if (!_initialized) await InitializeAsync();
        return _devices.TryGetValue(deviceId, out var device) && device.IsAvailable;
    }

    public void Dispose()
    {
        // Dispose DirectML devices
        foreach (var dmlDevice in _dmlDevices.Values)
        {
            dmlDevice?.Dispose();
        }
        _dmlDevices.Clear();

        // Dispose D3D12 devices
        foreach (var d3d12Device in _d3d12Devices.Values)
        {
            d3d12Device?.Dispose();
        }
        _d3d12Devices.Clear();

        // Dispose DXGI factory
        _dxgiFactory?.Dispose();
        _dxgiFactory = null;

        _devices.Clear();
        _initialized = false;
    }

    private static string GetVendorName(int vendorId)
    {
        return vendorId switch
        {
            0x1002 => "AMD",
            0x10DE => "NVIDIA", 
            0x8086 => "Intel",
            0x1414 => "Microsoft",
            _ => "Unknown"
        };
    }

    private static long GetSystemMemory()
    {
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            return gcMemoryInfo.TotalAvailableMemoryBytes;
        }
        catch
        {
            return 8L * 1024 * 1024 * 1024; // 8GB fallback
        }
    }

    private static long GetAvailableSystemMemory()
    {
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            return gcMemoryInfo.TotalAvailableMemoryBytes - gcMemoryInfo.MemoryLoadBytes;
        }
        catch
        {
            return 6L * 1024 * 1024 * 1024; // 6GB fallback
        }
    }

    /// <summary>
    /// Get the DirectML device for advanced operations
    /// </summary>
    public IDMLDevice? GetDirectMLDevice(string deviceId)
    {
        return _dmlDevices.GetValueOrDefault(deviceId);
    }

    /// <summary>
    /// Get the D3D12 device for advanced operations
    /// </summary>
    public ID3D12Device? GetD3D12Device(string deviceId)
    {
        return _d3d12Devices.GetValueOrDefault(deviceId);
    }
}
