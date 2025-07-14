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
    Task<ApiResponse<GetModelResponse>> GetModelAsync(string modelId);
    Task<ApiResponse<GetModelStatusResponse>> GetModelStatusAsync();
    Task<ApiResponse<GetModelStatusResponse>> GetModelStatusAsync(string deviceId);
    Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(string modelId, PostModelLoadRequest request);
    Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(PostModelLoadRequest request);
    Task<ApiResponse<PostModelLoadResponse>> PostModelLoadAsync(PostModelLoadRequest request, string deviceId);
    Task<ApiResponse<PostModelUnloadResponse>> PostModelUnloadAsync(string modelId, PostModelUnloadRequest request);
    Task<ApiResponse<PostModelUnloadResponse>> PostModelUnloadAsync(PostModelUnloadRequest request);
    Task<ApiResponse<DeleteModelUnloadResponse>> DeleteModelUnloadAsync();
    Task<ApiResponse<DeleteModelUnloadResponse>> DeleteModelUnloadAsync(string deviceId);
    Task<ApiResponse<PostModelValidateResponse>> PostModelValidateAsync(string modelId, PostModelValidateRequest request);
    Task<ApiResponse<PostModelValidateResponse>> PostModelValidateAsync(PostModelValidateRequest request);
    Task<ApiResponse<PostModelOptimizeResponse>> PostModelOptimizeAsync(string modelId, PostModelOptimizeRequest request);
    Task<ApiResponse<PostModelOptimizeResponse>> PostModelOptimizeAsync(PostModelOptimizeRequest request);
    Task<ApiResponse<PostModelBenchmarkResponse>> PostModelBenchmarkAsync(string modelId, PostModelBenchmarkRequest request);
    Task<ApiResponse<PostModelBenchmarkResponse>> PostModelBenchmarkAsync(PostModelBenchmarkRequest request);
    Task<ApiResponse<PostModelSearchResponse>> PostModelSearchAsync(PostModelSearchRequest request);
    Task<ApiResponse<GetModelMetadataResponse>> GetModelMetadataAsync(string modelId);
    Task<ApiResponse<PutModelMetadataResponse>> PutModelMetadataAsync(string modelId, PutModelMetadataRequest request);
    
    // VRAM Operations  
    Task<ApiResponse<PostModelVramLoadResponse>> PostModelVramLoadAsync(PostModelVramLoadRequest request);
    Task<ApiResponse<PostModelVramLoadResponse>> PostModelVramLoadAsync(PostModelVramLoadRequest request, string deviceId);
    Task<ApiResponse<DeleteModelVramUnloadResponse>> DeleteModelVramUnloadAsync(DeleteModelVramUnloadRequest request);
    Task<ApiResponse<DeleteModelVramUnloadResponse>> DeleteModelVramUnloadAsync(DeleteModelVramUnloadRequest request, string deviceId);
    
    // Week 9: Enhanced Filesystem Discovery Operations
    Task<ApiResponse<GetAvailableModelsResponse>> GetAvailableModelsAsync();
    Task<ApiResponse<GetAvailableModelsByTypeResponse>> GetAvailableModelsByTypeAsync(string modelType);    
    // Phase 4 Week 2: Foundation & Integration - Key missing operations
    // Using existing response models where available
    
    // Cache and VRAM Operations  
    Task<ApiResponse<GetModelStatusResponse>> GetModelCacheStatusAsync(string? modelId = null);
    Task<ApiResponse<PostModelLoadResponse>> PostModelCacheAsync(PostModelLoadRequest request);
    
    // Component Operations  
    Task<ApiResponse<ListModelsResponse>> GetModelComponentsAsync(string modelId);
}
