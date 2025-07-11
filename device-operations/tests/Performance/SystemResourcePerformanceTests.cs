using DeviceOperations.Services.Memory;
using DeviceOperations.Services.Device;
using DeviceOperations.Models.Requests;
using DeviceOperations.Models.Responses;
using DeviceOperations.Models.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

namespace DeviceOperations.Tests.Performance;

/// <summary>
/// Performance tests for system resource management under load
/// </summary>
public class SystemResourcePerformanceTests : PerformanceTestBase
{
    [Fact]
    public async Task Memory_Allocation_Performance_HighFrequency()
    {
        // Arrange
        var memoryService = ServiceProvider.GetRequiredService<IServiceMemory>();
        var deviceService = ServiceProvider.GetRequiredService<IServiceDevice>();
        
        var mockAllocateResponse = new PostMemoryAllocateResponse
        {
            AllocationId = Guid.NewGuid().ToString(),
            Success = true
        };

        var mockDeallocateResponse = new DeleteMemoryDeallocateResponse
        {
            Success = true,
            Message = "Memory deallocated successfully"
        };

        SetupMockResponse(mockAllocateResponse, TimeSpan.FromMilliseconds(10));

        // Act & Assert - Test high-frequency allocation/deallocation
        var allocationIds = new List<string>();
        
        var result = await ExecutePerformanceTest<object>(
            "High_Frequency_Memory_Operations",
            async () =>
            {
                // Allocate memory
                var allocateRequest = new PostMemoryAllocateRequest
                {
                    SizeBytes = 1024 * 1024 * 100, // 100MB
                    MemoryType = "GPU"
                };

                var allocateResult = await memoryService.PostMemoryAllocateAsync(allocateRequest);
                
                if (allocateResult.Success && allocateResult.Data != null)
                {
                    allocationIds.Add(allocateResult.Data.AllocationId);
                }

                // Simulate some work
                await Task.Delay(1);

                // Deallocate if we have allocations
                if (allocationIds.Count > 0)
                {
                    var idToRemove = allocationIds.First();
                    allocationIds.RemoveAt(0);
                    
                    var deallocateRequest = new DeleteMemoryDeallocateRequest
                    {
                        AllocationId = idToRemove
                    };

                    await memoryService.DeleteMemoryDeallocateAsync(deallocateRequest);
                }
                
                return new object();
            },
            iterations: 200,
            maxAcceptableTime: TimeSpan.FromMilliseconds(50)
        );

        AssertPerformance(result, "for high-frequency memory operations");
    }

    [Fact]
    public async Task Device_Status_Monitoring_Performance()
    {
        // Arrange
        var deviceService = ServiceProvider.GetRequiredService<IServiceDevice>();
        
        var mockDevicesResponse = new GetDeviceListResponse
        {
            Devices = Enumerable.Range(0, 8).Select(i => new DeviceInfo
            {
                Id = $"gpu-{i}",
                Name = $"NVIDIA RTX 4090 #{i}",
                Type = DeviceType.GPU,
                Status = i % 4 == 0 ? DeviceStatus.Busy : DeviceStatus.Available,
                Specifications = new DeviceSpecifications
                {
                    TotalMemoryBytes = 24 * 1024 * 1024 * 1024L, // 24GB
                    AvailableMemoryBytes = (long)(24 * 1024 * 1024 * 1024L * (0.9 - (i * 0.1)))
                },
                Utilization = new DeviceUtilization
                {
                    CpuUtilization = 10 + (i * 10),
                    MemoryUtilization = 10 + (i * 10),
                    GpuUtilization = 10 + (i * 10)
                }
            }).ToList()
        };

        var mockStatusResponse = new GetDeviceStatusResponse
        {
            DeviceId = "gpu-0",
            Status = DeviceStatus.Available,
            StatusDescription = "Device is available",
            Utilization = new DeviceUtilization
            {
                CpuUtilization = 45.5,
                MemoryUtilization = 68.2,
                GpuUtilization = 85.0
            },
            Performance = new DevicePerformanceMetrics
            {
                OperationsPerSecond = 100.0,
                AverageOperationTime = 10.0,
                ThroughputMBps = 1000.0,
                ErrorRate = 0.0,
                UptimePercentage = 99.9,
                PerformanceScore = 95.0
            }
        };

        SetupMockResponse(mockDevicesResponse, TimeSpan.FromMilliseconds(15));

        // Act & Assert - Test rapid device monitoring
        var result = await ExecutePerformanceTest<object>(
            "Device_Status_Monitoring",
            async () =>
            {
                // Get all devices
                await deviceService.GetDeviceListAsync();
                
                // Check status of multiple devices rapidly
                var statusTasks = new List<Task>();
                for (int i = 0; i < 4; i++)
                {
                    statusTasks.Add(deviceService.GetDeviceStatusAsync($"gpu-{i}"));
                }
                
                await Task.WhenAll(statusTasks);
                return new object();
            },
            iterations: 100,
            maxAcceptableTime: TimeSpan.FromMilliseconds(100)
        );

        AssertPerformance(result, "for device status monitoring");
    }

    [Fact]
    public async Task Memory_Fragmentation_Stress_Test()
    {
        // Arrange
        var memoryService = ServiceProvider.GetRequiredService<IServiceMemory>();
        var allocations = new List<string>();
        var random = new Random(42); // Fixed seed for reproducibility

        var mockResponse = new PostMemoryAllocateResponse
        {
            AllocationId = Guid.NewGuid().ToString(),
            Success = true
        };

        SetupMockResponse(mockResponse, TimeSpan.FromMilliseconds(5));

        // Act - Create fragmentation pattern
        var result = await ExecutePerformanceTest<object>(
            "Memory_Fragmentation_Stress",
            async () =>
            {
                // Randomly allocate and deallocate different sizes
                if (random.NextDouble() < 0.7) // 70% chance to allocate
                {
                    var sizes = new[] { 64, 128, 256, 512, 1024 }; // MB
                    var size = sizes[random.Next(sizes.Length)] * 1024 * 1024;
                    
                    var request = new PostMemoryAllocateRequest
                    {
                        SizeBytes = size,
                        MemoryType = "GPU"
                    };

                    var allocResult = await memoryService.PostMemoryAllocateAsync(request);
                    if (allocResult.Success && allocResult.Data != null)
                    {
                        allocations.Add(allocResult.Data.AllocationId);
                    }
                }
                else if (allocations.Count > 0) // 30% chance to deallocate
                {
                    var indexToRemove = random.Next(allocations.Count);
                    var allocationId = allocations[indexToRemove];
                    allocations.RemoveAt(indexToRemove);

                    var deallocRequest = new DeleteMemoryDeallocateRequest
                    {
                        AllocationId = allocationId
                    };

                    await memoryService.DeleteMemoryDeallocateAsync(deallocRequest);
                }
                
                return new object();
            },
            iterations: 500,
            maxAcceptableTime: TimeSpan.FromMilliseconds(30)
        );

        AssertPerformance(result, "for memory fragmentation patterns");

        // Verify we don't have excessive leftover allocations
        allocations.Count.Should().BeLessThan(100, "should not accumulate too many allocations");
    }

    [Fact]
    public async Task System_Under_Heavy_Load_Performance()
    {
        // Arrange
        var memoryService = ServiceProvider.GetRequiredService<IServiceMemory>();
        var deviceService = ServiceProvider.GetRequiredService<IServiceDevice>();

        var mockMemoryResponse = new GetMemoryStatusResponse
        {
            MemoryStatus = new Dictionary<string, object>
            {
                { "TotalMemoryBytes", 128L * 1024 * 1024 * 1024 }, // 128GB
                { "UsedMemoryBytes", 96L * 1024 * 1024 * 1024 },   // 96GB (75% used)
                { "AvailableMemoryBytes", 32L * 1024 * 1024 * 1024 }, // 32GB
                { "UtilizationPercentage", 75.0 },
                { "AllocationCount", 45 },
                { "FragmentationLevel", 0.15 }
            }
        };

        var mockDeviceResponse = new GetDeviceListResponse
        {
            Devices = new List<DeviceInfo>
            {
                new DeviceInfo
                {
                    Id = "gpu-0",
                    Status = DeviceStatus.Busy,
                    Utilization = new DeviceUtilization
                    {
                        CpuUtilization = 95,
                        MemoryUtilization = 83,
                        GpuUtilization = 95
                    }
                }
            }
        };

        SetupMockResponse(mockMemoryResponse, TimeSpan.FromMilliseconds(50));

        // Act - Simulate system under heavy load
        var concurrentOperations = 20;
        var tasks = new List<Task>();

        for (int i = 0; i < concurrentOperations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var result = await ExecutePerformanceTest<object>(
                    $"Heavy_Load_Operation_{i}",
                    async () =>
                    {
                        // Simulate multiple operations happening simultaneously
                        var operations = new Task[]
                        {
                            memoryService.GetMemoryStatusAsync(),
                            deviceService.GetDeviceListAsync(),
                            memoryService.GetMemoryStatusAsync(), // Duplicate calls simulate real load
                            deviceService.GetDeviceListAsync()
                        };

                        await Task.WhenAll(operations);
                        return new object();
                    },
                    iterations: 25,
                    maxAcceptableTime: TimeSpan.FromMilliseconds(200)
                );

                // Each concurrent operation should still perform reasonably
                result.SuccessfulIterations.Should().BeGreaterThan(20);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert system remains responsive under load
        // (Individual assertions happen in the task bodies above)
        Console.WriteLine($"Successfully completed {concurrentOperations} concurrent load operations");
    }

    [Fact]
    public async Task Resource_Cleanup_Performance()
    {
        // Arrange
        var memoryService = ServiceProvider.GetRequiredService<IServiceMemory>();
        var allocatedResources = new List<string>();

        // First, create many allocations
        var mockAllocateResponse = new PostMemoryAllocateResponse
        {
            AllocationId = Guid.NewGuid().ToString(),
            Success = true
        };

        var mockDeallocateResponse = new DeleteMemoryDeallocateResponse
        {
            Success = true,
            Message = "Deallocated successfully"
        };

        SetupMockResponse(mockAllocateResponse, TimeSpan.FromMilliseconds(5));

        // Create resources to clean up
        for (int i = 0; i < 100; i++)
        {
            allocatedResources.Add(Guid.NewGuid().ToString());
        }

        // Act & Assert - Test batch cleanup performance
        var result = await ExecutePerformanceTest<object>(
            "Resource_Cleanup_Batch",
            async () =>
            {
                // Clean up resources in batches
                var batchSize = 10;
                var batch = allocatedResources.Take(batchSize).ToList();
                
                var cleanupTasks = batch.Select(async allocationId =>
                {
                    var request = new DeleteMemoryDeallocateRequest
                    {
                        AllocationId = allocationId
                    };
                    
                    return await memoryService.DeleteMemoryDeallocateAsync(request);
                });

                await Task.WhenAll(cleanupTasks);
                
                // Remove processed items
                foreach (var id in batch)
                {
                    allocatedResources.Remove(id);
                }
                return new object();
            },
            iterations: 10,
            maxAcceptableTime: TimeSpan.FromMilliseconds(100)
        );

        AssertPerformance(result, "for batch resource cleanup");
    }
}
