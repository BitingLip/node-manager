using Microsoft.AspNetCore.Mvc;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Models.Common;
using DeviceOperations.Services.SDXL;
using System.ComponentModel.DataAnnotations;

namespace DeviceOperations.Controllers;

/// <summary>
/// Controller for enhanced SDXL generation with full control over all parameters
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EnhancedSDXLController : ControllerBase
{
    private readonly IEnhancedSDXLService _enhancedSDXLService;
    private readonly ILogger<EnhancedSDXLController> _logger;
    
    public EnhancedSDXLController(
        IEnhancedSDXLService enhancedSDXLService,
        ILogger<EnhancedSDXLController> logger)
    {
        _enhancedSDXLService = enhancedSDXLService;
        _logger = logger;
    }
    
    /// <summary>
    /// Generate images using enhanced SDXL pipeline with full control
    /// </summary>
    /// <param name="request">Enhanced SDXL generation request with all parameters</param>
    /// <returns>Generated images with detailed metrics</returns>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(EnhancedSDXLResponse), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GenerateSDXL([FromBody] EnhancedSDXLRequest request)
    {
        try
        {
            _logger.LogInformation("Generating enhanced SDXL with scheduler {Scheduler}", 
                request.Scheduler.Type);
            
            // Use the enhanced SDXL service for generation
            var result = await _enhancedSDXLService.GenerateEnhancedSDXLAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                _logger.LogError("Enhanced SDXL generation failed: {Message}", result.Message);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enhanced SDXL generation failed with exception");
            return StatusCode(500, new EnhancedSDXLResponse
            {
                Success = false,
                Error = "Internal server error",
                Message = ex.Message
            });
        }
    }
    
    /// <summary>
    /// Validate SDXL request and provide detailed feedback
    /// </summary>
    /// <param name="request">SDXL request to validate</param>
    /// <returns>Validation results with warnings and recommendations</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(PromptValidationResponse), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<IActionResult> ValidateRequest([FromBody] EnhancedSDXLRequest request)
    {
        try
        {
            var result = await _enhancedSDXLService.ValidateSDXLRequestAsync(request);
            
            if (result.Valid)
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
            _logger.LogError(ex, "Request validation failed");
            return StatusCode(500, new PromptValidationResponse
            {
                Valid = false,
                Error = $"Validation error: {ex.Message}"
            });
        }
    }
    
    /// <summary>
    /// Get available schedulers for SDXL
    /// </summary>
    /// <returns>List of supported schedulers</returns>
    [HttpGet("schedulers")]
    [ProducesResponseType(typeof(SDXLSchedulersResponse), 200)]
    public async Task<IActionResult> GetSchedulers()
    {
        try
        {
            var result = await _enhancedSDXLService.GetAvailableSchedulersAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedulers");
            return StatusCode(500, new { error = "Failed to get schedulers" });
        }
    }
    
    /// <summary>
    /// Get available ControlNet types
    /// </summary>
    /// <returns>List of supported ControlNet types</returns>
    [HttpGet("controlnet-types")]
    [ProducesResponseType(typeof(ControlNetTypesResponse), 200)]
    public async Task<IActionResult> GetControlNetTypes()
    {
        try
        {
            var result = await _enhancedSDXLService.GetControlNetTypesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ControlNet types");
            return StatusCode(500, new { error = "Failed to get ControlNet types" });
        }
    }
    
    /// <summary>
    /// Get system capabilities and GPU status
    /// </summary>
    /// <returns>System capabilities and available resources</returns>
    [HttpGet("capabilities")]
    [ProducesResponseType(typeof(SDXLCapabilitiesResponse), 200)]
    public async Task<IActionResult> GetCapabilities()
    {
        try
        {
            var result = await _enhancedSDXLService.GetSDXLCapabilitiesAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system capabilities");
            return StatusCode(500, new { error = "Failed to get system capabilities" });
        }
    }
    
    /// <summary>
    /// Get performance estimates for a request
    /// </summary>
    /// <param name="request">SDXL request to estimate</param>
    /// <returns>Performance estimates including time and memory usage</returns>
    [HttpPost("estimate")]
    [ProducesResponseType(typeof(SDXLPerformanceEstimate), 200)]
    public async Task<IActionResult> GetPerformanceEstimate([FromBody] EnhancedSDXLRequest request)
    {
        try
        {
            var result = await _enhancedSDXLService.GetPerformanceEstimateAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to estimate performance");
            return StatusCode(500, new { error = "Failed to estimate performance" });
        }
    }
}
