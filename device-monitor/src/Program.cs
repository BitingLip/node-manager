using System;
using System.Threading.Tasks;
using DeviceMonitorApp.Services;

namespace DeviceMonitorApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Check command line arguments
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "--service":
                        Console.WriteLine("=== Device Monitor Background Service Mode ===");
                        await BackgroundServiceProgram.RunBackgroundServiceAsync();
                        return;
                        
                    case "--setup":
                        Console.WriteLine("=== Database Setup Mode ===");
                        await SetupDatabaseAsync();
                        return;
                        
                    case "--test-database":
                        Console.WriteLine("=== Database Connection Test Mode ===");
                        await DatabaseTestProgram.RunDatabaseTestOnly();
                        return;

                    case "--test-hardware":
                        Console.WriteLine("=== Hardware Monitoring Test Mode ===");
                        await BackgroundServiceProgram.RunHardwareTableMonitoringAsync();
                        return;
                        
                    case "--help":
                        ShowHelp();
                        return;
                        
                    default:
                        Console.WriteLine("Unknown argument. Use --help for usage information.");
                        return;
                }
            }

            // Default: Auto setup and run service (--setup --service)
            Console.WriteLine("=== Device Monitor Auto Setup and Service ===");
            
            // Try setup first
            Console.WriteLine("Step 1: Setting up database...");
            bool setupSuccess = await SetupDatabaseAsync();
            
            if (setupSuccess)
            {
                Console.WriteLine("Step 2: Starting background service...");
                await BackgroundServiceProgram.RunBackgroundServiceAsync();
            }
            else
            {
                Console.WriteLine("Database setup failed. Falling back to hardware-only mode...");
                Console.WriteLine("Step 2: Starting hardware monitoring...");
                await BackgroundServiceProgram.RunHardwareTableMonitoringAsync();
            }
        }

        private static async Task<bool> SetupDatabaseAsync()
        {
            try
            {
                string connectionString = "Host=localhost;Database=biting_lip;Username=postgres;Password=postgres;Port=5432";
                using var dbService = new DatabaseService(connectionString);
                
                Console.WriteLine("Checking database connection and table setup...");
                var initialized = await dbService.InitializeAsync();
                
                if (initialized)
                {
                    Console.WriteLine("Database setup completed successfully.");
                    return true;
                }
                else
                {
                    Console.WriteLine("Database setup failed.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database setup error: {ex.Message}");
                return false;
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Device Monitor Application");
            Console.WriteLine("==========================");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  DeviceMonitor.exe                    - Auto setup database and run service (fallback to hardware table)");
            Console.WriteLine("  DeviceMonitor.exe --setup            - Setup database and create devices table");
            Console.WriteLine("  DeviceMonitor.exe --service          - Run background service with database integration");
            Console.WriteLine("  DeviceMonitor.exe --test-database    - Test database connection and table structure");
            Console.WriteLine("  DeviceMonitor.exe --test-hardware    - Display live hardware metrics table (updates every second)");
            Console.WriteLine("  DeviceMonitor.exe --help             - Show this help information");
            Console.WriteLine();
            Console.WriteLine("Default behavior (no arguments):");
            Console.WriteLine("  1. Attempts database setup (--setup)");
            Console.WriteLine("  2. If successful, runs background service (--service)");
            Console.WriteLine("  3. If database fails, runs hardware metrics table (--test-hardware)");
        }
    }
}