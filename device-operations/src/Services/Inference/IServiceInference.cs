using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Models.Inference;

namespace DeviceOperations.Services.Inference;

/// <summary>
/// Interface for inference operations with advanced features
/// </summary>
public interface IServiceInference
{
    // Core inference methods
    Task<ApiResponse<GetInferenceCapabilitiesResponse>> GetInferenceCapabilitiesAsync();
    Task<ApiResponse<GetInferenceCapabilitiesDeviceResponse>> GetInferenceCapabilitiesAsync(string idDevice);
    Task<ApiResponse<PostInferenceExecuteResponse>> PostInferenceExecuteAsync(PostInferenceExecuteRequest request);
    Task<ApiResponse<PostInferenceExecuteDeviceResponse>> PostInferenceExecuteAsync(PostInferenceExecuteDeviceRequest request, string idDevice);
    Task<ApiResponse<PostInferenceValidateResponse>> PostInferenceValidateAsync(PostInferenceValidateRequest request);
    Task<ApiResponse<GetSupportedTypesResponse>> GetSupportedTypesAsync();
    Task<ApiResponse<GetSupportedTypesDeviceResponse>> GetSupportedTypesAsync(string idDevice);
    Task<ApiResponse<GetInferenceSessionsResponse>> GetInferenceSessionsAsync();
    Task<ApiResponse<GetInferenceSessionResponse>> GetInferenceSessionAsync(string idSession);
    Task<ApiResponse<DeleteInferenceSessionResponse>> DeleteInferenceSessionAsync(string idSession, DeleteInferenceSessionRequest request);

    // Advanced feature methods (Week 18)
    Task<ApiResponse<PostInferenceBatchResponse>> PostInferenceBatchAsync(PostInferenceBatchRequest request);
    Task<ApiResponse<PostInferenceBatchResponse>> MonitorBatchProgressAsync(string batchId);
    Task<ApiResponse<PostInferenceControlNetResponse>> PostInferenceControlNetAsync(PostInferenceControlNetRequest request);
    Task<ApiResponse<PostInferenceLoRAResponse>> PostInferenceLoRAAsync(PostInferenceLoRARequest request);
    
    // Week 20: Advanced Operations
    Task<ApiResponse<PostInferenceInpaintingResponse>> PostInferenceInpaintingAsync(PostInferenceInpaintingRequest request);
    Task<ApiResponse<GetInferenceSessionResponse>> GetInferenceSessionAnalyticsAsync(string idSession);
    
    // Utility methods
    Task<int> CalculateOptimalBatchSizeAsync(string modelId, string deviceId);
    Task<bool> ValidateControlNetRequestAsync(PostInferenceControlNetRequest request);
    Task<bool> ValidateInpaintingRequestAsync(PostInferenceInpaintingRequest request);
}
