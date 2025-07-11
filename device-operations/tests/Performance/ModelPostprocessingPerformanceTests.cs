using DeviceOperations.Services.Model;
using DeviceOperations.Services.Postprocessing;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Models.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;

namespace DeviceOperations.Tests.Performance;

/// <summary>
/// Performance tests for model management and postprocessing operations
/// </summary>
public class ModelPostprocessingPerformanceTests : PerformanceTestBase
{
    [Fact]
    public async Task Model_Loading_Performance_Sequential()
    {
        // Arrange
        var modelService = ServiceProvider.GetRequiredService<IServiceModel>();
        
        var mockLoadResponse = new PostModelLoadResponse
        {
            Success = true,
            ModelId = "test-model-v1.5",
            LoadSessionId = Guid.NewGuid(),
            LoadTime = TimeSpan.FromMilliseconds(200),
            MemoryUsed = 1024 * 1024 * 1024,
            DeviceId = Guid.NewGuid(),
            LoadedAt = DateTime.UtcNow,
            LoadMetrics = new Dictionary<string, object> { { "Status", "loaded" } }
        };

        var mockUnloadResponse = new DeleteModelUnloadResponse
        {
            Success = true,
            Message = "Model unloaded successfully",
            UnloadTime = TimeSpan.FromMilliseconds(50),
            MemoryFreed = 1024 * 1024 * 1024
        };

        SetupMockResponse(mockLoadResponse, TimeSpan.FromMilliseconds(200));

        // Act & Assert - Test sequential model loading
        var modelIds = new[] { "sd-v1.5", "sd-v2.1", "sd-xl-base", "sd-xl-refiner" };
        
        var result = await ExecutePerformanceTest<object>(
            "Sequential_Model_Loading",
            async () =>
            {
                foreach (var modelId in modelIds)
                {
                    var loadRequest = new PostModelLoadRequest
                    {
                        ModelPath = $"/models/{modelId}.safetensors",
                        ModelType = ModelType.SD15,
                        DeviceId = Guid.NewGuid(),
                        LoadingStrategy = "standard"
                    };

                    var loadResult = await modelService.PostModelLoadAsync(modelId, loadRequest);
                    loadResult.Should().NotBeNull();

                    // Simulate brief usage
                    await Task.Delay(10);

                    // Unload model (simulate with another service call)
                    await modelService.GetModelsAsync();
                }
                return new object();
            },
            iterations: 10,
            maxAcceptableTime: TimeSpan.FromSeconds(2)
        );

        AssertPerformance(result, "for sequential model loading/unloading");
    }

    [Fact]
    public async Task Model_Discovery_Performance_LargeLibrary()
    {
        // Arrange
        var modelService = ServiceProvider.GetRequiredService<IServiceModel>();
        
        var mockModelsResponse = new ListModelsResponse
        {
            Models = Enumerable.Range(0, 150).Select(i => new ModelInfo
            {
                Id = $"Model_{i:D3}",
                Name = $"Model_{i:D3}",
                FilePath = $"/models/model_{i:D3}.safetensors",
                FileSize = (long)(1.5 + (i % 8)) * 1024 * 1024 * 1024,
                LastUpdated = DateTime.UtcNow.AddDays(-(i % 30))
            }).ToList()
        };

        SetupMockResponse(mockModelsResponse, TimeSpan.FromMilliseconds(100));

        // Act & Assert - Test model discovery with filtering
        var result = await ExecutePerformanceTest<object>(
            "Model_Discovery_Large_Library",
            async () =>
            {
                // Test various discovery scenarios
                await modelService.GetModelsAsync();
                await modelService.GetModelsAsync();
                await modelService.GetModelsAsync();
                await modelService.GetModelsAsync();
                return new object();
            },
            iterations: 50,
            maxAcceptableTime: TimeSpan.FromMilliseconds(500)
        );

        AssertPerformance(result, "for model discovery in large library");
    }

    [Fact]
    public async Task Postprocessing_Pipeline_Performance()
    {
        // Arrange
        var postprocessingService = ServiceProvider.GetRequiredService<IServicePostprocessing>();
        
        var mockUpscaleResponse = new PostPostprocessingUpscaleResponse
        {
            OperationId = Guid.NewGuid(),
            InputImagePath = "/tmp/generated_image.png",
            OutputImagePath = "/tmp/upscaled_output.png",
            ScaleFactor = 2,
            ModelUsed = "Real-ESRGAN-x2plus",
            ProcessingTime = TimeSpan.FromSeconds(8.5),
            CompletedAt = DateTime.UtcNow,
            OriginalResolution = new DeviceOperations.Models.Common.ImageResolution { Width = 512, Height = 512 },
            NewResolution = new DeviceOperations.Models.Common.ImageResolution { Width = 1024, Height = 1024 },
            QualityMetrics = new Dictionary<string, float> { { "PSNR", 28.5f } }
        };

        var mockEnhanceResponse = new PostPostprocessingEnhanceResponse
        {
            OperationId = Guid.NewGuid(),
            InputImagePath = "/tmp/upscaled.png",
            OutputImagePath = "/tmp/enhanced_output.png",
            EnhancementType = "noise_reduction",
            Strength = 0.8f,
            ProcessingTime = TimeSpan.FromSeconds(3.2),
            CompletedAt = DateTime.UtcNow,
            EnhancementsApplied = new List<string> { "noise_reduction" },
            QualityMetrics = new Dictionary<string, float> { { "SSIM", 0.92f } },
            BeforeAfterComparison = new { QualityImprovement = 15.2 }
        };

        SetupMockResponse(mockUpscaleResponse, TimeSpan.FromMilliseconds(300));

        // Act & Assert - Test postprocessing pipeline
        var result = await ExecutePerformanceTest<object>(
            "Postprocessing_Pipeline",
            async () =>
            {
                // Simulate a typical postprocessing workflow
                var upscaleRequest = new PostPostprocessingUpscaleRequest
                {
                    InputImagePath = "/tmp/generated_image.png",
                    ScaleFactor = 2
                };

                var upscaleResult = await postprocessingService.PostUpscaleAsync(upscaleRequest);
                upscaleResult.Should().NotBeNull();

                // Chain enhancement after upscaling
                var enhanceRequest = new PostPostprocessingEnhanceRequest
                {
                    InputImagePath = "/tmp/upscaled.png",
                    EnhancementType = "noise_reduction"
                };

                await postprocessingService.PostEnhanceAsync(enhanceRequest);
                return new object();
            },
            iterations: 20,
            maxAcceptableTime: TimeSpan.FromMilliseconds(800)
        );

        AssertPerformance(result, "for postprocessing pipeline operations");
    }

    [Fact]
    public async Task Concurrent_Postprocessing_Performance()
    {
        // Arrange
        var postprocessingService = ServiceProvider.GetRequiredService<IServicePostprocessing>();
        
        var mockBatchResponse = new PostPostprocessingBatchResponse
        {
            BatchId = Guid.NewGuid(),
            Operation = "upscale",
            TotalImages = 10,
            ProcessedImages = 0,
            SuccessfulImages = 0,
            FailedImages = 0,
            TotalProcessingTime = TimeSpan.FromSeconds(0),
            AverageProcessingTime = TimeSpan.FromSeconds(0),
            CompletedAt = DateTime.UtcNow,
            Results = new List<object>(),
            BatchStatistics = new Dictionary<string, object> { { "Status", "processing" } },
            ConcurrencyInfo = new Dictionary<string, object> { { "MaxConcurrency", 4 } }
        };

        SetupMockResponse(mockBatchResponse, TimeSpan.FromMilliseconds(50));

        // Act & Assert - Test concurrent postprocessing
        var concurrentTasks = 8;
        var tasks = new List<Task>();

        for (int i = 0; i < concurrentTasks; i++)
        {
            int taskIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                var result = await ExecutePerformanceTest<object>(
                    $"Concurrent_Postprocessing_{taskIndex}",
                    async () =>
                    {
                        var batchRequest = new PostPostprocessingBatchRequest
                        {
                            Operation = "upscale",
                            InputImagePaths = new List<string> { "/tmp/image1.png", "/tmp/image2.png" }
                        };

                        await postprocessingService.PostPostprocessingBatchAsync(batchRequest);
                        return new object();
                    },
                    iterations: 10,
                    maxAcceptableTime: TimeSpan.FromMilliseconds(200)
                );

                result.SuccessfulIterations.Should().BeGreaterThan(8);
            }));
        }

        await Task.WhenAll(tasks);
        Console.WriteLine($"Successfully completed {concurrentTasks} concurrent postprocessing tasks");
    }

    [Fact]
    public async Task Model_Memory_Management_Performance()
    {
        // Arrange
        var modelService = ServiceProvider.GetRequiredService<IServiceModel>();
        
        var mockOptimizeResponse = new PostModelOptimizeResponse
        {
            Success = true,
            Message = "Model optimization completed"
        };

        var mockStatusResponse = new GetModelStatusResponse
        {
            Status = new Dictionary<string, object> 
            {
                { "ModelId", "test-model" },
                { "Status", "loaded" },
                { "MemoryUsage", "3.2GB" },
                { "LastUsed", DateTime.UtcNow.AddMinutes(-5) },
                { "UsageCount", 147 }
            },
            LoadedModels = new List<LoadedModelInfo>
            {
                new LoadedModelInfo
                {
                    ModelId = "test-model",
                    ModelName = "Test Model",
                    DeviceId = Guid.NewGuid(),
                    LoadedAt = DateTime.UtcNow,
                    MemoryUsed = 3L * 1024 * 1024 * 1024,
                    Status = "loaded"
                }
            },
            LoadingStatistics = new Dictionary<string, object>()
        };

        SetupMockResponse(mockOptimizeResponse, TimeSpan.FromMilliseconds(150));

        // Act & Assert - Test model memory optimization
        var result = await ExecutePerformanceTest<object>(
            "Model_Memory_Optimization",
            async () =>
            {
                // Check current status
                await modelService.GetModelStatusAsync("test-model");
                
                // Optimize model memory usage
                var optimizeRequest = new PostModelOptimizeRequest
                {
                    Target = DeviceOperations.Models.Requests.OptimizationTarget.MemoryUsage,
                    DeviceId = Guid.Parse("test-device"),
                    OptimizationTarget = DeviceOperations.Models.Requests.OptimizationTarget.MemoryUsage
                };

                await modelService.PostModelOptimizeAsync("test-model", optimizeRequest);
                
                // Verify optimization
                await modelService.GetModelStatusAsync("test-model");
                return new object();
            },
            iterations: 15,
            maxAcceptableTime: TimeSpan.FromMilliseconds(600)
        );

        AssertPerformance(result, "for model memory optimization");
    }

    [Fact]
    public async Task Large_File_Processing_Performance()
    {
        // Arrange
        var postprocessingService = ServiceProvider.GetRequiredService<IServicePostprocessing>();
        
        var mockLargeFileResponse = new PostPostprocessingUpscaleResponse
        {
            OperationId = Guid.NewGuid(),
            InputImagePath = "/tmp/large_image_8k.png",
            OutputImagePath = "/tmp/large_image_16k.png",
            ScaleFactor = 2,
            ModelUsed = "Real-ESRGAN-x2plus",
            ProcessingTime = TimeSpan.FromMinutes(15),
            CompletedAt = DateTime.UtcNow,
            OriginalResolution = new DeviceOperations.Models.Common.ImageResolution { Width = 7680, Height = 4320 },
            NewResolution = new DeviceOperations.Models.Common.ImageResolution { Width = 15360, Height = 8640 },
            QualityMetrics = new Dictionary<string, float> { { "PSNR", 30.2f } }
        };

        SetupMockResponse(mockLargeFileResponse, TimeSpan.FromSeconds(1));

        // Act & Assert - Test large file handling
        var result = await ExecutePerformanceTest<object>(
            "Large_File_Processing",
            async () =>
            {
                var upscaleRequest = new PostPostprocessingUpscaleRequest
                {
                    InputImagePath = "/tmp/large_image_4k.png",
                    ScaleFactor = 2
                };

                await postprocessingService.PostUpscaleAsync(upscaleRequest);
                return new object();
            },
            iterations: 5,
            maxAcceptableTime: TimeSpan.FromSeconds(3)
        );

        AssertPerformance(result, "for large file processing");
        
        // Verify all iterations completed
        result.SuccessfulIterations.Should().Be(5, "all large file processing operations should complete");
    }
}
