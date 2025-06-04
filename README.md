# Node Manager

A sophisticated per-node agent for the BitingLip distributed AI inference platform.

## Overview

The Node Manager serves as the "commandant" for individual nodes in the BitingLip cluster, handling local resource management, worker coordination, and communication with the central cluster manager.

## Architecture

### Current State (June 2025)

The Node Manager has evolved into a comprehensive system with the following components:

#### ✅ **Core Components** (Production Ready)
- **NodeController**: Main orchestrator with sophisticated lifecycle management
- **ResourceManager**: Hardware resource detection and monitoring 
- **WorkerManager**: GPU worker process management and coordination
- **TaskDispatcher**: Task queue management and routing

#### ✅ **Communication Layer** (Complete)
- **ClusterClient**: HTTP-based communication with cluster manager
- **MessageQueue**: Asynchronous message handling with pub/sub
- **APIServer**: FastAPI-based REST endpoints
- **CommunicationCoordinator**: Centralized communication orchestration

#### 🔄 **Specialized Workers** (In Development)
- Base worker classes and registry system
- GPU-specific worker implementations
- Training, inference, and utility worker types

#### 🔄 **Monitoring & Database** (Partial)
- Health monitoring and metrics collection
- PostgreSQL integration for state persistence
- Environment detection and validation

## Key Features

### Node Management
- **Automatic Node Registration**: Generates unique node IDs and registers with cluster
- **Resource Detection**: Automatic GPU detection and capability reporting
- **Heartbeat Monitoring**: Continuous health reporting to cluster manager
- **Graceful Shutdown**: Proper cleanup and resource deallocation

### Worker Coordination
- **Multi-GPU Support**: Intelligent GPU allocation and worker spawning
- **Process Management**: Worker lifecycle control and monitoring
- **Task Routing**: Efficient task distribution to available workers
- **Resource Isolation**: Per-worker resource allocation and limits

### Communication
- **Cluster Integration**: Seamless integration with cluster manager
- **Event-Driven Architecture**: Message-based coordination between components
- **Error Handling**: Comprehensive error reporting and recovery
- **API Endpoints**: REST API for external management and monitoring

## Quick Start

### Prerequisites
- Python 3.10+
- PostgreSQL (optional, for persistence)
- NVIDIA GPUs (for GPU workers)

### Installation
```bash
# Install dependencies
pip install -r requirements.txt

# Optional: Set up configuration
cp config/node_config.py.example config/node_config.py
```

### Running the Node Manager
```bash
# Start with default configuration
python main.py

# Start with custom configuration
python main.py --config /path/to/config.json

# Start with specific cluster manager
python main.py --cluster-url http://cluster-manager:8083

# Development mode with detailed logging
python main.py --log-level DEBUG
```

### Basic Testing
```bash
# Run simple functionality test
python test_simple.py

# Run specific component tests (when package imports are fixed)
python -m pytest tests/ -v
```

## Configuration

### Command Line Options
- `--config`: Path to configuration file
- `--log-level`: Logging level (DEBUG, INFO, WARNING, ERROR)
- `--port`: API server port (default: 8080)
- `--cluster-url`: Cluster manager URL
- `--node-id`: Override node identifier

### Environment Variables
- `NODE_MANAGER_CONFIG`: Configuration file path
- `CLUSTER_MANAGER_URL`: Cluster manager endpoint
- `GPU_DEVICES`: Comma-separated GPU device IDs
- `LOG_LEVEL`: Default logging level

## API Endpoints

### Health & Status
```
GET /health              # Health check
GET /status              # Detailed node status
GET /metrics             # Performance metrics
```

### Worker Management
```
GET /workers             # List active workers
POST /workers            # Spawn new worker
DELETE /workers/{id}     # Stop worker
```

### Task Management
```
GET /tasks               # List active tasks
POST /tasks              # Submit task
GET /tasks/{id}          # Task status
```

## Architecture Vision

### Current vs. Target State

**Current**: The Node Manager currently combines both cluster-level coordination and node-level operations in a single service.

**Target**: Split into dedicated roles:
- **Cluster Manager**: Global orchestration, multi-node coordination
- **Node Manager**: Per-node operations, worker management, local resources

### Planned Improvements

1. **Architectural Split**: Separate cluster management from node operations
2. **Lightweight Agent**: Create minimal node agent for easy deployment
3. **Enhanced Monitoring**: Distributed monitoring with centralized aggregation
4. **Auto-scaling**: Dynamic worker scaling based on load
5. **High Availability**: Failover and redundancy mechanisms

## Development Status

### ✅ Completed
- Core NodeController with proper initialization
- Communication layer with cluster integration
- Basic worker management framework
- API server and endpoints
- Message queue and event system

### 🔄 In Progress
- Test suite alignment with current architecture
- Package import structure fixes
- Worker implementation completion
- Enhanced monitoring integration

### 📋 Planned
- Architectural refactoring (cluster vs. node separation)
- Production deployment configurations
- Performance optimization
- Documentation updates

## Contributing

The Node Manager is part of the larger BitingLip platform. When contributing:

1. **Follow the Architecture**: Understand the cluster vs. node responsibility split
2. **Test Thoroughly**: Ensure changes work with existing communication layer
3. **Update Documentation**: Keep this README current with changes
4. **Consider Scale**: Design for multi-node, multi-GPU environments

## Related Components

- **Cluster Manager**: Global orchestration and node registry
- **Task Manager**: Job scheduling and queue management  
- **Model Manager**: Model storage and distribution
- **Gateway Manager**: API gateway and request routing

---

*Last Updated: June 4, 2025*
*Status: Active Development - Communication Layer Complete*