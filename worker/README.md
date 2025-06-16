# Modular GPU Worker System

A redesigned, modular worker architecture for GPU-based AI inference tasks. This system separates concerns into distinct modules for better maintainability, observability, and scalability.

## Architecture Overview

The worker is split into the following core modules:

- **`worker.py`** - Main orchestrator with three threads (communication, database, action processing)
- **`core/memory.py`** - Memory management and model loading (RAM ↔ VRAM operations)
- **`core/processing.py`** - Inference execution with pre-configured settings
- **`core/communication.py`** - Communication with node-manager via HTTP/REST
- **`core/database.py`** - TimescaleDB integration for logging and metrics
- **`core/hardware.py`** - Hardware monitoring and metrics collection
- **`core/config.py`** - Configuration management
- **`core/logger.py`** - Centralized logging utilities

## Key Design Principles

### 1. Separation of Optimization Responsibilities

- **Worker Responsibility**: Hardware-level optimizations (VRAM cleanup, memory management)
- **Node-Manager Responsibility**: Inference strategy optimizations (scheduler selection, attention slicing parameters)

The worker receives pre-configured settings from the node-manager and applies them without making optimization decisions.

### 2. Three-Threaded Architecture

- **Communication Thread**: Handles node-manager communication without blocking
- **Database Thread**: Manages all database operations and logging
- **Action Thread**: Processes instructions and executes tasks

### 3. Database-Driven Observability

All operations are logged to TimescaleDB:

- **Instructions Table**: Commands received from node-manager
- **Actions Table**: Actions performed by the worker
- **Results Table**: Results of actions/tasks
- **Hardware Metrics Table**: Real-time hardware monitoring
- **Worker Status Table**: Current worker state summary

## Quick Start

### 1. Install Dependencies

```bash
pip install -r requirements.txt
```

### 2. Launch Worker

```bash
# Basic launch for GPU 0
python launch_worker.py --device-id 0

# Custom worker ID
python launch_worker.py --device-id 1 --worker-id gpu1_production

# Test mode (no GPU operations)
python launch_worker.py --device-id 0 --test-mode
```

### 3. Test Installation

```bash
python test_worker.py
```

## Configuration

Configuration is managed through `worker_config.json` or environment variables:

### Environment Variables

```bash
export DB_HOST=localhost
export DB_PORT=5432
export DB_NAME=node_manager
export DB_USER=postgres
export DB_PASSWORD=your_password
export NODE_MANAGER_HOST=localhost
export NODE_MANAGER_PORT=8080
export LOG_LEVEL=INFO
```

### Config File Example

```json
{
  "database": {
    "host": "localhost",
    "port": 5432,
    "name": "node_manager",
    "user": "postgres",
    "password": "password"
  },
  "communication": {
    "node_manager_host": "localhost",
    "node_manager_port": 8080,
    "heartbeat_interval": 10
  },
  "hardware": {
    "monitoring_interval": 5,
    "temperature_threshold": 80,
    "vram_threshold_mb": 7000
  }
}
```

## Worker Actions

The worker supports the following direct actions:

### Memory Actions
- `load_model_to_ram` - Load model from disk to RAM
- `clear_ram` - Clear RAM staging area
- `load_model_from_ram_to_vram` - Move model from RAM to GPU VRAM
- `clear_vram` - Unload model from VRAM
- `clean_vram` - Clean VRAM residuals after inference

### Inference Actions
- `run_inference` - Execute inference with provided settings
- `start_inference` - Start inference process
- `stop_inference` - Stop current inference
- `get_inference_status` - Get inference status

### Database Actions
- `put_to_database` - Store data in database
- `get_from_database` - Retrieve data from database
- `del_from_database` - Delete data from database

### Communication Actions
- `send_status` - Send status to node-manager
- `send_result` - Send result to node-manager

### Combined Actions
- `run_task` - Complete task (load model if needed + run inference + cleanup)

## Database Schema

### Instructions Table
```sql
CREATE TABLE instructions (
    id SERIAL PRIMARY KEY,
    timestamp TIMESTAMPTZ DEFAULT NOW(),
    worker_id VARCHAR(50) NOT NULL,
    instruction JSONB NOT NULL,
    action_ids INTEGER[]
);
```

### Hardware Metrics Table (TimescaleDB Hypertable)
```sql
CREATE TABLE hardware_metrics (
    timestamp TIMESTAMPTZ DEFAULT NOW(),
    worker_id VARCHAR(50) NOT NULL,
    gpu_id INTEGER,
    gpu_usage FLOAT,
    gpu_vram INTEGER,
    gpu_temperature FLOAT,
    cpu_usage FLOAT,
    cpu_ram INTEGER,
    cpu_temperature FLOAT
);
```

## Communication Protocol

The worker communicates with the node-manager via HTTP REST API:

### Registration
```
POST /api/workers/register
{
  "worker_id": "worker_0",
  "capabilities": {
    "gpu_inference": true,
    "model_types": ["stable_diffusion_xl"]
  }
}
```

### Receiving Instructions
```
GET /api/workers/{worker_id}/messages
```

### Sending Results
```
POST /api/workers/{worker_id}/results
{
  "result": {
    "success": true,
    "task_id": "task_123",
    "output_path": "/path/to/output.png"
  }
}
```

## Memory Management

The worker implements a two-stage memory management system:

1. **RAM Staging**: Models are loaded to RAM first
2. **VRAM Transfer**: Models are moved from RAM to GPU VRAM
3. **RAM Cleanup**: RAM is immediately cleared after VRAM transfer
4. **VRAM Residual Cleanup**: After each inference, residuals are cleaned

This minimizes RAM usage while ensuring efficient VRAM management.

## Hardware Monitoring

Real-time monitoring includes:

- GPU VRAM usage and temperature
- CPU usage, RAM, and temperature  
- System resource availability
- Performance metrics

All metrics are stored in TimescaleDB for historical analysis.

## Error Handling

The worker implements comprehensive error handling:

- Database connection failures
- GPU memory errors with automatic cleanup
- Communication timeouts with retry logic
- Graceful shutdown on errors

## Differences from Previous Architecture

### What Changed:
1. **Modular Design**: Separated into focused modules
2. **Database Integration**: All operations logged to TimescaleDB
3. **Thread Separation**: Communication, database, and action processing run independently
4. **Optimization Responsibility**: Worker no longer decides inference optimizations
5. **Enhanced Monitoring**: Real-time hardware metrics collection

### What Stayed:
1. **DirectML Support**: Still uses DirectML for GPU operations
2. **VRAM Management**: Advanced VRAM cleanup and optimization
3. **Model Loading**: Efficient RAM→VRAM transfer pattern
4. **Error Recovery**: Robust error handling and cleanup

## Development

### Adding New Actions

1. Add action handler to `worker.py` in `_process_instruction()`
2. Implement logic in appropriate core module
3. Add database logging if needed
4. Update documentation

### Testing

Run comprehensive tests:
```bash
python test_worker.py
```

For individual component testing, import and test specific modules.

## Monitoring and Debugging

### View Logs
```bash
# Worker logs
tail -f logs/worker_0.log

# Database logs (check worker status)
# Connect to your database and query worker_status table
```

### Check Worker Status
The worker status is available in the database and via the communication API.

## Deployment

For production deployment:

1. Set up TimescaleDB database
2. Configure environment variables
3. Launch workers with appropriate device IDs
4. Monitor via database queries and logs

Each worker should run on a separate process/container for isolation.
