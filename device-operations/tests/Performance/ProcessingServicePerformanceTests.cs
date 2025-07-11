using DeviceOperations.Services.Processing;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DeviceOperations.Tests.Performance;

/// <summary>
/// Performance tests for Processing Service operations
/// </summary>
public class ProcessingServicePerformanceTests : PerformanceTestBase
{
    [Fact]
    public async Task GetProcessingWorkflowsAsync_Performance_FastRetrieval()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceProcessing>();
        var mockResponse = new
        {
            workflows = Enumerable.Range(1, 50).Select(i => new
            {
                id = $"workflow-{i}",
                name = $"Test Workflow {i}",
                type = i % 3 == 0 ? "txt2img" : i % 3 == 1 ? "img2img" : "inpainting",
                description = $"Description for workflow {i}",
                category = "test",
                complexity = i % 5 + 1,
                estimatedTime = TimeSpan.FromMinutes(i % 10 + 1)
            }).ToArray()
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(20));

        // Act & Assert
        var result = await ExecutePerformanceTest<object>(
            "GetProcessingWorkflowsAsync",
            async () => {
                await service.GetProcessingWorkflowsAsync();
                return new object();
            },
            iterations: 100,
            maxAcceptableTime: TimeSpan.FromMilliseconds(50)
        );

        AssertPerformance(result, "for workflow listing");
    }

    [Fact]
    public async Task PostWorkflowExecuteAsync_Performance_HandlesComplexWorkflows()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceProcessing>();
        
        // Create complex workflow request with many parameters
        var request = new PostWorkflowExecuteRequest
        {
            WorkflowId = "complex-workflow-1",
            Parameters = new Dictionary<string, object>
            {
                ["prompt"] = "A highly detailed, photorealistic landscape with complex lighting and atmospheric effects",
                ["negative_prompt"] = "blurry, low quality, artifacts, distorted",
                ["width"] = 1024,
                ["height"] = 1024,
                ["steps"] = 50,
                ["cfg_scale"] = 8.5f,
                ["sampler"] = "dpm++_2m_karras",
                ["seed"] = 123456789,
                ["batch_size"] = 4,
                ["batch_count"] = 2,
                ["denoise_strength"] = 0.8f,
                ["clip_skip"] = 2,
                ["face_restoration"] = true,
                ["tiling"] = false,
                ["hires_fix"] = true,
                ["hires_upscaler"] = "Latent",
                ["hires_steps"] = 20,
                ["hires_denoising_strength"] = 0.7f,
                ["controlnet_enabled"] = true,
                ["controlnet_model"] = "canny",
                ["controlnet_weight"] = 1.0f,
                ["lora_models"] = new[] { "detail_tweaker", "add_detail" },
                ["lora_weights"] = new[] { 0.8f, 0.6f }
            }
        };

        var mockResponse = new
        {
            success = true,
            execution_id = Guid.NewGuid(),
            workflow_id = request.WorkflowId,
            status = "running",
            estimated_completion = DateTime.UtcNow.AddMinutes(5),
            queue_position = 0,
            processing_nodes = new[] { "node-1", "node-2" }
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(150));

        // Act & Assert
        var result = await ExecutePerformanceTest<object>(
            "PostWorkflowExecuteAsync_Complex",
            async () => {
                await service.PostWorkflowExecuteAsync(request);
                return new object();
            },
            iterations: 30,
            maxAcceptableTime: TimeSpan.FromMilliseconds(300)
        );

        AssertPerformance(result, "for complex workflow execution");
    }

    [Fact]
    public async Task GetProcessingSessionsAsync_Performance_LargeDatasets()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceProcessing>();
        
        // Mock large session dataset
        var mockResponse = new
        {
            sessions = Enumerable.Range(1, 1000).Select(i => new
            {
                id = Guid.NewGuid().ToString(),
                workflow_id = $"workflow-{i % 10}",
                status = new[] { "running", "completed", "queued", "failed" }[i % 4],
                progress = (float)(i % 100) / 100f,
                created_at = DateTime.UtcNow.AddMinutes(-i),
                updated_at = DateTime.UtcNow.AddMinutes(-(i / 2)),
                priority = i % 5,
                resource_usage = new
                {
                    cpu_percent = (i * 7) % 100,
                    memory_mb = (i * 50) % 8192,
                    gpu_percent = (i * 11) % 100
                }
            }).ToArray(),
            total_count = 1000,
            active_count = 250,
            queue_depth = 50
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var result = await ExecutePerformanceTest<object>(
            "GetProcessingSessionsAsync_Large",
            async () => {
                await service.GetProcessingSessionsAsync();
                return new object();
            },
            iterations: 50,
            maxAcceptableTime: TimeSpan.FromMilliseconds(200)
        );

        AssertPerformance(result, "for large session datasets");
    }

    [Fact]
    public async Task Workflow_Execution_Throughput_Test()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceProcessing>();
        var mockResponse = new
        {
            success = true,
            execution_id = Guid.NewGuid(),
            workflow_id = "throughput-test",
            status = "queued"
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(25));

        // Act - Test throughput over time
        var testDuration = TimeSpan.FromSeconds(10);
        var stopwatch = Stopwatch.StartNew();
        var completedOperations = 0;
        var tasks = new List<Task>();

        while (stopwatch.Elapsed < testDuration)
        {
            tasks.Add(Task.Run(async () =>
            {
                var request = new PostWorkflowExecuteRequest
                {
                    WorkflowId = "throughput-test",
                    Parameters = new Dictionary<string, object>
                    {
                        ["prompt"] = "Throughput test",
                        ["steps"] = 20
                    }
                };

                try
                {
                    await service.PostWorkflowExecuteAsync(request);
                    Interlocked.Increment(ref completedOperations);
                }
                catch
                {
                    // Continue on error for throughput testing
                }
            }));

            // Limit concurrent tasks to prevent overwhelming
            if (tasks.Count >= 50)
            {
                await Task.WhenAny(tasks);
                tasks.RemoveAll(t => t.IsCompleted);
            }

            await Task.Delay(10); // Small delay between submissions
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert throughput
        var operationsPerSecond = completedOperations / stopwatch.Elapsed.TotalSeconds;
        operationsPerSecond.Should().BeGreaterThan(10, "should handle at least 10 operations per second");

        Console.WriteLine($"Throughput Test Results:");
        Console.WriteLine($"  Duration: {stopwatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"  Completed Operations: {completedOperations}");
        Console.WriteLine($"  Operations/Second: {operationsPerSecond:F2}");
    }

    [Fact]
    public async Task Session_Monitoring_Performance_UnderLoad()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceProcessing>();
        var sessionIds = Enumerable.Range(1, 100).Select(_ => Guid.NewGuid().ToString()).ToList();
        
        var mockResponse = new
        {
            id = "test-session",
            workflow_id = "monitoring-test",
            status = "running",
            progress = 0.5f,
            start_time = DateTime.UtcNow.AddMinutes(-5),
            estimated_completion = DateTime.UtcNow.AddMinutes(5),
            resource_usage = new
            {
                cpu_percent = 75.5f,
                memory_mb = 2048,
                gpu_percent = 80.0f
            },
            current_operation = "Processing batch 3/10",
            debug_info = new
            {
                processed_samples = 1500,
                total_samples = 3000,
                current_batch_size = 8,
                avg_time_per_sample = 2.5f
            }
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(15));

        // Act - Simulate monitoring multiple sessions rapidly
        var result = await ExecutePerformanceTest<object>(
            "Session_Monitoring_Load",
            async () =>
            {
                var monitoringTasks = sessionIds.Take(10).Select(id => 
                    service.GetProcessingSessionAsync(id));
                await Task.WhenAll(monitoringTasks);
                return new object();
            },
            iterations: 50,
            maxAcceptableTime: TimeSpan.FromMilliseconds(100)
        );

        AssertPerformance(result, "for concurrent session monitoring");
    }

    [Fact]
    public async Task Resource_Intensive_Workflow_Performance()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceProcessing>();
        
        // Simulate resource-intensive workflow with high memory usage
        var heavyRequest = new PostWorkflowExecuteRequest
        {
            WorkflowId = "resource-intensive",
            Parameters = new Dictionary<string, object>
            {
                ["batch_size"] = 16,
                ["width"] = 2048,
                ["height"] = 2048,
                ["steps"] = 100,
                ["high_res_fix"] = true,
                ["controlnet_stack"] = new[]
                {
                    new { model = "canny", weight = 1.0f },
                    new { model = "depth", weight = 0.8f },
                    new { model = "openpose", weight = 0.6f }
                },
                ["lora_stack"] = Enumerable.Range(1, 5).Select(i => new
                {
                    model = $"lora_model_{i}",
                    weight = 0.8f / i
                }).ToArray(),
                ["preprocessing_filters"] = new[]
                {
                    "denoise", "sharpen", "enhance_details", "color_correction"
                }
            }
        };

        var mockResponse = new
        {
            success = true,
            execution_id = Guid.NewGuid(),
            workflow_id = heavyRequest.WorkflowId,
            status = "queued",
            estimated_memory_usage = "12GB",
            estimated_processing_time = "15 minutes",
            resource_requirements = new
            {
                min_vram = 8192,
                recommended_vram = 16384,
                cpu_cores = 8,
                system_ram = 32768
            }
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(500));

        // Act & Assert
        var result = await ExecutePerformanceTest<object>(
            "Resource_Intensive_Workflow",
            async () => {
                await service.PostWorkflowExecuteAsync(heavyRequest);
                return new object();
            },
            iterations: 10,
            maxAcceptableTime: TimeSpan.FromSeconds(1)
        );

        AssertPerformance(result, "for resource-intensive workflows");
    }
}
