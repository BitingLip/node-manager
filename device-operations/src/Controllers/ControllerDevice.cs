using Microsoft.AspNetCore.Mvc;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Device;

namespace DeviceOperations.Controllers
{
    /// <summary>
    /// Device management controller for hardware discovery, monitoring, control, and optimization
    /// Implements all device endpoints as defined in BUILD_PLAN.md Phase 4
    /// </summary>
    [ApiController]
    [Route("api/device")]
    [Produces("application/json")]
    [Tags("Device Management")]
    public class ControllerDevice : ControllerBase
    {
        private readonly IServiceDevice _serviceDevice;
        private readonly ILogger<ControllerDevice> _logger;

        public ControllerDevice(
            IServiceDevice serviceDevice,
            ILogger<ControllerDevice> logger)
        {
            _serviceDevice = serviceDevice;
            _logger = logger;
        }

        #region Device Discovery and Information

        /// <summary>
        /// Get a list of all available devices in the system
        /// </summary>
        /// <returns>List of available devices with their IDs and basic information</returns>
        /// <response code="200">Returns the list of available devices</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("list")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceListResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceListResponse>>> GetDeviceList()
        {
            try
            {
                _logger.LogInformation("Getting device list");
                var result = await _serviceDevice.GetDeviceListAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceList");
                return StatusCode(500, ApiResponse<GetDeviceListResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get device capabilities and feature support for all devices
        /// </summary>
        /// <returns>Device capabilities across all devices</returns>
        /// <response code="200">Returns device capabilities</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("capabilities")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceCapabilitiesResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceCapabilitiesResponse>>> GetDeviceCapabilities()
        {
            try
            {
                _logger.LogInformation("Getting device capabilities for all devices");
                var result = await _serviceDevice.GetDeviceCapabilitiesAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceCapabilities");
                return StatusCode(500, ApiResponse<GetDeviceCapabilitiesResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get device capabilities and feature support for a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <returns>Device capabilities for the specified device</returns>
        /// <response code="200">Returns device capabilities</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("capabilities/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceCapabilitiesResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceCapabilitiesResponse>>> GetDeviceCapabilities(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<GetDeviceCapabilitiesResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting device capabilities for device: {DeviceId}", idDevice);
                var result = await _serviceDevice.GetDeviceCapabilitiesAsync(idDevice);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                if (result.Error?.Message?.Contains("not found") == true)
                {
                    return NotFound(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceCapabilities for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<GetDeviceCapabilitiesResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion

        #region Device Status and Health

        /// <summary>
        /// Get current operational status of all devices
        /// </summary>
        /// <returns>Status information for all devices</returns>
        /// <response code="200">Returns status for all devices</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("status")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceStatusResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceStatusResponse>>> GetDeviceStatus()
        {
            try
            {
                _logger.LogInformation("Getting device status for all devices");
                var result = await _serviceDevice.GetDeviceStatusAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceStatus");
                return StatusCode(500, ApiResponse<GetDeviceStatusResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get current operational status of a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <returns>Status information for the specified device</returns>
        /// <response code="200">Returns device status</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("status/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceStatusResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceStatusResponse>>> GetDeviceStatus(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<GetDeviceStatusResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting device status for device: {DeviceId}", idDevice);
                var result = await _serviceDevice.GetDeviceStatusAsync(idDevice);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                if (result.Error?.Message?.Contains("not found") == true)
                {
                    return NotFound(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceStatus for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<GetDeviceStatusResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get device health metrics and performance indicators for all devices
        /// </summary>
        /// <returns>Health metrics for all devices</returns>
        /// <response code="200">Returns health metrics for all devices</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("health")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceHealthResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceHealthResponse>>> GetDeviceHealth()
        {
            try
            {
                _logger.LogInformation("Getting device health for all devices");
                var result = await _serviceDevice.GetDeviceHealthAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceHealth");
                return StatusCode(500, ApiResponse<GetDeviceHealthResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get device health metrics and performance indicators for a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <returns>Health metrics for the specified device</returns>
        /// <response code="200">Returns device health metrics</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("health/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceHealthResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceHealthResponse>>> GetDeviceHealth(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<GetDeviceHealthResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting device health for device: {DeviceId}", idDevice);
                var result = await _serviceDevice.GetDeviceHealthAsync(idDevice);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                if (result.Error?.Message?.Contains("not found") == true)
                {
                    return NotFound(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceHealth for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<GetDeviceHealthResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion

        #region Device Control Operations

        /// <summary>
        /// Control device power states for all devices
        /// </summary>
        /// <param name="request">Power control request with action and parameters</param>
        /// <returns>Result of power control operation</returns>
        /// <response code="200">Power control completed successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("control/power")]
        [ProducesResponseType(typeof(ApiResponse<PostDevicePowerResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostDevicePowerResponse>>> PostDevicePower([FromBody] PostDevicePowerRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostDevicePowerResponse>.CreateError(
                        new ErrorDetails { Message = "Power control request cannot be null" }));
                }

                _logger.LogInformation("Controlling device power for all devices: {Action}", request.PowerAction);
                var result = await _serviceDevice.PostDevicePowerAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostDevicePower");
                return StatusCode(500, ApiResponse<PostDevicePowerResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Control device power states for a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <param name="request">Power control request with action and parameters</param>
        /// <returns>Result of power control operation</returns>
        /// <response code="200">Power control completed successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("control/{idDevice}/power")]
        [ProducesResponseType(typeof(ApiResponse<PostDevicePowerResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostDevicePowerResponse>>> PostDevicePower(string idDevice, [FromBody] PostDevicePowerRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<PostDevicePowerResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<PostDevicePowerResponse>.CreateError(
                        new ErrorDetails { Message = "Power control request cannot be null" }));
                }

                _logger.LogInformation("Controlling device power for device {DeviceId}: {Action}", idDevice, request.PowerAction);
                var result = await _serviceDevice.PostDevicePowerAsync(request, idDevice);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                if (result.Error?.Message?.Contains("not found") == true)
                {
                    return NotFound(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostDevicePower for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<PostDevicePowerResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Reset devices to default operational state for all devices
        /// </summary>
        /// <param name="request">Reset control request with parameters</param>
        /// <returns>Result of reset operation</returns>
        /// <response code="200">Reset completed successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("control/reset")]
        [ProducesResponseType(typeof(ApiResponse<PostDeviceResetResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostDeviceResetResponse>>> PostDeviceReset([FromBody] PostDeviceResetRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostDeviceResetResponse>.CreateError(
                        new ErrorDetails { Message = "Reset control request cannot be null" }));
                }

                _logger.LogInformation("Resetting all devices");
                var result = await _serviceDevice.PostDeviceResetAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostDeviceReset");
                return StatusCode(500, ApiResponse<PostDeviceResetResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Reset device to default operational state for a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <param name="request">Reset control request with parameters</param>
        /// <returns>Result of reset operation</returns>
        /// <response code="200">Reset completed successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("control/{idDevice}/reset")]
        [ProducesResponseType(typeof(ApiResponse<PostDeviceResetResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostDeviceResetResponse>>> PostDeviceReset(string idDevice, [FromBody] PostDeviceResetRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<PostDeviceResetResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<PostDeviceResetResponse>.CreateError(
                        new ErrorDetails { Message = "Reset control request cannot be null" }));
                }

                _logger.LogInformation("Resetting device: {DeviceId}", idDevice);
                var result = await _serviceDevice.PostDeviceResetAsync(request, idDevice);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                if (result.Error?.Message?.Contains("not found") == true)
                {
                    return NotFound(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostDeviceReset for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<PostDeviceResetResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Optimize device performance settings for all devices
        /// </summary>
        /// <param name="request">Optimization request with parameters</param>
        /// <returns>Result of optimization operation</returns>
        /// <response code="200">Optimization completed successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("control/optimize")]
        [ProducesResponseType(typeof(ApiResponse<PostDeviceOptimizeResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostDeviceOptimizeResponse>>> PostDeviceOptimize([FromBody] PostDeviceOptimizeRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostDeviceOptimizeResponse>.CreateError(
                        new ErrorDetails { Message = "Optimization request cannot be null" }));
                }

                _logger.LogInformation("Optimizing all devices");
                var result = await _serviceDevice.PostDeviceOptimizeAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostDeviceOptimize");
                return StatusCode(500, ApiResponse<PostDeviceOptimizeResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Optimize device performance settings for a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <param name="request">Optimization request with parameters</param>
        /// <returns>Result of optimization operation</returns>
        /// <response code="200">Optimization completed successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("control/{idDevice}/optimize")]
        [ProducesResponseType(typeof(ApiResponse<PostDeviceOptimizeResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostDeviceOptimizeResponse>>> PostDeviceOptimize(string idDevice, [FromBody] PostDeviceOptimizeRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<PostDeviceOptimizeResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<PostDeviceOptimizeResponse>.CreateError(
                        new ErrorDetails { Message = "Optimization request cannot be null" }));
                }

                _logger.LogInformation("Optimizing device: {DeviceId}", idDevice);
                var result = await _serviceDevice.PostDeviceOptimizeAsync(request, idDevice);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                if (result.Error?.Message?.Contains("not found") == true)
                {
                    return NotFound(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostDeviceOptimize for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<PostDeviceOptimizeResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion

        #region Device Information Details

        /// <summary>
        /// Get detailed information for all devices
        /// </summary>
        /// <returns>Comprehensive device information for all devices</returns>
        /// <response code="200">Returns detailed device information</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("details")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceDetailsResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceDetailsResponse>>> GetDeviceDetails()
        {
            try
            {
                _logger.LogInformation("Getting device details for all devices");
                var result = await _serviceDevice.GetDeviceDetailsAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceDetails");
                return StatusCode(500, ApiResponse<GetDeviceDetailsResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get detailed information for a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <returns>Comprehensive device information for the specified device</returns>
        /// <response code="200">Returns detailed device information</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("details/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceDetailsResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceDetailsResponse>>> GetDeviceDetails(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<GetDeviceDetailsResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting device details for device: {DeviceId}", idDevice);
                var result = await _serviceDevice.GetDeviceDetailsAsync(idDevice);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                if (result.Error?.Message?.Contains("not found") == true)
                {
                    return NotFound(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceDetails for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<GetDeviceDetailsResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get driver information for all devices
        /// </summary>
        /// <returns>Driver information for all devices</returns>
        /// <response code="200">Returns driver information for all devices</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("drivers")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceDriversResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceDriversResponse>>> GetDeviceDrivers()
        {
            try
            {
                _logger.LogInformation("Getting device drivers for all devices");
                var result = await _serviceDevice.GetDeviceDriversAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceDrivers");
                return StatusCode(500, ApiResponse<GetDeviceDriversResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get driver information for a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <returns>Driver information for the specified device</returns>
        /// <response code="200">Returns driver information for the device</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("drivers/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<GetDeviceDriversResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetDeviceDriversResponse>>> GetDeviceDrivers(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<GetDeviceDriversResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting device drivers for device: {DeviceId}", idDevice);
                var result = await _serviceDevice.GetDeviceDriversAsync(idDevice);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                if (result.Error?.Message?.Contains("not found") == true)
                {
                    return NotFound(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetDeviceDrivers for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<GetDeviceDriversResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion
    }
}
