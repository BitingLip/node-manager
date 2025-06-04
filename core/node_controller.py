"""
Node Controller - Main Orchestrator
Central coordinator for the node manager, handling lifecycle and coordination between components
Acts as the "commandant" orchestrating all node operations
"""

import logging
import threading
import time
import uuid
import socket
from typing import Dict, List, Optional, Any
from datetime import datetime
import structlog

# Import components (with fallbacks for optional components)
try:
    from ..database.node_database import NodeDatabase
except ImportError:
    NodeDatabase = None

try:
    from ..monitoring.metrics_collector import MetricsCollector
    from ..monitoring.health_monitor import HealthMonitor
except ImportError:
    MetricsCollector = None
    HealthMonitor = None

logger = structlog.get_logger(__name__)


class NodeController:
    """
    Main orchestrator for node operations
    Coordinates between resource management, worker management, and task dispatching
    """
    
    def __init__(self, config_path: Optional[str] = None):
        """Initialize the node controller"""
        self.node_id = f"node-{uuid.uuid4().hex[:8]}"
        self.status = "initializing"
        self.config = self._load_config(config_path)
        self.hostname = socket.gethostname()
        
        # Component managers
        self.resource_manager = None
        self.worker_manager = None  
        self.task_dispatcher = None
        self.cluster_client = None
        self.database = None
        self.metrics_collector = None
        self.health_monitor = None
        
        # Threading control
        self._shutdown_event = threading.Event()
        self._monitor_thread = None
        
        logger.info(f"NodeController initializing with ID: {self.node_id}")
    
    def _load_config(self, config_path: Optional[str] = None) -> Dict[str, Any]:
        """Load configuration from file or use defaults"""
        default_config = {
            'database': {
                'host': 'localhost',
                'port': 5432,
                'database': 'bitinglip_nodes',
                'user': 'postgres',
                'password': 'password',
                'min_connections': 1,
                'max_connections': 10
            },
            'cluster': {
                'manager_host': 'localhost',
                'manager_port': 8005,
                'register_interval': 30,
                'heartbeat_interval': 10
            },
            'node': {
                'port': 8010,
                'max_workers': 4,
                'resource_monitoring_interval': 30
            }
        }
        
        if config_path:
            try:
                from ..config.node_config import NodeConfig
                node_config = NodeConfig(config_path)
                config_data = node_config.get_all_config()
                
                # Merge with defaults
                for section, section_config in default_config.items():
                    if section not in config_data:
                        config_data[section] = section_config
                    else:
                        # Merge section-level configs
                        for key, value in section_config.items():
                            if key not in config_data[section]:
                                config_data[section][key] = value
                
                logger.info(f"Configuration loaded from {config_path}")
                return config_data
                
            except Exception as e:
                logger.warning(f"Failed to load config from {config_path}: {e}, using defaults")
        
        return default_config
    
    def start(self) -> bool:
        """Start the node manager and all its components"""
        try:
            logger.info(f"Starting Node Manager {self.node_id}")
            
            # 1. Initialize database
            if not self._initialize_database():
                logger.error("Failed to initialize database")
                return False
            
            # 2. Start resource manager
            if not self._start_resource_manager():
                logger.error("Failed to start resource manager")
                return False
            
            # 3. Initialize worker manager
            if not self._initialize_worker_manager():
                logger.error("Failed to initialize worker manager")
                return False
            
            # 4. Start monitoring systems
            if not self._start_monitoring():
                logger.error("Failed to start monitoring systems")
                return False
            
            # 5. Connect to cluster manager
            if not self.register_with_cluster():
                logger.warning("Failed to register with cluster (continuing anyway)")
            
            # 6. Start task dispatcher
            if not self._start_task_dispatcher():
                logger.error("Failed to start task dispatcher")
                return False
            
            # 7. Begin monitoring loop
            self._start_monitoring_loop()
            
            self.status = "ready"
            logger.info(f"Node Manager {self.node_id} started successfully")
            return True
            
        except Exception as e:
            logger.error(f"Failed to start node manager: {e}")
            self.status = "error"
            return False
    
    def _initialize_database(self) -> bool:
        """Initialize database connection"""
        if NodeDatabase is None:
            logger.warning("Database module not available, skipping database initialization")
            return True
            
        try:
            db_config = self.config['database']
            self.database = NodeDatabase(db_config)
            
            # Register this node in database
            self.database.update_node_status(
                self.node_id, 
                "initializing",
                {
                    'hostname': self.hostname,
                    'capabilities': self._get_node_capabilities(),
                    'resources': {}
                }
            )
            
            logger.info("Database initialized successfully")
            return True
        except Exception as e:
            logger.error(f"Failed to initialize database: {e}")
            return False
    
    def _start_resource_manager(self) -> bool:
        """Start resource manager"""
        try:
            from .resource_manager import ResourceManager
            self.resource_manager = ResourceManager(self.config)
            # Initialize resource detection
            self.resource_manager.detect_resources()
            logger.info("Resource manager started")
            return True
        except Exception as e:
            logger.error(f"Failed to start resource manager: {e}")
            return False
    
    def _initialize_worker_manager(self) -> bool:
        """Initialize worker manager"""
        try:
            from .worker_manager import WorkerManager
            self.worker_manager = WorkerManager(
                node_id=self.node_id,
                config=self.config,
                resource_manager=self.resource_manager,
                database=self.database
            )
            logger.info("Worker manager initialized")
            return True
        except Exception as e:
            logger.error(f"Failed to initialize worker manager: {e}")
            return False
    
    def _start_monitoring(self) -> bool:
        """Start monitoring systems"""
        try:
            # Start metrics collector if available
            if MetricsCollector:
                self.metrics_collector = MetricsCollector(
                    node_id=self.node_id,
                    collection_interval=self.config['node']['resource_monitoring_interval']
                )
                self.metrics_collector.start_collection()
                logger.info("Metrics collection started")
            
            # Start health monitor if available
            if HealthMonitor:
                self.health_monitor = HealthMonitor(
                    node_id=self.node_id,
                    worker_manager=self.worker_manager
                )
                self.health_monitor.start_monitoring()
                logger.info("Health monitoring started")
            
            return True
        except Exception as e:
            logger.error(f"Failed to start monitoring: {e}")
            return False
    
    def stop(self):
        """Stop the node manager gracefully"""
        logger.info(f"Stopping Node Manager {self.node_id}")
        
        # 1. Stop accepting new tasks
        if self.task_dispatcher:
            self.task_dispatcher.stop()
            
        # 2. Complete current tasks (with timeout)
        if self.worker_manager:
            self.worker_manager.shutdown_workers(timeout=30)
            
        # 3. Stop monitoring
        self._shutdown_event.set()
        if self._monitor_thread and self._monitor_thread.is_alive():
            self._monitor_thread.join(timeout=5)
            
        if self.metrics_collector:
            self.metrics_collector.stop_collection()
            
        if self.health_monitor:
            self.health_monitor.stop_monitoring()
            
        # 4. Disconnect from cluster
        if self.cluster_client:
            self.cluster_client.disconnect()
            
        # 5. Close database connections
        if self.database:
            self.database.close()
            
        self.status = "offline"
        logger.info(f"Node Manager {self.node_id} stopped")
    
    def get_status(self) -> Dict[str, Any]:
        """Get current node status"""
        status_data = {
            'node_id': self.node_id,
            'hostname': self.hostname,
            'status': self.status,
            'uptime': time.time() - getattr(self, '_start_time', time.time()),
            'capabilities': self._get_node_capabilities(),
            'timestamp': datetime.now().isoformat()
        }
        
        # Add resource information if available
        if self.resource_manager:
            try:
                status_data['resources'] = self.resource_manager.get_current_usage()
            except Exception as e:
                logger.warning(f"Failed to get resource status: {e}")
                status_data['resources'] = {}
        
        # Add worker information if available
        if self.worker_manager:
            try:
                status_data['workers'] = {
                    'total': len(self.worker_manager.get_all_workers()),
                    'active': len(self.worker_manager.get_available_workers()),
                    'capabilities': self.worker_manager.get_worker_capabilities()
                }
            except Exception as e:
                logger.warning(f"Failed to get worker status: {e}")
                status_data['workers'] = {}
        
        # Add task information if available
        if self.task_dispatcher:
            try:
                status_data['tasks'] = self.task_dispatcher.get_queue_stats()
            except Exception as e:
                logger.warning(f"Failed to get task status: {e}")
                status_data['tasks'] = {}
        
        return status_data
    
    def register_with_cluster(self) -> bool:
        """Register this node with the cluster manager"""
        try:
            cluster_config = self.config['cluster']
            
            registration_data = {
                'node_id': self.node_id,
                'hostname': self.hostname,
                'capabilities': self._get_node_capabilities(),
                'status': self.status,
                'endpoint': f"http://{self.hostname}:{self.config['node']['port']}"
            }
            
            logger.info(f"Registering with cluster manager at {cluster_config['manager_host']}:{cluster_config['manager_port']}")
            
            # Implement HTTP registration
            try:
                import requests
                
                url = f"http://{cluster_config['manager_host']}:{cluster_config['manager_port']}/api/nodes/register"
                response = requests.post(url, json=registration_data, timeout=10)
                
                if response.status_code == 200:
                    logger.info("Successfully registered with cluster manager")
                    return True
                else:
                    logger.warning(f"Cluster registration failed: {response.status_code} - {response.text}")
                    return False
                    
            except ImportError:
                logger.warning("requests library not available, skipping HTTP registration")
                return True  # Continue without cluster registration
            except Exception as e:
                logger.warning(f"HTTP registration failed: {e}")
                return False
            
        except Exception as e:
            logger.error(f"Failed to register with cluster manager: {e}")
            return False
    
    def _monitoring_loop(self):
        """Main monitoring and heartbeat loop"""
        logger.info("Starting monitoring loop")
        heartbeat_interval = self.config['cluster']['heartbeat_interval']
        
        while not self._shutdown_event.is_set():
            try:
                # 1. Send heartbeat to cluster
                self._send_heartbeat()
                
                # 2. Update resource status in database
                self._update_resource_status()
                
                # 3. Check worker health
                self._check_worker_health()
                
                # 4. Report metrics
                self._report_metrics()
                
                # 5. Cleanup old records periodically
                if hasattr(self, '_last_cleanup'):
                    if time.time() - self._last_cleanup > 3600:  # Every hour
                        self._cleanup_old_data()
                        self._last_cleanup = time.time()
                else:
                    self._last_cleanup = time.time()
                
            except Exception as e:
                logger.error(f"Error in monitoring loop: {e}")
            
            # Wait for next iteration
            self._shutdown_event.wait(heartbeat_interval)
        
        logger.info("Monitoring loop stopped")
    
    def _send_heartbeat(self):
        """Send heartbeat to cluster manager"""
        try:
            if self.database:
                self.database.record_node_heartbeat(self.node_id)
            
            # Send heartbeat to cluster manager via HTTP
            try:
                import requests
                
                cluster_config = self.config['cluster']
                heartbeat_data = {
                    'node_id': self.node_id,
                    'status': self.status,
                    'timestamp': time.time()
                }
                
                url = f"http://{cluster_config['manager_host']}:{cluster_config['manager_port']}/api/nodes/{self.node_id}/heartbeat"
                response = requests.post(url, json=heartbeat_data, timeout=5)
                
                if response.status_code == 200:
                    logger.debug(f"Heartbeat sent for node {self.node_id}")
                else:
                    logger.warning(f"Heartbeat failed: {response.status_code}")
                    
            except ImportError:
                logger.debug(f"Heartbeat recorded locally for node {self.node_id}")
            except Exception as e:
                logger.warning(f"Failed to send HTTP heartbeat: {e}")
            
        except Exception as e:
            logger.warning(f"Failed to send heartbeat: {e}")
    
    def _update_resource_status(self):
        """Update resource status in database"""
        try:
            if self.resource_manager and self.database:
                resource_data = self.resource_manager.get_current_usage()
                resource_data['node_id'] = self.node_id
                self.database.record_resource_usage(resource_data)
                
        except Exception as e:
            logger.warning(f"Failed to update resource status: {e}")
    
    def _check_worker_health(self):
        """Check worker health and restart if needed"""
        try:
            if self.worker_manager:
                self.worker_manager.health_check()
                
        except Exception as e:
            logger.warning(f"Failed to check worker health: {e}")
    
    def _report_metrics(self):
        """Report collected metrics"""
        try:
            if self.metrics_collector:
                metrics = self.metrics_collector.get_current_metrics()
                logger.debug(f"Current metrics: {metrics}")
                
        except Exception as e:
            logger.warning(f"Failed to report metrics: {e}")
    
    def _cleanup_old_data(self):
        """Cleanup old database records"""
        try:
            if self.database:
                self.database.cleanup_old_records(days=7)
                logger.debug("Old data cleanup completed")
                
        except Exception as e:
            logger.warning(f"Failed to cleanup old data: {e}")
    
    def _start_task_dispatcher(self) -> bool:
        """Start task dispatcher"""
        try:
            from .task_dispatcher import TaskDispatcher
            self.task_dispatcher = TaskDispatcher(
                node_id=self.node_id,
                worker_manager=self.worker_manager,
                database=self.database
            )
            self.task_dispatcher.start()
            logger.info("Task dispatcher started")
            return True
        except Exception as e:
            logger.error(f"Failed to start task dispatcher: {e}")
            return False
    
    def _start_monitoring_loop(self):
        """Start the monitoring thread"""
        self._monitor_thread = threading.Thread(
            target=self._monitoring_loop,
            name="NodeMonitoring",
            daemon=True
        )
        self._monitor_thread.start()
        logger.info("Monitoring loop started")
    
    def _get_node_capabilities(self) -> Dict[str, Any]:
        """Get node capabilities for registration"""
        import platform
        
        capabilities = {
            'hostname': self.hostname,
            'architecture': platform.machine(),
            'platform': platform.system(),
            'supported_workers': ['inference', 'training', 'utility'],
            'max_workers': self.config['node']['max_workers']
        }
        
        # Add GPU capabilities if detected
        try:
            import torch
            if torch.cuda.is_available():
                capabilities['gpu'] = {
                    'available': True,
                    'count': torch.cuda.device_count(),
                    'devices': []
                }
                
                for i in range(torch.cuda.device_count()):
                    device_props = torch.cuda.get_device_properties(i)
                    capabilities['gpu']['devices'].append({
                        'index': i,
                        'name': device_props.name,
                        'memory_mb': device_props.total_memory // (1024 * 1024),
                        'compute_capability': f"{device_props.major}.{device_props.minor}"
                    })
            else:
                capabilities['gpu'] = {'available': False, 'count': 0}
                
        except ImportError:
            capabilities['gpu'] = {'available': False, 'count': 0, 'error': 'torch not available'}
        except Exception as e:
            capabilities['gpu'] = {'available': False, 'count': 0, 'error': str(e)}
        
        # Add CPU information
        try:
            import psutil
            capabilities['cpu'] = {
                'cores': psutil.cpu_count(logical=False),
                'threads': psutil.cpu_count(logical=True),
                'frequency_mhz': psutil.cpu_freq().current if psutil.cpu_freq() else None
            }
        except ImportError:
            import multiprocessing
            capabilities['cpu'] = {
                'cores': multiprocessing.cpu_count(),
                'threads': multiprocessing.cpu_count()
            }
        
        # Add memory information
        try:
            import psutil
            memory_info = psutil.virtual_memory()
            capabilities['memory'] = {
                'total_mb': memory_info.total // (1024 * 1024),
                'available_mb': memory_info.available // (1024 * 1024)
            }
        except ImportError:
            capabilities['memory'] = {'total_mb': 'unknown', 'available_mb': 'unknown'}
        
        return capabilities
