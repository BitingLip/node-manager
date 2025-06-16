#!/usr/bin/env python3
"""
Config - Configuration management for worker processes
"""
import json
import os
from pathlib import Path
from typing import Dict, Any, Optional


class Config:
    """Configuration manager for worker settings"""
    
    def __init__(self, config_file: Optional[str] = None):
        self.config_file = config_file or "worker_config.json"
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
        self.config_data = {
            "communication": {
                "node_manager_host": os.getenv("NODE_MANAGER_HOST", "localhost"),
                "node_manager_port": int(os.getenv("NODE_MANAGER_PORT", "8080")),
                "heartbeat_interval": 10,
                "message_timeout": 30,
                "retry_attempts": 3
            },
            "hardware": {
                "monitoring_interval": 5,
                "temperature_threshold": 80,
                "vram_threshold_mb": 7000,
                "ram_threshold_mb": 16000
            },
            "memory": {
                "cleanup_threshold_mb": 500,
                "aggressive_cleanup": False,
                "model_cache_size": 1,
                "ram_staging_enabled": True
            },
            "processing": {
                "max_concurrent_tasks": 1,
                "inference_timeout": 300,
                "default_steps": 20,
                "default_guidance_scale": 7.0,
                "output_format": "png",
                "output_quality": 95
            },
            "clip_processor": {
                "type": "openclip",  # "openclip" or "standard"
                "model": "ViT-L-14",
                "pretrained": "laion2b_s32b_b82k",
                "max_tokens": 248,
                "fallback_chunking": True,
                "truncate_on_failure": False
            },
            "logging": {
                "level": "INFO",
                "max_log_size_mb": 100,
                "backup_count": 5,
                "console_output": True
            }
        }
    
    def save_config(self):
        """Save current configuration to file"""
        try:
            with open(self.config_file, 'w') as f:
                json.dump(self.config_data, f, indent=2)
        except Exception as e:
            print(f"Failed to save config file: {e}")
    
    def get(self, key_path: str, default=None):
        """Get configuration value using dot notation (e.g., 'communication.host')"""
        keys = key_path.split('.')
        value = self.config_data
        
        try:
            for key in keys:
                value = value[key]
            return value
        except (KeyError, TypeError):
            return default
    
    def set(self, key_path: str, value: Any):
        """Set configuration value using dot notation"""
        keys = key_path.split('.')
        config = self.config_data
        
        # Navigate to the parent dictionary
        for key in keys[:-1]:
            if key not in config:
                config[key] = {}
            config = config[key]
        
        # Set the final value
        config[keys[-1]] = value
    
    def get_communication_config(self) -> Dict[str, Any]:
        """Get communication configuration"""
        return self.config_data.get("communication", {})
    
    def get_hardware_config(self) -> Dict[str, Any]:
        """Get hardware monitoring configuration"""
        return self.config_data.get("hardware", {})
    
    def get_memory_config(self) -> Dict[str, Any]:
        """Get memory management configuration"""
        return self.config_data.get("memory", {})
    
    def get_processing_config(self) -> Dict[str, Any]:
        """Get processing configuration"""
        return self.config_data.get("processing", {})
    
    def get_logging_config(self) -> Dict[str, Any]:
        """Get logging configuration"""
        return self.config_data.get("logging", {})
    
    def get_clip_config(self) -> Dict[str, Any]:
        """Get OpenCLIP configuration"""
        return self.config_data.get("clip_processor", {})
    
    def update_from_env(self):
        """Update configuration from environment variables"""
        env_mappings = {
            "NODE_MANAGER_HOST": "communication.node_manager_host",
            "NODE_MANAGER_PORT": "communication.node_manager_port",
            "LOG_LEVEL": "logging.level"
        }
        
        for env_var, config_path in env_mappings.items():
            env_value = os.getenv(env_var)
            if env_value is not None:
                # Try to convert to appropriate type
                if config_path.endswith(('.port', '.timeout', '.interval', '.threshold')):
                    try:
                        env_value = int(env_value)
                    except ValueError:
                        continue
                elif config_path.endswith(('.enabled', '.aggressive_cleanup', '.console_output')):
                    env_value = env_value.lower() in ('true', '1', 'yes', 'on')
                
                self.set(config_path, env_value)
    
    def validate_config(self) -> bool:
        """Validate configuration values"""
        issues = []
        
        # Validate communication config
        comm_config = self.get_communication_config()
        if not comm_config.get('node_manager_host'):
            issues.append("Node manager host is required")
        if not isinstance(comm_config.get('node_manager_port'), int):
            issues.append("Node manager port must be an integer")
        
        # Validate hardware config
        hw_config = self.get_hardware_config()
        if hw_config.get('monitoring_interval', 0) <= 0:
            issues.append("Hardware monitoring interval must be positive")
        
        if issues:
            for issue in issues:
                print(f"Config validation error: {issue}")
            return False
        
        return True
    
    def __str__(self) -> str:
        """String representation of configuration"""
        return json.dumps(self.config_data, indent=2)
