using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using DeviceOperations;
using DeviceOperations.Services.Device;
using DeviceOperations.Services.Memory;
using DeviceOperations.Services.Model;
using DeviceOperations.Services.Inference;
using DeviceOperations.Services.Postprocessing;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using System.Text.Json;
using System.Net.Http.Json;

namespace DeviceOperations.Tests.Integration;

/// <summary>
/// End-to-end workflow integration tests
/// Tests complete inference pipeline: Device → Memory → Model → Inference → Postprocessing
/// </summary>
public class EndToEndWorkflowTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly IServiceDevice _deviceService;
    private readonly IServiceMemory _memoryService;
    private readonly IServiceModel _modelService;
    private readonly IServiceInference _inferenceService;
    private readonly IServicePostprocessing _postprocessingService;
    private bool _disposed = false;

    public EndToEndWorkflowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Configure test-specific services if needed
                services.AddLogging(logging => logging.AddConsole());
            });
        });

        _client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        
        _deviceService = _scope.ServiceProvider.GetRequiredService<IServiceDevice>();
        _memoryService = _scope.ServiceProvider.GetRequiredService<IServiceMemory>();
        _modelService = _scope.ServiceProvider.GetRequiredService<IServiceModel>();
        _inferenceService = _scope.ServiceProvider.GetRequiredService<IServiceInference>();
        _postprocessingService = _scope.ServiceProvider.GetRequiredService<IServicePostprocessing>();
    }

    #region Complete Inference Pipeline Tests

    [Fact]
    public async Task CompleteInferencePipeline_ShouldExecuteSuccessfully_WhenAllComponentsAvailable()
    {
        // Phase 1: Device Discovery and Selection
        var deviceListResponse = await _deviceService.GetDeviceListAsync();
        deviceListResponse.Success.Should().BeTrue();
        deviceListResponse.Data.Should().NotBeNull();
        deviceListResponse.Data.Devices.Should().NotBeEmpty();

        var selectedDevice = deviceListResponse.Data.Devices.First(d => d.Type == "GPU" && d.IsAvailable);
        var deviceId = selectedDevice.Id.ToString();

        // Phase 2: Memory Allocation
        var memoryAllocateRequest = new PostMemoryAllocateRequest
        {
            Size = 8589934592, // 8GB
            DeviceId = deviceId,
            Purpose = "Model Loading",
            Priority = "High"
        };

        var memoryResponse = await _memoryService.PostMemoryAllocateAsync(memoryAllocateRequest);
        memoryResponse.Success.Should().BeTrue();
        memoryResponse.Data.Should().NotBeNull();
        var allocationId = memoryResponse.Data.AllocationId;

        try
        {
            // Phase 3: Model Discovery and Loading
            var modelsResponse = await _modelService.GetModelsAsync();
            modelsResponse.Success.Should().BeTrue();
            modelsResponse.Data.Should().NotBeNull();
            modelsResponse.Data.Models.Should().NotBeEmpty();

            var selectedModel = modelsResponse.Data.Models.First(m => m.Type == "TextToImage" && m.Status == "Available");
            var modelId = selectedModel.Id.ToString();

            var modelLoadRequest = new PostModelLoadRequest
            {
                DeviceId = deviceId,
                Precision = "FP16",
                MaxMemoryUsage = 6442450944, // 6GB
                OptimizationLevel = "Balanced"
            };

            var modelLoadResponse = await _modelService.PostModelLoadAsync(modelId, modelLoadRequest);
            modelLoadResponse.Success.Should().BeTrue();
            modelLoadResponse.Data.Should().NotBeNull();
            modelLoadResponse.Data.Status.Should().Be("Loaded");

            try
            {
                // Phase 4: Inference Execution
                var inferenceRequest = new PostInferenceExecuteRequest
                {
                    ModelId = Guid.Parse(modelId),
                    InferenceType = "TextToImage",
                    Parameters = new Dictionary<string, object>
                    {
                        { "prompt", "A beautiful landscape with mountains and lakes" },
                        { "negative_prompt", "blurry, low quality" },
                        { "steps", 20 },
                        { "guidance_scale", 7.5 },
                        { "width", 1024 },
                        { "height", 1024 },
                        { "seed", 42 }
                    }
                };

                var inferenceResponse = await _inferenceService.PostInferenceExecuteAsync(inferenceRequest, deviceId);
                inferenceResponse.Success.Should().BeTrue();
                inferenceResponse.Data.Should().NotBeNull();
                inferenceResponse.Data.Status.Should().Be("Completed");
                inferenceResponse.Data.Results.Should().ContainKey("GeneratedImages");

                // Phase 5: Postprocessing
                var generatedImages = inferenceResponse.Data.Results["GeneratedImages"] as string[];
                generatedImages.Should().NotBeNull();
                generatedImages.Should().NotBeEmpty();

                var postprocessingRequest = new PostPostprocessingUpscaleRequest
                {
                    InputImagePath = generatedImages[0],
                    UpscaleModel = "RealESRGAN",
                    ScaleFactor = 2.0f,
                    OutputFormat = "PNG"
                };

                var postprocessingResponse = await _postprocessingService.PostPostprocessingUpscaleAsync(postprocessingRequest);
                postprocessingResponse.Success.Should().BeTrue();
                postprocessingResponse.Data.Should().NotBeNull();
                postprocessingResponse.Data.Status.Should().Be("Completed");

                // Verify complete pipeline results
                postprocessingResponse.Data.OutputImagePath.Should().NotBeNullOrEmpty();
                postprocessingResponse.Data.ScaleFactor.Should().Be(2.0f);
            }
            finally
            {
                // Cleanup: Unload Model
                var modelUnloadRequest = new PostModelUnloadRequest
                {
                    DeviceId = deviceId,
                    Force = false,
                    PreserveCacheData = false
                };

                await _modelService.PostModelUnloadAsync(modelId, modelUnloadRequest);
            }
        }
        finally
        {
            // Cleanup: Deallocate Memory
            var memoryDeallocateRequest = new DeleteMemoryDeallocateRequest
            {
                AllocationId = allocationId,
                DeviceId = deviceId,
                Force = false
            };

            await _memoryService.DeleteMemoryDeallocateAsync(memoryDeallocateRequest);
        }
    }

    [Fact]
    public async Task BatchInferencePipeline_ShouldProcessMultipleImages_WhenBatchRequestSubmitted()
    {
        // Phase 1: Setup - Device and Memory
        var deviceListResponse = await _deviceService.GetDeviceListAsync();
        var selectedDevice = deviceListResponse.Data.Devices.First(d => d.Type == "GPU" && d.IsAvailable);
        var deviceId = selectedDevice.Id.ToString();

        var memoryAllocateRequest = new PostMemoryAllocateRequest
        {
            Size = 12884901888, // 12GB for batch processing
            DeviceId = deviceId,
            Purpose = "Batch Inference",
            Priority = "High"
        };

        var memoryResponse = await _memoryService.PostMemoryAllocateAsync(memoryAllocateRequest);
        var allocationId = memoryResponse.Data.AllocationId;

        try
        {
            // Phase 2: Model Loading
            var modelsResponse = await _modelService.GetModelsAsync();
            var selectedModel = modelsResponse.Data.Models.First(m => m.Type == "TextToImage");
            var modelId = selectedModel.Id.ToString();

            var modelLoadRequest = new PostModelLoadRequest
            {
                DeviceId = deviceId,
                Precision = "FP16",
                MaxMemoryUsage = 10737418240, // 10GB
                OptimizationLevel = "Performance"
            };

            await _modelService.PostModelLoadAsync(modelId, modelLoadRequest);

            try
            {
                // Phase 3: Batch Inference
                var batchPrompts = new[]
                {
                    "A serene mountain landscape at sunset",
                    "A bustling city street at night",
                    "A peaceful forest with flowing river",
                    "A futuristic space station orbiting Earth"
                };

                var batchTasks = batchPrompts.Select(async (prompt, index) =>
                {
                    var inferenceRequest = new PostInferenceExecuteRequest
                    {
                        ModelId = Guid.Parse(modelId),
                        InferenceType = "TextToImage",
                        Parameters = new Dictionary<string, object>
                        {
                            { "prompt", prompt },
                            { "steps", 15 }, // Reduced steps for faster batch processing
                            { "guidance_scale", 7.0 },
                            { "width", 512 },
                            { "height", 512 },
                            { "seed", 42 + index }
                        }
                    };

                    return await _inferenceService.PostInferenceExecuteAsync(inferenceRequest, deviceId);
                });

                var batchResults = await Task.WhenAll(batchTasks);

                // Phase 4: Verify Batch Results
                batchResults.Should().AllSatisfy(result =>
                {
                    result.Success.Should().BeTrue();
                    result.Data.Status.Should().Be("Completed");
                    result.Data.Results.Should().ContainKey("GeneratedImages");
                });

                // Phase 5: Batch Postprocessing
                var postprocessingTasks = batchResults.Select(async result =>
                {
                    var generatedImages = result.Data.Results["GeneratedImages"] as string[];
                    var postprocessingRequest = new PostPostprocessingSafetyCheckRequest
                    {
                        ImagePath = generatedImages[0],
                        CheckTypes = new List<string> { "NSFW", "Violence", "Inappropriate" }
                    };

                    return await _postprocessingService.PostPostprocessingSafetyCheckAsync(postprocessingRequest);
                });

                var postprocessingResults = await Task.WhenAll(postprocessingTasks);

                // Verify all safety checks passed
                postprocessingResults.Should().AllSatisfy(result =>
                {
                    result.Success.Should().BeTrue();
                    result.Data.Status.Should().Be("Completed");
                    result.Data.IsSafe.Should().BeTrue();
                });
            }
            finally
            {
                // Cleanup: Unload Model
                var modelUnloadRequest = new PostModelUnloadRequest
                {
                    DeviceId = deviceId,
                    Force = false
                };

                await _modelService.PostModelUnloadAsync(modelId, modelUnloadRequest);
            }
        }
        finally
        {
            // Cleanup: Deallocate Memory
            var memoryDeallocateRequest = new DeleteMemoryDeallocateRequest
            {
                AllocationId = allocationId,
                DeviceId = deviceId,
                Force = false
            };

            await _memoryService.DeleteMemoryDeallocateAsync(memoryDeallocateRequest);
        }
    }

    #endregion

    #region Resource Management and Cleanup Tests

    [Fact]
    public async Task ResourceCleanupPipeline_ShouldProperlyCleanup_WhenErrorOccurs()
    {
        // Phase 1: Setup resources
        var deviceListResponse = await _deviceService.GetDeviceListAsync();
        var selectedDevice = deviceListResponse.Data.Devices.First(d => d.Type == "GPU" && d.IsAvailable);
        var deviceId = selectedDevice.Id.ToString();

        var memoryAllocateRequest = new PostMemoryAllocateRequest
        {
            Size = 4294967296, // 4GB
            DeviceId = deviceId,
            Purpose = "Error Recovery Test"
        };

        var memoryResponse = await _memoryService.PostMemoryAllocateAsync(memoryAllocateRequest);
        var allocationId = memoryResponse.Data.AllocationId;

        var modelsResponse = await _modelService.GetModelsAsync();
        var selectedModel = modelsResponse.Data.Models.First();
        var modelId = selectedModel.Id.ToString();

        var modelLoadRequest = new PostModelLoadRequest
        {
            DeviceId = deviceId,
            Precision = "FP16"
        };

        await _modelService.PostModelLoadAsync(modelId, modelLoadRequest);

        // Phase 2: Simulate error during inference
        Exception caughtException = null;
        try
        {
            var invalidInferenceRequest = new PostInferenceExecuteRequest
            {
                ModelId = Guid.Parse(modelId),
                InferenceType = "InvalidType", // This should cause an error
                Parameters = new Dictionary<string, object>
                {
                    { "invalid_param", "invalid_value" }
                }
            };

            await _inferenceService.PostInferenceExecuteAsync(invalidInferenceRequest, deviceId);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Verify error was caught
        caughtException.Should().NotBeNull();

        // Phase 3: Cleanup should still work despite error
        var modelUnloadRequest = new PostModelUnloadRequest
        {
            DeviceId = deviceId,
            Force = true // Force unload due to error condition
        };

        var unloadResult = await _modelService.PostModelUnloadAsync(modelId, modelUnloadRequest);
        unloadResult.Success.Should().BeTrue();

        var memoryDeallocateRequest = new DeleteMemoryDeallocateRequest
        {
            AllocationId = allocationId,
            DeviceId = deviceId,
            Force = true // Force deallocation due to error condition
        };

        var deallocateResult = await _memoryService.DeleteMemoryDeallocateAsync(memoryDeallocateRequest);
        deallocateResult.Success.Should().BeTrue();

        // Phase 4: Verify system is clean
        var finalMemoryStatus = await _memoryService.GetMemoryStatusDeviceAsync(deviceId);
        finalMemoryStatus.Success.Should().BeTrue();
        
        // Memory should be available again after cleanup
        var deviceMemory = finalMemoryStatus.Data;
        deviceMemory.AvailableMemory.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SessionManagement_ShouldTrackAndManageSessions_WhenMultipleInferencesRun()
    {
        // Phase 1: Start multiple inference sessions
        var deviceListResponse = await _deviceService.GetDeviceListAsync();
        var selectedDevice = deviceListResponse.Data.Devices.First(d => d.Type == "GPU" && d.IsAvailable);
        var deviceId = selectedDevice.Id.ToString();

        // Load a model first
        var modelsResponse = await _modelService.GetModelsAsync();
        var selectedModel = modelsResponse.Data.Models.First();
        var modelId = selectedModel.Id.ToString();

        var modelLoadRequest = new PostModelLoadRequest
        {
            DeviceId = deviceId,
            Precision = "FP16"
        };

        await _modelService.PostModelLoadAsync(modelId, modelLoadRequest);

        try
        {
            // Start multiple inference sessions (simulated as sequential for testing)
            var sessionIds = new List<Guid>();

            for (int i = 0; i < 3; i++)
            {
                var inferenceRequest = new PostInferenceExecuteRequest
                {
                    ModelId = Guid.Parse(modelId),
                    InferenceType = "TextToImage",
                    Parameters = new Dictionary<string, object>
                    {
                        { "prompt", $"Test image {i + 1}" },
                        { "steps", 10 }, // Quick inference for testing
                        { "width", 512 },
                        { "height", 512 }
                    }
                };

                var result = await _inferenceService.PostInferenceExecuteAsync(inferenceRequest, deviceId);
                result.Success.Should().BeTrue();
                sessionIds.Add(result.Data.InferenceId);
            }

            // Phase 2: Check session management
            var sessionsResponse = await _inferenceService.GetInferenceSessionsAsync();
            sessionsResponse.Success.Should().BeTrue();
            sessionsResponse.Data.Should().NotBeNull();

            // Verify sessions are tracked
            sessionIds.Should().AllSatisfy(sessionId =>
            {
                var session = sessionsResponse.Data.Sessions.FirstOrDefault(s => s.Id == sessionId);
                session.Should().NotBeNull();
                session.Status.Should().BeOneOf("Running", "Completed", "Queued");
            });

            // Phase 3: Individual session details
            foreach (var sessionId in sessionIds)
            {
                var sessionDetailResponse = await _inferenceService.GetInferenceSessionAsync(sessionId);
                sessionDetailResponse.Success.Should().BeTrue();
                sessionDetailResponse.Data.Should().NotBeNull();
                sessionDetailResponse.Data.Session.Id.Should().Be(sessionId);
            }
        }
        finally
        {
            // Cleanup: Unload model
            var modelUnloadRequest = new PostModelUnloadRequest
            {
                DeviceId = deviceId,
                Force = false
            };

            await _modelService.PostModelUnloadAsync(modelId, modelUnloadRequest);
        }
    }

    #endregion

    #region Performance and Load Tests

    [Fact]
    public async Task HighLoadPipeline_ShouldHandleMultipleConcurrentRequests_WhenSystemUnderLoad()
    {
        // Phase 1: Setup
        var deviceListResponse = await _deviceService.GetDeviceListAsync();
        var availableDevices = deviceListResponse.Data.Devices.Where(d => d.IsAvailable).ToList();
        availableDevices.Should().NotBeEmpty();

        // Phase 2: Concurrent device status checks
        var deviceStatusTasks = availableDevices.Take(5).Select(async device =>
        {
            return await _deviceService.GetDeviceStatusAsync(device.Id.ToString());
        });

        var deviceStatusResults = await Task.WhenAll(deviceStatusTasks);
        deviceStatusResults.Should().AllSatisfy(result => result.Success.Should().BeTrue());

        // Phase 3: Concurrent memory status checks
        var memoryStatusTasks = availableDevices.Take(3).Select(async device =>
        {
            return await _memoryService.GetMemoryStatusDeviceAsync(device.Id.ToString());
        });

        var memoryStatusResults = await Task.WhenAll(memoryStatusTasks);
        memoryStatusResults.Should().AllSatisfy(result => result.Success.Should().BeTrue());

        // Phase 4: Concurrent model listing
        var modelTasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            return await _modelService.GetModelsAsync();
        });

        var modelResults = await Task.WhenAll(modelTasks);
        modelResults.Should().AllSatisfy(result => result.Success.Should().BeTrue());

        // Phase 5: Verify system stability
        var finalDeviceStatus = await _deviceService.GetDeviceListAsync();
        finalDeviceStatus.Success.Should().BeTrue();
        finalDeviceStatus.Data.Devices.Should().HaveCount(deviceListResponse.Data.Devices.Count);
    }

    #endregion

    #region HTTP API Integration Tests

    [Fact]
    public async Task HttpApiPipeline_ShouldWorkThroughRestApi_WhenCallingEndpoints()
    {
        // Phase 1: Device discovery via HTTP API
        var devicesResponse = await _client.GetAsync("/api/device");
        devicesResponse.IsSuccessStatusCode.Should().BeTrue();

        var devicesContent = await devicesResponse.Content.ReadAsStringAsync();
        var devicesJson = JsonDocument.Parse(devicesContent);
        devicesJson.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        // Phase 2: Memory status via HTTP API
        var memoryResponse = await _client.GetAsync("/api/memory");
        memoryResponse.IsSuccessStatusCode.Should().BeTrue();

        var memoryContent = await memoryResponse.Content.ReadAsStringAsync();
        var memoryJson = JsonDocument.Parse(memoryContent);
        memoryJson.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        // Phase 3: Models listing via HTTP API
        var modelsResponse = await _client.GetAsync("/api/model");
        modelsResponse.IsSuccessStatusCode.Should().BeTrue();

        var modelsContent = await modelsResponse.Content.ReadAsStringAsync();
        var modelsJson = JsonDocument.Parse(modelsContent);
        modelsJson.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        // Phase 4: Inference capabilities via HTTP API
        var inferenceCapabilitiesResponse = await _client.GetAsync("/api/inference/capabilities");
        inferenceCapabilitiesResponse.IsSuccessStatusCode.Should().BeTrue();

        var inferenceContent = await inferenceCapabilitiesResponse.Content.ReadAsStringAsync();
        var inferenceJson = JsonDocument.Parse(inferenceContent);
        inferenceJson.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    #endregion

    #region Resource Cleanup

    public void Dispose()
    {
        if (!_disposed)
        {
            _scope?.Dispose();
            _client?.Dispose();
            _disposed = true;
        }
    }

    #endregion
}
