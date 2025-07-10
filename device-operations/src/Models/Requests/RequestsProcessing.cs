using DeviceOperations.Models.Common;

namespace DeviceOperations.Models.Requests
{
    /// <summary>
    /// Request model for executing a workflow
    /// </summary>
    public class PostWorkflowExecuteRequest
    {
        /// <summary>
        /// ID of the workflow to execute
        /// </summary>
        public string WorkflowId { get; set; } = string.Empty;

        /// <summary>
        /// Input parameters for the workflow
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// Priority level for execution (1-10, higher is more priority)
        /// </summary>
        public int Priority { get; set; } = 5;

        /// <summary>
        /// Whether to execute in background
        /// </summary>
        public bool Background { get; set; } = false;
    }

    /// <summary>
    /// Request model for controlling a session
    /// </summary>
    public class PostSessionControlRequest
    {
        /// <summary>
        /// Action to perform on the session
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Additional parameters for the action
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Request model for creating a batch processing job
    /// </summary>
    public class PostBatchCreateRequest
    {
        /// <summary>
        /// Name of the batch job
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Type of batch processing
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// List of items to process in the batch
        /// </summary>
        public List<BatchItem> Items { get; set; } = new();

        /// <summary>
        /// Configuration for the batch job
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();

        /// <summary>
        /// Priority level for the batch (1-10)
        /// </summary>
        public int Priority { get; set; } = 5;
    }

    /// <summary>
    /// Request model for executing a batch
    /// </summary>
    public class PostBatchExecuteRequest
    {
        /// <summary>
        /// Execution parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// Whether to execute in background
        /// </summary>
        public bool Background { get; set; } = true;
    }

    /// <summary>
    /// Represents an item in a batch processing job
    /// </summary>
    public class BatchItem
    {
        /// <summary>
        /// Unique identifier for the item
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Type of the item
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Input data for the item
        /// </summary>
        public Dictionary<string, object> Input { get; set; } = new();

        /// <summary>
        /// Item-specific configuration
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();
    }
}
