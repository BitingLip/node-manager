using Microsoft.AspNetCore.Mvc;
using DeviceOperations.Models;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services;
using DeviceOperations.Services.Processing;

namespace DeviceOperations.Controllers
{
    /// <summary>
    /// Controller for processing workflow coordination and batch operations
    /// </summary>
    [ApiController]
    [Route("api/processing")]
    public class ControllerProcessing : ControllerBase
    {
        private readonly IServiceProcessing _serviceProcessing;
        private readonly ILogger<ControllerProcessing> _logger;

        /// <summary>
        /// Initializes a new instance of the ControllerProcessing class
        /// </summary>
        /// <param name="serviceProcessing">The processing service</param>
        /// <param name="logger">The logger instance</param>
        public ControllerProcessing(IServiceProcessing serviceProcessing, ILogger<ControllerProcessing> logger)
        {
            _serviceProcessing = serviceProcessing ?? throw new ArgumentNullException(nameof(serviceProcessing));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Workflow Management

        /// <summary>
        /// Get all available processing workflows
        /// </summary>
        /// <returns>List of available processing workflows</returns>
        [HttpGet("workflows")]
        public async Task<IActionResult> GetProcessingWorkflows()
        {
            try
            {
                _logger.LogInformation("Retrieving all available processing workflows");
                
                var workflows = await _serviceProcessing.GetProcessingWorkflowsAsync();
                return Ok(ApiResponse<object>.CreateSuccess(workflows, "Processing workflows retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve processing workflows");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve processing workflows", ex.Message));
            }
        }

        /// <summary>
        /// Get details of a specific workflow
        /// </summary>
        /// <param name="workflowId">The unique identifier of the workflow</param>
        /// <returns>Detailed workflow information</returns>
        [HttpGet("workflows/{workflowId}")]
        public async Task<IActionResult> GetProcessingWorkflow(string workflowId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(workflowId))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Workflow ID is required", "Parameter 'workflowId' cannot be null or empty"));
                }

                _logger.LogInformation("Retrieving workflow details for ID: {WorkflowId}", workflowId);
                
                var workflow = await _serviceProcessing.GetProcessingWorkflowAsync(workflowId);
                if (workflow == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Workflow not found", $"Workflow '{workflowId}' not found"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(workflow, "Workflow details retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve workflow: {WorkflowId}", workflowId);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve workflow details", ex.Message));
            }
        }

        /// <summary>
        /// Execute a processing workflow
        /// </summary>
        /// <param name="request">Workflow execution parameters</param>
        /// <returns>Workflow execution results</returns>
        [HttpPost("workflows/execute")]
        public async Task<IActionResult> PostWorkflowExecute([FromBody] PostWorkflowExecuteRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostWorkflowExecuteResponse>.CreateError("Request body is required", "Workflow execution request cannot be null"));
                }

                _logger.LogInformation("Executing processing workflow");
                
                var result = await _serviceProcessing.PostWorkflowExecuteAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid workflow execution request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute workflow");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to execute workflow", ex.Message));
            }
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Get all active processing sessions
        /// </summary>
        /// <returns>List of active processing sessions</returns>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetProcessingSessions()
        {
            try
            {
                _logger.LogInformation("Retrieving all active processing sessions");
                
                var sessions = await _serviceProcessing.GetProcessingSessionsAsync();
                return Ok(ApiResponse<object>.CreateSuccess(sessions, "Processing sessions retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve processing sessions");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve processing sessions", ex.Message));
            }
        }

        /// <summary>
        /// Get details of a specific processing session
        /// </summary>
        /// <param name="sessionId">The unique identifier of the session</param>
        /// <returns>Detailed session information</returns>
        [HttpGet("sessions/{sessionId}")]
        public async Task<IActionResult> GetProcessingSession(string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Session ID is required", "Parameter 'sessionId' cannot be null or empty"));
                }

                _logger.LogInformation("Retrieving processing session: {SessionId}", sessionId);
                
                var session = await _serviceProcessing.GetProcessingSessionAsync(sessionId);
                if (session == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Session not found", $"Session '{sessionId}' not found"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(session, "Processing session retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve processing session: {SessionId}", sessionId);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve processing session", ex.Message));
            }
        }

        /// <summary>
        /// Control session (pause, resume, cancel)
        /// </summary>
        /// <param name="sessionId">The unique identifier of the session</param>
        /// <param name="request">Session control parameters</param>
        /// <returns>Session control results</returns>
        [HttpPost("sessions/{sessionId}/control")]
        public async Task<IActionResult> PostSessionControl(string sessionId, [FromBody] PostSessionControlRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return BadRequest(ApiResponse<PostSessionControlResponse>.CreateError("Session ID is required", "Parameter 'sessionId' cannot be null or empty"));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<PostSessionControlResponse>.CreateError("Request body is required", "Session control request cannot be null"));
                }

                _logger.LogInformation("Applying control action to session: {SessionId}", sessionId);
                
                var result = await _serviceProcessing.PostSessionControlAsync(sessionId, request);
                if (result == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Session not found", $"Session '{sessionId}' not found"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(result, "Session control operation completed successfully"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid session control request parameters for session: {SessionId}", sessionId);
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply control action to session: {SessionId}", sessionId);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to apply session control action", ex.Message));
            }
        }

        /// <summary>
        /// Cancel and remove a processing session
        /// </summary>
        /// <param name="sessionId">The unique identifier of the session</param>
        /// <returns>Session deletion results</returns>
        [HttpDelete("sessions/{sessionId}")]
        public async Task<IActionResult> DeleteProcessingSession(string sessionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Session ID is required", "Parameter 'sessionId' cannot be null or empty"));
                }

                _logger.LogInformation("Cancelling and removing processing session: {SessionId}", sessionId);
                
                var result = await _serviceProcessing.DeleteProcessingSessionAsync(sessionId);
                if (result == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Session not found", $"Session '{sessionId}' not found"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(result, "Processing session cancelled and removed successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel processing session: {SessionId}", sessionId);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to cancel and remove processing session", ex.Message));
            }
        }

        #endregion

        #region Batch Operations

        /// <summary>
        /// Get all batch operations
        /// </summary>
        /// <returns>List of batch operations</returns>
        [HttpGet("batches")]
        public async Task<IActionResult> GetProcessingBatches()
        {
            try
            {
                _logger.LogInformation("Retrieving all batch operations");
                
                var batches = await _serviceProcessing.GetProcessingBatchesAsync();
                return Ok(ApiResponse<object>.CreateSuccess(batches, "Batch operations retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve batch operations");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve batch operations", ex.Message));
            }
        }

        /// <summary>
        /// Get details of a specific batch
        /// </summary>
        /// <param name="batchId">The unique identifier of the batch</param>
        /// <returns>Detailed batch information</returns>
        [HttpGet("batches/{batchId}")]
        public async Task<IActionResult> GetProcessingBatch(string batchId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Batch ID is required", "Parameter 'batchId' cannot be null or empty"));
                }

                _logger.LogInformation("Retrieving batch details for ID: {BatchId}", batchId);
                
                var batch = await _serviceProcessing.GetProcessingBatchAsync(batchId);
                if (batch == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Batch not found", $"Batch '{batchId}' not found"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(batch, "Batch details retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve batch: {BatchId}", batchId);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve batch details", ex.Message));
            }
        }

        /// <summary>
        /// Create a new batch processing operation
        /// </summary>
        /// <param name="request">Batch creation parameters</param>
        /// <returns>Batch creation results</returns>
        [HttpPost("batches/create")]
        public async Task<IActionResult> PostBatchCreate([FromBody] PostBatchCreateRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostBatchCreateResponse>.CreateError("Request body is required", "Batch creation request cannot be null"));
                }

                _logger.LogInformation("Creating new batch processing operation");
                
                var result = await _serviceProcessing.PostBatchCreateAsync(request);
                return Ok(ApiResponse<object>.CreateSuccess(result, "Batch created successfully"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid batch creation request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create batch");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to create batch", ex.Message));
            }
        }

        /// <summary>
        /// Execute a batch processing operation
        /// </summary>
        /// <param name="batchId">The unique identifier of the batch</param>
        /// <param name="request">Batch execution parameters</param>
        /// <returns>Batch execution results</returns>
        [HttpPost("batches/{batchId}/execute")]
        public async Task<IActionResult> PostBatchExecute(string batchId, [FromBody] PostBatchExecuteRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    return BadRequest(ApiResponse<PostBatchExecuteResponse>.CreateError("Batch ID is required", "Parameter 'batchId' cannot be null or empty"));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<PostBatchExecuteResponse>.CreateError("Request body is required", "Batch execution request cannot be null"));
                }

                _logger.LogInformation("Executing batch: {BatchId}", batchId);
                
                var result = await _serviceProcessing.PostBatchExecuteAsync(batchId, request);
                if (result == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Batch not found", $"Batch '{batchId}' not found"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(result, "Batch execution started successfully"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid batch execution request parameters for batch: {BatchId}", batchId);
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute batch: {BatchId}", batchId);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to execute batch", ex.Message));
            }
        }

        /// <summary>
        /// Cancel and remove a batch operation
        /// </summary>
        /// <param name="batchId">The unique identifier of the batch</param>
        /// <returns>Batch deletion results</returns>
        [HttpDelete("batches/{batchId}")]
        public async Task<IActionResult> DeleteProcessingBatch(string batchId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Batch ID is required", "Parameter 'batchId' cannot be null or empty"));
                }

                _logger.LogInformation("Cancelling and removing batch: {BatchId}", batchId);
                
                var result = await _serviceProcessing.DeleteProcessingBatchAsync(batchId);
                if (result == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Batch not found", $"Batch '{batchId}' not found"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(result, "Batch cancelled and removed successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel batch: {BatchId}", batchId);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to cancel and remove batch", ex.Message));
            }
        }

        #endregion
    }
}
