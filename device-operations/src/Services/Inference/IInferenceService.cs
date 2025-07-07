using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Inference;

public interface IInferenceService
{
    Task<LoadModelResponse> LoadModelAsync(LoadModelRequest request);
    Task<InferenceResponse> RunInferenceAsync(InferenceRequest request);
    Task<bool> UnloadModelAsync(string modelId);
    Task<ModelListResponse> GetLoadedModelsAsync();
    Task<LoadedModelInfo?> GetModelInfoAsync(string modelId);
    Task<InferenceStatusResponse?> GetInferenceStatusAsync(string sessionId);
    Task<bool> CancelInferenceAsync(string sessionId);
    Task InitializeAsync();
    Task<IEnumerable<InferenceSession>> GetActiveSessionsAsync(string? modelId = null);
    
    // Enhanced SDXL support
    Task<InferenceResponse> RunStructuredInferenceAsync(StructuredPromptRequest request);
    Task<InferenceResponse> RunEnhancedSDXLAsync(EnhancedSDXLRequest request);
}
