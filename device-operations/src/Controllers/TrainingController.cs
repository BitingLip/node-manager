using Microsoft.AspNetCore.Mvc;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Training;

namespace DeviceOperations.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrainingController : ControllerBase
{
    private readonly ILogger<TrainingController> _logger;
    private readonly ITrainingService _trainingService;

    public TrainingController(ILogger<TrainingController> logger, ITrainingService trainingService)
    {
        _logger = logger;
        _trainingService = trainingService;
    }

    /// <summary>
    /// Start a new training session
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartTraining([FromBody] StartTrainingRequest request)
    {
        try
        {
            var result = await _trainingService.StartTrainingAsync(request);
            
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
            _logger.LogError(ex, $"Failed to start training for model {request.ModelId}");
            return StatusCode(500, new { error = "Training start failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all training sessions
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetTrainingSessions([FromQuery] TrainingState? state = null)
    {
        try
        {
            var result = await _trainingService.GetTrainingSessionsAsync(state);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get training sessions");
            return StatusCode(500, new { error = "Failed to retrieve training sessions", details = ex.Message });
        }
    }

    /// <summary>
    /// Get training session status
    /// </summary>
    [HttpGet("sessions/{sessionId}/status")]
    public async Task<IActionResult> GetTrainingStatus(string sessionId)
    {
        try
        {
            var result = await _trainingService.GetTrainingStatusAsync(sessionId);
            
            if (result == null)
            {
                return NotFound(new { error = $"Training session {sessionId} not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get training status for session {sessionId}");
            return StatusCode(500, new { error = "Failed to retrieve training status", details = ex.Message });
        }
    }

    /// <summary>
    /// Get training session history
    /// </summary>
    [HttpGet("sessions/{sessionId}/history")]
    public async Task<IActionResult> GetTrainingHistory(string sessionId)
    {
        try
        {
            var result = await _trainingService.GetTrainingHistoryAsync(sessionId);
            
            if (result == null)
            {
                return NotFound(new { error = $"Training session {sessionId} not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get training history for session {sessionId}");
            return StatusCode(500, new { error = "Failed to retrieve training history", details = ex.Message });
        }
    }

    /// <summary>
    /// Get current training metrics
    /// </summary>
    [HttpGet("sessions/{sessionId}/metrics")]
    public async Task<IActionResult> GetCurrentMetrics(string sessionId)
    {
        try
        {
            var result = await _trainingService.GetCurrentMetricsAsync(sessionId);
            
            if (result == null)
            {
                return NotFound(new { error = $"Training session {sessionId} not found or no metrics available" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get metrics for session {sessionId}");
            return StatusCode(500, new { error = "Failed to retrieve training metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Pause a running training session
    /// </summary>
    [HttpPost("sessions/{sessionId}/pause")]
    public async Task<IActionResult> PauseTraining(string sessionId, [FromBody] PauseTrainingRequest? request = null)
    {
        try
        {
            var saveCheckpoint = request?.SaveCheckpoint ?? true;
            var result = await _trainingService.PauseTrainingAsync(sessionId, saveCheckpoint);
            
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
            _logger.LogError(ex, $"Failed to pause training session {sessionId}");
            return StatusCode(500, new { error = "Training pause failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Resume a paused training session
    /// </summary>
    [HttpPost("sessions/{sessionId}/resume")]
    public async Task<IActionResult> ResumeTraining(string sessionId, [FromBody] ResumeTrainingRequest? request = null)
    {
        try
        {
            var result = await _trainingService.ResumeTrainingAsync(sessionId, request?.CheckpointPath);
            
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
            _logger.LogError(ex, $"Failed to resume training session {sessionId}");
            return StatusCode(500, new { error = "Training resume failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Stop a training session
    /// </summary>
    [HttpPost("sessions/{sessionId}/stop")]
    public async Task<IActionResult> StopTraining(string sessionId, [FromBody] StopTrainingRequest? request = null)
    {
        try
        {
            var saveFinalModel = request?.SaveFinalModel ?? true;
            var finalModelPath = request?.FinalModelPath;
            
            var result = await _trainingService.StopTrainingAsync(sessionId, saveFinalModel, finalModelPath);
            
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
            _logger.LogError(ex, $"Failed to stop training session {sessionId}");
            return StatusCode(500, new { error = "Training stop failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete/cleanup a training session
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> CleanupSession(string sessionId)
    {
        try
        {
            var success = await _trainingService.CleanupSessionAsync(sessionId);
            
            if (success)
            {
                return Ok(new { message = $"Training session {sessionId} cleaned up successfully" });
            }
            else
            {
                return NotFound(new { error = $"Training session {sessionId} not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to cleanup training session {sessionId}");
            return StatusCode(500, new { error = "Session cleanup failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Start SDXL fine-tuning training
    /// </summary>
    [HttpPost("start-sdxl-fine-tune")]
    public async Task<IActionResult> StartSDXLFineTuning([FromBody] StartSDXLFineTuningRequest request)
    {
        try
        {
            _logger.LogInformation($"Starting SDXL fine-tuning for base model: {request.BaseModelPath}");

            // Validate SDXL training requirements
            var validation = ValidateSDXLTrainingRequirements(request);
            if (!validation.isValid)
            {
                return BadRequest(new { error = "SDXL training validation failed", details = validation.errors });
            }

            // Create enhanced training request
            var trainingRequest = new StartTrainingRequest
            {
                ModelId = $"sdxl_finetune_{request.TrainingName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                TrainingDataPath = request.TrainingDataPath,
                ValidationDataPath = request.ValidationDataPath,
                Configuration = new TrainingConfiguration
                {
                    Epochs = request.MaxEpochs,
                    BatchSize = request.BatchSize,
                    LearningRate = request.LearningRate,
                    ValidationSplit = request.ValidationSplit,
                    SaveCheckpointEvery = request.SaveEveryNEpochs
                },
                Hyperparameters = new Dictionary<string, object>
                {
                    ["training_type"] = "sdxl_fine_tune",
                    ["base_model_path"] = request.BaseModelPath,
                    ["refiner_model_path"] = request.RefinerModelPath ?? "",
                    ["vae_model_path"] = request.VaeModelPath ?? "",
                    ["training_technique"] = request.TrainingTechnique.ToString(),
                    ["optimizer"] = request.Optimizer,
                    ["scheduler"] = request.Scheduler,
                    ["gradient_accumulation_steps"] = request.GradientAccumulationSteps,
                    ["mixed_precision"] = request.MixedPrecision,
                    ["resolution"] = request.Resolution,
                    ["enable_text_encoder_training"] = request.EnableTextEncoderTraining,
                    ["enable_unet_training"] = request.EnableUnetTraining,
                    ["lora_rank"] = request.LoraRank,
                    ["lora_alpha"] = request.LoraAlpha
                }
            };

            var result = await _trainingService.StartTrainingAsync(trainingRequest);
            
            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    sessionId = result.SessionId,
                    message = "SDXL fine-tuning started successfully",
                    trainingName = request.TrainingName,
                    estimatedDuration = EstimateSDXLTrainingDuration(request),
                    checkStatusUrl = $"/api/training/sessions/{result.SessionId}/status"
                });
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to start SDXL fine-tuning for {request.TrainingName}");
            return StatusCode(500, new { error = "SDXL fine-tuning start failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get SDXL training capabilities and requirements
    /// </summary>
    [HttpGet("sdxl-capabilities")]
    public IActionResult GetSDXLTrainingCapabilities()
    {
        try
        {
            // Check available GPUs and their SDXL training readiness
            var capabilities = new
            {
                supportedTechniques = new[]
                {
                    "LoRA",
                    "DreamBooth",
                    "TextualInversion",
                    "FullFineTune"
                },
                minimumRequirements = new
                {
                    vramGB = 12,
                    diskSpaceGB = 50,
                    ramGB = 16
                },
                recommendedRequirements = new
                {
                    vramGB = 24,
                    diskSpaceGB = 100,
                    ramGB = 32
                },
                supportedResolutions = new[] { 512, 768, 1024, 1536 },
                supportedOptimizers = new[] { "AdamW", "AdamW8bit", "Lion", "DAdaptation" },
                supportedSchedulers = new[] { "cosine", "linear", "cosine_with_restarts", "polynomial" },
                estimatedTrainingTimes = new
                {
                    lora_100_images = "2-4 hours",
                    dreambooth_50_images = "4-8 hours",
                    full_finetune_1000_images = "12-24 hours"
                }
            };

            return Ok(capabilities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SDXL training capabilities");
            return StatusCode(500, new { error = "Failed to get capabilities", details = ex.Message });
        }
    }

    /// <summary>
    /// Get SDXL training templates and presets
    /// </summary>
    [HttpGet("sdxl-templates")]
    public IActionResult GetSDXLTrainingTemplates()
    {
        try
        {
            var templates = new
            {
                templates = new object[]
                {
                    new
                    {
                        name = "LoRA - Character Training",
                        description = "Efficient LoRA training for character consistency",
                        technique = "LoRA",
                        recommendedImages = 50,
                        settings = new
                        {
                            learningRate = 1e-4,
                            batchSize = 1,
                            maxEpochs = 20,
                            loraRank = 64,
                            loraAlpha = 32,
                            resolution = 1024
                        }
                    },
                    new
                    {
                        name = "DreamBooth - Style Transfer",
                        description = "DreamBooth training for artistic style transfer",
                        technique = "DreamBooth",
                        recommendedImages = 30,
                        settings = new
                        {
                            learningRate = 5e-6,
                            batchSize = 1,
                            maxEpochs = 100,
                            resolution = 1024,
                            enableTextEncoderTraining = true
                        }
                    },
                    new
                    {
                        name = "Full Fine-tune - Domain Adaptation",
                        description = "Full model fine-tuning for domain-specific imagery",
                        technique = "FullFineTune",
                        recommendedImages = 1000,
                        settings = new
                        {
                            learningRate = 1e-5,
                            batchSize = 2,
                            maxEpochs = 50,
                            resolution = 1024,
                            enableTextEncoderTraining = true,
                            enableUnetTraining = true
                        }
                    }
                }
            };

            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SDXL training templates");
            return StatusCode(500, new { error = "Failed to get templates", details = ex.Message });
        }
    }

    /// <summary>
    /// Validate training data for SDXL training
    /// </summary>
    [HttpPost("validate-sdxl-data")]
    public IActionResult ValidateSDXLTrainingData([FromBody] ValidateSDXLDataRequest request)
    {
        try
        {
            var validation = ValidateSDXLTrainingData(request.DataPath, request.TrainingTechnique);
            
            return Ok(new
            {
                isValid = validation.isValid,
                errors = validation.errors,
                warnings = validation.warnings,
                statistics = validation.statistics,
                recommendations = validation.recommendations
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to validate SDXL training data at {request.DataPath}");
            return StatusCode(500, new { error = "Data validation failed", details = ex.Message });
        }
    }

    // Helper methods for SDXL training support
    private (bool isValid, List<string> errors) ValidateSDXLTrainingRequirements(StartSDXLFineTuningRequest request)
    {
        var errors = new List<string>();

        // Check if base model exists
        if (!System.IO.File.Exists(request.BaseModelPath))
            errors.Add($"Base model not found: {request.BaseModelPath}");

        // Check refiner model if provided
        if (!string.IsNullOrEmpty(request.RefinerModelPath) && !System.IO.File.Exists(request.RefinerModelPath))
            errors.Add($"Refiner model not found: {request.RefinerModelPath}");

        // Check VAE model if provided
        if (!string.IsNullOrEmpty(request.VaeModelPath) && !System.IO.File.Exists(request.VaeModelPath))
            errors.Add($"VAE model not found: {request.VaeModelPath}");

        // Check training data
        if (!System.IO.Directory.Exists(request.TrainingDataPath))
            errors.Add($"Training data directory not found: {request.TrainingDataPath}");

        // Validate batch size and memory requirements
        if (request.BatchSize > 4)
            errors.Add("Batch size too large for SDXL training. Maximum recommended: 4");

        // Validate resolution
        var supportedResolutions = new[] { 512, 768, 1024, 1536, 2048 };
        if (!supportedResolutions.Contains(request.Resolution))
            errors.Add($"Unsupported resolution: {request.Resolution}. Supported: {string.Join(", ", supportedResolutions)}");

        return (errors.Count == 0, errors);
    }

    private string EstimateSDXLTrainingDuration(StartSDXLFineTuningRequest request)
    {
        // Rough estimation based on training parameters
        var baseHours = request.TrainingTechnique switch
        {
            SDXLTrainingTechnique.LoRA => 2,
            SDXLTrainingTechnique.DreamBooth => 6,
            SDXLTrainingTechnique.TextualInversion => 1,
            SDXLTrainingTechnique.FullFineTune => 12,
            _ => 4
        };

        var multiplier = request.BatchSize > 1 ? 0.7 : 1.0; // Larger batch sizes are more efficient
        var estimatedHours = (int)(baseHours * multiplier * (request.MaxEpochs / 20.0));

        return estimatedHours switch
        {
            < 2 => "1-2 hours",
            < 6 => "2-6 hours", 
            < 12 => "6-12 hours",
            < 24 => "12-24 hours",
            _ => "24+ hours"
        };
    }

    private (bool isValid, List<string> errors, List<string> warnings, object? statistics, List<string> recommendations) ValidateSDXLTrainingData(string dataPath, SDXLTrainingTechnique technique)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var recommendations = new List<string>();

        if (!System.IO.Directory.Exists(dataPath))
        {
            errors.Add($"Training data directory not found: {dataPath}");
            return (false, errors, warnings, null, recommendations);
        }

        var imageFiles = System.IO.Directory.GetFiles(dataPath, "*.*", SearchOption.AllDirectories)
            .Where(f => new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" }.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        var textFiles = System.IO.Directory.GetFiles(dataPath, "*.txt", SearchOption.AllDirectories).ToList();

        var statistics = new
        {
            imageCount = imageFiles.Count,
            textFileCount = textFiles.Count,
            averageFileSize = imageFiles.Any() ? imageFiles.Average(f => new FileInfo(f).Length) : 0,
            recommendedMinImages = technique switch
            {
                SDXLTrainingTechnique.LoRA => 20,
                SDXLTrainingTechnique.DreamBooth => 15,
                SDXLTrainingTechnique.TextualInversion => 10,
                SDXLTrainingTechnique.FullFineTune => 500,
                _ => 50
            }
        };

        // Validation logic
        if (imageFiles.Count < statistics.recommendedMinImages)
            warnings.Add($"Low image count. Recommended minimum: {statistics.recommendedMinImages}");

        if (textFiles.Count == 0)
            warnings.Add("No caption files found. Consider adding captions for better results.");

        if (imageFiles.Count > textFiles.Count * 2)
            warnings.Add("Many images without captions. Caption all images for best results.");

        return (errors.Count == 0, errors, warnings, statistics, recommendations);
    }
}
