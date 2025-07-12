namespace DeviceOperations.Models.Configuration
{
    /// <summary>
    /// Configuration options for the inference service
    /// </summary>
    public class InferenceServiceOptions
    {
        public const string ConfigurationKey = "InferenceService";

        /// <summary>
        /// Maximum connection pool size
        /// </summary>
        public int? MaxConnectionPoolSize { get; set; } = 10;

        /// <summary>
        /// Minimum connection pool size
        /// </summary>
        public int? MinConnectionPoolSize { get; set; } = 2;

        /// <summary>
        /// Connection idle timeout in minutes
        /// </summary>
        public int? ConnectionIdleTimeoutMinutes { get; set; } = 15;

        /// <summary>
        /// Connection maximum lifetime in hours
        /// </summary>
        public int? ConnectionMaxLifetimeHours { get; set; } = 4;

        /// <summary>
        /// Health check interval in minutes
        /// </summary>
        public int? HealthCheckIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// Default request timeout in seconds
        /// </summary>
        public int? DefaultRequestTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Python worker script path
        /// </summary>
        public string PythonWorkerScriptPath { get; set; } = "workers/inference_worker.py";

        /// <summary>
        /// Python executable path
        /// </summary>
        public string PythonExecutablePath { get; set; } = "python";

        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>
        /// Enable request tracing
        /// </summary>
        public bool EnableRequestTracing { get; set; } = true;

        /// <summary>
        /// Maximum batch size for batch operations
        /// </summary>
        public int? MaxBatchSize { get; set; } = 50;

        /// <summary>
        /// Default batch concurrency
        /// </summary>
        public int? DefaultBatchConcurrency { get; set; } = 4;

        /// <summary>
        /// Enable connection pooling
        /// </summary>
        public bool EnableConnectionPooling { get; set; } = true;

        /// <summary>
        /// Enable streaming progress updates
        /// </summary>
        public bool EnableProgressStreaming { get; set; } = true;

        /// <summary>
        /// Cache configuration for capabilities
        /// </summary>
        public CacheOptions CapabilitiesCache { get; set; } = new();

        /// <summary>
        /// Model loading configuration
        /// </summary>
        public ModelLoadingOptions ModelLoading { get; set; } = new();

        /// <summary>
        /// Retry configuration
        /// </summary>
        public RetryOptions Retry { get; set; } = new();
    }

    /// <summary>
    /// Cache configuration options
    /// </summary>
    public class CacheOptions
    {
        /// <summary>
        /// Enable caching
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Cache expiry time in minutes
        /// </summary>
        public int ExpiryMinutes { get; set; } = 30;

        /// <summary>
        /// Maximum cache size (number of entries)
        /// </summary>
        public int MaxCacheSize { get; set; } = 100;

        /// <summary>
        /// Enable cache warming
        /// </summary>
        public bool EnableCacheWarming { get; set; } = true;

        /// <summary>
        /// Cache refresh interval in minutes
        /// </summary>
        public int RefreshIntervalMinutes { get; set; } = 15;
    }

    /// <summary>
    /// Model loading configuration options
    /// </summary>
    public class ModelLoadingOptions
    {
        /// <summary>
        /// Enable model preloading
        /// </summary>
        public bool EnablePreloading { get; set; } = true;

        /// <summary>
        /// Model loading timeout in seconds
        /// </summary>
        public int LoadingTimeoutSeconds { get; set; } = 600;

        /// <summary>
        /// Maximum models to keep loaded
        /// </summary>
        public int MaxLoadedModels { get; set; } = 5;

        /// <summary>
        /// Model unload timeout in minutes
        /// </summary>
        public int UnloadTimeoutMinutes { get; set; } = 60;

        /// <summary>
        /// Enable model memory optimization
        /// </summary>
        public bool EnableMemoryOptimization { get; set; } = true;
    }

    /// <summary>
    /// Retry configuration options
    /// </summary>
    public class RetryOptions
    {
        /// <summary>
        /// Enable automatic retries
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum retry attempts
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Base retry delay in milliseconds
        /// </summary>
        public int BaseDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maximum retry delay in milliseconds
        /// </summary>
        public int MaxDelayMs { get; set; } = 30000;

        /// <summary>
        /// Exponential backoff multiplier
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Jitter factor for randomization (0.0 to 1.0)
        /// </summary>
        public double JitterFactor { get; set; } = 0.1;
    }
}
