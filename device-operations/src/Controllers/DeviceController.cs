using DeviceOperations.Services.Core;
using Microsoft.AspNetCore.Mvc;

namespace DeviceOperations.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeviceController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ILogger<DeviceController> _logger;

    public DeviceController(IDeviceService deviceService, ILogger<DeviceController> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetDevices()
    {
        var devices = await _deviceService.GetAvailableDevicesAsync();
        return Ok(new { devices, count = devices.Count() });
    }

    [HttpGet("{deviceId}")]
    public async Task<IActionResult> GetDevice(string deviceId)
    {
        var device = await _deviceService.GetDeviceAsync(deviceId);
        if (device == null)
        {
            return NotFound(new { error = $"Device {deviceId} not found" });
        }
        return Ok(device);
    }
}
