using DeviceOperations.Models.Postprocessing;
using DeviceOperations.Services.Postprocessing;

namespace DeviceOperations.Models.Postprocessing
{
    // ============================================================================
    // Performance Analytics Models (Week 20)
    // ============================================================================

    /// <summary>
    /// Request for performance analytics generation
    /// </summary>
    public class PerformanceAnalyticsRequest
    {
        public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-7);
        public DateTime EndDate { get; set; } = DateTime.UtcNow;
        public bool IncludeComparativeAnalysis { get; set; } = false;
        public bool IncludePredictiveAnalysis { get; set; } = false;
        public string AnalysisType { get; set; } = "comprehensive";
        public List<string> MetricTypes { get; set; } = new();
        public string? OperationFilter { get; set; }
        public string? ModelFilter { get; set; }
        public string? DeviceFilter { get; set; }
    }

    /// <summary>
    /// Comprehensive performance analytics results
    /// </summary>
    public class PostprocessingPerformanceAnalytics
    {
        public string RequestId { get; set; } = string.Empty;
        public TimeframeInfo AnalysisTimeframe { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        
        // Core metrics
        public PostprocessingCoreMetrics CoreMetrics { get; set; } = new();
        
        // Trend analysis
        public PostprocessingPerformanceTrends? PerformanceTrends { get; set; }
        
        // Resource analysis
        public PostprocessingResourceUtilization? ResourceUtilization { get; set; }
        
        // Quality analysis
        public PostprocessingQualityMetrics? QualityMetrics { get; set; }
        
        // Error analysis
        public PostprocessingErrorAnalysis? ErrorAnalysis { get; set; }
        
        // Operation insights
        public PostprocessingOperationInsights? OperationInsights { get; set; }
        
        // Optimization recommendations
        public List<PostprocessingOptimizationRecommendation> OptimizationRecommendations { get; set; } = new();
        
        // Predictive insights (optional)
        public PostprocessingPredictiveInsights? PredictiveInsights { get; set; }
        
        // Comparative analysis (optional)
        public PostprocessingComparativeAnalysis? ComparativeAnalysis { get; set; }
    }

    /// <summary>
    /// Core performance metrics
    /// </summary>
    public class PostprocessingCoreMetrics
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double SuccessRate { get; set; }
        public double AverageProcessingTimeMs { get; set; }
        public double MedianProcessingTimeMs { get; set; }
        public double MinProcessingTimeMs { get; set; }
        public double MaxProcessingTimeMs { get; set; }
        public double ThroughputPerHour { get; set; }
        public double ErrorRate { get; set; }
    }

    /// <summary>
    /// Performance trends over time
    /// </summary>
    public class PostprocessingPerformanceTrends
    {
        public List<PerformanceTrendPoint> TrendPoints { get; set; } = new();
        public List<DateTime> PeakUsageHours { get; set; } = new();
        public string PerformanceStability { get; set; } = string.Empty;
        public string TrendDirection { get; set; } = string.Empty;
    }

    /// <summary>
    /// Individual trend point
    /// </summary>
    public class PerformanceTrendPoint
    {
        public DateTime Timestamp { get; set; }
        public int RequestCount { get; set; }
        public double AverageProcessingTime { get; set; }
        public double SuccessRate { get; set; }
        public int ErrorCount { get; set; }
    }

    /// <summary>
    /// Resource utilization metrics
    /// </summary>
    public class PostprocessingResourceUtilization
    {
        public ResourceMetric CpuUtilization { get; set; } = new();
        public ResourceMetric MemoryUtilization { get; set; } = new();
        public ResourceMetric NetworkUtilization { get; set; } = new();
        public ResourceMetric StorageUtilization { get; set; } = new();
        public ConcurrencyMetrics ConcurrentRequests { get; set; } = new();
        public List<string> ResourceBottlenecks { get; set; } = new();
        public int OptimalConcurrencyLevel { get; set; }
    }

    /// <summary>
    /// Resource metric details
    /// </summary>
    public class ResourceMetric
    {
        public double Average { get; set; }
        public double Peak { get; set; }
        public double Minimum { get; set; }
        public string Unit { get; set; } = string.Empty;
        public List<ResourceUsagePoint> TimeSeries { get; set; } = new();
    }

    /// <summary>
    /// Resource usage at a specific time
    /// </summary>
    public class ResourceUsagePoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    /// <summary>
    /// Concurrency metrics
    /// </summary>
    public class ConcurrencyMetrics
    {
        public double AverageConcurrency { get; set; }
        public int MaxConcurrency { get; set; }
        public double ConcurrencyEfficiency { get; set; }
        public List<ConcurrencyPoint> ConcurrencyTimeSeries { get; set; } = new();
    }

    /// <summary>
    /// Concurrency at a specific time
    /// </summary>
    public class ConcurrencyPoint
    {
        public DateTime Timestamp { get; set; }
        public int ActiveRequests { get; set; }
        public int QueuedRequests { get; set; }
    }

    /// <summary>
    /// Quality metrics analysis
    /// </summary>
    public class PostprocessingQualityMetrics
    {
        public double AverageQualityScore { get; set; }
        public double QualityConsistency { get; set; }
        public Dictionary<string, double> QualityByOperation { get; set; } = new();
        public Dictionary<string, double> QualityByModel { get; set; } = new();
        public List<QualityTrendPoint> QualityTrends { get; set; } = new();
    }

    /// <summary>
    /// Quality trend point
    /// </summary>
    public class QualityTrendPoint
    {
        public DateTime Timestamp { get; set; }
        public double QualityScore { get; set; }
        public string Operation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Error analysis results
    /// </summary>
    public class PostprocessingErrorAnalysis
    {
        public int TotalErrors { get; set; }
        public Dictionary<string, int> ErrorsByType { get; set; } = new();
        public Dictionary<string, int> ErrorsByOperation { get; set; } = new();
        public List<ErrorTrendPoint> ErrorTrends { get; set; } = new();
        public List<ErrorPattern> ErrorPatterns { get; set; } = new();
        public double MeanTimeBetweenFailures { get; set; }
    }

    /// <summary>
    /// Error trend point
    /// </summary>
    public class ErrorTrendPoint
    {
        public DateTime Timestamp { get; set; }
        public int ErrorCount { get; set; }
        public string ErrorType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Error pattern analysis
    /// </summary>
    public class ErrorPattern
    {
        public string Pattern { get; set; } = string.Empty;
        public int Occurrences { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Operation-specific insights
    /// </summary>
    public class PostprocessingOperationInsights
    {
        public Dictionary<string, OperationStats> OperationStatistics { get; set; } = new();
        public List<string> TopPerformingOperations { get; set; } = new();
        public List<string> BottleneckOperations { get; set; } = new();
        public Dictionary<string, List<string>> OptimizationOpportunities { get; set; } = new();
    }

    /// <summary>
    /// Statistics for a specific operation
    /// </summary>
    public class OperationStats
    {
        public string OperationType { get; set; } = string.Empty;
        public int RequestCount { get; set; }
        public double AverageProcessingTime { get; set; }
        public double SuccessRate { get; set; }
        public double AverageQualityScore { get; set; }
        public double ResourceUsage { get; set; }
        public string PerformanceRating { get; set; } = string.Empty;
    }

    /// <summary>
    /// Optimization recommendation
    /// </summary>
    public class PostprocessingOptimizationRecommendation
    {
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EstimatedImpact { get; set; } = string.Empty;
        public string Implementation { get; set; } = string.Empty;
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    /// <summary>
    /// Predictive insights (optional)
    /// </summary>
    public class PostprocessingPredictiveInsights
    {
        public LoadForecast LoadForecast { get; set; } = new();
        public PerformancePrediction PerformancePrediction { get; set; } = new();
        public List<PredictedBottleneck> PredictedBottlenecks { get; set; } = new();
        public CapacityRecommendations CapacityRecommendations { get; set; } = new();
    }

    /// <summary>
    /// Load forecast
    /// </summary>
    public class LoadForecast
    {
        public List<LoadPredictionPoint> PredictedLoad { get; set; } = new();
        public double ConfidenceLevel { get; set; }
        public string ForecastMethod { get; set; } = string.Empty;
        public double PredictionAccuracy { get; set; }
    }

    /// <summary>
    /// Load prediction point
    /// </summary>
    public class LoadPredictionPoint
    {
        public DateTime Timestamp { get; set; }
        public double PredictedRequests { get; set; }
        public double ConfidenceInterval { get; set; }
    }

    /// <summary>
    /// Performance prediction
    /// </summary>
    public class PerformancePrediction
    {
        public double PredictedAverageResponseTime { get; set; }
        public double PredictedThroughput { get; set; }
        public double PredictedErrorRate { get; set; }
        public string Confidence { get; set; } = string.Empty;
        public double ExpectedThroughput { get; set; }
        public TimeSpan ExpectedLatency { get; set; }
        public List<string> OptimizationOpportunities { get; set; } = new();
        public ResourceForecast ResourceUtilizationForecast { get; set; } = new();
    }

    /// <summary>
    /// Predicted bottleneck
    /// </summary>
    public class PredictedBottleneck
    {
        public string Resource { get; set; } = string.Empty;
        public DateTime PredictedTime { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Mitigation { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
    }

    /// <summary>
    /// Capacity recommendations
    /// </summary>
    public class CapacityRecommendations
    {
        public int RecommendedConcurrency { get; set; }
        public string ScalingAdvice { get; set; } = string.Empty;
        public Dictionary<string, object> ResourceRecommendations { get; set; } = new();
    }

    /// <summary>
    /// Comparative analysis (optional)
    /// </summary>
    public class PostprocessingComparativeAnalysis
    {
        public PeriodComparison PeriodComparison { get; set; } = new();
        public OperationComparison OperationComparison { get; set; } = new();
        public ModelComparison ModelComparison { get; set; } = new();
        public Dictionary<string, double> ImprovementOpportunities { get; set; } = new();
    }

    /// <summary>
    /// Period-to-period comparison
    /// </summary>
    public class PeriodComparison
    {
        public string ComparisonPeriod { get; set; } = string.Empty;
        public double PerformanceChange { get; set; }
        public double QualityChange { get; set; }
        public double EfficiencyChange { get; set; }
        public List<string> KeyChanges { get; set; } = new();
    }

    /// <summary>
    /// Operation comparison
    /// </summary>
    public class OperationComparison
    {
        public Dictionary<string, OperationPerformanceComparison> Operations { get; set; } = new();
        public string BestPerformingOperation { get; set; } = string.Empty;
        public string MostImprovedOperation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Performance comparison for an operation
    /// </summary>
    public class OperationPerformanceComparison
    {
        public double PerformanceChange { get; set; }
        public double QualityChange { get; set; }
        public double UsageChange { get; set; }
        public string Trend { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model comparison
    /// </summary>
    public class ModelComparison
    {
        public Dictionary<string, ModelPerformanceComparison> Models { get; set; } = new();
        public string BestPerformingModel { get; set; } = string.Empty;
        public string MostEfficientModel { get; set; } = string.Empty;
    }

    /// <summary>
    /// Performance comparison for a model
    /// </summary>
    public class ModelPerformanceComparison
    {
        public double PerformanceRating { get; set; }
        public double QualityRating { get; set; }
        public double EfficiencyRating { get; set; }
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Timeframe information
    /// </summary>
    public class TimeframeInfo
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan Duration { get; set; }
    }

    // ============================================================================
    // Request Tracing Models
    // ============================================================================

    /// <summary>
    /// Request trace for performance monitoring
    /// </summary>
    public class PostprocessingRequestTrace
    {
        public string RequestId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public long? ProcessingTimeMs => EndTime.HasValue ? (long)(EndTime.Value - StartTime).TotalMilliseconds : null;
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
        public PostprocessingRequestStatus Status { get; set; } = PostprocessingRequestStatus.Pending;
        public List<PostprocessingError> Errors { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
    }

    /// <summary>
    /// Request status enumeration
    /// </summary>
    public enum PostprocessingRequestStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Cancelled,
        Timeout
    }

    /// <summary>
    /// Postprocessing error information
    /// </summary>
    public class PostprocessingError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public PostprocessingErrorSeverity Severity { get; set; } = PostprocessingErrorSeverity.Error;
        public bool IsRetryable { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// Error severity levels
    /// </summary>
    public enum PostprocessingErrorSeverity
    {
        Info,
        Warning,
        Error,
        Critical,
        Fatal
    }

    // ============================================================================
    // Error Codes and Categories
    // ============================================================================

    /// <summary>
    /// Standard postprocessing error codes
    /// </summary>
    public static class PostprocessingErrorCodes
    {
        public const string VALIDATION_ERROR = "POSTPROCESSING_VALIDATION_ERROR";
        public const string COMMUNICATION_ERROR = "POSTPROCESSING_COMMUNICATION_ERROR";
        public const string PROCESSING_ERROR = "POSTPROCESSING_PROCESSING_ERROR";
        public const string TIMEOUT_ERROR = "POSTPROCESSING_TIMEOUT_ERROR";
        public const string MEMORY_ERROR = "POSTPROCESSING_MEMORY_ERROR";
        public const string MODEL_ERROR = "POSTPROCESSING_MODEL_ERROR";
        public const string SYSTEM_ERROR = "POSTPROCESSING_SYSTEM_ERROR";
        public const string NETWORK_ERROR = "POSTPROCESSING_NETWORK_ERROR";
        public const string BATCH_PROCESSING_ERROR = "POSTPROCESSING_BATCH_ERROR";
        public const string BATCH_ITEM_FAILED = "POSTPROCESSING_BATCH_ITEM_FAILED";
        public const string PYTHON_WORKER_ERROR = "POSTPROCESSING_PYTHON_WORKER_ERROR";
        public const string INSUFFICIENT_MEMORY = "POSTPROCESSING_INSUFFICIENT_MEMORY";
        public const string PROCESSING_TIMEOUT = "POSTPROCESSING_PROCESSING_TIMEOUT";
        public const string RESOURCE_ALLOCATION_ERROR = "POSTPROCESSING_RESOURCE_ALLOCATION_ERROR";
    }

    /// <summary>
    /// Standard postprocessing error categories
    /// </summary>
    public static class PostprocessingErrorCategories
    {
        public const string VALIDATION = "Validation";
        public const string COMMUNICATION = "Communication";
        public const string PROCESSING = "Processing";
        public const string SYSTEM = "System";
        public const string NETWORK = "Network";
        public const string BATCH = "Batch";
        public const string PERFORMANCE = "Performance";
        public const string RESOURCE = "Resource";
    }
}
