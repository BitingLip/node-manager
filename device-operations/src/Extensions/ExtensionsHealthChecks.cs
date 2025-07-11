using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace DeviceOperations.Extensions;

/// <summary>
/// Extension methods for configuring health checks and monitoring
/// </summary>
public static class ExtensionsHealthChecks
{
    /// <summary>
    /// Adds comprehensive health checks for the application
    /// </summary>
    public static IServiceCollection AddApplicationHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddHealthChecks();

        // Basic application health check
        builder.AddCheck("application", () => HealthCheckResult.Healthy("Application is running"));

        // Memory usage health check
        builder.AddCheck<MemoryUsageHealthCheck>("memory_usage");

        // Disk space health check
        builder.AddCheck<DiskSpaceHealthCheck>("disk_space");

        // GPU health check
        builder.AddCheck<GpuHealthCheck>("gpu_health");

        return services;
    }

    /// <summary>
    /// Configures health check endpoints
    /// </summary>
    public static IApplicationBuilder UseApplicationHealthChecks(this IApplicationBuilder app)
    {
        // Basic health check endpoint
        app.UseHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    timestamp = DateTime.UtcNow,
                    duration = report.TotalDuration.TotalMilliseconds
                };
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            }
        });

        // Detailed health check endpoint
        app.UseHealthChecks("/health/detailed", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    timestamp = DateTime.UtcNow,
                    duration = report.TotalDuration.TotalMilliseconds,
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        duration = e.Value.Duration.TotalMilliseconds,
                        description = e.Value.Description,
                        data = e.Value.Data,
                        exception = e.Value.Exception?.Message
                    })
                };
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            }
        });

        return app;
    }
}

/// <summary>
/// Health check for memory usage
/// </summary>
public class MemoryUsageHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryUsage = process.WorkingSet64;
            var memoryUsageMB = memoryUsage / (1024 * 1024);
            
            var data = new Dictionary<string, object>
            {
                ["memory_usage_bytes"] = memoryUsage,
                ["memory_usage_mb"] = memoryUsageMB
            };

            // Warning threshold: 4GB
            if (memoryUsageMB > 4096)
            {
                return Task.FromResult(new HealthCheckResult(HealthStatus.Degraded, $"High memory usage: {memoryUsageMB} MB", null, data));
            }
            // Critical threshold: 6GB
            else if (memoryUsageMB > 6144)
            {
                return Task.FromResult(new HealthCheckResult(HealthStatus.Unhealthy, $"Critical memory usage: {memoryUsageMB} MB", null, data));
            }
            else
            {
                return Task.FromResult(new HealthCheckResult(HealthStatus.Healthy, $"Memory usage: {memoryUsageMB} MB", null, data));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Memory usage check failed: {ex.Message}"));
        }
    }
}

/// <summary>
/// Health check for disk space
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
            var issues = new List<string>();
            var data = new Dictionary<string, object>();
            
            foreach (var drive in drives)
            {
                var freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                var totalSpaceGB = drive.TotalSize / (1024 * 1024 * 1024);
                var usedPercentage = ((double)(totalSpaceGB - freeSpaceGB) / totalSpaceGB) * 100;
                
                data[$"drive_{drive.Name.Replace(":", "").Replace("\\", "")}_free_gb"] = freeSpaceGB;
                data[$"drive_{drive.Name.Replace(":", "").Replace("\\", "")}_used_percent"] = Math.Round(usedPercentage, 2);
                
                if (usedPercentage > 90)
                {
                    issues.Add($"Drive {drive.Name}: {usedPercentage:F1}% used");
                }
            }
            
            if (issues.Any())
            {
                return Task.FromResult(new HealthCheckResult(HealthStatus.Degraded, $"Disk space issues: {string.Join(", ", issues)}", null, data));
            }
            else
            {
                return Task.FromResult(new HealthCheckResult(HealthStatus.Healthy, "Disk space is adequate", null, data));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Disk space check failed: {ex.Message}"));
        }
    }
}

/// <summary>
/// Health check for GPU status
/// </summary>
public class GpuHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // This would typically use GPU monitoring libraries
            // For now, we'll do a basic check
            var data = new Dictionary<string, object>();
            
            // Check CUDA availability
            var cudaAvailable = Environment.GetEnvironmentVariable("CUDA_VISIBLE_DEVICES") != null;
            data["cuda_available"] = cudaAvailable;
            
            // Check DirectML availability (Windows)
            var directMLAvailable = OperatingSystem.IsWindows();
            data["directml_available"] = directMLAvailable;
            
            if (cudaAvailable || directMLAvailable)
            {
                return Task.FromResult(HealthCheckResult.Healthy("GPU acceleration available", data));
            }
            else
            {
                return Task.FromResult(new HealthCheckResult(HealthStatus.Degraded, "No GPU acceleration available", null, data));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"GPU health check failed: {ex.Message}"));
        }
    }
}
