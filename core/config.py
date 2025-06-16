#!/usr/bin/env python3
"""
Config - Configuration management and settings for Node Manager
"""
import json
import os
from pathlib import Path
from typing import Dict, Any, Optional


class Config:
    """Configuration manager for node manager settings"""
    
    def __init__(self, config_file: Optional[str] = None):
        self.config_file = config_file or "node_config.json"
        self.config_data = {}
        self.load_config()
    
    def load_config(self):
        """Load configuration from file"""
        config_path = Path(self.config_file)
        
        if config_path.exists():
            try:
                with open(config_path, 'r') as f:
                    self.config_data = json.load(f)
            except Exception as e:
                print(f"Failed to load config file {config_path}: {e}")
                self._load_default_config()
        else:
            self._load_default_config()
            self.save_config()  # Create default config file
    
    def _load_default_config(self):
        """Load default configuration values"""
        self.config_data = {            "node_manager": {
                "host": os.getenv("NODE_MANAGER_HOST", "localhost"),
                "port": int(os.getenv("NODE_MANAGER_PORT", "8080")),
                "max_ram_cache_gb": float(os.getenv("MAX_RAM_CACHE_GB", "8.0")),
                "model_dir": os.getenv("MODEL_DIR", r"C:\Users\admin\Desktop\BitingLip\biting-lip\managers\model-manager\models"),
                "output_dir": os.getenv("OUTPUT_DIR", "outputs"),
                "device_list": [0, 1, 2, 4],  # Default DirectML devices
                "auto_start_workers": True,
                "parallel_worker_spawn": True,  # Enable parallel worker spawning for faster startup
                "worker_spawn_delay": 0.1  # Reduced delay for faster startup
            },
            "database": {
                "host": os.getenv("DB_HOST", "localhost"),
                "port": int(os.getenv("DB_PORT", "5432")),
                "name": os.getenv("DB_NAME", "node_manager"),
                "user": os.getenv("DB_USER", "postgres"),
                "password": os.getenv("DB_PASSWORD", "postgres"),
                "connection_timeout": 30,
                "max_connections": 10
            },
            "communication": {
                "worker_timeout": 60,
                "heartbeat_interval": 10,
                "message_timeout": 30,
                "retry_attempts": 3,
                "api_routes": {
                    "worker_register": "/api/workers/register",
                    "worker_messages": "/api/workers/{worker_id}/messages",
                    "worker_status": "/api/workers/{worker_id}/status",
                    "submit_task": "/api/tasks/submit",
                    "task_status": "/api/tasks/{task_id}/status"
                }
            },
            "processing": {
                "default_model": "cyberrealistic_pony_v110",
                "task_timeout": 300,  # 5 minutes
                "batch_size": 4,  # Number of parallel tasks
                "scheduler_interval": 0.1,  # Task scheduler interval in seconds
            },
            "memory": {
                "vram_monitoring": True,
                "cleanup_interval": 60,  # seconds
                "memory_threshold": 0.9  # 90% VRAM usage threshold
            },            "logging": {
                "level": "INFO",
                "format": "%(asctime)s - NODE_MANAGER - %(levelname)s - %(message)s",
                "file": "logs/node_manager.log",
                "max_size_mb": 100,
                "backup_count": 5
            }
        }
    
    def save_config(self):
        """Save current configuration to file"""
        try:
            with open(self.config_file, 'w') as f:
                json.dump(self.config_data, f, indent=4)
        except Exception as e:
            print(f"Failed to save config file: {e}")
    
    def get(self, section: str, key: Optional[str] = None, default=None):
        """Get configuration value"""
        if key is None:
            return self.config_data.get(section, default)
        return self.config_data.get(section, {}).get(key, default)
    
    def set(self, section: str, key: str, value: Any):
        """Set configuration value"""
        if section not in self.config_data:
            self.config_data[section] = {}
        self.config_data[section][key] = value
    
    def get_database_config(self) -> Dict[str, Any]:
        """Get database configuration"""
        return self.config_data.get("database", {})
    
    def get_node_manager_config(self) -> Dict[str, Any]:
        """Get node manager configuration"""
        return self.config_data.get("node_manager", {})
    
    def get_communication_config(self) -> Dict[str, Any]:
        """Get communication configuration"""
        return self.config_data.get("communication", {})
    
    def get_processing_config(self) -> Dict[str, Any]:
        """Get processing configuration"""
        return self.config_data.get("processing", {})
    
    def get_memory_config(self) -> Dict[str, Any]:
        """Get memory configuration"""
        return self.config_data.get("memory", {})
    
    def get_logging_config(self) -> Dict[str, Any]:
        """Get logging configuration"""
        return self.config_data.get("logging", {})
