using System;
using System.Threading;
using System.Threading.Tasks;
using DeviceMonitorApp.Services;

namespace DeviceMonitorApp
{
    public class BackgroundServiceProgram
    {
        private static SystemMonitorService? _monitor;
        private static DatabaseMonitoringService? _databaseService;
        private static CancellationTokenSource? _cancellationTokenSource;
        private static bool _isRunning = false;

        public static async Task RunBackgroundServiceAsync()
        {
            Console.WriteLine("Starting Device Monitor Background Service...");
            
            _cancellationTokenSource = new CancellationTokenSource();
            _monitor = new SystemMonitorService();
            
            try
            {
                // Initialize hardware monitoring
                _monitor.Initialize();
                
                // Initialize database service
                _databaseService = new DatabaseMonitoringService();
                
                // Initialize the database connection and tables
                var initialized = await _databaseService.InitializeAsync();
                if (!initialized)
                {
                    Console.WriteLine("Failed to initialize database.");
                    return;
                }
                
                // Start the monitoring
                _databaseService.StartMonitoring();
                
                _isRunning = true;
                Console.WriteLine("Background service running. Press Ctrl+C to stop.");
                
                // Set up Ctrl+C handler
                Console.CancelKeyPress += OnCancelKeyPress;
                
                // Keep service running
                await Task.Delay(-1, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Service shutdown requested.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running background service: {ex.Message}");
            }
            finally
            {
                await StopServiceAsync();
            }
        }

        public static async Task RunHardwareTableMonitoringAsync()
        {
            Console.WriteLine("Starting Hardware Metrics Table Monitor...");
            Console.WriteLine("Press Ctrl+C to stop.");
            Console.WriteLine();
            
            _cancellationTokenSource = new CancellationTokenSource();
            _monitor = new SystemMonitorService();
            
            try
            {
                // Initialize hardware monitoring
                _monitor.Initialize();
                
                _isRunning = true;
                
                // Set up Ctrl+C handler
                Console.CancelKeyPress += OnCancelKeyPress;
                
                // Monitoring loop with table output
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Get current metrics
                    var gpus = _monitor.GetGpuMetrics();
                    var cpu = _monitor.GetCpuMetrics();
                    var memory = _monitor.GetMemoryMetrics();
                    
                    // Clear console and redraw table completely
                    Console.Clear();
                    
                    // Print current timestamp
                    Console.WriteLine($"Hardware Metrics Table - {DateTime.Now:HH:mm:ss}");
                    Console.WriteLine("Press Ctrl+C to stop.");
                    Console.WriteLine();
                    
                    // Print table header
                    PrintTableHeader();
                    
                    // Print CPU metrics
                    PrintDeviceRow("cpu_0", "CPU", cpu.Name, 
                        $"{cpu.Usage:F1}%", 
                        $"{memory.UsedGB:F1}GB/{memory.TotalGB:F1}GB",
                        $"{cpu.Temperature:F1}°C",
                        $"{cpu.PowerConsumption:F1}W");
                    
                    // Print GPU metrics
                    for (int i = 0; i < gpus.Count; i++)
                    {
                        var gpu = gpus[i];
                        string status = gpu.UtilizationPercentage > 10 ? "MINING" : "IDLE";
                        PrintDeviceRow($"gpu_{i}", "GPU", gpu.GpuName,
                            $"{gpu.UtilizationPercentage:F1}%",
                            $"{gpu.MemoryUsage / (1024 * 1024):F0}MB/{gpu.MemoryTotal / (1024 * 1024):F0}MB",
                            $"{gpu.Temperature:F1}°C",
                            $"{gpu.PowerUsage:F1}W",
                            status);
                    }
                    
                    // Print table footer
                    Console.WriteLine("└────────────┴──────┴─────────────────────────────┴─────────┴─────────────────────┴─────────────┴──────────┴─────────┘");
                    
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nMonitoring stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during hardware monitoring: {ex.Message}");
            }
            finally
            {
                await StopServiceAsync();
            }
        }

        private static void PrintTableHeader()
        {
            Console.WriteLine("┌────────────┬──────┬─────────────────────────────┬─────────┬─────────────────────┬─────────────┬──────────┬─────────┐");
            Console.WriteLine("│ Device ID  │ Type │ Name                        │ Load    │ Memory              │ Temperature │ Power    │ Status  │");
            Console.WriteLine("├────────────┼──────┼─────────────────────────────┼─────────┼─────────────────────┼─────────────┼──────────┼─────────┤");
        }

        private static void PrintDeviceRow(string deviceId, string type, string name, string load, string memory, string temp, string power, string status = "ONLINE")
        {
            // Truncate long names
            if (name.Length > 27) name = name.Substring(0, 24) + "...";
            
            Console.WriteLine($"│ {deviceId,-10} │ {type,-4} │ {name,-27} │ {load,-7} │ {memory,-19} │ {temp,-11} │ {power,-8} │ {status,-7} │");
        }

        public static async Task RunBackgroundServiceWithoutDatabaseAsync()
        {
            Console.WriteLine("Starting Device Monitor Background Service (Hardware only)...");
            
            _cancellationTokenSource = new CancellationTokenSource();
            _monitor = new SystemMonitorService();
            
            try
            {
                // Initialize hardware monitoring
                _monitor.Initialize();
                
                _isRunning = true;
                Console.WriteLine("Background service running. Press Ctrl+C to stop.");
                
                // Set up Ctrl+C handler
                Console.CancelKeyPress += OnCancelKeyPress;
                
                // Simple monitoring loop for hardware only mode
                int loopCount = 0;
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token); // Wait 1 second
                    
                    // Get metrics every second for internal processing
                    var gpus = _monitor.GetGpuMetrics();
                    var cpu = _monitor.GetCpuMetrics();
                    
                    // Only log every 10 seconds to avoid console spam
                    loopCount++;
                    if (loopCount >= 10)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Monitoring: {gpus.Count} GPUs, CPU: {cpu.Usage:F1}%");
                        loopCount = 0;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Service shutdown requested.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running background service: {ex.Message}");
            }
            finally
            {
                await StopServiceAsync();
            }
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            Console.WriteLine("\nShutdown signal received. Stopping service gracefully...");
            _cancellationTokenSource?.Cancel();
        }

        private static Task StopServiceAsync()
        {
            if (_isRunning)
            {
                // Stop database monitoring
                if (_databaseService != null)
                {
                    _databaseService.StopMonitoring();
                    _databaseService.Dispose();
                }
                
                // Dispose hardware monitoring
                _monitor?.Dispose();
                
                _isRunning = false;
            }
            
            return Task.CompletedTask;
        }
    }
}
