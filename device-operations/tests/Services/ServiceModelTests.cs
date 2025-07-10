using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using DeviceOperations.Services.Model;
using DeviceOperations.Services.Python;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using System.Text.Json;

namespace DeviceOperations.Tests.Services;

/// <summary>
/// Unit tests for ServiceModel
/// Tests model management operations including loading, validation, optimization, and metadata management
/// </summary>
public class ServiceModelTests
{
    private readonly Mock<IPythonWorkerService> _mockPythonWorkerService;
    private readonly Mock<ILogger<ServiceModel>> _mockLogger;
    private readonly ServiceModel _serviceModel;

    public ServiceModelTests()
    {
        _mockPythonWorkerService = new Mock<IPythonWorkerService>();
        _mockLogger = new Mock<ILogger<ServiceModel>>();
        _serviceModel = new ServiceModel(_mockPythonWorkerService.Object, _mockLogger.Object);
    }

    #region Model Discovery Tests

    [Fact]
    public async Task GetModelsAsync_ShouldReturnModelList_WhenSuccessful()
    {
        // Arrange
        var expectedResponse = new ListModelsResponse
        {
            Models = new List<ModelInfo>
            {
                new ModelInfo
                {
                    Id = Guid.NewGuid(),
                    Name = "FLUX.1 Dev",
                    Type = "TextToImage",
                    Version = "dev",
                    Status = "Available",
                    IsLoaded = true,
                    SizeBytes = 23625000000,
                    CreatedAt = DateTime.UtcNow.AddDays(-15),
                    UpdatedAt = DateTime.UtcNow
                },
                new ModelInfo
                {
                    Id = Guid.NewGuid(),
                    Name = "Stable Diffusion XL",
                    Type = "TextToImage",
                    Version = "1.0",
                    Status = "Available",
                    IsLoaded = false,
                    SizeBytes = 6625000000,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5)
                }
            },
            TotalModels = 2,
            LoadedModels = 1,
            AvailableModels = 2,
            Categories = new List<string> { "TextToImage", "ImageToImage" }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.GetModelsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Models.Should().HaveCount(2);
        result.Data.TotalModels.Should().Be(2);
        result.Data.LoadedModels.Should().Be(1);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("list_models", It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task GetModelsAsync_ShouldReturnError_WhenPythonWorkerFails()
    {
        // Arrange
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new Exception("Python worker communication failed"));

        // Act
        var result = await _serviceModel.GetModelsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("Failed to retrieve models");
    }

    [Fact]
    public async Task GetModelAsync_ShouldReturnModel_WhenModelExists()
    {
        // Arrange
        var modelId = "test-model-id";
        var expectedResponse = new GetModelResponse
        {
            Model = new ModelInfo
            {
                Id = Guid.NewGuid(),
                Name = "FLUX.1 Dev",
                Type = "TextToImage",
                Version = "dev",
                Status = "Available",
                IsLoaded = true,
                SizeBytes = 23625000000,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                UpdatedAt = DateTime.UtcNow
            },
            LoadedDevices = new List<DeviceInfo>
            {
                new DeviceInfo
                {
                    Id = Guid.NewGuid(),
                    Name = "NVIDIA RTX 4090",
                    Type = "GPU",
                    Status = "Available",
                    IsAvailable = true
                }
            },
            MemoryUsage = new Dictionary<string, long>
            {
                { "SystemMemory", 2147483648 }, // 2GB
                { "VRAMUsage", 21474836480 } // 20GB
            },
            Performance = new Dictionary<string, float>
            {
                { "InferenceSpeed", 5.2f },
                { "LoadTime", 15.8f }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.GetModelAsync(modelId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Model.Should().NotBeNull();
        result.Data.Model.Name.Should().Be("FLUX.1 Dev");
        result.Data.LoadedDevices.Should().HaveCount(1);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("get_model", 
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(modelId))), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetModelAsync_ShouldThrowArgumentException_WhenInvalidModelId(string invalidModelId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _serviceModel.GetModelAsync(invalidModelId));
    }

    #endregion

    #region Model Loading Tests

    [Fact]
    public async Task PostModelLoadAsync_ShouldLoadModel_WhenRequestValid()
    {
        // Arrange
        var modelId = "test-model-id";
        var request = new PostModelLoadRequest
        {
            DeviceId = "test-device-id",
            Precision = "FP16",
            MaxMemoryUsage = 20971520000, // 19.5GB
            OptimizationLevel = "Balanced",
            PreloadComponents = true
        };

        var expectedResponse = new PostModelLoadResponse
        {
            ModelId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Status = "Loaded",
            LoadTime = TimeSpan.FromSeconds(18.5),
            MemoryAllocated = 19327352832, // ~18GB
            Precision = request.Precision,
            OptimizationLevel = request.OptimizationLevel,
            LoadedAt = DateTime.UtcNow,
            LoadedComponents = new List<string>
            {
                "Transformer",
                "VAE",
                "Text Encoder",
                "Scheduler"
            },
            PerformanceMetrics = new Dictionary<string, float>
            {
                { "LoadSpeed", 1.2f },
                { "MemoryEfficiency", 88.5f },
                { "OptimizationScore", 92.0f }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelLoadAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Loaded");
        result.Data.Precision.Should().Be("FP16");
        result.Data.LoadedComponents.Should().HaveCount(4);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("load_model",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(modelId) && 
                              JsonSerializer.Serialize(o).Contains("FP16"))), Times.Once);
    }

    [Fact]
    public async Task PostModelLoadAsync_ShouldThrowArgumentNullException_WhenRequestIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _serviceModel.PostModelLoadAsync("model-id", null!));
    }

    [Fact]
    public async Task PostModelUnloadAsync_ShouldUnloadModel_WhenRequestValid()
    {
        // Arrange
        var modelId = "test-model-id";
        var request = new PostModelUnloadRequest
        {
            DeviceId = "test-device-id",
            Force = false,
            PreserveCacheData = true,
            GracefulShutdown = true
        };

        var expectedResponse = new PostModelUnloadResponse
        {
            ModelId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Status = "Unloaded",
            UnloadTime = TimeSpan.FromSeconds(5.2),
            MemoryFreed = 19327352832, // ~18GB
            UnloadedAt = DateTime.UtcNow,
            UnloadedComponents = new List<string>
            {
                "Transformer",
                "VAE",
                "Text Encoder",
                "Scheduler"
            },
            CacheDataPreserved = request.PreserveCacheData,
            CleanupOperations = new List<string>
            {
                "Memory cleared",
                "GPU context released",
                "Cache optimization applied"
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelUnloadAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Unloaded");
        result.Data.CacheDataPreserved.Should().BeTrue();
        result.Data.CleanupOperations.Should().HaveCount(3);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("unload_model",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(modelId))), Times.Once);
    }

    #endregion

    #region Model Validation Tests

    [Fact]
    public async Task PostModelValidateAsync_ShouldValidateModel_WhenRequestValid()
    {
        // Arrange
        var modelId = "test-model-id";
        var request = new PostModelValidateRequest
        {
            ValidationType = "Full",
            CheckIntegrity = true,
            CheckCompatibility = true,
            ValidateWeights = true,
            TargetDevice = "GPU"
        };

        var expectedResponse = new PostModelValidateResponse
        {
            ModelId = Guid.NewGuid(),
            ValidationType = request.ValidationType,
            Status = "Valid",
            ValidationTime = TimeSpan.FromSeconds(8.3),
            ValidationResults = new Dictionary<string, bool>
            {
                { "IntegrityCheck", true },
                { "CompatibilityCheck", true },
                { "WeightValidation", true },
                { "ConfigurationValid", true }
            },
            Issues = new List<string>(),
            Warnings = new List<string>
            {
                "Model uses deprecated configuration format"
            },
            Recommendations = new List<string>
            {
                "Consider updating to latest model format for better performance"
            },
            ComputedHash = "sha256:1234567890abcdef",
            ExpectedHash = "sha256:1234567890abcdef",
            CompatibleDevices = new List<string> { "GPU", "CPU" }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelValidateAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Valid");
        result.Data.ValidationResults.Should().HaveCount(4);
        result.Data.ComputedHash.Should().Be(result.Data.ExpectedHash);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("validate_model",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(modelId) && 
                              JsonSerializer.Serialize(o).Contains("Full"))), Times.Once);
    }

    #endregion

    #region Model Optimization Tests

    [Fact]
    public async Task PostModelOptimizeAsync_ShouldOptimizeModel_WhenRequestValid()
    {
        // Arrange
        var modelId = "test-model-id";
        var request = new PostModelOptimizeRequest
        {
            OptimizationType = "Performance",
            TargetDevice = "GPU",
            PreservePrecision = true,
            AggressiveOptimization = false,
            OptimizationLevel = "Balanced"
        };

        var expectedResponse = new PostModelOptimizeResponse
        {
            ModelId = Guid.NewGuid(),
            OptimizationType = request.OptimizationType,
            Status = "Optimized",
            OptimizationTime = TimeSpan.FromMinutes(12.5),
            OptimizationsApplied = new List<string>
            {
                "Graph fusion",
                "Kernel optimization",
                "Memory layout optimization",
                "Quantization"
            },
            PerformanceImprovement = 18.5f,
            MemoryReduction = 12.3f,
            CompletedAt = DateTime.UtcNow,
            OptimizedModelSize = 20971520000, // ~19.5GB
            Metrics = new Dictionary<string, float>
            {
                { "InferenceSpeedImprovement", 18.5f },
                { "MemoryEfficiencyGain", 12.3f },
                { "ThermalReduction", 8.2f }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelOptimizeAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Optimized");
        result.Data.PerformanceImprovement.Should().Be(18.5f);
        result.Data.OptimizationsApplied.Should().HaveCount(4);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("optimize_model",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(modelId) && 
                              JsonSerializer.Serialize(o).Contains("Performance"))), Times.Once);
    }

    [Fact]
    public async Task PostModelBenchmarkAsync_ShouldBenchmarkModel_WhenRequestValid()
    {
        // Arrange
        var modelId = "test-model-id";
        var request = new PostModelBenchmarkRequest
        {
            BenchmarkType = "Performance",
            Duration = TimeSpan.FromMinutes(5),
            InputSizes = new List<string> { "512x512", "1024x1024" },
            BatchSizes = new List<int> { 1, 2, 4 },
            Iterations = 100
        };

        var expectedResponse = new PostModelBenchmarkResponse
        {
            ModelId = Guid.NewGuid(),
            BenchmarkType = request.BenchmarkType,
            Status = "Completed",
            ExecutionTime = TimeSpan.FromMinutes(5.2),
            Results = new Dictionary<string, object>
            {
                { "AverageInferenceTime", 4.2 },
                { "ThroughputQPS", 14.3 },
                { "MemoryPeakUsage", 18.5 },
                { "PowerEfficiency", 85.2 }
            },
            PerformanceMetrics = new Dictionary<string, float>
            {
                { "512x512_Batch1", 2.1f },
                { "512x512_Batch2", 3.8f },
                { "1024x1024_Batch1", 4.2f },
                { "1024x1024_Batch2", 7.5f }
            },
            CompletedAt = DateTime.UtcNow,
            Score = 8750.5f
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelBenchmarkAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Completed");
        result.Data.Score.Should().Be(8750.5f);
        result.Data.PerformanceMetrics.Should().HaveCount(4);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("benchmark_model",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(modelId) && 
                              JsonSerializer.Serialize(o).Contains("Performance"))), Times.Once);
    }

    #endregion

    #region Model Search and Metadata Tests

    [Fact]
    public async Task PostModelSearchAsync_ShouldSearchModels_WhenRequestValid()
    {
        // Arrange
        var request = new PostModelSearchRequest
        {
            Query = "FLUX",
            ModelType = "TextToImage",
            MaxResults = 10,
            IncludeMetadata = true
        };

        var expectedResponse = new PostModelSearchResponse
        {
            Query = request.Query,
            Results = new List<ModelInfo>
            {
                new ModelInfo
                {
                    Id = Guid.NewGuid(),
                    Name = "FLUX.1 Dev",
                    Type = "TextToImage",
                    Version = "dev",
                    Status = "Available",
                    IsLoaded = true,
                    SizeBytes = 23625000000
                }
            },
            TotalResults = 1,
            SearchTime = TimeSpan.FromMilliseconds(150),
            FilterCriteria = new Dictionary<string, string>
            {
                { "Type", "TextToImage" },
                { "Query", "FLUX" }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelSearchAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Results.Should().HaveCount(1);
        result.Data.TotalResults.Should().Be(1);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("search_models",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains("FLUX"))), Times.Once);
    }

    [Fact]
    public async Task GetModelMetadataAsync_ShouldReturnMetadata_WhenModelExists()
    {
        // Arrange
        var modelId = "test-model-id";
        var expectedResponse = new GetModelMetadataResponse
        {
            ModelId = Guid.NewGuid(),
            Metadata = new Dictionary<string, object>
            {
                { "Author", "Black Forest Labs" },
                { "License", "Apache 2.0" },
                { "TrainingDataset", "Custom Dataset" },
                { "Architecture", "Diffusion Transformer" }
            },
            Tags = new List<string> { "text-to-image", "diffusion", "high-quality" },
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-5),
            Version = "1.0",
            ChecksumMD5 = "abc123def456",
            ChecksumSHA256 = "sha256:1234567890abcdef"
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.GetModelMetadataAsync(modelId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Tags.Should().HaveCount(3);
        result.Data.Metadata.Should().ContainKey("Author");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("get_model_metadata",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(modelId))), Times.Once);
    }

    [Fact]
    public async Task PutModelMetadataAsync_ShouldUpdateMetadata_WhenRequestValid()
    {
        // Arrange
        var modelId = "test-model-id";
        var request = new PutModelMetadataRequest
        {
            Metadata = new Dictionary<string, object>
            {
                { "Author", "Updated Author" },
                { "Version", "2.0" }
            },
            Tags = new List<string> { "updated", "improved" },
            MergeWithExisting = true
        };

        var expectedResponse = new PutModelMetadataResponse
        {
            ModelId = Guid.NewGuid(),
            UpdatedMetadata = request.Metadata,
            UpdatedTags = request.Tags,
            Status = "Updated",
            UpdatedAt = DateTime.UtcNow,
            MetadataVersion = 2
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PutModelMetadataAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Status.Should().Be("Updated");
        result.Data.MetadataVersion.Should().Be(2);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync("update_model_metadata",
            It.Is<object>(o => JsonSerializer.Serialize(o).Contains(modelId))), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PostModelLoadAsync_ShouldLogError_WhenExceptionOccurs()
    {
        // Arrange
        var modelId = "test-model-id";
        var request = new PostModelLoadRequest
        {
            DeviceId = "test-device-id",
            Precision = "FP16"
        };

        var exception = new Exception("Test exception");
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _serviceModel.PostModelLoadAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error loading model")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetModelsAsync_ShouldHandleInvalidResponse_Gracefully()
    {
        // Arrange
        // Return invalid JSON response
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync("invalid json response");

        // Act
        var result = await _serviceModel.GetModelsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    #endregion

    #region Resource Cleanup Tests

    [Fact]
    public void Dispose_ShouldNotThrow_WhenCalled()
    {
        // Act & Assert
        var exception = Record.Exception(() => _serviceModel.Dispose());
        exception.Should().BeNull();
    }

    #endregion
}
