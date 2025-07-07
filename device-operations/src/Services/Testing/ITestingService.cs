using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Testing;

/// <summary>
/// Comprehensive testing service interface for system validation, SDXL testing, and performance benchmarking
/// </summary>
public interface ITestingService
{
    // Core System Testing
    Task<EndToEndTestResult> RunEndToEndTestAsync();
    Task<PerformanceBenchmarkResult> RunPerformanceBenchmarkAsync();
    Task<SystemHealthTestResult> RunSystemHealthTestAsync();
    Task<ComponentIntegrationTestResult> TestComponentIntegrationAsync();
    
    // SDXL-Specific Testing
    Task<SDXLTestResult> RunSDXLComprehensiveTestAsync();
    Task<SDXLInferenceBenchmarkResult> RunSDXLInferenceBenchmarkAsync();
    Task<SDXLModelValidationResult> ValidateSDXLModelsAsync(SDXLModelValidationRequest request);
    Task<SDXLDataQualityResult> TestSDXLDataQualityAsync(SDXLDataQualityRequest request);
    Task<SDXLTrainingTestResult> TestSDXLTrainingCapabilitiesAsync();
    Task<SDXLPerformanceAnalysisResult> AnalyzeSDXLPerformanceAsync(SDXLPerformanceAnalysisRequest request);
    
    // Specialized Testing Operations
    Task<DeviceCompatibilityTestResult> TestDeviceCompatibilityAsync();
    Task<MemoryStressTestResult> RunMemoryStressTestAsync(MemoryStressTestRequest request);
    Task<InferenceStressTestResult> RunInferenceStressTestAsync(InferenceStressTestRequest request);
    Task<TrainingValidationResult> ValidateTrainingEnvironmentAsync();
    
    // Monitoring and Diagnostics
    Task<ContinuousTestResult> StartContinuousTestingAsync(ContinuousTestRequest request);
    Task<ContinuousTestResult> StopContinuousTestingAsync(string testId);
    Task<TestHistoryResult> GetTestHistoryAsync(TestHistoryRequest request);
    Task<SystemDiagnosticsResult> RunSystemDiagnosticsAsync();
}

// Core Test Result Models
public class EndToEndTestResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool OverallSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<TestPhaseResult> Phases { get; set; } = new();
    public TestMetrics Metrics { get; set; } = new();
    public SystemResourceUsage ResourceUsage { get; set; } = new();
}

public class TestPhaseResult
{
    public string PhaseName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string Details { get; set; } = string.Empty;
    public Dictionary<string, object> Metrics { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class PerformanceBenchmarkResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public double MemoryOperationsPerSecond { get; set; }
    public double DeviceListingsPerSecond { get; set; }
    public double HealthChecksPerSecond { get; set; }
    public double InferenceRequestsPerSecond { get; set; }
    public double TrainingOperationsPerSecond { get; set; }
    public PerformanceProfile Profile { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

// SDXL Test Result Models
public class SDXLTestResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool OverallSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<TestPhaseResult> Phases { get; set; } = new();
    public SDXLCapabilityAssessment Capabilities { get; set; } = new();
    public SDXLCompatibilityReport Compatibility { get; set; } = new();
}

public class SDXLInferenceBenchmarkResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public double AverageTimePerImage { get; set; }
    public List<SDXLBenchmarkConfiguration> Results { get; set; } = new();
    public SDXLPerformanceProfile PerformanceProfile { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class SDXLModelValidationResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public ModelValidationDetails? BaseModelValidation { get; set; }
    public ModelValidationDetails? RefinerModelValidation { get; set; }
    public ModelValidationDetails? VaeModelValidation { get; set; }
    public CompatibilityAssessment? CompatibilityCheck { get; set; }
    public bool OverallValid { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class SDXLDataQualityResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string DatasetPath { get; set; } = string.Empty;
    public DataQualityMetrics Metrics { get; set; } = new();
    public List<DataQualityIssue> Issues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DataQualityScore Score { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

// Specialized Test Result Models
public class SystemHealthTestResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime TestTime { get; set; }
    public bool IsHealthy { get; set; }
    public List<HealthCheckResult> HealthChecks { get; set; } = new();
    public SystemResourceStatus ResourceStatus { get; set; } = new();
    public List<string> CriticalIssues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ComponentIntegrationTestResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool AllComponentsIntegrated { get; set; }
    public List<ComponentTestResult> ComponentResults { get; set; } = new();
    public IntegrationMatrix IntegrationMatrix { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class DeviceCompatibilityTestResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime TestTime { get; set; }
    public List<DeviceCompatibilityReport> DeviceReports { get; set; } = new();
    public CompatibilitySummary Summary { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class MemoryStressTestResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public long MaxMemoryAllocated { get; set; }
    public double MemoryEfficiency { get; set; }
    public List<MemoryAllocationPhase> Phases { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class InferenceStressTestResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public double RequestsPerSecond { get; set; }
    public double AverageResponseTime { get; set; }
    public List<InferenceStressPhase> Phases { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

// Support Classes
public class TestMetrics
{
    public double TotalTestTime { get; set; }
    public int TestsPassed { get; set; }
    public int TestsFailed { get; set; }
    public double SuccessRate { get; set; }
    public Dictionary<string, double> PhaseTimings { get; set; } = new();
}

public class SystemResourceUsage
{
    public double CPUUsage { get; set; }
    public long MemoryUsage { get; set; }
    public double GPUUsage { get; set; }
    public long VRAMUsage { get; set; }
    public double DiskIO { get; set; }
    public double NetworkIO { get; set; }
}

public class PerformanceProfile
{
    public string ProfileName { get; set; } = string.Empty;
    public Dictionary<string, double> Benchmarks { get; set; } = new();
    public string PerformanceRating { get; set; } = string.Empty;
    public List<string> BottleneckAreas { get; set; } = new();
}

public class SDXLCapabilityAssessment
{
    public bool SupportsSDXL { get; set; }
    public List<string> SupportedResolutions { get; set; } = new();
    public List<string> SupportedTechniques { get; set; } = new();
    public long MaxVRAMAvailable { get; set; }
    public int RecommendedBatchSize { get; set; }
    public string PerformanceTier { get; set; } = string.Empty;
}

public class SDXLCompatibilityReport
{
    public bool IsCompatible { get; set; }
    public List<string> CompatibleDevices { get; set; } = new();
    public List<string> IncompatibleDevices { get; set; } = new();
    public List<string> RequiredUpdates { get; set; } = new();
    public string CompatibilityLevel { get; set; } = string.Empty;
}

public class SDXLBenchmarkConfiguration
{
    public string ConfigurationName { get; set; } = string.Empty;
    public int Resolution { get; set; }
    public int BatchSize { get; set; }
    public int Steps { get; set; }
    public long ExecutionTimeMs { get; set; }
    public bool Success { get; set; }
    public long MemoryUsedMB { get; set; }
    public string Details { get; set; } = string.Empty;
    public double ThroughputScore { get; set; }
}

public class SDXLPerformanceProfile
{
    public string PerformanceTier { get; set; } = string.Empty;
    public double OptimalResolution { get; set; }
    public int OptimalBatchSize { get; set; }
    public List<string> OptimizationSuggestions { get; set; } = new();
    public Dictionary<string, double> BenchmarkScores { get; set; } = new();
}

// Request Models
public class SDXLModelValidationRequest
{
    public string? BaseModelPath { get; set; }
    public string? RefinerModelPath { get; set; }
    public string? VaeModelPath { get; set; }
    public bool ValidateCompatibility { get; set; } = true;
    public bool DeepValidation { get; set; } = false;
}

public class SDXLDataQualityRequest
{
    public string DatasetPath { get; set; } = string.Empty;
    public string TrainingTechnique { get; set; } = "LoRA";
    public bool AnalyzeCaptions { get; set; } = true;
    public bool GenerateRecommendations { get; set; } = true;
}

public class SDXLTrainingTestResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime TestTime { get; set; }
    public bool CanTrainSDXL { get; set; }
    public List<string> SupportedTechniques { get; set; } = new();
    public TrainingCapabilityMatrix Capabilities { get; set; } = new();
    public List<string> Limitations { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class SDXLPerformanceAnalysisRequest
{
    public List<string> TestConfigurations { get; set; } = new();
    public int MaxTestDuration { get; set; } = 300; // 5 minutes
    public bool IncludeMemoryAnalysis { get; set; } = true;
    public bool IncludeThermalAnalysis { get; set; } = false;
}

public class SDXLPerformanceAnalysisResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<PerformanceTestResult> Results { get; set; } = new();
    public PerformanceOptimizationSuggestions Optimizations { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class MemoryStressTestRequest
{
    public string DeviceId { get; set; } = "gpu_0";
    public long MaxMemoryToTest { get; set; }
    public int TestDurationMinutes { get; set; } = 5;
    public int AllocationSteps { get; set; } = 10;
}

public class InferenceStressTestRequest
{
    public string ModelType { get; set; } = "SDXL";
    public int ConcurrentRequests { get; set; } = 5;
    public int TestDurationMinutes { get; set; } = 5;
    public string TestPrompt { get; set; } = "A test image";
}

public class ContinuousTestRequest
{
    public string TestType { get; set; } = "health";
    public int IntervalMinutes { get; set; } = 15;
    public List<string> TestComponents { get; set; } = new();
    public Dictionary<string, object> TestParameters { get; set; } = new();
}

public class ContinuousTestResult
{
    public string TestId { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int TestIterations { get; set; }
    public List<TestIterationResult> Iterations { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class TestHistoryRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? TestType { get; set; }
    public bool? SuccessOnly { get; set; }
    public int MaxResults { get; set; } = 100;
}

public class TestHistoryResult
{
    public List<TestHistoryEntry> Tests { get; set; } = new();
    public TestHistoryStatistics Statistics { get; set; } = new();
    public int TotalCount { get; set; }
}

public class TrainingValidationResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime TestTime { get; set; }
    public bool IsValidEnvironment { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
    public EnvironmentCapabilities Capabilities { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class SystemDiagnosticsResult
{
    public string TestId { get; set; } = string.Empty;
    public DateTime DiagnosticsTime { get; set; }
    public SystemHealthOverview Health { get; set; } = new();
    public List<DiagnosticTest> Tests { get; set; } = new();
    public List<SystemRecommendation> Recommendations { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

// Additional Support Classes
public class ModelValidationDetails
{
    public string ModelType { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public long FileSizeMB { get; set; }
    public string FileFormat { get; set; } = string.Empty;
    public List<string> Issues { get; set; } = new();
    public string Details { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class CompatibilityAssessment
{
    public bool IsCompatible { get; set; }
    public List<string> Issues { get; set; } = new();
    public string Details { get; set; } = string.Empty;
    public double CompatibilityScore { get; set; }
}

public class DataQualityMetrics
{
    public int ImageCount { get; set; }
    public int CaptionCount { get; set; }
    public double AverageFileSize { get; set; }
    public double CaptionCoverage { get; set; }
    public List<string> ImageFormats { get; set; } = new();
    public string DominantResolution { get; set; } = string.Empty;
}

public class DataQualityIssue
{
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}

public class DataQualityScore
{
    public double OverallScore { get; set; }
    public double ImageQualityScore { get; set; }
    public double CaptionQualityScore { get; set; }
    public double DatasetSizeScore { get; set; }
    public string Grade { get; set; } = string.Empty;
}

public class HealthCheckResult
{
    public string Component { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object> Metrics { get; set; } = new();
    public List<string> Issues { get; set; } = new();
}

public class SystemResourceStatus
{
    public double CPUUsage { get; set; }
    public long TotalMemoryGB { get; set; }
    public long AvailableMemoryGB { get; set; }
    public List<GpuResourceStatus> GPUs { get; set; } = new();
    public long DiskSpaceGB { get; set; }
}

public class GpuResourceStatus
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double UtilizationPercentage { get; set; }
    public long TotalVRAMGB { get; set; }
    public long UsedVRAMGB { get; set; }
    public double Temperature { get; set; }
}

public class ComponentTestResult
{
    public string ComponentName { get; set; } = string.Empty;
    public bool IsOperational { get; set; }
    public string Version { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
    public Dictionary<string, object> TestResults { get; set; } = new();
}

public class IntegrationMatrix
{
    public Dictionary<string, Dictionary<string, bool>> ComponentConnections { get; set; } = new();
    public double IntegrationScore { get; set; }
    public List<string> MissingConnections { get; set; } = new();
}

public class DeviceCompatibilityReport
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public bool IsCompatible { get; set; }
    public List<string> SupportedFeatures { get; set; } = new();
    public List<string> UnsupportedFeatures { get; set; } = new();
    public string CompatibilityLevel { get; set; } = string.Empty;
}

public class CompatibilitySummary
{
    public int TotalDevices { get; set; }
    public int CompatibleDevices { get; set; }
    public int PartiallyCompatibleDevices { get; set; }
    public int IncompatibleDevices { get; set; }
    public string OverallCompatibility { get; set; } = string.Empty;
}

public class MemoryAllocationPhase
{
    public string PhaseName { get; set; } = string.Empty;
    public long AllocationSize { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class InferenceStressPhase
{
    public string PhaseName { get; set; } = string.Empty;
    public int ConcurrentRequests { get; set; }
    public TimeSpan Duration { get; set; }
    public int SuccessfulRequests { get; set; }
    public double AverageResponseTime { get; set; }
}

public class TrainingCapabilityMatrix
{
    public Dictionary<string, bool> TechniqueSupport { get; set; } = new();
    public Dictionary<string, int> MaxResolutions { get; set; } = new();
    public Dictionary<string, int> OptimalBatchSizes { get; set; } = new();
}

public class PerformanceTestResult
{
    public string TestName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public double Score { get; set; }
    public string Units { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
}

public class PerformanceOptimizationSuggestions
{
    public List<string> MemoryOptimizations { get; set; } = new();
    public List<string> ProcessingOptimizations { get; set; } = new();
    public List<string> ConfigurationChanges { get; set; } = new();
    public double PotentialSpeedup { get; set; }
}

public class TestIterationResult
{
    public int IterationNumber { get; set; }
    public DateTime TestTime { get; set; }
    public bool Success { get; set; }
    public Dictionary<string, object> Results { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class TestHistoryEntry
{
    public string TestId { get; set; } = string.Empty;
    public string TestType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class TestHistoryStatistics
{
    public int TotalTests { get; set; }
    public int SuccessfulTests { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageTestDuration { get; set; }
    public Dictionary<string, int> TestTypeBreakdown { get; set; } = new();
}

public class ValidationIssue
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Resolution { get; set; }
}

public class EnvironmentCapabilities
{
    public bool SupportsTraining { get; set; }
    public bool SupportsInference { get; set; }
    public bool SupportsSDXL { get; set; }
    public List<string> AvailableOptimizers { get; set; } = new();
    public List<string> SupportedModelFormats { get; set; } = new();
}

public class SystemHealthOverview
{
    public string OverallStatus { get; set; } = string.Empty;
    public int HealthScore { get; set; }
    public List<string> CriticalIssues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTime LastFullCheck { get; set; }
}

public class DiagnosticTest
{
    public string TestName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Result { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
}

public class SystemRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public List<string> Actions { get; set; } = new();
}
