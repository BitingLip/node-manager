using DeviceOperations.Models.Common;

namespace DeviceOperations.Models.Responses
{
    /// <summary>
    /// Response model for listing available workflows
    /// </summary>
    public class GetProcessingWorkflowsResponse
    {
        /// <summary>
        /// List of available workflows
        /// </summary>
        public List<WorkflowInfo> Workflows { get; set; } = new();

        /// <summary>
        /// Total count of workflows
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Response model for getting a specific workflow
    /// </summary>
    public class GetProcessingWorkflowResponse
    {
        /// <summary>
        /// Workflow information
        /// </summary>
        public WorkflowInfo Workflow { get; set; } = new();
    }

    /// <summary>
    /// Response model for executing a workflow
    /// </summary>
    public class PostWorkflowExecuteResponse
    {
        /// <summary>
        /// Execution ID for tracking
        /// </summary>
        public string ExecutionId { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the execution
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Estimated completion time
        /// </summary>
        public DateTime? EstimatedCompletion { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double Progress { get; set; }
    }

    /// <summary>
    /// Response model for listing processing sessions
    /// </summary>
    public class GetProcessingSessionsResponse
    {
        /// <summary>
        /// List of active sessions
        /// </summary>
        public List<ProcessingSession> Sessions { get; set; } = new();

        /// <summary>
        /// Total count of sessions
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Response model for getting a specific processing session
    /// </summary>
    public class GetProcessingSessionResponse
    {
        /// <summary>
        /// Session information
        /// </summary>
        public ProcessingSession Session { get; set; } = new();
    }

    /// <summary>
    /// Response model for session control operations
    /// </summary>
    public class PostSessionControlResponse
    {
        /// <summary>
        /// Action that was performed
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Result of the action
        /// </summary>
        public string Result { get; set; } = string.Empty;

        /// <summary>
        /// New session status
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for deleting a processing session
    /// </summary>
    public class DeleteProcessingSessionResponse
    {
        /// <summary>
        /// Whether the session was successfully deleted
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Additional message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for listing batch jobs
    /// </summary>
    public class GetProcessingBatchesResponse
    {
        /// <summary>
        /// List of batch jobs
        /// </summary>
        public List<BatchJob> Batches { get; set; } = new();

        /// <summary>
        /// Total count of batches
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Response model for getting a specific batch job
    /// </summary>
    public class GetProcessingBatchResponse
    {
        /// <summary>
        /// Batch job information
        /// </summary>
        public BatchJob Batch { get; set; } = new();
    }

    /// <summary>
    /// Response model for creating a batch job
    /// </summary>
    public class PostBatchCreateResponse
    {
        /// <summary>
        /// ID of the created batch job
        /// </summary>
        public string BatchId { get; set; } = string.Empty;

        /// <summary>
        /// Status of the batch job
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Estimated processing time
        /// </summary>
        public TimeSpan? EstimatedDuration { get; set; }
    }

    /// <summary>
    /// Response model for executing a batch job
    /// </summary>
    public class PostBatchExecuteResponse
    {
        /// <summary>
        /// Execution ID for tracking
        /// </summary>
        public string ExecutionId { get; set; } = string.Empty;

        /// <summary>
        /// Current status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Progress information
        /// </summary>
        public BatchProgress Progress { get; set; } = new();
    }

    /// <summary>
    /// Response model for deleting a batch job
    /// </summary>
    public class DeleteProcessingBatchResponse
    {
        /// <summary>
        /// Whether the batch was successfully deleted
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Additional message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Information about a workflow
    /// </summary>
    public class WorkflowInfo
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Workflow version
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Required parameters
        /// </summary>
        public List<WorkflowParameter> Parameters { get; set; } = new();

        /// <summary>
        /// Estimated execution time
        /// </summary>
        public TimeSpan? EstimatedDuration { get; set; }

        /// <summary>
        /// Resource requirements
        /// </summary>
        public Dictionary<string, object> ResourceRequirements { get; set; } = new();
    }

    /// <summary>
    /// Information about a processing session
    /// </summary>
    public class ProcessingSession
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Session name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Current status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Creation time
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last update time
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Associated workflow ID
        /// </summary>
        public string? WorkflowId { get; set; }

        /// <summary>
        /// Session configuration
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();

        /// <summary>
        /// Resource usage
        /// </summary>
        public Dictionary<string, object> ResourceUsage { get; set; } = new();
    }

    /// <summary>
    /// Information about a batch job
    /// </summary>
    public class BatchJob
    {
        /// <summary>
        /// Batch identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Batch name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Batch type
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Current status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Creation time
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Start time
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Completion time
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Total items in batch
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Items completed
        /// </summary>
        public int CompletedItems { get; set; }

        /// <summary>
        /// Items failed
        /// </summary>
        public int FailedItems { get; set; }

        /// <summary>
        /// Progress information
        /// </summary>
        public BatchProgress Progress { get; set; } = new();

        /// <summary>
        /// Configuration used
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// Progress information for batch processing
    /// </summary>
    public class BatchProgress
    {
        /// <summary>
        /// Percentage completed (0-100)
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Items processed
        /// </summary>
        public int ItemsProcessed { get; set; }

        /// <summary>
        /// Total items
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Estimated time remaining
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Processing rate (items per second)
        /// </summary>
        public double ProcessingRate { get; set; }
    }

    /// <summary>
    /// Parameter definition for a workflow
    /// </summary>
    public class WorkflowParameter
    {
        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Parameter type
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Whether the parameter is required
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Default value
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Parameter description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Validation rules
        /// </summary>
        public Dictionary<string, object> Validation { get; set; } = new();
    }
}
