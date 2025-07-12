using DeviceOperations.Models.Responses;
using DeviceOperations.Models.Common;

namespace DeviceOperations.Models.Inference
{
    /// <summary>
    /// Represents a Python worker connection with lifecycle management
    /// </summary>
    public class PythonWorkerConnection
    {
        /// <summary>
        /// Unique connection identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Connection creation timestamp
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Last usage timestamp
        /// </summary>
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// Total number of operations performed on this connection
        /// </summary>
        public long UsageCount { get; set; }

        /// <summary>
        /// Connection health status
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Python process ID (if applicable)
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// Connection-specific configuration
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();

        /// <summary>
        /// Current operation being performed (for tracking)
        /// </summary>
        public string? CurrentOperation { get; set; }

        /// <summary>
        /// Connection age
        /// </summary>
        public TimeSpan Age => DateTime.UtcNow - Created;

        /// <summary>
        /// Idle time since last use
        /// </summary>
        public TimeSpan IdleTime => DateTime.UtcNow - LastUsed;
    }

    /// <summary>
    /// Real-time progress information for streaming operations
    /// </summary>
    public class StreamingProgress
    {
        /// <summary>
        /// Operation identifier
        /// </summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Current operation stage
        /// </summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double ProgressPercentage { get; set; }

        /// <summary>
        /// Current status message
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Whether the operation is completed
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Whether an error occurred
        /// </summary>
        public bool HasError { get; set; }

        /// <summary>
        /// Error message if applicable
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Progress timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Elapsed time since operation start
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Estimated time remaining
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Current processing rate (items/second)
        /// </summary>
        public double? ProcessingRate { get; set; }

        /// <summary>
        /// Items processed so far
        /// </summary>
        public long? ItemsProcessed { get; set; }

        /// <summary>
        /// Total items to process
        /// </summary>
        public long? TotalItems { get; set; }
    }

    /// <summary>
    /// Options for batch execution
    /// </summary>
    public class BatchExecutionOptions
    {
        /// <summary>
        /// Maximum number of concurrent operations
        /// </summary>
        public int? MaxConcurrency { get; set; }

        /// <summary>
        /// Retry failed operations
        /// </summary>
        public bool RetryFailedOperations { get; set; } = true;

        /// <summary>
        /// Maximum retry attempts
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay between retries
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Stop on first error
        /// </summary>
        public bool StopOnFirstError { get; set; } = false;

        /// <summary>
        /// Progress reporting interval
        /// </summary>
        public TimeSpan ProgressReportingInterval { get; set; } = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Result of a batch execution
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    public class BatchExecutionResult<T>
    {
        /// <summary>
        /// Batch identifier
        /// </summary>
        public string BatchId { get; set; } = string.Empty;

        /// <summary>
        /// Individual operation results
        /// </summary>
        public List<BatchOperationResult<T>> Results { get; set; } = new();

        /// <summary>
        /// Total number of operations
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Number of successful operations
        /// </summary>
        public int SuccessfulOperations { get; set; }

        /// <summary>
        /// Number of failed operations
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate => TotalOperations > 0 ? 
            (double)SuccessfulOperations / TotalOperations * 100 : 0;

        /// <summary>
        /// Total execution time
        /// </summary>
        public TimeSpan TotalExecutionTime { get; set; }

        /// <summary>
        /// Average execution time per operation
        /// </summary>
        public TimeSpan AverageExecutionTime => TotalOperations > 0 ?
            TimeSpan.FromMilliseconds(TotalExecutionTime.TotalMilliseconds / TotalOperations) : TimeSpan.Zero;

        /// <summary>
        /// Execution start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Execution end time
        /// </summary>
        public DateTime EndTime { get; set; }
    }

    /// <summary>
    /// Result of an individual batch operation
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    public class BatchOperationResult<T>
    {
        /// <summary>
        /// Operation index in the batch
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Whether the operation succeeded
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Operation result (if successful)
        /// </summary>
        public T? Result { get; set; }

        /// <summary>
        /// Error information (if failed)
        /// </summary>
        public Exception? Error { get; set; }

        /// <summary>
        /// Operation execution time
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Number of retry attempts
        /// </summary>
        public int RetryAttempts { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Connection pool performance metrics
    /// </summary>
    public class ConnectionPoolMetrics
    {
        /// <summary>
        /// Total requests processed
        /// </summary>
        public long TotalRequestsProcessed { get; set; }

        /// <summary>
        /// Connection pool hits
        /// </summary>
        public long ConnectionPoolHits { get; set; }

        /// <summary>
        /// Connection pool misses
        /// </summary>
        public long ConnectionPoolMisses { get; set; }

        /// <summary>
        /// Pool hit rate percentage
        /// </summary>
        public double PoolHitRate { get; set; }

        /// <summary>
        /// Currently active connections
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Connections in pool (idle)
        /// </summary>
        public int PooledConnections { get; set; }

        /// <summary>
        /// Total connections (active + pooled)
        /// </summary>
        public int TotalConnections { get; set; }

        /// <summary>
        /// Maximum pool size
        /// </summary>
        public int MaxPoolSize { get; set; }

        /// <summary>
        /// Minimum pool size
        /// </summary>
        public int MinPoolSize { get; set; }

        /// <summary>
        /// Service uptime
        /// </summary>
        public TimeSpan ServiceUptime { get; set; }

        /// <summary>
        /// Average connection age
        /// </summary>
        public TimeSpan AverageConnectionAge { get; set; }

        /// <summary>
        /// Individual connection metrics
        /// </summary>
        public List<ConnectionMetrics> ConnectionMetrics { get; set; } = new();

        /// <summary>
        /// Pool utilization percentage
        /// </summary>
        public double PoolUtilization => MaxPoolSize > 0 ? 
            (double)TotalConnections / MaxPoolSize * 100 : 0;

        /// <summary>
        /// Requests per second (current rate)
        /// </summary>
        public double RequestsPerSecond => ServiceUptime.TotalSeconds > 0 ?
            TotalRequestsProcessed / ServiceUptime.TotalSeconds : 0;
    }

    /// <summary>
    /// Metrics for individual connections
    /// </summary>
    public class ConnectionMetrics
    {
        /// <summary>
        /// Connection identifier
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// Connection creation time
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Total operations performed
        /// </summary>
        public long TotalOperations { get; set; }

        /// <summary>
        /// Total execution time
        /// </summary>
        public TimeSpan TotalExecutionTime { get; set; }

        /// <summary>
        /// Average execution time per operation
        /// </summary>
        public TimeSpan AverageExecutionTime { get; set; }

        /// <summary>
        /// Last operation start time (for tracking)
        /// </summary>
        public DateTime? LastOperationStart { get; set; }

        /// <summary>
        /// Connection age
        /// </summary>
        public TimeSpan Age => DateTime.UtcNow - Created;

        /// <summary>
        /// Operations per minute
        /// </summary>
        public double OperationsPerMinute => Age.TotalMinutes > 0 ?
            TotalOperations / Age.TotalMinutes : 0;
    }

    /// <summary>
    /// Cached capabilities with metadata for intelligent caching
    /// </summary>
    public class CachedCapabilities
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Cached capabilities data
        /// </summary>
        public InferenceCapabilities Capabilities { get; set; } = new();

        /// <summary>
        /// Cache timestamp
        /// </summary>
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// Last access timestamp
        /// </summary>
        public DateTime LastAccessed { get; set; }

        /// <summary>
        /// Access count
        /// </summary>
        public long AccessCount { get; set; }

        /// <summary>
        /// Cache expiry timestamp
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Whether the cache entry is still valid
        /// </summary>
        public bool IsValid => DateTime.UtcNow < ExpiresAt;

        /// <summary>
        /// Cache age
        /// </summary>
        public TimeSpan Age => DateTime.UtcNow - CachedAt;

        /// <summary>
        /// Time since last access
        /// </summary>
        public TimeSpan TimeSinceLastAccess => DateTime.UtcNow - LastAccessed;
    }

    /// <summary>
    /// Operation metrics for performance tracking
    /// </summary>
    public class OperationMetrics
    {
        /// <summary>
        /// Operation name
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// Total invocations
        /// </summary>
        public long TotalInvocations { get; set; }

        /// <summary>
        /// Successful invocations
        /// </summary>
        public long SuccessfulInvocations { get; set; }

        /// <summary>
        /// Failed invocations
        /// </summary>
        public long FailedInvocations { get; set; }

        /// <summary>
        /// Total execution time
        /// </summary>
        public TimeSpan TotalExecutionTime { get; set; }

        /// <summary>
        /// Average execution time
        /// </summary>
        public TimeSpan AverageExecutionTime => TotalInvocations > 0 ?
            TimeSpan.FromMilliseconds(TotalExecutionTime.TotalMilliseconds / TotalInvocations) : TimeSpan.Zero;

        /// <summary>
        /// Minimum execution time
        /// </summary>
        public TimeSpan MinExecutionTime { get; set; } = TimeSpan.MaxValue;

        /// <summary>
        /// Maximum execution time
        /// </summary>
        public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate => TotalInvocations > 0 ?
            (double)SuccessfulInvocations / TotalInvocations * 100 : 0;

        /// <summary>
        /// First invocation timestamp
        /// </summary>
        public DateTime FirstInvocation { get; set; }

        /// <summary>
        /// Last invocation timestamp
        /// </summary>
        public DateTime LastInvocation { get; set; }

        /// <summary>
        /// Operations per minute
        /// </summary>
        public double OperationsPerMinute
        {
            get
            {
                if (FirstInvocation == default || LastInvocation == default || TotalInvocations <= 1)
                    return 0;

                var duration = LastInvocation - FirstInvocation;
                return duration.TotalMinutes > 0 ? TotalInvocations / duration.TotalMinutes : 0;
            }
        }
    }
}
