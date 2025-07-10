using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DeviceOperations.Extensions;
using DeviceOperations.Models.Common;
using Microsoft.Extensions.Options;

namespace DeviceOperations.Services.Python;

/// <summary>
/// Python worker communication service implementation using STDIN/STDOUT
/// </summary>
public class PythonWorkerService : IPythonWorkerService, IDisposable
{
    private readonly ILogger<PythonWorkerService> _logger;
    private readonly PythonWorkerConfiguration _config;
    private readonly Dictionary<string, Process> _workerProcesses;
    private readonly SemaphoreSlim _processLock;
    private bool _disposed = false;

    public PythonWorkerService(ILogger<PythonWorkerService> logger, IOptions<PythonWorkerConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
        _workerProcesses = new Dictionary<string, Process>();
        _processLock = new SemaphoreSlim(1, 1);
    }

    public async Task<TResponse> ExecuteAsync<TRequest, TResponse>(
        string workerType, 
        string command, 
        TRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rawResponse = await ExecuteRawAsync(workerType, command, request, cancellationToken);
            
            var workerResponse = JsonSerializer.Deserialize<PythonWorkerResponse>(rawResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (workerResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize Python worker response");
            }

            if (!workerResponse.Success)
            {
                throw new InvalidOperationException($"Python worker error: {workerResponse.Error}");
            }

            if (workerResponse.Data == null)
            {
                return default(TResponse)!;
            }

            // Convert the data to the expected response type
            var dataJson = JsonSerializer.Serialize(workerResponse.Data);
            var response = JsonSerializer.Deserialize<TResponse>(dataJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return response!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Python worker command: {WorkerType}.{Command}", workerType, command);
            throw;
        }
    }

    public async Task<string> ExecuteRawAsync(
        string workerType, 
        string command, 
        object? request = null, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workerType))
            throw new ArgumentException("Worker type cannot be null or empty", nameof(workerType));

        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command cannot be null or empty", nameof(command));

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        
        try
        {
            var workerCommand = new PythonWorkerCommand
            {
                WorkerType = workerType,
                Command = command,
                Data = request,
                CorrelationId = correlationId
            };

            var commandJson = JsonSerializer.Serialize(workerCommand, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            _logger.LogDebug("Executing Python worker command: {Command} | Correlation: {CorrelationId}", 
                $"{workerType}.{command}", correlationId);

            var process = await GetOrCreateWorkerProcess(workerType);
            var response = await SendCommandToProcess(process, commandJson, cancellationToken);

            _logger.LogDebug("Python worker command completed: {Command} | Correlation: {CorrelationId}", 
                $"{workerType}.{command}", correlationId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Python worker command: {WorkerType}.{Command} | Correlation: {CorrelationId}", 
                workerType, command, correlationId);
            throw;
        }
    }

    public async Task<bool> IsWorkerAvailableAsync(string workerType)
    {
        try
        {
            await _processLock.WaitAsync();
            
            if (_workerProcesses.TryGetValue(workerType, out var process))
            {
                return process != null && !process.HasExited;
            }

            // Try to create a worker process to test availability
            var testProcess = await CreateWorkerProcess(workerType);
            if (testProcess != null)
            {
                _workerProcesses[workerType] = testProcess;
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check worker availability: {WorkerType}", workerType);
            return false;
        }
        finally
        {
            _processLock.Release();
        }
    }

    public async Task<Dictionary<string, object>> GetWorkerHealthAsync()
    {
        var health = new Dictionary<string, object>();
        
        try
        {
            await _processLock.WaitAsync();
            
            foreach (var kvp in _workerProcesses)
            {
                var workerType = kvp.Key;
                var process = kvp.Value;
                
                health[workerType] = new
                {
                    Available = process != null && !process.HasExited,
                    ProcessId = process?.Id,
                    StartTime = process?.StartTime,
                    MemoryUsage = process?.WorkingSet64
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting worker health status");
            health["error"] = ex.Message;
        }
        finally
        {
            _processLock.Release();
        }

        return health;
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Python worker service");
            
            // Test that we can create a basic worker process
            var testProcess = await CreateWorkerProcess("test");
            if (testProcess != null)
            {
                testProcess.Kill();
                testProcess.Dispose();
                _logger.LogInformation("Python worker service initialized successfully");
                return true;
            }

            _logger.LogWarning("Failed to initialize Python worker service");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Python worker service");
            return false;
        }
    }

    public async Task ShutdownAsync()
    {
        try
        {
            await _processLock.WaitAsync();
            
            _logger.LogInformation("Shutting down Python worker service");
            
            foreach (var kvp in _workerProcesses)
            {
                var workerType = kvp.Key;
                var process = kvp.Value;
                
                try
                {
                    if (process != null && !process.HasExited)
                    {
                        _logger.LogDebug("Terminating worker process: {WorkerType}", workerType);
                        process.Kill();
                        await process.WaitForExitAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error terminating worker process: {WorkerType}", workerType);
                }
                finally
                {
                    process?.Dispose();
                }
            }
            
            _workerProcesses.Clear();
            _logger.LogInformation("Python worker service shutdown complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Python worker service shutdown");
        }
        finally
        {
            _processLock.Release();
        }
    }

    private async Task<Process> GetOrCreateWorkerProcess(string workerType)
    {
        await _processLock.WaitAsync();
        try
        {
            if (_workerProcesses.TryGetValue(workerType, out var existingProcess) && 
                existingProcess != null && !existingProcess.HasExited)
            {
                return existingProcess;
            }

            var process = await CreateWorkerProcess(workerType);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to create worker process for type: {workerType}");
            }

            _workerProcesses[workerType] = process;
            return process;
        }
        finally
        {
            _processLock.Release();
        }
    }

    private async Task<Process?> CreateWorkerProcess(string workerType)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _config.PythonExecutable,
                Arguments = $"\"{_config.WorkerScriptPath}\" {workerType}",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(_config.WorkerScriptPath))
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start Python worker process for type: {WorkerType}", workerType);
                return null;
            }

            _logger.LogDebug("Started Python worker process: {WorkerType} (PID: {ProcessId})", workerType, process.Id);
            
            // Give the process a moment to initialize
            await Task.Delay(1000);
            
            if (process.HasExited)
            {
                _logger.LogError("Python worker process exited immediately: {WorkerType} (Exit Code: {ExitCode})", 
                    workerType, process.ExitCode);
                return null;
            }

            return process;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Python worker process: {WorkerType}", workerType);
            return null;
        }
    }

    private async Task<string> SendCommandToProcess(Process process, string command, CancellationToken cancellationToken)
    {
        try
        {
            // Send command to process STDIN
            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();

            // Read response from process STDOUT with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

            var response = await process.StandardOutput.ReadLineAsync();
            
            if (response == null)
            {
                throw new InvalidOperationException("No response received from Python worker");
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Python worker command timed out after {TimeoutSeconds} seconds", _config.TimeoutSeconds);
            throw new TimeoutException($"Python worker command timed out after {_config.TimeoutSeconds} seconds");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Python worker process");
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            ShutdownAsync().Wait();
            _processLock?.Dispose();
            _disposed = true;
        }
    }
}
