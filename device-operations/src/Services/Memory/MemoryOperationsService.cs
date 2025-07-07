using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Core;
using System.Collections.Concurrent;

namespace DeviceOperations.Services.Memory;

public class MemoryOperationsService : IMemoryOperationsService, IDisposable
{
    private readonly IDeviceService _deviceService;
    private readonly ILogger<MemoryOperationsService> _logger;
    private readonly ConcurrentDictionary<string, MemoryAllocation> _allocations = new();
    private readonly ConcurrentDictionary<string, long> _deviceAllocatedMemory = new();
    private bool _initialized = false;

    public MemoryOperationsService(IDeviceService deviceService, ILogger<MemoryOperationsService> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing Memory Operations Service...");
        
        // Initialize device memory tracking
        var devices = await _deviceService.GetAvailableDevicesAsync();
        foreach (var device in devices)
        {
            _deviceAllocatedMemory[device.DeviceId] = 0;
            _logger.LogInformation($"Initialized memory tracking for device: {device.DeviceId}");
        }

        _initialized = true;
        _logger.LogInformation("Memory Operations Service initialized successfully");
    }

    public async Task<MemoryStatusResponse> GetMemoryStatusAsync(string deviceId)
    {
        if (!_initialized) await InitializeAsync();

        var device = await _deviceService.GetDeviceAsync(deviceId);
        if (device == null)
        {
            throw new ArgumentException($"Device {deviceId} not found");
        }

        var allocatedMemory = _deviceAllocatedMemory.GetValueOrDefault(deviceId, 0);
        var deviceAllocations = _allocations.Values
            .Where(a => a.DeviceId == deviceId && a.IsActive)
            .Select(a => new MemoryAllocationSummary
            {
                AllocationId = a.AllocationId,
                SizeInBytes = a.SizeInBytes,
                Purpose = a.Purpose,
                CreatedAt = a.CreatedAt,
                LastAccessed = a.LastAccessed
            })
            .ToList();

        return new MemoryStatusResponse
        {
            DeviceId = deviceId,
            TotalMemoryBytes = device.TotalMemory,
            AvailableMemoryBytes = device.AvailableMemory - allocatedMemory,
            AllocatedMemoryBytes = allocatedMemory,
            MemoryUsagePercentage = device.TotalMemory > 0 ? (double)allocatedMemory / device.TotalMemory * 100 : 0,
            ActiveAllocations = deviceAllocations.Count,
            Allocations = deviceAllocations,
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<MemoryStatusResponse> GetAllMemoryStatusAsync()
    {
        if (!_initialized) await InitializeAsync();

        var devices = await _deviceService.GetAvailableDevicesAsync();
        var totalMemory = devices.Sum(d => d.TotalMemory);
        var totalAllocated = _deviceAllocatedMemory.Values.Sum();
        var allAllocations = _allocations.Values
            .Where(a => a.IsActive)
            .Select(a => new MemoryAllocationSummary
            {
                AllocationId = a.AllocationId,
                SizeInBytes = a.SizeInBytes,
                Purpose = a.Purpose,
                CreatedAt = a.CreatedAt,
                LastAccessed = a.LastAccessed
            })
            .ToList();

        return new MemoryStatusResponse
        {
            DeviceId = "all",
            TotalMemoryBytes = totalMemory,
            AvailableMemoryBytes = totalMemory - totalAllocated,
            AllocatedMemoryBytes = totalAllocated,
            MemoryUsagePercentage = totalMemory > 0 ? (double)totalAllocated / totalMemory * 100 : 0,
            ActiveAllocations = allAllocations.Count,
            Allocations = allAllocations,
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<MemoryOperationResponse> AllocateMemoryAsync(MemoryAllocateRequest request)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            // Validate device exists and is available
            var device = await _deviceService.GetDeviceAsync(request.DeviceId);
            if (device == null || !device.IsAvailable)
            {
                return new MemoryOperationResponse
                {
                    Success = false,
                    Message = $"Device {request.DeviceId} is not available"
                };
            }

            // Check available memory
            var currentAllocated = _deviceAllocatedMemory.GetValueOrDefault(request.DeviceId, 0);
            if (currentAllocated + request.SizeInBytes > device.AvailableMemory)
            {
                return new MemoryOperationResponse
                {
                    Success = false,
                    Message = $"Insufficient memory on device {request.DeviceId}. Required: {request.SizeInBytes:N0} bytes, Available: {device.AvailableMemory - currentAllocated:N0} bytes"
                };
            }

            // Generate allocation ID
            var allocationId = Guid.NewGuid().ToString("N")[..16];

            // For now, simulate memory allocation (in real implementation, this would create D3D12 resources)
            var allocation = new MemoryAllocation
            {
                AllocationId = allocationId,
                DeviceId = request.DeviceId,
                SizeInBytes = request.SizeInBytes,
                DevicePointer = new IntPtr(0x12345678), // Mock pointer
                Purpose = request.Purpose,
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                IsActive = true,
                GPUVirtualAddress = (ulong)(0x100000000L + _allocations.Count * 0x10000000L) // Mock GPU address
            };

            _allocations[allocationId] = allocation;
            _deviceAllocatedMemory.AddOrUpdate(request.DeviceId, request.SizeInBytes, (key, current) => current + request.SizeInBytes);

            _logger.LogInformation($"Allocated {request.SizeInBytes:N0} bytes on device {request.DeviceId}, allocation ID: {allocationId}");

            return new MemoryOperationResponse
            {
                Success = true,
                AllocationId = allocationId,
                SizeInBytes = request.SizeInBytes,
                Message = $"Successfully allocated {request.SizeInBytes:N0} bytes"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to allocate memory on device {request.DeviceId}");
            return new MemoryOperationResponse
            {
                Success = false,
                Message = $"Memory allocation failed: {ex.Message}"
            };
        }
    }

    public async Task<MemoryOperationResponse> DeallocateMemoryAsync(string allocationId)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            if (!_allocations.TryGetValue(allocationId, out var allocation) || !allocation.IsActive)
            {
                return new MemoryOperationResponse
                {
                    Success = false,
                    Message = $"Allocation {allocationId} not found or already deallocated"
                };
            }

            // Mark as inactive and update device memory tracking
            allocation.IsActive = false;
            _deviceAllocatedMemory.AddOrUpdate(allocation.DeviceId, 0, (key, current) => Math.Max(0, current - allocation.SizeInBytes));

            _logger.LogInformation($"Deallocated {allocation.SizeInBytes:N0} bytes from device {allocation.DeviceId}, allocation ID: {allocationId}");

            return new MemoryOperationResponse
            {
                Success = true,
                AllocationId = allocationId,
                SizeInBytes = allocation.SizeInBytes,
                Message = $"Successfully deallocated {allocation.SizeInBytes:N0} bytes"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to deallocate memory for allocation {allocationId}");
            return new MemoryOperationResponse
            {
                Success = false,
                Message = $"Memory deallocation failed: {ex.Message}"
            };
        }
    }

    public async Task<MemoryOperationResponse> CopyMemoryAsync(MemoryCopyRequest request)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            // Validate source allocation
            if (!_allocations.TryGetValue(request.SourceAllocationId, out var sourceAllocation) || !sourceAllocation.IsActive)
            {
                return new MemoryOperationResponse
                {
                    Success = false,
                    Message = $"Source allocation {request.SourceAllocationId} not found or inactive"
                };
            }

            // Validate destination allocation
            if (!_allocations.TryGetValue(request.DestinationAllocationId, out var destAllocation) || !destAllocation.IsActive)
            {
                return new MemoryOperationResponse
                {
                    Success = false,
                    Message = $"Destination allocation {request.DestinationAllocationId} not found or inactive"
                };
            }

            // Determine copy size
            var copySize = request.SizeInBytes ?? Math.Min(
                sourceAllocation.SizeInBytes - request.SourceOffset,
                destAllocation.SizeInBytes - request.DestinationOffset
            );

            // Validate bounds
            if (request.SourceOffset + copySize > sourceAllocation.SizeInBytes)
            {
                return new MemoryOperationResponse
                {
                    Success = false,
                    Message = "Copy operation exceeds source allocation bounds"
                };
            }

            if (request.DestinationOffset + copySize > destAllocation.SizeInBytes)
            {
                return new MemoryOperationResponse
                {
                    Success = false,
                    Message = "Copy operation exceeds destination allocation bounds"
                };
            }

            // Update last accessed times
            sourceAllocation.LastAccessed = DateTime.UtcNow;
            destAllocation.LastAccessed = DateTime.UtcNow;

            // In real implementation, this would perform actual GPU memory copy
            _logger.LogInformation($"Copied {copySize:N0} bytes from {request.SourceAllocationId} to {request.DestinationAllocationId}");

            return new MemoryOperationResponse
            {
                Success = true,
                SizeInBytes = copySize,
                Message = $"Successfully copied {copySize:N0} bytes"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to copy memory from {request.SourceAllocationId} to {request.DestinationAllocationId}");
            return new MemoryOperationResponse
            {
                Success = false,
                Message = $"Memory copy failed: {ex.Message}"
            };
        }
    }

    public async Task<MemoryOperationResponse> ClearMemoryAsync(MemoryClearRequest request)
    {
        if (!_initialized) await InitializeAsync();

        try
        {
            if (!_allocations.TryGetValue(request.AllocationId, out var allocation) || !allocation.IsActive)
            {
                return new MemoryOperationResponse
                {
                    Success = false,
                    Message = $"Allocation {request.AllocationId} not found or inactive"
                };
            }

            // Determine clear size
            var clearSize = request.SizeInBytes ?? (allocation.SizeInBytes - request.Offset);

            // Validate bounds
            if (request.Offset + clearSize > allocation.SizeInBytes)
            {
                return new MemoryOperationResponse
                {
                    Success = false,
                    Message = "Clear operation exceeds allocation bounds"
                };
            }

            // Update last accessed time
            allocation.LastAccessed = DateTime.UtcNow;

            // In real implementation, this would perform actual GPU memory clear
            _logger.LogInformation($"Cleared {clearSize:N0} bytes in allocation {request.AllocationId} with value {request.Value}");

            return new MemoryOperationResponse
            {
                Success = true,
                SizeInBytes = clearSize,
                Message = $"Successfully cleared {clearSize:N0} bytes"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to clear memory in allocation {request.AllocationId}");
            return new MemoryOperationResponse
            {
                Success = false,
                Message = $"Memory clear failed: {ex.Message}"
            };
        }
    }

    public async Task<MemoryAllocation?> GetAllocationAsync(string allocationId)
    {
        if (!_initialized) await InitializeAsync();

        _allocations.TryGetValue(allocationId, out var allocation);
        return allocation?.IsActive == true ? allocation : null;
    }

    public async Task<IEnumerable<MemoryAllocation>> GetActiveAllocationsAsync(string? deviceId = null)
    {
        if (!_initialized) await InitializeAsync();

        var query = _allocations.Values.Where(a => a.IsActive);
        
        if (!string.IsNullOrEmpty(deviceId))
        {
            query = query.Where(a => a.DeviceId == deviceId);
        }

        return query.ToList();
    }

    public void Dispose()
    {
        // In real implementation, this would dispose D3D12 resources
        foreach (var allocation in _allocations.Values.Where(a => a.IsActive))
        {
            allocation.IsActive = false;
        }
        
        _allocations.Clear();
        _deviceAllocatedMemory.Clear();
        _initialized = false;
        
        _logger.LogInformation("Memory Operations Service disposed");
    }
}
