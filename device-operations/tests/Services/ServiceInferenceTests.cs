using DeviceOperations.Services.Inference;
using DeviceOperations.Services.Python;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace DeviceOperations.Tests.Services;

/// <summary>
/// Unit tests for ServiceInference
/// </summary>
public class ServiceInferenceTests
{
    private readonly Mock<ILogger<ServiceInference>> _mockLogger;
    private readonly Mock<IPythonWorkerService> _mockPythonWorkerService;
    private readonly ServiceInference _serviceInference;

    public ServiceInferenceTests()
    {
        _mockLogger = new Mock<ILogger<ServiceInference>>();
        _mockPythonWorkerService = new Mock<IPythonWorkerService>();
        _serviceInference = new ServiceInference(_mockLogger.Object, _mockPythonWorkerService.Object);
    }

    #region GetInferenceCapabilitiesAsync Tests

    [Fact]
    public async Task GetInferenceCapabilitiesAsync_Success_ReturnsCapabilities()
    {
        // Arrange
        var mockCapabilities = new
        {
            supported_inference_types = new[] { "txt2img", "img2img" },
            max_concurrent_inferences = 4,
            device_capabilities = new Dictionary<string, object>
            { 
                { 
                    "gpu-0",
                    new 
                    {
                        SupportedInferenceTypes = new[] { "txt2img", "img2img" },
                        SupportedPrecisions = new[] { "fp16", "fp32" },
                        MaxBatchSize = 4,
                        MaxConcurrentInferences = 2,
                        MaxResolution = 2048
                    }
                }
            }
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCapabilities);

        // Act
        var result = await _serviceInference.GetInferenceCapabilitiesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInferenceCapabilitiesAsync_PythonWorkerException_ReturnsFailure()
    {
        // Arrange
        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Python worker error"));

        // Act
        var result = await _serviceInference.GetInferenceCapabilitiesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task GetInferenceCapabilitiesAsync_DeviceSpecific_Success()
    {
        // Arrange
        var deviceId = "gpu-0";
        var mockCapabilities = new
        {
            device_id = deviceId,
            supported_inference_types = new[] { "txt2img", "img2img" },
            max_concurrent_inferences = 2,
            memory_available = 12000000000L
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCapabilities);

        // Act
        var result = await _serviceInference.GetInferenceCapabilitiesAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    #endregion

    #region PostInferenceExecuteAsync Tests

    [Fact]
    public async Task PostInferenceExecuteAsync_Success_ReturnsExecutionResult()
    {
        // Arrange
        var request = new PostInferenceExecuteRequest
        {
            ModelId = "stable-diffusion-v1-5",
            Parameters = new Dictionary<string, object>
            {
                ["prompt"] = "a beautiful landscape",
                ["width"] = 512,
                ["height"] = 512
            }
        };

        var mockResponse = new
        {
            success = true,
            inference_id = Guid.NewGuid(),
            model_id = request.ModelId,
            device_id = "gpu-0",
            results = new Dictionary<string, object>
            {
                ["images"] = new[] { "base64_image_data" }
            },
            execution_time = TimeSpan.FromSeconds(5),
            completed_at = DateTime.UtcNow
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceInference.PostInferenceExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task PostInferenceExecuteAsync_InvalidModel_ReturnsFailure()
    {
        // Arrange
        var request = new PostInferenceExecuteRequest
        {
            ModelId = "invalid-model",
            Parameters = new Dictionary<string, object>()
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Model not found"));

        // Act
        var result = await _serviceInference.PostInferenceExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task PostInferenceExecuteAsync_DeviceSpecific_Success()
    {
        // Arrange
        var deviceId = "gpu-0";
        var request = new PostInferenceExecuteDeviceRequest
        {
            ModelId = "stable-diffusion-v1-5",
            Parameters = new Dictionary<string, object>
            {
                ["prompt"] = "a beautiful landscape"
            }
        };

        var mockResponse = new
        {
            success = true,
            inference_id = Guid.NewGuid(),
            model_id = request.ModelId,
            device_id = deviceId,
            results = new Dictionary<string, object>()
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceInference.PostInferenceExecuteAsync(request, deviceId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    #endregion

    #region PostInferenceValidateAsync Tests

    [Fact]
    public async Task PostInferenceValidateAsync_ValidRequest_ReturnsValid()
    {
        // Arrange
        var request = new PostInferenceValidateRequest
        {
            ModelId = "stable-diffusion-v1-5",
            Parameters = new Dictionary<string, object>
            {
                ["prompt"] = "a beautiful landscape",
                ["width"] = 512,
                ["height"] = 512
            }
        };

        var mockResponse = new
        {
            is_valid = true,
            validation_errors = new List<string>(),
            validation_time = TimeSpan.FromMilliseconds(100),
            validated_at = DateTime.UtcNow,
            estimated_execution_time = TimeSpan.FromSeconds(5),
            estimated_memory_usage = 2048000000L
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceInference.PostInferenceValidateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task PostInferenceValidateAsync_InvalidRequest_ReturnsInvalid()
    {
        // Arrange
        var request = new PostInferenceValidateRequest
        {
            ModelId = "invalid-model",
            Parameters = new Dictionary<string, object>()
        };

        var mockResponse = new
        {
            is_valid = false,
            validation_errors = new[] { "Model not found", "Missing required parameters" },
            validation_time = TimeSpan.FromMilliseconds(50),
            validated_at = DateTime.UtcNow
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceInference.PostInferenceValidateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    #endregion

    #region GetSupportedTypesAsync Tests

    [Fact]
    public async Task GetSupportedTypesAsync_Success_ReturnsTypes()
    {
        // Arrange
        var mockResponse = new
        {
            supported_types = new[] { "txt2img", "img2img", "inpainting", "controlnet" },
            total_types = 4,
            last_updated = DateTime.UtcNow
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceInference.GetSupportedTypesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSupportedTypesAsync_DeviceSpecific_Success()
    {
        // Arrange
        var deviceId = "gpu-0";
        var mockResponse = new
        {
            supported_types = new[] { "txt2img", "img2img" },
            device_id = deviceId,
            device_name = "NVIDIA RTX 4090",
            total_types = 2,
            last_updated = DateTime.UtcNow
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceInference.GetSupportedTypesAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public async Task GetInferenceSessionsAsync_Success_ReturnsSessions()
    {
        // Arrange
        var mockResponse = new
        {
            sessions = new[]
            {
                new { session_id = Guid.NewGuid(), status = "running" },
                new { session_id = Guid.NewGuid(), status = "completed" }
            }
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceInference.GetInferenceSessionsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetInferenceSessionAsync_ValidSession_ReturnsSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var mockResponse = new
        {
            session_id = sessionId,
            status = "running",
            progress = 0.75f,
            started_at = DateTime.UtcNow.AddMinutes(-5)
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceInference.GetInferenceSessionAsync(sessionId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteInferenceSessionAsync_ValidSession_ReturnsSuccess()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var deleteRequest = new DeleteInferenceSessionRequest
        {
            Force = false
        };

        var mockResponse = new
        {
            success = true,
            session_id = sessionId,
            message = "Session deleted successfully"
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceInference.DeleteInferenceSessionAsync(sessionId, deleteRequest);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PostInferenceExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _serviceInference.PostInferenceExecuteAsync(null!));
    }

    [Fact]
    public async Task PostInferenceValidateAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _serviceInference.PostInferenceValidateAsync(null!));
    }

    [Fact]
    public async Task GetInferenceSessionAsync_EmptySessionId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _serviceInference.GetInferenceSessionAsync(string.Empty));
    }

    [Fact]
    public async Task DeleteInferenceSessionAsync_EmptySessionId_ThrowsArgumentException()
    {
        // Arrange
        var deleteRequest = new DeleteInferenceSessionRequest();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _serviceInference.DeleteInferenceSessionAsync(string.Empty, deleteRequest));
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task PostInferenceExecuteAsync_LargeRequest_HandlesCorrectly()
    {
        // Arrange
        var request = new PostInferenceExecuteRequest
        {
            ModelId = "stable-diffusion-xl",
            Parameters = new Dictionary<string, object>
            {
                ["prompt"] = new string('a', 10000), // Large prompt
                ["width"] = 1024,
                ["height"] = 1024,
                ["batch_size"] = 4
            }
        };

        var mockResponse = new
        {
            success = true,
            inference_id = Guid.NewGuid(),
            results = new Dictionary<string, object>()
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceInference.PostInferenceExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetInferenceCapabilitiesAsync_ConcurrentCalls_HandlesCorrectly()
    {
        // Arrange
        var mockCapabilities = new
        {
            supported_inference_types = new[] { "txt2img" },
            max_concurrent_inferences = 4
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCapabilities);

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _serviceInference.GetInferenceCapabilitiesAsync())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        _mockPythonWorkerService.Verify(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), 
            Times.AtLeastOnce);
    }

    #endregion

    #region Resource Management Tests

    [Fact]
    public async Task PostInferenceExecuteAsync_MemoryConstraints_HandlesGracefully()
    {
        // Arrange
        var request = new PostInferenceExecuteRequest
        {
            ModelId = "large-model",
            Parameters = new Dictionary<string, object>
            {
                ["prompt"] = "test",
                ["width"] = 2048,
                ["height"] = 2048
            }
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OutOfMemoryException("Insufficient GPU memory"));

        // Act
        var result = await _serviceInference.PostInferenceExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    #endregion
}
