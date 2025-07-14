using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Processing;

/// <summary>
/// Interface for processing operations
/// </summary>
public interface IServiceProcessing
{
    Task<ApiResponse<GetProcessingWorkflowsResponse>> GetProcessingWorkflowsAsync();
    Task<ApiResponse<GetProcessingWorkflowResponse>> GetProcessingWorkflowAsync(string workflowId);
    Task<ApiResponse<PostWorkflowExecuteResponse>> PostWorkflowExecuteAsync(PostWorkflowExecuteRequest request);
    Task<ApiResponse<GetProcessingSessionsResponse>> GetProcessingSessionsAsync();
    Task<ApiResponse<GetProcessingSessionResponse>> GetProcessingSessionAsync(string sessionId);
    Task<ApiResponse<PostSessionControlResponse>> PostSessionControlAsync(string sessionId, PostSessionControlRequest request);
    Task<ApiResponse<DeleteProcessingSessionResponse>> DeleteProcessingSessionAsync(string sessionId);
    Task<ApiResponse<GetProcessingBatchesResponse>> GetProcessingBatchesAsync();
    Task<ApiResponse<GetProcessingBatchResponse>> GetProcessingBatchAsync(string batchId);
    Task<ApiResponse<PostBatchCreateResponse>> PostBatchCreateAsync(PostBatchCreateRequest request);
    Task<ApiResponse<PostBatchExecuteResponse>> PostBatchExecuteAsync(string batchId, PostBatchExecuteRequest request);
    Task<ApiResponse<DeleteProcessingBatchResponse>> DeleteProcessingBatchAsync(string batchId);
}
