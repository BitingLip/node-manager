namespace DeviceOperations.Models.Common;

/// <summary>
/// Standardized API response wrapper for all endpoints
/// </summary>
/// <typeparam name="T">The type of data being returned</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Convenience property to check if operation was successful (same as Success)
    /// </summary>
    public bool IsSuccess => Success;

    /// <summary>
    /// The response data (null if operation failed)
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Optional success message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Error details (null if operation succeeded)
    /// </summary>
    public ErrorDetails? Error { get; set; }

    /// <summary>
    /// Timestamp when the response was generated
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Request correlation ID for tracking
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Create a successful response
    /// </summary>
    /// <param name="data">The response data</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>Successful API response</returns>
    public static ApiResponse<T> CreateSuccess(T data, string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Error = null,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create an error response
    /// </summary>
    /// <param name="error">Error details</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>Error API response</returns>
    public static ApiResponse<T> CreateError(ErrorDetails error, string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default(T),
            Error = error,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Create an error response with message
    /// </summary>
    /// <param name="code">Error code</param>
    /// <param name="message">Error message</param>
    /// <param name="statusCode">HTTP status code</param>
    /// <param name="correlationId">Optional correlation ID</param>
    /// <returns>Error API response</returns>
    public static ApiResponse<T> CreateError(string code, string message, int statusCode = 500, string? correlationId = null)
    {
        var error = new ErrorDetails
        {
            Code = code,
            Message = message,
            StatusCode = statusCode
        };

        return CreateError(error, correlationId);
    }
}
