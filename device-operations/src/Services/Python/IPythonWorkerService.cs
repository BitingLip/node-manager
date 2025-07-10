using DeviceOperations.Models.Common;

namespace DeviceOperations.Services.Python;

/// <summary>
/// Interface for Python worker communication service
/// </summary>
public interface IPythonWorkerService
{
    /// <summary>
    /// Execute a command on a Python worker and return the result
    /// </summary>
    /// <typeparam name="TRequest">Type of the request object</typeparam>
    /// <typeparam name="TResponse">Type of the response object</typeparam>
    /// <param name="workerType">Type of worker to execute on</param>
    /// <param name="command">Command to execute</param>
    /// <param name="request">Request data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Worker response</returns>
    Task<TResponse> ExecuteAsync<TRequest, TResponse>(
        string workerType, 
        string command, 
        TRequest request, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a command on a Python worker without typed response
    /// </summary>
    /// <param name="workerType">Type of worker to execute on</param>
    /// <param name="command">Command to execute</param>
    /// <param name="request">Request data as object</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Raw response string</returns>
    Task<string> ExecuteRawAsync(
        string workerType, 
        string command, 
        object? request = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a specific worker type is available
    /// </summary>
    /// <param name="workerType">Type of worker to check</param>
    /// <returns>True if worker is available</returns>
    Task<bool> IsWorkerAvailableAsync(string workerType);

    /// <summary>
    /// Get health status of all Python workers
    /// </summary>
    /// <returns>Health status information</returns>
    Task<Dictionary<string, object>> GetWorkerHealthAsync();

    /// <summary>
    /// Initialize Python worker environment
    /// </summary>
    /// <returns>True if initialization succeeded</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Shutdown all Python worker processes
    /// </summary>
    Task ShutdownAsync();
}

/// <summary>
/// Python worker command message format
/// </summary>
public class PythonWorkerCommand
{
    /// <summary>
    /// Type of worker to execute on
    /// </summary>
    public string WorkerType { get; set; } = string.Empty;

    /// <summary>
    /// Command to execute
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Request data
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Request correlation ID for tracking
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Request timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Python worker response message format
/// </summary>
public class PythonWorkerResponse
{
    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Response data
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Error information if operation failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Request correlation ID for tracking
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Response timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }
}

/// <summary>
/// Common Python worker commands
/// </summary>
public static class PythonWorkerCommands
{
    // Device commands
    public const string DEVICE_DISCOVER = "discover";
    public const string DEVICE_STATUS = "status";
    public const string DEVICE_MONITOR = "monitor";
    
    // Memory commands
    public const string MEMORY_ALLOCATE = "allocate";
    public const string MEMORY_STATUS = "status";
    public const string MEMORY_TRANSFER = "transfer";
    
    // Model commands
    public const string MODEL_LOAD = "load";
    public const string MODEL_UNLOAD = "unload";
    public const string MODEL_STATUS = "status";
    
    // Inference commands
    public const string INFERENCE_EXECUTE = "execute";
    public const string INFERENCE_VALIDATE = "validate";
    public const string INFERENCE_CAPABILITIES = "capabilities";
    
    // General commands
    public const string HEALTH_CHECK = "health";
    public const string INITIALIZE = "initialize";
    public const string SHUTDOWN = "shutdown";
}
