using DeviceOperations.Models.Inference;

namespace DeviceOperations.Services
{
    /// <summary>
    /// Interface for optimized Python worker service with connection pooling and streaming capabilities
    /// </summary>
    public interface IOptimizedPythonWorkerService : IDisposable
    {
        /// <summary>
        /// Execute inference operation with optimized connection pooling
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operation">Operation to execute with the connection</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Operation result</returns>
        Task<T> ExecuteWithPoolingAsync<T>(Func<PythonWorkerConnection, Task<T>> operation, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute operation with real-time progress streaming
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operation">Operation to execute with progress reporting</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of progress updates</returns>
        IAsyncEnumerable<StreamingProgress> ExecuteWithProgressStreamingAsync<T>(
            Func<PythonWorkerConnection, IProgress<StreamingProgress>, Task<T>> operation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute batch operation with intelligent batching and connection optimization
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operations">Collection of operations to execute</param>
        /// <param name="options">Batch execution options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Batch execution result</returns>
        Task<BatchExecutionResult<T>> ExecuteBatchWithOptimizationAsync<T>(
            IEnumerable<Func<PythonWorkerConnection, Task<T>>> operations,
            BatchExecutionOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get current performance metrics and connection pool status
        /// </summary>
        /// <returns>Connection pool metrics</returns>
        Task<ConnectionPoolMetrics> GetPerformanceMetricsAsync();

        /// <summary>
        /// Warm up the connection pool by pre-creating connections
        /// </summary>
        /// <param name="targetConnections">Target number of connections (0 for minimum pool size)</param>
        /// <returns>Task representing the warmup operation</returns>
        Task WarmupConnectionPoolAsync(int targetConnections = 0);
    }
}
