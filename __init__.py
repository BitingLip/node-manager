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

# Worker implementations  
from .workers import BaseWorker, LLMWorker, StableDiffusionWorker, TTSWorker, WorkerFactory

# Communication components
from .communication import APIServer, ClusterClient, MessageQueue

# Database components
from .database import NodeDatabase

# Monitoring components
from .monitoring import HealthMonitor, MetricsCollector

# Configuration components
from .config import NodeConfig, WorkerConfig

__all__ = [
    # Core
    'NodeController',
    'ResourceManager', 
    'WorkerManager',
    'TaskDispatcher',
    
    # Workers
    'BaseWorker',
    'LLMWorker',
    'StableDiffusionWorker', 
    'TTSWorker',
    'WorkerFactory',
    
    # Communication
    'APIServer',
    'ClusterClient',
    'MessageQueue',
    
    # Database
    'NodeDatabase',
    
    # Monitoring
    'HealthMonitor',
    'MetricsCollector',
    
    # Configuration
    'NodeConfig',
    'WorkerConfig'
]


def create_node_manager(config_path: str = None) -> NodeController:
    """
    Factory function to create a configured node manager instance
    
    Args:
        config_path: Path to configuration file
        
    Returns:
        Configured NodeController instance
    """
    # TODO: Implement node manager factory
    # 1. Load configuration
    # 2. Initialize database
    # 3. Create component instances
    # 4. Wire components together
    # 5. Return configured controller
    
    from .config import NodeConfig
    
    config = NodeConfig(config_path)
    controller = NodeController()
    
    return controller
