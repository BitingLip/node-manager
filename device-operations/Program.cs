using DeviceOperations.Extensions;
using DeviceOperations.Middleware;
using Serilog;

namespace DeviceOperations;

public class Program
{
    public static void Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(GetConfiguration())
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/device-operations-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Starting Device Operations API");
            
            var builder = CreateWebApplicationBuilder(args);
            var app = builder.Build();
            
            ConfigureApplication(app);
            
            Log.Information("Device Operations API configured successfully");
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static WebApplicationBuilder CreateWebApplicationBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add Serilog
        builder.Host.UseSerilog();

        // Add configuration
        builder.Configuration.AddJsonFile("config/appsettings.json", optional: false, reloadOnChange: true);
        builder.Configuration.AddJsonFile($"config/appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
        builder.Configuration.AddJsonFile("config/workers_config.json", optional: true, reloadOnChange: true);
        builder.Configuration.AddEnvironmentVariables();

        // Configure services
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.WriteIndented = true;
            });

        // Add comprehensive API documentation
        builder.Services.AddSwaggerDocumentation();

        // Register application services using extension methods
        builder.Services.RegisterApplicationServices(builder.Configuration);

        // Add comprehensive health checks
        builder.Services.AddApplicationHealthChecks(builder.Configuration);

        return builder;
    }

    private static void ConfigureApplication(WebApplication app)
    {
        // Configure middleware pipeline using extension methods
        app.ConfigureMiddlewarePipeline();

        // Configure health check endpoints
        app.UseApplicationHealthChecks();

        // Configure API endpoints
        app.MapControllers();

        // Configure comprehensive Swagger documentation
        app.UseSwaggerDocumentation(app.Environment);
    }

    private static IConfiguration GetConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("config/appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
