using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DeviceOperations.Controllers;
using DeviceOperations.Services.Model;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Tests.Controllers;

/// <summary>
/// Unit tests for ControllerModel
/// Tests model management API endpoints including loading, caching, VRAM operations, and discovery
/// </summary>
public class ControllerModelTests
{
    private readonly Mock<IServiceModel> _mockServiceModel;
    private readonly Mock<ILogger<ControllerModel>> _mockLogger;
    private readonly ControllerModel _controller;

    public ControllerModelTests()
    {
        _mockServiceModel = new Mock<IServiceModel>();
        _mockLogger = new Mock<ILogger<ControllerModel>>();
        _controller = new ControllerModel(_mockServiceModel.Object, _mockLogger.Object);
    }

    #region Model Status Tests

    [Fact]
    public async Task GetModelStatus_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new GetModelStatusResponse
        {
            Status = new Dictionary<string, object>
            {
                ["TotalModels"] = 5,
                ["LoadedCount"] = 2,
                ["CachedCount"] = 3,
                ["AvailableMemory"] = 16106127360L,
                ["UsedMemory"] = 2147483648L
            },
            LoadedModels = new List<LoadedModelInfo>
            {
                new LoadedModelInfo
                {
                    ModelId = "model1",
                    ModelName = "Test Model 1",
                    DeviceId = Guid.NewGuid(),
                    LoadedAt = DateTime.UtcNow,
                    MemoryUsed = 1073741824,
                    Status = "Loaded"
                }
            },
            LoadingStatistics = new Dictionary<string, object>
            {
                ["LastUpdated"] = DateTime.UtcNow,
                ["AverageLoadTime"] = TimeSpan.FromSeconds(30)
            }
        };

        var serviceResponse = ApiResponse<GetModelStatusResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceModel.Setup(x => x.GetModelStatusAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetModelStatus();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetModelStatusResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Status.Should().ContainKey("TotalModels");

        _mockServiceModel.Verify(x => x.GetModelStatusAsync(), Times.Once);
    }

    [Fact]
    public async Task GetModelStatus_WithDeviceId_ShouldReturnOk_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";
        var expectedResponse = new GetModelStatusResponse
        {
            Status = new Dictionary<string, object>
            {
                ["DeviceId"] = deviceId,
                ["TotalModels"] = 3,
                ["LoadedCount"] = 1,
                ["AvailableMemory"] = 8589934592L,
                ["UsedMemory"] = 1073741824L
            },
            LoadedModels = new List<LoadedModelInfo>
            {
                new LoadedModelInfo
                {
                    ModelId = "device-model",
                    ModelName = "Device Specific Model",
                    DeviceId = Guid.Parse(deviceId),
                    LoadedAt = DateTime.UtcNow,
                    MemoryUsed = 1073741824,
                    Status = "Loaded"
                }
            }
        };

        var serviceResponse = ApiResponse<GetModelStatusResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceModel.Setup(x => x.GetModelStatusAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetModelStatus(deviceId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceModel.Verify(x => x.GetModelStatusAsync(deviceId), Times.Once);
    }

    [Fact]
    public async Task GetModelStatus_ShouldReturnBadRequest_WhenDeviceIdIsEmpty()
    {
        // Act
        var result = await _controller.GetModelStatus("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Model Loading Tests

    [Fact]
    public async Task PostModelLoad_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new PostModelLoadRequest
        {
            ModelPath = "/models/test-model.safetensors",
            ModelType = ModelType.SDXL,
            DeviceId = Guid.NewGuid(),
            LoadingStrategy = "default"
        };

        var expectedResponse = new PostModelLoadResponse
        {
            Success = true,
            ModelId = "test-model",
            LoadSessionId = Guid.NewGuid(),
            LoadTime = TimeSpan.FromSeconds(10),
            MemoryUsed = 2147483648,
            DeviceId = request.DeviceId,
            LoadedAt = DateTime.UtcNow,
            LoadMetrics = new Dictionary<string, object>
            {
                ["strategy"] = "default",
                ["device_count"] = 2
            }
        };

        var serviceResponse = ApiResponse<PostModelLoadResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceModel.Setup(x => x.PostModelLoadAsync(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostModelLoad(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostModelLoadResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();

        _mockServiceModel.Verify(x => x.PostModelLoadAsync(request), Times.Once);
    }

    [Fact]
    public async Task PostModelLoad_WithDeviceId_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostModelLoadRequest
        {
            ModelPath = "/models/device-model.safetensors",
            ModelType = ModelType.SD15,
            DeviceId = Guid.NewGuid(),
            LoadingStrategy = "optimized"
        };

        var expectedResponse = new PostModelLoadResponse
        {
            Success = true,
            ModelId = "device-model",
            LoadSessionId = Guid.NewGuid(),
            LoadTime = TimeSpan.FromSeconds(5),
            MemoryUsed = 1073741824,
            DeviceId = Guid.Parse(deviceId),
            LoadedAt = DateTime.UtcNow,
            LoadMetrics = new Dictionary<string, object>
            {
                ["strategy"] = "device-specific",
                ["device_id"] = deviceId
            }
        };

        var serviceResponse = ApiResponse<PostModelLoadResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceModel.Setup(x => x.PostModelLoadAsync(request, deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostModelLoad(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceModel.Verify(x => x.PostModelLoadAsync(request, deviceId), Times.Once);
    }

    [Fact]
    public async Task PostModelLoad_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostModelLoad(null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostModelLoad_WithDeviceId_ShouldReturnBadRequest_WhenDeviceIdIsEmpty()
    {
        // Arrange
        var request = new PostModelLoadRequest
        {
            ModelPath = "/models/test.safetensors",
            ModelType = ModelType.SDXL,
            DeviceId = Guid.NewGuid()
        };

        // Act
        var result = await _controller.PostModelLoad("", request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Model Unloading Tests

    [Fact]
    public async Task DeleteModelUnload_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Act
        var result = await _controller.DeleteModelUnload();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<DeleteModelUnloadResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();
        apiResponse.Data.Message.Should().Be("All models unloaded successfully from all devices");
    }

    [Fact]
    public async Task DeleteModelUnload_WithDeviceId_ShouldReturnOk_WhenDeviceExists()
    {
        // Arrange
        var deviceId = "test-device-id";

        // Act
        var result = await _controller.DeleteModelUnload(deviceId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<DeleteModelUnloadResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteModelUnload_WithDeviceId_ShouldReturnBadRequest_WhenDeviceIdIsEmpty()
    {
        // Act
        var result = await _controller.DeleteModelUnload("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Model Cache Tests

    [Fact]
    public async Task GetModelCache_ShouldReturnOk_WhenCalled()
    {
        // Act
        var result = await _controller.GetModelCache();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetModelCacheResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.TotalCacheSize.Should().Be(1024000);
        apiResponse.Data.CachedModels.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetModelCacheComponent_ShouldReturnOk_WhenComponentExists()
    {
        // Arrange
        var componentId = "test-component-id";

        // Act
        var result = await _controller.GetModelCacheComponent(componentId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetModelCacheComponentResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Component.CacheId.Should().Be(componentId);
    }

    [Fact]
    public async Task GetModelCacheComponent_ShouldReturnBadRequest_WhenComponentIdIsEmpty()
    {
        // Act
        var result = await _controller.GetModelCacheComponent("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostModelCache_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new PostModelCacheRequest
        {
            ModelId = "test-model",
            Components = new List<string> { "unet", "vae" },
            CacheOptions = new Dictionary<string, object>
            {
                ["compression"] = "lz4",
                ["priority"] = "high"
            }
        };

        // Act
        var result = await _controller.PostModelCache(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostModelCacheResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task PostModelCache_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostModelCache(null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task DeleteModelCache_ShouldReturnOk_WhenCalled()
    {
        // Act
        var result = await _controller.DeleteModelCache();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<DeleteModelCacheResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();
        apiResponse.Data.Message.Should().Be("Model cache cleared successfully");
    }

    [Fact]
    public async Task DeleteModelCacheComponent_ShouldReturnOk_WhenComponentExists()
    {
        // Arrange
        var componentId = "test-component-id";

        // Act
        var result = await _controller.DeleteModelCacheComponent(componentId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<DeleteModelCacheComponentResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteModelCacheComponent_ShouldReturnBadRequest_WhenComponentIdIsEmpty()
    {
        // Act
        var result = await _controller.DeleteModelCacheComponent("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region VRAM Operations Tests

    [Fact]
    public async Task PostModelVramLoad_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new PostModelVramLoadRequest
        {
            ModelId = "test-model",
            DeviceId = Guid.NewGuid(),
            LoadOptions = new Dictionary<string, object>
            {
                ["optimization"] = "memory",
                ["precision"] = "fp16"
            }
        };

        // Act
        var result = await _controller.PostModelVramLoad(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostModelVramLoadResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task PostModelVramLoad_WithDeviceId_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new PostModelVramLoadRequest
        {
            ModelId = "device-model",
            DeviceId = Guid.NewGuid(),
            LoadOptions = new Dictionary<string, object>
            {
                ["strategy"] = "device-specific"
            }
        };

        // Act
        var result = await _controller.PostModelVramLoad(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<PostModelVramLoadResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task PostModelVramLoad_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostModelVramLoad(null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostModelVramLoad_WithDeviceId_ShouldReturnBadRequest_WhenDeviceIdIsEmpty()
    {
        // Arrange
        var request = new PostModelVramLoadRequest
        {
            ModelId = "test-model",
            DeviceId = Guid.NewGuid()
        };

        // Act
        var result = await _controller.PostModelVramLoad("", request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task DeleteModelVramUnload_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new DeleteModelVramUnloadRequest
        {
            ForceUnload = false,
            UnloadOptions = new Dictionary<string, object>
            {
                ["preserve_cache"] = true
            }
        };

        // Act
        var result = await _controller.DeleteModelVramUnload(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<DeleteModelVramUnloadResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteModelVramUnload_WithDeviceId_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var deviceId = "test-device-id";
        var request = new DeleteModelVramUnloadRequest
        {
            ForceUnload = true,
            UnloadOptions = new Dictionary<string, object>
            {
                ["immediate"] = true
            }
        };

        // Act
        var result = await _controller.DeleteModelVramUnload(deviceId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<DeleteModelVramUnloadResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteModelVramUnload_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.DeleteModelVramUnload(null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task DeleteModelVramUnload_WithDeviceId_ShouldReturnBadRequest_WhenDeviceIdIsEmpty()
    {
        // Arrange
        var request = new DeleteModelVramUnloadRequest
        {
            ForceUnload = false
        };

        // Act
        var result = await _controller.DeleteModelVramUnload("", request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Model Component Discovery Tests

    [Fact]
    public async Task GetModelComponents_ShouldReturnOk_WhenCalled()
    {
        // Act
        var result = await _controller.GetModelComponents();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetModelComponentsResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.Components.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetModelComponentsByType_ShouldReturnOk_WhenComponentTypeExists()
    {
        // Arrange
        var componentType = "unet";

        // Act
        var result = await _controller.GetModelComponentsByType(componentType);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetModelComponentsByTypeResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.ComponentType.Should().Be(componentType);
    }

    [Fact]
    public async Task GetModelComponentsByType_ShouldReturnBadRequest_WhenComponentTypeIsEmpty()
    {
        // Act
        var result = await _controller.GetModelComponentsByType("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetAvailableModels_ShouldReturnOk_WhenCalled()
    {
        // Act
        var result = await _controller.GetAvailableModels();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetAvailableModelsResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.AvailableModels.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAvailableModelsByType_ShouldReturnOk_WhenModelTypeExists()
    {
        // Arrange
        var modelType = "sdxl";

        // Act
        var result = await _controller.GetAvailableModelsByType(modelType);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GetAvailableModelsByTypeResponse>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.ModelType.Should().Be(modelType);
    }

    [Fact]
    public async Task GetAvailableModelsByType_ShouldReturnBadRequest_WhenModelTypeIsEmpty()
    {
        // Act
        var result = await _controller.GetAvailableModelsByType("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ControllerModel(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ControllerModel(_mockServiceModel.Object, null!));
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task GetModelStatus_ShouldReturnInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        _mockServiceModel.Setup(x => x.GetModelStatusAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetModelStatus();

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error in GetModelStatus")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostModelLoad_ShouldReturnInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        var request = new PostModelLoadRequest
        {
            ModelPath = "/models/test.safetensors",
            ModelType = ModelType.SDXL,
            DeviceId = Guid.NewGuid()
        };

        _mockServiceModel.Setup(x => x.PostModelLoadAsync(request))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.PostModelLoad(request);

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error in PostModelLoad")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Service Response Tests

    [Fact]
    public async Task GetModelStatus_ShouldReturnBadRequest_WhenServiceReturnsError()
    {
        // Arrange
        var serviceResponse = ApiResponse<GetModelStatusResponse>.CreateError(
            new ErrorDetails { Message = "Service error" });
        _mockServiceModel.Setup(x => x.GetModelStatusAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetModelStatus();

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostModelLoad_ShouldReturnBadRequest_WhenServiceReturnsError()
    {
        // Arrange
        var request = new PostModelLoadRequest
        {
            ModelPath = "/models/test.safetensors",
            ModelType = ModelType.SDXL,
            DeviceId = Guid.NewGuid()
        };

        var serviceResponse = ApiResponse<PostModelLoadResponse>.CreateError(
            new ErrorDetails { Message = "Load failed" });
        _mockServiceModel.Setup(x => x.PostModelLoadAsync(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostModelLoad(request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetModelStatus_WithDeviceId_ShouldReturnNotFound_WhenServiceReturnsNotFound()
    {
        // Arrange
        var deviceId = "non-existent-device";
        var serviceResponse = ApiResponse<GetModelStatusResponse>.CreateError(
            new ErrorDetails { Message = "Device not found" });
        _mockServiceModel.Setup(x => x.GetModelStatusAsync(deviceId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetModelStatus(deviceId);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    #endregion
}
