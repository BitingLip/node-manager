using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Postprocessing;

/// <summary>
/// Interface for postprocessing operations
/// </summary>
public interface IServicePostprocessing
{
    Task<ApiResponse<GetPostprocessingCapabilitiesResponse>> GetPostprocessingCapabilitiesAsync();
    Task<ApiResponse<GetPostprocessingCapabilitiesResponse>> GetPostprocessingCapabilitiesAsync(string deviceId);
    Task<ApiResponse<PostPostprocessingApplyResponse>> PostPostprocessingApplyAsync(PostPostprocessingApplyRequest request);
    Task<ApiResponse<PostPostprocessingUpscaleResponse>> PostPostprocessingUpscaleAsync(PostPostprocessingUpscaleRequest request);
    Task<ApiResponse<PostPostprocessingUpscaleResponse>> PostUpscaleAsync(PostPostprocessingUpscaleRequest request);
    Task<ApiResponse<PostPostprocessingUpscaleResponse>> PostUpscaleAsync(PostPostprocessingUpscaleRequest request, string deviceId);
    Task<ApiResponse<PostPostprocessingEnhanceResponse>> PostPostprocessingEnhanceAsync(PostPostprocessingEnhanceRequest request);
    Task<ApiResponse<PostPostprocessingEnhanceResponse>> PostEnhanceAsync(PostPostprocessingEnhanceRequest request);
    Task<ApiResponse<PostPostprocessingEnhanceResponse>> PostEnhanceAsync(PostPostprocessingEnhanceRequest request, string deviceId);
    Task<ApiResponse<PostPostprocessingFaceRestoreResponse>> PostPostprocessingFaceRestoreAsync(PostPostprocessingFaceRestoreRequest request);
    Task<ApiResponse<PostPostprocessingFaceRestoreResponse>> PostFaceRestoreAsync(PostPostprocessingFaceRestoreRequest request);
    Task<ApiResponse<PostPostprocessingStyleTransferResponse>> PostPostprocessingStyleTransferAsync(PostPostprocessingStyleTransferRequest request);
    Task<ApiResponse<PostPostprocessingStyleTransferResponse>> PostStyleTransferAsync(PostPostprocessingStyleTransferRequest request);
    Task<ApiResponse<PostPostprocessingBackgroundRemoveResponse>> PostPostprocessingBackgroundRemoveAsync(PostPostprocessingBackgroundRemoveRequest request);
    Task<ApiResponse<PostPostprocessingBackgroundRemoveResponse>> PostBackgroundRemoveAsync(PostPostprocessingBackgroundRemoveRequest request);
    Task<ApiResponse<PostPostprocessingColorCorrectResponse>> PostPostprocessingColorCorrectAsync(PostPostprocessingColorCorrectRequest request);
    Task<ApiResponse<PostPostprocessingColorCorrectResponse>> PostColorCorrectAsync(PostPostprocessingColorCorrectRequest request);
    Task<ApiResponse<PostPostprocessingBatchResponse>> PostPostprocessingBatchAsync(PostPostprocessingBatchRequest request);
}
