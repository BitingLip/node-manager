using System;
using System.Collections.Generic;

namespace DeviceOperations.Models.Postprocessing
{
    /// <summary>
    /// Interface for postprocessing model objects
    /// </summary>
    public interface IPostprocessingModel
    {
        string ModelId { get; set; }
        PostprocessingModelType ModelType { get; set; }
        Dictionary<string, object> Capabilities { get; set; }
        OptimizationLevel OptimizationLevel { get; set; }
        MemoryRequirements MemoryRequirements { get; set; }
        PostprocessingPerformanceMetrics? PerformanceMetrics { get; set; }
    }

    /// <summary>
    /// Interface for postprocessing configuration objects
    /// </summary>
    public interface IPostprocessingConfiguration
    {
        string ConfigId { get; set; }
        ConfigurationType ConfigType { get; set; }
        Dictionary<string, object> Parameters { get; set; }
        bool Enabled { get; set; }
        int Priority { get; set; }
        ValidationSettings? Validation { get; set; }
    }

    /// <summary>
    /// Interface for postprocessing metrics objects
    /// </summary>
    public interface IPostprocessingMetrics
    {
        string MetricsId { get; set; }
        DateTime Timestamp { get; set; }
        Dictionary<string, object> PerformanceData { get; set; }
        Dictionary<string, object> ResourceUsage { get; set; }
        Dictionary<string, object> QualityMetrics { get; set; }
        Dictionary<string, object> ErrorData { get; set; }
    }

    /// <summary>
    /// Interface for custom postprocessing types
    /// </summary>
    public interface ICustomPostprocessingType
    {
        string TypeId { get; set; }
        Dictionary<string, object> Properties { get; set; }
    }

    /// <summary>
    /// Optimization level enumeration
    /// </summary>
    public enum OptimizationLevel
    {
        None,
        Basic,
        Standard,
        Advanced,
        Maximum
    }

    /// <summary>
    /// Configuration type enumeration
    /// </summary>
    public enum ConfigurationType
    {
        Model,
        Pipeline,
        Performance,
        Safety,
        Custom
    }

    /// <summary>
    /// Memory requirements specification
    /// </summary>
    public class MemoryRequirements
    {
        public long MinimumMemoryMB { get; set; }
        public long RecommendedMemoryMB { get; set; }
        public long MaximumMemoryMB { get; set; }
        public bool RequiresGPU { get; set; }
        public int MinimumVRAMMB { get; set; }
    }

    /// <summary>
    /// Validation settings for configurations
    /// </summary>
    public class ValidationSettings
    {
        public bool StrictValidation { get; set; }
        public List<string> RequiredFields { get; set; } = new();
        public Dictionary<string, object> ValidationRules { get; set; } = new();
    }

    /// <summary>
    /// Transformation test result data
    /// </summary>
    public class TransformationTestResult
    {
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public double AccuracyScore { get; set; }
        public bool ComplexObjectSupport { get; set; }
        public bool EdgeCaseHandling { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
        public TimeSpan TestDuration { get; set; }
    }

    /// <summary>
    /// Individual transformation accuracy test
    /// </summary>
    public class TransformationAccuracyTest
    {
        public string TestName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public double AccuracyScore { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan TestDuration { get; set; }
        public Dictionary<string, object> TestData { get; set; } = new();
    }

    /// <summary>
    /// Test case data for transformation testing
    /// </summary>
    public class TransformationTestCase
    {
        public string TestName { get; set; } = string.Empty;
        public Dictionary<string, object> InputData { get; set; } = new();
        public Dictionary<string, object> ExpectedOutput { get; set; } = new();
        public bool IsComplexObject { get; set; }
        public bool IsLargePayload { get; set; }
    }
}
