using System.Collections.Concurrent;

namespace DeviceOperations.Models.Common
{
    /// <summary>
    /// Processing workflow definition with steps and requirements
    /// </summary>
    public class ProcessingWorkflow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
        public string Category { get; set; } = "General";
        public TimeSpan EstimatedDuration { get; set; }
        public int UsageCount { get; set; }
        public DateTime LastUsed { get; set; }
        public List<WorkflowStep> Steps { get; set; } = new();
        public Dictionary<string, object> ResourceRequirements { get; set; } = new();
        public List<string> RequiredModels { get; set; } = new();
        public bool IsAvailable { get; set; } = true;
        public float AverageRating { get; set; } = 4.5f;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Processing session representing an active workflow execution
    /// </summary>
    public class ProcessingSession
    {
        public string Id { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
        public ProcessingStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public int Progress { get; set; }
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// Processing batch for handling multiple items
    /// </summary>
    public class ProcessingBatch
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public int FailedItems { get; set; }
        public ProcessingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
        public int Priority { get; set; } = 0;
    }

    /// <summary>
    /// Workflow definition containing the complete workflow specification
    /// </summary>
    public class WorkflowDefinition
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TimeSpan EstimatedDuration { get; set; }
        public List<WorkflowStep> Steps { get; set; } = new();
        public List<string> RequiredModels { get; set; } = new();
        public Dictionary<string, object> Requirements { get; set; } = new();
        public Dictionary<string, object> ResourceRequirements { get; set; } = new();
    }

    /// <summary>
    /// Individual step within a workflow
    /// </summary>
    public class WorkflowStep
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool IsRequired { get; set; } = true;
        public int Order { get; set; }
        public int StepNumber { get; set; }
        public List<string> RequiredModels { get; set; } = new();
    }

    /// <summary>
    /// Status information for a domain involved in processing
    /// </summary>
    public class DomainSessionStatus
    {
        public string Domain { get; set; } = string.Empty;
        public ProcessingStatus Status { get; set; }
        public int Progress { get; set; }
        public string? CurrentStep { get; set; }
        public string? CurrentOperation { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastUpdate { get; set; }
        public DateTime LastUpdated { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of session status aggregation
    /// </summary>
    public class SessionStatusResult
    {
        public ProcessingStatus Status { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of domain control operations
    /// </summary>
    public class DomainControlResult
    {
        public string Domain { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of domain cleanup operations
    /// </summary>
    public class DomainCleanupResult
    {
        public string Domain { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Execution history item for workflow tracking
    /// </summary>
    public class ExecutionHistoryItem
    {
        public DateTime ExecutedAt { get; set; }
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public string? UserId { get; set; }
    }

    /// <summary>
    /// Batch item detail for individual batch processing
    /// </summary>
    public class BatchItemDetail
    {
        public string ItemId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Progress { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Step execution result for domain routing
    /// </summary>
    public class StepExecutionResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Error { get; set; }
        public dynamic? StepData { get; set; }
        public ResourceUsage? ResourceUsage { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Workflow step definition for domain routing
    /// </summary>
    public class WorkflowStepDefinition
    {
        public string StepId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; }
        public int StepNumber { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public List<string> RequiredModels { get; set; } = new();
        public bool IsOptional { get; set; }
    }

    /// <summary>
    /// Resource usage tracking for monitoring
    /// </summary>
    public class ResourceUsage
    {
        public string MemoryUsed { get; set; } = string.Empty;
        public string GpuMemoryUsed { get; set; } = string.Empty;
        public double CpuUsage { get; set; }
        public double GpuUsage { get; set; }
        public TimeSpan Duration { get; set; }
        public int MemoryUsage { get; set; }
        public int VramUsage { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Workflow execution result for session management
    /// </summary>
    public class WorkflowExecutionResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Error { get; set; }
        public List<StepExecutionResult> StepResults { get; set; } = new();
        public Dictionary<string, object> Outputs { get; set; } = new();
        public ResourceUsage? TotalResourceUsage { get; set; }
        public ResourceUsage? ResourceUsage { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
    }

    /// <summary>
    /// Batch cleanup result for multi-domain coordination
    /// </summary>
    public class BatchCleanupResult
    {
        public string Domain { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Processing status enumeration
    /// </summary>
    public enum ProcessingStatus
    {
        Unknown = 0,
        Pending = 1,
        Running = 2,
        Paused = 3,
        Completed = 4,
        Failed = 5,
        Cancelled = 6,
        Error = 7,
        Created = 8,
        Stopped = 9
    }

    /// <summary>
    /// Parameter definition for a workflow
    /// </summary>
    public class WorkflowParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Required { get; set; }
        public object? DefaultValue { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Validation { get; set; } = new();
    }
}
