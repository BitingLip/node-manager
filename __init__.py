"""
Node Manager
Local node management and worker coordination system

The Node Manager acts as the "commandant" for a local node, managing worker processes,
resources, and communication with the cluster manager.

Main Components:
- Core: Node controller, resource manager, worker manager, task dispatcher
- Workers: Specialized worker processes for different AI model types
- Communication: API server, cluster client, message queue
- Database: PostgreSQL integration for local state management
- Monitoring: Health monitoring and metrics collection
- Configuration: Node and worker configuration management
"""

__version__ = "1.0.0"
__author__ = "BitingLip Team"

# Core components
from .core import NodeController, ResourceManager, WorkerManager, TaskDispatcher

# Worker implementations (base classes and registry)
from .workers import (
    BaseWorker, WorkerStatus, WorkerMetrics, 
    WorkerPool, PoolManager,
    WorkerRegistry, worker_registry
)

# Communication components (if they exist)
try:
    from .communication import APIServer, ClusterClient, MessageQueue
except ImportError:
    APIServer = None
    ClusterClient = None
    MessageQueue = None

# Database components
try:
    from .database import NodeDatabase
except ImportError:
    NodeDatabase = None

# Monitoring components
try:
    from .monitoring import HealthMonitor, MetricsCollector
except ImportError:
    HealthMonitor = None
    MetricsCollector = None

# Configuration components
try:
    from .config import NodeConfig, WorkerConfig
except ImportError:
    NodeConfig = None
    WorkerConfig = None

__all__ = [
    # Core
    'NodeController',
    'ResourceManager', 
    'WorkerManager',
    'TaskDispatcher',
    
    # Workers - Base classes and registry
    'BaseWorker',
    'WorkerStatus',
    'WorkerMetrics',
    'WorkerPool',
    'PoolManager',
    'WorkerRegistry',
    'worker_registry',
    
    # Communication (if available)
    'APIServer',
    'ClusterClient',
    'MessageQueue',
    
    # Database (if available)
    'NodeDatabase',
    
    # Monitoring (if available)
    'HealthMonitor',
    'MetricsCollector',
    
    # Configuration (if available)
    'NodeConfig',
    'WorkerConfig'
]


from typing import Optional

def create_node_manager(config_path: Optional[str] = None, port: Optional[int] = None) -> NodeController:
    """
    Factory function to create a configured node manager instance
    
    Args:
        config_path: Path to configuration file
        port: Optional port override for API server
        
    Returns:
        Configured NodeController instance
    """    # TODO: Implement node manager factory
    # 1. Load configuration
    # 2. Initialize database
    # 3. Create component instances
    # 4. Wire components together    # 5. Return configured controller
    
    from .config import NodeConfig
    
    config = NodeConfig(config_path)
    
    # Apply port override if provided
    config_overrides = {}
    if port is not None:
        config_overrides['api'] = {'port': port}
    
    return NodeController(config_path, config_overrides)
