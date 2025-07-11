using System.Diagnostics;
using DeviceOperations.Services.Device;
using DeviceOperations.Services.Inference;
using DeviceOperations.Services.Memory;
using DeviceOperations.Services.Model;
using DeviceOperations.Services.Processing;
using DeviceOperations.Services.Postprocessing;
using DeviceOperations.Services.Python;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace DeviceOperations.Tests.Performance;

/// <summary>
/// Base class for performance tests with common infrastructure
/// </summary>
public abstract class PerformanceTestBase : IDisposable
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly Mock<IPythonWorkerService> MockPythonWorkerService;
    protected readonly Stopwatch Stopwatch;
    protected readonly List<PerformanceMetric> Metrics;

    protected PerformanceTestBase()
    {
        // Setup service collection
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        // Mock Python worker service
        MockPythonWorkerService = new Mock<IPythonWorkerService>();
        services.AddSingleton(MockPythonWorkerService.Object);
        
        // Add services
        services.AddScoped<IServiceDevice, ServiceDevice>();
        services.AddScoped<IServiceInference, ServiceInference>();
        services.AddScoped<IServiceMemory, ServiceMemory>();
        services.AddScoped<IServiceModel, ServiceModel>();
        services.AddScoped<IServiceProcessing, ServiceProcessing>();
        services.AddScoped<IServicePostprocessing, ServicePostprocessing>();
        
        ServiceProvider = services.BuildServiceProvider();
        Stopwatch = new Stopwatch();
        Metrics = new List<PerformanceMetric>();
    }

    /// <summary>
    /// Execute a performance test and capture metrics
    /// </summary>
    protected async Task<PerformanceResult> ExecutePerformanceTest<T>(
        string testName,
        Func<Task<T>> operation,
        int iterations = 100,
        TimeSpan? maxAcceptableTime = null)
    {
        var results = new List<TimeSpan>();
        var errors = new List<Exception>();

        // Warmup
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            // Warmup failure is acceptable
            Console.WriteLine($"Warmup failed for {testName}: {ex.Message}");
        }

        // Execute test iterations
        for (int i = 0; i < iterations; i++)
        {
            Stopwatch.Restart();
            try
            {
                await operation();
                Stopwatch.Stop();
                results.Add(Stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                Stopwatch.Stop();
                errors.Add(ex);
            }
        }

        var result = new PerformanceResult
        {
            TestName = testName,
            Iterations = iterations,
            SuccessfulIterations = results.Count,
            FailedIterations = errors.Count,
            AverageTime = results.Count > 0 ? TimeSpan.FromTicks((long)results.Average(r => r.Ticks)) : TimeSpan.Zero,
            MinTime = results.Count > 0 ? results.Min() : TimeSpan.Zero,
            MaxTime = results.Count > 0 ? results.Max() : TimeSpan.Zero,
            MedianTime = results.Count > 0 ? results.OrderBy(r => r).Skip(results.Count / 2).First() : TimeSpan.Zero,
            MaxAcceptableTime = maxAcceptableTime,
            Errors = errors
        };

        Metrics.Add(new PerformanceMetric
        {
            TestName = testName,
            Value = result.AverageTime.TotalMilliseconds,
            Unit = "ms",
            Timestamp = DateTime.UtcNow
        });

        return result;
    }

    /// <summary>
    /// Setup mock responses for Python worker service
    /// </summary>
    protected void SetupMockResponse<TResponse>(TResponse response, TimeSpan? delay = null)
    {
        var setup = MockPythonWorkerService.Setup(x => x.ExecuteAsync<It.IsAnyType, TResponse>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()));

        if (delay.HasValue)
        {
            setup.Returns(async () =>
            {
                await Task.Delay(delay.Value);
                return response;
            });
        }
        else
        {
            setup.ReturnsAsync(response);
        }
    }

    /// <summary>
    /// Assert performance result meets expectations
    /// </summary>
    protected void AssertPerformance(PerformanceResult result, string description = "")
    {
        result.Should().NotBeNull();
        result.SuccessfulIterations.Should().BeGreaterThan(0, "at least some iterations should succeed");
        
        if (result.MaxAcceptableTime.HasValue)
        {
            result.AverageTime.Should().BeLessThan(result.MaxAcceptableTime.Value, 
                $"average time should be less than {result.MaxAcceptableTime.Value.TotalMilliseconds}ms {description}");
        }

        // Log performance metrics
        Console.WriteLine($"Performance Test: {result.TestName}");
        Console.WriteLine($"  Iterations: {result.SuccessfulIterations}/{result.Iterations}");
        Console.WriteLine($"  Average: {result.AverageTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"  Min: {result.MinTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"  Max: {result.MaxTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"  Median: {result.MedianTime.TotalMilliseconds:F2}ms");
        
        if (result.FailedIterations > 0)
        {
            Console.WriteLine($"  Failures: {result.FailedIterations}");
        }
    }

    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Performance test result
/// </summary>
public class PerformanceResult
{
    public string TestName { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public int SuccessfulIterations { get; set; }
    public int FailedIterations { get; set; }
    public TimeSpan AverageTime { get; set; }
    public TimeSpan MinTime { get; set; }
    public TimeSpan MaxTime { get; set; }
    public TimeSpan MedianTime { get; set; }
    public TimeSpan? MaxAcceptableTime { get; set; }
    public List<Exception> Errors { get; set; } = new();
}

/// <summary>
/// Performance metric for tracking
/// </summary>
public class PerformanceMetric
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
