using Microsoft.AspNetCore.Mvc;
using DeviceOperations.Services.Testing;
using DeviceOperations.Models.Requests;

namespace DeviceOperations.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestingController : ControllerBase
{
    private readonly ILogger<TestingController> _logger;
    private readonly ITestingService _testingService;

    public TestingController(
        ILogger<TestingController> logger,
        ITestingService testingService)
    {
        _logger = logger;
        _testingService = testingService;
    }

    /// <summary>
    /// Comprehensive end-to-end system test
    /// </summary>
    [HttpPost("end-to-end")]
    public async Task<IActionResult> RunEndToEndTest()
    {
        try
        {
            _logger.LogInformation("Starting end-to-end test via Testing Service");
            var testResult = await _testingService.RunEndToEndTestAsync();
            
            return testResult.OverallSuccess ? Ok(testResult) : StatusCode(500, testResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "End-to-end test failed");
            return StatusCode(500, new { error = "End-to-end test failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Test device health integration with device-monitor
    /// </summary>
    [HttpGet("device-health")]
    public async Task<IActionResult> GetDeviceHealth()
    {
        try
        {
            var healthResult = await _testingService.RunSystemHealthTestAsync();
            return Ok(new { health = healthResult, timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device health information");
            return StatusCode(500, new { error = "Failed to retrieve device health", details = ex.Message });
        }
    }

    /// <summary>
    /// Performance benchmark test
    /// </summary>
    [HttpPost("performance-benchmark")]
    public async Task<IActionResult> RunPerformanceBenchmark()
    {
        try
        {
            _logger.LogInformation("Starting performance benchmark via Testing Service");
            var benchmarkResult = await _testingService.RunPerformanceBenchmarkAsync();
            
            return Ok(benchmarkResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Performance benchmark failed");
            return StatusCode(500, new { error = "Performance benchmark failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Comprehensive SDXL system test
    /// </summary>
    [HttpPost("sdxl-comprehensive")]
    public async Task<IActionResult> RunSDXLComprehensiveTest()
    {
        try
        {
            _logger.LogInformation("Starting comprehensive SDXL test via Testing Service");
            var testResult = await _testingService.RunSDXLComprehensiveTestAsync();
            
            return testResult.OverallSuccess ? Ok(testResult) : StatusCode(500, testResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SDXL comprehensive test failed");
            return StatusCode(500, new { error = "SDXL comprehensive test failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Test SDXL inference performance across different configurations
    /// </summary>
    [HttpPost("sdxl-inference-benchmark")]
    public async Task<IActionResult> RunSDXLInferenceBenchmark()
    {
        try
        {
            _logger.LogInformation("Starting SDXL inference benchmark via Testing Service");
            var benchmarkResult = await _testingService.RunSDXLInferenceBenchmarkAsync();
            
            return Ok(benchmarkResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SDXL inference benchmark failed");
            return StatusCode(500, new { error = "SDXL inference benchmark failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Validate SDXL model integrity and compatibility
    /// </summary>
    [HttpPost("sdxl-model-validation")]
    public async Task<IActionResult> ValidateSDXLModels([FromBody] SDXLModelValidationRequest request)
    {
        try
        {
            _logger.LogInformation("Starting SDXL model validation via Testing Service");
            var validationResult = await _testingService.ValidateSDXLModelsAsync(request);
            
            return validationResult.OverallValid ? Ok(validationResult) : StatusCode(500, validationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SDXL model validation failed");
            return StatusCode(500, new { error = "SDXL model validation failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Test SDXL training data quality and suitability
    /// </summary>
    [HttpPost("sdxl-data-quality-test")]
    public async Task<IActionResult> TestSDXLDataQuality([FromBody] SDXLDataQualityRequest request)
    {
        try
        {
            _logger.LogInformation("Starting SDXL data quality test via Testing Service");
            var qualityResult = await _testingService.TestSDXLDataQualityAsync(request);
            
            return Ok(qualityResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SDXL data quality test failed");
            return StatusCode(500, new { error = "SDXL data quality test failed", details = ex.Message });
        }
    }
}
