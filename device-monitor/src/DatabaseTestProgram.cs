using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeviceMonitorApp.Services;
using DeviceMonitorApp.Models;

namespace DeviceMonitorApp
{
    public class DatabaseTestProgram
    {
        public static async Task RunDatabaseMonitoringTestAsync()
        {
            Console.WriteLine("=== Database Integration Test ===");

            // You can modify this connection string for your PostgreSQL setup
            string connectionString = "Host=localhost;Database=biting_lip;Username=postgres;Password=postgres;Port=5432";
            
            using var dbMonitor = new DatabaseMonitoringService(connectionString, updateIntervalMs: 1000);

            try
            {
                // Initialize the service
                var initialized = await dbMonitor.InitializeAsync();
                if (!initialized)
                {
                    Console.WriteLine("Failed to initialize database monitoring service");
                    Console.WriteLine("Make sure PostgreSQL is running and the connection string is correct");
                    Console.WriteLine($"Current connection: {connectionString}");
                    return;
                }

                // Print initial status
                dbMonitor.PrintCurrentStatus();
                
                // Start monitoring
                dbMonitor.StartMonitoring();

                Console.WriteLine("\nRunning database monitoring (Press Ctrl+C to stop)...");
                Console.WriteLine("Devices table will be updated every second with current hardware metrics\n");

                // Run monitoring loop with periodic summaries
                var startTime = DateTime.Now;
                int summaryCount = 0;

                while (true)
                {
                    await Task.Delay(5000); // Wait 5 seconds between summaries
                    
                    Console.Clear();
                    Console.WriteLine("=== Database Integration Test ===");
                    Console.WriteLine($"Runtime: {DateTime.Now - startTime:hh\\:mm\\:ss}");
                    Console.WriteLine($"Database updates: ~{summaryCount * 5} seconds of monitoring\n");

                    // Show current devices from database
                    await dbMonitor.PrintDevicesSummaryAsync();
                    
                    Console.WriteLine($"\nNext update in 5 seconds... (Ctrl+C to stop)");
                    summaryCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during database monitoring: {ex.Message}");
                
                if (ex.Message.Contains("password authentication failed") || 
                    ex.Message.Contains("connection refused") ||
                    ex.Message.Contains("database") && ex.Message.Contains("does not exist"))
                {
                    Console.WriteLine("\nDatabase Connection Troubleshooting:");
                    Console.WriteLine("  1. Ensure PostgreSQL is running");
                    Console.WriteLine("  2. Verify the database 'biting_lip' exists");
                    Console.WriteLine("  3. Check username/password credentials");
                    Console.WriteLine("  4. Confirm the connection string is correct");
                    Console.WriteLine($"  5. Current connection: {connectionString}");
                }
            }
            finally
            {
                dbMonitor.StopMonitoring();
                Console.WriteLine("\nDatabase monitoring stopped");
            }
        }

        public static async Task RunDatabaseTestOnly()
        {
            Console.WriteLine("=== Database Connection Test ===");

            string connectionString = "Host=localhost;Database=biting_lip;Username=postgres;Password=postgres;Port=5432";
            
            using var dbService = new DatabaseService(connectionString);

            Console.WriteLine("\nStep 1: Testing database connection and table creation...");
            var initialized = await dbService.InitializeAsync();
            if (!initialized)
            {
                Console.WriteLine("Database initialization failed. Please check:");
                Console.WriteLine("   1. PostgreSQL is running");
                Console.WriteLine("   2. Database 'biting_lip' exists");
                Console.WriteLine("   3. Username/password are correct");
                Console.WriteLine("   4. Connection string is valid");
                return;
            }

            Console.WriteLine("\nStep 2: Getting database statistics...");
            var stats = await dbService.GetDatabaseStatsAsync();
            foreach (var stat in stats)
            {
                if (stat.Key == "device_list" && stat.Value is List<string> devices)
                {
                    Console.WriteLine($"   {stat.Key}: [{string.Join(", ", devices)}]");
                }
                else
                {
                    Console.WriteLine($"   {stat.Key}: {stat.Value}");
                }
            }

            Console.WriteLine("\nStep 3: Testing device insertion...");
            var testDevice = new DeviceRecord
            {
                DeviceId = "test_cpu_0",
                DeviceVendor = "TEST",
                DeviceName = "Test CPU Device for Table Validation",
                Status = "online",
                StatusMessage = "Automated test device",
                MemoryCapacity = 32768,
                MemoryUsage = 8192.5f,
                ProcessingUsage = 25.7f,
                TimeCreated = DateTime.UtcNow,
                TimeUpdated = DateTime.UtcNow
            };

            var insertSuccess = await dbService.UpsertDeviceAsync(testDevice);
            if (insertSuccess)
            {
                Console.WriteLine("Test device inserted successfully");
            }
            else
            {
                Console.WriteLine("Failed to insert test device");
                return;
            }

            Console.WriteLine("\nStep 4: Retrieving all devices...");
            var allDevices = await dbService.GetAllDevicesAsync();
            Console.WriteLine($"   Found {allDevices.Count} device(s) in database:");
            
            foreach (var device in allDevices)
            {
                Console.WriteLine($"   {device.DeviceId}: {device.DeviceName} ({device.Status})");
                Console.WriteLine($"      Memory: {device.MemoryUsage:F1}MB / {device.MemoryCapacity}MB");
                Console.WriteLine($"      Load: {device.ProcessingUsage:F1}%");
                Console.WriteLine($"      Updated: {device.TimeUpdated:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();
            }

            Console.WriteLine("\nStep 5: Cleaning up test data...");
            var deleteSuccess = await dbService.DeleteDeviceAsync("test_cpu_0");
            if (deleteSuccess)
            {
                Console.WriteLine("Test device cleaned up successfully");
            }
            else
            {
                Console.WriteLine("Warning: Failed to clean up test device");
            }
            
            Console.WriteLine("\nDatabase connection, table creation, and operations test completed successfully!");
            Console.WriteLine("The application can now safely use the database for device monitoring.");
            Console.WriteLine("You can now run: dotnet run -- --database");
        }
    }
}
