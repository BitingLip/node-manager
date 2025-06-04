"""
Worker Configuration
Configuration management for worker-specific settings
Handles resource allocation, model configurations, and worker parameters
"""

from typing import Dict, Any, List, Optional
import structlog

logger = structlog.get_logger(__name__)


class WorkerConfig:
    """
    Worker-specific configuration management
    Handles resource limits, model settings, and worker parameters
    """
    
    def __init__(self, worker_type: str, base_config: Optional[Dict[str, Any]] = None):
        """Initialize worker configuration"""
        self.worker_type = worker_type
        self.config_data = base_config or {}
        
        # Set default configurations based on worker type
        self._set_defaults()
        
        logger.info(f"WorkerConfig initialized for {worker_type}")
    
    def _set_defaults(self):
        """Set default configuration values based on worker type"""
        # TODO: Set worker-type specific defaults
        defaults = {
            "llm": {
                "max_context_length": 4096,
                "batch_size": 1,
                "gpu_memory_gb": 4,
                "cpu_cores": 2,
                "ram_gb": 8,
                "model_cache_size": 2
            },
            "stable_diffusion": {
                "max_resolution": "512x512",
                "batch_size": 1,
                "gpu_memory_gb": 8,
                "cpu_cores": 2,
                "ram_gb": 16,
                "model_cache_size": 1
            },
            "tts": {
                "sample_rate": 22050,
                "batch_size": 1,
                "gpu_memory_gb": 2,
                "cpu_cores": 1,
                "ram_gb": 4,
                "model_cache_size": 1
            }
        }
        
        if self.worker_type in defaults:
            for key, value in defaults[self.worker_type].items():
                if key not in self.config_data:
                    self.config_data[key] = value
    
    def get(self, key: str, default: Any = None) -> Any:
        """Get configuration value"""
        return self.config_data.get(key, default)
    
    def set(self, key: str, value: Any):
        """Set configuration value"""
        self.config_data[key] = value
    
    def update(self, config_dict: Dict[str, Any]):
        """Update configuration with dictionary"""
        self.config_data.update(config_dict)
    
    def get_resource_requirements(self) -> Dict[str, Any]:
        """Get resource requirements for this worker"""
        # TODO: Return resource requirements
        return {
            "gpu_memory_gb": self.get("gpu_memory_gb", 4),
            "cpu_cores": self.get("cpu_cores", 1),
            "ram_gb": self.get("ram_gb", 4),
            "storage_gb": self.get("storage_gb", 10)
        }
    
    def get_model_config(self, model_name: str) -> Dict[str, Any]:
        """Get configuration for a specific model"""
        # TODO: Return model-specific configuration
        models_config = self.get("models", {})
        return models_config.get(model_name, {})
    
    def validate(self) -> bool:
        """Validate worker configuration"""
        # TODO: Implement configuration validation
        # 1. Check required fields
        # 2. Validate resource limits
        # 3. Check model configurations
        # 4. Validate worker-specific settings
        return True
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert configuration to dictionary"""
        return self.config_data.copy()
    
    @classmethod
    def from_dict(cls, worker_type: str, config_dict: Dict[str, Any]) -> 'WorkerConfig':
        """Create WorkerConfig from dictionary"""
        return cls(worker_type, config_dict)
    
    @classmethod
    def get_default_config(cls, worker_type: str) -> 'WorkerConfig':
        """Get default configuration for worker type"""
        return cls(worker_type)
