using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Inference;

/// <summary>
/// Interface for inference operations
/// </summary>
public interface IServiceInference
{
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
}
