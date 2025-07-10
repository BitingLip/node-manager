using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Memory;

/// <summary>
/// Interface for memory management operations
/// </summary>
public interface IServiceMemory
{
    Task<ApiResponse<GetMemoryStatusResponse>> GetMemoryStatusAsync();
    Task<ApiResponse<GetMemoryStatusResponse>> GetMemoryStatusAsync(string deviceId);
    Task<ApiResponse<GetMemoryStatusDeviceResponse>> GetMemoryStatusDeviceAsync(string deviceId);
    Task<ApiResponse<GetMemoryUsageResponse>> GetMemoryUsageAsync();
    Task<ApiResponse<GetMemoryUsageResponse>> GetMemoryUsageAsync(string deviceId);
    Task<ApiResponse<GetMemoryAllocationsResponse>> GetMemoryAllocationsAsync();
    Task<ApiResponse<GetMemoryAllocationsResponse>> GetMemoryAllocationsAsync(string deviceId);
    Task<ApiResponse<GetMemoryAllocationResponse>> GetMemoryAllocationAsync(string allocationId);
    Task<ApiResponse<PostMemoryAllocateResponse>> PostMemoryAllocateAsync(PostMemoryAllocateRequest request);
    Task<ApiResponse<PostMemoryAllocateResponse>> PostMemoryAllocateAsync(PostMemoryAllocateRequest request, string deviceId);
    Task<ApiResponse<DeleteMemoryDeallocateResponse>> DeleteMemoryDeallocateAsync(DeleteMemoryDeallocateRequest request);
    Task<ApiResponse<DeleteMemoryAllocationResponse>> DeleteMemoryAllocationAsync(string allocationId);
    Task<ApiResponse<PostMemoryTransferResponse>> PostMemoryTransferAsync(PostMemoryTransferRequest request);
    Task<ApiResponse<GetMemoryTransferResponse>> GetMemoryTransferAsync(string transferId);
    Task<ApiResponse<PostMemoryCopyResponse>> PostMemoryCopyAsync(PostMemoryCopyRequest request);
    Task<ApiResponse<PostMemoryClearResponse>> PostMemoryClearAsync(PostMemoryClearRequest request);
    Task<ApiResponse<PostMemoryClearResponse>> PostMemoryClearAsync(PostMemoryClearRequest request, string deviceId);
    Task<ApiResponse<PostMemoryOptimizeResponse>> PostMemoryOptimizeAsync(PostMemoryOptimizeRequest request);
    Task<ApiResponse<PostMemoryDefragmentResponse>> PostMemoryDefragmentAsync(PostMemoryDefragmentRequest request);
    Task<ApiResponse<PostMemoryDefragmentResponse>> PostMemoryDefragmentAsync(PostMemoryDefragmentRequest request, string deviceId);
}
