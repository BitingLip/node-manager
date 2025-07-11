using System.Text;

namespace DeviceOperations.Middleware;

/// <summary>
/// API authentication and authorization middleware
/// </summary>
public class MiddlewareAuthentication
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MiddlewareAuthentication> _logger;
    private readonly IConfiguration _configuration;

    public MiddlewareAuthentication(RequestDelegate next, ILogger<MiddlewareAuthentication> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for health check endpoints
        if (IsHealthCheckEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Skip authentication for Swagger/OpenAPI documentation endpoints
        if (IsSwaggerEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Skip authentication if disabled in configuration
        if (!_configuration.GetValue<bool>("Authentication:Enabled", false))
        {
            await _next(context);
            return;
        }

        try
        {
            if (!await IsAuthenticated(context))
            {
                await WriteUnauthorizedResponse(context);
                return;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            await WriteUnauthorizedResponse(context);
        }
    }

    private async Task<bool> IsAuthenticated(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            _logger.LogWarning("Missing Authorization header");
            return false;
        }

        // Handle Bearer token authentication
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            return await ValidateBearerToken(token);
        }

        // Handle API Key authentication
        if (authHeader.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = authHeader["ApiKey ".Length..].Trim();
            return await ValidateApiKey(apiKey);
        }

        // Handle Basic authentication
        if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var credentials = authHeader["Basic ".Length..].Trim();
            return await ValidateBasicAuth(credentials);
        }

        _logger.LogWarning("Unsupported authentication scheme in Authorization header");
        return false;
    }

    private Task<bool> ValidateBearerToken(string token)
    {
        // TODO: Implement proper JWT token validation
        // For now, accept any non-empty token in development
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(false);
        }

        // In development, accept the configured test token
        var testToken = _configuration.GetValue<string>("Authentication:TestToken");
        if (!string.IsNullOrEmpty(testToken) && token == testToken)
        {
            return Task.FromResult(true);
        }

        // TODO: Add JWT validation logic here
        _logger.LogWarning("Bearer token validation not implemented");
        return Task.FromResult(false);
    }

    private Task<bool> ValidateApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(false);
        }

        // Get valid API keys from configuration
        var validApiKeys = _configuration.GetSection("Authentication:ValidApiKeys").Get<string[]>() ?? Array.Empty<string>();
        
        return Task.FromResult(validApiKeys.Contains(apiKey, StringComparer.OrdinalIgnoreCase));
    }

    private Task<bool> ValidateBasicAuth(string credentials)
    {
        try
        {
            var credentialsBytes = Convert.FromBase64String(credentials);
            var credentialsString = Encoding.UTF8.GetString(credentialsBytes);
            var parts = credentialsString.Split(':', 2);

            if (parts.Length != 2)
            {
                return Task.FromResult(false);
            }

            var username = parts[0];
            var password = parts[1];

            // Get valid credentials from configuration
            var validUsername = _configuration.GetValue<string>("Authentication:BasicAuth:Username");
            var validPassword = _configuration.GetValue<string>("Authentication:BasicAuth:Password");

            var isValid = !string.IsNullOrEmpty(validUsername) && 
                   !string.IsNullOrEmpty(validPassword) && 
                   username == validUsername && 
                   password == validPassword;

            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode Basic authentication credentials");
            return Task.FromResult(false);
        }
    }

    private static bool IsHealthCheckEndpoint(string path)
    {
        return path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSwaggerEndpoint(string path)
    {
        return path.StartsWith("/api-docs", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/", StringComparison.OrdinalIgnoreCase) || // Allow root in development
               path.StartsWith("/swagger-ui", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteUnauthorizedResponse(HttpContext context)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            success = false,
            error = new
            {
                code = "UNAUTHORIZED",
                message = "Authentication required",
                statusCode = 401
            },
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
}
