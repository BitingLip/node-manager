using System.Diagnostics;

namespace DeviceOperations.Middleware;

/// <summary>
/// Request/response logging middleware with correlation IDs and performance tracking
/// </summary>
public class MiddlewareLogging
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MiddlewareLogging> _logger;

    public MiddlewareLogging(RequestDelegate next, ILogger<MiddlewareLogging> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GenerateCorrelationId();
        context.Items["CorrelationId"] = correlationId;
        
        // Add correlation ID to response headers
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Log incoming request
            LogRequest(context, correlationId);

            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            // Log outgoing response
            LogResponse(context, correlationId, stopwatch.ElapsedMilliseconds);
        }
    }

    private void LogRequest(HttpContext context, string correlationId)
    {
        var request = context.Request;
        
        _logger.LogInformation(
            "Incoming Request: {Method} {Path} | Correlation: {CorrelationId} | ContentType: {ContentType} | UserAgent: {UserAgent}",
            request.Method,
            request.Path + request.QueryString,
            correlationId,
            request.ContentType ?? "N/A",
            request.Headers.UserAgent.ToString()
        );

        // Log query parameters if present
        if (request.QueryString.HasValue)
        {
            _logger.LogDebug(
                "Query Parameters: {QueryString} | Correlation: {CorrelationId}",
                request.QueryString.Value,
                correlationId
            );
        }
    }

    private void LogResponse(HttpContext context, string correlationId, long elapsedMs)
    {
        var response = context.Response;
        
        var logLevel = response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
        
        _logger.Log(logLevel,
            "Outgoing Response: {StatusCode} | Duration: {ElapsedMs}ms | Correlation: {CorrelationId} | ContentType: {ContentType}",
            response.StatusCode,
            elapsedMs,
            correlationId,
            response.ContentType ?? "N/A"
        );

        // Log performance warnings for slow requests
        if (elapsedMs > 5000) // 5 seconds
        {
            _logger.LogWarning(
                "Slow Request Detected: {Method} {Path} took {ElapsedMs}ms | Correlation: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                elapsedMs,
                correlationId
            );
        }
    }

    private static string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..16]; // Use first 16 characters for brevity
    }
}
