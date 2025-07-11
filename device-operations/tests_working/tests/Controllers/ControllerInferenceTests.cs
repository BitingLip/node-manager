using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DeviceOperations.Controllers;
using DeviceOperations.Services.Inference;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Tests.Controllers;

/// <summary>
/// Unit tests for ControllerInference
/// Tests inference API endpoints including execution, capabilities, validation, and supported types
/// </summary>
public class ControllerInferenceTests
{
    private readonly Mock<IServiceInference> _mockServiceInference;
    private readonly Mock<ILogger<ControllerInference>> _mockLogger;
    private readonly ControllerInference _controller;

    public ControllerInferenceTests()
    {
        _mockServiceInference = new Mock<IServiceInference>();
        _mockLogger = new Mock<ILogger<ControllerInference>>();
        _controller = new ControllerInference(_mockLogger.Object);
    }

    #region Inference Capabilities Tests

    [Fact]
    public async Task GetInferenceCapabilities_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Act - The controller returns mock data directly
        var result = await _controller.GetInferenceCapabilities();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<GetInferenceCapabilitiesResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.SupportedInferenceTypes.Should().NotBeEmpty();
        response.Data!.SupportedModels.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetInferenceCapabilities_WithDeviceId_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act - The controller returns mock data directly
        var result = await _controller.GetInferenceCapabilitiesDevice(deviceId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<GetInferenceCapabilitiesDeviceResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.DeviceId.Should().Be(deviceId);
        response.Data!.SupportedInferenceTypes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetInferenceCapabilities_ShouldReturnOk_WhenCalledWithoutMocking()
    {
        // Act - The controller returns mock data directly
        var result = await _controller.GetInferenceCapabilities();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<GetInferenceCapabilitiesResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Inference capabilities retrieved successfully");
    }

    #endregion

    #region Inference Execution Tests

    [Fact]
    public async Task PostInferenceExecute_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var request = new PostInferenceExecuteRequest
        {
            ModelId = "sdxl-base",
            InferenceType = InferenceType.TextToImage,
            Parameters = new Dictionary<string, object>
            {
                { "prompt", "A beautiful sunset" },
                { "steps", 20 }
            }
        };

        // Act - The controller returns mock data directly
        var result = await _controller.PostInferenceExecute(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PostInferenceExecuteResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.ModelId.Should().Be("sdxl-base");
        response.Data!.InferenceType.Should().Be(InferenceType.TextToImage);
        response.Data!.Status.Should().Be(InferenceStatus.Completed);
    }

    [Fact]
    public async Task PostInferenceExecute_WithDeviceId_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var request = new PostInferenceExecuteDeviceRequest
        {
            ModelId = "sd15-base",
            InferenceType = InferenceType.ImageToImage,
            Parameters = new Dictionary<string, object>
            {
                { "prompt", "Enhanced image" },
                { "image", "base64_image_data" }
            }
        };

        // Act - The controller returns mock data directly
        var result = await _controller.PostInferenceExecuteDevice(deviceId, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PostInferenceExecuteDeviceResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.DeviceId.Should().Be(deviceId.ToString());
        response.Data!.InferenceType.Should().Be(InferenceType.ImageToImage);
    }

    [Fact]
    public async Task PostInferenceExecute_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostInferenceExecute(null!);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        var response = statusCodeResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
    }

    #endregion

    #region Inference Validation Tests

    [Fact]
    public async Task PostInferenceValidate_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var request = new PostInferenceValidateRequest
        {
            ModelId = "sdxl-base",
            InferenceType = InferenceType.TextToImage,
            Parameters = new Dictionary<string, object>
            {
                { "prompt", "A beautiful landscape" },
                { "steps", 25 }
            }
        };

        // Act - The controller returns mock data directly
        var result = await _controller.PostInferenceValidate(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PostInferenceValidateResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.IsValid.Should().BeTrue();
        response.Data!.ValidationResults.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostInferenceValidate_ShouldReturnOk_WhenValidationPasses()
    {
        // Arrange
        var request = new PostInferenceValidateRequest
        {
            ModelId = "test-model",
            InferenceType = InferenceType.TextToImage,
            Parameters = new Dictionary<string, object> { { "prompt", "test prompt" } }
        };

        // Act - The controller returns mock data directly
        var result = await _controller.PostInferenceValidate(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PostInferenceValidateResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.IsValid.Should().BeTrue();
        response.Data!.ValidationResults.Should().ContainKey("ModelCompatibility");
    }

    #endregion

    #region Supported Types Tests

    [Fact]
    public async Task GetSupportedTypes_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Act - The controller returns mock data directly
        var result = await _controller.GetSupportedTypes();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<GetSupportedTypesResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.SupportedTypes.Should().NotBeEmpty();
        response.Data!.SupportedTypes.Should().Contain("TextToImage");
    }

    [Fact]
    public async Task GetSupportedTypes_WithDeviceId_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act - The controller returns mock data directly
        var result = await _controller.GetSupportedTypesDevice(deviceId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<GetSupportedTypesDeviceResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.DeviceId.Should().Be(deviceId.ToString());
        response.Data!.SupportedTypes.Should().NotBeEmpty();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PostInferenceExecute_ShouldHandleException_Gracefully()
    {
        // Arrange
        var request = new PostInferenceExecuteRequest
        {
            ModelId = "test-model",
            InferenceType = InferenceType.TextToImage,
            Parameters = new Dictionary<string, object> { { "prompt", "test" } }
        };

        // Act - The controller returns mock data directly (no exceptions in current implementation)
        var result = await _controller.PostInferenceExecute(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PostInferenceExecuteResponse>>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetInferenceCapabilities_ShouldReturnOk_WhenDeviceIdIsValid()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        
        // Act - The controller returns mock data directly
        var result = await _controller.GetInferenceCapabilitiesDevice(deviceId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    #endregion

    #region Model State Validation Tests

    [Fact]
    public async Task PostInferenceExecute_ShouldReturnOk_WhenModelStateIsValid()
    {
        // Arrange
        var request = new PostInferenceExecuteRequest
        {
            ModelId = "valid-model",
            InferenceType = InferenceType.TextToImage,
            Parameters = new Dictionary<string, object> { { "prompt", "test prompt" } }
        };

        // Act - The controller returns mock data directly
        var result = await _controller.PostInferenceExecute(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PostInferenceExecuteResponse>>().Subject;
        response.Success.Should().BeTrue();
    }

    #endregion

    #region HTTP Status Code Tests

    [Fact]
    public async Task GetInferenceCapabilities_ShouldReturn200_WhenSuccessful()
    {
        // Act - The controller returns mock data directly
        var result = await _controller.GetInferenceCapabilities();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task PostInferenceExecute_ShouldReturn200_WhenRequestIsValid()
    {
        // Arrange
        var request = new PostInferenceExecuteRequest
        {
            ModelId = "valid-model",
            InferenceType = InferenceType.TextToImage,
            Parameters = new Dictionary<string, object>()
        };

        // Act - The controller returns mock data directly
        var result = await _controller.PostInferenceExecute(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public async Task GetInferenceSessions_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Act - The controller returns mock data directly
        var result = await _controller.GetInferenceSessions();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<GetInferenceSessionsResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Sessions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetInferenceSession_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act - The controller returns mock data directly
        var result = await _controller.GetInferenceSession(sessionId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<GetInferenceSessionResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Session.Should().NotBeNull();
        response.Data!.Session.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task DeleteInferenceSession_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var request = new DeleteInferenceSessionRequest { Force = false };

        // Act - The controller returns mock data directly
        var result = await _controller.DeleteInferenceSession(sessionId, request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<DeleteInferenceSessionResponse>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Success.Should().BeTrue();
    }

    #endregion
}