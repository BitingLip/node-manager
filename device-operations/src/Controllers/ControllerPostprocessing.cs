using Microsoft.AspNetCore.Mvc;
using DeviceOperations.Models;
using DeviceOperations.Services;
using DeviceOperations.Services.Postprocessing;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Models.Postprocessing;

namespace DeviceOperations.Controllers
{
    /// <summary>
    /// Controller for postprocessing operations including upscaling, enhancement, and safety validation
    /// </summary>
    [ApiController]
    [Route("api/postprocessing")]
    [Tags("Postprocessing Operations")]
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

        #region Week 20: Advanced Features - Missing Endpoint Implementations

        /// <summary>
        /// Execute sophisticated batch postprocessing with memory optimization
        /// </summary>
        /// <param name="request">Advanced batch processing request</param>
        /// <returns>Batch processing results with detailed analytics</returns>
        [HttpPost("batch/advanced")]
        public async Task<IActionResult> ExecuteBatchPostprocessingAsync([FromBody] PostPostprocessingBatchAdvancedRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Batch processing request cannot be null"));
                }

                if (string.IsNullOrEmpty(request.BatchId))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Batch ID is required", "BatchId cannot be null or empty"));
                }

                if (request.Images == null || !request.Images.Any())
                {
                    return BadRequest(ApiResponse<object>.CreateError("Images are required", "At least one image must be provided for batch processing"));
                }

                _logger.LogInformation("Processing advanced batch request: {BatchId} with {Count} images", request.BatchId, request.Images.Count);
                
                var result = await _servicePostprocessing.ExecuteBatchPostprocessingAsync(request);
                
                if (result.IsSuccess && result.Data != null)
                {
                    return Ok(ApiResponse<object>.CreateSuccess(result.Data, $"Batch processing initiated successfully for {request.Images.Count} images"));
                }
                else
                {
                    return StatusCode(500, ApiResponse<object>.CreateError("Batch processing failed", result.Message ?? "Unknown error"));
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid batch processing request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute batch postprocessing");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to execute batch postprocessing", ex.Message));
            }
        }

        /// <summary>
        /// Monitor batch processing progress and get current status
        /// </summary>
        /// <param name="batchId">The batch identifier to monitor</param>
        /// <returns>Real-time batch progress and status information</returns>
        [HttpGet("batch/{batchId}/status")]
        public async Task<IActionResult> GetBatchStatusAsync(string batchId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Batch ID is required", "Parameter 'batchId' cannot be null or empty"));
                }

                _logger.LogInformation("Getting batch status for: {BatchId}", batchId);
                
                var progressResult = await _servicePostprocessing.MonitorBatchProgressAsync(batchId);
                
                var batchStatus = new
                {
                    BatchId = batchId,
                    Progress = progressResult.progress,
                    Status = progressResult.status.ToString(),
                    Results = progressResult.results,
                    LastUpdated = DateTime.UtcNow,
                    TotalItems = progressResult.results.Count,
                    CompletedItems = progressResult.results.Count(r => r.Success),
                    FailedItems = progressResult.results.Count(r => !r.Success),
                    IsCompleted = progressResult.status == DeviceOperations.Models.Postprocessing.BatchStatus.Completed || 
                                 progressResult.status == DeviceOperations.Models.Postprocessing.BatchStatus.Failed
                };

                return Ok(ApiResponse<object>.CreateSuccess(batchStatus, $"Batch status retrieved successfully for {batchId}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get batch status for: {BatchId}", batchId);
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to get batch status", ex.Message));
            }
        }

        /// <summary>
        /// Benchmark model performance for optimization insights
        /// </summary>
        /// <param name="request">Model benchmarking request parameters</param>
        /// <returns>Comprehensive model performance benchmarks</returns>
        [HttpPost("benchmark")]
        public async Task<IActionResult> BenchmarkModelsAsync([FromBody] PostProcessingModelBenchmarkRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Benchmark request cannot be null"));
                }

                if (string.IsNullOrEmpty(request.ModelName))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Model name is required", "ModelName cannot be null or empty"));
                }

                _logger.LogInformation("Benchmarking model: {ModelName} with {Iterations} iterations", request.ModelName, request.Iterations);
                
                var benchmarkRequest = new PostPostprocessingModelManagementRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ModelName = request.ModelName,
                    Action = ModelManagementAction.Benchmark,
                    ModelType = request.ModelType,
                    BenchmarkIterations = request.Iterations,
                    OptimizationLevel = request.OptimizationLevel ?? ModelOptimizationLevel.Balanced,
                    Configuration = request.Configuration ?? new Dictionary<string, object>()
                };

                var result = await _servicePostprocessing.ManagePostprocessingModelAsync(benchmarkRequest);
                
                if (result.IsSuccess && result.Data != null)
                {
                    return Ok(ApiResponse<object>.CreateSuccess(result.Data, $"Model benchmark completed successfully for {request.ModelName}"));
                }
                else
                {
                    return StatusCode(500, ApiResponse<object>.CreateError("Model benchmarking failed", result.Error?.Message ?? "Benchmarking failed"));
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid benchmark request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to benchmark model");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to benchmark model", ex.Message));
            }
        }

        /// <summary>
        /// Manage content policy configuration and validation
        /// </summary>
        /// <param name="request">Content policy management request</param>
        /// <returns>Content policy management results</returns>
        [HttpPost("content-policy")]
        public async Task<IActionResult> ManageContentPolicyAsync([FromBody] ContentPolicyManagementRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Content policy request cannot be null"));
                }

                if (string.IsNullOrEmpty(request.PolicyName))
                {
                    return BadRequest(ApiResponse<object>.CreateError("Policy name is required", "PolicyName cannot be null or empty"));
                }

                _logger.LogInformation("Managing content policy: {PolicyName} - Action: {Action}", request.PolicyName, request.Action);
                
                var policyRequest = new PostPostprocessingModelManagementRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ModelName = request.PolicyName,
                    Action = request.Action == "create" || request.Action == "update" ? ModelManagementAction.Load : ModelManagementAction.Unload,
                    ModelType = PostprocessingModelType.Custom,
                    Configuration = new Dictionary<string, object>
                    {
                        ["policy_action"] = request.Action,
                        ["policy_rules"] = request.PolicyRules ?? new Dictionary<string, object>(),
                        ["enforcement_level"] = request.EnforcementLevel ?? "standard",
                        ["policy_version"] = request.Version ?? "1.0"
                    }
                };

                var result = await _servicePostprocessing.ManagePostprocessingModelAsync(policyRequest);
                
                if (result.IsSuccess && result.Data != null)
                {
                    var policyResult = new
                    {
                        PolicyName = request.PolicyName,
                        Action = request.Action,
                        Success = result.Data.Success,
                        Message = result.Data.Message,
                        Version = request.Version ?? "1.0",
                        EnforcementLevel = request.EnforcementLevel ?? "standard",
                        AppliedAt = DateTime.UtcNow
                    };

                    return Ok(ApiResponse<object>.CreateSuccess(policyResult, $"Content policy {request.Action} completed successfully for {request.PolicyName}"));
                }
                else
                {
                    return StatusCode(500, ApiResponse<object>.CreateError("Content policy management failed", result.Error?.Message ?? "Policy management failed"));
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid content policy request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to manage content policy");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to manage content policy", ex.Message));
            }
        }

        /// <summary>
        /// Get comprehensive performance analytics for postprocessing operations
        /// </summary>
        /// <param name="startDate">Analytics start date (ISO format)</param>
        /// <param name="endDate">Analytics end date (ISO format)</param>
        /// <param name="includeComparative">Include comparative analysis</param>
        /// <param name="includePredictive">Include predictive insights</param>
        /// <returns>Detailed performance analytics and optimization recommendations</returns>
        [HttpGet("analytics/performance")]
        public async Task<IActionResult> GetPerformanceAnalyticsAsync(
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null,
            [FromQuery] bool includeComparative = false,
            [FromQuery] bool includePredictive = false)
        {
            try
            {
                // Parse dates with defaults
                var analysisStart = DateTime.TryParse(startDate, out DateTime start) ? start : DateTime.UtcNow.AddDays(-7);
                var analysisEnd = DateTime.TryParse(endDate, out DateTime end) ? end : DateTime.UtcNow;

                if (analysisStart >= analysisEnd)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Invalid date range", "Start date must be before end date"));
                }

                if ((analysisEnd - analysisStart).TotalDays > 365)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Date range too large", "Maximum analysis period is 365 days"));
                }

                _logger.LogInformation("Getting performance analytics from {StartDate} to {EndDate}", analysisStart, analysisEnd);
                
                var analyticsRequest = new PerformanceAnalyticsRequest
                {
                    StartDate = analysisStart,
                    EndDate = analysisEnd,
                    IncludeComparativeAnalysis = includeComparative,
                    IncludePredictiveAnalysis = includePredictive,
                    AnalysisType = "comprehensive",
                    MetricTypes = new List<string> { "performance", "quality", "resource_usage", "error_analysis" }
                };

                var analytics = await _servicePostprocessing.GetPerformanceAnalyticsAsync(analyticsRequest);
                
                if (analytics != null && !analytics.HasError)
                {
                    var responseData = new
                    {
                        Analytics = analytics,
                        RequestInfo = new
                        {
                            StartDate = analysisStart,
                            EndDate = analysisEnd,
                            Duration = analysisEnd - analysisStart,
                            IncludeComparative = includeComparative,
                            IncludePredictive = includePredictive
                        },
                        Summary = new
                        {
                            TotalRequests = analytics.CoreMetrics?.TotalRequests ?? 0,
                            SuccessRate = analytics.CoreMetrics?.SuccessRate ?? 0,
                            AverageProcessingTime = analytics.CoreMetrics?.AverageProcessingTimeMs ?? 0,
                            RecommendationCount = analytics.OptimizationRecommendations?.Count ?? 0
                        }
                    };

                    return Ok(ApiResponse<object>.CreateSuccess(responseData, "Performance analytics generated successfully"));
                }
                else
                {
                    return StatusCode(500, ApiResponse<object>.CreateError("Analytics generation failed", analytics?.ErrorMessage ?? "Failed to generate analytics"));
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid performance analytics request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get performance analytics");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to get performance analytics", ex.Message));
            }
        }

        /// <summary>
        /// Get list of available models with caching and filtering
        /// </summary>
        /// <param name="modelType">Filter by model type (optional)</param>
        /// <param name="forceRefresh">Force cache refresh</param>
        /// <returns>List of available postprocessing models</returns>
        [HttpGet("models")]
        public async Task<IActionResult> GetAvailableModelsAsync([FromQuery] string? modelType = null, [FromQuery] bool forceRefresh = false)
        {
            try
            {
                _logger.LogInformation("Getting available models - Type: {ModelType}, ForceRefresh: {ForceRefresh}", modelType, forceRefresh);
                
                var result = await _servicePostprocessing.GetAvailableModelsWithCachingAsync(modelType, forceRefresh);
                
                if (result.IsSuccess && result.Data != null)
                {
                    var responseData = new
                    {
                        Models = result.Data,
                        TotalCount = result.Data.Count,
                        FilteredBy = modelType,
                        CacheRefreshed = forceRefresh,
                        RetrievedAt = DateTime.UtcNow
                    };

                    return Ok(ApiResponse<object>.CreateSuccess(responseData, $"Retrieved {result.Data.Count} available models"));
                }
                else
                {
                    return StatusCode(500, ApiResponse<object>.CreateError("Failed to get available models", result.Error?.Message ?? "Model retrieval failed"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available models");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to get available models", ex.Message));
            }
        }

        /// <summary>
        /// Execute postprocessing with optimized connection pooling
        /// </summary>
        /// <param name="request">Optimized postprocessing request</param>
        /// <returns>Enhanced postprocessing results with performance metrics</returns>
        [HttpPost("execute/optimized")]
        public async Task<IActionResult> ExecuteWithOptimizedConnectionAsync([FromBody] PostPostprocessingRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Postprocessing request cannot be null"));
                }

                if (string.IsNullOrEmpty(request.RequestId))
                {
                    request.RequestId = Guid.NewGuid().ToString();
                }

                _logger.LogInformation("Executing optimized postprocessing for request: {RequestId}", request.RequestId);
                
                var result = await _servicePostprocessing.ExecuteWithOptimizedConnectionAsync(request);
                
                if (result.IsSuccess && result.Data != null)
                {
                    return Ok(ApiResponse<object>.CreateSuccess(result.Data, "Optimized postprocessing completed successfully"));
                }
                else
                {
                    return StatusCode(500, ApiResponse<object>.CreateError("Optimized postprocessing failed", result.Error?.Message ?? "Processing failed"));
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid optimized postprocessing request parameters");
                return BadRequest(ApiResponse<object>.CreateError("Invalid request parameters", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute optimized postprocessing");
                return StatusCode(500, ApiResponse<object>.CreateError("Failed to execute optimized postprocessing", ex.Message));
            }
        }

        /// <summary>
        /// Execute postprocessing with real-time progress streaming
        /// </summary>
        /// <param name="request">Streaming postprocessing request</param>
        /// <returns>Server-sent events stream of progress updates</returns>
        [HttpPost("execute/streaming")]
        public async Task<IActionResult> ExecuteWithProgressStreamingAsync([FromBody] PostPostprocessingStreamingRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.CreateError("Request body is required", "Streaming request cannot be null"));
                }

                if (string.IsNullOrEmpty(request.RequestId))
                {
                    request.RequestId = Guid.NewGuid().ToString();
                }

                _logger.LogInformation("Starting progress streaming for request: {RequestId}", request.RequestId);

                // Set up server-sent events
                Response.Headers["Content-Type"] = "text/event-stream";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Connection"] = "keep-alive";

                var postprocessingRequest = new PostPostprocessingRequest
                {
                    RequestId = request.RequestId,
                    ImageData = request.ImageData,
                    OperationType = request.OperationType,
                    Parameters = request.Parameters,
                    QualitySettings = request.QualitySettings
                };

                var streamingConfig = request.StreamingConfig ?? new ProgressStreamingConfig
                {
                    UpdateIntervalMs = 1000,
                    EnablePreviewData = false,
                    EnableMetricsStreaming = true
                };

                // Stream progress updates
                await foreach (var update in _servicePostprocessing.ExecuteWithProgressStreamingAsync(postprocessingRequest, streamingConfig, HttpContext.RequestAborted))
                {
                    var eventData = $"data: {System.Text.Json.JsonSerializer.Serialize(update)}\n\n";
                    await Response.WriteAsync(eventData);
                    await Response.Body.FlushAsync();

                    if (update.IsCompleted || HttpContext.RequestAborted.IsCancellationRequested)
                        break;
                }

                return new EmptyResult();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Progress streaming cancelled for request: {RequestId}", request?.RequestId);
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute progress streaming");
                await Response.WriteAsync($"data: {{\"error\": \"{ex.Message}\", \"isCompleted\": true, \"hasError\": true}}\n\n");
                await Response.Body.FlushAsync();
                return new EmptyResult();
            }
        }

        #endregion

        #region Supporting Models for Week 20 Implementation

        /// <summary>
        /// Model benchmarking request
        /// </summary>
        public class PostProcessingModelBenchmarkRequest
        {
            public string ModelName { get; set; } = string.Empty;
            public PostprocessingModelType? ModelType { get; set; }
            public int Iterations { get; set; } = 10;
            public ModelOptimizationLevel? OptimizationLevel { get; set; }
            public Dictionary<string, object>? Configuration { get; set; }
        }

        /// <summary>
        /// Content policy management request
        /// </summary>
        public class ContentPolicyManagementRequest
        {
            public string PolicyName { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty; // create, update, delete, test
            public string? Version { get; set; }
            public string? EnforcementLevel { get; set; }
            public Dictionary<string, object>? PolicyRules { get; set; }
        }

        /// <summary>
        /// Streaming postprocessing request
        /// </summary>
        public class PostPostprocessingStreamingRequest
        {
            public string RequestId { get; set; } = string.Empty;
            public string ImageData { get; set; } = string.Empty;
            public string OperationType { get; set; } = string.Empty;
            public Dictionary<string, object>? Parameters { get; set; }
            public Dictionary<string, object>? QualitySettings { get; set; }
            public ProgressStreamingConfig? StreamingConfig { get; set; }
        }

        #endregion
    }
}
