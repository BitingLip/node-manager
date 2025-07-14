using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Models.Postprocessing;

namespace DeviceOperations.Services.Postprocessing;

/// <summary>
/// Interface for postprocessing operations
/// </summary>
public interface IServicePostprocessing
{
    // Core postprocessing operations
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

    // Advanced features (Weeks 18-19)
    Task<ApiResponse<PostPostprocessingBatchAdvancedResponse>> ExecuteBatchPostprocessingAsync(PostPostprocessingBatchAdvancedRequest request);
    Task<(double progress, DeviceOperations.Models.Postprocessing.BatchStatus status, List<BatchImageResult> results)> MonitorBatchProgressAsync(string batchId);
    Task<ApiResponse<PostPostprocessingModelManagementResponse>> ManagePostprocessingModelAsync(PostPostprocessingModelManagementRequest request);
    Task<PostprocessingPerformanceAnalytics> GetPerformanceAnalyticsAsync(PerformanceAnalyticsRequest request, CancellationToken cancellationToken = default);
    
    // Performance optimization features (Week 19)
    Task<ApiResponse<PostPostprocessingResponse>> ExecuteWithOptimizedConnectionAsync(PostPostprocessingRequest request);
    Task<ApiResponse<List<PostprocessingModelInfo>>> GetAvailableModelsWithCachingAsync(string? modelType = null, bool forceRefresh = false);
    IAsyncEnumerable<PostprocessingProgressUpdate> ExecuteWithProgressStreamingAsync(PostPostprocessingRequest request, ProgressStreamingConfig? config = null, CancellationToken cancellationToken = default);
}
