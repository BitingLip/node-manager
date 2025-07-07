"""
Euler scheduler implementation for SDXL diffusion models.
Provides both Euler Ancestral and regular Euler sampling.
"""

import logging
import torch
from typing import Optional, Dict, Any, Union
from diffusers import EulerAncestralDiscreteScheduler, EulerDiscreteScheduler
from .base_scheduler import BaseScheduler

logger = logging.getLogger(__name__)


class EulerSchedulerWrapper(BaseScheduler):
    """Euler scheduler wrapper with ancestral and regular modes."""
    
    def __init__(
        self,
        ancestral: bool = True,
        num_train_timesteps: int = 1000,
        beta_start: float = 0.0001,
        beta_end: float = 0.02,
        beta_schedule: str = "linear",
        prediction_type: str = "epsilon",
        **kwargs
    ):
        """Initialize Euler scheduler."""
        if ancestral:
            scheduler = EulerAncestralDiscreteScheduler(
                num_train_timesteps=num_train_timesteps,
                beta_start=beta_start,
                beta_end=beta_end,
                beta_schedule=beta_schedule,
                prediction_type=prediction_type,
                **kwargs
            )
        else:
            scheduler = EulerDiscreteScheduler(
                num_train_timesteps=num_train_timesteps,
                beta_start=beta_start,
                beta_end=beta_end,
                beta_schedule=beta_schedule,
                prediction_type=prediction_type,
                **kwargs
            )
        
        super().__init__(scheduler)
        self.ancestral = ancestral
        
        logger.info(f"Euler {'Ancestral' if ancestral else 'Discrete'} scheduler initialized")
    
    def configure(self, **kwargs) -> None:
        """Configure Euler specific parameters."""
        # Euler schedulers don't have many configurable parameters
        logger.debug("Euler scheduler configured")
    
    def get_scheduler_config(self) -> Dict[str, Any]:
        """Get Euler scheduler configuration."""
        config = getattr(self.scheduler, 'config', {})
        return {
            "scheduler_type": f"Euler{'Ancestral' if self.ancestral else 'Discrete'}",
            "ancestral": self.ancestral,
            "num_train_timesteps": getattr(config, 'num_train_timesteps', 1000),
            "beta_start": getattr(config, 'beta_start', 0.0001),
            "beta_end": getattr(config, 'beta_end', 0.02),
            "beta_schedule": getattr(config, 'beta_schedule', 'linear'),
            "prediction_type": getattr(config, 'prediction_type', 'epsilon'),
            "num_inference_steps": self.num_inference_steps
        }


def create_euler_ancestral_scheduler(**kwargs) -> EulerSchedulerWrapper:
    """Create an Euler Ancestral scheduler."""
    return EulerSchedulerWrapper(ancestral=True, **kwargs)


def create_euler_discrete_scheduler(**kwargs) -> EulerSchedulerWrapper:
    """Create an Euler Discrete scheduler."""
    return EulerSchedulerWrapper(ancestral=False, **kwargs)
