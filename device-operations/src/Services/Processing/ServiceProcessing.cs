using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Python;
using System.Text.Json;

namespace DeviceOperations.Services.Processing
{
    /// <summary>
    /// Service implementation for processing operations and workflow management
    /// </summary>
    public class ServiceProcessing : IServiceProcessing
    {
        private readonly ILogger<ServiceProcessing> _logger;
        private readonly IPythonWorkerService _pythonWorkerService;
        private readonly Dictionary<string, ProcessingWorkflow> _workflowCache;
        private readonly Dictionary<string, ProcessingSession> _activeSessions;
        private readonly Dictionary<string, ProcessingBatch> _activeBatches;
        private DateTime _lastWorkflowRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(30);

        public ServiceProcessing(
            ILogger<ServiceProcessing> logger,
            IPythonWorkerService pythonWorkerService)
        {
            _logger = logger;
            _pythonWorkerService = pythonWorkerService;
            _workflowCache = new Dictionary<string, ProcessingWorkflow>();
            _activeSessions = new Dictionary<string, ProcessingSession>();
            _activeBatches = new Dictionary<string, ProcessingBatch>();
        }

        public async Task<ApiResponse<GetProcessingWorkflowsResponse>> GetProcessingWorkflowsAsync()
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

                var response = new GetProcessingWorkflowsResponse
                {
                    Workflows = allWorkflows.Select(w => new WorkflowInfo
                    {
                        Id = w.Id,
                        Name = w.Name,
                        Description = w.Description,
                        Version = w.Version,
                        EstimatedDuration = w.EstimatedDuration,
                        Parameters = new List<WorkflowParameter>(),
                        ResourceRequirements = new Dictionary<string, object>
                        {
                            ["memory"] = "4GB",
                            ["gpu_memory"] = "6GB"
                        }
                    }).ToList(),
                    TotalCount = allWorkflows.Count
                };

                _logger.LogInformation($"Retrieved {allWorkflows.Count} workflows across {categories.Count} categories");
                return ApiResponse<GetProcessingWorkflowsResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get processing workflows");
                return ApiResponse<GetProcessingWorkflowsResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get processing workflows: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetProcessingWorkflowResponse>> GetProcessingWorkflowAsync(string idWorkflow)
        {
            try
            {
                _logger.LogInformation($"Getting processing workflow: {idWorkflow}");

                if (string.IsNullOrWhiteSpace(idWorkflow))
                    return ApiResponse<GetProcessingWorkflowResponse>.CreateError(
                        new ErrorDetails { Message = "Workflow ID cannot be null or empty" });

                await RefreshWorkflowsAsync();

                if (!_workflowCache.TryGetValue(idWorkflow, out var workflow))
                {
                    // Try to get from Python worker
                    var pythonRequest = new { workflow_id = idWorkflow, action = "get_workflow" };
                    var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                        PythonWorkerTypes.PROCESSING, "get_workflow", pythonRequest);

                    if (pythonResponse?.success == true)
                    {
                        workflow = CreateWorkflowFromPython(pythonResponse.workflow);
                        _workflowCache[idWorkflow] = workflow;
                    }
                    else
                    {
                        return ApiResponse<GetProcessingWorkflowResponse>.CreateError(
                            new ErrorDetails { Message = $"Workflow '{idWorkflow}' not found" });
                    }
                }

                var response = new GetProcessingWorkflowResponse
                {
                    Workflow = new WorkflowInfo
                    {
                        Id = workflow.Id,
                        Name = workflow.Name,
                        Description = workflow.Description,
                        Version = workflow.Version,
                        EstimatedDuration = workflow.EstimatedDuration,
                        Parameters = new List<WorkflowParameter>(),
                        ResourceRequirements = await CalculateRequiredResourcesAsync(workflow)
                    }
                };

                _logger.LogInformation($"Retrieved workflow details for: {idWorkflow}");
                return ApiResponse<GetProcessingWorkflowResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get processing workflow: {idWorkflow}");
                return ApiResponse<GetProcessingWorkflowResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get workflow: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostWorkflowExecuteResponse>> PostWorkflowExecuteAsync(PostWorkflowExecuteRequest request)
        {
            try
            {
                _logger.LogInformation($"Executing workflow: {request.WorkflowId}");

                if (request == null)
                    return ApiResponse<PostWorkflowExecuteResponse>.CreateError(
                        new ErrorDetails { Message = "Workflow execution request cannot be null" });

                if (!_workflowCache.TryGetValue(request.WorkflowId, out var workflow))
                {
                    await RefreshWorkflowsAsync();
                    if (!_workflowCache.TryGetValue(request.WorkflowId, out workflow))
                    {
                        return ApiResponse<PostWorkflowExecuteResponse>.CreateError(
                            new ErrorDetails { Message = $"Workflow '{request.WorkflowId}' not found" });
                    }
                }

                if (!workflow.IsAvailable)
                {
                    return ApiResponse<PostWorkflowExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Workflow '{request.WorkflowId}' is not currently available" });
                }

                var sessionId = Guid.NewGuid().ToString();
                var pythonRequest = new
                {
                    session_id = sessionId,
                    workflow_id = request.WorkflowId,
                    parameters = request.Parameters,
                    priority = request.Priority,
                    background = request.Background,
                    action = "execute_workflow"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.PROCESSING, "execute_workflow", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var session = new ProcessingSession
                    {
                        Id = sessionId,
                        WorkflowId = request.WorkflowId,
                        Status = ProcessingStatus.Running,
                        StartedAt = DateTime.UtcNow,
                        CurrentStep = 0,
                        TotalSteps = workflow.Steps.Count,
                        Progress = 0
                    };

                    _activeSessions[sessionId] = session;

                    var response = new PostWorkflowExecuteResponse
                    {
                        ExecutionId = sessionId,
                        Status = "Running",
                        EstimatedCompletion = DateTime.UtcNow.AddSeconds(pythonResponse.estimated_time ?? 120),
                        Progress = 0
                    };

                    workflow.UsageCount++;
                    _logger.LogInformation($"Started workflow execution session: {sessionId}");
                    return ApiResponse<PostWorkflowExecuteResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to execute workflow {request.WorkflowId}: {error}");
                    return ApiResponse<PostWorkflowExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to execute workflow: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to execute workflow: {request?.WorkflowId}");
                return ApiResponse<PostWorkflowExecuteResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to execute workflow: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetProcessingSessionsResponse>> GetProcessingSessionsAsync()
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

                var response = new GetProcessingSessionsResponse
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
                return ApiResponse<GetProcessingSessionsResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get processing sessions");
                return ApiResponse<GetProcessingSessionsResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get processing sessions: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetProcessingSessionResponse>> GetProcessingSessionAsync(string idSession)
        {
            try
            {
                _logger.LogInformation($"Getting processing session: {idSession}");

                if (string.IsNullOrWhiteSpace(idSession))
                    return ApiResponse<GetProcessingSessionResponse>.CreateError(
                        new ErrorDetails { Message = "Session ID cannot be null or empty" });

                if (!_activeSessions.TryGetValue(idSession, out var session))
                {
                    return ApiResponse<GetProcessingSessionResponse>.CreateError(
                        new ErrorDetails { Message = $"Session '{idSession}' not found" });
                }

                // Update session status
                await UpdateSessionStatusAsync(session);

                var response = new GetProcessingSessionResponse
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
                            ["detailed_progress"] = await GetDetailedProgressAsync(idSession),
                            ["execution_logs"] = await GetSessionExecutionLogsAsync(idSession),
                            ["output_preview"] = await GetSessionOutputPreviewAsync(idSession)
                        },
                        ResourceUsage = await GetSessionResourceUsageAsync(idSession)
                    }
                };

                _logger.LogInformation($"Retrieved session details for: {idSession}");
                return ApiResponse<GetProcessingSessionResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get processing session: {idSession}");
                return ApiResponse<GetProcessingSessionResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get processing session: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostSessionControlResponse>> PostSessionControlAsync(string idSession, PostSessionControlRequest request)
        {
            try
            {
                _logger.LogInformation($"Controlling processing session: {idSession}, Action: {request.Action}");

                if (string.IsNullOrWhiteSpace(idSession))
                    return ApiResponse<PostSessionControlResponse>.CreateError(
                        new ErrorDetails { Message = "Session ID cannot be null or empty" });

                if (request == null)
                    return ApiResponse<PostSessionControlResponse>.CreateError(
                        new ErrorDetails { Message = "Session control request cannot be null" });

                if (!_activeSessions.TryGetValue(idSession, out var session))
                {
                    return ApiResponse<PostSessionControlResponse>.CreateError(
                        new ErrorDetails { Message = $"Session '{idSession}' not found" });
                }

                var pythonRequest = new
                {
                    session_id = idSession,
                    action = request.Action.ToLowerInvariant(),
                    parameters = request.Parameters
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.PROCESSING, "control_session", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    var previousStatus = session.Status;

                    session.Status = request.Action.ToLowerInvariant() switch
                    {
                        "pause" => ProcessingStatus.Paused,
                        "resume" => ProcessingStatus.Running,
                        "cancel" => ProcessingStatus.Cancelled,
                        "restart" => ProcessingStatus.Running,
                        _ => session.Status
                    };

                    session.LastUpdated = DateTime.UtcNow;

                    var response = new PostSessionControlResponse
                    {
                        Action = request.Action,
                        Result = "Success",
                        Status = session.Status.ToString()
                    };

                    _logger.LogInformation($"Successfully applied {request.Action} to session: {idSession}");
                    return ApiResponse<PostSessionControlResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to control session {idSession}: {error}");
                    return ApiResponse<PostSessionControlResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to control session: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to control processing session: {idSession}");
                return ApiResponse<PostSessionControlResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to control session: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<DeleteProcessingSessionResponse>> DeleteProcessingSessionAsync(string idSession)
        {
            try
            {
                _logger.LogInformation($"Deleting processing session: {idSession}");

                if (string.IsNullOrWhiteSpace(idSession))
                    return ApiResponse<DeleteProcessingSessionResponse>.CreateError(
                        new ErrorDetails { Message = "Session ID cannot be null or empty" });

                if (!_activeSessions.TryGetValue(idSession, out var session))
                {
                    return ApiResponse<DeleteProcessingSessionResponse>.CreateError(
                        new ErrorDetails { Message = $"Session '{idSession}' not found" });
                }

                var wasRunning = session.Status == ProcessingStatus.Running;
                var pythonRequest = new
                {
                    session_id = idSession,
                    force_cleanup = true,
                    action = "delete_session"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.PROCESSING, "delete_session", pythonRequest);

                // Update session status
                session.Status = ProcessingStatus.Cancelled;
                session.LastUpdated = DateTime.UtcNow;
                session.CompletedAt = DateTime.UtcNow;

                var response = new DeleteProcessingSessionResponse
                {
                    Success = true,
                    Message = $"Session {idSession} deleted successfully"
                };

                // Remove from active sessions
                _activeSessions.Remove(idSession);

                _logger.LogInformation($"Successfully deleted session: {idSession}");
                return ApiResponse<DeleteProcessingSessionResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete processing session: {idSession}");
                return ApiResponse<DeleteProcessingSessionResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to delete processing session: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetProcessingBatchesResponse>> GetProcessingBatchesAsync()
        {
            try
            {
                _logger.LogInformation("Getting all processing batches");

                // Update batch statuses
                await UpdateBatchStatusesAsync();

                var allBatches = _activeBatches.Values.ToList();
                var runningBatches = allBatches.Where(b => b.Status == ProcessingStatus.Running).ToList();
                var completedBatches = allBatches.Where(b => b.Status == ProcessingStatus.Completed).ToList();

                var response = new GetProcessingBatchesResponse
                {
                    Batches = allBatches.Select(b => new BatchJob
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
                        Progress = new BatchProgress
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
                return ApiResponse<GetProcessingBatchesResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get processing batches");
                return ApiResponse<GetProcessingBatchesResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get processing batches: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetProcessingBatchResponse>> GetProcessingBatchAsync(string idBatch)
        {
            try
            {
                _logger.LogInformation($"Getting processing batch: {idBatch}");

                if (string.IsNullOrWhiteSpace(idBatch))
                    return ApiResponse<GetProcessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = "Batch ID cannot be null or empty" });

                if (!_activeBatches.TryGetValue(idBatch, out var batch))
                {
                    return ApiResponse<GetProcessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = $"Batch '{idBatch}' not found" });
                }

                // Update batch status
                await UpdateBatchStatusAsync(batch);

                var response = new GetProcessingBatchResponse
                {
                    Batch = new BatchJob
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
                        Progress = new BatchProgress
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
                return ApiResponse<GetProcessingBatchResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get processing batch: {idBatch}");
                return ApiResponse<GetProcessingBatchResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to get processing batch: {ex.Message}" });
            }
        }

        public Task<ApiResponse<PostBatchCreateResponse>> PostBatchCreateAsync(PostBatchCreateRequest request)
        {
            try
            {
                _logger.LogInformation("Creating new processing batch");

                if (request == null)
                    return Task.FromResult(ApiResponse<PostBatchCreateResponse>.CreateError(
                        new ErrorDetails { Message = "Batch creation request cannot be null" }));

                if (request.Items?.Any() != true)
                    return Task.FromResult(ApiResponse<PostBatchCreateResponse>.CreateError(
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

                var response = new PostBatchCreateResponse
                {
                    BatchId = batchId,
                    Status = ProcessingStatus.Created.ToString(),
                    EstimatedDuration = TimeSpan.FromMinutes(request.Items.Count * 2) // 2 minutes per item estimate
                };

                _logger.LogInformation($"Created batch: {batchId} with {request.Items.Count} items");
                return Task.FromResult(ApiResponse<PostBatchCreateResponse>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create processing batch");
                return Task.FromResult(ApiResponse<PostBatchCreateResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to create batch: {ex.Message}" }));
            }
        }

        public async Task<ApiResponse<PostBatchExecuteResponse>> PostBatchExecuteAsync(string idBatch, PostBatchExecuteRequest request)
        {
            try
            {
                _logger.LogInformation($"Executing processing batch: {idBatch}");

                if (string.IsNullOrWhiteSpace(idBatch))
                    return ApiResponse<PostBatchExecuteResponse>.CreateError(
                        new ErrorDetails { Message = "Batch ID cannot be null or empty" });

                if (!_activeBatches.TryGetValue(idBatch, out var batch))
                {
                    return ApiResponse<PostBatchExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Batch '{idBatch}' not found" });
                }

                if (batch.Status != ProcessingStatus.Created && batch.Status != ProcessingStatus.Paused)
                {
                    return ApiResponse<PostBatchExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Batch '{idBatch}' cannot be executed in current status: {batch.Status}" });
                }

                var pythonRequest = new
                {
                    batch_id = idBatch,
                    workflow_id = batch.WorkflowId,
                    parameters = request?.Parameters,
                    background = request?.Background ?? true,
                    action = "execute_batch"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.PROCESSING, "execute_batch", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    batch.Status = ProcessingStatus.Running;
                    batch.StartedAt = DateTime.UtcNow;
                    batch.LastUpdated = DateTime.UtcNow;

                    var response = new PostBatchExecuteResponse
                    {
                        ExecutionId = idBatch,
                        Status = ProcessingStatus.Running.ToString(),
                        Progress = new BatchProgress
                        {
                            Percentage = 0,
                            ItemsProcessed = 0,
                            TotalItems = batch.TotalItems,
                            ProcessingRate = 2.0 // Mock rate
                        }
                    };

                    _logger.LogInformation($"Started batch execution: {idBatch}");
                    return ApiResponse<PostBatchExecuteResponse>.CreateSuccess(response);
                }
                else
                {
                    var error = pythonResponse?.error ?? "Unknown error occurred";
                    _logger.LogError($"Failed to execute batch {idBatch}: {error}");
                    return ApiResponse<PostBatchExecuteResponse>.CreateError(
                        new ErrorDetails { Message = $"Failed to execute batch: {error}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to execute processing batch: {idBatch}");
                return ApiResponse<PostBatchExecuteResponse>.CreateError(
                    new ErrorDetails { Message = $"Failed to execute batch: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<DeleteProcessingBatchResponse>> DeleteProcessingBatchAsync(string idBatch)
        {
            try
            {
                _logger.LogInformation($"Deleting processing batch: {idBatch}");

                if (string.IsNullOrWhiteSpace(idBatch))
                    return ApiResponse<DeleteProcessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = "Batch ID cannot be null or empty" });

                if (!_activeBatches.TryGetValue(idBatch, out var batch))
                {
                    return ApiResponse<DeleteProcessingBatchResponse>.CreateError(
                        new ErrorDetails { Message = $"Batch '{idBatch}' not found" });
                }

                var wasRunning = batch.Status == ProcessingStatus.Running;
                var pythonRequest = new
                {
                    batch_id = idBatch,
                    force_cleanup = true,
                    action = "delete_batch"
                };

                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.PROCESSING, "delete_batch", pythonRequest);

                // Update batch status
                batch.Status = ProcessingStatus.Cancelled;
                batch.LastUpdated = DateTime.UtcNow;
                batch.CompletedAt = DateTime.UtcNow;

                var response = new DeleteProcessingBatchResponse
                {
                    Success = true,
                    Message = $"Batch {idBatch} deleted successfully"
                };

                // Remove from active batches
                _activeBatches.Remove(idBatch);

                _logger.LogInformation($"Successfully deleted batch: {idBatch}");
                return ApiResponse<DeleteProcessingBatchResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete processing batch: {idBatch}");
                return ApiResponse<DeleteProcessingBatchResponse>.CreateError(
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
                var pythonRequest = new { action = "get_workflows" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.PROCESSING, "get_workflows", pythonRequest);

                if (pythonResponse?.success == true && pythonResponse?.workflows != null)
                {
                    _workflowCache.Clear();
                    foreach (var workflow in pythonResponse.workflows)
                    {
                        var wf = CreateWorkflowFromPython(workflow);
                        _workflowCache[wf.Id] = wf;
                    }
                }
                else
                {
                    // Fallback to mock data
                    await PopulateMockWorkflowsAsync();
                }

                _lastWorkflowRefresh = DateTime.UtcNow;
                _logger.LogDebug($"Workflows refreshed with {_workflowCache.Count} workflows");
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
            foreach (var session in _activeSessions.Values.Where(s => s.Status == ProcessingStatus.Running))
            {
                await UpdateSessionStatusAsync(session);
            }
        }

        private async Task UpdateSessionStatusAsync(ProcessingSession session)
        {
            try
            {
                var pythonRequest = new { session_id = session.Id, action = "get_status" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.PROCESSING, "get_session_status", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    if (Enum.TryParse<ProcessingStatus>(pythonResponse.status?.ToString(), true, out ProcessingStatus status))
                    {
                        session.Status = status;
                    }

                    session.CurrentStep = pythonResponse.current_step ?? session.CurrentStep;
                    session.Progress = pythonResponse.progress ?? session.Progress;

                    if (session.Status == ProcessingStatus.Completed && pythonResponse.completed_at != null)
                    {
                        session.CompletedAt = DateTime.Parse(pythonResponse.completed_at.ToString());
                    }

                    session.LastUpdated = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to update status for session: {session.Id}");
            }
        }

        private async Task UpdateBatchStatusesAsync()
        {
            foreach (var batch in _activeBatches.Values.Where(b => b.Status == ProcessingStatus.Running))
            {
                await UpdateBatchStatusAsync(batch);
            }
        }

        private async Task UpdateBatchStatusAsync(ProcessingBatch batch)
        {
            try
            {
                var pythonRequest = new { batch_id = batch.Id, action = "get_status" };
                var pythonResponse = await _pythonWorkerService.ExecuteAsync<object, dynamic>(
                    PythonWorkerTypes.PROCESSING, "get_batch_status", pythonRequest);

                if (pythonResponse?.success == true)
                {
                    if (Enum.TryParse<ProcessingStatus>(pythonResponse.status?.ToString(), true, out ProcessingStatus status))
                    {
                        batch.Status = status;
                    }

                    batch.ProcessedItems = pythonResponse.processed_items ?? batch.ProcessedItems;
                    batch.FailedItems = pythonResponse.failed_items ?? batch.FailedItems;

                    if (batch.Status == ProcessingStatus.Completed && pythonResponse.completed_at != null)
                    {
                        batch.CompletedAt = DateTime.Parse(pythonResponse.completed_at.ToString());
                    }

                    batch.LastUpdated = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to update status for batch: {batch.Id}");
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

        #endregion
    }

    #region Helper Classes

    public class ProcessingWorkflow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public List<WorkflowStep> Steps { get; set; } = new();
        public List<string> RequiredModels { get; set; } = new();
        public TimeSpan EstimatedDuration { get; set; }
        public bool IsAvailable { get; set; }
        public int UsageCount { get; set; }
        public float AverageRating { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class WorkflowStep
    {
        public int Order { get; set; }
        public string Operation { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ProcessingSession
    {
        public string Id { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
        public ProcessingStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public int Progress { get; set; }
    }

    public class ProcessingBatch
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public int FailedItems { get; set; }
        public ProcessingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public int Priority { get; set; }
    }

    public class ExecutionHistoryItem
    {
        public DateTime ExecutedAt { get; set; }
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class BatchItemDetail
    {
        public string ItemId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Progress { get; set; }
    }

    public enum ProcessingStatus
    {
        Created,
        Queued,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled
    }

    public enum SessionAction
    {
        Pause,
        Resume,
        Cancel,
        Restart
    }

    #endregion
}
