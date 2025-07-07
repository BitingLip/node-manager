"""
DPM++ scheduler implementations for SDXL diffusion models.
Provides DPM++ 2M, DPM++ SDE, and DPM++ variants.
"""

import logging
import torch
from typing import Optional, Dict, Any, Union
try:
    from diffusers.schedulers.scheduling_dpmsolver_multistep import DPMSolverMultistepScheduler
    from diffusers.schedulers.scheduling_dpmsolver_singlestep import DPMSolverSinglestepScheduler
except ImportError:
    # Fallback for older diffusers versions
    DPMSolverMultistepScheduler = None  # type: ignore
    DPMSolverSinglestepScheduler = None  # type: ignore
from .base_scheduler import BaseScheduler

logger = logging.getLogger(__name__)


class DPMPlusPlusSchedulerWrapper(BaseScheduler):
    """DPM++ scheduler wrapper with multiple variants."""
    
    def __init__(
        self,
        variant: str = "multistep",  # multistep, singlestep, sde
        num_train_timesteps: int = 1000,
        beta_start: float = 0.0001,
        beta_end: float = 0.02,
        beta_schedule: str = "linear",
        solver_order: int = 2,
        prediction_type: str = "epsilon",
        thresholding: bool = False,
        dynamic_thresholding_ratio: float = 0.995,
        sample_max_value: float = 1.0,
        algorithm_type: str = "dpmsolver++",
        solver_type: str = "midpoint",
        lower_order_final: bool = True,
        use_karras_sigmas: bool = False,
        **kwargs
    ):
        """Initialize DPM++ scheduler."""
        
        if variant == "multistep":
            if DPMSolverMultistepScheduler is None:
                raise ImportError("DPMSolverMultistepScheduler not available in this diffusers version")
            scheduler = DPMSolverMultistepScheduler(
                num_train_timesteps=num_train_timesteps,
                beta_start=beta_start,
                beta_end=beta_end,
                beta_schedule=beta_schedule,
                solver_order=solver_order,
                prediction_type=prediction_type,
                thresholding=thresholding,
                dynamic_thresholding_ratio=dynamic_thresholding_ratio,
                sample_max_value=sample_max_value,
                algorithm_type=algorithm_type,
                solver_type=solver_type,
                lower_order_final=lower_order_final,
                use_karras_sigmas=use_karras_sigmas,
                **kwargs
            )
        elif variant == "singlestep":
            if DPMSolverSinglestepScheduler is None:
                raise ImportError("DPMSolverSinglestepScheduler not available in this diffusers version")
            scheduler = DPMSolverSinglestepScheduler(
                num_train_timesteps=num_train_timesteps,
                beta_start=beta_start,
                beta_end=beta_end,
                beta_schedule=beta_schedule,
                solver_order=solver_order,
                prediction_type=prediction_type,
                thresholding=thresholding,
                dynamic_thresholding_ratio=dynamic_thresholding_ratio,
                sample_max_value=sample_max_value,
                use_karras_sigmas=use_karras_sigmas,
                **kwargs
            )
        else:
            raise ValueError(f"Unsupported DPM++ variant: {variant}")
        
        super().__init__(scheduler)
        self.variant = variant
        self.solver_order = solver_order
        self.use_karras_sigmas = use_karras_sigmas
        
        logger.info(f"DPM++ {variant} scheduler initialized")
    
    def configure(
        self,
        solver_order: Optional[int] = None,
        use_karras_sigmas: Optional[bool] = None,
        **kwargs
    ) -> None:
        """Configure DPM++ specific parameters."""
        if solver_order is not None:
            self.solver_order = solver_order
        if use_karras_sigmas is not None:
            self.use_karras_sigmas = use_karras_sigmas
        
        logger.debug(f"DPM++ configured: order={self.solver_order}, karras={self.use_karras_sigmas}")
    
    def get_scheduler_config(self) -> Dict[str, Any]:
        """Get DPM++ scheduler configuration."""
        config = getattr(self.scheduler, 'config', {})
        return {
            "scheduler_type": f"DPM++_{self.variant}",
            "variant": self.variant,
            "solver_order": self.solver_order,
            "use_karras_sigmas": self.use_karras_sigmas,
            "num_train_timesteps": getattr(config, 'num_train_timesteps', 1000),
            "beta_start": getattr(config, 'beta_start', 0.0001),
            "beta_end": getattr(config, 'beta_end', 0.02),
            "beta_schedule": getattr(config, 'beta_schedule', 'linear'),
            "prediction_type": getattr(config, 'prediction_type', 'epsilon'),
            "algorithm_type": getattr(config, 'algorithm_type', 'dpmsolver++'),
            "num_inference_steps": self.num_inference_steps
        }
    
    def set_karras_sigmas(self, use_karras: bool = True) -> None:
        """Enable or disable Karras noise schedule."""
        self.use_karras_sigmas = use_karras
        # Update scheduler if it supports this
        if hasattr(self.scheduler, 'use_karras_sigmas'):
            self.scheduler.use_karras_sigmas = use_karras  # type: ignore
        
        logger.debug(f"Karras sigmas {'enabled' if use_karras else 'disabled'}")
    
    def set_solver_order(self, order: int) -> None:
        """Set the solver order (1, 2, or 3)."""
        if order not in [1, 2, 3]:
            raise ValueError("Solver order must be 1, 2, or 3")
        
        self.solver_order = order
        if hasattr(self.scheduler, 'solver_order'):
            self.scheduler.solver_order = order  # type: ignore
        
        logger.debug(f"Solver order set to {order}")


def create_dpm_plus_plus_multistep_scheduler(
    quality_preset: str = "balanced",
    use_karras_sigmas: bool = True,
    solver_order: int = 2,
    **kwargs
) -> DPMPlusPlusSchedulerWrapper:
    """Create a DPM++ Multistep scheduler."""
    from .base_scheduler import SchedulerQualityPreset
    
    preset = SchedulerQualityPreset.get_preset(quality_preset)
    
    scheduler = DPMPlusPlusSchedulerWrapper(
        variant="multistep",
        use_karras_sigmas=use_karras_sigmas,
        solver_order=solver_order,
        **kwargs
    )
    
    logger.info(f"Created DPM++ Multistep scheduler with {quality_preset} quality preset")
    return scheduler


def create_dpm_plus_plus_sde_scheduler(
    quality_preset: str = "balanced",
    use_karras_sigmas: bool = True,
    **kwargs
) -> DPMPlusPlusSchedulerWrapper:
    """Create a DPM++ SDE scheduler."""
    from .base_scheduler import SchedulerQualityPreset
    
    preset = SchedulerQualityPreset.get_preset(quality_preset)
    
    scheduler = DPMPlusPlusSchedulerWrapper(
        variant="singlestep",
        use_karras_sigmas=use_karras_sigmas,
        **kwargs
    )
    
    logger.info(f"Created DPM++ SDE scheduler with {quality_preset} quality preset")
    return scheduler
