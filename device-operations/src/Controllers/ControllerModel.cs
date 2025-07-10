using Microsoft.AspNetCore.Mvc;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Model;

namespace DeviceOperations.Controllers
{
    /// <summary>
    /// Model management controller for loading, caching, VRAM operations, and discovery
    /// Implements all model endpoints as defined in BUILD_PLAN.md Phase 4
    /// </summary>
    [ApiController]
    [Route("api/model")]
    [Produces("application/json")]
    [Tags("Model Management")]
    public class ControllerModel : ControllerBase
    {
        private readonly IServiceModel _serviceModel;
        private readonly ILogger<ControllerModel> _logger;

        public ControllerModel(
            IServiceModel serviceModel,
            ILogger<ControllerModel> logger)
        {
            _serviceModel = serviceModel;
            _logger = logger;
        }

        #region Core Model Operations

        /// <summary>
        /// Get model status for all devices
        /// </summary>
        /// <returns>Model status information for all devices</returns>
        /// <response code="200">Returns model status for all devices</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("status")]
        [ProducesResponseType(typeof(ApiResponse<GetModelStatusResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetModelStatusResponse>>> GetModelStatus()
        {
            try
            {
                _logger.LogInformation("Getting model status for all devices");
                var result = await _serviceModel.GetModelStatusAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetModelStatus");
                return StatusCode(500, ApiResponse<GetModelStatusResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get model status for specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <returns>Model status information for the specified device</returns>
        /// <response code="200">Returns model status for the specified device</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("status/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<GetModelStatusResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetModelStatusResponse>>> GetModelStatus(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<GetModelStatusResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting model status for device: {DeviceId}", idDevice);
                var result = await _serviceModel.GetModelStatusAsync(idDevice);

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
                _logger.LogError(ex, "Unexpected error in GetModelStatus for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<GetModelStatusResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Load model configuration onto all devices
        /// </summary>
        /// <param name="request">Model load request with configuration parameters</param>
        /// <returns>Result of model load operation</returns>
        /// <response code="200">Model loaded successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("load")]
        [ProducesResponseType(typeof(ApiResponse<PostModelLoadResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostModelLoadResponse>>> PostModelLoad([FromBody] PostModelLoadRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostModelLoadResponse>.CreateError(
                        new ErrorDetails { Message = "Model load request cannot be null" }));
                }

                _logger.LogInformation("Loading model configuration onto all devices: {ModelId}", request.ModelId);
                var result = await _serviceModel.PostModelLoadAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostModelLoad");
                return StatusCode(500, ApiResponse<PostModelLoadResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Load model configuration onto specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <param name="request">Model load request with configuration parameters</param>
        /// <returns>Result of model load operation</returns>
        /// <response code="200">Model loaded successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("load/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<PostModelLoadResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostModelLoadResponse>>> PostModelLoad(string idDevice, [FromBody] PostModelLoadRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<PostModelLoadResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<PostModelLoadResponse>.CreateError(
                        new ErrorDetails { Message = "Model load request cannot be null" }));
                }

                _logger.LogInformation("Loading model configuration onto device {DeviceId}: {ModelId}", idDevice, request.ModelId);
                var result = await _serviceModel.PostModelLoadAsync(request, idDevice);

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
                _logger.LogError(ex, "Unexpected error in PostModelLoad for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<PostModelLoadResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Unload all models from all devices
        /// </summary>
        /// <returns>Result of model unload operation</returns>
        /// <response code="200">Models unloaded successfully</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpDelete("unload")]
        [ProducesResponseType(typeof(ApiResponse<DeleteModelUnloadResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<DeleteModelUnloadResponse>>> DeleteModelUnload()
        {
            try
            {
                _logger.LogInformation("Unloading all models from all devices");
                var result = await _serviceModel.DeleteModelUnloadAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in DeleteModelUnload");
                return StatusCode(500, ApiResponse<DeleteModelUnloadResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Unload model from specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <returns>Result of model unload operation</returns>
        /// <response code="200">Model unloaded successfully</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpDelete("unload/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<DeleteModelUnloadResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<DeleteModelUnloadResponse>>> DeleteModelUnload(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<DeleteModelUnloadResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                _logger.LogInformation("Unloading model from device: {DeviceId}", idDevice);
                var result = await _serviceModel.DeleteModelUnloadAsync(idDevice);

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
                _logger.LogError(ex, "Unexpected error in DeleteModelUnload for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<DeleteModelUnloadResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion

        #region Model Cache Management (RAM)

        /// <summary>
        /// Get cached model components status from RAM
        /// </summary>
        /// <returns>Status of all cached model components</returns>
        /// <response code="200">Returns cached model components status</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("cache")]
        [ProducesResponseType(typeof(ApiResponse<GetModelCacheResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetModelCacheResponse>>> GetModelCache()
        {
            try
            {
                _logger.LogInformation("Getting cached model components status from RAM");
                var result = await _serviceModel.GetModelCacheAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetModelCache");
                return StatusCode(500, ApiResponse<GetModelCacheResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get specific component cache status
        /// </summary>
        /// <param name="componentId">Component identifier</param>
        /// <returns>Cache status of the specified component</returns>
        /// <response code="200">Returns component cache status</response>
        /// <response code="404">Component not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("cache/{componentId}")]
        [ProducesResponseType(typeof(ApiResponse<GetModelCacheComponentResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetModelCacheComponentResponse>>> GetModelCacheComponent(string componentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(componentId))
                {
                    return BadRequest(ApiResponse<GetModelCacheComponentResponse>.CreateError(
                        new ErrorDetails { Message = "Component ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting cache status for component: {ComponentId}", componentId);
                var result = await _serviceModel.GetModelCacheComponentAsync(componentId);

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
                _logger.LogError(ex, "Unexpected error in GetModelCacheComponent for component: {ComponentId}", componentId);
                return StatusCode(500, ApiResponse<GetModelCacheComponentResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Cache model components into RAM
        /// </summary>
        /// <param name="request">Model cache request with component parameters</param>
        /// <returns>Result of model cache operation</returns>
        /// <response code="200">Model components cached successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("cache")]
        [ProducesResponseType(typeof(ApiResponse<PostModelCacheResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostModelCacheResponse>>> PostModelCache([FromBody] PostModelCacheRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostModelCacheResponse>.CreateError(
                        new ErrorDetails { Message = "Model cache request cannot be null" }));
                }

                _logger.LogInformation("Caching model components into RAM");
                var result = await _serviceModel.PostModelCacheAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostModelCache");
                return StatusCode(500, ApiResponse<PostModelCacheResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Clear all model components from RAM
        /// </summary>
        /// <returns>Result of cache clear operation</returns>
        /// <response code="200">Model cache cleared successfully</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpDelete("cache")]
        [ProducesResponseType(typeof(ApiResponse<DeleteModelCacheResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<DeleteModelCacheResponse>>> DeleteModelCache()
        {
            try
            {
                _logger.LogInformation("Clearing all model components from RAM");
                var result = await _serviceModel.DeleteModelCacheAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in DeleteModelCache");
                return StatusCode(500, ApiResponse<DeleteModelCacheResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Clear specific component from RAM
        /// </summary>
        /// <param name="componentId">Component identifier</param>
        /// <returns>Result of component cache clear operation</returns>
        /// <response code="200">Component cleared from cache successfully</response>
        /// <response code="404">Component not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpDelete("cache/{componentId}")]
        [ProducesResponseType(typeof(ApiResponse<DeleteModelCacheComponentResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<DeleteModelCacheComponentResponse>>> DeleteModelCacheComponent(string componentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(componentId))
                {
                    return BadRequest(ApiResponse<DeleteModelCacheComponentResponse>.CreateError(
                        new ErrorDetails { Message = "Component ID cannot be null or empty" }));
                }

                _logger.LogInformation("Clearing component from RAM cache: {ComponentId}", componentId);
                var result = await _serviceModel.DeleteModelCacheComponentAsync(componentId);

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
                _logger.LogError(ex, "Unexpected error in DeleteModelCacheComponent for component: {ComponentId}", componentId);
                return StatusCode(500, ApiResponse<DeleteModelCacheComponentResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion

        #region VRAM Load/Unload Operations

        /// <summary>
        /// Load cached components to VRAM on all devices
        /// </summary>
        /// <param name="request">VRAM load request with component parameters</param>
        /// <returns>Result of VRAM load operation</returns>
        /// <response code="200">Components loaded to VRAM successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("vram/load")]
        [ProducesResponseType(typeof(ApiResponse<PostModelVramLoadResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostModelVramLoadResponse>>> PostModelVramLoad([FromBody] PostModelVramLoadRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostModelVramLoadResponse>.CreateError(
                        new ErrorDetails { Message = "VRAM load request cannot be null" }));
                }

                _logger.LogInformation("Loading cached components to VRAM on all devices");
                var result = await _serviceModel.PostModelVramLoadAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostModelVramLoad");
                return StatusCode(500, ApiResponse<PostModelVramLoadResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Load cached components to VRAM on specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <param name="request">VRAM load request with component parameters</param>
        /// <returns>Result of VRAM load operation</returns>
        /// <response code="200">Components loaded to VRAM successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("vram/load/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<PostModelVramLoadResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostModelVramLoadResponse>>> PostModelVramLoad(string idDevice, [FromBody] PostModelVramLoadRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<PostModelVramLoadResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<PostModelVramLoadResponse>.CreateError(
                        new ErrorDetails { Message = "VRAM load request cannot be null" }));
                }

                _logger.LogInformation("Loading cached components to VRAM on device: {DeviceId}", idDevice);
                var result = await _serviceModel.PostModelVramLoadAsync(request, idDevice);

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
                _logger.LogError(ex, "Unexpected error in PostModelVramLoad for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<PostModelVramLoadResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Unload components from VRAM on all devices
        /// </summary>
        /// <param name="request">VRAM unload request with component parameters</param>
        /// <returns>Result of VRAM unload operation</returns>
        /// <response code="200">Components unloaded from VRAM successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpDelete("vram/unload")]
        [ProducesResponseType(typeof(ApiResponse<DeleteModelVramUnloadResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<DeleteModelVramUnloadResponse>>> DeleteModelVramUnload([FromBody] DeleteModelVramUnloadRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<DeleteModelVramUnloadResponse>.CreateError(
                        new ErrorDetails { Message = "VRAM unload request cannot be null" }));
                }

                _logger.LogInformation("Unloading components from VRAM on all devices");
                var result = await _serviceModel.DeleteModelVramUnloadAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in DeleteModelVramUnload");
                return StatusCode(500, ApiResponse<DeleteModelVramUnloadResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Unload components from VRAM on specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <param name="request">VRAM unload request with component parameters</param>
        /// <returns>Result of VRAM unload operation</returns>
        /// <response code="200">Components unloaded from VRAM successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpDelete("vram/unload/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<DeleteModelVramUnloadResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<DeleteModelVramUnloadResponse>>> DeleteModelVramUnload(string idDevice, [FromBody] DeleteModelVramUnloadRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<DeleteModelVramUnloadResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<DeleteModelVramUnloadResponse>.CreateError(
                        new ErrorDetails { Message = "VRAM unload request cannot be null" }));
                }

                _logger.LogInformation("Unloading components from VRAM on device: {DeviceId}", idDevice);
                var result = await _serviceModel.DeleteModelVramUnloadAsync(request, idDevice);

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
                _logger.LogError(ex, "Unexpected error in DeleteModelVramUnload for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<DeleteModelVramUnloadResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion

        #region Model Component Discovery

        /// <summary>
        /// List all available model components
        /// </summary>
        /// <returns>List of all available model components</returns>
        /// <response code="200">Returns list of available model components</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("components")]
        [ProducesResponseType(typeof(ApiResponse<GetModelComponentsResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetModelComponentsResponse>>> GetModelComponents()
        {
            try
            {
                _logger.LogInformation("Getting list of all available model components");
                var result = await _serviceModel.GetModelComponentsAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetModelComponents");
                return StatusCode(500, ApiResponse<GetModelComponentsResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get components by type (unet, vae, encoder, etc.)
        /// </summary>
        /// <param name="componentType">Component type identifier</param>
        /// <returns>List of components matching the specified type</returns>
        /// <response code="200">Returns components by type</response>
        /// <response code="404">Component type not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("components/{componentType}")]
        [ProducesResponseType(typeof(ApiResponse<GetModelComponentsByTypeResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetModelComponentsByTypeResponse>>> GetModelComponentsByType(string componentType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(componentType))
                {
                    return BadRequest(ApiResponse<GetModelComponentsByTypeResponse>.CreateError(
                        new ErrorDetails { Message = "Component type cannot be null or empty" }));
                }

                _logger.LogInformation("Getting components by type: {ComponentType}", componentType);
                var result = await _serviceModel.GetModelComponentsByTypeAsync(componentType);

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
                _logger.LogError(ex, "Unexpected error in GetModelComponentsByType for type: {ComponentType}", componentType);
                return StatusCode(500, ApiResponse<GetModelComponentsByTypeResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get list of available models from model directory
        /// </summary>
        /// <returns>List of available models</returns>
        /// <response code="200">Returns list of available models</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("available")]
        [ProducesResponseType(typeof(ApiResponse<GetAvailableModelsResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetAvailableModelsResponse>>> GetAvailableModels()
        {
            try
            {
                _logger.LogInformation("Getting list of available models from model directory");
                var result = await _serviceModel.GetAvailableModelsAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetAvailableModels");
                return StatusCode(500, ApiResponse<GetAvailableModelsResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get available models by type (sdxl, sd15, flux, etc.)
        /// </summary>
        /// <param name="modelType">Model type identifier</param>
        /// <returns>List of available models matching the specified type</returns>
        /// <response code="200">Returns available models by type</response>
        /// <response code="404">Model type not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("available/{modelType}")]
        [ProducesResponseType(typeof(ApiResponse<GetAvailableModelsByTypeResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetAvailableModelsByTypeResponse>>> GetAvailableModelsByType(string modelType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(modelType))
                {
                    return BadRequest(ApiResponse<GetAvailableModelsByTypeResponse>.CreateError(
                        new ErrorDetails { Message = "Model type cannot be null or empty" }));
                }

                _logger.LogInformation("Getting available models by type: {ModelType}", modelType);
                var result = await _serviceModel.GetAvailableModelsByTypeAsync(modelType);

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
                _logger.LogError(ex, "Unexpected error in GetAvailableModelsByType for type: {ModelType}", modelType);
                return StatusCode(500, ApiResponse<GetAvailableModelsByTypeResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion
    }
}
