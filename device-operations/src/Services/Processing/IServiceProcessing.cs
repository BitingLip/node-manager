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
    Task<ApiResponse<GetProcessingWorkflowResponse>> GetProcessingWorkflowAsync(string idWorkflow);
    Task<ApiResponse<PostWorkflowExecuteResponse>> PostWorkflowExecuteAsync(PostWorkflowExecuteRequest request);
    Task<ApiResponse<GetProcessingSessionsResponse>> GetProcessingSessionsAsync();
    Task<ApiResponse<GetProcessingSessionResponse>> GetProcessingSessionAsync(string idSession);
    Task<ApiResponse<PostSessionControlResponse>> PostSessionControlAsync(string idSession, PostSessionControlRequest request);
    Task<ApiResponse<DeleteProcessingSessionResponse>> DeleteProcessingSessionAsync(string idSession);
    Task<ApiResponse<GetProcessingBatchesResponse>> GetProcessingBatchesAsync();
    Task<ApiResponse<GetProcessingBatchResponse>> GetProcessingBatchAsync(string idBatch);
    Task<ApiResponse<PostBatchCreateResponse>> PostBatchCreateAsync(PostBatchCreateRequest request);
    Task<ApiResponse<PostBatchExecuteResponse>> PostBatchExecuteAsync(string idBatch, PostBatchExecuteRequest request);
    Task<ApiResponse<DeleteProcessingBatchResponse>> DeleteProcessingBatchAsync(string idBatch);
}
