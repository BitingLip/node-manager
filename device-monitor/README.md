# Device Monitor Background Service

This project is a Windows console application designed to monitor hardware metrics of AMD/NVIDIA GPUs and CPUs using LibreHardwareMonitor. The application collects various metrics such as GPU utilization, memory usage, temperature, and power consumption, providing real-time hardware monitoring with PostgreSQL database integration for data persistence.

## Features

- **Real-time Hardware Monitoring**: 1-second update intervals for GPU and CPU metrics
- **Database Integration**: PostgreSQL storage for historical data and analytics
- **Mining Detection**: Automatic detection of active mining GPUs
- **Background Service**: Silent operation suitable for server environments
- **Multi-GPU Support**: Monitors up to 5+ GPUs simultaneously
- **Cross-platform Monitoring**: AMD and NVIDIA GPU support via LibreHardwareMonitor

## Project Structure

The project is organized into the following directories and files:

- **src/**: Contains the source code for the application.
  - **Program.cs**: Entry point with command-line interface for different operation modes.
  - **DatabaseTestProgram.cs**: Database testing and validation utilities.
  - **BackgroundServiceProgram.cs**: Main background service implementation with graceful shutdown.
  - **Models/**: Contains data models used in the application.
    - **EnhancedGpuMetrics.cs**: Comprehensive GPU metrics including temperature, power, and clocks.
    - **CpuMetrics.cs**: CPU performance metrics including load, temperature, and power.
    - **MemoryMetrics.cs**: System memory usage metrics.
    - **DeviceRecord.cs**: Database entity for device storage and tracking.
  - **Services/**: Contains services for monitoring and data management.
    - **SystemMonitorService.cs**: Core hardware monitoring using LibreHardwareMonitor.
    - **DatabaseService.cs**: PostgreSQL database operations and connection management.
    - **DatabaseMonitoringService.cs**: Coordinates hardware monitoring with database storage.

- **DeviceMonitor.csproj**: Project file configured for console application (.NET 6.0).

## Prerequisites

- **.NET 6.0 Runtime**: Required for application execution
- **PostgreSQL Database**: For data storage and historical analytics
  - Database: `biting_lip`
  - Default connection: `Host=localhost;Database=biting_lip;Username=postgres;Password=postgres`
- **Administrator Privileges**: Required for hardware sensor access
- **LibreHardwareMonitor**: Automatically included via NuGet package

## Setup Instructions

1. **Clone the repository** to your local machine:
   ```bash
   git clone <repository-url>
   cd node-manager/device-monitor
   ```

2. **Install PostgreSQL** and create the database:
   ```sql
   CREATE DATABASE biting_lip;
   ```

3. **Restore NuGet packages** and build:
   ```bash
   dotnet restore
   dotnet build
   ```

4. **Test the setup** with database connection:
   ```bash
   dotnet run -- --test-database
   ```

## Usage Guidelines

### Command Line Options

```bash
# Auto setup and run (default behavior)
DeviceMonitor.exe

# Setup database and create devices table
DeviceMonitor.exe --setup

# Background service with database integration
DeviceMonitor.exe --service

# Test database connection and table structure
DeviceMonitor.exe --test-database

# Hardware monitoring only (displays live metrics table)
DeviceMonitor.exe --test-hardware

# Show help and usage information
DeviceMonitor.exe --help
```

### Default Behavior (No Arguments)

When run without arguments, the application will:
1. **Auto Setup**: Attempt to connect to PostgreSQL and create/verify the devices table
2. **Service Mode**: If database setup succeeds, run as background service with database integration
3. **Fallback Mode**: If database setup fails, automatically switch to live hardware metrics table display

### Individual Modes

- **`--setup`**: Database initialization and table creation only
- **`--service`**: Full background service with PostgreSQL integration
- **`--test-database`**: Verify database connectivity and table structure
- **`--test-hardware`**: Live hardware metrics table with real-time updates
- **`--help`**: Display usage information and examples

# Show help and usage information
DeviceMonitor.exe --help
```

### Background Service Operation

- **Silent Monitoring**: Runs as console application with minimal output
- **Automatic Table Creation**: Creates PostgreSQL tables on first run
- **Real-time Updates**: 1-second update intervals for all metrics
- **Graceful Shutdown**: Ctrl+C for clean service termination
- **Mining Detection**: Automatically identifies GPUs under mining load

### Database Schema

The application creates a `devices` table with the following structure:

```sql
CREATE TABLE devices (
    device_id VARCHAR(50) PRIMARY KEY,
    device_vendor VARCHAR(50),
    device_name VARCHAR(100),
    status VARCHAR(20),
    status_message TEXT,
    memory_capacity INTEGER,
    memory_usage REAL,
    processing_usage REAL,
    time_created TIMESTAMP,
    time_updated TIMESTAMP
);
```

### Monitoring Output Example

```
=== Device Monitor Background Service Mode ===
Starting Device Monitor Background Service...
Background service running. Press Ctrl+C to stop.
```

## Dependencies

- **LibreHardwareMonitorLib** (v0.9.4): Hardware sensor access and monitoring
- **Npgsql** (v8.0.3): PostgreSQL database connectivity and operations

## Architecture

### Hardware Monitoring Layer
- **SystemMonitorService**: Direct hardware access via LibreHardwareMonitor
- **Real-time Metrics**: GPU utilization, memory usage, temperature, power, clocks
- **Multi-vendor Support**: AMD and NVIDIA GPU detection and monitoring

### Database Layer  
- **DatabaseService**: PostgreSQL connection and query management
- **DatabaseMonitoringService**: Coordinates hardware monitoring with data persistence
- **Automatic Schema**: Creates tables, indexes, and constraints on initialization

### Service Layer
- **BackgroundServiceProgram**: Main service orchestration and lifecycle management
- **Graceful Shutdown**: Proper cleanup of hardware and database connections
- **Error Handling**: Silent operation with robust error recovery

## Performance

- **Update Frequency**: 1-second intervals for real-time monitoring
- **Resource Usage**: Minimal CPU overhead (~1-2% on modern systems)
- **Database Efficiency**: Optimized upsert operations for high-frequency updates
- **Memory Footprint**: Lightweight service suitable for server environments

## Troubleshooting

### Common Issues

1. **Database Connection Failed**
   ```bash
   # Test connection manually
   dotnet run -- --dbtest
   ```

2. **Hardware Access Denied**
   - Run as Administrator
   - Check Windows permissions for hardware access

3. **GPU Not Detected** 
   - Ensure latest GPU drivers are installed
   - Verify LibreHardwareMonitor compatibility

4. **Service Won't Start**
   ```bash
   # Check detailed error output
   dotnet run -- --database
   ```

## Contributing

Contributions to the project are welcome. Please submit a pull request or open an issue for any enhancements or bug fixes.

### Development Setup

1. Fork the repository
2. Create a feature branch
3. Test with both `--service` and `--service-hardware` modes
4. Verify database integration with `--dbtest`
5. Submit pull request with detailed description

## License

This project is licensed under the MIT License. See the LICENSE file for more details.

---

**Device Monitor** - Real-time hardware monitoring with PostgreSQL integration  
*Built with LibreHardwareMonitor for reliable cross-platform hardware access*