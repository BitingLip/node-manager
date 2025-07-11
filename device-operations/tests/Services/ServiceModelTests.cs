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
        _serviceModel = new ServiceModel(_mockLogger.Object, _mockPythonWorkerService.Object);
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
                    Id = Guid.NewGuid().ToString(),
                    Name = "FLUX.1 Dev",
                    Type = ModelType.Diffusion,
                    Version = "dev",
                    Status = ModelStatus.Available,
                    FileSize = 23625000000,
                    LastUpdated = DateTime.UtcNow.AddDays(-15)
                },
                new ModelInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Stable Diffusion XL",
                    Type = ModelType.Diffusion,
                    Version = "1.0",
                    Status = ModelStatus.Available,
                    FileSize = 6625000000,
                    LastUpdated = DateTime.UtcNow.AddDays(-5)
                }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.GetModelsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Models.Should().HaveCount(2);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetModelsAsync_ShouldReturnError_WhenPythonWorkerFails()
    {
        // Arrange
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Python worker communication failed"));

        // Act
        var result = await _serviceModel.GetModelsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("Failed to retrieve models");
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
                Id = Guid.NewGuid().ToString(),
                Name = "FLUX.1 Dev",
                Type = ModelType.Diffusion,
                Version = "dev",
                Status = ModelStatus.Available,
                FileSize = 23625000000,
                LastUpdated = DateTime.UtcNow.AddDays(-15)
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.GetModelAsync(modelId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Model.Should().NotBeNull();
        result.Data.Model!.Name.Should().Be("FLUX.1 Dev");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>("ml_worker", "get_model", 
            It.Is<object>(o => o.ToString()!.Contains(modelId)), It.IsAny<CancellationToken>()), Times.Once);
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
            ModelPath = "/models/test-model",
            ModelType = ModelType.Diffusion,
            DeviceId = Guid.NewGuid(),
            LoadingStrategy = "balanced"
        };

        var expectedResponse = new PostModelLoadResponse
        {
            Success = true,
            ModelId = modelId,
            LoadSessionId = Guid.NewGuid(),
            LoadTime = TimeSpan.FromSeconds(18.5),
            MemoryUsed = 19327352832, // ~18GB
            DeviceId = request.DeviceId,
            LoadedAt = DateTime.UtcNow,
            LoadMetrics = new Dictionary<string, object>
            {
                { "LoadStrategy", request.LoadingStrategy ?? "default" },
                { "ModelType", request.ModelType.ToString() }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelLoadAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Success.Should().BeTrue();
        result.Data.ModelId.Should().Be(modelId);
        result.Data.LoadMetrics.Should().ContainKey("ModelType");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>("ml_worker", "load_model",
            It.Is<object>(o => o.ToString()!.Contains(modelId)), It.IsAny<CancellationToken>()), Times.Once);
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
            DeviceId = Guid.NewGuid(),
            Force = false
        };

        var expectedResponse = new PostModelUnloadResponse
        {
            Success = true,
            Message = $"Model '{modelId}' successfully unloaded"
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelUnloadAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Success.Should().BeTrue();
        result.Data.Message.Should().Contain(modelId);

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>("ml_worker", "unload_model",
            It.Is<object>(o => o.ToString()!.Contains(modelId)), It.IsAny<CancellationToken>()), Times.Once);
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
            ValidationLevel = "full",
            DeviceId = Guid.NewGuid()
        };

        var expectedResponse = new PostModelValidateResponse
        {
            IsValid = true,
            ValidationErrors = new List<string>()
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelValidateAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IsValid.Should().BeTrue();
        result.Data.ValidationErrors.Should().BeEmpty();

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>("ml_worker", "validate_model",
            It.Is<object>(o => o.ToString()!.Contains(modelId)), It.IsAny<CancellationToken>()), Times.Once);
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
            Target = DeviceOperations.Models.Requests.OptimizationTarget.Performance,
            DeviceId = Guid.NewGuid(),
            OptimizationTarget = DeviceOperations.Models.Requests.OptimizationTarget.Performance
        };

        var expectedResponse = new PostModelOptimizeResponse
        {
            Success = true,
            Message = "Model optimization completed successfully"
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelOptimizeAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Success.Should().BeTrue();
        result.Data.Message.Should().Be("Model optimization completed successfully");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>("ml_worker", "optimize_model",
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostModelBenchmarkAsync_ShouldBenchmarkModel_WhenRequestValid()
    {
        // Arrange
        var modelId = "test-model-id";
        var request = new PostModelBenchmarkRequest
        {
            BenchmarkType = DeviceOperations.Models.Requests.BenchmarkType.Inference,
            DeviceId = Guid.NewGuid()
        };

        var expectedResponse = new PostModelBenchmarkResponse
        {
            BenchmarkResults = new Dictionary<string, object>
            {
                { "inference_time_ms", 125.5 },
                { "memory_usage_mb", 2048 },
                { "throughput_tokens_per_second", 45.2 }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelBenchmarkAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.BenchmarkResults.Should().ContainKey("inference_time_ms");
        result.Data.BenchmarkResults.Should().ContainKey("memory_usage_mb");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>("ml_worker", "benchmark_model",
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
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
            Tags = new List<string> { "text-to-image", "diffusion" }
        };

        var expectedResponse = new PostModelSearchResponse
        {
            Models = new List<ModelInfo>
            {
                new ModelInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "FLUX.1 Dev",
                    Type = ModelType.Diffusion,
                    Version = "dev",
                    Status = ModelStatus.Available,
                    FileSize = 23625000000,
                    LastUpdated = DateTime.UtcNow.AddDays(-15)
                }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PostModelSearchAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Models.Should().HaveCount(1);
        result.Data.Models.First().Name.Should().Be("FLUX.1 Dev");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>("ml_worker", "search_models",
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetModelMetadataAsync_ShouldReturnMetadata_WhenModelExists()
    {
        // Arrange
        var modelId = "test-model-id";
        var expectedResponse = new GetModelMetadataResponse
        {
            Metadata = new Dictionary<string, object>
            {
                { "Author", "Black Forest Labs" },
                { "License", "Apache 2.0" },
                { "TrainingDataset", "Custom Dataset" },
                { "Architecture", "Diffusion Transformer" }
            }
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.GetModelMetadataAsync(modelId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Metadata.Should().ContainKey("Author");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>("ml_worker", "get_model_metadata",
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
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
            }
        };

        var expectedResponse = new PutModelMetadataResponse
        {
            Success = true,
            Message = "Metadata updated successfully"
        };

        var pythonResponse = JsonSerializer.Serialize(new { success = true, data = expectedResponse });
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _serviceModel.PutModelMetadataAsync(modelId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Success.Should().BeTrue();
        result.Data.Message.Should().Be("Metadata updated successfully");

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>("ml_worker", "update_model_metadata",
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
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
            ModelPath = "/models/test-model",
            ModelType = ModelType.Diffusion,
            DeviceId = Guid.NewGuid(),
            LoadingStrategy = "balanced"
        };

        var exception = new Exception("Test exception");
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error loading model")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetModelsAsync_ShouldHandleInvalidResponse_Gracefully()
    {
        // Arrange
        // Return invalid JSON response
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
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
    public void ServiceModel_ShouldNotThrow_WhenInstantiated()
    {
        // Act & Assert
        var exception = Record.Exception(() => new ServiceModel(_mockLogger.Object, _mockPythonWorkerService.Object));
        exception.Should().BeNull();
    }

    #endregion
}
