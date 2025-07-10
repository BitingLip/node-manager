namespace DeviceOperations.Models.Responses;

using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;

/// <summary>
/// Memory operation responses
/// </summary>
public static class ResponsesMemory
{
    /// <summary>
    /// Response for getting memory status
    /// </summary>
    public class GetMemoryStatusResponse
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Overall memory information
        /// </summary>
        public MemoryInfo MemoryInfo { get; set; } = new();

        /// <summary>
        /// Memory usage statistics
        /// </summary>
        public MemoryUsageStats UsageStats { get; set; } = new();

        /// <summary>
        /// Active memory allocations
        /// </summary>
        public List<MemoryAllocation> Allocations { get; set; } = new();

        /// <summary>
        /// Memory fragmentation information
        /// </summary>
        public MemoryFragmentation? Fragmentation { get; set; }

        /// <summary>
        /// Memory health score (0-100)
        /// </summary>
        public double HealthScore { get; set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for memory allocation
    /// </summary>
    public class AllocateMemoryResponse
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Allocated memory details
        /// </summary>
        public MemoryAllocation Allocation { get; set; } = new();

        /// <summary>
        /// Allocation success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Allocation time in milliseconds
        /// </summary>
        public double AllocationTimeMs { get; set; }

        /// <summary>
        /// Available memory after allocation
        /// </summary>
        public long AvailableMemoryBytes { get; set; }

        /// <summary>
        /// Memory utilization percentage after allocation
        /// </summary>
        public double UtilizationPercentage { get; set; }

        /// <summary>
        /// Allocation message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Allocation timestamp
        /// </summary>
        public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for memory deallocation
    /// </summary>
    public class DeallocateMemoryResponse
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Deallocated allocation ID
        /// </summary>
        public string AllocationId { get; set; } = string.Empty;

        /// <summary>
        /// Deallocation success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Freed memory size in bytes
        /// </summary>
        public long FreedBytes { get; set; }

        /// <summary>
        /// Deallocation time in milliseconds
        /// </summary>
        public double DeallocationTimeMs { get; set; }

        /// <summary>
        /// Available memory after deallocation
        /// </summary>
        public long AvailableMemoryBytes { get; set; }

        /// <summary>
        /// Memory utilization percentage after deallocation
        /// </summary>
        public double UtilizationPercentage { get; set; }

        /// <summary>
        /// Deallocation message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Deallocation timestamp
        /// </summary>
        public DateTime DeallocatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for memory transfer
    /// </summary>
    public class TransferMemoryResponse
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
        /// Memory transfer details
        /// </summary>
        public MemoryTransfer Transfer { get; set; } = new();

        /// <summary>
        /// Transfer success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Transfer throughput in MB/s
        /// </summary>
        public double ThroughputMBps { get; set; }

        /// <summary>
        /// Transfer efficiency percentage
        /// </summary>
        public double Efficiency { get; set; }

        /// <summary>
        /// Destination allocation ID
        /// </summary>
        public string DestinationAllocationId { get; set; } = string.Empty;

        /// <summary>
        /// Transfer message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Transfer completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for memory copy
    /// </summary>
    public class CopyMemoryResponse
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
        /// Copy success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Copied data size in bytes
        /// </summary>
        public long CopiedBytes { get; set; }

        /// <summary>
        /// Copy time in milliseconds
        /// </summary>
        public double CopyTimeMs { get; set; }

        /// <summary>
        /// Copy throughput in MB/s
        /// </summary>
        public double ThroughputMBps { get; set; }

        /// <summary>
        /// Copy message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Copy completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for memory clear
    /// </summary>
    public class ClearMemoryResponse
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Allocation ID that was cleared
        /// </summary>
        public string AllocationId { get; set; } = string.Empty;

        /// <summary>
        /// Clear success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Cleared memory size in bytes
        /// </summary>
        public long ClearedBytes { get; set; }

        /// <summary>
        /// Clear time in milliseconds
        /// </summary>
        public double ClearTimeMs { get; set; }

        /// <summary>
        /// Clear message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Clear completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for memory optimization
    /// </summary>
    public class OptimizeMemoryResponse
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Optimization type performed
        /// </summary>
        public MemoryOptimizationType OptimizationType { get; set; }

        /// <summary>
        /// Optimization results
        /// </summary>
        public MemoryOptimizationResults Results { get; set; } = new();

        /// <summary>
        /// Optimization success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Optimization time in milliseconds
        /// </summary>
        public double OptimizationTimeMs { get; set; }

        /// <summary>
        /// Memory usage before optimization
        /// </summary>
        public MemoryUsageSnapshot BeforeOptimization { get; set; } = new();

        /// <summary>
        /// Memory usage after optimization
        /// </summary>
        public MemoryUsageSnapshot AfterOptimization { get; set; } = new();

        /// <summary>
        /// Optimization message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Optimization completion timestamp
        /// </summary>
        public DateTime OptimizedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for memory defragmentation
    /// </summary>
    public class DefragmentMemoryResponse
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Defragmentation strategy used
        /// </summary>
        public DefragmentationStrategy Strategy { get; set; }

        /// <summary>
        /// Defragmentation results
        /// </summary>
        public DefragmentationResults Results { get; set; } = new();

        /// <summary>
        /// Defragmentation success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Defragmentation time in milliseconds
        /// </summary>
        public double DefragmentationTimeMs { get; set; }

        /// <summary>
        /// Fragmentation level before defragmentation
        /// </summary>
        public double FragmentationBefore { get; set; }

        /// <summary>
        /// Fragmentation level after defragmentation
        /// </summary>
        public double FragmentationAfter { get; set; }

        /// <summary>
        /// Defragmentation message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Defragmentation completion timestamp
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for getting allocation details
    /// </summary>
    public class GetAllocationDetailsResponse
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Memory allocations
        /// </summary>
        public List<MemoryAllocation> Allocations { get; set; } = new();

        /// <summary>
        /// Allocation history
        /// </summary>
        public List<AllocationHistoryEntry>? AllocationHistory { get; set; }

        /// <summary>
        /// Memory content statistics
        /// </summary>
        public Dictionary<string, MemoryContentStats>? ContentStats { get; set; }

        /// <summary>
        /// Total allocated memory
        /// </summary>
        public long TotalAllocatedBytes { get; set; }

        /// <summary>
        /// Number of active allocations
        /// </summary>
        public int ActiveAllocationCount { get; set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for setting memory limits
    /// </summary>
    public class SetMemoryLimitsResponse
    {
        /// <summary>
        /// Device identifier
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Previous memory limits
        /// </summary>
        public MemoryLimits PreviousLimits { get; set; } = new();

        /// <summary>
        /// New memory limits
        /// </summary>
        public MemoryLimits NewLimits { get; set; } = new();

        /// <summary>
        /// Limits application success status
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Impact analysis
        /// </summary>
        public MemoryLimitsImpact Impact { get; set; } = new();

        /// <summary>
        /// Limits application message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Limits application timestamp
        /// </summary>
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    }
}

/// <summary>
/// Memory fragmentation information
/// </summary>
public class MemoryFragmentation
{
    /// <summary>
    /// Fragmentation percentage (0-100)
    /// </summary>
    public double FragmentationPercentage { get; set; }

    /// <summary>
    /// Largest contiguous block size in bytes
    /// </summary>
    public long LargestContiguousBlock { get; set; }

    /// <summary>
    /// Number of memory holes
    /// </summary>
    public int MemoryHoles { get; set; }

    /// <summary>
    /// Average hole size in bytes
    /// </summary>
    public long AverageHoleSize { get; set; }

    /// <summary>
    /// Fragmentation score (0-100, lower is better)
    /// </summary>
    public double FragmentationScore { get; set; }

    /// <summary>
    /// Defragmentation recommendation
    /// </summary>
    public bool RecommendDefragmentation { get; set; }
}

/// <summary>
/// Memory optimization results
/// </summary>
public class MemoryOptimizationResults
{
    /// <summary>
    /// Memory freed in bytes
    /// </summary>
    public long MemoryFreed { get; set; }

    /// <summary>
    /// Allocations consolidated
    /// </summary>
    public int AllocationsConsolidated { get; set; }

    /// <summary>
    /// Fragmentation reduction percentage
    /// </summary>
    public double FragmentationReduction { get; set; }

    /// <summary>
    /// Performance improvement estimation
    /// </summary>
    public double EstimatedPerformanceImprovement { get; set; }

    /// <summary>
    /// Optimization efficiency score
    /// </summary>
    public double EfficiencyScore { get; set; }

    /// <summary>
    /// Detailed optimization metrics
    /// </summary>
    public Dictionary<string, double> DetailedMetrics { get; set; } = new();
}

/// <summary>
/// Memory usage snapshot
/// </summary>
public class MemoryUsageSnapshot
{
    /// <summary>
    /// Total memory in bytes
    /// </summary>
    public long TotalMemoryBytes { get; set; }

    /// <summary>
    /// Used memory in bytes
    /// </summary>
    public long UsedMemoryBytes { get; set; }

    /// <summary>
    /// Available memory in bytes
    /// </summary>
    public long AvailableMemoryBytes { get; set; }

    /// <summary>
    /// Memory utilization percentage
    /// </summary>
    public double UtilizationPercentage { get; set; }

    /// <summary>
    /// Number of active allocations
    /// </summary>
    public int ActiveAllocations { get; set; }

    /// <summary>
    /// Snapshot timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Defragmentation results
/// </summary>
public class DefragmentationResults
{
    /// <summary>
    /// Memory compacted in bytes
    /// </summary>
    public long MemoryCompacted { get; set; }

    /// <summary>
    /// Allocations moved
    /// </summary>
    public int AllocationsMoved { get; set; }

    /// <summary>
    /// Memory holes eliminated
    /// </summary>
    public int MemoryHolesEliminated { get; set; }

    /// <summary>
    /// Largest contiguous block size achieved
    /// </summary>
    public long LargestContiguousBlock { get; set; }

    /// <summary>
    /// Defragmentation efficiency score
    /// </summary>
    public double EfficiencyScore { get; set; }

    /// <summary>
    /// Estimated performance improvement
    /// </summary>
    public double EstimatedPerformanceImprovement { get; set; }
}

/// <summary>
/// Allocation history entry
/// </summary>
public class AllocationHistoryEntry
{
    /// <summary>
    /// Allocation ID
    /// </summary>
    public string AllocationId { get; set; } = string.Empty;

    /// <summary>
    /// Operation type (allocated, deallocated, moved)
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Memory size in bytes
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Memory address
    /// </summary>
    public long Address { get; set; }

    /// <summary>
    /// Allocation purpose
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// Operation timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Operation duration in milliseconds
    /// </summary>
    public double DurationMs { get; set; }
}

/// <summary>
/// Memory content statistics
/// </summary>
public class MemoryContentStats
{
    /// <summary>
    /// Data type distribution
    /// </summary>
    public Dictionary<string, long> DataTypeDistribution { get; set; } = new();

    /// <summary>
    /// Access frequency
    /// </summary>
    public long AccessCount { get; set; }

    /// <summary>
    /// Last access timestamp
    /// </summary>
    public DateTime LastAccessed { get; set; }

    /// <summary>
    /// Read/write ratio
    /// </summary>
    public double ReadWriteRatio { get; set; }

    /// <summary>
    /// Memory hotness score (0-100)
    /// </summary>
    public double HotnessScore { get; set; }
}

/// <summary>
/// Memory limits
/// </summary>
public class MemoryLimits
{
    /// <summary>
    /// Maximum memory usage in bytes
    /// </summary>
    public long? MaxMemoryBytes { get; set; }

    /// <summary>
    /// Maximum memory usage percentage
    /// </summary>
    public double? MaxMemoryPercentage { get; set; }

    /// <summary>
    /// Reserved memory in bytes
    /// </summary>
    public long? ReservedMemoryBytes { get; set; }

    /// <summary>
    /// Emergency threshold percentage
    /// </summary>
    public double? EmergencyThresholdPercentage { get; set; }

    /// <summary>
    /// Limits are active
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Limits set timestamp
    /// </summary>
    public DateTime SetAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Memory limits impact analysis
/// </summary>
public class MemoryLimitsImpact
{
    /// <summary>
    /// Allocations that may be affected
    /// </summary>
    public int PotentiallyAffectedAllocations { get; set; }

    /// <summary>
    /// Sessions that may be impacted
    /// </summary>
    public int PotentiallyImpactedSessions { get; set; }

    /// <summary>
    /// Memory that may need to be freed
    /// </summary>
    public long MemoryToFreeBytes { get; set; }

    /// <summary>
    /// Estimated performance impact
    /// </summary>
    public double EstimatedPerformanceImpact { get; set; }

    /// <summary>
    /// Recommendations for mitigating impact
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
}
