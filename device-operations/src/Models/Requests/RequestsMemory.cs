namespace DeviceOperations.Models.Requests;

using DeviceOperations.Models.Common;

/// <summary>
/// Memory operation requests
/// </summary>
public static class RequestsMemory
{
    /// <summary>
    /// Request to get memory status for a device
    /// </summary>
    public class GetMemoryStatusRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Include allocation details
        /// </summary>
        public bool IncludeAllocations { get; set; } = true;

        /// <summary>
        /// Include usage statistics
        /// </summary>
        public bool IncludeUsageStats { get; set; } = true;

        /// <summary>
        /// Include fragmentation information
        /// </summary>
        public bool IncludeFragmentation { get; set; } = false;
    }

    /// <summary>
    /// Request to allocate memory on a device
    /// </summary>
    public class AllocateMemoryRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Memory size to allocate in bytes
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Memory allocation type
        /// </summary>
        public MemoryAllocationType AllocationType { get; set; }

        /// <summary>
        /// Memory alignment requirements
        /// </summary>
        public int Alignment { get; set; } = 0;

        /// <summary>
        /// Allocation purpose/tag
        /// </summary>
        public string Purpose { get; set; } = string.Empty;

        /// <summary>
        /// Whether allocation should be persistent
        /// </summary>
        public bool Persistent { get; set; } = false;

        /// <summary>
        /// Allocation timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Request to deallocate memory
    /// </summary>
    public class DeallocateMemoryRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Memory allocation ID to deallocate
        /// </summary>
        public string AllocationId { get; set; } = string.Empty;

        /// <summary>
        /// Force deallocation even if memory is in use
        /// </summary>
        public bool Force { get; set; } = false;

        /// <summary>
        /// Wait for deallocation completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;
    }

    /// <summary>
    /// Request to transfer memory between devices
    /// </summary>
    public class TransferMemoryRequest
    {
        /// <summary>
        /// Source device identifier
        /// </summary>
        public string SourceDeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Destination device identifier
        /// </summary>
        public string DestinationDeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Source allocation ID
        /// </summary>
        public string SourceAllocationId { get; set; } = string.Empty;

        /// <summary>
        /// Size to transfer in bytes (optional, defaults to full allocation)
        /// </summary>
        public long? SizeBytes { get; set; }

        /// <summary>
        /// Transfer type
        /// </summary>
        public MemoryTransferType TransferType { get; set; }

        /// <summary>
        /// Transfer priority
        /// </summary>
        public TransferPriority Priority { get; set; } = TransferPriority.Normal;

        /// <summary>
        /// Wait for transfer completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;

        /// <summary>
        /// Transfer timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;
    }

    /// <summary>
    /// Request to copy memory within the same device
    /// </summary>
    public class CopyMemoryRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Source allocation ID
        /// </summary>
        public string SourceAllocationId { get; set; } = string.Empty;

        /// <summary>
        /// Destination allocation ID
        /// </summary>
        public string DestinationAllocationId { get; set; } = string.Empty;

        /// <summary>
        /// Source offset in bytes
        /// </summary>
        public long SourceOffset { get; set; } = 0;

        /// <summary>
        /// Destination offset in bytes
        /// </summary>
        public long DestinationOffset { get; set; } = 0;

        /// <summary>
        /// Size to copy in bytes
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Copy type
        /// </summary>
        public MemoryCopyType CopyType { get; set; }

        /// <summary>
        /// Wait for copy completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;
    }

    /// <summary>
    /// Request to clear/zero memory
    /// </summary>
    public class ClearMemoryRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Allocation ID to clear
        /// </summary>
        public string AllocationId { get; set; } = string.Empty;

        /// <summary>
        /// Offset to start clearing from
        /// </summary>
        public long Offset { get; set; } = 0;

        /// <summary>
        /// Size to clear in bytes (optional, defaults to full allocation)
        /// </summary>
        public long? SizeBytes { get; set; }

        /// <summary>
        /// Value to fill with (defaults to zero)
        /// </summary>
        public byte FillValue { get; set; } = 0;

        /// <summary>
        /// Wait for clear completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;
    }

    /// <summary>
    /// Request to optimize memory usage on a device
    /// </summary>
    public class OptimizeMemoryRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Optimization type
        /// </summary>
        public MemoryOptimizationType OptimizationType { get; set; }

        /// <summary>
        /// Force optimization even if device is busy
        /// </summary>
        public bool Force { get; set; } = false;

        /// <summary>
        /// Target memory usage percentage (0.0-1.0)
        /// </summary>
        public double? TargetUsagePercentage { get; set; }

        /// <summary>
        /// Preserve specific allocations
        /// </summary>
        public List<string> PreserveAllocations { get; set; } = new();

        /// <summary>
        /// Wait for optimization completion
        /// </summary>
        public bool WaitForCompletion { get; set; } = true;
    }

    /// <summary>
    /// Request to defragment memory on a device
    /// </summary>
    public class DefragmentMemoryRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Defragmentation strategy
        /// </summary>
        public DefragmentationStrategy Strategy { get; set; }

        /// <summary>
        /// Maximum time to spend defragmenting in seconds
        /// </summary>
        public int MaxTimeSeconds { get; set; } = 300;

        /// <summary>
        /// Minimum free space percentage to achieve
        /// </summary>
        public double MinFreeSpacePercentage { get; set; } = 0.1;

        /// <summary>
        /// Force defragmentation even if device is busy
        /// </summary>
        public bool Force { get; set; } = false;
    }

    /// <summary>
    /// Request to get memory allocation details
    /// </summary>
    public class GetAllocationDetailsRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Allocation ID (optional, if not provided returns all allocations)
        /// </summary>
        public string? AllocationId { get; set; }

        /// <summary>
        /// Include allocation history
        /// </summary>
        public bool IncludeHistory { get; set; } = false;

        /// <summary>
        /// Include memory content statistics
        /// </summary>
        public bool IncludeContentStats { get; set; } = false;
    }

    /// <summary>
    /// Request to set memory limits for a device
    /// </summary>
    public class SetMemoryLimitsRequest
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Maximum memory usage in bytes (optional)
        /// </summary>
        public long? MaxMemoryBytes { get; set; }

        /// <summary>
        /// Maximum memory usage percentage (0.0-1.0)
        /// </summary>
        public double? MaxMemoryPercentage { get; set; }

        /// <summary>
        /// Reserved memory in bytes
        /// </summary>
        public long? ReservedMemoryBytes { get; set; }

        /// <summary>
        /// Emergency memory threshold percentage
        /// </summary>
        public double? EmergencyThresholdPercentage { get; set; }

        /// <summary>
        /// Apply limits immediately
        /// </summary>
        public bool ApplyImmediately { get; set; } = true;
    }
}

/// <summary>
/// Memory allocation type enumeration
/// </summary>
public enum MemoryAllocationType
{
    Default = 0,
    Pinned = 1,
    Mapped = 2,
    Texture = 3,
    Buffer = 4,
    Unified = 5
}

/// <summary>
/// Memory transfer type enumeration
/// </summary>
public enum MemoryTransferType
{
    Copy = 0,
    Move = 1,
    Stream = 2,
    Async = 3
}

/// <summary>
/// Transfer priority enumeration
/// </summary>
public enum TransferPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Memory copy type enumeration
/// </summary>
public enum MemoryCopyType
{
    Synchronous = 0,
    Asynchronous = 1,
    Streaming = 2
}

/// <summary>
/// Memory optimization type enumeration
/// </summary>
public enum MemoryOptimizationType
{
    Defragment = 0,
    Compact = 1,
    GarbageCollect = 2,
    Preload = 3,
    Cache = 4
}

/// <summary>
/// Defragmentation strategy enumeration
/// </summary>
public enum DefragmentationStrategy
{
    Quick = 0,
    Thorough = 1,
    Balanced = 2,
    Conservative = 3
}
