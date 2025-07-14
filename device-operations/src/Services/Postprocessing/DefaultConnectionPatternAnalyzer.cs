using DeviceOperations.Models.Postprocessing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeviceOperations.Services.Postprocessing
{
    /// <summary>
    /// Default implementation of connection pattern analysis
    /// </summary>
    public class DefaultConnectionPatternAnalyzer : IConnectionPatternAnalyzer
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, List<PatternObservation>> _patternObservations;
        private readonly Dictionary<string, PatternStatistics> _patternStats;
        private readonly object _lockObject = new object();

        public DefaultConnectionPatternAnalyzer(ILogger logger)
        {
            _logger = logger;
            _patternObservations = new Dictionary<string, List<PatternObservation>>();
            _patternStats = new Dictionary<string, PatternStatistics>();
        }

        public async Task<RequestPattern> AnalyzeRequestPattern(object request)
        {
            try
            {
                await Task.Delay(1); // Simulate async analysis
                
                var requestType = DetermineRequestType(request);
                var parameters = ExtractParameters(request);
                var complexity = CalculateComplexityScore(parameters);
                var resourceRequirements = EstimateResourceRequirements(parameters, complexity);
                var expectedDuration = EstimateDuration(requestType, complexity, resourceRequirements);

                var pattern = new RequestPattern
                {
                    RequestType = requestType,
                    Parameters = parameters,
                    ExpectedDuration = expectedDuration,
                    ResourceRequirements = resourceRequirements,
                    ComplexityScore = complexity,
                    Timestamp = DateTime.UtcNow
                };

                // Record pattern observation
                await RecordPatternObservation(pattern);

                _logger.LogDebug($"Analyzed request pattern: {requestType} with complexity {complexity:F2}");
                return pattern;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing request pattern");
                return CreateFallbackPattern();
            }
        }

        public async Task RecordPatternOutcome(PostprocessingPerformanceData performanceData)
        {
            try
            {
                await Task.Delay(1); // Simulate async processing
                
                var patternKey = GeneratePatternKey(performanceData);
                
                lock (_lockObject)
                {
                    if (!_patternObservations.ContainsKey(patternKey))
                    {
                        _patternObservations[patternKey] = new List<PatternObservation>();
                        _patternStats[patternKey] = new PatternStatistics();
                    }
                    
                    var observation = new PatternObservation
                    {
                        Timestamp = performanceData.Timestamp,
                        ActualDuration = TimeSpan.FromMilliseconds(performanceData.AverageLatencyMs),
                        ActualMemoryUsage = performanceData.MemoryUsageMB,
                        ActualCpuUsage = performanceData.CpuUtilization,
                        ActualGpuUsage = performanceData.GpuUtilization,
                        Success = performanceData.ErrorRate < 0.05,
                        Throughput = performanceData.RequestsPerSecond
                    };
                    
                    _patternObservations[patternKey].Add(observation);
                    
                    // Keep only recent observations (last 500 per pattern)
                    if (_patternObservations[patternKey].Count > 500)
                    {
                        _patternObservations[patternKey].RemoveAt(0);
                    }
                    
                    // Update pattern statistics
                    UpdatePatternStatistics(patternKey, observation);
                }

                _logger.LogDebug($"Recorded pattern outcome for: {patternKey}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording pattern outcome");
            }
        }

        public async Task<PatternAnalysis> AnalyzeLongTermTrends(List<PostprocessingRequestTrace> traces)
        {
            try
            {
                await Task.Delay(1); // Simulate async analysis
                
                var analysis = new PatternAnalysis();
                
                if (!traces.Any())
                {
                    return analysis;
                }

                // Analyze trends over time periods
                var timeWindows = new[]
                {
                    ("Last_Hour", TimeSpan.FromHours(1)),
                    ("Last_Day", TimeSpan.FromDays(1)),
                    ("Last_Week", TimeSpan.FromDays(7))
                };

                foreach (var (windowName, window) in timeWindows)
                {
                    var windowTraces = traces.Where(t => t.Timestamp > DateTime.UtcNow - window).ToList();
                    if (windowTraces.Any())
                    {
                        analysis.PatternTrends[windowName + "_AvgLatency"] = windowTraces.Average(t => t.ProcessingTimeMs);
                        analysis.PatternTrends[windowName + "_AvgMemory"] = windowTraces.Average(t => t.MemoryUsageMB);
                        analysis.PatternTrends[windowName + "_AvgCpu"] = windowTraces.Average(t => t.CpuUtilization);
                        analysis.PatternTrends[windowName + "_AvgGpu"] = windowTraces.Average(t => t.GpuUtilization);
                        analysis.PatternTrends[windowName + "_Throughput"] = windowTraces.Count / window.TotalHours;
                    }
                }

                // Identify common patterns
                analysis.IdentifiedPatterns = IdentifyCommonPatterns(traces);
                
                // Calculate pattern stability
                analysis.PatternStability = CalculatePatternStability(traces);

                _logger.LogDebug($"Analyzed long-term trends for {traces.Count} traces");
                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing long-term trends");
                return new PatternAnalysis();
            }
        }

        public async Task<ResourceRequirements> AnalyzeResourceRequirements(object request)
        {
            try
            {
                await Task.Delay(1); // Simulate async analysis
                
                var requestType = DetermineRequestType(request);
                var parameters = ExtractParameters(request);
                var complexity = CalculateComplexityScore(parameters);
                
                return EstimateResourceRequirements(parameters, complexity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing resource requirements");
                return CreateFallbackResourceRequirements();
            }
        }

        // Private helper methods
        private string DetermineRequestType(object request)
        {
            if (request == null) return "Unknown";
            
            var requestTypeName = request.GetType().Name;
            
            return requestTypeName switch
            {
                var name when name.Contains("Upscale") => "Upscale",
                var name when name.Contains("Enhance") => "Enhance",
                var name when name.Contains("Safety") => "SafetyCheck",
                var name when name.Contains("Batch") => "BatchProcessing",
                var name when name.Contains("Pipeline") => "Pipeline",
                _ => "General"
            };
        }

        private Dictionary<string, object> ExtractParameters(object request)
        {
            var parameters = new Dictionary<string, object>();
            
            if (request == null) return parameters;
            
            try
            {
                var properties = request.GetType().GetProperties();
                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(request);
                        if (value != null)
                        {
                            parameters[prop.Name] = value;
                        }
                    }
                    catch
                    {
                        // Skip properties that can't be read
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting request parameters");
            }
            
            return parameters;
        }

        private double CalculateComplexityScore(Dictionary<string, object> parameters)
        {
            double complexity = 0.1; // Base complexity
            
            // Increase complexity based on specific parameters
            foreach (var kvp in parameters)
            {
                switch (kvp.Key.ToLower())
                {
                    case "targetwidth":
                    case "targetheight":
                        if (kvp.Value is int size && size > 1024)
                        {
                            complexity += size / 4096.0; // Normalize to max reasonable size
                        }
                        break;
                    
                    case "upscalefactor":
                        if (kvp.Value is double factor)
                        {
                            complexity += Math.Max(0, (factor - 1.0) / 3.0); // Normalize to max factor 4
                        }
                        break;
                    
                    case "qualitylevel":
                        if (kvp.Value is double quality)
                        {
                            complexity += quality / 100.0; // Normalize to percentage
                        }
                        break;
                    
                    case "batchsize":
                        if (kvp.Value is int batchSize)
                        {
                            complexity += Math.Min(0.5, batchSize / 20.0); // Normalize to max batch 20
                        }
                        break;
                }
            }
            
            return Math.Min(1.0, complexity); // Cap at 1.0
        }

        private ResourceRequirements EstimateResourceRequirements(Dictionary<string, object> parameters, double complexity)
        {
            var baseMemory = 512; // Base memory in MB
            var baseCpu = 0.3; // Base CPU utilization
            var baseGpu = 0.4; // Base GPU utilization
            var baseDuration = TimeSpan.FromSeconds(5); // Base duration
            
            // Adjust based on complexity
            var memoryMultiplier = 1.0 + (complexity * 3.0); // Up to 4x memory
            var cpuMultiplier = 1.0 + (complexity * 1.5); // Up to 2.5x CPU
            var gpuMultiplier = 1.0 + (complexity * 2.0); // Up to 3x GPU
            var durationMultiplier = 1.0 + (complexity * 4.0); // Up to 5x duration
            
            // Apply image size adjustments
            if (parameters.ContainsKey("TargetWidth") && parameters.ContainsKey("TargetHeight"))
            {
                var width = Convert.ToInt32(parameters["TargetWidth"]);
                var height = Convert.ToInt32(parameters["TargetHeight"]);
                var pixels = width * height;
                var pixelMultiplier = Math.Max(1.0, pixels / (1024.0 * 1024.0)); // Normalize to 1MP
                
                memoryMultiplier *= pixelMultiplier;
                durationMultiplier *= Math.Sqrt(pixelMultiplier);
            }
            
            return new ResourceRequirements
            {
                EstimatedMemoryMB = (long)(baseMemory * memoryMultiplier),
                EstimatedCpuUtilization = Math.Min(1.0, baseCpu * cpuMultiplier),
                EstimatedGpuUtilization = Math.Min(1.0, baseGpu * gpuMultiplier),
                EstimatedConcurrency = Math.Max(1, (int)(4 * (1.0 - complexity))), // Fewer concurrent for complex operations
                EstimatedDuration = TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds * durationMultiplier)
            };
        }

        private TimeSpan EstimateDuration(string requestType, double complexity, ResourceRequirements resourceRequirements)
        {
            var baseDuration = requestType switch
            {
                "Upscale" => TimeSpan.FromSeconds(8),
                "Enhance" => TimeSpan.FromSeconds(6),
                "SafetyCheck" => TimeSpan.FromSeconds(2),
                "BatchProcessing" => TimeSpan.FromMinutes(2),
                "Pipeline" => TimeSpan.FromMinutes(1),
                _ => TimeSpan.FromSeconds(5)
            };
            
            var complexityMultiplier = 1.0 + (complexity * 2.0);
            var memoryFactor = Math.Max(1.0, resourceRequirements.EstimatedMemoryMB / 1024.0);
            
            return TimeSpan.FromMilliseconds(
                baseDuration.TotalMilliseconds * complexityMultiplier * Math.Sqrt(memoryFactor));
        }

        private async Task RecordPatternObservation(RequestPattern pattern)
        {
            await Task.Delay(1); // Simulate async operation
            
            var patternKey = GeneratePatternKey(pattern);
            
            lock (_lockObject)
            {
                if (!_patternObservations.ContainsKey(patternKey))
                {
                    _patternObservations[patternKey] = new List<PatternObservation>();
                    _patternStats[patternKey] = new PatternStatistics();
                }
            }
        }

        private void UpdatePatternStatistics(string patternKey, PatternObservation observation)
        {
            var stats = _patternStats[patternKey];
            var observations = _patternObservations[patternKey];
            
            stats.TotalObservations = observations.Count;
            stats.SuccessRate = observations.Count(o => o.Success) / (double)observations.Count;
            stats.AverageDuration = TimeSpan.FromMilliseconds(observations.Average(o => o.ActualDuration.TotalMilliseconds));
            stats.AverageMemoryUsage = observations.Average(o => o.ActualMemoryUsage);
            stats.AverageCpuUsage = observations.Average(o => o.ActualCpuUsage);
            stats.AverageGpuUsage = observations.Average(o => o.ActualGpuUsage);
            stats.AverageThroughput = observations.Average(o => o.Throughput);
            stats.LastUpdated = DateTime.UtcNow;
        }

        private List<string> IdentifyCommonPatterns(List<PostprocessingRequestTrace> traces)
        {
            var patterns = new List<string>();
            
            if (!traces.Any()) return patterns;
            
            // Identify peak usage patterns
            var hourlyUsage = traces.GroupBy(t => t.Timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Count());
            
            var peakHour = hourlyUsage.OrderByDescending(h => h.Value).First();
            if (peakHour.Value > hourlyUsage.Values.Average() * 1.5)
            {
                patterns.Add($"Peak usage at hour {peakHour.Key}");
            }
            
            // Identify memory usage patterns
            var avgMemory = traces.Average(t => t.MemoryUsageMB);
            if (avgMemory > 2048)
            {
                patterns.Add("High memory usage pattern");
            }
            
            // Identify request type patterns
            var requestTypes = traces.GroupBy(t => t.RequestType)
                .OrderByDescending(g => g.Count())
                .Take(3);
            
            foreach (var type in requestTypes)
            {
                var percentage = (type.Count() / (double)traces.Count) * 100;
                if (percentage > 30)
                {
                    patterns.Add($"Dominant {type.Key} requests ({percentage:F1}%)");
                }
            }
            
            return patterns;
        }

        private double CalculatePatternStability(List<PostprocessingRequestTrace> traces)
        {
            if (traces.Count < 10) return 0.3; // Low stability for insufficient data
            
            // Calculate coefficient of variation for key metrics
            var latencyCV = CalculateCoefficientOfVariation(traces.Select(t => (double)t.ProcessingTimeMs));
            var memoryCV = CalculateCoefficientOfVariation(traces.Select(t => t.MemoryUsageMB));
            var cpuCV = CalculateCoefficientOfVariation(traces.Select(t => t.CpuUtilization));
            
            // Lower CV means higher stability
            var avgCV = (latencyCV + memoryCV + cpuCV) / 3.0;
            return Math.Max(0.1, Math.Min(1.0, 1.0 - avgCV));
        }

        private double CalculateCoefficientOfVariation(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (!valuesList.Any()) return 1.0;
            
            var mean = valuesList.Average();
            if (mean == 0) return 0.0;
            
            var variance = valuesList.Select(v => Math.Pow(v - mean, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            
            return stdDev / mean;
        }

        private RequestPattern CreateFallbackPattern()
        {
            return new RequestPattern
            {
                RequestType = "General",
                Parameters = new Dictionary<string, object>(),
                ExpectedDuration = TimeSpan.FromSeconds(5),
                ResourceRequirements = CreateFallbackResourceRequirements(),
                ComplexityScore = 0.5,
                Timestamp = DateTime.UtcNow
            };
        }

        private ResourceRequirements CreateFallbackResourceRequirements()
        {
            return new ResourceRequirements
            {
                EstimatedMemoryMB = 512,
                EstimatedCpuUtilization = 0.4,
                EstimatedGpuUtilization = 0.5,
                EstimatedConcurrency = 2,
                EstimatedDuration = TimeSpan.FromSeconds(5)
            };
        }

        private string GeneratePatternKey(PostprocessingPerformanceData data)
        {
            return $"{data.RequestType}_{data.ModelType}_{(data.MemoryUsageMB > 1024 ? "high_memory" : "normal_memory")}";
        }

        private string GeneratePatternKey(RequestPattern pattern)
        {
            return $"{pattern.RequestType}_{(pattern.ResourceRequirements.EstimatedMemoryMB > 1024 ? "high_memory" : "normal_memory")}";
        }
    }

    /// <summary>
    /// Pattern observation data point
    /// </summary>
    public class PatternObservation
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan ActualDuration { get; set; }
        public double ActualMemoryUsage { get; set; }
        public double ActualCpuUsage { get; set; }
        public double ActualGpuUsage { get; set; }
        public bool Success { get; set; }
        public double Throughput { get; set; }
    }

    /// <summary>
    /// Pattern statistics aggregation
    /// </summary>
    public class PatternStatistics
    {
        public int TotalObservations { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public double AverageMemoryUsage { get; set; }
        public double AverageCpuUsage { get; set; }
        public double AverageGpuUsage { get; set; }
        public double AverageThroughput { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
