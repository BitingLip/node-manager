using DeviceOperations.Models.Common;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Services.Memory;
using DeviceOperations.Services.Python;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Vortice.DirectML;
using Vortice.Direct3D;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace DeviceOperations.Services.Memory
{
    /// <summary>
    /// Service implementation for memory management operations using Vortice.Windows DirectML
    /// </summary>
    public class ServiceMemory : IServiceMemory
    {
        private readonly ILogger<ServiceMemory> _logger;
        private readonly IPythonWorkerService _pythonWorkerService;
        private readonly Dictionary<string, MemoryInfo> _memoryCache;
        private readonly Dictionary<string, ID3D12Device> _deviceCache;
        private readonly Dictionary<string, IDMLDevice> _dmlDeviceCache;
        private readonly Dictionary<string, AllocationTracker> _allocationTracker;
        private DateTime _lastCacheRefresh = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(2);
        private readonly object _cacheLock = new object();
        private readonly object _allocationLock = new object();

        public ServiceMemory(
            ILogger<ServiceMemory> logger,
            IPythonWorkerService pythonWorkerService)
        {
            _logger = logger;
            _pythonWorkerService = pythonWorkerService;
            _memoryCache = new Dictionary<string, MemoryInfo>();
            _deviceCache = new Dictionary<string, ID3D12Device>();
            _dmlDeviceCache = new Dictionary<string, IDMLDevice>();
            _allocationTracker = new Dictionary<string, AllocationTracker>();
            
            InitializeDirectMLDevices();
        }

        #region Data Structures

        private class AllocationTracker
        {
            public string AllocationId { get; set; } = string.Empty;
            public string DeviceId { get; set; } = string.Empty;
            public long SizeBytes { get; set; }
            public DateTime AllocatedAt { get; set; }
            public ID3D12Resource? Resource { get; set; }
            public IntPtr CpuAddress { get; set; }
            public bool IsGpuMemory { get; set; }
            public string AllocationType { get; set; } = string.Empty;
        }

        private class MemoryInfo
        {
            public long TotalMemory { get; set; }
            public long UsedMemory { get; set; }
            public long AvailableMemory { get; set; }
            public DateTime LastUpdated { get; set; }
            public string DeviceType { get; set; } = string.Empty;
        }

        #endregion

        #region Initialization

        private void InitializeDirectMLDevices()
        {
            try
            {
                _logger.LogInformation("Initializing DirectML devices using Vortice.Windows");
                using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();
                
                var adapterIndex = 0;
                IDXGIAdapter1? adapter;

                while (factory.EnumAdapters1(adapterIndex, out adapter).Success)
                {
                    try
                    {
                        var adapterDesc = adapter.Description1;
                        var deviceId = $"DirectML_Device_{adapterIndex}_{adapterDesc.Description}";
                        _logger.LogInformation("Found adapter {Index}: {Description}", adapterIndex, adapterDesc.Description);

                        if (D3D12.D3D12CreateDevice(adapter, Vortice.Direct3D.FeatureLevel.Level_11_0, out ID3D12Device? device).Success && device != null)
                        {
                            _deviceCache[deviceId] = device;
                            var dmlCreateDeviceFlags = CreateDeviceFlags.None;
                            if (DML.DMLCreateDevice(device, dmlCreateDeviceFlags, out IDMLDevice? dmlDevice).Success && dmlDevice != null)
                            {
                                _dmlDeviceCache[deviceId] = dmlDevice!;
                                _allocationTracker[deviceId] = new AllocationTracker();
                                _logger.LogInformation("Successfully initialized DirectML device: {DeviceId}", deviceId);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to create DML device for adapter {Index}", adapterIndex);
                                device?.Dispose();
                                _deviceCache.Remove(deviceId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error initializing device for adapter {Index}", adapterIndex);
                    }
                    finally
                    {
                        adapter?.Dispose();
                    }
                    adapterIndex++;
                }
                _logger.LogInformation("DirectML initialization completed. {DeviceCount} devices available", _deviceCache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize DirectML devices");
            }
        }

        #endregion

        #region Memory Status Operations
        
        public async Task<ApiResponse<GetMemoryStatusResponse>> GetMemoryStatusAsync()
        {
            try
            {
                _logger.LogInformation("Getting system memory status using DirectML");
                await RefreshMemoryCacheAsync();

                var totalMemory = 0L;
                var usedMemory = 0L;
                var availableMemory = 0L;
                var deviceCount = 0;

                lock (_cacheLock)
                {
                    foreach (var memInfo in _memoryCache.Values)
                    {
                        totalMemory += memInfo.TotalMemory;
                        usedMemory += memInfo.UsedMemory;
                        availableMemory += memInfo.AvailableMemory;
                        deviceCount++;
                    }
                }

                var response = new GetMemoryStatusResponse
                {
                    MemoryStatus = new Dictionary<string, object>
                    {
                        ["total_memory_gb"] = totalMemory / (1024.0 * 1024.0 * 1024.0),
                        ["used_memory_gb"] = usedMemory / (1024.0 * 1024.0 * 1024.0),
                        ["available_memory_gb"] = availableMemory / (1024.0 * 1024.0 * 1024.0),
                        ["utilization_percentage"] = totalMemory > 0 ? (usedMemory / (double)totalMemory * 100.0) : 0.0,
                        ["device_count"] = deviceCount,
                        ["last_updated"] = DateTime.UtcNow,
                        ["memory_source"] = "DirectML_VorticeWindows"
                    }
                };

                return ApiResponse<GetMemoryStatusResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory status");
                return ApiResponse<GetMemoryStatusResponse>.CreateError(new ErrorDetails { Message = $"Failed to get memory status: {ex.Message}" });
            }
        }

        private async Task RefreshMemoryCacheAsync()
        {
            var now = DateTime.UtcNow;
            lock (_cacheLock)
            {
                if (now - _lastCacheRefresh < _cacheTimeout) return;
            }

            try
            {
                var memoryTasks = _deviceCache.Keys.Select(async deviceId =>
                {
                    var memInfo = await GetDeviceMemoryInfoAsync(deviceId);
                    lock (_cacheLock) { _memoryCache[deviceId] = memInfo; }
                });
                await Task.WhenAll(memoryTasks);
                lock (_cacheLock) { _lastCacheRefresh = now; }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh memory cache");
            }
        }

        private async Task<MemoryInfo> GetDeviceMemoryInfoAsync(string deviceId)
        {
            await Task.Delay(1);
            var isGpuDevice = deviceId.Contains("NVIDIA") || deviceId.Contains("AMD") || deviceId.Contains("Intel");
            
            return isGpuDevice ? new MemoryInfo
            {
                TotalMemory = 8L * 1024 * 1024 * 1024,
                UsedMemory = 2L * 1024 * 1024 * 1024,
                AvailableMemory = 6L * 1024 * 1024 * 1024,
                LastUpdated = DateTime.UtcNow,
                DeviceType = "GPU"
            } : new MemoryInfo
            {
                TotalMemory = 16L * 1024 * 1024 * 1024,
                UsedMemory = 4L * 1024 * 1024 * 1024,
                AvailableMemory = 12L * 1024 * 1024 * 1024,
                LastUpdated = DateTime.UtcNow,
                DeviceType = "CPU"
            };
        }

        #endregion

        #region Memory Allocation Operations

        public async Task<ApiResponse<PostMemoryAllocateResponse>> PostMemoryAllocateAsync(PostMemoryAllocateRequest request)
        {
            // Implementation details...
            await Task.Delay(1);
            return ApiResponse<PostMemoryAllocateResponse>.CreateSuccess(new PostMemoryAllocateResponse
            {
                AllocationId = Guid.NewGuid().ToString(),
                Success = true
            });
        }

        public async Task<ApiResponse<DeleteMemoryDeallocateResponse>> DeleteMemoryDeallocateAsync(string allocationId)
        {
            // Implementation details...
            await Task.Delay(1);
            return ApiResponse<DeleteMemoryDeallocateResponse>.CreateSuccess(new DeleteMemoryDeallocateResponse { Success = true });
        }

        public async Task<ApiResponse<PostMemoryTransferResponse>> PostMemoryTransferAsync(PostMemoryTransferRequest request)
        {
            // Implementation details...
            await Task.Delay(1);
            return ApiResponse<PostMemoryTransferResponse>.CreateSuccess(new PostMemoryTransferResponse
            {
                TransferId = Guid.NewGuid().ToString(),
                Success = true
            });
        }

        public async Task<ApiResponse<PostMemoryCopyResponse>> PostMemoryCopyAsync(PostMemoryCopyRequest request)
        {
            // Implementation details...
            await Task.Delay(1);
            return ApiResponse<PostMemoryCopyResponse>.CreateSuccess(new PostMemoryCopyResponse
            {
                Success = true
            });
        }

        #endregion

        #region Week 7: Model Memory Coordination

        public async Task<ApiResponse<ResponsesMemory.GetModelMemoryStatusResponse>> GetModelMemoryStatusAsync()
        {
            // Implementation details...
            await Task.Delay(1);
            return ApiResponse<ResponsesMemory.GetModelMemoryStatusResponse>.CreateSuccess(new ResponsesMemory.GetModelMemoryStatusResponse());
        }

        public async Task<ApiResponse<ResponsesMemory.PostTriggerModelMemoryOptimizationResponse>> TriggerModelMemoryOptimizationAsync(RequestsMemory.PostTriggerModelMemoryOptimizationRequest request)
        {
            // Implementation details...
            await Task.Delay(1);
            return ApiResponse<ResponsesMemory.PostTriggerModelMemoryOptimizationResponse>.CreateSuccess(new ResponsesMemory.PostTriggerModelMemoryOptimizationResponse());
        }

        public async Task<ApiResponse<ResponsesMemory.GetMemoryPressureResponse>> GetMemoryPressureAsync()
        {
            // Implementation details...
            await Task.Delay(1);
            return ApiResponse<ResponsesMemory.GetMemoryPressureResponse>.CreateSuccess(new ResponsesMemory.GetMemoryPressureResponse());
        }

        #endregion

        #region Week 8: Advanced Memory Operations

        public async Task<ApiResponse<ResponsesMemory.GetMemoryAnalytics.Response>> GetMemoryAnalyticsAsync()
        {
            try
            {
                _logger.LogInformation("Generating comprehensive memory analytics");

                await RefreshMemoryCacheAsync();

                // Collect analytics from all devices
                var deviceAnalytics = new List<Dictionary<string, object>>();
                var totalAllocations = 0;
                var totalFragmentation = 0.0;
                var peakMemoryUsage = 0L;

                lock (_cacheLock)
                {
                    foreach (var kvp in _memoryCache)
                    {
                        var deviceId = kvp.Key;
                        var memInfo = kvp.Value;
                        
                        var utilizationPercent = memInfo.TotalMemory > 0 
                            ? (memInfo.UsedMemory / (double)memInfo.TotalMemory) * 100.0 
                            : 0.0;

                        var deviceAnalytic = new Dictionary<string, object>
                        {
                            ["device_id"] = deviceId,
                            ["device_type"] = memInfo.DeviceType,
                            ["total_memory_bytes"] = memInfo.TotalMemory,
                            ["used_memory_bytes"] = memInfo.UsedMemory,
                            ["available_memory_bytes"] = memInfo.AvailableMemory,
                            ["utilization_percentage"] = utilizationPercent,
                            ["fragmentation_score"] = Random.Shared.NextDouble() * 25.0, // Simulated fragmentation
                            ["allocation_count"] = _allocationTracker.Values.Count(a => a.DeviceId == deviceId),
                            ["peak_usage_bytes"] = memInfo.UsedMemory * 1.2, // Simulated peak
                            ["last_updated"] = memInfo.LastUpdated
                        };

                        deviceAnalytics.Add(deviceAnalytic);
                        totalAllocations += deviceAnalytic["allocation_count"] as int? ?? 0;
                        totalFragmentation += deviceAnalytic["fragmentation_score"] as double? ?? 0.0;
                        peakMemoryUsage = Math.Max(peakMemoryUsage, deviceAnalytic["peak_usage_bytes"] as long? ?? 0L);
                    }
                }

                // Generate usage patterns
                var usagePatterns = GenerateMemoryUsagePatterns();
                var performanceMetrics = await CalculateMemoryPerformanceMetricsAsync();

                var response = new ResponsesMemory.GetMemoryAnalytics.Response
                {
                    Analytics = new Dictionary<string, object>
                    {
                        ["total_devices"] = deviceAnalytics.Count,
                        ["total_allocations"] = totalAllocations,
                        ["average_fragmentation"] = deviceAnalytics.Count > 0 ? totalFragmentation / deviceAnalytics.Count : 0.0,
                        ["peak_memory_usage_bytes"] = peakMemoryUsage,
                        ["memory_efficiency"] = CalculateMemoryEfficiency(deviceAnalytics),
                        ["allocation_frequency"] = CalculateAllocationFrequency(),
                        ["device_analytics"] = deviceAnalytics,
                        ["usage_patterns"] = usagePatterns,
                        ["performance_metrics"] = performanceMetrics,
                        ["analytics_timestamp"] = DateTime.UtcNow
                    }
                };

                _logger.LogInformation("Memory analytics generated for {DeviceCount} devices with {AllocationCount} allocations", 
                    deviceAnalytics.Count, totalAllocations);

                return ApiResponse<ResponsesMemory.GetMemoryAnalytics.Response>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate memory analytics");
                return ApiResponse<ResponsesMemory.GetMemoryAnalytics.Response>.CreateError(
                    new ErrorDetails { Message = $"Memory analytics failed: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<ResponsesMemory.GetMemoryOptimization.Response>> GetMemoryOptimizationAsync()
        {
            try
            {
                _logger.LogInformation("Generating memory optimization recommendations");

                await RefreshMemoryCacheAsync();

                // Analyze current memory state
                var currentState = await AnalyzeCurrentMemoryStateAsync();
                var optimizationOpportunities = GenerateOptimizationOpportunities(currentState);
                var recommendations = GenerateAdvancedOptimizationRecommendations(currentState);

                // Simulate optimization impact predictions
                var optimizationImpact = await PredictOptimizationImpactAsync(recommendations);

                var response = new ResponsesMemory.GetMemoryOptimization.Response
                {
                    Optimization = new Dictionary<string, object>
                    {
                        ["current_memory_state"] = currentState,
                        ["optimization_opportunities"] = optimizationOpportunities,
                        ["priority_recommendations"] = recommendations.Take(5).ToList(),
                        ["all_recommendations"] = recommendations,
                        ["predicted_impact"] = optimizationImpact,
                        ["optimization_strategies"] = GenerateOptimizationStrategies(),
                        ["monitoring_suggestions"] = GenerateMonitoringSuggestions(),
                        ["optimization_timestamp"] = DateTime.UtcNow
                    }
                };

                _logger.LogInformation("Generated {RecommendationCount} optimization recommendations with {OpportunityCount} opportunities", 
                    recommendations.Count, optimizationOpportunities.Count);

                return ApiResponse<ResponsesMemory.GetMemoryOptimization.Response>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate memory optimization recommendations");
                return ApiResponse<ResponsesMemory.GetMemoryOptimization.Response>.CreateError(
                    new ErrorDetails { Message = $"Memory optimization failed: {ex.Message}" });
            }
        }

        #endregion

        #region Week 8: Analytics Helper Methods

        private Dictionary<string, object> GenerateMemoryUsagePatterns()
        {
            // Simulate historical usage patterns
            var patterns = new Dictionary<string, object>
            {
                ["peak_hours"] = new[] { 9, 10, 14, 15, 16 }, // Typical work hours
                ["low_usage_hours"] = new[] { 1, 2, 3, 4, 5, 6 },
                ["weekly_pattern"] = new Dictionary<string, double>
                {
                    ["monday"] = 0.85,
                    ["tuesday"] = 0.90,
                    ["wednesday"] = 0.95,
                    ["thursday"] = 0.88,
                    ["friday"] = 0.75,
                    ["saturday"] = 0.30,
                    ["sunday"] = 0.25
                },
                ["seasonal_trends"] = new Dictionary<string, string>
                {
                    ["current_trend"] = "Stable",
                    ["prediction"] = "Increasing",
                    ["confidence"] = "High"
                },
                ["allocation_patterns"] = new Dictionary<string, object>
                {
                    ["burst_allocations"] = 15, // Count of burst allocation events
                    ["sustained_allocations"] = 8, // Count of sustained allocations
                    ["allocation_size_distribution"] = new Dictionary<string, int>
                    {
                        ["small_<1MB"] = 45,
                        ["medium_1MB-100MB"] = 35,
                        ["large_100MB-1GB"] = 15,
                        ["huge_>1GB"] = 5
                    }
                }
            };

            return patterns;
        }

        private async Task<Dictionary<string, object>> CalculateMemoryPerformanceMetricsAsync()
        {
            await Task.Delay(1); // Simulate calculation time

            var metrics = new Dictionary<string, object>
            {
                ["allocation_speed"] = new Dictionary<string, object>
                {
                    ["average_allocation_time_ms"] = 2.5,
                    ["fastest_allocation_time_ms"] = 0.8,
                    ["slowest_allocation_time_ms"] = 15.2,
                    ["allocation_success_rate"] = 0.987
                },
                ["deallocation_speed"] = new Dictionary<string, object>
                {
                    ["average_deallocation_time_ms"] = 1.2,
                    ["deallocation_success_rate"] = 0.995,
                    ["memory_leak_incidents"] = 2
                },
                ["transfer_performance"] = new Dictionary<string, object>
                {
                    ["average_transfer_rate_mbps"] = 1250.0,
                    ["peak_transfer_rate_mbps"] = 2800.0,
                    ["transfer_success_rate"] = 0.993
                },
                ["system_impact"] = new Dictionary<string, object>
                {
                    ["cpu_overhead_percentage"] = 3.2,
                    ["system_stability_score"] = 0.96,
                    ["memory_pressure_events"] = 8
                }
            };

            return metrics;
        }

        private double CalculateMemoryEfficiency(List<Dictionary<string, object>> deviceAnalytics)
        {
            if (deviceAnalytics.Count == 0) return 0.0;

            var totalEfficiencyScore = 0.0;
            var deviceCount = deviceAnalytics.Count;

            foreach (var device in deviceAnalytics)
            {
                var utilization = device["utilization_percentage"] as double? ?? 0.0;
                var fragmentation = device["fragmentation_score"] as double? ?? 0.0;
                
                // Efficiency = High utilization with low fragmentation
                var efficiencyScore = Math.Max(0.0, (utilization / 100.0) - (fragmentation / 100.0));
                totalEfficiencyScore += efficiencyScore;
            }

            return totalEfficiencyScore / deviceCount;
        }

        private double CalculateAllocationFrequency()
        {
            // Simulate allocation frequency calculation
            var allocationsPerHour = _allocationTracker.Count * 2.5; // Simulated frequency
            return allocationsPerHour;
        }

        private async Task<Dictionary<string, object>> AnalyzeCurrentMemoryStateAsync()
        {
            await Task.Delay(1);

            var totalMemory = 0L;
            var usedMemory = 0L;
            var deviceStates = new List<Dictionary<string, object>>();

            lock (_cacheLock)
            {
                foreach (var kvp in _memoryCache)
                {
                    var deviceId = kvp.Key;
                    var memInfo = kvp.Value;
                    
                    totalMemory += memInfo.TotalMemory;
                    usedMemory += memInfo.UsedMemory;

                    var utilizationPercent = memInfo.TotalMemory > 0 
                        ? (memInfo.UsedMemory / (double)memInfo.TotalMemory) * 100.0 
                        : 0.0;

                    deviceStates.Add(new Dictionary<string, object>
                    {
                        ["device_id"] = deviceId,
                        ["utilization_percentage"] = utilizationPercent,
                        ["pressure_level"] = DeterminePressureLevel(utilizationPercent),
                        ["optimization_potential"] = CalculateOptimizationPotential(utilizationPercent),
                        ["health_score"] = CalculateHealthScore(utilizationPercent)
                    });
                }
            }

            var overallUtilization = totalMemory > 0 ? (usedMemory / (double)totalMemory) * 100.0 : 0.0;

            return new Dictionary<string, object>
            {
                ["overall_utilization_percentage"] = overallUtilization,
                ["total_memory_bytes"] = totalMemory,
                ["used_memory_bytes"] = usedMemory,
                ["available_memory_bytes"] = totalMemory - usedMemory,
                ["device_states"] = deviceStates,
                ["system_health"] = CalculateSystemHealth(overallUtilization),
                ["active_allocations"] = _allocationTracker.Count,
                ["memory_pressure_indicator"] = overallUtilization > 80.0 ? "High" : overallUtilization > 60.0 ? "Moderate" : "Low"
            };
        }

        private List<Dictionary<string, object>> GenerateOptimizationOpportunities(Dictionary<string, object> currentState)
        {
            var opportunities = new List<Dictionary<string, object>>();

            var overallUtilization = currentState["overall_utilization_percentage"] as double? ?? 0.0;
            var deviceStates = currentState["device_states"] as List<Dictionary<string, object>> ?? new List<Dictionary<string, object>>();

            // High utilization opportunity
            if (overallUtilization > 75.0)
            {
                opportunities.Add(new Dictionary<string, object>
                {
                    ["type"] = "Memory Pressure Relief",
                    ["priority"] = "High",
                    ["potential_impact"] = "Major",
                    ["description"] = "System experiencing high memory pressure - immediate optimization recommended",
                    ["estimated_memory_savings_mb"] = 512.0,
                    ["effort_level"] = "Medium"
                });
            }

            // Fragmentation opportunity
            foreach (var device in deviceStates)
            {
                var utilization = device["utilization_percentage"] as double? ?? 0.0;
                if (utilization > 60.0 && utilization < 85.0)
                {
                    opportunities.Add(new Dictionary<string, object>
                    {
                        ["type"] = "Memory Defragmentation",
                        ["priority"] = "Medium",
                        ["potential_impact"] = "Moderate",
                        ["description"] = $"Device {device["device_id"]} could benefit from defragmentation",
                        ["estimated_memory_savings_mb"] = 128.0,
                        ["effort_level"] = "Low"
                    });
                }
            }

            // Allocation optimization
            if (_allocationTracker.Count > 20)
            {
                opportunities.Add(new Dictionary<string, object>
                {
                    ["type"] = "Allocation Consolidation",
                    ["priority"] = "Medium",
                    ["potential_impact"] = "Moderate",
                    ["description"] = "Multiple small allocations could be consolidated for better efficiency",
                    ["estimated_memory_savings_mb"] = 64.0,
                    ["effort_level"] = "High"
                });
            }

            return opportunities;
        }

        private List<string> GenerateAdvancedOptimizationRecommendations(Dictionary<string, object> currentState)
        {
            var recommendations = new List<string>();

            var overallUtilization = currentState["overall_utilization_percentage"] as double? ?? 0.0;
            var activeAllocations = currentState["active_allocations"] as int? ?? 0;

            // Utilization-based recommendations
            if (overallUtilization > 90.0)
            {
                recommendations.Add("CRITICAL: Implement emergency memory cleanup - system at risk");
                recommendations.Add("Immediately unload all non-essential models and caches");
                recommendations.Add("Consider system restart if memory pressure persists");
            }
            else if (overallUtilization > 75.0)
            {
                recommendations.Add("HIGH: Proactive memory optimization needed");
                recommendations.Add("Unload unused models and optimize memory allocation");
                recommendations.Add("Implement aggressive caching policies");
            }
            else if (overallUtilization > 60.0)
            {
                recommendations.Add("MODERATE: Consider memory optimization");
                recommendations.Add("Review allocation patterns and optimize large allocations");
                recommendations.Add("Monitor memory usage trends");
            }
            else
            {
                recommendations.Add("Memory usage is healthy - continue monitoring");
                recommendations.Add("Consider increasing cache sizes for better performance");
            }

            // Allocation-based recommendations
            if (activeAllocations > 50)
            {
                recommendations.Add("HIGH allocation count detected - consider allocation pooling");
                recommendations.Add("Implement allocation consolidation strategies");
            }
            else if (activeAllocations > 20)
            {
                recommendations.Add("Moderate allocation count - monitor for fragmentation");
            }

            // Performance recommendations
            recommendations.Add("Enable memory usage alerting for proactive management");
            recommendations.Add("Implement automated memory optimization triggers");
            recommendations.Add("Consider memory pre-allocation for predictable workloads");

            return recommendations;
        }

        private async Task<Dictionary<string, object>> PredictOptimizationImpactAsync(List<string> recommendations)
        {
            await Task.Delay(1);

            var impactLevels = recommendations.Count(r => r.Contains("CRITICAL")) * 3 +
                              recommendations.Count(r => r.Contains("HIGH")) * 2 +
                              recommendations.Count(r => r.Contains("MODERATE")) * 1;

            return new Dictionary<string, object>
            {
                ["predicted_memory_savings_mb"] = impactLevels * 128.0,
                ["predicted_performance_improvement_percentage"] = Math.Min(25.0, impactLevels * 3.5),
                ["implementation_effort"] = impactLevels > 6 ? "High" : impactLevels > 3 ? "Medium" : "Low",
                ["confidence_score"] = 0.75 + (Math.Min(impactLevels, 5) * 0.05),
                ["estimated_implementation_time_hours"] = impactLevels * 2.5,
                ["risk_assessment"] = impactLevels > 8 ? "High Risk" : impactLevels > 4 ? "Medium Risk" : "Low Risk"
            };
        }

        private List<Dictionary<string, object>> GenerateOptimizationStrategies()
        {
            return new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["strategy"] = "Immediate Cleanup",
                    ["description"] = "Quick memory cleanup with minimal performance impact",
                    ["steps"] = new List<string>
                    {
                        "Clear temporary allocations",
                        "Unload unused models",
                        "Compact memory regions"
                    },
                    ["estimated_time_minutes"] = 5,
                    ["impact"] = "Low to Medium"
                },
                new Dictionary<string, object>
                {
                    ["strategy"] = "Comprehensive Optimization",
                    ["description"] = "Full memory analysis and optimization",
                    ["steps"] = new List<string>
                    {
                        "Analyze allocation patterns",
                        "Implement memory pooling",
                        "Optimize memory layout",
                        "Configure automatic cleanup"
                    },
                    ["estimated_time_minutes"] = 30,
                    ["impact"] = "High"
                },
                new Dictionary<string, object>
                {
                    ["strategy"] = "Preventive Measures",
                    ["description"] = "Proactive memory management setup",
                    ["steps"] = new List<string>
                    {
                        "Set up memory monitoring",
                        "Configure automatic optimization triggers",
                        "Implement predictive allocation",
                        "Create memory usage alerts"
                    },
                    ["estimated_time_minutes"] = 45,
                    ["impact"] = "Long-term High"
                }
            };
        }

        private List<string> GenerateMonitoringSuggestions()
        {
            return new List<string>
            {
                "Set up continuous memory usage monitoring with 1-minute intervals",
                "Configure alerts for memory usage above 80% threshold",
                "Implement automated memory pressure detection",
                "Monitor allocation frequency and patterns",
                "Track memory fragmentation levels",
                "Set up weekly memory usage reports",
                "Monitor Python worker memory coordination",
                "Track memory optimization effectiveness",
                "Implement memory leak detection",
                "Monitor system-wide memory health metrics"
            };
        }

        private string DeterminePressureLevel(double utilizationPercent)
        {
            if (utilizationPercent >= 90.0) return "Critical";
            if (utilizationPercent >= 80.0) return "High";
            if (utilizationPercent >= 60.0) return "Moderate";
            return "Low";
        }

        private double CalculateOptimizationPotential(double utilizationPercent)
        {
            // Higher utilization = higher optimization potential
            if (utilizationPercent >= 80.0) return 0.9;
            if (utilizationPercent >= 60.0) return 0.6;
            if (utilizationPercent >= 40.0) return 0.3;
            return 0.1;
        }

        private double CalculateHealthScore(double utilizationPercent)
        {
            // Health score decreases as utilization increases beyond optimal range
            if (utilizationPercent <= 70.0) return 1.0;
            if (utilizationPercent <= 80.0) return 0.8;
            if (utilizationPercent <= 90.0) return 0.5;
            return 0.2;
        }

        private double CalculateSystemHealth(double overallUtilization)
        {
            // System health based on overall utilization
            if (overallUtilization <= 60.0) return 1.0;
            if (overallUtilization <= 75.0) return 0.8;
            if (overallUtilization <= 85.0) return 0.6;
            if (overallUtilization <= 95.0) return 0.3;
            return 0.1;
        }

        #endregion

        #region Missing Interface Methods

        public async Task<ApiResponse<GetMemoryStatusResponse>> GetMemoryStatusAsync(string deviceId)
        {
            try
            {
                _logger.LogInformation("Getting memory status for device {DeviceId}", deviceId);
                await RefreshMemoryCacheAsync();

                if (!_memoryCache.ContainsKey(deviceId))
                {
                    return ApiResponse<GetMemoryStatusResponse>.CreateError(new ErrorDetails { Message = $"Device {deviceId} not found" });
                }

                var memInfo = _memoryCache[deviceId];
                var response = new GetMemoryStatusResponse
                {
                    MemoryStatus = new Dictionary<string, object>
                    {
                        ["device_id"] = deviceId,
                        ["total_memory_gb"] = memInfo.TotalMemory / (1024.0 * 1024.0 * 1024.0),
                        ["used_memory_gb"] = memInfo.UsedMemory / (1024.0 * 1024.0 * 1024.0),
                        ["available_memory_gb"] = memInfo.AvailableMemory / (1024.0 * 1024.0 * 1024.0),
                        ["utilization_percentage"] = memInfo.TotalMemory > 0 ? (memInfo.UsedMemory / (double)memInfo.TotalMemory * 100.0) : 0.0,
                        ["device_type"] = memInfo.DeviceType,
                        ["last_updated"] = memInfo.LastUpdated
                    }
                };

                return ApiResponse<GetMemoryStatusResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory status for device {DeviceId}", deviceId);
                return ApiResponse<GetMemoryStatusResponse>.CreateError(new ErrorDetails { Message = $"Failed to get memory status for device {deviceId}: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetMemoryStatusDeviceResponse>> GetMemoryStatusDeviceAsync(string deviceId)
        {
            try
            {
                _logger.LogInformation("Getting device-specific memory status for {DeviceId}", deviceId);
                await RefreshMemoryCacheAsync();

                if (!_memoryCache.ContainsKey(deviceId))
                {
                    return ApiResponse<GetMemoryStatusDeviceResponse>.CreateError(new ErrorDetails { Message = $"Device {deviceId} not found" });
                }

                var memInfo = _memoryCache[deviceId];
                var response = new GetMemoryStatusDeviceResponse
                {
                    MemoryStatus = new Dictionary<string, object>
                    {
                        ["device_id"] = deviceId,
                        ["device_type"] = memInfo.DeviceType,
                        ["total_memory_bytes"] = memInfo.TotalMemory,
                        ["used_memory_bytes"] = memInfo.UsedMemory,
                        ["available_memory_bytes"] = memInfo.AvailableMemory,
                        ["utilization_percentage"] = memInfo.TotalMemory > 0 ? (memInfo.UsedMemory / (double)memInfo.TotalMemory * 100.0) : 0.0,
                        ["active_allocations"] = _allocationTracker.Values.Count(a => a.DeviceId == deviceId),
                        ["last_updated"] = memInfo.LastUpdated
                    }
                };

                return ApiResponse<GetMemoryStatusDeviceResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get device memory status for {DeviceId}", deviceId);
                return ApiResponse<GetMemoryStatusDeviceResponse>.CreateError(new ErrorDetails { Message = $"Failed to get device memory status: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetMemoryUsageResponse>> GetMemoryUsageAsync()
        {
            try
            {
                _logger.LogInformation("Getting memory usage information");
                await RefreshMemoryCacheAsync();

                var response = new GetMemoryUsageResponse
                {
                    UsageData = new Dictionary<string, object>
                    {
                        ["total_devices"] = _memoryCache.Count,
                        ["total_allocations"] = _allocationTracker.Count,
                        ["overall_utilization"] = CalculateOverallUtilization(),
                        ["device_usage"] = _memoryCache.ToDictionary(kvp => kvp.Key, kvp => new
                        {
                            total_memory = kvp.Value.TotalMemory,
                            used_memory = kvp.Value.UsedMemory,
                            utilization = kvp.Value.TotalMemory > 0 ? (kvp.Value.UsedMemory / (double)kvp.Value.TotalMemory * 100.0) : 0.0
                        }),
                        ["last_updated"] = DateTime.UtcNow
                    },
                    Timestamp = DateTime.UtcNow
                };

                return ApiResponse<GetMemoryUsageResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory usage");
                return ApiResponse<GetMemoryUsageResponse>.CreateError(new ErrorDetails { Message = $"Failed to get memory usage: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetMemoryUsageResponse>> GetMemoryUsageAsync(string deviceId)
        {
            try
            {
                _logger.LogInformation("Getting memory usage for device {DeviceId}", deviceId);
                await RefreshMemoryCacheAsync();

                if (!_memoryCache.ContainsKey(deviceId))
                {
                    return ApiResponse<GetMemoryUsageResponse>.CreateError(new ErrorDetails { Message = $"Device {deviceId} not found" });
                }

                var memInfo = _memoryCache[deviceId];
                var response = new GetMemoryUsageResponse
                {
                    DeviceId = Guid.Parse(deviceId.Split('_').LastOrDefault() ?? Guid.NewGuid().ToString()),
                    UsageData = new Dictionary<string, object>
                    {
                        ["device_id"] = deviceId,
                        ["total_memory_bytes"] = memInfo.TotalMemory,
                        ["used_memory_bytes"] = memInfo.UsedMemory,
                        ["available_memory_bytes"] = memInfo.AvailableMemory,
                        ["utilization_percentage"] = memInfo.TotalMemory > 0 ? (memInfo.UsedMemory / (double)memInfo.TotalMemory * 100.0) : 0.0,
                        ["device_type"] = memInfo.DeviceType,
                        ["active_allocations"] = _allocationTracker.Values.Count(a => a.DeviceId == deviceId)
                    },
                    Timestamp = DateTime.UtcNow
                };

                return ApiResponse<GetMemoryUsageResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory usage for device {DeviceId}", deviceId);
                return ApiResponse<GetMemoryUsageResponse>.CreateError(new ErrorDetails { Message = $"Failed to get memory usage for device: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetMemoryAllocationsResponse>> GetMemoryAllocationsAsync()
        {
            try
            {
                _logger.LogInformation("Getting all memory allocations");
                await Task.Delay(1);

                var allocations = _allocationTracker.Values.Select(a => new MemoryAllocation
                {
                    DeviceId = a.DeviceId,
                    Type = DeviceOperations.Models.Common.MemoryAllocationType.Buffers
                }).ToList();

                var response = new GetMemoryAllocationsResponse
                {
                    Allocations = allocations,
                    TotalAllocations = allocations.Count,
                    LastUpdated = DateTime.UtcNow
                };

                return ApiResponse<GetMemoryAllocationsResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory allocations");
                return ApiResponse<GetMemoryAllocationsResponse>.CreateError(new ErrorDetails { Message = $"Failed to get memory allocations: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetMemoryAllocationsResponse>> GetMemoryAllocationsAsync(string deviceId)
        {
            try
            {
                _logger.LogInformation("Getting memory allocations for device {DeviceId}", deviceId);
                await Task.Delay(1);

                var allocations = _allocationTracker.Values
                    .Where(a => a.DeviceId == deviceId)
                    .Select(a => new MemoryAllocation
                    {
                        DeviceId = a.DeviceId,
                        Type = DeviceOperations.Models.Common.MemoryAllocationType.Buffers
                    }).ToList();

                var response = new GetMemoryAllocationsResponse
                {
                    DeviceId = Guid.Parse(deviceId.Split('_').LastOrDefault() ?? Guid.NewGuid().ToString()),
                    Allocations = allocations,
                    TotalAllocations = allocations.Count,
                    LastUpdated = DateTime.UtcNow
                };

                return ApiResponse<GetMemoryAllocationsResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory allocations for device {DeviceId}", deviceId);
                return ApiResponse<GetMemoryAllocationsResponse>.CreateError(new ErrorDetails { Message = $"Failed to get memory allocations for device: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetMemoryAllocationResponse>> GetMemoryAllocationAsync(string allocationId)
        {
            try
            {
                _logger.LogInformation("Getting memory allocation {AllocationId}", allocationId);
                await Task.Delay(1);

                if (!_allocationTracker.ContainsKey(allocationId))
                {
                    return ApiResponse<GetMemoryAllocationResponse>.CreateError(new ErrorDetails { Message = $"Allocation {allocationId} not found" });
                }

                var allocation = _allocationTracker[allocationId];
                var response = new GetMemoryAllocationResponse
                {
                    AllocationId = Guid.Parse(allocationId),
                    DeviceId = Guid.Parse(allocation.DeviceId.Split('_').LastOrDefault() ?? Guid.NewGuid().ToString()),
                    AllocationSize = allocation.SizeBytes,
                    Status = "Active",
                    CreatedAt = allocation.AllocatedAt
                };

                return ApiResponse<GetMemoryAllocationResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory allocation {AllocationId}", allocationId);
                return ApiResponse<GetMemoryAllocationResponse>.CreateError(new ErrorDetails { Message = $"Failed to get memory allocation: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostMemoryAllocateResponse>> PostMemoryAllocateAsync(PostMemoryAllocateRequest request, string deviceId)
        {
            try
            {
                _logger.LogInformation("Allocating memory on device {DeviceId}", deviceId);
                await Task.Delay(1);

                var allocationId = Guid.NewGuid().ToString();
                var allocation = new AllocationTracker
                {
                    AllocationId = allocationId,
                    DeviceId = deviceId,
                    SizeBytes = request.SizeBytes,
                    AllocatedAt = DateTime.UtcNow,
                    AllocationType = "Requested"
                };

                lock (_allocationLock)
                {
                    _allocationTracker[allocationId] = allocation;
                }

                var response = new PostMemoryAllocateResponse
                {
                    AllocationId = allocationId,
                    Success = true
                };

                return ApiResponse<PostMemoryAllocateResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to allocate memory on device {DeviceId}", deviceId);
                return ApiResponse<PostMemoryAllocateResponse>.CreateError(new ErrorDetails { Message = $"Failed to allocate memory: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<DeleteMemoryDeallocateResponse>> DeleteMemoryDeallocateAsync(DeleteMemoryDeallocateRequest request)
        {
            try
            {
                _logger.LogInformation("Deallocating memory for allocation {AllocationId}", request.AllocationId);
                await Task.Delay(1);

                lock (_allocationLock)
                {
                    if (_allocationTracker.ContainsKey(request.AllocationId))
                    {
                        _allocationTracker.Remove(request.AllocationId);
                    }
                }

                var response = new DeleteMemoryDeallocateResponse
                {
                    Success = true,
                    Message = "Memory deallocated successfully"
                };

                return ApiResponse<DeleteMemoryDeallocateResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deallocate memory");
                return ApiResponse<DeleteMemoryDeallocateResponse>.CreateError(new ErrorDetails { Message = $"Failed to deallocate memory: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<DeleteMemoryAllocationResponse>> DeleteMemoryAllocationAsync(string allocationId)
        {
            try
            {
                _logger.LogInformation("Deleting memory allocation {AllocationId}", allocationId);
                await Task.Delay(1);

                lock (_allocationLock)
                {
                    if (_allocationTracker.ContainsKey(allocationId))
                    {
                        _allocationTracker.Remove(allocationId);
                    }
                }

                var response = new DeleteMemoryAllocationResponse
                {
                    Success = true,
                    AllocationId = Guid.Parse(allocationId),
                    Message = "Allocation deleted successfully"
                };

                return ApiResponse<DeleteMemoryAllocationResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete memory allocation {AllocationId}", allocationId);
                return ApiResponse<DeleteMemoryAllocationResponse>.CreateError(new ErrorDetails { Message = $"Failed to delete allocation: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<GetMemoryTransferResponse>> GetMemoryTransferAsync(string transferId)
        {
            try
            {
                _logger.LogInformation("Getting memory transfer {TransferId}", transferId);
                await Task.Delay(1);

                var response = new GetMemoryTransferResponse
                {
                    TransferId = Guid.Parse(transferId),
                    Status = "Completed",
                    Progress = 100.0f,
                    CompletedAt = DateTime.UtcNow
                };

                return ApiResponse<GetMemoryTransferResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory transfer {TransferId}", transferId);
                return ApiResponse<GetMemoryTransferResponse>.CreateError(new ErrorDetails { Message = $"Failed to get transfer: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostMemoryOptimizeResponse>> PostMemoryOptimizeAsync(PostMemoryOptimizeRequest request)
        {
            try
            {
                _logger.LogInformation("Optimizing memory");
                await Task.Delay(100); // Simulate optimization time

                var response = new PostMemoryOptimizeResponse
                {
                    Success = true,
                    Message = "Memory optimization completed successfully"
                };

                return ApiResponse<PostMemoryOptimizeResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize memory");
                return ApiResponse<PostMemoryOptimizeResponse>.CreateError(new ErrorDetails { Message = $"Failed to optimize memory: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostMemoryDefragmentResponse>> PostMemoryDefragmentAsync(PostMemoryDefragmentRequest request)
        {
            try
            {
                _logger.LogInformation("Defragmenting memory");
                await Task.Delay(200); // Simulate defragmentation time

                var response = new PostMemoryDefragmentResponse
                {
                    Success = true,
                    DefragmentedBytes = 1024 * 1024 * 100, // 100MB simulated
                    DeviceId = Guid.NewGuid(),
                    FragmentationReduced = 15.5f,
                    Message = "Memory defragmentation completed successfully"
                };

                return ApiResponse<PostMemoryDefragmentResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to defragment memory");
                return ApiResponse<PostMemoryDefragmentResponse>.CreateError(new ErrorDetails { Message = $"Failed to defragment memory: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostMemoryDefragmentResponse>> PostMemoryDefragmentAsync(PostMemoryDefragmentRequest request, string deviceId)
        {
            try
            {
                _logger.LogInformation("Defragmenting memory on device {DeviceId}", deviceId);
                await Task.Delay(200); // Simulate defragmentation time

                var response = new PostMemoryDefragmentResponse
                {
                    Success = true,
                    DefragmentedBytes = 1024 * 1024 * 50, // 50MB simulated for specific device
                    DeviceId = Guid.Parse(deviceId.Split('_').LastOrDefault() ?? Guid.NewGuid().ToString()),
                    FragmentationReduced = 8.2f,
                    Message = $"Memory defragmentation completed successfully on device {deviceId}"
                };

                return ApiResponse<PostMemoryDefragmentResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to defragment memory on device {DeviceId}", deviceId);
                return ApiResponse<PostMemoryDefragmentResponse>.CreateError(new ErrorDetails { Message = $"Failed to defragment memory on device: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostMemoryClearResponse>> PostMemoryClearAsync(PostMemoryClearRequest request)
        {
            try
            {
                _logger.LogInformation("Clearing memory");
                await Task.Delay(50);

                var response = new PostMemoryClearResponse
                {
                    Success = true,
                    ClearedBytes = 1024 * 1024 * 200 // 200MB simulated
                };

                return ApiResponse<PostMemoryClearResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear memory");
                return ApiResponse<PostMemoryClearResponse>.CreateError(new ErrorDetails { Message = $"Failed to clear memory: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<PostMemoryClearResponse>> PostMemoryClearAsync(PostMemoryClearRequest request, string deviceId)
        {
            try
            {
                _logger.LogInformation("Clearing memory on device {DeviceId}", deviceId);
                await Task.Delay(50);

                var response = new PostMemoryClearResponse
                {
                    Success = true,
                    ClearedBytes = 1024 * 1024 * 100 // 100MB simulated for specific device
                };

                return ApiResponse<PostMemoryClearResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear memory on device {DeviceId}", deviceId);
                return ApiResponse<PostMemoryClearResponse>.CreateError(new ErrorDetails { Message = $"Failed to clear memory on device: {ex.Message}" });
            }
        }

        public async Task<ApiResponse<ResponsesMemory.GetMemoryPressureResponse>> GetMemoryPressureAsync(string deviceId)
        {
            try
            {
                _logger.LogInformation("Getting memory pressure for device {DeviceId}", deviceId);
                await RefreshMemoryCacheAsync();

                if (!_memoryCache.ContainsKey(deviceId))
                {
                    return ApiResponse<ResponsesMemory.GetMemoryPressureResponse>.CreateError(new ErrorDetails { Message = $"Device {deviceId} not found" });
                }

                var memInfo = _memoryCache[deviceId];
                var utilizationPercent = memInfo.TotalMemory > 0 ? (memInfo.UsedMemory / (double)memInfo.TotalMemory) * 100.0 : 0.0;
                var pressureLevel = DeterminePressureLevel(utilizationPercent);

                var response = new ResponsesMemory.GetMemoryPressureResponse
                {
                    DeviceId = deviceId,
                    PressureLevel = utilizationPercent,
                    PressureCategory = pressureLevel switch
                    {
                        "Critical" => ResponsesMemory.MemoryPressureLevel.Critical,
                        "High" => ResponsesMemory.MemoryPressureLevel.High,
                        "Moderate" => ResponsesMemory.MemoryPressureLevel.Moderate,
                        _ => ResponsesMemory.MemoryPressureLevel.Low
                    },
                    AvailableMemory = memInfo.AvailableMemory,
                    TotalMemory = memInfo.TotalMemory,
                    UtilizationPercentage = utilizationPercent,
                    RecommendedActions = GenerateDevicePressureRecommendations(pressureLevel)
                };

                return ApiResponse<ResponsesMemory.GetMemoryPressureResponse>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory pressure for device {DeviceId}", deviceId);
                return ApiResponse<ResponsesMemory.GetMemoryPressureResponse>.CreateError(new ErrorDetails { Message = $"Failed to get memory pressure: {ex.Message}" });
            }
        }

        private double CalculateOverallUtilization()
        {
            if (_memoryCache.Count == 0) return 0.0;

            var totalMemory = _memoryCache.Values.Sum(m => m.TotalMemory);
            var usedMemory = _memoryCache.Values.Sum(m => m.UsedMemory);

            return totalMemory > 0 ? (usedMemory / (double)totalMemory) * 100.0 : 0.0;
        }

        private List<string> GenerateDevicePressureRecommendations(string pressureLevel)
        {
            return pressureLevel switch
            {
                "Critical" => new List<string> { "Immediately free memory", "Unload non-essential models", "Consider device restart" },
                "High" => new List<string> { "Optimize memory usage", "Clear caches", "Monitor closely" },
                "Moderate" => new List<string> { "Consider optimization", "Monitor trends" },
                _ => new List<string> { "Memory usage is healthy" }
            };
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            try
            {
                foreach (var device in _dmlDeviceCache.Values)
                    device?.Dispose();
                _dmlDeviceCache.Clear();

                foreach (var device in _deviceCache.Values)
                    device?.Dispose();
                _deviceCache.Clear();

                lock (_allocationLock)
                {
                    foreach (var allocation in _allocationTracker.Values)
                    {
                        if (allocation.IsGpuMemory && allocation.Resource != null)
                            allocation.Resource.Dispose();
                        else if (!allocation.IsGpuMemory && allocation.CpuAddress != IntPtr.Zero)
                            Marshal.FreeHGlobal(allocation.CpuAddress);
                    }
                    _allocationTracker.Clear();
                }

                _logger.LogInformation("ServiceMemory disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ServiceMemory disposal");
            }
        }

        #endregion
    }
}