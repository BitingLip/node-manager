using DeviceOperations.Models.Requests;
using DeviceOperations.Services.Inference;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DeviceOperations.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InferenceController : ControllerBase
{
    private readonly IInferenceService _inferenceService;
    private readonly ILogger<InferenceController> _logger;

    public InferenceController(IInferenceService inferenceService, ILogger<InferenceController> logger)
    {
        _inferenceService = inferenceService;
        _logger = logger;
    }

    /// <summary>
    /// Load an ONNX model to a specific device
    /// </summary>
    [HttpPost("load-model")]
    public async Task<IActionResult> LoadModel([FromBody] LoadModelRequest request)
    {
        try
        {
            var result = await _inferenceService.LoadModelAsync(request);
            
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
            _logger.LogError(ex, $"Failed to load model from {request.ModelPath}");
            return StatusCode(500, new { error = "Model loading failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all loaded models
    /// </summary>
    [HttpGet("models")]
    public async Task<IActionResult> GetModels()
    {
        try
        {
            var result = await _inferenceService.GetLoadedModelsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get loaded models");
            return StatusCode(500, new { error = "Failed to retrieve models", details = ex.Message });
        }
    }

    /// <summary>
    /// Get information about a specific loaded model
    /// </summary>
    [HttpGet("models/{modelId}")]
    public async Task<IActionResult> GetModel(string modelId)
    {
        try
        {
            var model = await _inferenceService.GetModelInfoAsync(modelId);
            
            if (model == null)
            {
                return NotFound(new { error = $"Model {modelId} not found" });
            }

            return Ok(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get model {modelId}");
            return StatusCode(500, new { error = "Failed to retrieve model", details = ex.Message });
        }
    }

    /// <summary>
    /// Run inference on a loaded model
    /// </summary>
    [HttpPost("run")]
    public async Task<IActionResult> RunInference([FromBody] InferenceRequest request)
    {
        try
        {
            var result = await _inferenceService.RunInferenceAsync(request);
            
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
            _logger.LogError(ex, $"Failed to run inference on model {request.ModelId}");
            return StatusCode(500, new { error = "Inference failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the status of an inference session
    /// </summary>
    [HttpGet("status/{sessionId}")]
    public async Task<IActionResult> GetInferenceStatus(string sessionId)
    {
        try
        {
            var status = await _inferenceService.GetInferenceStatusAsync(sessionId);
            
            if (status == null)
            {
                return NotFound(new { error = $"Inference session {sessionId} not found" });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get inference status for session {sessionId}");
            return StatusCode(500, new { error = "Failed to retrieve inference status", details = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a running inference session
    /// </summary>
    [HttpPost("cancel/{sessionId}")]
    public async Task<IActionResult> CancelInference(string sessionId)
    {
        try
        {
            var success = await _inferenceService.CancelInferenceAsync(sessionId);
            
            if (success)
            {
                return Ok(new { message = $"Inference session {sessionId} cancelled successfully" });
            }
            else
            {
                return NotFound(new { error = $"Inference session {sessionId} not found or cannot be cancelled" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to cancel inference session {sessionId}");
            return StatusCode(500, new { error = "Failed to cancel inference", details = ex.Message });
        }
    }

    /// <summary>
    /// Unload a model from memory
    /// </summary>
    [HttpDelete("unload-model/{modelId}")]
    public async Task<IActionResult> UnloadModel(string modelId)
    {
        try
        {
            var success = await _inferenceService.UnloadModelAsync(modelId);
            
            if (success)
            {
                return Ok(new { message = $"Model {modelId} unloaded successfully" });
            }
            else
            {
                return NotFound(new { error = $"Model {modelId} not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to unload model {modelId}");
            return StatusCode(500, new { error = "Model unloading failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all active inference sessions
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetActiveSessions([FromQuery] string? modelId = null)
    {
        try
        {
            var sessions = await _inferenceService.GetActiveSessionsAsync(modelId);
            return Ok(new { sessions, count = sessions.Count() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get active sessions for model {modelId}");
            return StatusCode(500, new { error = "Failed to retrieve sessions", details = ex.Message });
        }
    }

    /// <summary>
    /// Test PyTorch DirectML image generation end-to-end (Legacy - consider using Enhanced SDXL API)
    /// </summary>
    [HttpPost("test-pytorch-directml")]
    public async Task<IActionResult> TestPyTorchDirectML([FromBody] TestGenerationRequest request)
    {
        try
        {
            _logger.LogInformation($"Test PyTorch DirectML generation requested for model {request.ModelPath}");

            // Load model
            var loadRequest = new LoadModelRequest
            {
                ModelPath = request.ModelPath,
                DeviceId = request.DeviceId ?? "1",
                ModelType = ModelType.SDXL,
                ModelName = "Test-CyberRealistic-Pony",
                EnableMultiGpu = request.EnableMultiGpu
            };

            var loadResponse = await _inferenceService.LoadModelAsync(loadRequest);
            if (!loadResponse.Success)
            {
                return BadRequest(new { error = $"Failed to load model: {loadResponse.Message}" });
            }

            // Run inference
            var inferenceRequest = new InferenceRequest
            {
                ModelId = loadResponse.ModelId!,
                Inputs = new Dictionary<string, object>
                {
                    ["prompt"] = request.Prompt ?? "a beautiful sunset over mountains, peaceful landscape, warm colors",
                    ["negative_prompt"] = request.NegativePrompt ?? "blurry, low quality, distorted",
                    ["width"] = request.Width ?? 512,
                    ["height"] = request.Height ?? 512,
                    ["steps"] = request.Steps ?? 20,
                    ["guidance_scale"] = request.GuidanceScale ?? 7.5,
                    ["seed"] = request.Seed ?? -1
                }
            };

            var inferenceResponse = await _inferenceService.RunInferenceAsync(inferenceRequest);
            if (!inferenceResponse.Success)
            {
                return BadRequest(new { error = $"Failed to start inference: {inferenceResponse.Message}" });
            }

            return Ok(new 
            { 
                message = "PyTorch DirectML test generation started successfully",
                modelId = loadResponse.ModelId,
                sessionId = inferenceResponse.SessionId,
                checkStatusUrl = $"/api/inference/status/{inferenceResponse.SessionId}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PyTorch DirectML test generation failed");
            return StatusCode(500, new { error = $"Test generation failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Generate images using structured prompt submission (Schema-Compliant)
    /// </summary>
    [HttpPost("generate-structured")]
    public async Task<IActionResult> GenerateStructured([FromBody] StructuredPromptRequest request)
    {
        try
        {
            _logger.LogInformation($"Structured generation requested for model {request.ModelName}");

            // Validate schema compliance
            if (string.IsNullOrEmpty(request.Prompt))
            {
                return BadRequest(new { error = "Prompt is required" });
            }

            if (string.IsNullOrEmpty(request.ModelName))
            {
                return BadRequest(new { error = "Model name is required" });
            }

            // Load model if needed
            var loadRequest = new LoadModelRequest
            {
                ModelPath = request.ModelPath ?? $"./models/{request.ModelName}",
                DeviceId = request.Metadata?.DeviceId ?? "gpu_0",
                ModelType = ModelType.SDXL,
                ModelName = request.ModelName,
                EnableMultiGpu = false
            };

            var loadResponse = await _inferenceService.LoadModelAsync(loadRequest);
            if (!loadResponse.Success)
            {
                return BadRequest(new { error = $"Failed to load model: {loadResponse.Message}" });
            }

            // Convert structured request to inference inputs (schema-compliant)
            var inputs = new Dictionary<string, object>
            {
                ["prompt"] = request.Prompt,
                ["negative_prompt"] = request.NegativePrompt ?? "",
                ["model_name"] = request.ModelName,
                ["scheduler"] = request.Scheduler ?? "DPMSolverMultistepScheduler",
                
                // Hyperparameters
                ["num_inference_steps"] = request.Hyperparameters?.NumInferenceSteps ?? 20,
                ["guidance_scale"] = request.Hyperparameters?.GuidanceScale ?? 7.5,
                ["strength"] = request.Hyperparameters?.Strength ?? 0.8,
                ["seed"] = request.Hyperparameters?.Seed ?? -1,
                
                // Dimensions
                ["width"] = request.Dimensions?.Width ?? 1024,
                ["height"] = request.Dimensions?.Height ?? 1024,
                
                // Conditioning
                ["clip_skip"] = request.Conditioning?.ClipSkip ?? 0,
                
                // Precision
                ["dtype"] = request.Precision?.Dtype ?? "float16",
                ["vae_dtype"] = request.Precision?.VaeDtype ?? "float32",
                ["cpu_offload"] = request.Precision?.CpuOffload ?? false,
                ["attention_slicing"] = request.Precision?.AttentionSlicing ?? true,
                ["vae_slicing"] = request.Precision?.VaeSlicing ?? true,
                
                // Output
                ["format"] = request.Output?.Format ?? "png",
                ["quality"] = request.Output?.Quality ?? 95,
                ["save_path"] = request.Output?.SavePath ?? string.Empty,
                
                // Batch
                ["batch_size"] = request.Batch?.Size ?? 1,
                ["parallel"] = request.Batch?.Parallel ?? false,
                
                // Advanced features
                ["lora_enabled"] = request.Lora?.Enabled ?? false,
                ["controlnet_enabled"] = request.Controlnet?.Enabled ?? false
            };

            // Add LoRA configuration if enabled
            if (request.Lora?.Enabled == true && request.Lora.Models?.Any() == true)
            {
                inputs["lora_models"] = request.Lora.Models.Select(l => new 
                { 
                    name = l.Name, 
                    weight = l.Weight 
                }).ToArray();
            }

            // Add ControlNet configuration if enabled
            if (request.Controlnet?.Enabled == true && !string.IsNullOrEmpty(request.Controlnet.Type))
            {
                inputs["controlnet_type"] = request.Controlnet.Type;
                inputs["controlnet_conditioning_scale"] = request.Controlnet.ConditioningScale;
                inputs["control_image"] = request.Controlnet.ControlImage ?? string.Empty;
            }

            // Add metadata
            if (request.Metadata != null)
            {
                inputs["request_id"] = request.Metadata.RequestId ?? string.Empty;
                inputs["priority"] = request.Metadata.Priority ?? "normal";
                inputs["timeout"] = request.Metadata.Timeout;
                inputs["tags"] = request.Metadata.Tags ?? new List<string>();
            }

            // Run inference
            var inferenceRequest = new InferenceRequest
            {
                ModelId = loadResponse.ModelId!,
                Inputs = inputs,
                TimeoutSeconds = request.Metadata?.Timeout ?? 300
            };

            var inferenceResponse = await _inferenceService.RunInferenceAsync(inferenceRequest);
            if (!inferenceResponse.Success)
            {
                return BadRequest(new { error = $"Failed to start inference: {inferenceResponse.Message}" });
            }

            return Ok(new 
            { 
                message = "Structured generation started successfully",
                modelId = loadResponse.ModelId,
                sessionId = inferenceResponse.SessionId,
                checkStatusUrl = $"/api/inference/status/{inferenceResponse.SessionId}",
                schemaCompliant = true,
                requestId = request.Metadata?.RequestId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Structured generation failed");
            return StatusCode(500, new { error = $"Structured generation failed: {ex.Message}" });
        }
    }
}

public class TestGenerationRequest
{
    public string ModelPath { get; set; } = "";
    public string? DeviceId { get; set; }
    public bool EnableMultiGpu { get; set; } = false;
    public string? Prompt { get; set; }
    public string? NegativePrompt { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Steps { get; set; }
    public double? GuidanceScale { get; set; }
    public int? Seed { get; set; }
}
