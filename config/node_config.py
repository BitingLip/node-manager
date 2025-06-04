"""
Node Configuration
Configuration management for node-level settings
"""

import os
from typing import Dict, Any, Optional
import structlog

logger = structlog.get_logger(__name__)


class NodeConfig:
    """
    Node-level configuration management
    Handles environment variables, config files, and settings
    """
    
    def __init__(self, config_path: Optional[str] = None):
        """Initialize node configuration"""
        self.config_path = config_path
        self.config_data = {}
        
        # Default configuration
        self.defaults = {
            "node_id": None,
            "cluster_manager_url": "http://localhost:8000",
            "api_port": 8080,
            "max_workers": 4,
            "heartbeat_interval": 30,
            "task_timeout": 300,
            "max_retries": 3,
            "log_level": "INFO",
            "data_directory": "./data",
            "model_cache_directory": "./models"
        }
        
        # Load configuration
        self._load_config()
        
        logger.info("NodeConfig initialized")
    
    def _load_config(self):
        """Load configuration from file and environment"""
        try:
            # Start with defaults
            self.config_data = self.defaults.copy()
            
            # Load from config file if specified
            if self.config_path and os.path.exists(self.config_path):
                self._load_from_file()
            
            # Override with environment variables
            self._load_from_environment()
            
            # Validate configuration
            self._validate_config()
            
            logger.info("Configuration loaded successfully")
            
        except Exception as e:
            logger.error(f"Failed to load configuration: {e}")
            # Use defaults on failure
            self.config_data = self.defaults.copy()
    
    def _load_from_file(self):
        """Load configuration from JSON/YAML file"""
        try:
            import json
            
            if not self.config_path:
                return
                
            with open(self.config_path, 'r') as f:
                if self.config_path.endswith('.json'):
                    file_config = json.load(f)
                elif self.config_path.endswith(('.yml', '.yaml')):
                    try:
                        import yaml
                        file_config = yaml.safe_load(f)
                    except ImportError:
                        logger.error("PyYAML not available for YAML config files")
                        return
                else:
                    logger.warning(f"Unsupported config file format: {self.config_path}")
                    return
            
            # Merge file config with defaults
            self.config_data.update(file_config)
            logger.info(f"Configuration loaded from file: {self.config_path}")
            
        except Exception as e:
            logger.error(f"Failed to load config file {self.config_path}: {e}")
    
    def _load_from_environment(self):
        """Load configuration from environment variables"""
        env_mapping = {
            'NODE_ID': 'node_id',
            'CLUSTER_MANAGER_URL': 'cluster_manager_url',
            'API_PORT': 'api_port',
            'MAX_WORKERS': 'max_workers',
            'HEARTBEAT_INTERVAL': 'heartbeat_interval',
            'TASK_TIMEOUT': 'task_timeout',
            'MAX_RETRIES': 'max_retries',
            'LOG_LEVEL': 'log_level',
            'DATA_DIRECTORY': 'data_directory',
            'MODEL_CACHE_DIRECTORY': 'model_cache_directory'
        }
        
        for env_var, config_key in env_mapping.items():
            env_value = os.getenv(env_var)
            if env_value is not None:
                # Convert to appropriate type
                if config_key in ['api_port', 'max_workers', 'heartbeat_interval', 'task_timeout', 'max_retries']:
                    try:
                        self.config_data[config_key] = int(env_value)
                    except ValueError:
                        logger.warning(f"Invalid integer value for {env_var}: {env_value}")
                else:
                    self.config_data[config_key] = env_value
    
    def _validate_config(self):
        """Validate configuration values"""
        # Validate required fields
        if not self.config_data.get('node_id'):
            import uuid
            self.config_data['node_id'] = f"node-{uuid.uuid4().hex[:8]}"
        
        # Validate numeric ranges
        if self.config_data['max_workers'] < 1:
            logger.warning("max_workers must be >= 1, setting to 1")
            self.config_data['max_workers'] = 1
        
        if self.config_data['heartbeat_interval'] < 5:
            logger.warning("heartbeat_interval must be >= 5, setting to 5")
            self.config_data['heartbeat_interval'] = 5
        
        # Ensure directories exist
        for dir_key in ['data_directory', 'model_cache_directory']:
            directory = self.config_data[dir_key]
            if not os.path.exists(directory):
                try:
                    os.makedirs(directory, exist_ok=True)
                    logger.info(f"Created directory: {directory}")
                except Exception as e:
                    logger.error(f"Failed to create directory {directory}: {e}")    
    def get_config(self, key: str, default=None):
        """Get configuration value"""
        return self.config_data.get(key, default)
    
    def get_all_config(self) -> Dict[str, Any]:
        """Get all configuration data"""
        return self.config_data.copy()
    
    def update_config(self, updates: Dict[str, Any]):
        """Update configuration values"""
        self.config_data.update(updates)
        self._validate_config()
    
    def save_to_file(self, file_path: str):
        """Save configuration to file"""
        try:
            import json
            
            with open(file_path, 'w') as f:
                json.dump(self.config_data, f, indent=2)
            
            logger.info(f"Configuration saved to: {file_path}")
            
        except Exception as e:
            logger.error(f"Failed to save configuration to {file_path}: {e}")
    
    def get(self, key: str, default: Any = None) -> Any:
        """Get configuration value"""
        # TODO: Get config value with fallback to default
        return self.config_data.get(key, self.defaults.get(key, default))
    
    def set(self, key: str, value: Any):
        """Set configuration value"""
        # TODO: Set config value and optionally persist
        self.config_data[key] = value
    
    def update(self, config_dict: Dict[str, Any]):
        """Update multiple configuration values"""
        # TODO: Update config with dictionary
        self.config_data.update(config_dict)
    
    def validate(self) -> bool:
        """Validate current configuration"""
        # TODO: Implement configuration validation
        # 1. Check required fields
        # 2. Validate data types
        # 3. Check resource limits
        # 4. Validate URLs and paths
        return True
    
    def save(self, config_path: Optional[str] = None):
        """Save configuration to file"""
        # TODO: Implement config saving
        pass
    
    def reload(self):
        """Reload configuration from sources"""
        # TODO: Reload config and notify components
        self._load_config()
    
    def get_database_config(self) -> Dict[str, Any]:
        """Get database configuration"""
        # TODO: Return PostgreSQL connection settings
        return {
            "host": self.get("db_host", "localhost"),
            "port": self.get("db_port", 5432),
            "database": self.get("db_name", "bitinglip_nodes"),
            "user": self.get("db_user", "postgres"),
            "password": self.get("db_password", "password"),
            "pool_size": self.get("db_pool_size", 10)
        }
