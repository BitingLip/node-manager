namespace DeviceOperations.Extensions;

/// <summary>
/// Extension methods for configuration binding and validation
/// </summary>
public static class ExtensionsConfiguration
{
    /// <summary>
    /// Validate that all required configuration sections are present
    /// </summary>
    /// <param name="configuration">The configuration to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing</exception>
    public static void ValidateConfiguration(this IConfiguration configuration)
    {
        ValidateSection(configuration, "PythonWorkers");
        ValidateSection(configuration, "Devices");
        ValidateSection(configuration, "Models");
        ValidateSection(configuration, "Logging");
    }

    /// <summary>
    /// Get a configuration value with a default fallback
    /// </summary>
    /// <typeparam name="T">The type of the configuration value</typeparam>
    /// <param name="configuration">The configuration</param>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultValue">The default value if not found</param>
    /// <returns>The configuration value or default</returns>
    public static T GetValueWithDefault<T>(this IConfiguration configuration, string key, T defaultValue)
    {
        var value = configuration.GetValue<T>(key);
        return value != null ? value : defaultValue;
    }

    /// <summary>
    /// Get the Python worker configuration with validation
    /// </summary>
    /// <param name="configuration">The configuration</param>
    /// <returns>Python worker configuration</returns>
    public static PythonWorkerConfiguration GetPythonWorkerConfiguration(this IConfiguration configuration)
    {
        var config = new PythonWorkerConfiguration();
        configuration.GetSection("PythonWorkers").Bind(config);
        
        // Validate required settings
        if (string.IsNullOrWhiteSpace(config.WorkerScriptPath))
        {
            throw new InvalidOperationException("PythonWorkers:WorkerScriptPath is required");
        }

        // Ensure paths are absolute
        if (!Path.IsPathRooted(config.WorkerScriptPath))
        {
            config.WorkerScriptPath = Path.GetFullPath(config.WorkerScriptPath);
        }

        return config;
    }

    /// <summary>
    /// Get the device configuration with validation
    /// </summary>
    /// <param name="configuration">The configuration</param>
    /// <returns>Device configuration</returns>
    public static DeviceConfiguration GetDeviceConfiguration(this IConfiguration configuration)
    {
        var config = new DeviceConfiguration();
        configuration.GetSection("Devices").Bind(config);
        
        // Validate settings
        if (config.MaxConcurrentOperations <= 0)
        {
            throw new InvalidOperationException("Devices:MaxConcurrentOperations must be greater than 0");
        }

        return config;
    }

    /// <summary>
    /// Get the model configuration with validation
    /// </summary>
    /// <param name="configuration">The configuration</param>
    /// <returns>Model configuration</returns>
    public static ModelConfiguration GetModelConfiguration(this IConfiguration configuration)
    {
        var config = new ModelConfiguration();
        configuration.GetSection("Models").Bind(config);
        
        // Validate and resolve paths
        if (string.IsNullOrWhiteSpace(config.ModelDirectory))
        {
            throw new InvalidOperationException("Models:ModelDirectory is required");
        }

        if (!Path.IsPathRooted(config.ModelDirectory))
        {
            config.ModelDirectory = Path.GetFullPath(config.ModelDirectory);
        }

        if (!Path.IsPathRooted(config.CacheDirectory))
        {
            config.CacheDirectory = Path.GetFullPath(config.CacheDirectory);
        }

        return config;
    }

    private static void ValidateSection(IConfiguration configuration, string sectionName)
    {
        var section = configuration.GetSection(sectionName);
        if (!section.Exists())
        {
            throw new InvalidOperationException($"Required configuration section '{sectionName}' is missing");
        }
    }
}
