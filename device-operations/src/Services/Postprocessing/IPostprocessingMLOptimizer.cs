using DeviceOperations.Models.Postprocessing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeviceOperations.Services.Postprocessing
{
    /// <summary>
    /// Interface for machine learning-based postprocessing optimization
    /// </summary>
    public interface IPostprocessingMLOptimizer
    {
        /// <summary>
        /// Get ML-based connection recommendations based on request patterns
        /// </summary>
        Task<MLConnectionRecommendation> GetConnectionRecommendation(RequestPattern pattern);
        
        /// <summary>
        /// Update ML model with performance data
        /// </summary>
        Task UpdateModel(PostprocessingPerformanceData performanceData);
        
        /// <summary>
        /// Determine if model should be retrained
        /// </summary>
        Task<bool> ShouldRetrain();
        
        /// <summary>
        /// Retrain ML model with accumulated data
        /// </summary>
        Task RetrainModel();
        
        /// <summary>
        /// Generate ML predictions based on request traces
        /// </summary>
        Task<MLPredictions> GeneratePredictions(List<PostprocessingRequestTrace> traces);
        
        /// <summary>
        /// Get system load adjustment factor
        /// </summary>
        Task<double> CalculateSystemLoadAdjustment();
    }

    /// <summary>
    /// Interface for analyzing connection patterns
    /// </summary>
    public interface IConnectionPatternAnalyzer
    {
        /// <summary>
        /// Analyze request pattern for optimization
        /// </summary>
        Task<RequestPattern> AnalyzeRequestPattern(object request);
        
        /// <summary>
        /// Record pattern outcome for learning
        /// </summary>
        Task RecordPatternOutcome(PostprocessingPerformanceData performanceData);
        
        /// <summary>
        /// Analyze long-term trends in request patterns
        /// </summary>
        Task<PatternAnalysis> AnalyzeLongTermTrends(List<PostprocessingRequestTrace> traces);
        
        /// <summary>
        /// Analyze resource requirements for a request
        /// </summary>
        Task<ResourceRequirements> AnalyzeResourceRequirements(object request);
    }

    /// <summary>
    /// ML connection recommendation data
    /// </summary>
    public class MLConnectionRecommendation
    {
        public int? OptimalPoolSize { get; set; }
        public int? OptimalTimeout { get; set; }
        public string? OptimizationStrategy { get; set; }
        public TimeSpan PredictedProcessingTime { get; set; }
        public RetryStrategy RetryStrategy { get; set; } = new RetryStrategy();
        public bool EnableAdaptivePooling { get; set; }
        public LoadBalancingStrategy LoadBalancingStrategy { get; set; }
        public double ConfidenceScore { get; set; }
    }

    /// <summary>
    /// ML predictions for performance and resource utilization
    /// </summary>
    public class MLPredictions
    {
        public List<double> LoadPredictions { get; set; } = new();
        public double LoadConfidence { get; set; }
        public double ExpectedThroughput { get; set; }
        public TimeSpan ExpectedLatency { get; set; }
        public ResourceForecast ResourceForecast { get; set; } = new();
        public double MemoryUtilizationTrend { get; set; }
        public double MemoryBottleneckETA { get; set; }
        public string MemoryBottleneckSeverity { get; set; } = string.Empty;
        public double MemoryPredictionConfidence { get; set; }
        public double ConnectionUtilizationTrend { get; set; }
        public double ConnectionBottleneckETA { get; set; }
        public string ConnectionBottleneckSeverity { get; set; } = string.Empty;
        public double ConnectionPredictionConfidence { get; set; }
        public double ProcessingCapacityTrend { get; set; }
        public double ProcessingBottleneckETA { get; set; }
        public string ProcessingBottleneckSeverity { get; set; } = string.Empty;
        public double ProcessingPredictionConfidence { get; set; }
        public double PredictedLoadIncrease { get; set; }
        public double TargetLatencyMs { get; set; }
    }

    /// <summary>
    /// Request pattern analysis data
    /// </summary>
    public class RequestPattern
    {
        public string RequestType { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public TimeSpan ExpectedDuration { get; set; }
        public ResourceRequirements ResourceRequirements { get; set; } = new();
        public double ComplexityScore { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Resource requirements analysis
    /// </summary>
    public class ResourceRequirements
    {
        public long EstimatedMemoryMB { get; set; }
        public double EstimatedCpuUtilization { get; set; }
        public double EstimatedGpuUtilization { get; set; }
        public int EstimatedConcurrency { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
    }

    /// <summary>
    /// Pattern analysis results
    /// </summary>
    public class PatternAnalysis
    {
        public Dictionary<string, double> PatternTrends { get; set; } = new();
        public List<string> IdentifiedPatterns { get; set; } = new();
        public double PatternStability { get; set; }
        public DateTime AnalysisTime { get; set; } = DateTime.UtcNow;
        public double OptimizationPotential { get; set; }
        public List<string> RecommendedOptimizations { get; set; } = new();
        public double ExpectedImprovement { get; set; }
    }

    /// <summary>
    /// Resource forecast data
    /// </summary>
    public class ResourceForecast
    {
        public double PredictedMemoryUsage { get; set; }
        public double PredictedCpuUsage { get; set; }
        public double PredictedGpuUsage { get; set; }
        public TimeSpan ForecastWindow { get; set; }
        public double ConfidenceLevel { get; set; }
    }

    /// <summary>
    /// Retry strategy configuration
    /// </summary>
    public class RetryStrategy
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
        public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;
        public JitterStrategy JitterStrategy { get; set; } = JitterStrategy.Equal;
    }

    /// <summary>
    /// Load balancing strategy
    /// </summary>
    public enum LoadBalancingStrategy
    {
        RoundRobin,
        LeastConnections,
        WeightedRoundRobin,
        ResourceBased,
        Adaptive
    }

    /// <summary>
    /// Backoff strategy for retries
    /// </summary>
    public enum BackoffStrategy
    {
        Linear,
        Exponential,
        Polynomial,
        Fibonacci
    }

    /// <summary>
    /// Jitter strategy for retries
    /// </summary>
    public enum JitterStrategy
    {
        None,
        Equal,
        Full,
        Decorrelated
    }

    /// <summary>
    /// Capacity recommendation for scaling
    /// </summary>
    public class CapacityRecommendation
    {
        public string RecommendationType { get; set; } = string.Empty;
        public double RecommendedIncrease { get; set; }
        public string Reason { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public DateTime ImplementationSuggestion { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
        public string EstimatedBenefit { get; set; } = string.Empty;
        public string ImplementationComplexity { get; set; } = string.Empty;
        public string EstimatedCost { get; set; } = string.Empty;
    }

    /// <summary>
    /// Performance data for ML training
    /// </summary>
    public class PostprocessingPerformanceData
    {
        public string RequestId { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double AverageLatencyMs { get; set; }
        public double RequestsPerSecond { get; set; }
        public double MemoryUsageMB { get; set; }
        public double CpuUtilization { get; set; }
        public double GpuUtilization { get; set; }
        public double ErrorRate { get; set; }
        public bool Success { get; set; }
        public string ErrorType { get; set; } = string.Empty;
        public Dictionary<string, object> Metrics { get; set; } = new();
    }
}
