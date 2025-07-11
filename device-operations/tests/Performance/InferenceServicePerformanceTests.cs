using DeviceOperations.Services.Inference;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Models.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

namespace DeviceOperations.Tests.Performance;

/// <summary>
/// Performance tests for Inference Service operations
/// </summary>
public class InferenceServicePerformanceTests : PerformanceTestBase
{
    [Fact]
    public async Task GetSupportedTypesAsync_Performance_MeetsExpectations()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceInference>();
        var mockResponse = new GetSupportedTypesResponse
        {
            SupportedTypes = new List<string> { "txt2img", "img2img" },
            TotalTypes = 2,
            LastUpdated = DateTime.UtcNow
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(10));

        // Act & Assert
        var result = await ExecutePerformanceTest<GetSupportedTypesResponse>(
            "GetSupportedTypesAsync",
            async () => {
                var apiResponse = await service.GetSupportedTypesAsync();
                return apiResponse.Data ?? new GetSupportedTypesResponse();
            },
            iterations: 100,
            maxAcceptableTime: TimeSpan.FromMilliseconds(50)
        );

        AssertPerformance(result, "for retrieving supported types");
    }

    [Fact]
    public async Task PostInferenceExecuteAsync_Performance_HandlesLoad()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceInference>();
        var request = new PostInferenceExecuteRequest
        {
            Parameters = new Dictionary<string, object>
            {
                { "prompt", "A beautiful landscape with mountains and lakes" },
                { "negative_prompt", "blurry, low quality" },
                { "width", 512 },
                { "height", 512 },
                { "steps", 20 },
                { "guidance_scale", 7.5 },
                { "seed", 12345 },
                { "scheduler", "euler_a" }
            },
            ModelId = "test-model",
            InferenceType = InferenceType.TextToImage
        };

        var mockResponse = new PostInferenceExecuteResponse
        {
            InferenceId = Guid.NewGuid(),
            Success = true,
            Status = InferenceStatus.Pending,
            ExecutionTime = TimeSpan.FromMinutes(2)
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var result = await ExecutePerformanceTest<PostInferenceExecuteResponse>(
            "PostInferenceExecuteAsync",
            async () => {
                var apiResponse = await service.PostInferenceExecuteAsync(request);
                return apiResponse.Data ?? new PostInferenceExecuteResponse();
            },
            iterations: 50,
            maxAcceptableTime: TimeSpan.FromMilliseconds(200)
        );

        AssertPerformance(result, "for inference execution requests");
    }

    [Fact]
    public async Task GetInferenceSessionAsync_Performance_FastRetrieval()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceInference>();
        var sessionId = Guid.NewGuid().ToString();
        
        var mockResponse = new GetInferenceSessionResponse
        {
            Session = new SessionInfo
            {
                SessionId = Guid.Parse(sessionId),
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(5));

        // Act & Assert
        var result = await ExecutePerformanceTest<GetInferenceSessionResponse>(
            "GetInferenceSessionAsync",
            async () => {
                var apiResponse = await service.GetInferenceSessionAsync(sessionId);
                return apiResponse.Data ?? new GetInferenceSessionResponse();
            },
            iterations: 200,
            maxAcceptableTime: TimeSpan.FromMilliseconds(25)
        );

        AssertPerformance(result, "for session status retrieval");
    }

    [Fact]
    public async Task Concurrent_InferenceExecution_Performance()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceInference>();
        var mockResponse = new PostInferenceExecuteResponse
        {
            InferenceId = Guid.NewGuid(),
            Success = true,
            Status = InferenceStatus.Pending,
            ExecutionTime = TimeSpan.FromMinutes(1)
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(50));

        // Act
        var concurrentTasks = 10;
        var tasks = new List<Task>();
        var results = new List<PerformanceResult>();

        for (int i = 0; i < concurrentTasks; i++)
        {
            var taskIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                var request = new PostInferenceExecuteRequest
                {
                    Parameters = new Dictionary<string, object>
                    {
                        { "prompt", $"Test image {taskIndex}" },
                        { "width", 512 },
                        { "height", 512 },
                        { "steps", 20 }
                    },
                    ModelId = "test-model",
                    InferenceType = InferenceType.TextToImage
                };

                var result = await ExecutePerformanceTest<PostInferenceExecuteResponse>(
                    $"ConcurrentGeneration_{taskIndex}",
                    async () => {
                        var apiResponse = await service.PostInferenceExecuteAsync(request);
                        return apiResponse.Data ?? new PostInferenceExecuteResponse();
                    },
                    iterations: 10,
                    maxAcceptableTime: TimeSpan.FromMilliseconds(100)
                );

                results.Add(result);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(concurrentTasks);
        results.Should().AllSatisfy(r => 
        {
            r.SuccessfulIterations.Should().BeGreaterThan(0);
            if (r.MaxAcceptableTime.HasValue)
            {
                r.AverageTime.Should().BeLessThan(r.MaxAcceptableTime.Value);
            }
        });

        var totalSuccessful = results.Sum(r => r.SuccessfulIterations);
        var totalIterations = results.Sum(r => r.Iterations);
        var successRate = (double)totalSuccessful / totalIterations;

        successRate.Should().BeGreaterThan(0.95, "success rate should be > 95% under concurrent load");
    }

    [Fact]
    public async Task Memory_Usage_During_Load_Test()
    {
        // Arrange
        var service = ServiceProvider.GetRequiredService<IServiceInference>();
        var initialMemory = GC.GetTotalMemory(true);

        var mockResponse = new PostInferenceExecuteResponse
        {
            InferenceId = Guid.NewGuid(),
            Success = true,
            Status = InferenceStatus.Pending
        };

        SetupMockResponse(mockResponse);

        // Act - Simulate sustained load
        var iterations = 1000;
        for (int i = 0; i < iterations; i++)
        {
            var request = new PostInferenceExecuteRequest
            {
                Parameters = new Dictionary<string, object>
                {
                    { "prompt", $"Load test image {i}" },
                    { "width", 512 },
                    { "height", 512 }
                },
                ModelId = "test-model",
                InferenceType = InferenceType.TextToImage
            };

            await service.PostInferenceExecuteAsync(request);

            // Force garbage collection every 100 iterations
            if (i % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        // Assert memory usage
        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseKB = memoryIncrease / 1024.0;

        // Memory should not increase excessively (< 10MB for this test)
        memoryIncreaseKB.Should().BeLessThan(10240, 
            $"memory increase should be less than 10MB, actual: {memoryIncreaseKB:F2}KB");

        Console.WriteLine($"Memory usage - Initial: {initialMemory / 1024:F2}KB, Final: {finalMemory / 1024:F2}KB, Increase: {memoryIncreaseKB:F2}KB");
    }
}
