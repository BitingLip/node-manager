using Microsoft.AspNetCore.Mvc;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Inference;
using CommonImageResolution = DeviceOperations.Models.Common.ImageResolution;

namespace DeviceOperations.Controllers
{
    /// <summary>
    /// Controller for inference operations
    /// Handles inference execution, validation, capabilities, and session management
    /// </summary>
    [ApiController]
    [Route("api/inference")]
    [Produces("application/json")]
    [Tags("Inference Operations")]
    public class ControllerInference : ControllerBase
    {
        private readonly ILogger<ControllerInference> _logger;
        private readonly IServiceInference _serviceInference;

        public ControllerInference(ILogger<ControllerInference> logger, IServiceInference serviceInference)
        {
            _logger = logger;
            _serviceInference = serviceInference;
        }

        #region Core Inference Operations

        /// <summary>
        /// Get system inference capabilities
        /// </summary>
        /// <returns>System inference capabilities and supported models</returns>
        /// <response code="200">Returns system inference capabilities</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("capabilities")]
        [ProducesResponseType(typeof(ApiResponse<GetInferenceCapabilitiesResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetInferenceCapabilitiesResponse>>> GetInferenceCapabilities()
        {
            try
            {
                _logger.LogInformation("Retrieving system inference capabilities");

                var result = await _serviceInference.GetInferenceCapabilitiesAsync();
                
                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving inference capabilities");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = new ErrorDetails
                    {
                        Code = "INFERENCE_CAPABILITIES_ERROR",
                        Message = "Failed to retrieve inference capabilities",
                        Details = new Dictionary<string, object> { ["error"] = ex.Message }
                    }
                });
            }
        }

        /// <summary>
        /// Get inference capabilities for a specific device
        /// </summary>
        /// <param name="idDevice">The unique identifier of the device</param>
        /// <returns>Device-specific inference capabilities</returns>
        /// <response code="200">Returns device-specific inference capabilities</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("capabilities/{idDevice:guid}")]
        [ProducesResponseType(typeof(ApiResponse<GetInferenceCapabilitiesDeviceResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetInferenceCapabilitiesDeviceResponse>>> GetInferenceCapabilitiesDevice([FromRoute] Guid idDevice)
        {
            try
            {
                _logger.LogInformation("Retrieving inference capabilities for device {DeviceId}", idDevice);

                var result = await _serviceInference.GetInferenceCapabilitiesAsync(idDevice.ToString());
                
                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving inference capabilities for device {DeviceId}", idDevice);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = new ErrorDetails
                    {
                        Code = "DEVICE_INFERENCE_CAPABILITIES_ERROR",
                        Message = "Failed to retrieve device inference capabilities",
                        Details = new Dictionary<string, object> { ["Exception"] = ex.Message }
                    }
                });
            }
        }

        /// <summary>
        /// Execute inference on optimal device
        /// </summary>
        /// <param name="request">Inference execution parameters</param>
        /// <returns>Inference results</returns>
        /// <response code="200">Inference completed successfully</response>
        /// <response code="400">Invalid inference request</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("execute")]
        [ProducesResponseType(typeof(ApiResponse<PostInferenceExecuteResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostInferenceExecuteResponse>>> PostInferenceExecute([FromBody] PostInferenceExecuteRequest request)
        {
            try
            {
                _logger.LogInformation("Executing {InferenceType} inference with model {ModelId}", request.InferenceType, request.ModelId);

                var result = await _serviceInference.PostInferenceExecuteAsync(request);
                
                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing inference");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = new ErrorDetails
                    {
                        Code = "INFERENCE_EXECUTION_ERROR",
                        Message = "Failed to execute inference",
                        Details = new Dictionary<string, object> { ["error"] = ex.Message }
                    }
                });
            }
        }

        /// <summary>
        /// Execute inference on a specific device
        /// </summary>
        /// <param name="idDevice">The unique identifier of the device</param>
        /// <param name="request">Inference execution parameters</param>
        /// <returns>Inference results</returns>
        /// <response code="200">Inference completed successfully</response>
        /// <response code="400">Invalid inference request</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("execute/{idDevice:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PostInferenceExecuteDeviceResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostInferenceExecuteDeviceResponse>>> PostInferenceExecuteDevice(
            [FromRoute] Guid idDevice, 
            [FromBody] PostInferenceExecuteDeviceRequest request)
        {
            try
            {
                _logger.LogInformation("Executing {InferenceType} inference on device {DeviceId} with model {ModelId}", 
                    request.InferenceType, idDevice, request.ModelId);

                var result = await _serviceInference.PostInferenceExecuteAsync(request, idDevice.ToString());
                
                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing inference on device {DeviceId}", idDevice);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = new ErrorDetails
                    {
                        Code = "DEVICE_INFERENCE_EXECUTION_ERROR",
                        Message = "Failed to execute inference on specified device",
                        Details = new Dictionary<string, object> { ["error"] = ex.Message }
                    }
                });
            }
        }

        #endregion

        #region Inference Validation

        /// <summary>
        /// Validate inference request without execution
        /// </summary>
        /// <param name="request">Inference validation parameters</param>
        /// <returns>Validation results</returns>
        /// <response code="200">Inference request validated</response>
        /// <response code="400">Invalid inference request</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(ApiResponse<PostInferenceValidateResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostInferenceValidateResponse>>> PostInferenceValidate([FromBody] PostInferenceValidateRequest request)
        {
            try
            {
                _logger.LogInformation("Validating {InferenceType} inference request", request.InferenceType);

                var result = await _serviceInference.PostInferenceValidateAsync(request);
                
                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating inference request");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = new ErrorDetails
                    {
                        Code = "INFERENCE_VALIDATION_ERROR",
                        Message = "Failed to validate inference request",
                        Details = new Dictionary<string, object> { ["error"] = ex.Message }
                    }
                });
            }
        }

        /// <summary>
        /// Get supported inference types
        /// </summary>
        /// <returns>List of supported inference types</returns>
        /// <response code="200">Returns supported inference types</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("supported-types")]
        [ProducesResponseType(typeof(ApiResponse<GetSupportedTypesResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetSupportedTypesResponse>>> GetSupportedTypes()
        {
            try
            {
                _logger.LogInformation("Retrieving supported inference types");

                var result = await _serviceInference.GetSupportedTypesAsync();
                
                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supported inference types");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = new ErrorDetails
                    {
                        Code = "SUPPORTED_TYPES_ERROR",
                        Message = "Failed to retrieve supported inference types",
                        Details = new Dictionary<string, object> { ["error"] = ex.Message }
                    }
                });
            }
        }

        /// <summary>
        /// Get supported inference types for a specific device
        /// </summary>
        /// <param name="idDevice">The unique identifier of the device</param>
        /// <returns>Device-specific supported inference types</returns>
        /// <response code="200">Returns device-specific supported inference types</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("supported-types/{idDevice:guid}")]
        [ProducesResponseType(typeof(ApiResponse<GetSupportedTypesDeviceResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetSupportedTypesDeviceResponse>>> GetSupportedTypesDevice([FromRoute] Guid idDevice)
        {
            try
            {
                _logger.LogInformation("Retrieving supported inference types for device {DeviceId}", idDevice);

                var result = await _serviceInference.GetSupportedTypesAsync(idDevice.ToString());
                
                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supported inference types for device {DeviceId}", idDevice);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = new ErrorDetails
                    {
                        Code = "DEVICE_SUPPORTED_TYPES_ERROR",
                        Message = "Failed to retrieve device-specific supported inference types",
                        Details = new Dictionary<string, object> { ["error"] = ex.Message }
                    }
                });
            }
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Get all active inference sessions
        /// </summary>
        /// <returns>List of active inference sessions</returns>
        /// <response code="200">Returns list of active inference sessions</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("sessions")]
        [ProducesResponseType(typeof(ApiResponse<GetInferenceSessionsResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetInferenceSessionsResponse>>> GetInferenceSessions()
        {
            try
            {
                _logger.LogInformation("Retrieving all active inference sessions");

                var result = await _serviceInference.GetInferenceSessionsAsync();
                
                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving inference sessions");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = new ErrorDetails
                    {
                        Code = "INFERENCE_SESSIONS_ERROR",
                        Message = "Failed to retrieve inference sessions",
                        Details = new Dictionary<string, object> { ["error"] = ex.Message }
                    }
                });
            }
        }

        /// <summary>
        /// Get details of a specific inference session
        /// </summary>
        /// <param name="idSession">The unique identifier of the session</param>
        /// <returns>Detailed session information</returns>
        /// <response code="200">Returns detailed session information</response>
        /// <response code="404">Session not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("sessions/{idSession:guid}")]
        [ProducesResponseType(typeof(ApiResponse<GetInferenceSessionResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetInferenceSessionResponse>>> GetInferenceSession([FromRoute] Guid idSession)
        {
            try
            {
                _logger.LogInformation("Retrieving inference session {SessionId}", idSession);

                var result = await _serviceInference.GetInferenceSessionAsync(idSession.ToString());
                
                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving inference session {SessionId}", idSession);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = new ErrorDetails
                    {
                        Code = "INFERENCE_SESSION_ERROR",
                        Message = "Failed to retrieve inference session",
                        Details = new Dictionary<string, object> { ["error"] = ex.Message }
                    }
                });
            }
        }

        /// <summary>
        /// Cancel an active inference session
        /// </summary>
        /// <param name="idSession">The unique identifier of the session</param>
        /// <param name="request">Session cancellation parameters</param>
        /// <returns>Session cancellation results</returns>
        /// <response code="200">Session cancelled successfully</response>
        /// <response code="404">Session not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpDelete("sessions/{idSession:guid}")]
        [ProducesResponseType(typeof(ApiResponse<DeleteInferenceSessionResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<DeleteInferenceSessionResponse>>> DeleteInferenceSession(
            [FromRoute] Guid idSession, 
            [FromBody] DeleteInferenceSessionRequest request)
        {
            try
            {
                _logger.LogInformation("Cancelling inference session {SessionId}", idSession);

                var result = await _serviceInference.DeleteInferenceSessionAsync(idSession.ToString(), request);
                
                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling inference session {SessionId}", idSession);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = new ErrorDetails
                    {
                        Code = "INFERENCE_SESSION_CANCEL_ERROR",
                        Message = "Failed to cancel inference session",
                        Details = new Dictionary<string, object> { ["error"] = ex.Message }
                    }
                });
            }
        }

        #endregion
    }
}
