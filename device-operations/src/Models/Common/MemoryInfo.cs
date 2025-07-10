namespace DeviceOperations.Models.Common;

/// <summary>
/// Memory status and allocation models
/// </summary>
public class MemoryInfo
{
    /// <summary>
    /// Device ID this memory information belongs to
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Total memory capacity in bytes
    /// </summary>
    public long TotalMemory { get; set; }

    /// <summary>
    /// Available memory in bytes
    /// </summary>
    public long AvailableMemory { get; set; }

    /// <summary>
    /// Used memory in bytes
    /// </summary>
    public long UsedMemory { get; set; }

    /// <summary>
    /// Memory usage percentage (0-100)
    /// </summary>
    public double UsagePercentage => TotalMemory > 0 ? (double)UsedMemory / TotalMemory * 100 : 0;

    /// <summary>
    /// Memory type (RAM, VRAM, etc.)
    /// </summary>
    public MemoryType Type { get; set; }

    /// <summary>
    /// Memory health status
    /// </summary>
    public MemoryHealthStatus Health { get; set; }

    /// <summary>
    /// Active memory allocations
    /// </summary>
    public List<MemoryAllocation> Allocations { get; set; } = new();

    /// <summary>
    /// Memory fragmentation percentage (0-100)
    /// </summary>
    public double FragmentationPercentage { get; set; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Memory allocation information
/// </summary>
public class MemoryAllocation
{
    /// <summary>
    /// Unique allocation identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Device ID where memory is allocated
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Allocation size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Allocation type/purpose
    /// </summary>
    public MemoryAllocationType Type { get; set; }

    /// <summary>
    /// Allocation status
    /// </summary>
    public MemoryAllocationStatus Status { get; set; }

    /// <summary>
    /// Owner/purpose description
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// Allocation priority
    /// </summary>
    public MemoryPriority Priority { get; set; }

    /// <summary>
    /// Whether allocation is pinned/locked
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Allocation creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last access timestamp
    /// </summary>
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Access count
    /// </summary>
    public int AccessCount { get; set; }
}

/// <summary>
/// Memory transfer operation information
/// </summary>
public class MemoryTransfer
{
    /// <summary>
    /// Unique transfer identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Source device ID
    /// </summary>
    public string SourceDeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Destination device ID
    /// </summary>
    public string DestinationDeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Transfer size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Transfer status
    /// </summary>
    public MemoryTransferStatus Status { get; set; }

    /// <summary>
    /// Transfer progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// Transfer speed in bytes per second
    /// </summary>
    public long TransferSpeed { get; set; }

    /// <summary>
    /// Estimated completion time
    /// </summary>
    public DateTime? EstimatedCompletion { get; set; }

    /// <summary>
    /// Transfer start timestamp
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Transfer completion timestamp
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if transfer failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Memory usage statistics
/// </summary>
public class MemoryUsageStats
{
    /// <summary>
    /// Device ID
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Peak memory usage in bytes
    /// </summary>
    public long PeakUsage { get; set; }

    /// <summary>
    /// Average memory usage in bytes
    /// </summary>
    public long AverageUsage { get; set; }

    /// <summary>
    /// Memory allocations per second
    /// </summary>
    public double AllocationsPerSecond { get; set; }

    /// <summary>
    /// Memory deallocations per second
    /// </summary>
    public double DeallocationsPerSecond { get; set; }

    /// <summary>
    /// Average allocation size in bytes
    /// </summary>
    public long AverageAllocationSize { get; set; }

    /// <summary>
    /// Total allocations count
    /// </summary>
    public int TotalAllocations { get; set; }

    /// <summary>
    /// Active allocations count
    /// </summary>
    public int ActiveAllocations { get; set; }

    /// <summary>
    /// Statistics time window start
    /// </summary>
    public DateTime WindowStart { get; set; }

    /// <summary>
    /// Statistics time window end
    /// </summary>
    public DateTime WindowEnd { get; set; }
}

/// <summary>
/// Memory type enumeration
/// </summary>
public enum MemoryType
{
    Unknown = 0,
    RAM = 1,
    VRAM = 2,
    SharedMemory = 3,
    UnifiedMemory = 4
}

/// <summary>
/// Memory health status enumeration
/// </summary>
public enum MemoryHealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Warning = 2,
    Critical = 3,
    Error = 4
}

/// <summary>
/// Memory allocation type enumeration
/// </summary>
public enum MemoryAllocationType
{
    Unknown = 0,
    ModelWeights = 1,
    Tensors = 2,
    Buffers = 3,
    Cache = 4,
    Temporary = 5,
    System = 6
}

/// <summary>
/// Memory allocation status enumeration
/// </summary>
public enum MemoryAllocationStatus
{
    Unknown = 0,
    Active = 1,
    Inactive = 2,
    Pending = 3,
    Failed = 4,
    Released = 5
}

/// <summary>
/// Memory priority enumeration
/// </summary>
public enum MemoryPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Memory transfer status enumeration
/// </summary>
public enum MemoryTransferStatus
{
    Unknown = 0,
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}
