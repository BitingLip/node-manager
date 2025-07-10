"""
Factory Manager for SDXL Workers System
=======================================

Migrated from schedulers/scheduler_factory.py
Factory pattern implementation for creating and managing different sampling schedulers.
"""

import logging
from typing import Dict, Any, Optional, Type, Union, List
from dataclasses import dataclass

from diffusers import (
    DDIMScheduler,
    DPMSolverMultistepScheduler,
    DPMSolverSinglestepScheduler,
    EulerDiscreteScheduler,
    EulerAncestralDiscreteScheduler,
    HeunDiscreteScheduler,
    LMSDiscreteScheduler,
    KDPM2DiscreteScheduler,
    KDPM2AncestralDiscreteScheduler,
    UniPCMultistepScheduler,
    SchedulerMixin
)


@dataclass
class SchedulerConfig:
    """Configuration for a diffusion scheduler."""
    scheduler_type: str
    num_inference_steps: int = 20
    guidance_scale: float = 7.5
    use_karras_sigmas: bool = False
    timestep_spacing: str = "leading"  # "leading", "linspace", "trailing"
    algorithm_type: str = "dpmsolver++"  # For DPM solvers
    solver_order: int = 2  # For DPM solvers
    thresholding: bool = False
    dynamic_thresholding_ratio: float = 0.995
    sample_max_value: float = 1.0
    prediction_type: str = "epsilon"
    beta_start: float = 0.00085
    beta_end: float = 0.012
    beta_schedule: str = "scaled_linear"
    clip_sample: bool = False


class FactoryManager:
    """
    Factory manager for creating and configuring diffusion schedulers.
    
    This manager handles the creation and configuration of different scheduler types
    for SDXL inference operations.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.initialized = False
        
        # Created scheduler instances
        self.created_schedulers: Dict[str, SchedulerMixin] = {}
        
        # Registry of available schedulers
        self.schedulers = {
            "ddim": DDIMScheduler,
            "dpm_multistep": DPMSolverMultistepScheduler,
            "dpm_singlestep": DPMSolverSinglestepScheduler,
            "euler": EulerDiscreteScheduler,
            "euler_ancestral": EulerAncestralDiscreteScheduler,
            "heun": HeunDiscreteScheduler,
            "lms": LMSDiscreteScheduler,
            "kdpm2": KDPM2DiscreteScheduler,
            "kdpm2_ancestral": KDPM2AncestralDiscreteScheduler,
            "unipc": UniPCMultistepScheduler
        }
        
        # Default configurations
        self.default_configs = {
            "ddim": {
                "num_train_timesteps": 1000,
                "beta_start": 0.00085,
                "beta_end": 0.012,
                "beta_schedule": "scaled_linear",
                "clip_sample": False,
                "prediction_type": "epsilon"
            },
            "dpm_multistep": {
                "num_train_timesteps": 1000,
                "beta_start": 0.00085,
                "beta_end": 0.012,
                "beta_schedule": "scaled_linear",
                "algorithm_type": "dpmsolver++",
                "solver_order": 2,
                "prediction_type": "epsilon"
            },
            "euler": {
                "num_train_timesteps": 1000,
                "beta_start": 0.00085,
                "beta_end": 0.012,
                "beta_schedule": "scaled_linear",
                "prediction_type": "epsilon"
            }
        }
        
    async def initialize(self) -> bool:
        """Initialize factory manager."""
        try:
            self.logger.info("Initializing factory manager...")
            self.initialized = True
            self.logger.info("Factory manager initialized successfully")
            return True
        except Exception as e:
            self.logger.error(f"Factory manager initialization failed: {e}")
            return False
    
    async def create_scheduler(self, scheduler_type: str, config: Dict[str, Any]) -> Dict[str, Any]:
        """Create a scheduler instance."""
        try:
            if scheduler_type not in self.schedulers:
                raise ValueError(f"Unknown scheduler type: {scheduler_type}")
            
            scheduler_class = self.schedulers[scheduler_type]
            
            # Use default config and override with provided config
            scheduler_config = self.default_configs.get(scheduler_type, {}).copy()
            scheduler_config.update(config)
            
            # Create scheduler instance
            scheduler = scheduler_class(**scheduler_config)
            
            # Store the created scheduler
            scheduler_id = f"{scheduler_type}_{len(self.created_schedulers)}"
            self.created_schedulers[scheduler_id] = scheduler
            
            self.logger.info(f"Created scheduler: {scheduler_type} with ID: {scheduler_id}")
            
            return {
                "scheduler_id": scheduler_id,
                "scheduler_type": scheduler_type,
                "config": scheduler_config,
                "created": True
            }
            
        except Exception as e:
            self.logger.error(f"Failed to create scheduler {scheduler_type}: {e}")
            return {"error": str(e), "created": False}
    
    async def get_available_schedulers(self) -> Dict[str, Any]:
        """Get list of available schedulers."""
        return {
            "available_schedulers": list(self.schedulers.keys()),
            "scheduler_count": len(self.schedulers),
            "created_schedulers": list(self.created_schedulers.keys())
        }
    
    async def get_scheduler_presets(self) -> Dict[str, Any]:
        """Get predefined scheduler presets."""
        presets = {
            "fast": {
                "scheduler_type": "euler",
                "num_inference_steps": 10,
                "guidance_scale": 5.0
            },
            "balanced": {
                "scheduler_type": "dpm_multistep",
                "num_inference_steps": 20,
                "guidance_scale": 7.5
            },
            "quality": {
                "scheduler_type": "ddim",
                "num_inference_steps": 30,
                "guidance_scale": 10.0
            }
        }
        
        return {
            "presets": presets,
            "preset_count": len(presets)
        }
    
    async def create_from_preset(self, preset_name: str, overrides: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Create scheduler from a preset configuration."""
        try:
            presets_response = await self.get_scheduler_presets()
            presets = presets_response.get("presets", {})
            
            if preset_name not in presets:
                raise ValueError(f"Unknown preset: {preset_name}")
            
            preset_config = presets[preset_name].copy()
            scheduler_type = preset_config.pop("scheduler_type")
            
            # Apply overrides
            if overrides:
                preset_config.update(overrides)
            
            result = await self.create_scheduler(scheduler_type, preset_config)
            result["preset_used"] = preset_name
            
            return result
            
        except Exception as e:
            self.logger.error(f"Failed to create scheduler from preset {preset_name}: {e}")
            return {"error": str(e), "created": False}
    
    async def get_scheduler_by_id(self, scheduler_id: str) -> Optional[SchedulerMixin]:
        """Get a created scheduler by ID."""
        return self.created_schedulers.get(scheduler_id)
    
    async def remove_scheduler(self, scheduler_id: str) -> bool:
        """Remove a created scheduler."""
        try:
            if scheduler_id in self.created_schedulers:
                del self.created_schedulers[scheduler_id]
                self.logger.info(f"Removed scheduler: {scheduler_id}")
                return True
            return False
        except Exception as e:
            self.logger.error(f"Failed to remove scheduler {scheduler_id}: {e}")
            return False
    
    async def get_status(self) -> Dict[str, Any]:
        """Get factory manager status."""
        return {
            "initialized": self.initialized,
            "available_scheduler_types": len(self.schedulers),
            "created_schedulers": len(self.created_schedulers),
            "scheduler_types": list(self.schedulers.keys()),
            "created_scheduler_ids": list(self.created_schedulers.keys())
        }
    
    async def cleanup(self) -> None:
        """Clean up factory manager resources."""
        try:
            self.logger.info("Cleaning up factory manager...")
            
            # Clear all created schedulers
            self.created_schedulers.clear()
            
            self.initialized = False
            self.logger.info("Factory manager cleanup complete")
        except Exception as e:
            self.logger.error(f"Factory manager cleanup error: {e}")


# Factory function for creating factory manager
def create_factory_manager(config: Optional[Dict[str, Any]] = None) -> FactoryManager:
    """
    Factory function to create a factory manager instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        FactoryManager instance
    """
    return FactoryManager(config or {})