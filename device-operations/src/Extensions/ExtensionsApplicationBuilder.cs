using DeviceOperations.Middleware;

namespace DeviceOperations.Extensions;

/// <summary>
/// Extension methods for application builder and middleware pipeline configuration
/// </summary>
public static class ExtensionsApplicationBuilder
{
    /// <summary>
    /// Configure the complete middleware pipeline
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static WebApplication ConfigureMiddlewarePipeline(this WebApplication app)
    {
        // Configure middleware pipeline in correct order
        
        // 1. Request/Response logging (first to capture everything)
        app.UseMiddleware<MiddlewareLogging>();
        
        // 2. Global error handling
        app.UseMiddleware<MiddlewareErrorHandling>();
        
        // 3. Authentication (if enabled)
        if (app.Configuration.GetValue<bool>("Authentication:Enabled", false))
        {
            app.UseMiddleware<MiddlewareAuthentication>();
        }
        
        // 4. Standard ASP.NET Core middleware
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthorization();
        
        return app;
    }

    /// <summary>
    /// Configure development-specific middleware
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static WebApplication ConfigureDevelopmentMiddleware(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            
            // Enable detailed error responses in development
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Environment"] = "Development";
                await next();
            });
        }
        
        return app;
    }

    /// <summary>
    /// Configure production-specific middleware
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static WebApplication ConfigureProductionMiddleware(this WebApplication app)
    {
        if (app.Environment.IsProduction())
        {
            // Add security headers
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
                await next();
            });
        }
        
        return app;
    }

    /// <summary>
    /// Configure health check endpoints (Removed - handled by ExtensionsHealthChecks)
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static WebApplication ConfigureHealthChecks(this WebApplication app)
    {
        // Health check endpoints are now configured in ExtensionsHealthChecks.UseApplicationHealthChecks()
        // This method is kept for backwards compatibility but no longer adds duplicate endpoints
        return app;
    }
}
