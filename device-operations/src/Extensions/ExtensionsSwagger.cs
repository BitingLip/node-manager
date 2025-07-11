using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Linq;

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

            // Track used schema IDs to avoid duplicates
            var usedSchemaIds = new HashSet<string>();
            
            // Custom schema ID selector to handle generic types and avoid conflicts
            options.CustomSchemaIds(type => 
            {
                try
                {
                    string schemaId;
                    
                    // Handle generic types like ApiResponse<T>
                    if (type.IsGenericType)
                    {
                        var genericTypeName = GetCleanTypeName(type);
                        var genericArgs = type.GetGenericArguments();
                        
                        if (genericArgs.Length == 1)
                        {
                            var innerType = genericArgs[0];
                            var innerTypeName = GetCleanTypeName(innerType);
                            
                            // Special handling for common generic wrappers
                            if (genericTypeName == "ApiResponse")
                            {
                                // For ApiResponse<T>, use "Api" prefix to make it clear it's the API wrapper
                                schemaId = $"Api{innerTypeName}";
                            }
                            else
                            {
                                schemaId = $"{genericTypeName}Of{innerTypeName}";
                            }
                        }
                        else if (genericArgs.Length > 1)
                        {
                            var argNames = string.Join("And", genericArgs.Select(GetCleanTypeName));
                            schemaId = $"{genericTypeName}Of{argNames}";
                        }
                        else
                        {
                            schemaId = GetCleanTypeName(type);
                        }
                    }
                    else
                    {
                        schemaId = GetCleanTypeName(type);
                    }
                    
                    // Handle duplicates by adding a counter
                    var originalId = schemaId;
                    var counter = 1;
                    while (usedSchemaIds.Contains(schemaId))
                    {
                        schemaId = $"{originalId}_{counter}";
                        counter++;
                    }
                    
                    usedSchemaIds.Add(schemaId);
                    return schemaId;
                }
                catch (Exception)
                {
                    // Fallback to safe default if any error occurs
                    var fallbackId = type.Name.Replace('`', '_').Replace('+', '_');
                    var counter = 1;
                    var originalFallback = fallbackId;
                    while (usedSchemaIds.Contains(fallbackId))
                    {
                        fallbackId = $"{originalFallback}_{counter}";
                        counter++;
                    }
                    usedSchemaIds.Add(fallbackId);
                    return fallbackId;
                }
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

    /// <summary>
    /// Gets a clean, readable name for a type, removing namespace prefixes and version info
    /// </summary>
    private static string GetCleanTypeName(Type type)
    {
        try
        {
            var name = type.Name;
            
            // Handle nested types (replace + with _)
            name = name.Replace('+', '_');
            
            // Remove generic arity indicators (like `1)
            if (name.Contains('`'))
            {
                name = name.Split('`')[0];
            }
            
            // For DeviceOperations types, create clean names by removing common prefixes
            if (type.FullName?.StartsWith("DeviceOperations.") == true)
            {
                // Remove common namespace patterns to create cleaner names
                var fullName = type.FullName;
                
                // Extract the meaningful part after DeviceOperations.Models.
                if (fullName.Contains("DeviceOperations.Models."))
                {
                    var afterModels = fullName.Substring(fullName.IndexOf("DeviceOperations.Models.") + "DeviceOperations.Models.".Length);
                    
                    // Keep track of the source namespace for potential duplicates
                    string sourceNamespace = "";
                    if (afterModels.StartsWith("Responses."))
                        sourceNamespace = "Response";
                    else if (afterModels.StartsWith("Requests."))
                        sourceNamespace = "Request";
                    else if (afterModels.StartsWith("Common."))
                        sourceNamespace = "Common";
                    
                    // Remove common prefixes and clean up the name
                    var cleanName = afterModels
                        .Replace("Responses.", "")
                        .Replace("Requests.", "")
                        .Replace("Common.", "")
                        .Replace(".", "_")
                        .Replace("+", "_");
                    
                    // Remove generic arity if present
                    if (cleanName.Contains('`'))
                    {
                        cleanName = cleanName.Split('`')[0];
                    }
                    
                    // For common types that might exist in multiple namespaces, add source context
                    if (IsLikelyDuplicateType(cleanName) && !string.IsNullOrEmpty(sourceNamespace))
                    {
                        return $"{cleanName}{sourceNamespace}";
                    }
                    
                    return cleanName;
                }
                
                // For other DeviceOperations types, just use the simple class name
                return name;
            }
            
            // For System types, use simple name
            if (type.FullName?.StartsWith("System.") == true)
            {
                return name;
            }
            
            return name;
        }
        catch (Exception)
        {
            // Fallback to safe default if any error occurs
            return type.Name.Replace('`', '_').Replace('+', '_');
        }
    }
    
    /// <summary>
    /// Determines if a type name is likely to be duplicated across namespaces
    /// </summary>
    private static bool IsLikelyDuplicateType(string typeName)
    {
        // With enum consolidation, we should have fewer duplicates
        // Keep this list minimal for any remaining edge cases
        var commonDuplicateTypes = new[]
        {
            "ComponentType",
            "DeviceType", 
            "ModelType",
            "InferenceType",
            "MemoryAllocationType",
            "SessionPriority",
            "PrecisionMode"
        };
        
        return commonDuplicateTypes.Contains(typeName);
    }
}
