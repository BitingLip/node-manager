using Microsoft.AspNetCore.Mvc;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Memory;

namespace DeviceOperations.Controllers
{
    /// <summary>
    /// Memory management controller for allocation, monitoring, optimization, and cleanup
    /// Implements all memory endpoints as defined in BUILD_PLAN.md Phase 4
    /// </summary>
    [ApiController]
    [Route("api/memory")]
    [Produces("application/json")]
    [Tags("Memory Management")]
    public class ControllerMemory : ControllerBase
    {
        private readonly IServiceMemory _serviceMemory;
        private readonly ILogger<ControllerMemory> _logger;

        public ControllerMemory(
            IServiceMemory serviceMemory,
            ILogger<ControllerMemory> logger)
        {
            _serviceMemory = serviceMemory;
            _logger = logger;
        }

        #region Memory Status Monitoring

        /// <summary>
        /// Get memory status for all devices
        /// </summary>
        /// <returns>Memory status information for all devices</returns>
        /// <response code="200">Returns memory status for all devices</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("status")]
        [ProducesResponseType(typeof(ApiResponse<GetMemoryStatusResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetMemoryStatusResponse>>> GetMemoryStatus()
        {
            try
            {
                _logger.LogInformation("Getting memory status for all devices");
                var result = await _serviceMemory.GetMemoryStatusAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetMemoryStatus");
                return StatusCode(500, ApiResponse<GetMemoryStatusResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get memory status for a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <returns>Memory status information for the specified device</returns>
        /// <response code="200">Returns memory status for the specified device</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("status/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<GetMemoryStatusResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetMemoryStatusResponse>>> GetMemoryStatus(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<GetMemoryStatusResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting memory status for device: {DeviceId}", idDevice);
                var result = await _serviceMemory.GetMemoryStatusAsync(idDevice);

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
                _logger.LogError(ex, "Unexpected error in GetMemoryStatus for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<GetMemoryStatusResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get detailed memory usage across all devices
        /// </summary>
        /// <returns>Detailed memory usage information for all devices</returns>
        /// <response code="200">Returns detailed memory usage for all devices</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("usage")]
        [ProducesResponseType(typeof(ApiResponse<GetMemoryUsageResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetMemoryUsageResponse>>> GetMemoryUsage()
        {
            try
            {
                _logger.LogInformation("Getting memory usage for all devices");
                var result = await _serviceMemory.GetMemoryUsageAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetMemoryUsage");
                return StatusCode(500, ApiResponse<GetMemoryUsageResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get detailed memory usage for a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <returns>Detailed memory usage information for the specified device</returns>
        /// <response code="200">Returns detailed memory usage for the device</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("usage/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<GetMemoryUsageResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetMemoryUsageResponse>>> GetMemoryUsage(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<GetMemoryUsageResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting memory usage for device: {DeviceId}", idDevice);
                var result = await _serviceMemory.GetMemoryUsageAsync(idDevice);

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
                _logger.LogError(ex, "Unexpected error in GetMemoryUsage for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<GetMemoryUsageResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion

        #region Memory Allocation Management

        /// <summary>
        /// Get all active memory allocations across all devices
        /// </summary>
        /// <returns>List of all active memory allocations</returns>
        /// <response code="200">Returns all active memory allocations</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("allocations")]
        [ProducesResponseType(typeof(ApiResponse<GetMemoryAllocationsResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetMemoryAllocationsResponse>>> GetMemoryAllocations()
        {
            try
            {
                _logger.LogInformation("Getting memory allocations for all devices");
                var result = await _serviceMemory.GetMemoryAllocationsAsync();

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetMemoryAllocations");
                return StatusCode(500, ApiResponse<GetMemoryAllocationsResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get memory allocations for a specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <returns>Memory allocations for the specified device</returns>
        /// <response code="200">Returns memory allocations for the device</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("allocations/device/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<GetMemoryAllocationsResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetMemoryAllocationsResponse>>> GetMemoryAllocations(string idDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<GetMemoryAllocationsResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting memory allocations for device: {DeviceId}", idDevice);
                var result = await _serviceMemory.GetMemoryAllocationsAsync(idDevice);

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
                _logger.LogError(ex, "Unexpected error in GetMemoryAllocations for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<GetMemoryAllocationsResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get details of a specific memory allocation
        /// </summary>
        /// <param name="allocationId">Allocation identifier</param>
        /// <returns>Details of the specified memory allocation</returns>
        /// <response code="200">Returns allocation details</response>
        /// <response code="404">Allocation not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("allocation/{allocationId}")]
        [ProducesResponseType(typeof(ApiResponse<GetMemoryAllocationResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetMemoryAllocationResponse>>> GetMemoryAllocation(string allocationId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(allocationId))
                {
                    return BadRequest(ApiResponse<GetMemoryAllocationResponse>.CreateError(
                        new ErrorDetails { Message = "Allocation ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting memory allocation: {AllocationId}", allocationId);
                var result = await _serviceMemory.GetMemoryAllocationAsync(allocationId);

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
                _logger.LogError(ex, "Unexpected error in GetMemoryAllocation for allocation: {AllocationId}", allocationId);
                return StatusCode(500, ApiResponse<GetMemoryAllocationResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Allocate memory block on optimal device
        /// </summary>
        /// <param name="request">Memory allocation request parameters</param>
        /// <returns>Result of memory allocation operation</returns>
        /// <response code="200">Memory allocated successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("allocations/allocate")]
        [ProducesResponseType(typeof(ApiResponse<PostMemoryAllocateResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostMemoryAllocateResponse>>> PostMemoryAllocate([FromBody] PostMemoryAllocateRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostMemoryAllocateResponse>.CreateError(
                        new ErrorDetails { Message = "Memory allocation request cannot be null" }));
                }

                _logger.LogInformation("Allocating memory on optimal device: {SizeBytes} bytes", request.SizeBytes);
                var result = await _serviceMemory.PostMemoryAllocateAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostMemoryAllocate");
                return StatusCode(500, ApiResponse<PostMemoryAllocateResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Allocate memory block on specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <param name="request">Memory allocation request parameters</param>
        /// <returns>Result of memory allocation operation</returns>
        /// <response code="200">Memory allocated successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("allocations/allocate/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<PostMemoryAllocateResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostMemoryAllocateResponse>>> PostMemoryAllocate(string idDevice, [FromBody] PostMemoryAllocateRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<PostMemoryAllocateResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<PostMemoryAllocateResponse>.CreateError(
                        new ErrorDetails { Message = "Memory allocation request cannot be null" }));
                }

                _logger.LogInformation("Allocating memory on device {DeviceId}: {SizeBytes} bytes", idDevice, request.SizeBytes);
                var result = await _serviceMemory.PostMemoryAllocateAsync(request, idDevice);

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
                _logger.LogError(ex, "Unexpected error in PostMemoryAllocate for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<PostMemoryAllocateResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Deallocate specific memory allocation
        /// </summary>
        /// <param name="allocationId">Allocation identifier</param>
        /// <returns>Result of memory deallocation operation</returns>
        /// <response code="200">Memory deallocated successfully</response>
        /// <response code="404">Allocation not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpDelete("allocations/{allocationId}")]
        [ProducesResponseType(typeof(ApiResponse<DeleteMemoryAllocationResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<DeleteMemoryAllocationResponse>>> DeleteMemoryAllocation(string allocationId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(allocationId))
                {
                    return BadRequest(ApiResponse<DeleteMemoryAllocationResponse>.CreateError(
                        new ErrorDetails { Message = "Allocation ID cannot be null or empty" }));
                }

                _logger.LogInformation("Deallocating memory allocation: {AllocationId}", allocationId);
                var result = await _serviceMemory.DeleteMemoryAllocationAsync(allocationId);

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
                _logger.LogError(ex, "Unexpected error in DeleteMemoryAllocation for allocation: {AllocationId}", allocationId);
                return StatusCode(500, ApiResponse<DeleteMemoryAllocationResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion

        #region Memory Operations

        /// <summary>
        /// Clear memory on all devices
        /// </summary>
        /// <param name="request">Memory clear request parameters</param>
        /// <returns>Result of memory clear operation</returns>
        /// <response code="200">Memory cleared successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("operations/clear")]
        [ProducesResponseType(typeof(ApiResponse<PostMemoryClearResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostMemoryClearResponse>>> PostMemoryClear([FromBody] PostMemoryClearRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostMemoryClearResponse>.CreateError(
                        new ErrorDetails { Message = "Memory clear request cannot be null" }));
                }

                _logger.LogInformation("Clearing memory on all devices");
                var result = await _serviceMemory.PostMemoryClearAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostMemoryClear");
                return StatusCode(500, ApiResponse<PostMemoryClearResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Clear memory on specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <param name="request">Memory clear request parameters</param>
        /// <returns>Result of memory clear operation</returns>
        /// <response code="200">Memory cleared successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("operations/clear/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<PostMemoryClearResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostMemoryClearResponse>>> PostMemoryClear(string idDevice, [FromBody] PostMemoryClearRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<PostMemoryClearResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<PostMemoryClearResponse>.CreateError(
                        new ErrorDetails { Message = "Memory clear request cannot be null" }));
                }

                _logger.LogInformation("Clearing memory on device: {DeviceId}", idDevice);
                var result = await _serviceMemory.PostMemoryClearAsync(request, idDevice);

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
                _logger.LogError(ex, "Unexpected error in PostMemoryClear for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<PostMemoryClearResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Defragment memory on all devices
        /// </summary>
        /// <param name="request">Memory defragmentation request parameters</param>
        /// <returns>Result of memory defragmentation operation</returns>
        /// <response code="200">Memory defragmented successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("operations/defragment")]
        [ProducesResponseType(typeof(ApiResponse<PostMemoryDefragmentResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostMemoryDefragmentResponse>>> PostMemoryDefragment([FromBody] PostMemoryDefragmentRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostMemoryDefragmentResponse>.CreateError(
                        new ErrorDetails { Message = "Memory defragmentation request cannot be null" }));
                }

                _logger.LogInformation("Defragmenting memory on all devices");
                var result = await _serviceMemory.PostMemoryDefragmentAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostMemoryDefragment");
                return StatusCode(500, ApiResponse<PostMemoryDefragmentResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Defragment memory on specific device
        /// </summary>
        /// <param name="idDevice">Device identifier</param>
        /// <param name="request">Memory defragmentation request parameters</param>
        /// <returns>Result of memory defragmentation operation</returns>
        /// <response code="200">Memory defragmented successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="404">Device not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("operations/defragment/{idDevice}")]
        [ProducesResponseType(typeof(ApiResponse<PostMemoryDefragmentResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostMemoryDefragmentResponse>>> PostMemoryDefragment(string idDevice, [FromBody] PostMemoryDefragmentRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(idDevice))
                {
                    return BadRequest(ApiResponse<PostMemoryDefragmentResponse>.CreateError(
                        new ErrorDetails { Message = "Device ID cannot be null or empty" }));
                }

                if (request == null)
                {
                    return BadRequest(ApiResponse<PostMemoryDefragmentResponse>.CreateError(
                        new ErrorDetails { Message = "Memory defragmentation request cannot be null" }));
                }

                _logger.LogInformation("Defragmenting memory on device: {DeviceId}", idDevice);
                var result = await _serviceMemory.PostMemoryDefragmentAsync(request, idDevice);

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
                _logger.LogError(ex, "Unexpected error in PostMemoryDefragment for device: {DeviceId}", idDevice);
                return StatusCode(500, ApiResponse<PostMemoryDefragmentResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion

        #region Memory Transfer Operations

        /// <summary>
        /// Transfer memory between devices
        /// </summary>
        /// <param name="request">Memory transfer request parameters</param>
        /// <returns>Result of memory transfer operation</returns>
        /// <response code="200">Memory transfer initiated successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpPost("transfer")]
        [ProducesResponseType(typeof(ApiResponse<PostMemoryTransferResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<PostMemoryTransferResponse>>> PostMemoryTransfer([FromBody] PostMemoryTransferRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<PostMemoryTransferResponse>.CreateError(
                        new ErrorDetails { Message = "Memory transfer request cannot be null" }));
                }

                _logger.LogInformation("Transferring memory from {SourceDevice} to {TargetDevice}", request.SourceDeviceId, request.TargetDeviceId);
                var result = await _serviceMemory.PostMemoryTransferAsync(request);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PostMemoryTransfer");
                return StatusCode(500, ApiResponse<PostMemoryTransferResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        /// <summary>
        /// Get status of memory transfer operation
        /// </summary>
        /// <param name="transferId">Transfer operation identifier</param>
        /// <returns>Status of the memory transfer operation</returns>
        /// <response code="200">Returns transfer status</response>
        /// <response code="404">Transfer not found</response>
        /// <response code="500">Internal server error occurred</response>
        [HttpGet("transfer/{transferId}")]
        [ProducesResponseType(typeof(ApiResponse<GetMemoryTransferResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<ActionResult<ApiResponse<GetMemoryTransferResponse>>> GetMemoryTransfer(string transferId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(transferId))
                {
                    return BadRequest(ApiResponse<GetMemoryTransferResponse>.CreateError(
                        new ErrorDetails { Message = "Transfer ID cannot be null or empty" }));
                }

                _logger.LogInformation("Getting memory transfer status: {TransferId}", transferId);
                var result = await _serviceMemory.GetMemoryTransferAsync(transferId);

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
                _logger.LogError(ex, "Unexpected error in GetMemoryTransfer for transfer: {TransferId}", transferId);
                return StatusCode(500, ApiResponse<GetMemoryTransferResponse>.CreateError(
                    new ErrorDetails { Message = "Internal server error occurred" }));
            }
        }

        #endregion
    }
}
