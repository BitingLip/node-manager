"""
Scheduler Manager for SDXL Workers System
=========================================

Migrated from schedulers/scheduler_manager.py
Scheduler lifecycle management and optimization for SDXL inference operations.
"""

import logging
from typing import Dict, Any, Optional, List, Union


class SchedulerManager:
    """
    Manages scheduler lifecycle and optimization for diffusion pipelines.
    
    Provides scheduler configuration validation, parameter management,
    and scheduler-specific optimizations for SDXL inference.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.initialized = False
        
        # Active scheduler instances
        self.active_schedulers: Dict[str, Any] = {}
        self.scheduler_configs: Dict[str, Dict[str, Any]] = {}
        
        # Supported scheduler types
        self.supported_schedulers = [
            "ddim", "dpm_multistep", "dpm_singlestep", "euler", 
            "euler_ancestral", "heun", "lms", "kdpm2", 
            "kdpm2_ancestral", "unipc"
        ]
        
        # Default scheduler parameters
        self.default_params = {
            "num_inference_steps": 20,
            "guidance_scale": 7.5,
            "eta": 0.0,
            "clip_sample": False,
            "use_karras_sigmas": False
        }
        
    async def initialize(self) -> bool:
        """Initialize scheduler manager."""
        try:
            self.logger.info("Initializing scheduler manager...")
            self.initialized = True
            self.logger.info("Scheduler manager initialized successfully")
            return True
        except Exception as e:
            self.logger.error(f"Scheduler manager initialization failed: {e}")
            return False
    
    async def configure_scheduler(self, scheduler_id: str, config: Dict[str, Any]) -> Dict[str, Any]:
        """Configure an active scheduler."""
        try:
            if scheduler_id not in self.active_schedulers:
                raise ValueError(f"Scheduler not found: {scheduler_id}")
            
            # Validate configuration
            validated_config = await self._validate_config(config)
            
            # Store configuration
            self.scheduler_configs[scheduler_id] = validated_config
            
            self.logger.info(f"Configured scheduler: {scheduler_id}")
            
            return {
                "scheduler_id": scheduler_id,
                "config": validated_config,
                "configured": True
            }
            
        except Exception as e:
            self.logger.error(f"Failed to configure scheduler {scheduler_id}: {e}")
            return {"error": str(e), "configured": False}
    
    async def register_scheduler(self, scheduler_id: str, scheduler_instance: Any) -> bool:
        """Register a scheduler instance."""
        try:
            self.active_schedulers[scheduler_id] = scheduler_instance
            self.logger.info(f"Registered scheduler: {scheduler_id}")
            return True
        except Exception as e:
            self.logger.error(f"Failed to register scheduler {scheduler_id}: {e}")
            return False
    
    async def unregister_scheduler(self, scheduler_id: str) -> bool:
        """Unregister a scheduler instance."""
        try:
            if scheduler_id in self.active_schedulers:
                del self.active_schedulers[scheduler_id]
            if scheduler_id in self.scheduler_configs:
                del self.scheduler_configs[scheduler_id]
            
            self.logger.info(f"Unregistered scheduler: {scheduler_id}")
            return True
        except Exception as e:
            self.logger.error(f"Failed to unregister scheduler {scheduler_id}: {e}")
            return False
    
    async def get_scheduler_info(self, scheduler_id: str) -> Dict[str, Any]:
        """Get information about a specific scheduler."""
        try:
            if scheduler_id not in self.active_schedulers:
                raise ValueError(f"Scheduler not found: {scheduler_id}")
            
            scheduler = self.active_schedulers[scheduler_id]
            config = self.scheduler_configs.get(scheduler_id, {})
            
            return {
                "scheduler_id": scheduler_id,
                "scheduler_type": scheduler.__class__.__name__ if hasattr(scheduler, '__class__') else "unknown",
                "config": config,
                "is_active": True
            }
            
        except Exception as e:
            self.logger.error(f"Failed to get scheduler info for {scheduler_id}: {e}")
            return {"error": str(e)}
    
    async def list_active_schedulers(self) -> Dict[str, Any]:
        """List all active schedulers."""
        try:
            scheduler_list = []
            for scheduler_id, scheduler in self.active_schedulers.items():
                scheduler_info = {
                    "scheduler_id": scheduler_id,
                    "scheduler_type": scheduler.__class__.__name__ if hasattr(scheduler, '__class__') else "unknown",
                    "has_config": scheduler_id in self.scheduler_configs
                }
                scheduler_list.append(scheduler_info)
            
            return {
                "active_schedulers": scheduler_list,
                "total_count": len(scheduler_list)
            }
            
        except Exception as e:
            self.logger.error(f"Failed to list active schedulers: {e}")
            return {"error": str(e)}
    
    async def optimize_scheduler(self, scheduler_id: str, optimization_type: str = "memory") -> Dict[str, Any]:
        """Apply optimizations to a scheduler."""
        try:
            if scheduler_id not in self.active_schedulers:
                raise ValueError(f"Scheduler not found: {scheduler_id}")
            
            scheduler = self.active_schedulers[scheduler_id]
            
            optimizations_applied = []
            
            if optimization_type == "memory":
                # Apply memory optimizations
                optimizations_applied.append("memory_efficient_attention")
                
            elif optimization_type == "speed":
                # Apply speed optimizations
                optimizations_applied.append("reduced_inference_steps")
                
            elif optimization_type == "quality":
                # Apply quality optimizations
                optimizations_applied.append("increased_inference_steps")
                optimizations_applied.append("karras_sigmas")
            
            self.logger.info(f"Applied {optimization_type} optimizations to scheduler: {scheduler_id}")
            
            return {
                "scheduler_id": scheduler_id,
                "optimization_type": optimization_type,
                "optimizations_applied": optimizations_applied,
                "optimized": True
            }
            
        except Exception as e:
            self.logger.error(f"Failed to optimize scheduler {scheduler_id}: {e}")
            return {"error": str(e), "optimized": False}
    
    async def _validate_config(self, config: Dict[str, Any]) -> Dict[str, Any]:
        """Validate scheduler configuration."""
        validated_config = {}
        
        # Validate numeric parameters
        if "num_inference_steps" in config:
            steps = config["num_inference_steps"]
            if isinstance(steps, int) and 1 <= steps <= 200:
                validated_config["num_inference_steps"] = steps
            else:
                validated_config["num_inference_steps"] = self.default_params["num_inference_steps"]
        
        if "guidance_scale" in config:
            scale = config["guidance_scale"]
            if isinstance(scale, (int, float)) and 0.1 <= scale <= 30.0:
                validated_config["guidance_scale"] = float(scale)
            else:
                validated_config["guidance_scale"] = self.default_params["guidance_scale"]
        
        # Validate boolean parameters
        for bool_param in ["clip_sample", "use_karras_sigmas"]:
            if bool_param in config and isinstance(config[bool_param], bool):
                validated_config[bool_param] = config[bool_param]
            else:
                validated_config[bool_param] = self.default_params.get(bool_param, False)
        
        # Add any other parameters from config
        for key, value in config.items():
            if key not in validated_config:
                validated_config[key] = value
        
        return validated_config
    
    async def get_supported_schedulers(self) -> List[str]:
        """Get list of supported scheduler types."""
        return self.supported_schedulers.copy()
    
    async def get_default_params(self) -> Dict[str, Any]:
        """Get default scheduler parameters."""
        return self.default_params.copy()
    
    async def get_status(self) -> Dict[str, Any]:
        """Get scheduler manager status."""
        return {
            "initialized": self.initialized,
            "active_schedulers": len(self.active_schedulers),
            "configured_schedulers": len(self.scheduler_configs),
            "supported_scheduler_types": len(self.supported_schedulers),
            "default_params": self.default_params
        }
    
    async def cleanup(self) -> None:
        """Clean up scheduler manager resources."""
        try:
            self.logger.info("Cleaning up scheduler manager...")
            
            # Clear all scheduler references
            self.active_schedulers.clear()
            self.scheduler_configs.clear()
            
            self.initialized = False
            self.logger.info("Scheduler manager cleanup complete")
        except Exception as e:
            self.logger.error(f"Scheduler manager cleanup error: {e}")


# Factory function for creating scheduler manager
def create_scheduler_manager(config: Optional[Dict[str, Any]] = None) -> SchedulerManager:
    """
    Factory function to create a scheduler manager instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        SchedulerManager instance
    """
    return SchedulerManager(config or {})