using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Model;

/// <summary>
/// Interface for model management operations
/// </summary>
public interface IServiceModel
{
    Task<ApiResponse<ListModelsResponse>> GetModelsAsync();
    Task<ApiResponse<GetModelResponse>> GetModelAsync(string idModel);
    Task<ApiResponse<GetModelStatusResponse>> GetModelStatusAsync();
    Task<ApiResponse<GetModelStatusResponse>> GetModelStatusAsync(string idDevice);
    Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(string idModel, PostModelLoadRequest request);
    Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(PostModelLoadRequest request);
    Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(PostModelLoadRequest request, string idDevice);
    Task<ApiResponse<PostModelUnloadResponse>> PostModelUnloadAsync(string idModel, PostModelUnloadRequest request);
    Task<ApiResponse<PostModelUnloadResponse>> PostModelUnloadAsync(PostModelUnloadRequest request);
    Task<ApiResponse<PostModelValidateResponse>> PostModelValidateAsync(string idModel, PostModelValidateRequest request);
    Task<ApiResponse<PostModelValidateResponse>> PostModelValidateAsync(PostModelValidateRequest request);
    Task<ApiResponse<PostModelOptimizeResponse>> PostModelOptimizeAsync(string idModel, PostModelOptimizeRequest request);
    Task<ApiResponse<PostModelOptimizeResponse>> PostModelOptimizeAsync(PostModelOptimizeRequest request);
    Task<ApiResponse<PostModelBenchmarkResponse>> PostModelBenchmarkAsync(string idModel, PostModelBenchmarkRequest request);
    Task<ApiResponse<PostModelBenchmarkResponse>> PostModelBenchmarkAsync(PostModelBenchmarkRequest request);
    Task<ApiResponse<PostModelSearchResponse>> PostModelSearchAsync(PostModelSearchRequest request);
    Task<ApiResponse<GetModelMetadataResponse>> GetModelMetadataAsync(string idModel);
    Task<ApiResponse<PutModelMetadataResponse>> PutModelMetadataAsync(string idModel, PutModelMetadataRequest request);
}
