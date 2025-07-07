using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Memory;

public interface IMemoryOperationsService
{
    Task<MemoryStatusResponse> GetMemoryStatusAsync(string deviceId);
    Task<MemoryStatusResponse> GetAllMemoryStatusAsync();
    Task<MemoryOperationResponse> AllocateMemoryAsync(MemoryAllocateRequest request);
    Task<MemoryOperationResponse> DeallocateMemoryAsync(string allocationId);
    Task<MemoryOperationResponse> CopyMemoryAsync(MemoryCopyRequest request);
    Task<MemoryOperationResponse> ClearMemoryAsync(MemoryClearRequest request);
    Task<MemoryAllocation?> GetAllocationAsync(string allocationId);
    Task<IEnumerable<MemoryAllocation>> GetActiveAllocationsAsync(string? deviceId = null);
    Task InitializeAsync();
}
