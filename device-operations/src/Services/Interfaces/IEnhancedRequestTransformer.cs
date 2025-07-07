using System;
using System.Collections.Generic;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;

namespace DeviceOperations.Services.Interfaces
{
    /// <summary>
    /// Interface for transforming C# Enhanced SDXL requests to Python worker format
    /// </summary>
    public interface IEnhancedRequestTransformer
    {
        /// <summary>
        /// Transform C# EnhancedSDXLRequest to Python worker format
        /// </summary>
        /// <param name="request">The C# enhanced SDXL request</param>
        /// <param name="requestId">Unique request identifier</param>
        /// <returns>Transformed request object for Python worker</returns>
        object TransformEnhancedSDXLRequest(EnhancedSDXLRequest request, string requestId);

        /// <summary>
        /// Validate enhanced SDXL request before transformation
        /// </summary>
        /// <param name="request">The request to validate</param>
        /// <param name="errors">List of validation errors if validation fails</param>
        /// <returns>True if valid, false otherwise</returns>
        bool ValidateRequest(EnhancedSDXLRequest request, out List<string> errors);

        /// <summary>
        /// Determine the appropriate worker type based on request features
        /// </summary>
        /// <param name="request">The enhanced SDXL request</param>
        /// <returns>Worker type string for routing</returns>
        string DetermineWorkerType(EnhancedSDXLRequest request);
    }

    /// <summary>
    /// Interface for handling Python worker responses and transforming them to C# format
    /// </summary>
    public interface IEnhancedResponseHandler
    {
        /// <summary>
        /// Transform Python worker response JSON to C# EnhancedSDXLResponse
        /// </summary>
        /// <param name="responseJson">JSON response from Python worker</param>
        /// <param name="requestId">Original request ID</param>
        /// <returns>Transformed C# response</returns>
        EnhancedSDXLResponse HandleEnhancedResponse(string responseJson, string requestId);

        /// <summary>
        /// Create error response for failed transformations
        /// </summary>
        /// <param name="requestId">Original request ID</param>
        /// <param name="errorMessage">Error message</param>
        /// <returns>Error response</returns>
        EnhancedSDXLResponse CreateErrorResponse(string requestId, string errorMessage);
    }
}
