using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using DeviceMonitorApp.Models;

namespace DeviceMonitorApp.Services
{
    public class DatabaseService : IDisposable
    {
        private readonly string _connectionString;
        private NpgsqlConnection? _connection;
        private bool _isInitialized = false;

        public DatabaseService(string connectionString = "Host=localhost;Database=biting_lip;Username=postgres;Password=postgres")
        {
            _connectionString = connectionString;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                _connection = new NpgsqlConnection(_connectionString);
                await _connection.OpenAsync();
                
                // Test connection and verify database access
                await TestDatabaseAccessAsync();
                
                // Create or verify the devices table
                await CreateDevicesTableAsync();
                
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database initialization failed: {ex.Message}");
                return false;
            }
        }

        private async Task TestDatabaseAccessAsync()
        {
            try
            {
                using var command = new NpgsqlCommand("SELECT version();", _connection);
                var version = await command.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Database connection test failed: {ex.Message}");
            }
        }

        private async Task CreateDevicesTableAsync()
        {
            try
            {
                // Check if table exists
                const string checkTableSql = @"
                    SELECT EXISTS (
                        SELECT FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                        AND table_name = 'devices'
                    );";
                
                using var checkCommand = new NpgsqlCommand(checkTableSql, _connection);
                var tableExists = (bool)(await checkCommand.ExecuteScalarAsync() ?? false);
                
                if (!tableExists)
                {
                    
                    const string createTableSql = @"
                        CREATE TABLE devices (
                            device_id VARCHAR(50) PRIMARY KEY,
                            device_vendor VARCHAR(50) NOT NULL,
                            device_name VARCHAR(255) NOT NULL,
                            status VARCHAR(20) NOT NULL DEFAULT 'online',
                            status_message TEXT,
                            memory_capacity INTEGER NOT NULL,
                            memory_usage REAL NOT NULL,
                            processing_usage REAL NOT NULL,
                            time_created TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            time_updated TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP
                        );

                        CREATE INDEX IF NOT EXISTS idx_devices_status ON devices(status);
                        CREATE INDEX IF NOT EXISTS idx_devices_time_updated ON devices(time_updated);
                        
                        COMMENT ON TABLE devices IS 'Hardware device monitoring data';
                        COMMENT ON COLUMN devices.device_id IS 'Unique identifier for the device (e.g., cpu_0, gpu_0)';
                        COMMENT ON COLUMN devices.memory_capacity IS 'Total memory capacity in MB';
                        COMMENT ON COLUMN devices.memory_usage IS 'Current memory usage in MB';
                        COMMENT ON COLUMN devices.processing_usage IS 'Current processing load percentage';
                    ";

                    using var command = new NpgsqlCommand(createTableSql, _connection);
                    await command.ExecuteNonQueryAsync();
                }
                
                // Verify table structure
                await VerifyTableStructureAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create/verify devices table: {ex.Message}");
            }
        }

        private async Task VerifyTableStructureAsync()
        {
            try
            {
                const string verifySql = @"
                    SELECT column_name, data_type, is_nullable, column_default
                    FROM information_schema.columns 
                    WHERE table_name = 'devices' 
                    ORDER BY ordinal_position;";
                
                using var command = new NpgsqlCommand(verifySql, _connection);
                using var reader = await command.ExecuteReaderAsync();
                
                var columnCount = 0;
                while (await reader.ReadAsync())
                {
                    columnCount++;
                }
                
                if (columnCount != 10)
                {
                    throw new Exception($"Table structure mismatch: expected 10 columns, found {columnCount}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to verify table structure: {ex.Message}");
            }
        }

        public async Task<bool> UpsertDeviceAsync(DeviceRecord device)
        {
            if (!_isInitialized || _connection == null)
            {
                Console.WriteLine("Database not initialized");
                return false;
            }

            try
            {
                const string upsertSql = @"
                    INSERT INTO devices (
                        device_id, device_vendor, device_name, status, status_message,
                        memory_capacity, memory_usage, processing_usage, time_created, time_updated
                    ) VALUES (
                        @device_id, @device_vendor, @device_name, @status, @status_message,
                        @memory_capacity, @memory_usage, @processing_usage, @time_created, @time_updated
                    )
                    ON CONFLICT (device_id) DO UPDATE SET
                        device_vendor = EXCLUDED.device_vendor,
                        device_name = EXCLUDED.device_name,
                        status = EXCLUDED.status,
                        status_message = EXCLUDED.status_message,
                        memory_capacity = EXCLUDED.memory_capacity,
                        memory_usage = EXCLUDED.memory_usage,
                        processing_usage = EXCLUDED.processing_usage,
                        time_updated = EXCLUDED.time_updated;
                ";

                using var command = new NpgsqlCommand(upsertSql, _connection);
                
                command.Parameters.AddWithValue("@device_id", device.DeviceId);
                command.Parameters.AddWithValue("@device_vendor", device.DeviceVendor);
                command.Parameters.AddWithValue("@device_name", device.DeviceName);
                command.Parameters.AddWithValue("@status", device.Status);
                command.Parameters.AddWithValue("@status_message", (object?)device.StatusMessage ?? DBNull.Value);
                command.Parameters.AddWithValue("@memory_capacity", device.MemoryCapacity);
                command.Parameters.AddWithValue("@memory_usage", device.MemoryUsage);
                command.Parameters.AddWithValue("@processing_usage", device.ProcessingUsage);
                command.Parameters.AddWithValue("@time_created", device.TimeCreated);
                command.Parameters.AddWithValue("@time_updated", device.TimeUpdated);

                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to upsert device {device.DeviceId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpsertDevicesAsync(List<DeviceRecord> devices)
        {
            if (!_isInitialized || _connection == null)
            {
                Console.WriteLine("Database not initialized");
                return false;
            }

            using var transaction = await _connection.BeginTransactionAsync();
            
            try
            {
                foreach (var device in devices)
                {
                    const string upsertSql = @"
                        INSERT INTO devices (
                            device_id, device_vendor, device_name, status, status_message,
                            memory_capacity, memory_usage, processing_usage, time_created, time_updated
                        ) VALUES (
                            @device_id, @device_vendor, @device_name, @status, @status_message,
                            @memory_capacity, @memory_usage, @processing_usage, @time_created, @time_updated
                        )
                        ON CONFLICT (device_id) DO UPDATE SET
                            device_vendor = EXCLUDED.device_vendor,
                            device_name = EXCLUDED.device_name,
                            status = EXCLUDED.status,
                            status_message = EXCLUDED.status_message,
                            memory_capacity = EXCLUDED.memory_capacity,
                            memory_usage = EXCLUDED.memory_usage,
                            processing_usage = EXCLUDED.processing_usage,
                            time_updated = EXCLUDED.time_updated;
                    ";

                    using var command = new NpgsqlCommand(upsertSql, _connection, transaction);
                    
                    command.Parameters.AddWithValue("@device_id", device.DeviceId);
                    command.Parameters.AddWithValue("@device_vendor", device.DeviceVendor);
                    command.Parameters.AddWithValue("@device_name", device.DeviceName);
                    command.Parameters.AddWithValue("@status", device.Status);
                    command.Parameters.AddWithValue("@status_message", (object?)device.StatusMessage ?? DBNull.Value);
                    command.Parameters.AddWithValue("@memory_capacity", device.MemoryCapacity);
                    command.Parameters.AddWithValue("@memory_usage", device.MemoryUsage);
                    command.Parameters.AddWithValue("@processing_usage", device.ProcessingUsage);
                    command.Parameters.AddWithValue("@time_created", device.TimeCreated);
                    command.Parameters.AddWithValue("@time_updated", device.TimeUpdated);

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Failed to batch upsert devices: {ex.Message}");
                return false;
            }
        }

        public async Task<List<DeviceRecord>> GetAllDevicesAsync()
        {
            var devices = new List<DeviceRecord>();
            
            if (!_isInitialized || _connection == null)
            {
                Console.WriteLine("Database not initialized");
                return devices;
            }

            try
            {
                const string selectSql = @"
                    SELECT device_id, device_vendor, device_name, status, status_message,
                           memory_capacity, memory_usage, processing_usage, time_created, time_updated
                    FROM devices
                    ORDER BY device_id;
                ";

                using var command = new NpgsqlCommand(selectSql, _connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    devices.Add(new DeviceRecord
                    {
                        DeviceId = reader.GetString("device_id"),
                        DeviceVendor = reader.GetString("device_vendor"),
                        DeviceName = reader.GetString("device_name"),
                        Status = reader.GetString("status"),
                        StatusMessage = reader.IsDBNull("status_message") ? null : reader.GetString("status_message"),
                        MemoryCapacity = reader.GetInt32("memory_capacity"),
                        MemoryUsage = reader.GetFloat("memory_usage"),
                        ProcessingUsage = reader.GetFloat("processing_usage"),
                        TimeCreated = reader.GetDateTime("time_created"),
                        TimeUpdated = reader.GetDateTime("time_updated")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve devices: {ex.Message}");
            }

            return devices;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var testConnection = new NpgsqlConnection(_connectionString);
                await testConnection.OpenAsync();
                
                using var command = new NpgsqlCommand("SELECT 1", testConnection);
                var result = await command.ExecuteScalarAsync();
                
                Console.WriteLine($"Database connection test successful: {result}");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> MarkDeviceOfflineAsync(string deviceId, string? statusMessage = null)
        {
            if (!_isInitialized || _connection == null)
            {
                return false;
            }

            try
            {
                const string updateSql = @"
                    UPDATE devices 
                    SET status = 'offline', 
                        status_message = @status_message,
                        time_updated = @time_updated
                    WHERE device_id = @device_id;
                ";

                using var command = new NpgsqlCommand(updateSql, _connection);
                command.Parameters.AddWithValue("@device_id", deviceId);
                command.Parameters.AddWithValue("@status_message", (object?)statusMessage ?? DBNull.Value);
                command.Parameters.AddWithValue("@time_updated", DateTime.UtcNow);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to mark device {deviceId} offline: {ex.Message}");
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetDatabaseStatsAsync()
        {
            var stats = new Dictionary<string, object>();
            
            if (!_isInitialized || _connection == null)
            {
                stats["error"] = "Database not initialized";
                return stats;
            }

            try
            {
                // Get total device count
                using (var command = new NpgsqlCommand("SELECT COUNT(*) FROM devices;", _connection))
                {
                    stats["total_devices"] = await command.ExecuteScalarAsync() ?? 0;
                }

                // Get online device count
                using (var command = new NpgsqlCommand("SELECT COUNT(*) FROM devices WHERE status = 'online';", _connection))
                {
                    stats["online_devices"] = await command.ExecuteScalarAsync() ?? 0;
                }

                // Get device types
                using (var command = new NpgsqlCommand("SELECT device_id FROM devices ORDER BY device_id;", _connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    var devices = new List<string>();
                    while (await reader.ReadAsync())
                    {
                        devices.Add(reader.GetString("device_id"));
                    }
                    stats["device_list"] = devices;
                }

                // Get last update time
                using (var command = new NpgsqlCommand("SELECT MAX(time_updated) FROM devices;", _connection))
                {
                    var lastUpdate = await command.ExecuteScalarAsync();
                    stats["last_update"] = lastUpdate ?? "Never";
                }

                stats["status"] = "Connected";
                stats["database"] = "biting_lip";
            }
            catch (Exception ex)
            {
                stats["error"] = ex.Message;
            }

            return stats;
        }

        public async Task<bool> DeleteDeviceAsync(string deviceId)
        {
            if (!_isInitialized || _connection == null)
            {
                Console.WriteLine("Database not initialized");
                return false;
            }

            try
            {
                const string deleteSql = "DELETE FROM devices WHERE device_id = @device_id;";
                using var command = new NpgsqlCommand(deleteSql, _connection);
                command.Parameters.AddWithValue("@device_id", deviceId);
                
                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected > 0)
                {
                    Console.WriteLine($"Test device '{deviceId}' deleted successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Device '{deviceId}' not found in database");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete device {deviceId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ClearAllDevicesAsync()
        {
            if (!_isInitialized || _connection == null)
            {
                Console.WriteLine("Database not initialized");
                return false;
            }

            try
            {
                const string deleteSql = "DELETE FROM devices;";
                using var command = new NpgsqlCommand(deleteSql, _connection);
                var rowsAffected = await command.ExecuteNonQueryAsync();
                Console.WriteLine($"Cleared {rowsAffected} devices from database");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clear devices: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
            _isInitialized = false;
        }
    }
}
