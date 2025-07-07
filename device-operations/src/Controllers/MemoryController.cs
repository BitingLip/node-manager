using DeviceOperations.Models.Requests;
using DeviceOperations.Services.Memory;
using Microsoft.AspNetCore.Mvc;

namespace DeviceOperations.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly IMemoryOperationsService _memoryService;
    private readonly ILogger<MemoryController> _logger;

    public MemoryController(IMemoryOperationsService memoryService, ILogger<MemoryController> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get memory status for all devices
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetMemoryStatus()
    {
        try
        {
            var status = await _memoryService.GetAllMemoryStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory status");
            return StatusCode(500, new { error = "Failed to retrieve memory status", details = ex.Message });
        }
    }

    /// <summary>
    /// Get memory status for a specific device
    /// </summary>
    [HttpGet("device/{deviceId}")]
    public async Task<IActionResult> GetDeviceMemoryStatus(string deviceId)
    {
        try
        {
            var status = await _memoryService.GetMemoryStatusAsync(deviceId);
            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get memory status for device {deviceId}");
            return StatusCode(500, new { error = "Failed to retrieve device memory status", details = ex.Message });
        }
    }

    /// <summary>
    /// Allocate memory on a specific device
    /// </summary>
    [HttpPost("allocate")]
    public async Task<IActionResult> AllocateMemory([FromBody] MemoryAllocateRequest request)
    {
        try
        {
            var result = await _memoryService.AllocateMemoryAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to allocate memory on device {request.DeviceId}");
            return StatusCode(500, new { error = "Memory allocation failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Deallocate memory by allocation ID
    /// </summary>
    [HttpDelete("deallocate/{allocationId}")]
    public async Task<IActionResult> DeallocateMemory(string allocationId)
    {
        try
        {
            var result = await _memoryService.DeallocateMemoryAsync(allocationId);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return NotFound(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to deallocate memory for allocation {allocationId}");
            return StatusCode(500, new { error = "Memory deallocation failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Copy data between memory allocations
    /// </summary>
    [HttpPost("copy")]
    public async Task<IActionResult> CopyMemory([FromBody] MemoryCopyRequest request)
    {
        try
        {
            var result = await _memoryService.CopyMemoryAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to copy memory from {request.SourceAllocationId} to {request.DestinationAllocationId}");
            return StatusCode(500, new { error = "Memory copy failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Clear memory allocation with specified value
    /// </summary>
    [HttpPost("clear/{allocationId}")]
    public async Task<IActionResult> ClearMemory(string allocationId, [FromBody] MemoryClearRequest request)
    {
        try
        {
            // Ensure the allocation ID in the route matches the request
            request.AllocationId = allocationId;
            
            var result = await _memoryService.ClearMemoryAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to clear memory for allocation {allocationId}");
            return StatusCode(500, new { error = "Memory clear failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get details of a specific memory allocation
    /// </summary>
    [HttpGet("allocation/{allocationId}")]
    public async Task<IActionResult> GetAllocation(string allocationId)
    {
        try
        {
            var allocation = await _memoryService.GetAllocationAsync(allocationId);
            
            if (allocation == null)
            {
                return NotFound(new { error = $"Allocation {allocationId} not found or inactive" });
            }

            return Ok(allocation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get allocation {allocationId}");
            return StatusCode(500, new { error = "Failed to retrieve allocation", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all active allocations, optionally filtered by device
    /// </summary>
    [HttpGet("allocations")]
    public async Task<IActionResult> GetAllocations([FromQuery] string? deviceId = null)
    {
        try
        {
            var allocations = await _memoryService.GetActiveAllocationsAsync(deviceId);
            return Ok(new { allocations, count = allocations.Count() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get allocations for device {deviceId}");
            return StatusCode(500, new { error = "Failed to retrieve allocations", details = ex.Message });
        }
    }
}
