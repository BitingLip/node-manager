namespace DeviceOperations.Models.Responses;

using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;

/// <summary>
/// Inference operation responses
/// </summary>
public static class ResponsesInference
{
    /// <summary>
    /// Response for creating an inference session
    /// </summary>
    public class CreateInferenceSessionResponse
    {
        /// <summary>
        /// Created session information
        /// </summary>
        public InferenceSession Session { get; set; } = new();

        /// <summary>
        /// Session creation success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Session initialization time in milliseconds
        /// </summary>
        public double InitializationTimeMs { get; set; }

        /// <summary>
        /// Resource allocations made for the session
        /// </summary>
        public List<string> ResourceAllocations { get; set; } = new();

        /// <summary>
        /// Session creation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Session creation message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Session creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for running inference
    /// </summary>
    public class RunInferenceResponse
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Inference success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Inference results
        /// </summary>
        public List<InferenceResult> Results { get; set; } = new();

        /// <summary>
        /// Total inference time in milliseconds
        /// </summary>
        public double InferenceTimeMs { get; set; }

        /// <summary>
        /// Inference performance metrics
        /// </summary>
        public InferencePerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Resource usage during inference
        /// </summary>
        public InferenceResourceUsage ResourceUsage { get; set; } = new();

        /// <summary>
        /// Inference quality metrics
        /// </summary>
        public Dictionary<string, double> QualityMetrics { get; set; } = new();

        /// <summary>
        /// Inference warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Inference message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Inference completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for running batch inference
    /// </summary>
    public class RunBatchInferenceResponse
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Batch processing success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Batch processing results
        /// </summary>
        public BatchInferenceResults Results { get; set; } = new();

        /// <summary>
        /// Total batch processing time in milliseconds
        /// </summary>
        public double TotalProcessingTimeMs { get; set; }

        /// <summary>
        /// Batch performance metrics
        /// </summary>
        public BatchPerformanceMetrics PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Resource usage during batch processing
        /// </summary>
        public InferenceResourceUsage ResourceUsage { get; set; } = new();

        /// <summary>
        /// Batch processing message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Batch completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for getting inference session status
    /// </summary>
    public class GetInferenceSessionStatusResponse
    {
        /// <summary>
        /// Session information
        /// </summary>
        public InferenceSession Session { get; set; } = new();

        /// <summary>
        /// Session health score (0-100)
        /// </summary>
        public double HealthScore { get; set; }

        /// <summary>
        /// Session performance summary
        /// </summary>
        public SessionPerformanceSummary? PerformanceSummary { get; set; }

        /// <summary>
        /// Resource utilization summary
        /// </summary>
        public ResourceUtilizationSummary? ResourceSummary { get; set; }

        /// <summary>
        /// Recent activity log
        /// </summary>
        public List<SessionActivityEntry> RecentActivity { get; set; } = new();

        /// <summary>
        /// Session warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Last status update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for listing inference sessions
    /// </summary>
    public class ListInferenceSessionsResponse
    {
        /// <summary>
        /// List of inference sessions
        /// </summary>
        public List<InferenceSession> Sessions { get; set; } = new();

        /// <summary>
        /// Total number of sessions
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Active sessions count
        /// </summary>
        public int ActiveCount { get; set; }

        /// <summary>
        /// Queued sessions count
        /// </summary>
        public int QueuedCount { get; set; }

        /// <summary>
        /// Status distribution
        /// </summary>
        public Dictionary<SessionStatus, int> StatusDistribution { get; set; } = new();

        /// <summary>
        /// Device distribution
        /// </summary>
        public Dictionary<string, int> DeviceDistribution { get; set; } = new();

        /// <summary>
        /// Priority distribution
        /// </summary>
        public Dictionary<SessionPriority, int> PriorityDistribution { get; set; } = new();
    }

    /// <summary>
    /// Response for pausing an inference session
    /// </summary>
    public class PauseInferenceSessionResponse
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Pause success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Pause time in milliseconds
        /// </summary>
        public double PauseTimeMs { get; set; }

        /// <summary>
        /// Session state saved
        /// </summary>
        public bool StateSaved { get; set; }

        /// <summary>
        /// State save location
        /// </summary>
        public string? StateSaveLocation { get; set; }

        /// <summary>
        /// Resources preserved
        /// </summary>
        public List<string> PreservedResources { get; set; } = new();

        /// <summary>
        /// Pause message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Pause timestamp
        /// </summary>
        public DateTime PausedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for resuming an inference session
    /// </summary>
    public class ResumeInferenceSessionResponse
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Resume success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Resume time in milliseconds
        /// </summary>
        public double ResumeTimeMs { get; set; }

        /// <summary>
        /// Session state restored
        /// </summary>
        public bool StateRestored { get; set; }

        /// <summary>
        /// Resources restored
        /// </summary>
        public List<string> RestoredResources { get; set; } = new();

        /// <summary>
        /// Resume message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Resume timestamp
        /// </summary>
        public DateTime ResumedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for canceling an inference session
    /// </summary>
    public class CancelInferenceSessionResponse
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Cancellation success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Cancellation time in milliseconds
        /// </summary>
        public double CancellationTimeMs { get; set; }

        /// <summary>
        /// Resources cleaned up
        /// </summary>
        public List<string> CleanedUpResources { get; set; } = new();

        /// <summary>
        /// Partial results preserved
        /// </summary>
        public bool PartialResultsPreserved { get; set; }

        /// <summary>
        /// Number of partial results
        /// </summary>
        public int PartialResultsCount { get; set; }

        /// <summary>
        /// Cancellation message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Cancellation timestamp
        /// </summary>
        public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for getting inference results
    /// </summary>
    public class GetInferenceResultsResponse
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Inference results
        /// </summary>
        public List<InferenceResult> Results { get; set; } = new();

        /// <summary>
        /// Total number of results
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Result statistics
        /// </summary>
        public ResultStatistics Statistics { get; set; } = new();

        /// <summary>
        /// Results retrieval timestamp
        /// </summary>
        public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for updating inference session
    /// </summary>
    public class UpdateInferenceSessionResponse
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Update success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Updated fields
        /// </summary>
        public List<string> UpdatedFields { get; set; } = new();

        /// <summary>
        /// Update impact assessment
        /// </summary>
        public SessionUpdateImpact Impact { get; set; } = new();

        /// <summary>
        /// Updated session information
        /// </summary>
        public InferenceSession? UpdatedSession { get; set; }

        /// <summary>
        /// Update message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Update timestamp
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

/// <summary>
/// Inference resource usage
/// </summary>
public class InferenceResourceUsage
{
    /// <summary>
    /// Peak memory usage in bytes
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Average memory usage in bytes
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Peak GPU utilization percentage
    /// </summary>
    public double PeakGpuUtilization { get; set; }

    /// <summary>
    /// Average GPU utilization percentage
    /// </summary>
    public double AverageGpuUtilization { get; set; }

    /// <summary>
    /// Peak CPU utilization percentage
    /// </summary>
    public double PeakCpuUtilization { get; set; }

    /// <summary>
    /// Average CPU utilization percentage
    /// </summary>
    public double AverageCpuUtilization { get; set; }

    /// <summary>
    /// Power consumption in watts
    /// </summary>
    public double PowerConsumption { get; set; }

    /// <summary>
    /// Resource efficiency score (0-100)
    /// </summary>
    public double EfficiencyScore { get; set; }
}

/// <summary>
/// Batch inference results
/// </summary>
public class BatchInferenceResults
{
    /// <summary>
    /// Individual inference results
    /// </summary>
    public List<InferenceResult> Results { get; set; } = new();

    /// <summary>
    /// Total operations processed
    /// </summary>
    public int TotalOperations { get; set; }

    /// <summary>
    /// Successful operations count
    /// </summary>
    public int SuccessfulOperations { get; set; }

    /// <summary>
    /// Failed operations count
    /// </summary>
    public int FailedOperations { get; set; }

    /// <summary>
    /// Skipped operations count
    /// </summary>
    public int SkippedOperations { get; set; }

    /// <summary>
    /// Batch success rate percentage
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Batch processing summary
    /// </summary>
    public Dictionary<string, object> ProcessingSummary { get; set; } = new();
}

/// <summary>
/// Batch performance metrics
/// </summary>
public class BatchPerformanceMetrics
{
    /// <summary>
    /// Average operation time in milliseconds
    /// </summary>
    public double AverageOperationTime { get; set; }

    /// <summary>
    /// Minimum operation time in milliseconds
    /// </summary>
    public double MinOperationTime { get; set; }

    /// <summary>
    /// Maximum operation time in milliseconds
    /// </summary>
    public double MaxOperationTime { get; set; }

    /// <summary>
    /// Operations per second
    /// </summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>
    /// Batch throughput efficiency
    /// </summary>
    public double ThroughputEfficiency { get; set; }

    /// <summary>
    /// Queue wait time statistics
    /// </summary>
    public TimeStatistics QueueWaitTime { get; set; } = new();

    /// <summary>
    /// Processing time statistics
    /// </summary>
    public TimeStatistics ProcessingTime { get; set; } = new();
}

/// <summary>
/// Session performance summary
/// </summary>
public class SessionPerformanceSummary
{
    /// <summary>
    /// Total operations completed
    /// </summary>
    public int TotalOperationsCompleted { get; set; }

    /// <summary>
    /// Average operation time in milliseconds
    /// </summary>
    public double AverageOperationTime { get; set; }

    /// <summary>
    /// Current operations per second
    /// </summary>
    public double CurrentOperationsPerSecond { get; set; }

    /// <summary>
    /// Performance trend
    /// </summary>
    public PerformanceTrend Trend { get; set; }

    /// <summary>
    /// Performance score (0-100)
    /// </summary>
    public double PerformanceScore { get; set; }

    /// <summary>
    /// Performance efficiency
    /// </summary>
    public double Efficiency { get; set; }

    /// <summary>
    /// Recent performance history
    /// </summary>
    public List<PerformanceDataPoint> RecentHistory { get; set; } = new();
}

/// <summary>
/// Resource utilization summary
/// </summary>
public class ResourceUtilizationSummary
{
    /// <summary>
    /// Current memory utilization
    /// </summary>
    public ResourceUtilization MemoryUtilization { get; set; } = new();

    /// <summary>
    /// Current GPU utilization
    /// </summary>
    public ResourceUtilization GpuUtilization { get; set; } = new();

    /// <summary>
    /// Current CPU utilization
    /// </summary>
    public ResourceUtilization CpuUtilization { get; set; } = new();

    /// <summary>
    /// Overall resource efficiency
    /// </summary>
    public double OverallEfficiency { get; set; }

    /// <summary>
    /// Resource bottlenecks
    /// </summary>
    public List<string> Bottlenecks { get; set; } = new();

    /// <summary>
    /// Resource optimization recommendations
    /// </summary>
    public List<string> OptimizationRecommendations { get; set; } = new();
}

/// <summary>
/// Session activity entry
/// </summary>
public class SessionActivityEntry
{
    /// <summary>
    /// Activity timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Activity type
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// Activity description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Activity duration in milliseconds
    /// </summary>
    public double? DurationMs { get; set; }

    /// <summary>
    /// Activity metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result statistics
/// </summary>
public class ResultStatistics
{
    /// <summary>
    /// Results by status
    /// </summary>
    public Dictionary<ResultStatus, int> StatusDistribution { get; set; } = new();

    /// <summary>
    /// Average result size in bytes
    /// </summary>
    public long AverageResultSize { get; set; }

    /// <summary>
    /// Total results size in bytes
    /// </summary>
    public long TotalResultsSize { get; set; }

    /// <summary>
    /// Average processing time per result
    /// </summary>
    public double AverageProcessingTime { get; set; }

    /// <summary>
    /// Quality metrics summary
    /// </summary>
    public Dictionary<string, double> QualityMetrics { get; set; } = new();
}

/// <summary>
/// Session update impact
/// </summary>
public class SessionUpdateImpact
{
    /// <summary>
    /// Requires session restart
    /// </summary>
    public bool RequiresRestart { get; set; }

    /// <summary>
    /// Affects current operations
    /// </summary>
    public bool AffectsCurrentOperations { get; set; }

    /// <summary>
    /// Estimated performance impact
    /// </summary>
    public double EstimatedPerformanceImpact { get; set; }

    /// <summary>
    /// Resource impact
    /// </summary>
    public Dictionary<string, double> ResourceImpact { get; set; } = new();

    /// <summary>
    /// Update recommendations
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Time statistics
/// </summary>
public class TimeStatistics
{
    /// <summary>
    /// Average time in milliseconds
    /// </summary>
    public double Average { get; set; }

    /// <summary>
    /// Minimum time in milliseconds
    /// </summary>
    public double Minimum { get; set; }

    /// <summary>
    /// Maximum time in milliseconds
    /// </summary>
    public double Maximum { get; set; }

    /// <summary>
    /// Standard deviation
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    /// 95th percentile
    /// </summary>
    public double Percentile95 { get; set; }
}

/// <summary>
/// Performance trend enumeration
/// </summary>
public enum PerformanceTrend
{
    Unknown = 0,
    Improving = 1,
    Stable = 2,
    Declining = 3,
    Volatile = 4
}

/// <summary>
/// Performance data point
/// </summary>
public class PerformanceDataPoint
{
    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Operations per second
    /// </summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>
    /// Average operation time
    /// </summary>
    public double AverageOperationTime { get; set; }

    /// <summary>
    /// Resource utilization
    /// </summary>
    public double ResourceUtilization { get; set; }
}

/// <summary>
/// Resource utilization
/// </summary>
public class ResourceUtilization
{
    /// <summary>
    /// Current utilization percentage
    /// </summary>
    public double Current { get; set; }

    /// <summary>
    /// Peak utilization percentage
    /// </summary>
    public double Peak { get; set; }

    /// <summary>
    /// Average utilization percentage
    /// </summary>
    public double Average { get; set; }

    /// <summary>
    /// Utilization trend
    /// </summary>
    public PerformanceTrend Trend { get; set; }
}
