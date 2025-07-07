using System.Diagnostics;
using System.Text.Json;
using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Inference;

/// <summary>
/// Direct communication service for ML workers using stdin/stdout.
/// No HTTP server needed - pure execution endpoints.
/// </summary>
public class DirectMLWorkerService : IDisposable
{
    private readonly ILogger<DirectMLWorkerService> _logger;
    private Process? _workerProcess;
    private StreamWriter? _workerInput;
    private StreamReader? _workerOutput;
    private bool _isInitialized = false;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _requestSemaphore = new(1, 1);

    public DirectMLWorkerService(ILogger<DirectMLWorkerService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initialize the direct ML worker process.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized) return true;

        await _requestSemaphore.WaitAsync();
        try
        {
            if (_isInitialized) return true;

            _logger.LogInformation("Starting Direct ML Worker...");

            // Find Python worker script
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var workerPath = Path.Combine(baseDir, "src", "Workers", "ml_worker_direct.py");
            
            if (!File.Exists(workerPath))
            {
                // Try relative path from current directory
                workerPath = Path.Combine("src", "Workers", "ml_worker_direct.py");
                
                if (!File.Exists(workerPath))
                {
                    _logger.LogError($"Direct ML worker not found. Checked paths:");
                    _logger.LogError($"  - {Path.Combine(baseDir, "src", "Workers", "ml_worker_direct.py")}");
                    _logger.LogError($"  - {Path.Combine("src", "Workers", "ml_worker_direct.py")}");
                    return false;
                }
            }

            // Start Python worker process
            var startInfo = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = workerPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(workerPath)
            };

            _workerProcess = Process.Start(startInfo);
            if (_workerProcess == null)
            {
                _logger.LogError("Failed to start Direct ML worker process");
                return false;
            }

            _workerInput = _workerProcess.StandardInput;
            _workerOutput = _workerProcess.StandardOutput;

            // Monitor stderr for logging
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_workerProcess.HasExited)
                    {
                        var errorLine = await _workerProcess.StandardError.ReadLineAsync();
                        if (!string.IsNullOrEmpty(errorLine))
                        {
                            _logger.LogInformation($"ML Worker: {errorLine}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error reading worker stderr: {ex.Message}");
                }
            });

            // Wait for ready signal
            var readyResponse = await ReadResponseAsync();
            if (readyResponse?.GetProperty("success").GetBoolean() == true)
            {
                _isInitialized = true;
                _logger.LogInformation("Direct ML Worker started successfully");
                return true;
            }
            else
            {
                _logger.LogError("Direct ML Worker failed to initialize");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Direct ML Worker");
            return false;
        }
        finally
        {
            _requestSemaphore.Release();
        }
    }

    /// <summary>
    /// Process an inference request directly.
    /// </summary>
    public async Task<InferenceResponse> ProcessInferenceAsync(EnhancedSDXLRequest request, CancellationToken cancellationToken = default)
    {
        if (!await InitializeAsync())
        {
            return new InferenceResponse
            {
                Success = false,
                Error = "Direct ML worker not initialized"
            };
        }

        await _requestSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Build request for Python worker
            var pythonRequest = new
            {
                type = "inference",
                request_id = Guid.NewGuid().ToString(),
                data = new
                {
                    prompt = request.Prompt,
                    negative_prompt = request.NegativePrompt ?? "",
                    model_name = request.ModelName ?? "stabilityai/stable-diffusion-xl-base-1.0",
                    hyperparameters = new
                    {
                        num_inference_steps = request.Steps ?? 30,
                        guidance_scale = request.GuidanceScale ?? 7.5,
                        seed = request.Seed ?? -1
                    },
                    dimensions = new
                    {
                        width = request.Width ?? 1024,
                        height = request.Height ?? 1024
                    },
                    batch = new
                    {
                        size = request.BatchSize ?? 1
                    },
                    scheduler = request.Scheduler ?? "DPMSolverMultistepScheduler",
                    precision = new
                    {
                        dtype = "float16"
                    }
                }
            };

            // Send request
            var requestJson = JsonSerializer.Serialize(pythonRequest);
            await _workerInput!.WriteLineAsync(requestJson);
            await _workerInput.FlushAsync();

            _logger.LogInformation($"Sent inference request: {request.Prompt?[..Math.Min(50, request.Prompt.Length)]}...");

            // Read response
            var response = await ReadResponseAsync();
            if (response == null)
            {
                return new InferenceResponse
                {
                    Success = false,
                    Error = "No response from Direct ML worker"
                };
            }

            // Parse response
            var success = response.GetProperty("success").GetBoolean();
            if (success)
            {
                var data = response.GetProperty("data");
                var images = new List<string>();
                
                if (data.TryGetProperty("images", out var imagesProperty))
                {
                    foreach (var imageElement in imagesProperty.EnumerateArray())
                    {
                        images.Add(imageElement.GetString() ?? "");
                    }
                }

                return new InferenceResponse
                {
                    Success = true,
                    Images = images,
                    ProcessingTimeSeconds = data.TryGetProperty("processing_time", out var timeProperty) 
                        ? timeProperty.GetDouble() : 0,
                    ModelUsed = pythonRequest.data.model_name,
                    SeedUsed = data.TryGetProperty("seed_used", out var seedProperty) 
                        ? seedProperty.GetInt32() : request.Seed ?? -1
                };
            }
            else
            {
                var error = response.GetProperty("error").GetString();
                return new InferenceResponse
                {
                    Success = false,
                    Error = error ?? "Unknown error from ML worker"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing inference request");
            return new InferenceResponse
            {
                Success = false,
                Error = $"Processing error: {ex.Message}"
            };
        }
        finally
        {
            _requestSemaphore.Release();
        }
    }

    /// <summary>
    /// Check worker health status.
    /// </summary>
    public async Task<bool> CheckHealthAsync()
    {
        if (!_isInitialized)
            return false;

        try
        {
            var healthRequest = new { type = "health" };
            var requestJson = JsonSerializer.Serialize(healthRequest);
            
            await _workerInput!.WriteLineAsync(requestJson);
            await _workerInput.FlushAsync();

            var response = await ReadResponseAsync();
            return response?.GetProperty("success").GetBoolean() == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Read a JSON response from the worker.
    /// </summary>
    private async Task<JsonElement?> ReadResponseAsync()
    {
        try
        {
            var responseJson = await _workerOutput!.ReadLineAsync();
            if (string.IsNullOrEmpty(responseJson))
                return null;

            return JsonSerializer.Deserialize<JsonElement>(responseJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading response from worker");
            return null;
        }
    }

    /// <summary>
    /// Get worker status information.
    /// </summary>
    public async Task<object> GetStatusAsync()
    {
        var isHealthy = await CheckHealthAsync();
        
        return new
        {
            IsInitialized = _isInitialized,
            IsHealthy = isHealthy,
            ProcessId = _workerProcess?.Id,
            HasExited = _workerProcess?.HasExited ?? true,
            CommunicationType = "Direct stdin/stdout",
            WorkerType = "DirectMLWorker"
        };
    }

    public void Dispose()
    {
        try
        {
            _requestSemaphore?.Dispose();
            
            if (_workerInput != null)
            {
                _workerInput.Close();
                _workerInput.Dispose();
            }

            if (_workerOutput != null)
            {
                _workerOutput.Close();
                _workerOutput.Dispose();
            }

            if (_workerProcess != null && !_workerProcess.HasExited)
            {
                _workerProcess.Kill();
                _workerProcess.WaitForExit(5000);
            }

            _workerProcess?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing DirectMLWorkerService");
        }
    }
}