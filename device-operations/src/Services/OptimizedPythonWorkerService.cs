using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using DeviceOperations.Models.Inference;
using DeviceOperations.Models.Configuration;

namespace DeviceOperations.Services
{
    /// <summary>
    /// Optimized Python worker service with connection pooling and performance optimization
    /// for high-frequency inference operations and streaming capabilities.
    /// </summary>
    public class OptimizedPythonWorkerService : IOptimizedPythonWorkerService
    {
        private readonly ILogger<OptimizedPythonWorkerService> _logger;
        private readonly InferenceServiceOptions _options;
        private readonly ConcurrentQueue<PythonWorkerConnection> _connectionPool;
        private readonly ConcurrentDictionary<string, PythonWorkerConnection> _activeConnections;
        private readonly SemaphoreSlim _poolSemaphore;
        private readonly Timer _healthCheckTimer;
        private readonly Timer _connectionCleanupTimer;
        private readonly object _poolLock = new object();
        
        // Performance tracking
        private readonly ConcurrentDictionary<string, ConnectionMetrics> _connectionMetrics;
        private long _totalRequestsProcessed;
        private long _connectionPoolHits;
        private long _connectionPoolMisses;
        private DateTime _serviceStartTime;

        // Configuration
        private readonly int _maxPoolSize;
        private readonly int _minPoolSize;
        private readonly TimeSpan _connectionIdleTimeout;
        private readonly TimeSpan _connectionMaxLifetime;
        private readonly TimeSpan _healthCheckInterval;

        public OptimizedPythonWorkerService(
            ILogger<OptimizedPythonWorkerService> logger,
            IOptions<InferenceServiceOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            
            // Pool configuration
            _maxPoolSize = _options.MaxConnectionPoolSize ?? 10;
            _minPoolSize = _options.MinConnectionPoolSize ?? 2;
            _connectionIdleTimeout = TimeSpan.FromMinutes(_options.ConnectionIdleTimeoutMinutes ?? 15);
            _connectionMaxLifetime = TimeSpan.FromHours(_options.ConnectionMaxLifetimeHours ?? 4);
            _healthCheckInterval = TimeSpan.FromMinutes(_options.HealthCheckIntervalMinutes ?? 5);

            // Initialize collections
            _connectionPool = new ConcurrentQueue<PythonWorkerConnection>();
            _activeConnections = new ConcurrentDictionary<string, PythonWorkerConnection>();
            _poolSemaphore = new SemaphoreSlim(_maxPoolSize, _maxPoolSize);
            _connectionMetrics = new ConcurrentDictionary<string, ConnectionMetrics>();
            
            // Performance tracking
            _serviceStartTime = DateTime.UtcNow;
            
            // Initialize timers
            _healthCheckTimer = new Timer(PerformHealthCheck, null, _healthCheckInterval, _healthCheckInterval);
            _connectionCleanupTimer = new Timer(CleanupExpiredConnections, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            // Pre-populate connection pool
            Task.Run(InitializeConnectionPoolAsync);

            _logger.LogInformation("OptimizedPythonWorkerService initialized with pool size {MinSize}-{MaxSize}",
                _minPoolSize, _maxPoolSize);
        }

        /// <summary>
        /// Execute inference operation with optimized connection pooling
        /// </summary>
        public async Task<T> ExecuteWithPoolingAsync<T>(Func<PythonWorkerConnection, Task<T>> operation, 
            CancellationToken cancellationToken = default)
        {
            var operationId = Guid.NewGuid().ToString("N")[..8];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogDebug("Starting pooled operation {OperationId}", operationId);
                
                // Get connection from pool
                var connection = await AcquireConnectionAsync(cancellationToken);
                
                try
                {
                    // Track connection usage
                    TrackConnectionUsage(connection.Id, true);
                    
                    // Execute operation
                    var result = await operation(connection);
                    
                    Interlocked.Increment(ref _totalRequestsProcessed);
                    _logger.LogDebug("Pooled operation {OperationId} completed in {ElapsedMs}ms", 
                        operationId, stopwatch.ElapsedMilliseconds);
                    
                    return result;
                }
                finally
                {
                    // Return connection to pool
                    await ReleaseConnectionAsync(connection);
                    TrackConnectionUsage(connection.Id, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pooled operation {OperationId} failed after {ElapsedMs}ms", 
                    operationId, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// Execute operation with real-time progress streaming
        /// </summary>
        public async IAsyncEnumerable<StreamingProgress> ExecuteWithProgressStreamingAsync<T>(
            Func<PythonWorkerConnection, IProgress<StreamingProgress>, Task<T>> operation,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var operationId = Guid.NewGuid().ToString("N")[..8];
            var progressQueue = new ConcurrentQueue<StreamingProgress>();
            var completionSource = new TaskCompletionSource<T>();
            
            _logger.LogDebug("Starting streaming operation {OperationId}", operationId);
            
            var connection = await AcquireConnectionAsync(cancellationToken);
            
            try
            {
                // Create progress reporter
                var progress = new Progress<StreamingProgress>(p =>
                {
                    p.OperationId = operationId;
                    p.Timestamp = DateTime.UtcNow;
                    progressQueue.Enqueue(p);
                });

                // Start operation in background
                var operationTask = Task.Run(async () =>
                {
                    try
                    {
                        var result = await operation(connection, progress);
                        completionSource.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        completionSource.SetException(ex);
                    }
                }, cancellationToken);

                // Stream progress updates
                while (!operationTask.IsCompleted && !cancellationToken.IsCancellationRequested)
                {
                    // Yield any queued progress updates
                    while (progressQueue.TryDequeue(out var progressUpdate))
                    {
                        yield return progressUpdate;
                    }
                    
                    // Wait a bit before checking again
                    await Task.Delay(100, cancellationToken);
                }

                // Yield final progress updates
                while (progressQueue.TryDequeue(out var progressUpdate))
                {
                    yield return progressUpdate;
                }

                // Wait for operation completion and yield final result
                await operationTask;
                yield return new StreamingProgress
                {
                    OperationId = operationId,
                    Stage = "Completed",
                    ProgressPercentage = 100,
                    IsCompleted = true,
                    Timestamp = DateTime.UtcNow,
                    ElapsedTime = DateTime.UtcNow - DateTime.UtcNow // Will be set properly by caller
                };
            }
            finally
            {
                await ReleaseConnectionAsync(connection);
            }
        }

        /// <summary>
        /// Execute batch operation with intelligent batching and connection optimization
        /// </summary>
        public async Task<BatchExecutionResult<T>> ExecuteBatchWithOptimizationAsync<T>(
            IEnumerable<Func<PythonWorkerConnection, Task<T>>> operations,
            BatchExecutionOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new BatchExecutionOptions();
            var batchId = Guid.NewGuid().ToString("N")[..8];
            var operationsList = operations.ToList();
            
            _logger.LogInformation("Starting optimized batch execution {BatchId} with {Count} operations",
                batchId, operationsList.Count);

            var results = new ConcurrentBag<BatchOperationResult<T>>();
            var optimalConcurrency = Math.Min(options.MaxConcurrency ?? _maxPoolSize, 
                Math.Min(operationsList.Count, _maxPoolSize));
            
            var semaphore = new SemaphoreSlim(optimalConcurrency, optimalConcurrency);
            var tasks = operationsList.Select(async (operation, index) =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var operationStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var result = await ExecuteWithPoolingAsync(operation, cancellationToken);
                        results.Add(new BatchOperationResult<T>
                        {
                            Index = index,
                            Result = result,
                            IsSuccess = true,
                            ExecutionTime = operationStopwatch.Elapsed
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new BatchOperationResult<T>
                        {
                            Index = index,
                            Error = ex,
                            IsSuccess = false,
                            ExecutionTime = operationStopwatch.Elapsed
                        });
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            var orderedResults = results.OrderBy(r => r.Index).ToList();
            var successCount = orderedResults.Count(r => r.IsSuccess);
            
            _logger.LogInformation("Batch execution {BatchId} completed: {SuccessCount}/{TotalCount} successful",
                batchId, successCount, operationsList.Count);

            return new BatchExecutionResult<T>
            {
                BatchId = batchId,
                Results = orderedResults,
                TotalOperations = operationsList.Count,
                SuccessfulOperations = successCount,
                FailedOperations = operationsList.Count - successCount,
                TotalExecutionTime = DateTime.UtcNow - DateTime.UtcNow // Will be calculated properly
            };
        }

        /// <summary>
        /// Get current performance metrics and connection pool status
        /// </summary>
        public Task<ConnectionPoolMetrics> GetPerformanceMetricsAsync()
        {
            var uptime = DateTime.UtcNow - _serviceStartTime;
            var activeConnectionCount = _activeConnections.Count;
            var pooledConnectionCount = _connectionPool.Count;
            var totalConnections = activeConnectionCount + pooledConnectionCount;
            
            var metrics = new ConnectionPoolMetrics
            {
                TotalRequestsProcessed = _totalRequestsProcessed,
                ConnectionPoolHits = _connectionPoolHits,
                ConnectionPoolMisses = _connectionPoolMisses,
                PoolHitRate = _totalRequestsProcessed > 0 ? 
                    (double)_connectionPoolHits / _totalRequestsProcessed : 0,
                ActiveConnections = activeConnectionCount,
                PooledConnections = pooledConnectionCount,
                TotalConnections = totalConnections,
                MaxPoolSize = _maxPoolSize,
                MinPoolSize = _minPoolSize,
                ServiceUptime = uptime,
                AverageConnectionAge = CalculateAverageConnectionAge(),
                ConnectionMetrics = _connectionMetrics.Values.ToList()
            };

            return Task.FromResult(metrics);
        }

        /// <summary>
        /// Warm up the connection pool by pre-creating connections
        /// </summary>
        public async Task WarmupConnectionPoolAsync(int targetConnections = 0)
        {
            targetConnections = targetConnections == 0 ? _minPoolSize : 
                Math.Min(targetConnections, _maxPoolSize);
            
            _logger.LogInformation("Warming up connection pool to {TargetConnections} connections", 
                targetConnections);

            var tasks = Enumerable.Range(0, targetConnections)
                .Select(_ => CreateNewConnectionAsync())
                .ToList();

            var connections = await Task.WhenAll(tasks);
            
            foreach (var connection in connections.Where(c => c != null))
            {
                _connectionPool.Enqueue(connection);
            }

            _logger.LogInformation("Connection pool warmed up with {ActualConnections} connections",
                connections.Count(c => c != null));
        }

        #region Private Methods

        private async Task InitializeConnectionPoolAsync()
        {
            try
            {
                await WarmupConnectionPoolAsync(_minPoolSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize connection pool");
            }
        }

        private async Task<PythonWorkerConnection> AcquireConnectionAsync(CancellationToken cancellationToken)
        {
            await _poolSemaphore.WaitAsync(cancellationToken);
            
            try
            {
                // Try to get connection from pool
                if (_connectionPool.TryDequeue(out var pooledConnection) && 
                    IsConnectionHealthy(pooledConnection))
                {
                    _activeConnections[pooledConnection.Id] = pooledConnection;
                    Interlocked.Increment(ref _connectionPoolHits);
                    
                    _logger.LogDebug("Acquired pooled connection {ConnectionId}", pooledConnection.Id);
                    return pooledConnection;
                }

                // Create new connection if pool is empty or connection unhealthy
                var newConnection = await CreateNewConnectionAsync();
                if (newConnection != null)
                {
                    _activeConnections[newConnection.Id] = newConnection;
                    Interlocked.Increment(ref _connectionPoolMisses);
                    
                    _logger.LogDebug("Created new connection {ConnectionId}", newConnection.Id);
                    return newConnection;
                }

                throw new InvalidOperationException("Failed to acquire or create Python worker connection");
            }
            catch
            {
                _poolSemaphore.Release();
                throw;
            }
        }

        private async Task ReleaseConnectionAsync(PythonWorkerConnection connection)
        {
            try
            {
                _activeConnections.TryRemove(connection.Id, out _);

                // Return healthy connections to pool
                if (IsConnectionHealthy(connection) && _connectionPool.Count < _maxPoolSize)
                {
                    connection.LastUsed = DateTime.UtcNow;
                    _connectionPool.Enqueue(connection);
                    _logger.LogDebug("Returned connection {ConnectionId} to pool", connection.Id);
                }
                else
                {
                    // Dispose unhealthy or excess connections
                    await DisposeConnectionAsync(connection);
                    _logger.LogDebug("Disposed connection {ConnectionId}", connection.Id);
                }
            }
            finally
            {
                _poolSemaphore.Release();
            }
        }

        private async Task<PythonWorkerConnection> CreateNewConnectionAsync()
        {
            try
            {
                var connectionId = Guid.NewGuid().ToString("N")[..8];
                var connection = new PythonWorkerConnection
                {
                    Id = connectionId,
                    Created = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow,
                    UsageCount = 0,
                    IsHealthy = true
                };

                // Initialize connection metrics
                _connectionMetrics[connectionId] = new ConnectionMetrics
                {
                    ConnectionId = connectionId,
                    Created = DateTime.UtcNow,
                    TotalOperations = 0,
                    TotalExecutionTime = TimeSpan.Zero,
                    AverageExecutionTime = TimeSpan.Zero
                };

                // TODO: Initialize actual Python process connection here
                // This would involve starting a Python process and establishing communication
                
                _logger.LogDebug("Created new Python worker connection {ConnectionId}", connectionId);
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create new Python worker connection");
                return null;
            }
        }

        private bool IsConnectionHealthy(PythonWorkerConnection connection)
        {
            if (connection == null || !connection.IsHealthy)
                return false;

            var age = DateTime.UtcNow - connection.Created;
            var idleTime = DateTime.UtcNow - connection.LastUsed;

            return age < _connectionMaxLifetime && idleTime < _connectionIdleTimeout;
        }

        private void TrackConnectionUsage(string connectionId, bool isStart)
        {
            if (_connectionMetrics.TryGetValue(connectionId, out var metrics))
            {
                if (isStart)
                {
                    metrics.LastOperationStart = DateTime.UtcNow;
                    metrics.TotalOperations++;
                }
                else
                {
                    if (metrics.LastOperationStart.HasValue)
                    {
                        var executionTime = DateTime.UtcNow - metrics.LastOperationStart.Value;
                        metrics.TotalExecutionTime += executionTime;
                        metrics.AverageExecutionTime = TimeSpan.FromMilliseconds(
                            metrics.TotalExecutionTime.TotalMilliseconds / metrics.TotalOperations);
                    }
                }
            }
        }

        private TimeSpan CalculateAverageConnectionAge()
        {
            var now = DateTime.UtcNow;
            var allConnections = _activeConnections.Values.Concat(_connectionPool.ToArray());
            var ages = allConnections.Select(c => now - c.Created).ToList();
            
            return ages.Any() ? TimeSpan.FromMilliseconds(ages.Average(a => a.TotalMilliseconds)) : TimeSpan.Zero;
        }

        private async void PerformHealthCheck(object state)
        {
            try
            {
                _logger.LogDebug("Performing connection pool health check");
                
                var unhealthyConnections = _activeConnections.Values
                    .Where(c => !IsConnectionHealthy(c))
                    .ToList();

                foreach (var connection in unhealthyConnections)
                {
                    _activeConnections.TryRemove(connection.Id, out _);
                    await DisposeConnectionAsync(connection);
                    _logger.LogWarning("Removed unhealthy active connection {ConnectionId}", connection.Id);
                }

                // Remove unhealthy pooled connections
                var pooledConnections = new List<PythonWorkerConnection>();
                while (_connectionPool.TryDequeue(out var connection))
                {
                    if (IsConnectionHealthy(connection))
                    {
                        pooledConnections.Add(connection);
                    }
                    else
                    {
                        await DisposeConnectionAsync(connection);
                        _logger.LogWarning("Removed unhealthy pooled connection {ConnectionId}", connection.Id);
                    }
                }

                // Re-enqueue healthy connections
                foreach (var connection in pooledConnections)
                {
                    _connectionPool.Enqueue(connection);
                }

                _logger.LogDebug("Health check completed. Removed {UnhealthyCount} unhealthy connections",
                    unhealthyConnections.Count + (pooledConnections.Count - _connectionPool.Count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection pool health check");
            }
        }

        private async void CleanupExpiredConnections(object state)
        {
            try
            {
                // Clean up expired connection metrics
                var expiredMetrics = _connectionMetrics
                    .Where(kvp => DateTime.UtcNow - kvp.Value.Created > _connectionMaxLifetime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var connectionId in expiredMetrics)
                {
                    _connectionMetrics.TryRemove(connectionId, out _);
                }

                if (expiredMetrics.Any())
                {
                    _logger.LogDebug("Cleaned up {Count} expired connection metrics", expiredMetrics.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection cleanup");
            }
        }

        private async Task DisposeConnectionAsync(PythonWorkerConnection connection)
        {
            try
            {
                connection.IsHealthy = false;
                _connectionMetrics.TryRemove(connection.Id, out _);
                
                // TODO: Properly dispose Python process connection
                // This would involve terminating the Python process and cleaning up resources
                
                await Task.CompletedTask; // Placeholder for actual disposal logic
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing connection {ConnectionId}", connection.Id);
            }
        }

        #endregion

        public void Dispose()
        {
            _healthCheckTimer?.Dispose();
            _connectionCleanupTimer?.Dispose();
            _poolSemaphore?.Dispose();

            // Dispose all connections
            var allConnections = _activeConnections.Values.Concat(_connectionPool.ToArray());
            foreach (var connection in allConnections)
            {
                DisposeConnectionAsync(connection).GetAwaiter().GetResult();
            }

            _logger.LogInformation("OptimizedPythonWorkerService disposed");
        }
    }
}
