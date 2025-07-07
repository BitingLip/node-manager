using System.Diagnostics;
using System.Text.Json;
using DeviceOperations.Models.Common;

namespace DeviceOperations.Services.Inference;

public class PyTorchDirectMLService : IDisposable
{
    private readonly ILogger<PyTorchDirectMLService> _logger;
    private Process? _workerProcess;
    private StreamWriter? _workerInput;
    private StreamReader? _workerOutput;
    private bool _isInitialized = false;
    private readonly object _lock = new();

    public PyTorchDirectMLService(ILogger<PyTorchDirectMLService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized) return true;

        lock (_lock)
        {
            if (_isInitialized) return true;

            try
            {
                _logger.LogInformation("Starting PyTorch DirectML Worker...");

                // Path to Python worker - check multiple locations
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var workerPath = Path.Combine(baseDir, "src", "Workers", "run_worker.py");
                
                if (!File.Exists(workerPath))
                {
                    // Try legacy path
                    workerPath = Path.Combine(baseDir, "src", "Workers", "PytorchDirectMLWorker.py");
                    
                    if (!File.Exists(workerPath))
                    {
                        // Try relative path from current directory
                        workerPath = Path.Combine("src", "Workers", "run_worker.py");
                        
                        if (!File.Exists(workerPath))
                        {
                            _logger.LogError($"PyTorch worker not found. Checked paths:");
                            _logger.LogError($"  - {Path.Combine(baseDir, "src", "Workers", "run_worker.py")}");
                            _logger.LogError($"  - {Path.Combine(baseDir, "src", "Workers", "PytorchDirectMLWorker.py")}");
                            _logger.LogError($"  - {Path.Combine("src", "Workers", "run_worker.py")}");
                            return false;
                        }
                    }
                }

                // Start Python worker process
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
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
                    _logger.LogError("Failed to start PyTorch worker process");
                    return false;
                }

                _workerInput = _workerProcess.StandardInput;
                _workerOutput = _workerProcess.StandardOutput;

                // Redirect stderr for logging
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!_workerProcess.HasExited)
                        {
                            var errorLine = await _workerProcess.StandardError.ReadLineAsync();
                            if (!string.IsNullOrEmpty(errorLine))
                            {
                                _logger.LogInformation($"PyTorch Worker: {errorLine}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error reading worker stderr: {ex.Message}");
                    }
                });

                _isInitialized = true;
                _logger.LogInformation("PyTorch DirectML Worker started successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize PyTorch DirectML Worker");
                return false;
            }
        }
    }

    public async Task<PyTorchResponse<T>> SendCommandAsync<T>(object command, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized && !await InitializeAsync())
        {
            return new PyTorchResponse<T>
            {
                Success = false,
                Error = "PyTorch worker not initialized"
            };
        }

        if (_workerInput == null || _workerOutput == null || _workerProcess?.HasExited == true)
        {
            return new PyTorchResponse<T>
            {
                Success = false,
                Error = "PyTorch worker process not available"
            };
        }

        try
        {
            // Send command to Python worker
            var commandJson = JsonSerializer.Serialize(command);
            await _workerInput.WriteLineAsync(commandJson);
            await _workerInput.FlushAsync();

            // Read response from Python worker
            var responseJson = await _workerOutput.ReadLineAsync();
            if (string.IsNullOrEmpty(responseJson))
            {
                return new PyTorchResponse<T>
                {
                    Success = false,
                    Error = "No response from PyTorch worker"
                };
            }

            // Parse response
            var response = JsonSerializer.Deserialize<PyTorchResponse<T>>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return response ?? new PyTorchResponse<T>
            {
                Success = false,
                Error = "Failed to parse response from PyTorch worker"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to communicate with PyTorch worker");
            return new PyTorchResponse<T>
            {
                Success = false,
                Error = $"Communication error: {ex.Message}"
            };
        }
    }

    public async Task<PyTorchResponse<InitializeResult>> InitializeDirectMLAsync(int deviceId = 1, bool enableMultiGpu = false)
    {
        var command = new
        {
            action = "initialize",
            device_id = deviceId,
            enable_multi_gpu = enableMultiGpu
        };

        return await SendCommandAsync<InitializeResult>(command);
    }

    public async Task<PyTorchResponse<LoadModelResult>> LoadModelAsync(string modelPath, string modelType = "SDXL")
    {
        var command = new
        {
            action = "load_model",
            model_path = modelPath,
            model_type = modelType
        };

        return await SendCommandAsync<LoadModelResult>(command);
    }

    public async Task<PyTorchResponse<GenerateImageResult>> GenerateImageAsync(GenerateImageRequest request)
    {
        var command = new
        {
            action = "generate_image",
            parameters = new
            {
                prompt = request.Prompt,
                negative_prompt = request.NegativePrompt ?? "",
                width = request.Width,
                height = request.Height,
                steps = request.Steps,
                guidance_scale = request.GuidanceScale,
                seed = request.Seed
            }
        };

        return await SendCommandAsync<GenerateImageResult>(command);
    }

    public async Task<PyTorchResponse<ModelInfoResult>> GetModelInfoAsync()
    {
        var command = new { action = "get_model_info" };
        return await SendCommandAsync<ModelInfoResult>(command);
    }

    public async Task<PyTorchResponse<UnloadModelResult>> UnloadModelAsync()
    {
        var command = new { action = "unload_model" };
        return await SendCommandAsync<UnloadModelResult>(command);
    }

    public async Task<PyTorchResponse<CleanupMemoryResult>> CleanupMemoryAsync()
    {
        var command = new { action = "cleanup_memory" };
        return await SendCommandAsync<CleanupMemoryResult>(command);
    }

    public void Dispose()
    {
        try
        {
            if (_workerProcess != null && !_workerProcess.HasExited)
            {
                _workerInput?.Close();
                _workerOutput?.Close();
                
                if (!_workerProcess.WaitForExit(5000))
                {
                    _workerProcess.Kill();
                }
                
                _workerProcess.Dispose();
            }

            _workerInput?.Dispose();
            _workerOutput?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing PyTorch DirectML Service");
        }

        _isInitialized = false;
        _logger.LogInformation("PyTorch DirectML Service disposed");
    }
}

// Response models for PyTorch worker communication
public class PyTorchResponse<T>
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public T? Data { get; set; }
}

public class InitializeResult
{
    public string? Device { get; set; }
    public int DeviceId { get; set; }
    public int DeviceCount { get; set; }
}

public class LoadModelResult
{
    public string? ModelId { get; set; }
    public double LoadTimeSeconds { get; set; }
    public long ModelSizeBytes { get; set; }
    public string? Device { get; set; }
}

public class GenerateImageResult
{
    public string? OutputPath { get; set; }
    public double GenerationTimeSeconds { get; set; }
    public int Seed { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class ModelInfoResult
{
    public string? ModelId { get; set; }
    public Dictionary<string, object>? ModelInfo { get; set; }
}

public class UnloadModelResult
{
    public string? Message { get; set; }
}

public class CleanupMemoryResult
{
    public string? Message { get; set; }
}

public class GenerateImageRequest
{
    public string Prompt { get; set; } = "";
    public string? NegativePrompt { get; set; }
    public int Width { get; set; } = 512;
    public int Height { get; set; } = 512;
    public int Steps { get; set; } = 20;
    public double GuidanceScale { get; set; } = 7.5;
    public int Seed { get; set; } = -1;
}
