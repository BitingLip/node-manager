namespace DeviceOperations.Models.Common;

/// <summary>
/// Standardized error response model with detailed error information
/// </summary>
public class ErrorDetails
{
    /// <summary>
    /// Unique error code for programmatic handling
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Request correlation ID for tracking
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Request path where the error occurred
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Additional error details or context
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }

    /// <summary>
    /// Timestamp when the error occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Stack trace information (only in development)
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Create a new error details instance
    /// </summary>
    /// <param name="code">Error code</param>
    /// <param name="message">Error message</param>
    /// <param name="statusCode">HTTP status code</param>
    /// <returns>New ErrorDetails instance</returns>
    public static ErrorDetails Create(string code, string message, int statusCode = 500)
    {
        return new ErrorDetails
        {
            Code = code,
            Message = message,
            StatusCode = statusCode,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Add additional detail to the error
    /// </summary>
    /// <param name="key">Detail key</param>
    /// <param name="value">Detail value</param>
    /// <returns>This instance for chaining</returns>
    public ErrorDetails AddDetail(string key, object value)
    {
        Details ??= new Dictionary<string, object>();
        Details[key] = value;
        return this;
    }

    /// <summary>
    /// Set the correlation ID
    /// </summary>
    /// <param name="correlationId">Correlation ID</param>
    /// <returns>This instance for chaining</returns>
    public ErrorDetails WithCorrelationId(string correlationId)
    {
        CorrelationId = correlationId;
        return this;
    }

    /// <summary>
    /// Set the request path
    /// </summary>
    /// <param name="path">Request path</param>
    /// <returns>This instance for chaining</returns>
    public ErrorDetails WithPath(string path)
    {
        Path = path;
        return this;
    }
}

/// <summary>
/// Common error codes used throughout the application
/// </summary>
public static class ErrorCodes
{
    // General errors
    public const string INTERNAL_ERROR = "INTERNAL_ERROR";
    public const string INVALID_ARGUMENT = "INVALID_ARGUMENT";
    public const string MISSING_PARAMETER = "MISSING_PARAMETER";
    public const string UNAUTHORIZED = "UNAUTHORIZED";
    public const string NOT_FOUND = "NOT_FOUND";
    public const string NOT_SUPPORTED = "NOT_SUPPORTED";
    public const string INVALID_OPERATION = "INVALID_OPERATION";
    public const string TIMEOUT = "TIMEOUT";
    
    // Device errors
    public const string DEVICE_NOT_FOUND = "DEVICE_NOT_FOUND";
    public const string DEVICE_UNAVAILABLE = "DEVICE_UNAVAILABLE";
    public const string DEVICE_INITIALIZATION_FAILED = "DEVICE_INITIALIZATION_FAILED";
    
    // Memory errors
    public const string MEMORY_ALLOCATION_FAILED = "MEMORY_ALLOCATION_FAILED";
    public const string INSUFFICIENT_MEMORY = "INSUFFICIENT_MEMORY";
    public const string MEMORY_TRANSFER_FAILED = "MEMORY_TRANSFER_FAILED";
    
    // Model errors
    public const string MODEL_NOT_FOUND = "MODEL_NOT_FOUND";
    public const string MODEL_LOADING_FAILED = "MODEL_LOADING_FAILED";
    public const string MODEL_INCOMPATIBLE = "MODEL_INCOMPATIBLE";
    
    // Inference errors
    public const string INFERENCE_FAILED = "INFERENCE_FAILED";
    public const string INFERENCE_VALIDATION_FAILED = "INFERENCE_VALIDATION_FAILED";
    public const string INFERENCE_TIMEOUT = "INFERENCE_TIMEOUT";
    
    // Processing errors
    public const string WORKFLOW_EXECUTION_FAILED = "WORKFLOW_EXECUTION_FAILED";
    public const string BATCH_PROCESSING_FAILED = "BATCH_PROCESSING_FAILED";
    public const string SESSION_NOT_FOUND = "SESSION_NOT_FOUND";
    
    // Python worker errors
    public const string PYTHON_WORKER_ERROR = "PYTHON_WORKER_ERROR";
    public const string PYTHON_WORKER_TIMEOUT = "PYTHON_WORKER_TIMEOUT";
    public const string PYTHON_WORKER_COMMUNICATION_FAILED = "PYTHON_WORKER_COMMUNICATION_FAILED";
}
