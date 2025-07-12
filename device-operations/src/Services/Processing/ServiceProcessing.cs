using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using ResponseModels = DeviceOperations.Models.Responses;
using DeviceOperations.Services.Python;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.CSharp.RuntimeBinder;

namespace DeviceOperations.Services.Processing
{
    /// <summary>
    /// Service implementation for processing operations and workflow management
    /// Enhanced with domain-specific routing to replace broken PROCESSING worker calls
    /// Week 13: Communication Infrastructure Reconstruction - Processing Domain Phase 2
    /// </summary>
    public class ServiceProcessing : IServiceProcessing
    {
        private readonly ILogger<ServiceProcessing> _logger;
        private readonly IPythonWorkerService _pythonWorkerService;
        private readonly Dictionary<string, ProcessingWorkflow> _workflowCache;
        private readonly ConcurrentDictionary<string, ProcessingSession> _activeSessions;
        private readonly ConcurrentDictionary<string, ProcessingBatch> _activeBatches;
        private readonly ConcurrentDictionary<string, WorkflowDefinition> _workflowDefinitions;
        private readonly ConcurrentDictionary<string, Task> _batchMonitoringTasks;
        private DateTime _lastWorkflowRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(30);

        // Domain routing constants - Week 13 Infrastructure
        private static readonly Dictionary<string, string> StepTypeToDomainMapping = new()
        {
            {"device_discovery", PythonWorkerTypes.DEVICE},
            {"device_optimization", PythonWorkerTypes.DEVICE},
            {"device_status", PythonWorkerTypes.DEVICE},
            {"model_loading", PythonWorkerTypes.MODEL},
            {"model_validation", PythonWorkerTypes.MODEL},
            {"model_optimization", PythonWorkerTypes.MODEL},
            {"inference_generation", PythonWorkerTypes.INFERENCE},
            {"inference_batch", PythonWorkerTypes.INFERENCE},
            {"inference_optimization", PythonWorkerTypes.INFERENCE},
            {"postprocessing_enhancement", PythonWorkerTypes.POSTPROCESSING},
            {"postprocessing_upscale", PythonWorkerTypes.POSTPROCESSING},
            {"postprocessing_batch", PythonWorkerTypes.POSTPROCESSING}
        };

        public ServiceProcessing(
            ILogger<ServiceProcessing> logger,
            IPythonWorkerService pythonWorkerService)
        {
            _logger = logger;
            _pythonWorkerService = pythonWorkerService;
            _workflowCache = new Dictionary<string, ProcessingWorkflow>();
            _activeSessions = new ConcurrentDictionary<string, ProcessingSession>();
            _activeBatches = new ConcurrentDictionary<string, ProcessingBatch>();
            _workflowDefinitions = new ConcurrentDictionary<string, WorkflowDefinition>();
            _batchMonitoringTasks = new ConcurrentDictionary<string, Task>();
        }

        public async Task<ApiResponse<ResponseModels.GetProcessingWorkflowsResponse>> GetProcessingWorkflowsAsync()
        {
            try
            {
                _logger.LogInformation("Getting available processing workflows");

                await RefreshWorkflowsAsync();

                var allWorkflows = _workflowCache.Values.ToList();
                var categories = allWorkflows.Select(w => w.Category).Distinct().ToList();
                var popularWorkflows = allWorkflows
                    .OrderByDescending(w => w.UsageCount)
                    .Take(5)
                    .ToList();

                var response = new ResponseModels.GetProcessingWorkflowsResponse
                {
                    Workflows = allWorkflows.Select(w => new ResponseModels.WorkflowInfo
                    {
                        Id = w.Id,
                        Name = w.Name,
                        Description = w.Description,
                        Version = w.Version,
                        EstimatedDuration = w.EstimatedDuration,
                        Parameters = new List<ResponseModels.WorkflowParameter>(),
                        ResourceRequirements = new Dictionary<string, object>
                        {
                            ["memory"] = "4GB",
                            ["gpu_memory"] = "6GB"
                        }
                    }).ToList(),
                    TotalCount = allWorkflows.Count
                };

                _logger.LogInformation($"Retrieved {allWorkflows.Count} workflows across {categories.Count} categories");
                return ApiResponse<ResponseModels.GetProcessingWorkflowsResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get processing workflows");
                return ApiResponse<ResponseModels.GetProcessingWorkflowsResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get processing workflows: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<ResponseModels.GetProcessingWorkflowResponse>> GetProcessingWorkflowAsync(string idWorkflow)
        {
            try
            {
                _logger.LogInformation($"Getting processing workflow: {idWorkflow}");

                if (string.IsNullOrWhiteSpace(idWorkflow))
                    return ApiResponse<ResponseModels.GetProcessingWorkflowResponse>.CreateError(
                        new ErrorDetails { Message = "Workflow ID cannot be null or empty" });

                await RefreshWorkflowsAsync();

                if (!_workflowCache.TryGetValue(idWorkflow, out var workflow))
                {
                    // Week 13: Replace broken PROCESSING call with domain routing
                    _logger.LogInformation("Workflow not in cache, attempting to get definition: {WorkflowId}", idWorkflow);
                    
                    var workflowDefinition = await GetWorkflowDefinition(idWorkflow);
                    if (workflowDefinition != null)
                    {
                        workflow = new ProcessingWorkflow
                        {
                            Id = workflowDefinition.WorkflowId,
                            Name = workflowDefinition.Name,
                            Description = workflowDefinition.Description,
                            Version = "1.0",
                            EstimatedDuration = workflowDefinition.EstimatedDuration,
                            UsageCount = 0,
                            Category = "Generated"
                        };
                        _workflowCache[idWorkflow] = workflow;
                    }
                    else
                    {
                        return ApiResponse<ResponseModels.GetProcessingWorkflowResponse>.CreateError(
                            new ErrorDetails { Message = $"Workflow '{idWorkflow}' not found or not supported" });
                    }
                }

                var response = new ResponseModels.GetProcessingWorkflowResponse
                {
                    Workflow = new ResponseModels.WorkflowInfo
                    {
                        Id = workflow.Id,
                        Name = workflow.Name,
                        Description = workflow.Description,
                        Version = workflow.Version,
                        EstimatedDuration = workflow.EstimatedDuration,
                        Parameters = new List<ResponseModels.WorkflowParameter>(),
                        ResourceRequirements = await CalculateRequiredResourcesAsync(workflow)
                    }
                };

                _logger.LogInformation($"Retrieved workflow details for: {idWorkflow}");
                return ApiResponse<ResponseModels.GetProcessingWorkflowResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get processing workflow: {idWorkflow}");
                return ApiResponse<ResponseModels.GetProcessingWorkflowResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get workflow: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<ResponseModels.PostWorkflowExecuteResponse>> PostWorkflowExecuteAsync(PostWorkflowExecuteRequest request)
        {
            try
            {
                _logger.LogInformation("Starting workflow execution: {WorkflowId}", request.WorkflowId);

                if (request == null)
                    return ApiResponse<ResponseModels.PostWorkflowExecuteResponse>.CreateError(
                        new ErrorDetails { Message = "Workflow execution request cannot be null" });

                // Week 13: Replace broken PROCESSING call with sophisticated workflow coordination
                // Create processing session for tracking
                var sessionId = Guid.NewGuid().ToString();
                var session = new ProcessingSession
                {
                    Id = sessionId,
                    WorkflowId = request.WorkflowId,
                    Status = ProcessingStatus.Pending,
                    StartedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    CurrentStep = 0,
                    TotalSteps = 0,
                    Progress = 0
                };

                // Get workflow definition
                var workflowDefinition = await GetWorkflowDefinition(request.WorkflowId);
                if (workflowDefinition == null)
                {
                    return ApiResponse<ResponseModels.PostWorkflowExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Workflow '{request.WorkflowId}' not found or not supported" });
                }

                session.TotalSteps = workflowDefinition.Steps.Count;

                // Validate workflow requirements against available resources
                var validationResult = await ValidateWorkflowRequirements(workflowDefinition, request.Parameters);
                if (!validationResult.IsValid)
                {
                    return ApiResponse<ResponseModels.PostWorkflowExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Workflow validation failed: {validationResult.Error}" });
                }

                // Store session
                _activeSessions.TryAdd(sessionId, session);

                // Execute workflow steps
                var executionResult = await ExecuteWorkflowSteps(session, workflowDefinition, request.Parameters);
                
                if (!executionResult.IsSuccess)
                {
                    session.Status = ProcessingStatus.Failed;
                    session.LastUpdated = DateTime.UtcNow;
                    return ApiResponse<ResponseModels.PostWorkflowExecuteResponse>.CreateError(
                        new ErrorDetails { Message = executionResult.Error });
                }

                // Update session status
                session.Status = ProcessingStatus.Running;
                session.LastUpdated = DateTime.UtcNow;

                var response = new ResponseModels.PostWorkflowExecuteResponse
                {
                    ExecutionId = sessionId,
                    Status = "Running",
                    EstimatedCompletion = DateTime.UtcNow.Add(workflowDefinition.EstimatedDuration),
                    Progress = 0
                };

                _logger.LogInformation("Workflow started successfully: {WorkflowId}, Session: {SessionId}", 
                    request.WorkflowId, sessionId);
                return ApiResponse<ResponseModels.PostWorkflowExecuteResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute workflow: {WorkflowId}", request?.WorkflowId);
                return ApiResponse<ResponseModels.PostWorkflowExecuteResponse>.CreateError(
                    new ErrorDetails { Message = $"Workflow execution failed: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<ResponseModels.GetProcessingSessionsResponse>> GetProcessingSessionsAsync()
        {
            try
            {
                _logger.LogInformation("Getting all processing sessions");

                // Update session statuses
                await UpdateSessionStatusesAsync();

                var allSessions = _activeSessions.Values.ToList();
                var runningSessions = allSessions.Where(s => s.Status == ProcessingStatus.Running).ToList();
                var completedSessions = allSessions.Where(s => s.Status == ProcessingStatus.Completed).ToList();
                var failedSessions = allSessions.Where(s => s.Status == ProcessingStatus.Failed).ToList();

                var response = new ResponseModels.GetProcessingSessionsResponse
                {
                    Sessions = allSessions.Select(s => new Models.Responses.ProcessingSession
                    {
                        Id = s.Id,
                        Name = $"Session {s.Id[..8]}",
                        Status = s.Status.ToString(),
                        CreatedAt = s.StartedAt,
                        UpdatedAt = s.LastUpdated,
                        WorkflowId = s.WorkflowId,
                        Configuration = new Dictionary<string, object>
                        {
                            ["current_step"] = s.CurrentStep,
                            ["total_steps"] = s.TotalSteps,
                            ["progress"] = s.Progress
                        },
                        ResourceUsage = new Dictionary<string, object>
                        {
                            ["memory_used"] = "4GB",
                            ["cpu_usage"] = 0.65
                        }
                    }).ToList(),
                    TotalCount = allSessions.Count
                };

                _logger.LogInformation($"Retrieved {allSessions.Count} processing sessions");
                return ApiResponse<ResponseModels.GetProcessingSessionsResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get processing sessions");
                return ApiResponse<ResponseModels.GetProcessingSessionsResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get processing sessions: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get processing session with multi-domain status aggregation - Week 14 Enhancement
        /// </summary>
        public async Task<ApiResponse<ResponseModels.GetProcessingSessionResponse>> GetProcessingSessionAsync(string idSession)
        {
            try
            {
                _logger.LogInformation($"Getting processing session with multi-domain aggregation: {idSession}");

                if (string.IsNullOrWhiteSpace(idSession))
                    return ApiResponse<ResponseModels.GetProcessingSessionResponse>.CreateError(
                        new ErrorDetails { Message = "Session ID cannot be null or empty" });

                if (!_activeSessions.TryGetValue(idSession, out var session))
                {
                    return ApiResponse<ResponseModels.GetProcessingSessionResponse>.CreateError(
                        new ErrorDetails { Message = $"Session '{idSession}' not found" });
                }

                // Get involved domains for multi-domain status aggregation
                var involvedDomains = GetSessionInvolvedDomains(session);
                
                // Aggregate session status from all domains
                var domainStatuses = await AggregateSessionStatusFromDomains(session.Id, involvedDomains);
                
                // Calculate aggregated metrics
                var overallStatus = DetermineOverallSessionStatus(domainStatuses);
                var overallProgress = CalculateOverallProgress(domainStatuses);
                
                // Update session with aggregated information
                session.Status = overallStatus.Status;
                session.Progress = (int)Math.Round(overallProgress);
                session.LastUpdated = DateTime.UtcNow;

                var response = new ResponseModels.GetProcessingSessionResponse
                {
                    Session = new Models.Responses.ProcessingSession
                    {
                        Id = session.Id,
                        Name = $"Session {session.Id[..8]}",
                        Status = session.Status.ToString(),
                        CreatedAt = session.StartedAt,
                        UpdatedAt = session.LastUpdated,
                        WorkflowId = session.WorkflowId,
                        Configuration = new Dictionary<string, object>
                        {
                            ["current_step"] = session.CurrentStep,
                            ["total_steps"] = session.TotalSteps,
                            ["progress"] = session.Progress,
                            ["involved_domains"] = involvedDomains,
                            ["domain_statuses"] = domainStatuses.Select(d => new 
                            {
                                domain = d.Domain,
                                status = d.Status.ToString(),
                                progress = d.Progress,
                                current_operation = d.CurrentOperation,
                                last_updated = d.LastUpdated
                            }).ToList(),
                            ["detailed_progress"] = await GetDetailedProgressAsync(idSession),
                            ["execution_logs"] = await GetSessionExecutionLogsAsync(idSession),
                            ["output_preview"] = await GetSessionOutputPreviewAsync(idSession)
                        },
                        ResourceUsage = await GetSessionResourceUsageAsync(idSession)
                    }
                };

                _logger.LogInformation($"Retrieved session with multi-domain aggregation: {idSession}, Overall Status: {overallStatus.Status}, Progress: {overallProgress:F1}%");
                return ApiResponse<ResponseModels.GetProcessingSessionResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get processing session with multi-domain aggregation: {idSession}");
                return ApiResponse<ResponseModels.GetProcessingSessionResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get processing session: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<ResponseModels.PostSessionControlResponse>> PostSessionControlAsync(string idSession, PostSessionControlRequest request)
        {
            try
            {
                _logger.LogInformation("Controlling session: {SessionId}, Action: {Action}", idSession, request.Action);

                if (string.IsNullOrWhiteSpace(idSession))
                    return ApiResponse<ResponseModels.PostSessionControlResponse>.CreateError(
                        new ErrorDetails { Message = "Session ID cannot be null or empty" });

                if (request == null)
                    return ApiResponse<ResponseModels.PostSessionControlResponse>.CreateError(
                        new ErrorDetails { Message = "Session control request cannot be null" });

                // Week 13: Replace broken PROCESSING call with multi-domain coordination
                if (!_activeSessions.TryGetValue(idSession, out var session))
                {
                    return ApiResponse<ResponseModels.PostSessionControlResponse>.CreateError(
                        new ErrorDetails { Message = $"Session '{idSession}' not found" });
                }

                // Validate control action
                if (!IsValidControlAction(session.Status, request.Action))
                {
                    return ApiResponse<ResponseModels.PostSessionControlResponse>.CreateError(
                        new ErrorDetails { Message = $"Cannot {request.Action} session in {session.Status} status" });
                }

                // Get involved domains for this session (simplified - could be enhanced)
                var involvedDomains = GetSessionInvolvedDomains(session);

                // Send control command to all involved domains
                var controlResults = await SendControlToAllDomains(idSession, involvedDomains, request.Action, request.Parameters ?? new Dictionary<string, object>());
                
                // Check if all domains successfully handled the control command
                var failedDomains = controlResults.Where(r => !r.IsSuccess).ToList();
                if (failedDomains.Any())
                {
                    var errors = string.Join(", ", failedDomains.Select(f => $"{f.Domain}: {f.Error}"));
                    return ApiResponse<ResponseModels.PostSessionControlResponse>.CreateError(
                        new ErrorDetails { Message = $"Control action failed in domains: {errors}" });
                }

                // Update session status
                var newStatus = DetermineNewStatus(session.Status, request.Action);
                session.Status = newStatus;
                session.LastUpdated = DateTime.UtcNow;

                var response = new ResponseModels.PostSessionControlResponse
                {
                    Action = request.Action,
                    Result = "Success",
                    Status = newStatus.ToString()
                };

                _logger.LogInformation("Session control completed: {SessionId}, Action: {Action}, Status: {Status}", 
                    idSession, request.Action, newStatus);
                return ApiResponse<ResponseModels.PostSessionControlResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to control session: {SessionId}, Action: {Action}", idSession, request.Action);
                return ApiResponse<ResponseModels.PostSessionControlResponse>.CreateError(
                    new ErrorDetails { Message = $"Session control failed: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<ResponseModels.DeleteProcessingSessionResponse>> DeleteProcessingSessionAsync(string idSession)
        {
            try
            {
                _logger.LogInformation($"Deleting processing session: {idSession}");

                if (string.IsNullOrWhiteSpace(idSession))
                    return ApiResponse<ResponseModels.DeleteProcessingSessionResponse>.CreateError(
                        new ErrorDetails { Message = "Session ID cannot be null or empty" });

                if (!_activeSessions.TryGetValue(idSession, out var session))
                {
                    return ApiResponse<ResponseModels.DeleteProcessingSessionResponse>.CreateError(
                        new ErrorDetails { Message = $"Session '{idSession}' not found" });
                }

                var wasRunning = session.Status == ProcessingStatus.Running;
                
                // Week 13: Replace broken PROCESSING call with coordinated cleanup across domains
                _logger.LogInformation("Starting session cleanup: {SessionId}", idSession);
                
                try
                {
                    // Get involved domains for cleanup coordination
                    var involvedDomains = GetSessionInvolvedDomains(session);

                    // Send cleanup command to all involved domains
                    var cleanupResults = await SendCleanupToAllDomains(idSession, involvedDomains, wasRunning);
                    
                    // Log any failed cleanups but continue with session deletion
                    var failedCleanups = cleanupResults.Where(r => !r.IsSuccess).ToList();
                    if (failedCleanups.Any())
                    {
                        var failures = string.Join(", ", failedCleanups.Select(f => $"{f.Domain}: {f.Error}"));
                        _logger.LogWarning("Some domain cleanups failed for session {SessionId}: {Failures}", idSession, failures);
                    }

                    // Update session status
                    session.Status = ProcessingStatus.Cancelled;
                    session.LastUpdated = DateTime.UtcNow;
                    session.CompletedAt = DateTime.UtcNow;

                    var response = new ResponseModels.DeleteProcessingSessionResponse
                    {
                        Success = true,
                        Message = $"Session {idSession} deleted successfully"
                    };

                    // Remove from active sessions
                    _activeSessions.TryRemove(idSession, out _);

                    _logger.LogInformation($"Successfully deleted session: {idSession}");
                    return ApiResponse<ResponseModels.DeleteProcessingSessionResponse>.CreateSuccess(response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to delete processing session: {idSession}");
                    return ApiResponse<ResponseModels.DeleteProcessingSessionResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to delete processing session: {ex.Message}" });
                }
            }
            catch (Exception outerEx)
            {
                _logger.LogError(outerEx, $"Outer exception in delete processing session: {idSession}");
                return ApiResponse<ResponseModels.DeleteProcessingSessionResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to delete processing session: {outerEx.Message}" });
            }
        }

        public async Task<ApiResponse<ResponseModels.GetProcessingBatchesResponse>> GetProcessingBatchesAsync()
        {
            try
            {
                _logger.LogInformation("Getting all processing batches");

                // Update batch statuses
                await UpdateBatchStatusesAsync();

                var allBatches = _activeBatches.Values.ToList();
                var runningBatches = allBatches.Where(b => b.Status == ProcessingStatus.Running).ToList();
                var completedBatches = allBatches.Where(b => b.Status == ProcessingStatus.Completed).ToList();

                var response = new ResponseModels.GetProcessingBatchesResponse
                {
                    Batches = allBatches.Select(b => new ResponseModels.BatchJob
                    {
                        Id = b.Id,
                        Name = b.Name,
                        Type = "ProcessingBatch",
                        Status = b.Status.ToString(),
                        CreatedAt = b.CreatedAt,
                        StartedAt = b.StartedAt,
                        CompletedAt = b.CompletedAt,
                        TotalItems = b.TotalItems,
                        CompletedItems = b.ProcessedItems,
                        FailedItems = b.FailedItems,
                        Progress = new ResponseModels.BatchProgress
                        {
                            Percentage = b.TotalItems > 0 ? (double)b.ProcessedItems / b.TotalItems * 100 : 0,
                            ItemsProcessed = b.ProcessedItems,
                            TotalItems = b.TotalItems,
                            ProcessingRate = 2.5 // Mock rate
                        },
                        Configuration = new Dictionary<string, object>
                        {
                            ["workflow_id"] = b.WorkflowId,
                            ["priority"] = b.Priority
                        }
                    }).ToList(),
                    TotalCount = allBatches.Count
                };

                _logger.LogInformation($"Retrieved {allBatches.Count} processing batches");
                return ApiResponse<ResponseModels.GetProcessingBatchesResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get processing batches");
                return ApiResponse<ResponseModels.GetProcessingBatchesResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get processing batches: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<ResponseModels.GetProcessingBatchResponse>> GetProcessingBatchAsync(string idBatch)
        {
            try
            {
                _logger.LogInformation($"Getting processing batch: {idBatch}");

                if (string.IsNullOrWhiteSpace(idBatch))
                    return ApiResponse<ResponseModels.GetProcessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = "Batch ID cannot be null or empty" });

                if (!_activeBatches.TryGetValue(idBatch, out var batch))
                {
                    return ApiResponse<ResponseModels.GetProcessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = $"Batch '{idBatch}' not found" });
                }

                // Update batch status
                await UpdateBatchStatusAsync(batch);

                var response = new ResponseModels.GetProcessingBatchResponse
                {
                    Batch = new ResponseModels.BatchJob
                    {
                        Id = batch.Id,
                        Name = batch.Name,
                        Type = "ProcessingBatch",
                        Status = batch.Status.ToString(),
                        CreatedAt = batch.CreatedAt,
                        StartedAt = batch.StartedAt,
                        CompletedAt = batch.CompletedAt,
                        TotalItems = batch.TotalItems,
                        CompletedItems = batch.ProcessedItems,
                        FailedItems = batch.FailedItems,
                        Progress = new ResponseModels.BatchProgress
                        {
                            Percentage = batch.TotalItems > 0 ? (double)batch.ProcessedItems / batch.TotalItems * 100 : 0,
                            ItemsProcessed = batch.ProcessedItems,
                            TotalItems = batch.TotalItems,
                            ProcessingRate = 2.5 // Mock rate
                        },
                        Configuration = new Dictionary<string, object>
                        {
                            ["workflow_id"] = batch.WorkflowId,
                            ["priority"] = batch.Priority
                        }
                    }
                };

                _logger.LogInformation($"Retrieved batch details for: {idBatch}");
                return ApiResponse<ResponseModels.GetProcessingBatchResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get processing batch: {idBatch}");
                return ApiResponse<ResponseModels.GetProcessingBatchResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get processing batch: {ex.Message}" });
            }
        }

        public Task<ApiResponse<ResponseModels.PostBatchCreateResponse>> PostBatchCreateAsync(PostBatchCreateRequest request)
        {
            try
            {
                _logger.LogInformation("Creating new processing batch");

                if (request == null)
                    return Task.FromResult(ApiResponse<ResponseModels.PostBatchCreateResponse>.CreateError(
                        new ErrorDetails { Message = "Batch creation request cannot be null" }));

                if (request.Items?.Any() != true)
                    return Task.FromResult(ApiResponse<ResponseModels.PostBatchCreateResponse>.CreateError(
                        new ErrorDetails { Message = "At least one batch item must be specified" }));

                var batchId = Guid.NewGuid().ToString();
                var batch = new ProcessingBatch
                {
                    Id = batchId,
                    Name = request.Name,
                    WorkflowId = request.Type, // Use Type as WorkflowId since WorkflowId doesn't exist in request
                    TotalItems = request.Items.Count,
                    ProcessedItems = 0,
                    FailedItems = 0,
                    Status = ProcessingStatus.Created,
                    CreatedAt = DateTime.UtcNow,
                    Priority = request.Priority
                };

                _activeBatches[batchId] = batch;

                var response = new ResponseModels.PostBatchCreateResponse
                {
                    BatchId = batchId,
                    Status = ProcessingStatus.Created.ToString(),
                    EstimatedDuration = TimeSpan.FromMinutes(request.Items.Count * 2) // 2 minutes per item estimate
                };

                _logger.LogInformation($"Created batch: {batchId} with {request.Items.Count} items");
                return Task.FromResult(ApiResponse<ResponseModels.PostBatchCreateResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create processing batch");
                return Task.FromResult(ApiResponse<ResponseModels.PostBatchCreateResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to create batch: {ex.Message}" }));
            }
        }

        public async Task<ApiResponse<ResponseModels.PostBatchExecuteResponse>> PostBatchExecuteAsync(string idBatch, PostBatchExecuteRequest request)
        {
            try
            {
                _logger.LogInformation($"Executing processing batch: {idBatch}");

                if (string.IsNullOrWhiteSpace(idBatch))
                    return ApiResponse<ResponseModels.PostBatchExecuteResponse>.CreateError(
                        new ErrorDetails { Message = "Batch ID cannot be null or empty" });

                if (!_activeBatches.TryGetValue(idBatch, out var batch))
                {
                    return ApiResponse<ResponseModels.PostBatchExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Batch '{idBatch}' not found" });
                }

                if (batch.Status != ProcessingStatus.Created && batch.Status != ProcessingStatus.Paused)
                {
                    return ApiResponse<ResponseModels.PostBatchExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Batch '{idBatch}' cannot be executed in current status: {batch.Status}" });
                }

                // Week 13: Replace broken PROCESSING call with sophisticated batch coordination
                _logger.LogInformation("Starting batch execution: {BatchId}", idBatch);
                
                try
                {
                    // Prepare sophisticated batch request for Python BatchManager coordination
                    var batchRequest = new
                    {
                        request_id = Guid.NewGuid().ToString(),
                        batch_id = idBatch,
                        action = "batch_process",
                        data = new
                        {
                            batch_config = new
                            {
                                total_items = batch.TotalItems,
                                preferred_batch_size = CalculateOptimalBatchSize(batch),
                                max_batch_size = GetMaxBatchSize(),
                                enable_dynamic_sizing = true,
                                memory_threshold = 0.8,                            parallel_processing = request?.Parameters?.GetValueOrDefault("enable_parallel", false),
                            max_parallel_batches = Convert.ToInt32(request?.Parameters?.GetValueOrDefault("max_parallel_batches", 2))
                            },
                            generation_params = CreateBatchGenerationParams(batch, request),
                            callback_config = new
                            {
                                progress_callback = true,
                                batch_callback = true,
                                memory_callback = true
                            }
                        }
                    };

                    // Delegate to Python BatchManager for sophisticated processing
                    var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                        PythonWorkerTypes.INFERENCE, "batch_process", batchRequest);

                    if (pythonResponse?.success == true)
                    {
                        // Update batch status with Python response
                        batch.Status = ProcessingStatus.Running;
                        batch.StartedAt = DateTime.UtcNow;
                        batch.LastUpdated = DateTime.UtcNow;
                        
                        // Start background monitoring of batch progress
                        _ = Task.Run(() => MonitorBatchProgress(idBatch, pythonResponse.data?.batch_tracking_id?.ToString()));

                        var response = new ResponseModels.PostBatchExecuteResponse
                        {
                            ExecutionId = idBatch,
                            Status = ProcessingStatus.Running.ToString(),
                            Progress = new ResponseModels.BatchProgress
                            {
                                Percentage = 0,
                                ItemsProcessed = 0,
                                TotalItems = batch.TotalItems,
                                ProcessingRate = 2.0 // Will be updated by monitoring
                            }
                        };

                        var trackingId = pythonResponse.data?.batch_tracking_id?.ToString() ?? "unknown";
                        _logger.LogInformation("Batch execution started: BatchId={BatchId}, TrackingId={TrackingId}", 
                            (object)idBatch, (object)trackingId);
                        return ApiResponse<ResponseModels.PostBatchExecuteResponse>.CreateSuccess(response);
                    }
                    else
                    {
                        var error = pythonResponse?.error?.ToString() ?? "Unknown error during batch execution";
                        batch.Status = ProcessingStatus.Failed;
                        batch.LastUpdated = DateTime.UtcNow;
                        _logger.LogError("Batch execution failed: BatchId={BatchId}, Error={Error}", (object)idBatch, (object)error);
                        return ApiResponse<ResponseModels.PostBatchExecuteResponse>.CreateError(
                            new ErrorDetails { Message = $"Batch execution failed: {error}" });
                    }
                }
                catch (Exception ex)
                {
                    batch.Status = ProcessingStatus.Failed;
                    batch.LastUpdated = DateTime.UtcNow;
                    _logger.LogError(ex, "Exception during batch execution: {BatchId}", idBatch);
                    return ApiResponse<ResponseModels.PostBatchExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Batch execution failed: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute processing batch: {BatchId}", idBatch);
                return ApiResponse<ResponseModels.PostBatchExecuteResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to execute batch: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<ResponseModels.DeleteProcessingBatchResponse>> DeleteProcessingBatchAsync(string idBatch)
        {
            try
            {
                _logger.LogInformation($"Deleting processing batch: {idBatch}");

                if (string.IsNullOrWhiteSpace(idBatch))
                    return ApiResponse<ResponseModels.DeleteProcessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = "Batch ID cannot be null or empty" });

                if (!_activeBatches.TryGetValue(idBatch, out var batch))
                {
                    return ApiResponse<ResponseModels.DeleteProcessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = $"Batch '{idBatch}' not found" });
                }

                var wasRunning = batch.Status == ProcessingStatus.Running;
                
                // Week 13: Replace broken PROCESSING call with coordinated batch cleanup across domains
                _logger.LogInformation("Starting batch cleanup: {BatchId}", idBatch);
                
                try
                {
                    // Get involved domains for this batch
                    var involvedDomains = GetBatchInvolvedDomains(batch);

                    // Send cleanup command to all involved domains
                    var cleanupResults = await SendBatchCleanupToAllDomains(idBatch, involvedDomains, wasRunning);
                    
                    // Log any failed cleanups but continue with batch deletion
                    var failedCleanups = cleanupResults.Where(r => !r.IsSuccess).ToList();
                    if (failedCleanups.Any())
                    {
                        var failures = string.Join(", ", failedCleanups.Select(f => $"{f.Domain}: {f.Error}"));
                        _logger.LogWarning("Some domain cleanups failed for batch {BatchId}: {Failures}", idBatch, failures);
                    }

                    // Stop background monitoring if running
                    if (wasRunning && _batchMonitoringTasks.TryRemove(idBatch, out var monitoringTask))
                    {
                        try
                        {
                            await monitoringTask;
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected when canceling monitoring
                        }
                    }

                    // Update batch status
                    batch.Status = ProcessingStatus.Cancelled;
                    batch.LastUpdated = DateTime.UtcNow;
                    batch.CompletedAt = DateTime.UtcNow;

                    var response = new ResponseModels.DeleteProcessingBatchResponse
                    {
                        Success = true,
                        Message = $"Batch {idBatch} deleted successfully"
                    };

                    // Remove from active batches
                    _activeBatches.TryRemove(idBatch, out _);

                    _logger.LogInformation($"Successfully deleted batch: {idBatch}");
                    return ApiResponse<ResponseModels.DeleteProcessingBatchResponse>.CreateSuccess(response);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Error during batch cleanup, continuing with deletion: {BatchId}", idBatch);
                    
                    // Still complete the deletion even if cleanup failed
                    batch.Status = ProcessingStatus.Cancelled;
                    batch.LastUpdated = DateTime.UtcNow;
                    batch.CompletedAt = DateTime.UtcNow;

                    var response = new ResponseModels.DeleteProcessingBatchResponse
                    {
                        Success = true,
                        Message = $"Batch {idBatch} deleted successfully (with cleanup warnings)"
                    };

                    // Remove from active batches
                    _activeBatches.TryRemove(idBatch, out _);

                    return ApiResponse<ResponseModels.DeleteProcessingBatchResponse>.CreateSuccess(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete processing batch: {idBatch}");
                return ApiResponse<ResponseModels.DeleteProcessingBatchResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to delete processing batch: {ex.Message}" });
            }
        }

        #region Private Helper Methods

        private async Task RefreshWorkflowsAsync()
        {
            if (DateTime.UtcNow - _lastWorkflowRefresh < _cacheTimeout && _workflowCache.Count > 0)
                return;

            try
            {
                // Week 13: Replace broken PROCESSING call with domain workflow discovery
                _logger.LogInformation("Refreshing workflows from all domains");
                
                var discoveredWorkflows = new List<ProcessingWorkflow>();
                
                // Discover workflows from each domain
                var domains = new[] { "device", "model", "inference", "postprocessing" };
                
                foreach (var domain in domains)
                {
                    try
                    {
                        var domainWorkflows = await DiscoverWorkflowsFromDomain(domain);
                        discoveredWorkflows.AddRange(domainWorkflows);
                        
                        _logger.LogDebug("Discovered {Count} workflows from {Domain} domain", 
                            domainWorkflows.Count, domain);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to discover workflows from domain: {Domain}", domain);
                    }
                }
                
                // Update workflow cache with discovered workflows
                _workflowCache.Clear();
                foreach (var workflow in discoveredWorkflows)
                {
                    _workflowCache[workflow.Id] = workflow;
                }
                
                // Add predefined C# workflows if none discovered
                if (_workflowCache.Count == 0)
                {
                    await PopulateMockWorkflowsAsync();
                }
                
                _lastWorkflowRefresh = DateTime.UtcNow;
                _logger.LogInformation("Workflows refreshed: {Count} total workflows from {DomainCount} domains", 
                    _workflowCache.Count, domains.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh workflows from Python worker, using mock data");
                await PopulateMockWorkflowsAsync();
            }
        }

        private ProcessingWorkflow CreateWorkflowFromPython(dynamic pythonWorkflow)
        {
            return new ProcessingWorkflow
            {
                Id = pythonWorkflow.id?.ToString() ?? Guid.NewGuid().ToString(),
                Name = pythonWorkflow.name?.ToString() ?? "Unknown Workflow",
                Description = pythonWorkflow.description?.ToString() ?? "No description available",
                Category = pythonWorkflow.category?.ToString() ?? "General",
                Version = pythonWorkflow.version?.ToString() ?? "1.0.0",
                Steps = pythonWorkflow.steps?.ToObject<List<WorkflowStep>>() ?? new List<WorkflowStep>(),
                RequiredModels = pythonWorkflow.required_models?.ToObject<List<string>>() ?? new List<string>(),
                EstimatedDuration = TimeSpan.FromSeconds(pythonWorkflow.estimated_duration ?? 120),
                IsAvailable = pythonWorkflow.is_available ?? true,
                UsageCount = pythonWorkflow.usage_count ?? 0,
                AverageRating = pythonWorkflow.average_rating ?? 4.5f,
                CreatedAt = pythonWorkflow.created_at != null ? DateTime.Parse(pythonWorkflow.created_at.ToString()) : DateTime.UtcNow.AddDays(-30),
                UpdatedAt = pythonWorkflow.updated_at != null ? DateTime.Parse(pythonWorkflow.updated_at.ToString()) : DateTime.UtcNow.AddDays(-1)
            };
        }

        private async Task PopulateMockWorkflowsAsync()
        {
            await Task.Delay(1); // Simulate async operation

            var mockWorkflows = new[]
            {
                new ProcessingWorkflow
                {
                    Id = "img-generation-basic",
                    Name = "Basic Image Generation",
                    Description = "Simple text-to-image generation workflow",
                    Category = "Image Generation",
                    Version = "1.0.0",
                    Steps = new List<WorkflowStep>
                    {
                        new WorkflowStep { Order = 1, Operation = "load_model", Description = "Load generation model" },
                        new WorkflowStep { Order = 2, Operation = "process_prompt", Description = "Process text prompt" },
                        new WorkflowStep { Order = 3, Operation = "generate", Description = "Generate image" },
                        new WorkflowStep { Order = 4, Operation = "save_output", Description = "Save generated image" }
                    },
                    RequiredModels = new List<string> { "sd15-base" },
                    EstimatedDuration = TimeSpan.FromMinutes(2),
                    IsAvailable = true,
                    UsageCount = 1250,
                    AverageRating = 4.3f,
                    CreatedAt = DateTime.UtcNow.AddDays(-60),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new ProcessingWorkflow
                {
                    Id = "img-upscale-enhance",
                    Name = "Image Upscale & Enhance",
                    Description = "Complete image enhancement workflow with upscaling and restoration",
                    Category = "Image Enhancement",
                    Version = "2.1.0",
                    Steps = new List<WorkflowStep>
                    {
                        new WorkflowStep { Order = 1, Operation = "load_image", Description = "Load input image" },
                        new WorkflowStep { Order = 2, Operation = "upscale", Description = "AI upscaling" },
                        new WorkflowStep { Order = 3, Operation = "face_restore", Description = "Face restoration" },
                        new WorkflowStep { Order = 4, Operation = "enhance", Description = "General enhancement" },
                        new WorkflowStep { Order = 5, Operation = "save_output", Description = "Save enhanced image" }
                    },
                    RequiredModels = new List<string> { "esrgan-x4", "gfpgan" },
                    EstimatedDuration = TimeSpan.FromMinutes(5),
                    IsAvailable = true,
                    UsageCount = 850,
                    AverageRating = 4.7f,
                    CreatedAt = DateTime.UtcNow.AddDays(-45),
                    UpdatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new ProcessingWorkflow
                {
                    Id = "batch-style-transfer",
                    Name = "Batch Style Transfer",
                    Description = "Apply artistic style transfer to multiple images",
                    Category = "Artistic Processing",
                    Version = "1.5.0",
                    Steps = new List<WorkflowStep>
                    {
                        new WorkflowStep { Order = 1, Operation = "load_style_model", Description = "Load style transfer model" },
                        new WorkflowStep { Order = 2, Operation = "process_batch", Description = "Process image batch" },
                        new WorkflowStep { Order = 3, Operation = "apply_style", Description = "Apply style transfer" },
                        new WorkflowStep { Order = 4, Operation = "save_results", Description = "Save styled images" }
                    },
                    RequiredModels = new List<string> { "neural-style-transfer" },
                    EstimatedDuration = TimeSpan.FromMinutes(8),
                    IsAvailable = true,
                    UsageCount = 420,
                    AverageRating = 4.1f,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    UpdatedAt = DateTime.UtcNow.AddDays(-7)
                }
            };

            _workflowCache.Clear();
            foreach (var workflow in mockWorkflows)
            {
                _workflowCache[workflow.Id] = workflow;
            }
        }

        private async Task UpdateSessionStatusesAsync()
        {
            try
            {
                // Get all active sessions that need status updates
                var activeSessions = _activeSessions.Values
                    .Where(s => s.Status == ProcessingStatus.Running || s.Status == ProcessingStatus.Pending)
                    .ToList();

                foreach (var session in activeSessions)
                {
                    await UpdateSessionStatusWithDomainAggregationAsync(session);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update session statuses");
            }
        }

        /// <summary>
        /// Update session status with multi-domain aggregation - Week 13 Implementation
        /// </summary>
        private async Task UpdateSessionStatusWithDomainAggregationAsync(ProcessingSession session)
        {
            try
            {
                _logger.LogDebug("Updating session status with domain aggregation: {SessionId}", session.Id);
                
                // Get involved domains for this session
                var involvedDomains = GetSessionInvolvedDomains(session);
                
                // Aggregate status from all involved domains
                var domainStatuses = await AggregateSessionStatusFromDomains(session.Id, involvedDomains);
                
                // Determine overall session status based on domain aggregation
                var overallStatus = DetermineOverallSessionStatus(domainStatuses);
                var overallProgress = CalculateOverallProgress(domainStatuses);
                
                // Update session with aggregated information
                session.Status = overallStatus.Status;
                session.Progress = (int)Math.Round(overallProgress);
                
                // Update current step index based on status
                if (overallStatus.Status == ProcessingStatus.Running && session.CurrentStep < session.TotalSteps)
                {
                    session.CurrentStep += 1;
                }
                else if (overallStatus.Status == ProcessingStatus.Completed)
                {
                    session.CurrentStep = session.TotalSteps;
                }
                
                session.LastUpdated = DateTime.UtcNow;
                
                // Set completion time if session is completed
                if (session.Status == ProcessingStatus.Completed && session.CompletedAt == null)
                {
                    session.CompletedAt = DateTime.UtcNow;
                }
                
                _logger.LogDebug("Session status updated: {SessionId}, Status: {Status}, Progress: {Progress}%, Step: {CurrentStep}/{TotalSteps}", 
                    session.Id, session.Status, session.Progress, session.CurrentStep, session.TotalSteps);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update session status with domain aggregation: {SessionId}", session.Id);
                // Keep existing status on error
            }
        }

        private async Task UpdateBatchStatusesAsync()
        {
            try
            {
                // Get all active batches that need status updates - Week 13 Implementation
                var activeBatches = _activeBatches.Values
                    .Where(b => b.Status == ProcessingStatus.Running || b.Status == ProcessingStatus.Pending)
                    .ToList();

                foreach (var batch in activeBatches)
                {
                    await UpdateBatchStatusWithCoordinationAsync(batch);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update batch statuses");
            }
        }

        /// <summary>
        /// Update batch status with multi-domain coordination - Week 13 Implementation
        /// Replaces broken PythonWorkerTypes.PROCESSING call with sophisticated coordination
        /// </summary>
        private async Task UpdateBatchStatusWithCoordinationAsync(ProcessingBatch batch)
        {
            try
            {
                _logger.LogDebug("Updating batch status with coordination: {BatchId}", batch.Id);

                // Get involved domains for this batch
                var involvedDomains = GetBatchInvolvedDomains(batch);

                // Request status from inference domain (primary for batch processing)
                var statusRequest = new
                {
                    request_id = Guid.NewGuid().ToString(),
                    batch_id = batch.Id,
                    action = "get_batch_status",
                    data = new { include_progress = true, include_metrics = true }
                };

                var response = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_batch_status", statusRequest);

                if (response?.success == true && response.data != null)
                {
                    // Update batch with response data
                    var status = ParseBatchStatus(response.data?.status?.ToString());
                    var processedItems = ParseBatchProcessedItems(response.data?.processed_items);
                    var failedItems = ParseBatchFailedItems(response.data?.failed_items);

                    batch.Status = status;
                    batch.ProcessedItems = processedItems;
                    batch.FailedItems = failedItems;
                    batch.LastUpdated = DateTime.UtcNow;

                    // Set completion time if batch is completed
                    if (batch.Status == ProcessingStatus.Completed && batch.CompletedAt == null)
                    {
                        batch.CompletedAt = DateTime.UtcNow;
                    }

                    _logger.LogDebug("Batch status updated: {BatchId}, Status: {Status}, Processed: {Processed}/{Total}, Failed: {Failed}",
                        batch.Id, batch.Status, batch.ProcessedItems, batch.TotalItems, batch.FailedItems);
                }
                else
                {
                    _logger.LogWarning("Failed to get batch status from Python workers: {BatchId}", batch.Id);
                    // Keep existing status on failure
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update batch status with coordination: {BatchId}", batch.Id);
                // Keep existing status on error
            }
        }

        // Additional helper methods for mock data...
        private async Task<List<ExecutionHistoryItem>> GetWorkflowExecutionHistoryAsync(string workflowId)
        {
            await Task.Delay(1);
            return new List<ExecutionHistoryItem>
            {
                new ExecutionHistoryItem { ExecutedAt = DateTime.UtcNow.AddHours(-2), Success = true, Duration = TimeSpan.FromMinutes(3) },
                new ExecutionHistoryItem { ExecutedAt = DateTime.UtcNow.AddHours(-6), Success = true, Duration = TimeSpan.FromMinutes(2.5) }
            };
        }

        private async Task<Dictionary<string, object>> GetWorkflowPerformanceAsync(string workflowId)
        {
            await Task.Delay(1);
            return new Dictionary<string, object>
            {
                ["average_duration"] = "2.8 minutes",
                ["success_rate"] = 0.95,
                ["last_30_days_executions"] = 45
            };
        }

        private async Task<Dictionary<string, object>> CalculateRequiredResourcesAsync(ProcessingWorkflow workflow)
        {
            await Task.Delay(1);
            return new Dictionary<string, object>
            {
                ["estimated_memory"] = "6GB",
                ["estimated_gpu_memory"] = "8GB",
                ["cpu_cores"] = 4,
                ["estimated_duration"] = workflow.EstimatedDuration.TotalMinutes
            };
        }

        private TimeSpan CalculateAverageExecutionTime(List<ProcessingSession> completedSessions)
        {
            if (!completedSessions.Any()) return TimeSpan.Zero;

            var totalMinutes = completedSessions
                .Where(s => s.CompletedAt.HasValue)
                .Average(s => (s.CompletedAt!.Value - s.StartedAt).TotalMinutes);

            return TimeSpan.FromMinutes(totalMinutes);
        }

        private async Task<Dictionary<string, object>> GetDetailedProgressAsync(string sessionId)
        {
            await Task.Delay(1);
            return new Dictionary<string, object>
            {
                ["current_operation"] = "Generating image",
                ["substep_progress"] = 0.75,
                ["eta_seconds"] = 30
            };
        }

        private async Task<List<string>> GetSessionExecutionLogsAsync(string sessionId)
        {
            await Task.Delay(1);
            return new List<string>
            {
                $"[{DateTime.UtcNow.AddMinutes(-5):HH:mm:ss}] Session started",
                $"[{DateTime.UtcNow.AddMinutes(-4):HH:mm:ss}] Model loaded successfully",
                $"[{DateTime.UtcNow.AddMinutes(-2):HH:mm:ss}] Processing step 2 of 4"
            };
        }

        private async Task<Dictionary<string, object>> GetSessionResourceUsageAsync(string sessionId)
        {
            await Task.Delay(1);
            return new Dictionary<string, object>
            {
                ["memory_used"] = "4.2GB",
                ["gpu_memory_used"] = "6.1GB",
                ["cpu_usage"] = 0.65
            };
        }

        private async Task<Dictionary<string, object>> GetSessionOutputPreviewAsync(string sessionId)
        {
            await Task.Delay(1);
            return new Dictionary<string, object>
            {
                ["preview_available"] = true,
                ["output_format"] = "PNG",
                ["file_size"] = "2.4MB"
            };
        }

        private async Task<List<BatchItemDetail>> GetBatchItemDetailsAsync(string batchId)
        {
            await Task.Delay(1);
            return new List<BatchItemDetail>
            {
                new BatchItemDetail { ItemId = "item_1", Status = "completed", Progress = 100 },
                new BatchItemDetail { ItemId = "item_2", Status = "processing", Progress = 45 }
            };
        }

        private async Task<Dictionary<string, object>> GetBatchExecutionMetricsAsync(string batchId)
        {
            await Task.Delay(1);
            return new Dictionary<string, object>
            {
                ["average_item_duration"] = "2.1 minutes",
                ["throughput_items_per_hour"] = 28,
                ["estimated_completion"] = DateTime.UtcNow.AddMinutes(15)
            };
        }

        private async Task<Dictionary<string, object>> GetBatchResourceUtilizationAsync(string batchId)
        {
            await Task.Delay(1);
            return new Dictionary<string, object>
            {
                ["total_memory_used"] = "8.5GB",
                ["worker_processes"] = 3,
                ["average_cpu_usage"] = 0.72
            };
        }

        /// <summary>
        /// Discover available workflows from a specific domain
        /// </summary>
        private async Task<List<ProcessingWorkflow>> DiscoverWorkflowsFromDomain(string domain)
        {
            var workflows = new List<ProcessingWorkflow>();
            
            try
            {
                var discoveryRequest = new
                {
                    request_id = Guid.NewGuid().ToString(),
                    action = "discover_workflows",
                    data = new
                    {
                        domain = domain,
                        include_templates = true
                    }
                };

                var workerType = domain.ToLowerInvariant() switch
                {
                    "device" => PythonWorkerTypes.DEVICE,
                    "model" => PythonWorkerTypes.MODEL,
                    "inference" => PythonWorkerTypes.INFERENCE,
                    "postprocessing" => PythonWorkerTypes.POSTPROCESSING,
                    _ => throw new InvalidOperationException($"Unknown domain: {domain}")
                };

                var response = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    workerType, "discover_workflows", discoveryRequest);

                if (response?.success == true && response.data?.workflows != null)
                {
                    try
                    {
                        foreach (var workflowData in response.data.workflows)
                        {
                            if (workflowData != null)
                            {
                                var workflow = CreateWorkflowFromDomainData(workflowData, domain);
                                workflows.Add(workflow);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing workflows from domain: {Domain}", domain);
                    }
                }
                else
                {
                    // Create default workflows for this domain
                    workflows.AddRange(CreateDefaultWorkflowsForDomain(domain));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to discover workflows from domain: {Domain}, using defaults", domain);
                workflows.AddRange(CreateDefaultWorkflowsForDomain(domain));
            }

            return workflows;
        }

        /// <summary>
        /// Create workflow from domain-specific data
        /// </summary>
        private ProcessingWorkflow CreateWorkflowFromDomainData(dynamic workflowData, string domain)
        {
            var workflowId = $"{domain}-{workflowData?.id ?? Guid.NewGuid().ToString()}";
            var name = workflowData?.name?.ToString() ?? $"{domain.ToUpper()} Workflow";
            var description = workflowData?.description?.ToString() ?? $"Workflow from {domain} domain";

            return new ProcessingWorkflow
            {
                Id = workflowId,
                Name = name,
                Description = description,
                Category = domain,
                Version = "1.0",
                Steps = CreateStepsFromDomainData(workflowData?.steps, domain),
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Create default workflows for a domain when discovery fails
        /// </summary>
        private List<ProcessingWorkflow> CreateDefaultWorkflowsForDomain(string domain)
        {
            var workflows = new List<ProcessingWorkflow>();

            switch (domain.ToLowerInvariant())
            {
                case "device":
                    workflows.Add(new ProcessingWorkflow
                    {
                        Id = $"device-optimization-{Guid.NewGuid():N}",
                        Name = "Device Optimization",
                        Description = "Optimize device performance and configuration",
                        Category = domain,
                        Version = "1.0",
                        Steps = new List<WorkflowStep>
                        {
                            new() { Order = 1, Operation = "device_discovery", Description = "Discover available devices" },
                            new() { Order = 2, Operation = "device_optimization", Description = "Optimize device settings" }
                        },
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    break;

                case "model":
                    workflows.Add(new ProcessingWorkflow
                    {
                        Id = $"model-loading-{Guid.NewGuid():N}",
                        Name = "Model Loading",
                        Description = "Load and validate models for inference",
                        Category = domain,
                        Version = "1.0",
                        Steps = new List<WorkflowStep>
                        {
                            new() { Order = 1, Operation = "model_loading", Description = "Load model into VRAM" },
                            new() { Order = 2, Operation = "model_validation", Description = "Validate loaded model" }
                        },
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    break;

                case "inference":
                    workflows.Add(new ProcessingWorkflow
                    {
                        Id = $"inference-execution-{Guid.NewGuid():N}",
                        Name = "Inference Execution",
                        Description = "Execute model inference operations",
                        Category = domain,
                        Version = "1.0",
                        Steps = new List<WorkflowStep>
                        {
                            new() { Order = 1, Operation = "inference_preparation", Description = "Prepare inference parameters" },
                            new() { Order = 2, Operation = "inference_execution", Description = "Execute model inference" }
                        },
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    break;

                case "postprocessing":
                    workflows.Add(new ProcessingWorkflow
                    {
                        Id = $"postprocessing-enhancement-{Guid.NewGuid():N}",
                        Name = "Image Enhancement",
                        Description = "Enhance and postprocess generated images",
                        Category = domain,
                        Version = "1.0",
                        Steps = new List<WorkflowStep>
                        {
                            new() { Order = 1, Operation = "postprocessing_enhancement", Description = "Enhance image quality" },
                            new() { Order = 2, Operation = "postprocessing_validation", Description = "Validate processed results" }
                        },
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    break;
            }

            return workflows;
        }

        /// <summary>
        /// Create workflow steps from domain data
        /// </summary>
        private List<WorkflowStep> CreateStepsFromDomainData(dynamic stepsData, string domain)
        {
            var steps = new List<WorkflowStep>();

            if (stepsData != null)
            {
                try
                {
                    foreach (var stepData in stepsData)
                    {
                        if (stepData != null)
                        {
                            steps.Add(new WorkflowStep
                            {
                                Operation = stepData?.type?.ToString() ?? $"{domain}_operation",
                                Order = (int)(stepData?.order ?? steps.Count + 1),
                                Description = stepData?.description?.ToString() ?? $"{domain} operation step"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing steps from domain data: {Domain}", domain);
                }
            }

            // Ensure at least one step exists
            if (steps.Count == 0)
            {
                steps.Add(new WorkflowStep
                {
                    Operation = $"{domain}_operation",
                    Order = 1,
                    Description = $"Default {domain} operation"
                });
            }

            return steps;
        }

        #endregion

        #region Week 13: Domain Routing Infrastructure

        /// <summary>
        /// Execute a workflow step by routing to the appropriate Python instructor
        /// Week 13: Core domain routing implementation
        /// </summary>
        private async Task<StepExecutionResult> ExecuteWorkflowStep(ProcessingSession session, WorkflowStep step, Dictionary<string, object> parameters)
        {
            try
            {
                _logger.LogInformation("Executing workflow step: {StepName} ({StepType})", step.Name, step.Type);
                
                // Route step to appropriate Python instructor based on step type
                return step.Type.ToLowerInvariant() switch
                {
                    "device_discovery" or "device_optimization" or "device_status" => await ExecuteDeviceStep(session, step, parameters),
                    "model_loading" or "model_validation" or "model_optimization" => await ExecuteModelStep(session, step, parameters),
                    "inference_generation" or "inference_batch" or "inference_optimization" => await ExecuteInferenceStep(session, step, parameters),
                    "postprocessing_enhancement" or "postprocessing_upscale" or "postprocessing_batch" => await ExecutePostprocessingStep(session, step, parameters),
                    "batch_processing" => await ExecuteBatchStep(session, step, parameters),
                    _ => throw new InvalidOperationException($"Unknown workflow step type: {step.Type}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow step: {StepName}", step.Name);
                return new StepExecutionResult
                {
                    IsSuccess = false,
                    Error = $"Step '{step.Name}' error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Execute device-related workflow step
        /// </summary>
        private async Task<StepExecutionResult> ExecuteDeviceStep(ProcessingSession session, WorkflowStep step, Dictionary<string, object> parameters)
        {
            var deviceRequest = new
            {
                request_id = Guid.NewGuid().ToString(),
                session_id = session.Id,
                step_id = step.Id,
                action = step.Type.Replace("device_", ""),
                data = new
                {
                    device_id = parameters.GetValueOrDefault("device_id", ""),
                    optimization_target = parameters.GetValueOrDefault("optimization_target", "performance"),
                    step_parameters = step.Parameters
                }
            };

            var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                PythonWorkerTypes.DEVICE, step.Type.Replace("device_", ""), deviceRequest);

            if (pythonResponse?.success == true)
            {
                return new StepExecutionResult
                {
                    IsSuccess = true,
                    StepData = pythonResponse.data,
                    ResourceUsage = ParseResourceUsage(pythonResponse.data?.resource_usage)
                };
            }
            else
            {
                var error = pythonResponse?.error ?? "Unknown error during device step";
                return new StepExecutionResult
                {
                    IsSuccess = false,
                    Error = error
                };
            }
        }

        /// <summary>
        /// Execute model-related workflow step
        /// </summary>
        private async Task<StepExecutionResult> ExecuteModelStep(ProcessingSession session, WorkflowStep step, Dictionary<string, object> parameters)
        {
            var modelRequest = new
            {
                request_id = Guid.NewGuid().ToString(),
                session_id = session.Id,
                step_id = step.Id,
                action = step.Type.Replace("model_", ""),
                data = new
                {
                    model_id = step.RequiredModels.FirstOrDefault() ?? parameters.GetValueOrDefault("model_id", ""),
                    optimization_target = parameters.GetValueOrDefault("optimization_target", "performance"),
                    step_parameters = step.Parameters
                }
            };

            var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                PythonWorkerTypes.MODEL, step.Type.Replace("model_", ""), modelRequest);

            if (pythonResponse?.success == true)
            {
                return new StepExecutionResult
                {
                    IsSuccess = true,
                    StepData = pythonResponse.data,
                    ResourceUsage = ParseResourceUsage(pythonResponse.data?.resource_usage)
                };
            }
            else
            {
                var error = pythonResponse?.error ?? "Unknown error during model step";
                return new StepExecutionResult
                {
                    IsSuccess = false,
                    Error = error
                };
            }
        }

        /// <summary>
        /// Execute inference-related workflow step
        /// </summary>
        private async Task<StepExecutionResult> ExecuteInferenceStep(ProcessingSession session, WorkflowStep step, Dictionary<string, object> parameters)
        {
            var inferenceRequest = new
            {
                request_id = Guid.NewGuid().ToString(),
                session_id = session.Id,
                step_id = step.Id,
                action = step.Type.Replace("inference_", ""),
                data = new
                {
                    model_id = step.RequiredModels.FirstOrDefault(),
                    prompt = parameters.GetValueOrDefault("prompt", ""),
                    negative_prompt = parameters.GetValueOrDefault("negative_prompt", ""),
                    width = Convert.ToInt32(parameters.GetValueOrDefault("width", 512)),
                    height = Convert.ToInt32(parameters.GetValueOrDefault("height", 512)),
                    steps = Convert.ToInt32(parameters.GetValueOrDefault("steps", 20)),
                    guidance_scale = Convert.ToDouble(parameters.GetValueOrDefault("guidance_scale", 7.5)),
                    seed = parameters.ContainsKey("seed") ? Convert.ToInt64(parameters["seed"]) : -1,
                    step_parameters = step.Parameters
                }
            };

            var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                PythonWorkerTypes.INFERENCE, step.Type.Replace("inference_", ""), inferenceRequest);

            if (pythonResponse?.success == true)
            {
                return new StepExecutionResult
                {
                    IsSuccess = true,
                    StepData = pythonResponse.data,
                    ResourceUsage = ParseResourceUsage(pythonResponse.data?.resource_usage)
                };
            }
            else
            {
                var error = pythonResponse?.error ?? "Unknown error during inference step";
                return new StepExecutionResult
                {
                    IsSuccess = false,
                    Error = error
                };
            }
        }

        /// <summary>
        /// Execute postprocessing-related workflow step
        /// </summary>
        private async Task<StepExecutionResult> ExecutePostprocessingStep(ProcessingSession session, WorkflowStep step, Dictionary<string, object> parameters)
        {
            var postprocessingRequest = new
            {
                request_id = Guid.NewGuid().ToString(),
                session_id = session.Id,
                step_id = step.Id,
                action = step.Type.Replace("postprocessing_", ""),
                data = new
                {
                    input_images = parameters.GetValueOrDefault("input_images", new string[0]),
                    enhancement_type = parameters.GetValueOrDefault("enhancement_type", "upscale"),
                    scale_factor = Convert.ToDouble(parameters.GetValueOrDefault("scale_factor", 2.0)),
                    step_parameters = step.Parameters
                }
            };

            var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                PythonWorkerTypes.POSTPROCESSING, step.Type.Replace("postprocessing_", ""), postprocessingRequest);

            if (pythonResponse?.success == true)
            {
                return new StepExecutionResult
                {
                    IsSuccess = true,
                    StepData = pythonResponse.data,
                    ResourceUsage = ParseResourceUsage(pythonResponse.data?.resource_usage)
                };
            }
            else
            {
                var error = pythonResponse?.error ?? "Unknown error during postprocessing step";
                return new StepExecutionResult
                {
                    IsSuccess = false,
                    Error = error
                };
            }
        }

        /// <summary>
        /// Execute batch processing step
        /// </summary>
        private async Task<StepExecutionResult> ExecuteBatchStep(ProcessingSession session, WorkflowStep step, Dictionary<string, object> parameters)
        {
            var batchRequest = new
            {
                request_id = Guid.NewGuid().ToString(),
                session_id = session.Id,
                step_id = step.Id,
                action = "batch_process",
                data = new
                {
                    batch_size = Convert.ToInt32(parameters.GetValueOrDefault("batch_size", 4)),
                    items = parameters.GetValueOrDefault("items", new object[0]),
                    parallel_processing = Convert.ToBoolean(parameters.GetValueOrDefault("parallel_processing", false)),
                    step_parameters = step.Parameters
                }
            };

            var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                PythonWorkerTypes.INFERENCE, "batch_process", batchRequest);

            if (pythonResponse?.success == true)
            {
                return new StepExecutionResult
                {
                    IsSuccess = true,
                    StepData = pythonResponse.data,
                    ResourceUsage = ParseResourceUsage(pythonResponse.data?.resource_usage)
                };
            }
            else
            {
                var error = pythonResponse?.error ?? "Unknown error during batch step";
                return new StepExecutionResult
                {
                    IsSuccess = false,
                    Error = error
                };
            }
        }

        /// <summary>
        /// Parse resource usage from Python response
        /// </summary>
        private ResourceUsage? ParseResourceUsage(dynamic? resourceData)
        {
            if (resourceData == null) return null;

            try
            {
                return new ResourceUsage
                {
                    MemoryUsage = resourceData.memory_mb ?? 0,
                    VramUsage = resourceData.vram_mb ?? 0,
                    CpuUsage = resourceData.cpu_usage ?? 0.0,
                    GpuUsage = resourceData.gpu_usage ?? 0.0,
                    ProcessingTime = TimeSpan.FromSeconds(resourceData.processing_time_seconds ?? 0.0)
                };
            }
            catch
            {
                return new ResourceUsage();
            }
        }

        /// <summary>
        /// Get workflow definition from cache or create default
        /// </summary>
        private async Task<WorkflowDefinition?> GetWorkflowDefinition(string workflowId)
        {
            if (_workflowDefinitions.TryGetValue(workflowId, out var cached))
            {
                return cached;
            }

            // Create default workflow definitions for common patterns
            var definition = workflowId.ToLowerInvariant() switch
            {
                "basic-image-generation" => CreateBasicImageGenerationWorkflow(),
                "model-loading-workflow" => CreateModelLoadingWorkflow(),
                "batch-processing-workflow" => CreateBatchProcessingWorkflow(),
                _ => await TryGetWorkflowFromPython(workflowId)
            };

            if (definition != null)
            {
                _workflowDefinitions.TryAdd(workflowId, definition);
            }

            return definition;
        }

        /// <summary>
        /// Create basic image generation workflow definition
        /// </summary>
        private WorkflowDefinition CreateBasicImageGenerationWorkflow()
        {
            return new WorkflowDefinition
            {
                WorkflowId = "basic-image-generation",
                Name = "Basic Image Generation",
                Description = "Standard text-to-image generation workflow",
                EstimatedDuration = TimeSpan.FromMinutes(2),
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Id = "step-1",
                        Name = "Model Loading",
                        Type = "model_loading",
                        Operation = "load_model",
                        Order = 1
                    },
                    new WorkflowStep
                    {
                        Id = "step-2", 
                        Name = "Image Generation",
                        Type = "inference_generation",
                        Operation = "generate_image",
                        Order = 2
                    }
                },
                ResourceRequirements = new Dictionary<string, object>
                {
                    ["memory_gb"] = 4,
                    ["vram_gb"] = 6,
                    ["estimated_time_minutes"] = 2
                }
            };
        }

        /// <summary>
        /// Create model loading workflow definition
        /// </summary>
        private WorkflowDefinition CreateModelLoadingWorkflow()
        {
            return new WorkflowDefinition
            {
                WorkflowId = "model-loading-workflow",
                Name = "Model Loading",
                Description = "Load and optimize model for inference",
                EstimatedDuration = TimeSpan.FromMinutes(1),
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Id = "step-1",
                        Name = "Model Validation",
                        Type = "model_validation",
                        Operation = "validate_model",
                        Order = 1
                    },
                    new WorkflowStep
                    {
                        Id = "step-2",
                        Name = "Model Loading",
                        Type = "model_loading",
                        Operation = "load_model", 
                        Order = 2
                    }
                }
            };
        }

        /// <summary>
        /// Create batch processing workflow definition
        /// </summary>
        private WorkflowDefinition CreateBatchProcessingWorkflow()
        {
            return new WorkflowDefinition
            {
                WorkflowId = "batch-processing-workflow",
                Name = "Batch Processing",
                Description = "Process multiple items in batches",
                EstimatedDuration = TimeSpan.FromMinutes(10),
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Id = "step-1",
                        Name = "Batch Preparation",
                        Type = "batch_processing",
                        Operation = "prepare_batch",
                        Order = 1
                    }
                }
            };
        }

        /// <summary>
        /// Try to get workflow definition from Python workers
        /// </summary>
        private async Task<WorkflowDefinition?> TryGetWorkflowFromPython(string workflowId)
        {
            try
            {
                // Try getting from inference worker which might have workflow templates
                var request = new
                {
                    request_id = Guid.NewGuid().ToString(),
                    workflow_id = workflowId,
                    action = "get_workflow_definition"
                };

                var response = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.INFERENCE, "get_workflow_definition", request);

                if (response?.success == true && response.data?.workflow != null)
                {
                    return ParseWorkflowDefinition(response.data.workflow!);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get workflow definition from Python: {WorkflowId}", workflowId);
            }

            return null;
        }

        /// <summary>
        /// Parse workflow definition from Python response
        /// </summary>
        private WorkflowDefinition ParseWorkflowDefinition(dynamic workflowData)
        {
            var definition = new WorkflowDefinition
            {
                WorkflowId = workflowData.workflow_id ?? "",
                Name = workflowData.name ?? "",
                Description = workflowData.description ?? "",
                EstimatedDuration = TimeSpan.FromSeconds(workflowData.estimated_duration_seconds ?? 60)
            };

            if (workflowData.steps != null)
            {
                foreach (var step in workflowData.steps)
                {
                    definition.Steps.Add(new WorkflowStep
                    {
                        Id = step.step_id ?? "",
                        Name = step.name ?? "",
                        Type = step.type ?? "",
                        Operation = step.operation ?? step.type ?? "",
                        Description = step.description ?? "",
                        Order = step.step_number ?? 1,
                        StepNumber = step.step_number ?? 1
                    });
                }
            }

            return definition;
        }

        /// <summary>
        /// Validate workflow requirements against available resources
        /// </summary>
        private Task<(bool IsValid, string Error)> ValidateWorkflowRequirements(WorkflowDefinition workflow, Dictionary<string, object> parameters)
        {
            try
            {
                _logger.LogInformation("Validating workflow requirements: {WorkflowId}", workflow.WorkflowId);

                // Basic parameter validation
                if (parameters == null)
                {
                    return Task.FromResult((false, "Parameters cannot be null"));
                }

                // Validate required models are available
                foreach (var requiredModel in workflow.RequiredModels)
                {
                    if (!string.IsNullOrEmpty(requiredModel))
                    {
                        // Could add model availability check here
                        _logger.LogDebug("Validating model availability: {ModelId}", requiredModel);
                    }
                }

                // Validate resource requirements
                if (workflow.ResourceRequirements.ContainsKey("memory_gb"))
                {
                    var requiredMemory = Convert.ToDouble(workflow.ResourceRequirements["memory_gb"]);
                    // Could add memory availability check here
                    _logger.LogDebug("Required memory: {MemoryGB} GB", requiredMemory);
                }

                return Task.FromResult((true, ""));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating workflow requirements: {WorkflowId}", workflow.WorkflowId);
                return Task.FromResult((false, $"Validation error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Execute workflow steps sequentially or in parallel based on dependencies
        /// </summary>
        private async Task<WorkflowExecutionResult> ExecuteWorkflowSteps(ProcessingSession session, WorkflowDefinition workflow, Dictionary<string, object> parameters)
        {
            var resources = new ResourceUsage();
            
            try
            {
                _logger.LogInformation("Executing {StepCount} workflow steps for session: {SessionId}", 
                    workflow.Steps.Count, session.Id);

                foreach (var step in workflow.Steps.OrderBy(s => s.StepNumber))
                {
                    try
                    {
                        _logger.LogInformation("Executing workflow step: {StepName} ({StepType})", step.Name, step.Type);
                        
                        // Update session progress
                        session.CurrentStep = step.StepNumber;
                        session.LastUpdated = DateTime.UtcNow;
                        
                        // Execute step
                        var stepResult = await ExecuteWorkflowStep(session, step, parameters);
                        
                        if (!stepResult.IsSuccess)
                        {
                            return new WorkflowExecutionResult 
                            { 
                                IsSuccess = false, 
                                Error = $"Step '{step.Name}' failed: {stepResult.Error}" 
                            };
                        }

                        // Update progress
                        session.Progress = (int)((double)step.StepNumber / workflow.Steps.Count * 100);
                        session.LastUpdated = DateTime.UtcNow;
                        
                        // Aggregate resource usage
                        if (stepResult.ResourceUsage != null)
                        {
                            resources = AggregateResourceUsage(resources, stepResult.ResourceUsage);
                        }
                        
                        _logger.LogInformation("Completed workflow step: {StepName}", step.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing workflow step: {StepName}", step.Name);
                        return new WorkflowExecutionResult 
                        { 
                            IsSuccess = false, 
                            Error = $"Step '{step.Name}' error: {ex.Message}" 
                        };
                    }
                }

                session.Status = ProcessingStatus.Completed;
                session.CompletedAt = DateTime.UtcNow;
                session.Progress = 100;

                return new WorkflowExecutionResult 
                { 
                    IsSuccess = true, 
                    ResourceUsage = resources 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow steps for session: {SessionId}", session.Id);
                return new WorkflowExecutionResult 
                { 
                    IsSuccess = false, 
                    Error = $"Workflow execution error: {ex.Message}" 
                };
            }
        }

        #endregion

        #region Week 13: Session Management Helper Methods

        /// <summary>
        /// Update individual session status
        /// </summary>
        private async Task UpdateSessionStatusAsync(ProcessingSession session)
        {
            await UpdateSessionStatusWithDomainAggregationAsync(session);
        }

        /// <summary>
        /// Update individual batch status
        /// </summary>
        private async Task UpdateBatchStatusAsync(ProcessingBatch batch)
        {
            await UpdateBatchStatusWithCoordinationAsync(batch);
        }

        /// <summary>
        /// Get involved domains for a session
        /// </summary>
        private List<string> GetSessionInvolvedDomains(ProcessingSession session)
        {
            var domains = new List<string>();
            
            if (!string.IsNullOrEmpty(session.WorkflowId))
            {
                // Analyze workflow to determine involved domains
                if (_workflowCache.TryGetValue(session.WorkflowId, out var workflow))
                {
                    foreach (var step in workflow.Steps)
                    {
                        if (StepTypeToDomainMapping.TryGetValue(step.Operation, out var domain))
                        {
                            if (!domains.Contains(domain))
                                domains.Add(domain);
                        }
                    }
                }
            }

            // Always include inference as primary domain if none found
            if (domains.Count == 0)
                domains.Add(PythonWorkerTypes.INFERENCE);

            return domains;
        }

        /// <summary>
        /// Get involved domains for a batch
        /// </summary>
        private List<string> GetBatchInvolvedDomains(ProcessingBatch batch)
        {
            var domains = new List<string> { PythonWorkerTypes.INFERENCE }; // Primary domain for batches
            
            if (!string.IsNullOrEmpty(batch.WorkflowId) && _workflowCache.TryGetValue(batch.WorkflowId, out var workflow))
            {
                foreach (var step in workflow.Steps)
                {
                    if (StepTypeToDomainMapping.TryGetValue(step.Operation, out var domain))
                    {
                        if (!domains.Contains(domain))
                            domains.Add(domain);
                    }
                }
            }

            return domains;
        }

        /// <summary>
        /// Aggregate session status from all domains
        /// </summary>
        private async Task<List<DomainSessionStatus>> AggregateSessionStatusFromDomains(string sessionId, List<string> involvedDomains)
        {
            var domainStatuses = new List<DomainSessionStatus>();

            foreach (var domain in involvedDomains)
            {
                try
                {
                    var statusRequest = new
                    {
                        request_id = Guid.NewGuid().ToString(),
                        session_id = sessionId,
                        action = "get_session_status"
                    };

                    var response = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                        domain, "get_session_status", statusRequest);

                    if (response?.success == true)
                    {
                        domainStatuses.Add(new DomainSessionStatus
                        {
                            Domain = domain,
                            Status = ParseProcessingStatus(response.data?.status?.ToString()),
                            Progress = Convert.ToDouble(response.data?.progress ?? 0.0),
                            CurrentOperation = response.data?.current_operation?.ToString(),
                            LastUpdated = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        domainStatuses.Add(new DomainSessionStatus
                        {
                            Domain = domain,
                            Status = ProcessingStatus.Unknown,
                            Progress = 0,
                            ErrorMessage = response?.error?.ToString(),
                            LastUpdated = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get status from domain: {Domain}", domain);
                    domainStatuses.Add(new DomainSessionStatus
                    {
                        Domain = domain,
                        Status = ProcessingStatus.Unknown,
                        Progress = 0,
                        ErrorMessage = ex.Message,
                        LastUpdated = DateTime.UtcNow
                    });
                }
            }

            return domainStatuses;
        }

        /// <summary>
        /// Determine overall session status from domain statuses
        /// </summary>
        private SessionStatusResult DetermineOverallSessionStatus(List<DomainSessionStatus> domainStatuses)
        {
            if (!domainStatuses.Any())
                return new SessionStatusResult { Status = ProcessingStatus.Unknown };

            // If any domain has failed, overall status is failed
            if (domainStatuses.Any(d => d.Status == ProcessingStatus.Failed))
                return new SessionStatusResult { Status = ProcessingStatus.Failed };

            // If all domains are completed, overall status is completed
            if (domainStatuses.All(d => d.Status == ProcessingStatus.Completed))
                return new SessionStatusResult { Status = ProcessingStatus.Completed };

            // If any domain is running, overall status is running
            if (domainStatuses.Any(d => d.Status == ProcessingStatus.Running))
                return new SessionStatusResult { Status = ProcessingStatus.Running };

            // If any domain is pending, overall status is pending
            if (domainStatuses.Any(d => d.Status == ProcessingStatus.Pending))
                return new SessionStatusResult { Status = ProcessingStatus.Pending };

            // Default to the most common status
            var mostCommonStatus = domainStatuses
                .GroupBy(d => d.Status)
                .OrderByDescending(g => g.Count())
                .First().Key;

            return new SessionStatusResult { Status = mostCommonStatus };
        }

        /// <summary>
        /// Calculate overall progress from domain statuses
        /// </summary>
        private double CalculateOverallProgress(List<DomainSessionStatus> domainStatuses)
        {
            if (!domainStatuses.Any())
                return 0.0;

            return domainStatuses.Average(d => d.Progress);
        }

        /// <summary>
        /// Determine new status based on action
        /// </summary>
        private ProcessingStatus DetermineNewStatus(ProcessingStatus currentStatus, string action)
        {
            return action.ToLowerInvariant() switch
            {
                "start" => ProcessingStatus.Running,
                "pause" => ProcessingStatus.Paused,
                "resume" => ProcessingStatus.Running,
                "stop" => ProcessingStatus.Cancelled,
                "cancel" => ProcessingStatus.Cancelled,
                _ => currentStatus
            };
        }

        /// <summary>
        /// Send control commands to all involved domains - Week 13 Implementation
        /// </summary>
        private async Task<List<DomainControlResult>> SendControlToAllDomains(string sessionId, List<string> domains, string action, Dictionary<string, object> parameters)
        {
            var results = new List<DomainControlResult>();
            
            foreach (var domain in domains)
            {
                try
                {
                    var controlRequest = new
                    {
                        request_id = Guid.NewGuid().ToString(),
                        session_id = sessionId,
                        action = action,
                        data = parameters
                    };

                    var workerType = domain.ToLowerInvariant() switch
                    {
                        "device" => PythonWorkerTypes.DEVICE,
                        "model" => PythonWorkerTypes.MODEL,
                        "inference" => PythonWorkerTypes.INFERENCE,
                        "postprocessing" => PythonWorkerTypes.POSTPROCESSING,
                        _ => throw new InvalidOperationException($"Unknown domain: {domain}")
                    };

                    var response = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                        workerType, "session_control", controlRequest);

                    results.Add(new DomainControlResult
                    {
                        Domain = domain,
                        IsSuccess = response?.success == true,
                        Error = response?.success == true ? null : response?.error?.ToString() ?? "Unknown error"
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new DomainControlResult
                    {
                        Domain = domain,
                        IsSuccess = false,
                        Error = ex.Message
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Send cleanup commands to all involved domains - Week 13 Implementation
        /// </summary>
        private async Task<List<DomainCleanupResult>> SendCleanupToAllDomains(string sessionId, List<string> domains, bool wasRunning)
        {
            var results = new List<DomainCleanupResult>();
            
            foreach (var domain in domains)
            {
                try
                {
                    var cleanupRequest = new
                    {
                        request_id = Guid.NewGuid().ToString(),
                        session_id = sessionId,
                        action = "cleanup_session",
                        data = new { was_running = wasRunning }
                    };

                    var workerType = domain.ToLowerInvariant() switch
                    {
                        "device" => PythonWorkerTypes.DEVICE,
                        "model" => PythonWorkerTypes.MODEL,
                        "inference" => PythonWorkerTypes.INFERENCE,
                        "postprocessing" => PythonWorkerTypes.POSTPROCESSING,
                        _ => throw new InvalidOperationException($"Unknown domain: {domain}")
                    };

                    var response = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                        workerType, "cleanup_session", cleanupRequest);

                    results.Add(new DomainCleanupResult
                    {
                        Domain = domain,
                        IsSuccess = response?.success == true,
                        Error = response?.success == true ? null : response?.error?.ToString() ?? "Unknown error"
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new DomainCleanupResult
                    {
                        Domain = domain,
                        IsSuccess = false,
                        Error = ex.Message
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Send batch cleanup commands to all involved domains - Week 13 Implementation
        /// </summary>
        private async Task<List<BatchCleanupResult>> SendBatchCleanupToAllDomains(string batchId, List<string> domains, bool wasRunning)
        {
            var results = new List<BatchCleanupResult>();
            
            foreach (var domain in domains)
            {
                try
                {
                    var cleanupRequest = new
                    {
                        request_id = Guid.NewGuid().ToString(),
                        batch_id = batchId,
                        action = "cleanup_batch",
                        data = new { was_running = wasRunning }
                    };

                    var workerType = domain.ToLowerInvariant() switch
                    {
                        "device" => PythonWorkerTypes.DEVICE,
                        "model" => PythonWorkerTypes.MODEL,
                        "inference" => PythonWorkerTypes.INFERENCE,
                        "postprocessing" => PythonWorkerTypes.POSTPROCESSING,
                        _ => throw new InvalidOperationException($"Unknown domain: {domain}")
                    };

                    var response = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                        workerType, "cleanup_batch", cleanupRequest);

                    results.Add(new BatchCleanupResult
                    {
                        Domain = domain,
                        IsSuccess = response?.success == true,
                        Error = response?.success == true ? null : response?.error?.ToString() ?? "Unknown error"
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new BatchCleanupResult
                    {
                        Domain = domain,
                        IsSuccess = false,
                        Error = ex.Message
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Aggregate resource usage from multiple steps - Week 13 Implementation
        /// </summary>
        private ResourceUsage AggregateResourceUsage(ResourceUsage existing, ResourceUsage? additional)
        {
            if (additional == null) return existing;

            return new ResourceUsage
            {
                MemoryUsed = $"{existing.MemoryUsage + additional.MemoryUsage}MB",
                GpuMemoryUsed = $"{existing.VramUsage + additional.VramUsage}MB",
                CpuUsage = Math.Max(existing.CpuUsage, additional.CpuUsage),
                GpuUsage = Math.Max(existing.GpuUsage, additional.GpuUsage),
                Duration = existing.Duration + additional.Duration,
                MemoryUsage = existing.MemoryUsage + additional.MemoryUsage,
                VramUsage = existing.VramUsage + additional.VramUsage,
                ProcessingTime = existing.ProcessingTime + additional.ProcessingTime
            };
        }

        public class DomainCleanupResult
        {
            public string Domain { get; set; } = string.Empty;
            public bool IsSuccess { get; set; }
            public string? Error { get; set; }
        }

        #endregion



        /// <summary>
        /// Helper methods for parsing Python responses
        /// </summary>
        private ProcessingStatus ParseBatchStatus(string? statusString)
        {
            return statusString?.ToLowerInvariant() switch
            {
                "created" => ProcessingStatus.Created,
                "pending" => ProcessingStatus.Pending,
                "running" => ProcessingStatus.Running,
                "completed" => ProcessingStatus.Completed,
                "failed" => ProcessingStatus.Failed,
                "cancelled" => ProcessingStatus.Cancelled,
                "paused" => ProcessingStatus.Paused,
                _ => ProcessingStatus.Created
            };
        }

        private int ParseBatchProcessedItems(dynamic? processedData)
        {
            if (processedData == null) return 0;
            return Convert.ToInt32(processedData);
        }

        private int ParseBatchFailedItems(dynamic? failedData)
        {
            if (failedData == null) return 0;
            return Convert.ToInt32(failedData);
        }

        private ProcessingStatus ParseProcessingStatus(string? statusString)
        {
            return ParseBatchStatus(statusString);
        }

        private bool IsValidControlAction(ProcessingStatus currentStatus, string action)
        {
            return action.ToLowerInvariant() switch
            {
                "start" => currentStatus == ProcessingStatus.Created || currentStatus == ProcessingStatus.Paused,
                "pause" => currentStatus == ProcessingStatus.Running,
                "resume" => currentStatus == ProcessingStatus.Paused,
                "stop" => currentStatus == ProcessingStatus.Running || currentStatus == ProcessingStatus.Paused,
                "cancel" => currentStatus != ProcessingStatus.Completed && currentStatus != ProcessingStatus.Failed,
                _ => false
            };
        }

        private int CalculateOptimalBatchSize(ProcessingBatch batch)
        {
            // Simple calculation based on total items and priority
            var baseSize = Math.Min(batch.TotalItems, 10);
            return batch.Priority > 5 ? Math.Min(baseSize * 2, 20) : baseSize;
        }

        private int GetMaxBatchSize()
        {
            return 50; // Maximum batch size for processing
        }

        private Dictionary<string, object> CreateBatchGenerationParams(ProcessingBatch batch, PostBatchExecuteRequest? request)
        {
            return new Dictionary<string, object>
            {
                ["batch_id"] = batch.Id,
                ["total_items"] = batch.TotalItems,
                ["priority"] = batch.Priority,
                ["workflow_id"] = batch.WorkflowId,
                ["configuration"] = batch.Configuration,
                ["request_params"] = request?.Parameters ?? new Dictionary<string, object>()
            };
        }

        private async Task MonitorBatchProgress(string batchId, string? trackingId)
        {
            try
            {
                // Background monitoring logic
                await Task.Delay(1000); // Placeholder for actual monitoring
                _logger.LogInformation("Monitoring batch progress: {BatchId}, TrackingId: {TrackingId}", batchId, trackingId ?? "unknown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring batch progress: {BatchId}", batchId);
            }
        }
    }
}



