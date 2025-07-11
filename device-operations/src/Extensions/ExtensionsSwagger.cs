using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace DeviceOperations.Extensions;

/// <summary>
/// Extension methods for configuring Swagger/OpenAPI documentation
/// </summary>
public static class ExtensionsSwagger
{
    /// <summary>
    /// Adds comprehensive Swagger/OpenAPI documentation services
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            // API Information
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "Device Operations API",
                Description = "Comprehensive ML Device Operations and Inference Management API"
            });

            // Include XML comments for documentation (but don't fail if missing)
            try
            {
                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            }
            catch (Exception)
            {
                // Ignore XML comment errors during startup
            }

            // Custom schema ID selector to avoid conflicts
            options.CustomSchemaIds(type => 
            {
                var fullName = type.FullName?.Replace('+', '.');
                if (fullName?.Contains("DeviceOperations.Models.") == true)
                {
                    // Use full namespace for DeviceOperations models to avoid conflicts
                    return fullName.Replace("DeviceOperations.Models.", "").Replace(".", "_");
                }
                return type.Name;
            });

            // Simplified configuration to avoid conflicts
            options.DocInclusionPredicate((docName, description) => true);
        });

        return services;
    }

    /// <summary>
    /// Configures Swagger UI with enhanced options
    /// </summary>
    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger(options =>
        {
            options.SerializeAsV2 = false;
            options.RouteTemplate = "api-docs/{documentName}/swagger.json";
        });

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/api-docs/v1/swagger.json", "Device Operations API v1");
            
            if (env.IsDevelopment())
            {
                options.RoutePrefix = string.Empty; // Serve Swagger UI at root in development
            }
            else
            {
                options.RoutePrefix = "api-docs"; // Serve at /api-docs in production
            }

            // Basic UI Configuration
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
            options.DefaultModelExpandDepth(2);
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
        });

        return app;
    }

    private static string GetControllerName(Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription api)
    {
        return api.ActionDescriptor.RouteValues["controller"] ?? "Unknown";
    }
}
