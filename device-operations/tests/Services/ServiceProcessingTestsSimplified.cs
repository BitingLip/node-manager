using DeviceOperations.Services.Processing;
using DeviceOperations.Services.Python;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace DeviceOperations.Tests.Services;

/// <summary>
/// Unit tests for ServiceProcessing
/// </summary>
public class ServiceProcessingTestsSimplified
{
    private readonly Mock<ILogger<ServiceProcessing>> _mockLogger;
    private readonly Mock<IPythonWorkerService> _mockPythonWorkerService;
    private readonly ServiceProcessing _serviceProcessing;

    public ServiceProcessingTestsSimplified()
    {
        _mockLogger = new Mock<ILogger<ServiceProcessing>>();
        _mockPythonWorkerService = new Mock<IPythonWorkerService>();
        _serviceProcessing = new ServiceProcessing(_mockLogger.Object, _mockPythonWorkerService.Object);
    }

    #region Basic Workflow Tests

    [Fact]
    public async Task GetProcessingWorkflowsAsync_Success_ReturnsWorkflows()
    {
        // Arrange
        var mockResponse = new
        {
            workflows = new[]
            {
                new { id = "workflow-1", name = "Text to Image", type = "txt2img" },
                new { id = "workflow-2", name = "Image to Image", type = "img2img" }
            }
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceProcessing.GetProcessingWorkflowsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProcessingWorkflowAsync_ValidId_ReturnsWorkflow()
    {
        // Arrange
        var workflowId = "workflow-1";
        var mockResponse = new
        {
            id = workflowId,
            name = "Text to Image",
            type = "txt2img",
            description = "Generate images from text prompts"
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceProcessing.GetProcessingWorkflowAsync(workflowId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task PostWorkflowExecuteAsync_ValidRequest_ReturnsExecution()
    {
        // Arrange
        var request = new PostWorkflowExecuteRequest
        {
            WorkflowId = "workflow-1",
            Parameters = new Dictionary<string, object>
            {
                ["prompt"] = "A beautiful landscape",
                ["steps"] = 20
            }
        };

        var mockResponse = new
        {
            success = true,
            execution_id = Guid.NewGuid(),
            workflow_id = request.WorkflowId,
            status = "running"
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceProcessing.PostWorkflowExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public async Task GetProcessingSessionsAsync_Success_ReturnsSessions()
    {
        // Arrange
        var mockResponse = new
        {
            sessions = new[]
            {
                new { id = Guid.NewGuid().ToString(), type = "workflow_execution", status = "running" },
                new { id = Guid.NewGuid().ToString(), type = "batch_processing", status = "completed" }
            }
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceProcessing.GetProcessingSessionsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProcessingSessionAsync_ValidId_ReturnsSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var mockResponse = new
        {
            id = sessionId,
            type = "workflow_execution",
            status = "running",
            progress = 0.5f
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _serviceProcessing.GetProcessingSessionAsync(sessionId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteProcessingSessionAsync_ValidId_ReturnsSuccess()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var request = new DeleteProcessingSessionRequest
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
        var result = await _serviceProcessing.DeleteProcessingSessionAsync(sessionId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task PostWorkflowExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _serviceProcessing.PostWorkflowExecuteAsync(null!));
    }

    [Fact]
    public async Task GetProcessingWorkflowAsync_EmptyId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _serviceProcessing.GetProcessingWorkflowAsync(string.Empty));
    }

    [Fact]
    public async Task DeleteProcessingSessionAsync_EmptyId_ThrowsArgumentException()
    {
        // Arrange
        var request = new DeleteProcessingSessionRequest();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _serviceProcessing.DeleteProcessingSessionAsync(string.Empty, request));
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task GetProcessingWorkflowsAsync_ConcurrentCalls_HandlesCorrectly()
    {
        // Arrange
        var mockResponse = new
        {
            workflows = new[] { new { id = "workflow-1", name = "Test Workflow" } }
        };

        _mockPythonWorkerService.Setup(x => x.ExecuteAsync<object, dynamic>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _serviceProcessing.GetProcessingWorkflowsAsync())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    #endregion
}
