using DeviceOperations.Services.Core;

namespace DeviceOperations.Middleware;

public class DeviceAvailabilityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DeviceAvailabilityMiddleware> _logger;

    public DeviceAvailabilityMiddleware(RequestDelegate next, ILogger<DeviceAvailabilityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IDeviceService deviceService)
    {
        // Skip for non-API routes
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Extract device ID from route or query
        string? deviceId = null;
        
        if (context.Request.RouteValues.TryGetValue("deviceId", out var routeDeviceId))
        {
            deviceId = routeDeviceId?.ToString();
        }
        else if (context.Request.Query.TryGetValue("deviceId", out var queryDeviceId))
        {
            deviceId = queryDeviceId.FirstOrDefault();
        }

        if (!string.IsNullOrEmpty(deviceId))
        {
            var isAvailable = await deviceService.IsDeviceAvailableAsync(deviceId);
            if (!isAvailable)
            {
                _logger.LogWarning($"Device {deviceId} is not available");
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new 
                { 
                    error = $"Device {deviceId} is not available" 
                });
                return;
            }
        }

        await _next(context);
    }
}
