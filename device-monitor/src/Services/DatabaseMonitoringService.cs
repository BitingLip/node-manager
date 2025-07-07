using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeviceMonitorApp.Models;

namespace DeviceMonitorApp.Services
{
    public class DatabaseMonitoringService : IDisposable
    {
        private readonly SystemMonitorService _systemMonitor;
        private readonly DatabaseService _databaseService;
        private Timer? _updateTimer;
        private bool _isRunning = false;
        private readonly int _updateIntervalMs;

        public DatabaseMonitoringService(string connectionString = "Host=localhost;Database=biting_lip;Username=postgres;Password=postgres", int updateIntervalMs = 1000)
        {
            _systemMonitor = new SystemMonitorService();
            _databaseService = new DatabaseService(connectionString);
            _updateIntervalMs = updateIntervalMs;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                // Initialize system monitoring
                _systemMonitor.Initialize();

                // Test database connection
                var dbConnected = await _databaseService.TestConnectionAsync();
                if (!dbConnected)
                {
                    return false;
                }

                // Initialize database
                var dbInitialized = await _databaseService.InitializeAsync();
                if (!dbInitialized)
                {
                    return false;
                }

                // Perform initial device discovery and insert
                await DiscoverAndInsertInitialDevicesAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task DiscoverAndInsertInitialDevicesAsync()
        {
            try
            {
                var devices = await CollectCurrentDeviceDataAsync();
                
                // Set creation time for initial discovery
                foreach (var device in devices)
                {
                    device.TimeCreated = DateTime.UtcNow;
                    device.TimeUpdated = DateTime.UtcNow;
                }

                var success = await _databaseService.UpsertDevicesAsync(devices);
                // Silent operation - no console output
            }
            catch (Exception)
            {
                // Silent operation - no console output
            }
        }

        public void StartMonitoring()
        {
            if (_isRunning)
            {
                return;
            }

            _updateTimer = new Timer(UpdateDevicesCallback, null, 0, _updateIntervalMs);
            _isRunning = true;
        }

        public void StopMonitoring()
        {
            if (!_isRunning)
            {
                return;
            }

            _updateTimer?.Dispose();
            _updateTimer = null;
            _isRunning = false;
        }

        private async void UpdateDevicesCallback(object? state)
        {
            try
            {
                var devices = await CollectCurrentDeviceDataAsync();
                var success = await _databaseService.UpsertDevicesAsync(devices);
                
                // Silent operation - no console output
            }
            catch (Exception)
            {
                // Silent operation - no console output
            }
        }

        private async Task<List<DeviceRecord>> CollectCurrentDeviceDataAsync()
        {
            return await Task.Run(() =>
            {
                var devices = new List<DeviceRecord>();

                try
                {
                    // Get CPU and Memory data
                    var cpuMetrics = _systemMonitor.GetCpuMetrics();
                    var memoryMetrics = _systemMonitor.GetMemoryMetrics();
                    
                    if (!string.IsNullOrEmpty(cpuMetrics.Name))
                    {
                        var cpuDevice = DeviceRecord.FromCpuMetrics(cpuMetrics, memoryMetrics);
                        devices.Add(cpuDevice);
                    }

                    // Get GPU data
                    var gpuMetrics = _systemMonitor.GetGpuMetrics();
                    for (int i = 0; i < gpuMetrics.Count; i++)
                    {
                        var gpuDevice = DeviceRecord.FromGpuMetrics(gpuMetrics[i], i);
                        devices.Add(gpuDevice);
                    }
                }
                catch (Exception)
                {
                    // Silent operation - no console output
                }

                return devices;
            });
        }

        public async Task<List<DeviceRecord>> GetCurrentDevicesFromDatabaseAsync()
        {
            return await _databaseService.GetAllDevicesAsync();
        }

        public async Task<bool> MarkDeviceOfflineAsync(string deviceId, string? reason = null)
        {
            return await _databaseService.MarkDeviceOfflineAsync(deviceId, reason);
        }

        public void PrintCurrentStatus()
        {
            Console.WriteLine($"Database Monitoring Status:");
            Console.WriteLine($"  Running: {(_isRunning ? "Yes" : "No")}");
            Console.WriteLine($"  Update Interval: {_updateIntervalMs}ms");
            Console.WriteLine($"  System Monitor: {(_systemMonitor != null ? "Initialized" : "Not Initialized")}");
        }

        public async Task PrintDevicesSummaryAsync()
        {
            try
            {
                var devices = await GetCurrentDevicesFromDatabaseAsync();
                
                Console.WriteLine($"\nCurrent Devices in Database ({devices.Count} total):");
                Console.WriteLine("─".PadRight(120, '─'));
                Console.WriteLine($"{"Device ID",-12} {"Vendor",-8} {"Name",-25} {"Status",-8} {"CPU/GPU %",-10} {"Memory",-20} {"Last Updated",-20}");
                Console.WriteLine("─".PadRight(120, '─'));

                foreach (var device in devices)
                {
                    var memoryInfo = $"{device.MemoryUsage:F0}/{device.MemoryCapacity} MB";
                    var lastUpdated = device.TimeUpdated.ToString("HH:mm:ss");
                    
                    Console.WriteLine($"{device.DeviceId,-12} {device.DeviceVendor,-8} {device.DeviceName,-25} {device.Status,-8} {device.ProcessingUsage,-9:F1}% {memoryInfo,-20} {lastUpdated,-20}");
                }
                Console.WriteLine("─".PadRight(120, '─'));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve devices summary: {ex.Message}");
            }
        }

        public void Dispose()
        {
            StopMonitoring();
            _systemMonitor?.Dispose();
            _databaseService?.Dispose();
        }
    }
}
