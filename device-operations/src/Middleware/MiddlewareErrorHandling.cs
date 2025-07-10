using System.Net;
using System.Text.Json;
using DeviceOperations.Models.Common;

namespace DeviceOperations.Middleware;

/// <summary>
/// Global exception handling middleware for consistent error responses
/// </summary>
public class MiddlewareErrorHandling
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MiddlewareErrorHandling> _logger;

    public MiddlewareErrorHandling(RequestDelegate next, ILogger<MiddlewareErrorHandling> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during request processing");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var errorDetails = exception switch
        {
            ArgumentNullException nullEx => new ErrorDetails
            {
                Code = "MISSING_PARAMETER",
                Message = nullEx.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },
            ArgumentException argEx => new ErrorDetails
            {
                Code = "INVALID_ARGUMENT",
                Message = argEx.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },
            UnauthorizedAccessException => new ErrorDetails
            {
                Code = "UNAUTHORIZED",
                Message = "Unauthorized access",
                StatusCode = (int)HttpStatusCode.Unauthorized
            },
            NotSupportedException notSupportedEx => new ErrorDetails
            {
                Code = "NOT_SUPPORTED",
                Message = notSupportedEx.Message,
                StatusCode = (int)HttpStatusCode.NotImplemented
            },
            InvalidOperationException invalidOpEx => new ErrorDetails
            {
                Code = "INVALID_OPERATION",
                Message = invalidOpEx.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },
            TimeoutException timeoutEx => new ErrorDetails
            {
                Code = "TIMEOUT",
                Message = timeoutEx.Message,
                StatusCode = (int)HttpStatusCode.RequestTimeout
            },
            _ => new ErrorDetails
            {
                Code = "INTERNAL_ERROR",
                Message = "An internal server error occurred",
                StatusCode = (int)HttpStatusCode.InternalServerError
            }
        };

        // Add correlation ID if available
        if (context.Items.TryGetValue("CorrelationId", out var correlationId))
        {
            errorDetails.CorrelationId = correlationId?.ToString();
        }

        // Add request path for context
        errorDetails.Path = context.Request.Path;

        response.StatusCode = errorDetails.StatusCode;

        var jsonResponse = JsonSerializer.Serialize(new ApiResponse<object>
        {
            Success = false,
            Data = null,
            Error = errorDetails,
            Timestamp = DateTime.UtcNow
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await response.WriteAsync(jsonResponse);
    }
}
