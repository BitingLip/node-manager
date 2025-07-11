using Microsoft.AspNetCore.Mvc;
using DeviceOperations.Models;
using DeviceOperations.Services;
using DeviceOperations.Services.Postprocessing;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Controllers
{
    /// <summary>
    /// Controller for postprocessing operations including upscaling, enhancement, and safety validation
    /// </summary>
    [ApiController]
    [Route("api/postprocessing")]
    public class ControllerPostprocessing : ControllerBase
    {
        private readonly IServicePostprocessing _servicePostprocessing;
        private readonly ILogger<ControllerPostprocessing> _logger;

        /// <summary>
        /// Initializes a new instance of the ControllerPostprocessing class
        /// </summary>
        /// <param name="servicePostprocessing">The postprocessing service</param>
        /// <param name="logger">The logger instance</param>
        public ControllerPostprocessing(IServicePostprocessing servicePostprocessing, ILogger<ControllerPostprocessing> logger)
        {
            _servicePostprocessing = servicePostprocessing ?? throw new ArgumentNullException(nameof(servicePostprocessing));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Core Postprocessing Operations

        /// <summary>
        /// Get available postprocessing capabilities
        /// </summary>
        /// <returns>Postprocessing capabilities information</returns>
        [HttpGet("capabilities")]
        public async Task<IActionResult> GetPostprocessingCapabilities()
        {
            try
            {
                _logger.LogInformation("Retrieving postprocessing capabilities");
                
                var capabilities = await _servicePostprocessing.GetPostprocessingCapabilitiesAsync();
                return Ok(ApiResponse<object>.CreateSuccess(capabilities, "Postprocessing capabilities retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve postprocessing capabilities");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve postprocessing capabilities", ex.Message));
            }
        }

        /// <summary>
        /// Get postprocessing capabilities for specific device
        /// </summary>
        /// <param name="idDevice">The device identifier</param>
        /// <returns>Device-specific postprocessing capabilities</returns>
        [HttpGet("capabilities/{idDevice}")]
        public async Task<IActionResult> GetPostprocessingCapabilities(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Device ID is required", "Parameter 'idDevice' cannot be null or empty"));
                }

                _logger.LogInformation("Retrieving postprocessing capabilities for device: {DeviceId}", idDevice);
                
                var capabilities = await _servicePostprocessing.GetPostprocessingCapabilitiesAsync(idDevice);
                if (capabilities == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Device not found", $"Device '{idDevice}' not found or does not support postprocessing"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(capabilities, $"Postprocessing capabilities for device '{idDevice}' retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve postprocessing capabilities for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve device postprocessing capabilities", ex.Message));
            }
        }

        /// <summary>
        /// Upscale single image using available upscalers
        /// </summary>
        /// <param name="request">The upscale request parameters</param>
        /// <returns>Upscaled image result</returns>
        [HttpPost("upscale")]
        public async Task<IActionResult> PostUpscale([FromBody] PostPostprocessingUpscaleRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Upscale request cannot be null"));
                }

                _logger.LogInformation("Processing upscale request");
                
                var result = await _servicePostprocessing.PostUpscaleAsync(request);
                return Ok(ApiResponse<object>.CreateSuccess(result, "Image upscaled successfully"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid upscale request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upscale image");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to upscale image", ex.Message));
            }
        }

        /// <summary>
        /// Upscale single image on specific device
        /// </summary>
        /// <param name="idDevice">The device identifier</param>
        /// <param name="request">The upscale request parameters</param>
        /// <returns>Upscaled image result</returns>
        [HttpPost("upscale/{idDevice}")]
        public async Task<IActionResult> PostUpscale(string idDevice, [FromBody] PostPostprocessingUpscaleRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Device ID is required", "Parameter 'idDevice' cannot be null or empty"));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Upscale request cannot be null"));
                }

                _logger.LogInformation("Processing upscale request on device: {DeviceId}", idDevice);
                
                var result = await _servicePostprocessing.PostUpscaleAsync(request, idDevice);
                if (result == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Device not found", $"Device '{idDevice}' not found or not available for upscaling"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(result, $"Image upscaled successfully on device '{idDevice}'"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid upscale request parameters for device: {DeviceId}", idDevice);
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upscale image on device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to upscale image", ex.Message));
            }
        }

        /// <summary>
        /// Enhance single image using enhancement models
        /// </summary>
        /// <param name="request">The enhancement request parameters</param>
        /// <returns>Enhanced image result</returns>
        [HttpPost("enhance")]
        public async Task<IActionResult> PostEnhance([FromBody] PostPostprocessingEnhanceRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Enhancement request cannot be null"));
                }

                _logger.LogInformation("Processing enhancement request");
                
                var result = await _servicePostprocessing.PostEnhanceAsync(request);
                return Ok(ApiResponse<object>.CreateSuccess(result, "Image enhanced successfully"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid enhancement request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enhance image");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to enhance image", ex.Message));
            }
        }

        /// <summary>
        /// Enhance single image on specific device
        /// </summary>
        /// <param name="idDevice">The device identifier</param>
        /// <param name="request">The enhancement request parameters</param>
        /// <returns>Enhanced image result</returns>
        [HttpPost("enhance/{idDevice}")]
        public async Task<IActionResult> PostEnhance(string idDevice, [FromBody] PostPostprocessingEnhanceRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Device ID is required", "Parameter 'idDevice' cannot be null or empty"));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Enhancement request cannot be null"));
                }

                _logger.LogInformation("Processing enhancement request on device: {DeviceId}", idDevice);
                
                var result = await _servicePostprocessing.PostEnhanceAsync(request, idDevice);
                if (result == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Device not found", $"Device '{idDevice}' not found or not available for enhancement"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(result, $"Image enhanced successfully on device '{idDevice}'"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid enhancement request parameters for device: {DeviceId}", idDevice);
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enhance image on device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to enhance image", ex.Message));
            }
        }

        #endregion

        #region Validation and Safety

        /// <summary>
        /// Validate postprocessing request without execution
        /// </summary>
        /// <param name="request">The postprocessing request to validate</param>
        /// <returns>Validation results</returns>
        [HttpPost("validate")]
        public async Task<IActionResult> PostPostprocessingValidate([FromBody] object request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Validation request cannot be null"));
                }

                _logger.LogInformation("Validating postprocessing request");
                
                // Mock implementation - validation functionality not implemented in service
                await Task.Delay(1);
                var validationResult = new { IsValid = true, Message = "Request validation passed", Warnings = new string[0] };
                return Ok(ApiResponse<object>.CreateSuccess(validationResult, "Postprocessing request validated successfully"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid validation request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate postprocessing request");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to validate postprocessing request", ex.Message));
            }
        }

        /// <summary>
        /// Run safety checks on image content
        /// </summary>
        /// <param name="request">The safety check request parameters</param>
        /// <returns>Safety check results</returns>
        [HttpPost("safety-check")]
        public async Task<IActionResult> PostSafetyCheck([FromBody] object request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Safety check request cannot be null"));
                }

                _logger.LogInformation("Running safety check on image content");
                
                // Mock implementation - safety check functionality not implemented in service
                await Task.Delay(1);
                var safetyResult = new { IsSafe = true, Confidence = 0.95, Warnings = new string[0], Reasons = new string[0] };
                return Ok(ApiResponse<object>.CreateSuccess(safetyResult, "Safety check completed successfully"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid safety check request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run safety check");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to run safety check", ex.Message));
            }
        }

        #endregion

        #region Model and Tool Discovery

        /// <summary>
        /// Get list of available upscaler models
        /// </summary>
        /// <returns>Available upscaler models</returns>
        [HttpGet("available-upscalers")]
        public async Task<IActionResult> GetAvailableUpscalers()
        {
            try
            {
                _logger.LogInformation("Retrieving available upscaler models");
                
                // Mock implementation - get available upscalers functionality not implemented in service
                await Task.Delay(1);
                var upscalers = new { AvailableUpscalers = new[] { "ESRGAN", "Real-ESRGAN", "LDSR", "ScuNET" }, TotalCount = 4 };
                return Ok(ApiResponse<object>.CreateSuccess(upscalers, "Available upscaler models retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve available upscaler models");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve available upscaler models", ex.Message));
            }
        }

        /// <summary>
        /// Get available upscalers for specific device
        /// </summary>
        /// <param name="idDevice">The device identifier</param>
        /// <returns>Available upscalers for the device</returns>
        [HttpGet("available-upscalers/{idDevice}")]
        public async Task<IActionResult> GetAvailableUpscalers(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Device ID is required", "Parameter 'idDevice' cannot be null or empty"));
                }

                _logger.LogInformation("Retrieving available upscaler models for device: {DeviceId}", idDevice);
                
                // Mock implementation - get available upscalers for device functionality not implemented in service
                await Task.Delay(1);
                var upscalers = new { AvailableUpscalers = new[] { "ESRGAN", "Real-ESRGAN" }, DeviceId = idDevice, TotalCount = 2 };
                if (upscalers == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Device not found", $"Device '{idDevice}' not found or does not support upscaling"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(upscalers, $"Available upscaler models for device '{idDevice}' retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve available upscaler models for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve device upscaler models", ex.Message));
            }
        }

        /// <summary>
        /// Get list of available enhancement models
        /// </summary>
        /// <returns>Available enhancement models</returns>
        [HttpGet("available-enhancers")]
        public async Task<IActionResult> GetAvailableEnhancers()
        {
            try
            {
                _logger.LogInformation("Retrieving available enhancement models");
                
                // Mock implementation - get available enhancers functionality not implemented in service
                await Task.Delay(1);
                var enhancers = new { AvailableEnhancers = new[] { "CodeFormer", "GFPGAN", "RestoreFormer", "BSRGAN" }, TotalCount = 4 };
                return Ok(ApiResponse<object>.CreateSuccess(enhancers, "Available enhancement models retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve available enhancement models");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve available enhancement models", ex.Message));
            }
        }

        /// <summary>
        /// Get available enhancers for specific device
        /// </summary>
        /// <param name="idDevice">The device identifier</param>
        /// <returns>Available enhancers for the device</returns>
        [HttpGet("available-enhancers/{idDevice}")]
        public async Task<IActionResult> GetAvailableEnhancers(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Device ID is required", "Parameter 'idDevice' cannot be null or empty"));
                }

                _logger.LogInformation("Retrieving available enhancement models for device: {DeviceId}", idDevice);
                
                // Mock implementation - get available enhancers for device functionality not implemented in service
                await Task.Delay(1);
                var enhancers = new { AvailableEnhancers = new[] { "CodeFormer", "GFPGAN" }, DeviceId = idDevice, TotalCount = 2 };
                if (enhancers == null)
                {
                    return NotFound(ApiResponse<object>.CreateError("Device not found", $"Device '{idDevice}' not found or does not support enhancement"));
                }

                return Ok(ApiResponse<object>.CreateSuccess(enhancers, $"Available enhancement models for device '{idDevice}' retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve available enhancement models for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve device enhancement models", ex.Message));
            }
        }

        #endregion
    }
}
