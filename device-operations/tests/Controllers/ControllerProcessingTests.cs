using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DeviceOperations.Controllers;
using DeviceOperations.Services.Processing;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Tests.Controllers;

/// <summary>
/// Unit tests for ControllerProcessing
/// Tests processing workflow coordination and batch operations API endpoints
/// </summary>
public class ControllerProcessingTests
{
    private readonly Mock<IServiceProcessing> _mockServiceProcessing;
    private readonly Mock<ILogger<ControllerProcessing>> _mockLogger;
    private readonly ControllerProcessing _controller;

    public ControllerProcessingTests()
    {
        _mockServiceProcessing = new Mock<IServiceProcessing>();
        _mockLogger = new Mock<ILogger<ControllerProcessing>>();
        _controller = new ControllerProcessing(_mockServiceProcessing.Object, _mockLogger.Object);
    }

    #region Workflow Management Tests

    [Fact]
    public async Task GetProcessingWorkflows_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new GetProcessingWorkflowsResponse
        {
            Workflows = new List<WorkflowInfo>
            {
                new WorkflowInfo
                {
                    Id = "workflow-1",
                    Name = "Text to Image Workflow",
                    Description = "Standard text to image generation workflow",
                    Version = "1.0",
                    Parameters = new List<WorkflowParameter>
                    {
                        new WorkflowParameter
                        {
                            Name = "prompt",
                            Type = "string",
                            Required = true,
                            Description = "Text prompt for generation"
                        }
                    },
                    EstimatedDuration = TimeSpan.FromMinutes(2),
                    ResourceRequirements = new Dictionary<string, object>
                    {
                        ["gpu_memory"] = "4GB",
                        ["device_type"] = "CUDA"
                    }
                }
            },
            TotalCount = 1
        };

        var serviceResponse = ApiResponse<GetProcessingWorkflowsResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.GetProcessingWorkflowsAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetProcessingWorkflows();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();

        _mockServiceProcessing.Verify(x => x.GetProcessingWorkflowsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetProcessingWorkflow_ShouldReturnOk_WhenWorkflowExists()
    {
        // Arrange
        var workflowId = "test-workflow-id";
        var expectedResponse = new GetProcessingWorkflowResponse
        {
            Workflow = new WorkflowInfo
            {
                Id = workflowId,
                Name = "Test Workflow",
                Description = "Test workflow description",
                Version = "1.0",
                Parameters = new List<WorkflowParameter>(),
                EstimatedDuration = TimeSpan.FromMinutes(1),
                ResourceRequirements = new Dictionary<string, object>()
            }
        };

        var serviceResponse = ApiResponse<GetProcessingWorkflowResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.GetProcessingWorkflowAsync(workflowId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetProcessingWorkflow(workflowId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.GetProcessingWorkflowAsync(workflowId), Times.Once);
    }

    [Fact]
    public async Task GetProcessingWorkflow_ShouldReturnBadRequest_WhenWorkflowIdIsEmpty()
    {
        // Act
        var result = await _controller.GetProcessingWorkflow("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostWorkflowExecute_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new PostWorkflowExecuteRequest
        {
            WorkflowId = "test-workflow",
            Parameters = new Dictionary<string, object>
            {
                ["prompt"] = "test prompt",
                ["steps"] = 20
            },
            Priority = 5,
            Background = false
        };

        var expectedResponse = new PostWorkflowExecuteResponse
        {
            ExecutionId = "exec-123",
            Status = "Running",
            EstimatedCompletion = DateTime.UtcNow.AddMinutes(5),
            Progress = 0.0
        };

        var serviceResponse = ApiResponse<PostWorkflowExecuteResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.PostWorkflowExecuteAsync(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostWorkflowExecute(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.PostWorkflowExecuteAsync(request), Times.Once);
    }

    [Fact]
    public async Task PostWorkflowExecute_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostWorkflowExecute(null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public async Task GetProcessingSessions_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new GetProcessingSessionsResponse
        {
            Sessions = new List<DeviceOperations.Models.Responses.ProcessingSession>
            {
                new DeviceOperations.Models.Responses.ProcessingSession
                {
                    Id = "session-1",
                    Name = "Test Session",
                    Status = "Running",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                    UpdatedAt = DateTime.UtcNow,
                    WorkflowId = "workflow-1",
                    Configuration = new Dictionary<string, object>
                    {
                        ["gpu"] = "0",
                        ["batch_size"] = 1
                    },
                    ResourceUsage = new Dictionary<string, object>
                    {
                        ["memory"] = "2048MB",
                        ["gpu_utilization"] = "85%"
                    }
                }
            },
            TotalCount = 1
        };

        var serviceResponse = ApiResponse<GetProcessingSessionsResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.GetProcessingSessionsAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetProcessingSessions();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.GetProcessingSessionsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetProcessingSession_ShouldReturnOk_WhenSessionExists()
    {
        // Arrange
        var sessionId = "test-session-id";
        var expectedResponse = new GetProcessingSessionResponse
        {
            Session = new DeviceOperations.Models.Responses.ProcessingSession
            {
                Id = sessionId,
                Name = "Test Session",
                Status = "Running",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow,
                WorkflowId = "workflow-1",
                Configuration = new Dictionary<string, object>(),
                ResourceUsage = new Dictionary<string, object>()
            }
        };

        var serviceResponse = ApiResponse<GetProcessingSessionResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.GetProcessingSessionAsync(sessionId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetProcessingSession(sessionId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.GetProcessingSessionAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task GetProcessingSession_ShouldReturnBadRequest_WhenSessionIdIsEmpty()
    {
        // Act
        var result = await _controller.GetProcessingSession("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostSessionControl_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var sessionId = "test-session-id";
        var request = new PostSessionControlRequest
        {
            Action = "pause",
            Parameters = new Dictionary<string, object>
            {
                ["reason"] = "user_request"
            }
        };

        var expectedResponse = new PostSessionControlResponse
        {
            Action = "pause",
            Result = "success",
            Status = "Paused"
        };

        var serviceResponse = ApiResponse<PostSessionControlResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.PostSessionControlAsync(sessionId, request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostSessionControl(sessionId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.PostSessionControlAsync(sessionId, request), Times.Once);
    }

    [Fact]
    public async Task PostSessionControl_ShouldReturnBadRequest_WhenSessionIdIsEmpty()
    {
        // Arrange
        var request = new PostSessionControlRequest
        {
            Action = "pause",
            Parameters = new Dictionary<string, object>()
        };

        // Act
        var result = await _controller.PostSessionControl("", request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostSessionControl_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostSessionControl("session-id", null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task DeleteProcessingSession_ShouldReturnOk_WhenSessionExists()
    {
        // Arrange
        var sessionId = "test-session-id";
        var expectedResponse = new DeleteProcessingSessionResponse
        {
            Success = true,
            Message = "Session deleted successfully"
        };

        var serviceResponse = ApiResponse<DeleteProcessingSessionResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.DeleteProcessingSessionAsync(sessionId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DeleteProcessingSession(sessionId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.DeleteProcessingSessionAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task DeleteProcessingSession_ShouldReturnBadRequest_WhenSessionIdIsEmpty()
    {
        // Act
        var result = await _controller.DeleteProcessingSession("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Batch Operations Tests

    [Fact]
    public async Task GetProcessingBatches_ShouldReturnOk_WhenServiceReturnsSuccess()
    {
        // Arrange
        var expectedResponse = new GetProcessingBatchesResponse
        {
            Batches = new List<BatchJob>
            {
                new BatchJob
                {
                    Id = "batch-1",
                    Name = "Test Batch",
                    Type = "image_generation",
                    Status = "Running",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                    StartedAt = DateTime.UtcNow.AddMinutes(-25),
                    TotalItems = 10,
                    CompletedItems = 5,
                    FailedItems = 0,
                    Progress = new BatchProgress
                    {
                        Percentage = 50.0,
                        ItemsProcessed = 5,
                        TotalItems = 10,
                        EstimatedTimeRemaining = TimeSpan.FromMinutes(5),
                        ProcessingRate = 2.0
                    },
                    Configuration = new Dictionary<string, object>
                    {
                        ["batch_size"] = 1,
                        ["priority"] = 5
                    }
                }
            },
            TotalCount = 1
        };

        var serviceResponse = ApiResponse<GetProcessingBatchesResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.GetProcessingBatchesAsync())
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetProcessingBatches();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.GetProcessingBatchesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetProcessingBatch_ShouldReturnOk_WhenBatchExists()
    {
        // Arrange
        var batchId = "test-batch-id";
        var expectedResponse = new GetProcessingBatchResponse
        {
            Batch = new BatchJob
            {
                Id = batchId,
                Name = "Test Batch",
                Type = "text_to_image",
                Status = "Completed",
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                StartedAt = DateTime.UtcNow.AddMinutes(-50),
                CompletedAt = DateTime.UtcNow.AddMinutes(-10),
                TotalItems = 5,
                CompletedItems = 5,
                FailedItems = 0,
                Progress = new BatchProgress
                {
                    Percentage = 100.0,
                    ItemsProcessed = 5,
                    TotalItems = 5,
                    ProcessingRate = 1.5
                },
                Configuration = new Dictionary<string, object>()
            }
        };

        var serviceResponse = ApiResponse<GetProcessingBatchResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.GetProcessingBatchAsync(batchId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.GetProcessingBatch(batchId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.GetProcessingBatchAsync(batchId), Times.Once);
    }

    [Fact]
    public async Task GetProcessingBatch_ShouldReturnBadRequest_WhenBatchIdIsEmpty()
    {
        // Act
        var result = await _controller.GetProcessingBatch("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostBatchCreate_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new PostBatchCreateRequest
        {
            Name = "Test Batch",
            Type = "text_to_image",
            Items = new List<BatchItem>
            {
                new BatchItem
                {
                    Id = "item-1",
                    Type = "generation",
                    Input = new Dictionary<string, object>
                    {
                        ["prompt"] = "test prompt 1"
                    },
                    Configuration = new Dictionary<string, object>()
                }
            },
            Configuration = new Dictionary<string, object>
            {
                ["model"] = "sdxl",
                ["steps"] = 20
            },
            Priority = 5
        };

        var expectedResponse = new PostBatchCreateResponse
        {
            BatchId = "batch-123",
            Status = "Created",
            EstimatedDuration = TimeSpan.FromMinutes(10)
        };

        var serviceResponse = ApiResponse<PostBatchCreateResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.PostBatchCreateAsync(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostBatchCreate(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.PostBatchCreateAsync(request), Times.Once);
    }

    [Fact]
    public async Task PostBatchCreate_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostBatchCreate(null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostBatchExecute_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var batchId = "test-batch-id";
        var request = new PostBatchExecuteRequest
        {
            Parameters = new Dictionary<string, object>
            {
                ["concurrent_limit"] = 2,
                ["retry_failed"] = true
            },
            Background = true
        };

        var expectedResponse = new PostBatchExecuteResponse
        {
            ExecutionId = "exec-456",
            Status = "Running",
            Progress = new BatchProgress
            {
                Percentage = 0.0,
                ItemsProcessed = 0,
                TotalItems = 5,
                ProcessingRate = 0.0
            }
        };

        var serviceResponse = ApiResponse<PostBatchExecuteResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.PostBatchExecuteAsync(batchId, request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.PostBatchExecute(batchId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.PostBatchExecuteAsync(batchId, request), Times.Once);
    }

    [Fact]
    public async Task PostBatchExecute_ShouldReturnBadRequest_WhenBatchIdIsEmpty()
    {
        // Arrange
        var request = new PostBatchExecuteRequest
        {
            Parameters = new Dictionary<string, object>(),
            Background = true
        };

        // Act
        var result = await _controller.PostBatchExecute("", request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task PostBatchExecute_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        // Act
        var result = await _controller.PostBatchExecute("batch-id", null!);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task DeleteProcessingBatch_ShouldReturnOk_WhenBatchExists()
    {
        // Arrange
        var batchId = "test-batch-id";
        var expectedResponse = new DeleteProcessingBatchResponse
        {
            Success = true,
            Message = "Batch deleted successfully"
        };

        var serviceResponse = ApiResponse<DeleteProcessingBatchResponse>.CreateSuccess(expectedResponse, "Success");
        _mockServiceProcessing.Setup(x => x.DeleteProcessingBatchAsync(batchId))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await _controller.DeleteProcessingBatch(batchId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        _mockServiceProcessing.Verify(x => x.DeleteProcessingBatchAsync(batchId), Times.Once);
    }

    [Fact]
    public async Task DeleteProcessingBatch_ShouldReturnBadRequest_WhenBatchIdIsEmpty()
    {
        // Act
        var result = await _controller.DeleteProcessingBatch("");

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ControllerProcessing(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ControllerProcessing(_mockServiceProcessing.Object, null!));
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task GetProcessingWorkflows_ShouldReturnInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        _mockServiceProcessing.Setup(x => x.GetProcessingWorkflowsAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetProcessingWorkflows();

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to retrieve processing workflows")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostWorkflowExecute_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // Arrange
        var request = new PostWorkflowExecuteRequest
        {
            WorkflowId = "invalid-workflow",
            Parameters = new Dictionary<string, object>()
        };

        _mockServiceProcessing.Setup(x => x.PostWorkflowExecuteAsync(request))
            .ThrowsAsync(new ArgumentException("Invalid workflow parameters"));

        // Act
        var result = await _controller.PostWorkflowExecute(request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid workflow execution request parameters")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostSessionControl_ShouldReturnInternalServerError_WhenExceptionThrown()
    {
        // Arrange
        var sessionId = "session-id";
        var request = new PostSessionControlRequest
        {
            Action = "pause",
            Parameters = new Dictionary<string, object>()
        };

        _mockServiceProcessing.Setup(x => x.PostSessionControlAsync(sessionId, request))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.PostSessionControl(sessionId, request);

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to apply control action to session")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostBatchCreate_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // Arrange
        var request = new PostBatchCreateRequest
        {
            Name = "",
            Type = "invalid",
            Items = new List<BatchItem>()
        };

        _mockServiceProcessing.Setup(x => x.PostBatchCreateAsync(request))
            .ThrowsAsync(new ArgumentException("Invalid batch parameters"));

        // Act
        var result = await _controller.PostBatchCreate(request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid batch creation request parameters")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PostBatchExecute_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // Arrange
        var batchId = "batch-id";
        var request = new PostBatchExecuteRequest
        {
            Parameters = new Dictionary<string, object>(),
            Background = true
        };

        _mockServiceProcessing.Setup(x => x.PostBatchExecuteAsync(batchId, request))
            .ThrowsAsync(new ArgumentException("Invalid execution parameters"));

        // Act
        var result = await _controller.PostBatchExecute(batchId, request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);

        // Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid batch execution request parameters")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Service Response Tests

    [Fact]
    public async Task GetProcessingWorkflow_ShouldReturnNotFound_WhenWorkflowNotFound()
    {
        // Arrange
        var workflowId = "non-existent-workflow";
        _mockServiceProcessing.Setup(x => x.GetProcessingWorkflowAsync(workflowId))
            .ReturnsAsync((ApiResponse<GetProcessingWorkflowResponse>)null!);

        // Act
        var result = await _controller.GetProcessingWorkflow(workflowId);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetProcessingSession_ShouldReturnNotFound_WhenSessionNotFound()
    {
        // Arrange
        var sessionId = "non-existent-session";
        _mockServiceProcessing.Setup(x => x.GetProcessingSessionAsync(sessionId))
            .ReturnsAsync((ApiResponse<GetProcessingSessionResponse>)null!);

        // Act
        var result = await _controller.GetProcessingSession(sessionId);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task PostSessionControl_ShouldReturnNotFound_WhenSessionNotFound()
    {
        // Arrange
        var sessionId = "non-existent-session";
        var request = new PostSessionControlRequest
        {
            Action = "pause",
            Parameters = new Dictionary<string, object>()
        };

        _mockServiceProcessing.Setup(x => x.PostSessionControlAsync(sessionId, request))
            .ReturnsAsync((ApiResponse<PostSessionControlResponse>)null!);

        // Act
        var result = await _controller.PostSessionControl(sessionId, request);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task DeleteProcessingSession_ShouldReturnNotFound_WhenSessionNotFound()
    {
        // Arrange
        var sessionId = "non-existent-session";
        _mockServiceProcessing.Setup(x => x.DeleteProcessingSessionAsync(sessionId))
            .ReturnsAsync((ApiResponse<DeleteProcessingSessionResponse>)null!);

        // Act
        var result = await _controller.DeleteProcessingSession(sessionId);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetProcessingBatch_ShouldReturnNotFound_WhenBatchNotFound()
    {
        // Arrange
        var batchId = "non-existent-batch";
        _mockServiceProcessing.Setup(x => x.GetProcessingBatchAsync(batchId))
            .ReturnsAsync((ApiResponse<GetProcessingBatchResponse>)null!);

        // Act
        var result = await _controller.GetProcessingBatch(batchId);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task PostBatchExecute_ShouldReturnNotFound_WhenBatchNotFound()
    {
        // Arrange
        var batchId = "non-existent-batch";
        var request = new PostBatchExecuteRequest
        {
            Parameters = new Dictionary<string, object>(),
            Background = true
        };

        _mockServiceProcessing.Setup(x => x.PostBatchExecuteAsync(batchId, request))
            .ReturnsAsync((ApiResponse<PostBatchExecuteResponse>)null!);

        // Act
        var result = await _controller.PostBatchExecute(batchId, request);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task DeleteProcessingBatch_ShouldReturnNotFound_WhenBatchNotFound()
    {
        // Arrange
        var batchId = "non-existent-batch";
        _mockServiceProcessing.Setup(x => x.DeleteProcessingBatchAsync(batchId))
            .ReturnsAsync((ApiResponse<DeleteProcessingBatchResponse>)null!);

        // Act
        var result = await _controller.DeleteProcessingBatch(batchId);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    #endregion
}
