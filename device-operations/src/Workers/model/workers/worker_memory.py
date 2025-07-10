"""
Memory Worker for SDXL Workers System
=====================================

Consolidates model_loader.py, unified_model_manager.py, and gpu_model_manager.py
Handles loading and unloading, automatic memory management across devices.
"""

import logging
from typing import Dict, Any, Optional, List

logger = logging.getLogger(__name__)


class MemoryWorker:
    """
    Memory management worker for loading and unloading models,
    automatic memory management across devices.
    
    This worker consolidates functionality from model_loader.py,
    unified_model_manager.py, and gpu_model_manager.py.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.loaded_models: Dict[str, Any] = {}
        self.memory_usage: Dict[str, int] = {}
        self.memory_limit = config.get("memory_limit_mb", 8192)  # 8GB default
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize memory worker."""
        try:
            self.logger.info("Initializing memory worker...")
            self.initialized = True
            self.logger.info("Memory worker initialized successfully")
            return True
        except Exception as e:
            self.logger.error(f"Memory worker initialization failed: {e}")
            return False
    
    async def load_model(self, model_data: Dict[str, Any]) -> Dict[str, Any]:
        """Load a model into memory."""
        try:
            model_name = model_data.get("name", "default")
            model_path = model_data.get("path", "")
            model_type = model_data.get("type", "unknown")
            
            # Check memory constraints
            estimated_size = model_data.get("estimated_size_mb", 1024)
            if not self._check_memory_available(estimated_size):
                return {
                    "loaded": False,
                    "error": f"Insufficient memory. Need {estimated_size}MB, available: {self._get_available_memory()}MB"
                }
            
            # Simulate model loading
            self.loaded_models[model_name] = {
                "path": model_path,
                "type": model_type,
                "size_mb": estimated_size,
                "loaded": True
            }
            
            self.memory_usage[model_name] = estimated_size
            
            self.logger.info(f"Loaded model: {model_name} ({model_type})")
            
            return {
                "loaded": True,
                "model_name": model_name,
                "model_type": model_type,
                "size_mb": estimated_size
            }
        except Exception as e:
            self.logger.error(f"Failed to load model: {e}")
            return {"loaded": False, "error": str(e)}
    
    async def unload_model(self, model_data: Dict[str, Any]) -> Dict[str, Any]:
        """Unload a model from memory."""
        try:
            model_name = model_data.get("name", "default")
            
            if model_name in self.loaded_models:
                size_mb = self.memory_usage.get(model_name, 0)
                del self.loaded_models[model_name]
                del self.memory_usage[model_name]
                
                self.logger.info(f"Unloaded model: {model_name}")
                
                return {
                    "unloaded": True,
                    "model_name": model_name,
                    "freed_mb": size_mb
                }
            else:
                return {
                    "unloaded": False,
                    "error": f"Model {model_name} not found"
                }
        except Exception as e:
            self.logger.error(f"Failed to unload model: {e}")
            return {"unloaded": False, "error": str(e)}
    
    async def get_model_info(self) -> Dict[str, Any]:
        """Get information about loaded models."""
        return {
            "loaded_models": self.loaded_models,
            "memory_usage_mb": sum(self.memory_usage.values()),
            "memory_limit_mb": self.memory_limit,
            "available_memory_mb": self._get_available_memory(),
            "model_count": len(self.loaded_models)
        }
    
    async def optimize_memory(self) -> Dict[str, Any]:
        """Optimize memory usage by unloading unused models."""
        try:
            # Simple optimization: unload models if we're over 80% memory usage
            current_usage = sum(self.memory_usage.values())
            usage_percentage = (current_usage / self.memory_limit) * 100
            
            if usage_percentage > 80:
                # Unload oldest models (simple FIFO strategy)
                models_to_unload = []
                target_usage = self.memory_limit * 0.7  # Target 70% usage
                
                for model_name in list(self.loaded_models.keys()):
                    if current_usage <= target_usage:
                        break
                    
                    size_mb = self.memory_usage.get(model_name, 0)
                    models_to_unload.append(model_name)
                    current_usage -= size_mb
                
                # Unload selected models
                for model_name in models_to_unload:
                    await self.unload_model({"name": model_name})
                
                return {
                    "optimized": True,
                    "unloaded_models": models_to_unload,
                    "freed_mb": sum(self.memory_usage.get(name, 0) for name in models_to_unload)
                }
            else:
                return {
                    "optimized": False,
                    "reason": f"Memory usage ({usage_percentage:.1f}%) below threshold"
                }
        except Exception as e:
            self.logger.error(f"Memory optimization failed: {e}")
            return {"optimized": False, "error": str(e)}
    
    def _check_memory_available(self, required_mb: int) -> bool:
        """Check if enough memory is available."""
        current_usage = sum(self.memory_usage.values())
        return (current_usage + required_mb) <= self.memory_limit
    
    def _get_available_memory(self) -> int:
        """Get available memory in MB."""
        current_usage = sum(self.memory_usage.values())
        return max(0, self.memory_limit - current_usage)
    
    async def get_status(self) -> Dict[str, Any]:
        """Get memory worker status."""
        return {
            "initialized": self.initialized,
            "loaded_models": len(self.loaded_models),
            "memory_usage_mb": sum(self.memory_usage.values()),
            "memory_limit_mb": self.memory_limit,
            "available_memory_mb": self._get_available_memory()
        }
    
    async def cleanup(self) -> None:
        """Clean up memory worker resources."""
        try:
            self.logger.info("Cleaning up memory worker...")
            
            # Unload all models
            for model_name in list(self.loaded_models.keys()):
                await self.unload_model({"name": model_name})
            
            self.initialized = False
            self.logger.info("Memory worker cleanup complete")
        except Exception as e:
            self.logger.error(f"Memory worker cleanup error: {e}")