"""
Scheduler Factory Module
========================

Creates and configures diffusion schedulers for SDXL inference with advanced
settings including Karras sigmas, timestep spacing, and custom configurations.
"""

import logging
from typing import Dict, Any, Optional, Type, Union
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


class SchedulerFactory:
    """
    Factory for creating and configuring diffusion schedulers.
    
    Provides easy creation of various schedulers with optimized settings
    for SDXL inference and custom configuration options.
    """
    
    # Registry of available schedulers
    SCHEDULERS = {
        "DDIMScheduler": DDIMScheduler,
        "DPMSolverMultistepScheduler": DPMSolverMultistepScheduler,
        "DPMSolverSinglestepScheduler": DPMSolverSinglestepScheduler,
        "EulerDiscreteScheduler": EulerDiscreteScheduler,
        "EulerAncestralDiscreteScheduler": EulerAncestralDiscreteScheduler,
        "HeunDiscreteScheduler": HeunDiscreteScheduler,
        "LMSDiscreteScheduler": LMSDiscreteScheduler,
        "KDPM2DiscreteScheduler": KDPM2DiscreteScheduler,
        "KDPM2AncestralDiscreteScheduler": KDPM2AncestralDiscreteScheduler,
        "UniPCMultistepScheduler": UniPCMultistepScheduler
    }
    
    # Default configurations for each scheduler
    DEFAULT_CONFIGS = {
        "DDIMScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "clip_sample": False,
            "set_alpha_to_one": False,
            "steps_offset": 1,
            "prediction_type": "epsilon",
            "thresholding": False,
            "dynamic_thresholding_ratio": 0.995,
            "clip_sample_range": 1.0,
            "sample_max_value": 1.0,
            "timestep_spacing": "leading",
            "rescale_betas_zero_snr": False
        },
        "DPMSolverMultistepScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "trained_betas": None,
            "prediction_type": "epsilon",
            "thresholding": False,
            "dynamic_thresholding_ratio": 0.995,
            "sample_max_value": 1.0,
            "algorithm_type": "dpmsolver++",
            "solver_type": "midpoint",
            "lower_order_final": True,
            "use_karras_sigmas": False,
            "timestep_spacing": "leading",
            "steps_offset": 0
        },
        "DPMSolverSinglestepScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "trained_betas": None,
            "prediction_type": "epsilon",
            "thresholding": False,
            "dynamic_thresholding_ratio": 0.995,
            "sample_max_value": 1.0,
            "algorithm_type": "dpmsolver++",
            "solver_type": "midpoint",
            "lower_order_final": True,
            "use_karras_sigmas": False,
            "timestep_spacing": "leading",
            "steps_offset": 0
        },
        "EulerDiscreteScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "trained_betas": None,
            "prediction_type": "epsilon",
            "interpolation_type": "linear",
            "use_karras_sigmas": False,
            "timestep_spacing": "leading",
            "steps_offset": 0
        },
        "EulerAncestralDiscreteScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "trained_betas": None,
            "prediction_type": "epsilon",
            "timestep_spacing": "leading",
            "steps_offset": 0
        },
        "HeunDiscreteScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "trained_betas": None,
            "prediction_type": "epsilon",
            "use_karras_sigmas": False,
            "timestep_spacing": "leading",
            "steps_offset": 0
        },
        "LMSDiscreteScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "trained_betas": None,
            "prediction_type": "epsilon",
            "timestep_spacing": "leading",
            "steps_offset": 0,
            "use_karras_sigmas": False
        },
        "KDPM2DiscreteScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "trained_betas": None,
            "prediction_type": "epsilon",
            "timestep_spacing": "leading",
            "steps_offset": 0
        },
        "KDPM2AncestralDiscreteScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "trained_betas": None,
            "prediction_type": "epsilon",
            "timestep_spacing": "leading",
            "steps_offset": 0
        },
        "UniPCMultistepScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "trained_betas": None,
            "prediction_type": "epsilon",
            "thresholding": False,
            "dynamic_thresholding_ratio": 0.995,
            "sample_max_value": 1.0,
            "predict_x0": True,
            "solver_order": 2,
            "lower_order_final": True,
            "disable_corrector": [],
            "solver_type": "bh2",
            "timestep_spacing": "leading",
            "steps_offset": 0
        }
    }
    
    # Recommended settings for different quality levels
    QUALITY_PRESETS = {
        "fast": {
            "num_inference_steps": 10,
            "guidance_scale": 7.5,
            "scheduler_type": "DPMSolverMultistepScheduler",
            "use_karras_sigmas": False
        },
        "balanced": {
            "num_inference_steps": 20,
            "guidance_scale": 7.5,
            "scheduler_type": "DPMSolverMultistepScheduler",
            "use_karras_sigmas": True
        },
        "quality": {
            "num_inference_steps": 30,
            "guidance_scale": 7.5,
            "scheduler_type": "EulerAncestralDiscreteScheduler",
            "use_karras_sigmas": True
        },
        "ultra": {
            "num_inference_steps": 50,
            "guidance_scale": 7.5,
            "scheduler_type": "HeunDiscreteScheduler",
            "use_karras_sigmas": True
        }
    }
    
    def __init__(self):
        self.logger = logging.getLogger(__name__)
    
    def create_scheduler(self, config: Union[SchedulerConfig, Dict[str, Any]]) -> SchedulerMixin:
        """
        Create a scheduler from configuration.
        
        Args:
            config: Scheduler configuration
            
        Returns:
            Configured scheduler instance
        """
        if isinstance(config, dict):
            config = SchedulerConfig(**config)
        
        scheduler_class = self.SCHEDULERS.get(config.scheduler_type)
        if not scheduler_class:
            raise ValueError(f"Unknown scheduler type: {config.scheduler_type}")
        
        # Get default configuration for the scheduler
        default_config = self.DEFAULT_CONFIGS.get(config.scheduler_type, {}).copy()
        
        # Update with custom settings
        scheduler_kwargs = self._build_scheduler_kwargs(config, default_config)
        
        try:
            scheduler = scheduler_class(**scheduler_kwargs)
            self.logger.info(f"Created scheduler: {config.scheduler_type}")
            return scheduler
            
        except Exception as e:
            self.logger.error(f"Failed to create scheduler {config.scheduler_type}: {str(e)}")
            raise
    
    def create_from_preset(self, preset_name: str, **overrides) -> SchedulerMixin:
        """
        Create a scheduler from a quality preset.
        
        Args:
            preset_name: Name of the quality preset
            **overrides: Additional configuration overrides
            
        Returns:
            Configured scheduler instance
        """
        if preset_name not in self.QUALITY_PRESETS:
            raise ValueError(f"Unknown preset: {preset_name}")
        
        preset_config = self.QUALITY_PRESETS[preset_name].copy()
        preset_config.update(overrides)
        
        return self.create_scheduler(preset_config)
    
    def create_from_pipeline(self, pipeline, scheduler_type: str, 
                           num_inference_steps: int = 20, **kwargs) -> SchedulerMixin:
        """
        Create a scheduler compatible with an existing pipeline.
        
        Args:
            pipeline: Diffusion pipeline
            scheduler_type: Type of scheduler to create
            num_inference_steps: Number of inference steps
            **kwargs: Additional scheduler arguments
            
        Returns:
            Configured scheduler instance
        """
        # Get scheduler configuration from pipeline
        original_scheduler = pipeline.scheduler
        
        scheduler_class = self.SCHEDULERS.get(scheduler_type)
        if not scheduler_class:
            raise ValueError(f"Unknown scheduler type: {scheduler_type}")
        
        # Extract compatible configuration
        compatible_config = self._extract_compatible_config(original_scheduler, scheduler_class)
        compatible_config.update(kwargs)
        
        try:
            scheduler = scheduler_class.from_config(original_scheduler.config, **compatible_config)
            scheduler.set_timesteps(num_inference_steps)
            
            self.logger.info(f"Created compatible scheduler: {scheduler_type}")
            return scheduler
            
        except Exception as e:
            self.logger.error(f"Failed to create compatible scheduler: {str(e)}")
            raise
    
    def _build_scheduler_kwargs(self, config: SchedulerConfig, default_config: Dict[str, Any]) -> Dict[str, Any]:
        """Build scheduler keyword arguments from configuration."""
        kwargs = default_config.copy()
        
        # Update with configuration values
        if hasattr(config, 'use_karras_sigmas') and 'use_karras_sigmas' in kwargs:
            kwargs['use_karras_sigmas'] = config.use_karras_sigmas
        
        if hasattr(config, 'timestep_spacing') and 'timestep_spacing' in kwargs:
            kwargs['timestep_spacing'] = config.timestep_spacing
        
        if hasattr(config, 'algorithm_type') and 'algorithm_type' in kwargs:
            kwargs['algorithm_type'] = config.algorithm_type
        
        if hasattr(config, 'solver_order') and 'solver_order' in kwargs:
            kwargs['solver_order'] = config.solver_order
        
        if hasattr(config, 'thresholding') and 'thresholding' in kwargs:
            kwargs['thresholding'] = config.thresholding
        
        if hasattr(config, 'dynamic_thresholding_ratio') and 'dynamic_thresholding_ratio' in kwargs:
            kwargs['dynamic_thresholding_ratio'] = config.dynamic_thresholding_ratio
        
        if hasattr(config, 'sample_max_value') and 'sample_max_value' in kwargs:
            kwargs['sample_max_value'] = config.sample_max_value
        
        if hasattr(config, 'prediction_type') and 'prediction_type' in kwargs:
            kwargs['prediction_type'] = config.prediction_type
        
        if hasattr(config, 'beta_start') and 'beta_start' in kwargs:
            kwargs['beta_start'] = config.beta_start
        
        if hasattr(config, 'beta_end') and 'beta_end' in kwargs:
            kwargs['beta_end'] = config.beta_end
        
        if hasattr(config, 'beta_schedule') and 'beta_schedule' in kwargs:
            kwargs['beta_schedule'] = config.beta_schedule
        
        if hasattr(config, 'clip_sample') and 'clip_sample' in kwargs:
            kwargs['clip_sample'] = config.clip_sample
        
        return kwargs
    
    def _extract_compatible_config(self, original_scheduler: SchedulerMixin, 
                                 target_scheduler_class: Type) -> Dict[str, Any]:
        """Extract compatible configuration between schedulers."""
        original_config = original_scheduler.config
        compatible_config = {}
        
        # Common parameters that can be transferred
        common_params = [
            'num_train_timesteps', 'beta_start', 'beta_end', 'beta_schedule',
            'prediction_type', 'timestep_spacing', 'steps_offset'
        ]
        
        for param in common_params:
            if param in original_config:
                compatible_config[param] = original_config[param]
        
        return compatible_config
    
    def get_scheduler_info(self, scheduler_type: str) -> Dict[str, Any]:
        """
        Get information about a scheduler type.
        
        Args:
            scheduler_type: Type of scheduler
            
        Returns:
            Scheduler information dictionary
        """
        if scheduler_type not in self.SCHEDULERS:
            raise ValueError(f"Unknown scheduler type: {scheduler_type}")
        
        scheduler_class = self.SCHEDULERS[scheduler_type]
        default_config = self.DEFAULT_CONFIGS.get(scheduler_type, {})
        
        return {
            "name": scheduler_type,
            "class": scheduler_class.__name__,
            "description": scheduler_class.__doc__ or "No description available",
            "default_config": default_config,
            "supports_karras_sigmas": "use_karras_sigmas" in default_config,
            "supports_timestep_spacing": "timestep_spacing" in default_config,
            "supports_thresholding": "thresholding" in default_config
        }
    
    def list_available_schedulers(self) -> List[str]:
        """Get list of available scheduler types."""
        return list(self.SCHEDULERS.keys())
    
    def list_quality_presets(self) -> List[str]:
        """Get list of available quality presets."""
        return list(self.QUALITY_PRESETS.keys())
    
    def get_quality_preset(self, preset_name: str) -> Dict[str, Any]:
        """
        Get configuration for a quality preset.
        
        Args:
            preset_name: Name of the preset
            
        Returns:
            Preset configuration dictionary
        """
        if preset_name not in self.QUALITY_PRESETS:
            raise ValueError(f"Unknown preset: {preset_name}")
        
        return self.QUALITY_PRESETS[preset_name].copy()
    
    def validate_scheduler_config(self, config: Union[SchedulerConfig, Dict[str, Any]]) -> Tuple[bool, Optional[str]]:
        """
        Validate a scheduler configuration.
        
        Args:
            config: Scheduler configuration to validate
            
        Returns:
            Tuple of (is_valid, error_message)
        """
        try:
            if isinstance(config, dict):
                config = SchedulerConfig(**config)
            
            # Check if scheduler type is supported
            if config.scheduler_type not in self.SCHEDULERS:
                return False, f"Unknown scheduler type: {config.scheduler_type}"
            
            # Validate parameter ranges
            if config.num_inference_steps < 1 or config.num_inference_steps > 1000:
                return False, "num_inference_steps must be between 1 and 1000"
            
            if config.guidance_scale < 1.0 or config.guidance_scale > 30.0:
                return False, "guidance_scale must be between 1.0 and 30.0"
            
            if config.timestep_spacing not in ["leading", "linspace", "trailing"]:
                return False, "timestep_spacing must be 'leading', 'linspace', or 'trailing'"
            
            return True, None
            
        except Exception as e:
            return False, f"Configuration error: {str(e)}"
    
    def recommend_scheduler(self, use_case: str, quality: str = "balanced") -> str:
        """
        Recommend a scheduler for a specific use case.
        
        Args:
            use_case: Use case ("text2img", "img2img", "inpainting", "upscaling")
            quality: Quality level ("fast", "balanced", "quality", "ultra")
            
        Returns:
            Recommended scheduler type
        """
        recommendations = {
            "text2img": {
                "fast": "DPMSolverMultistepScheduler",
                "balanced": "DPMSolverMultistepScheduler", 
                "quality": "EulerAncestralDiscreteScheduler",
                "ultra": "HeunDiscreteScheduler"
            },
            "img2img": {
                "fast": "DDIMScheduler",
                "balanced": "DPMSolverMultistepScheduler",
                "quality": "EulerDiscreteScheduler",
                "ultra": "HeunDiscreteScheduler"
            },
            "inpainting": {
                "fast": "DDIMScheduler",
                "balanced": "DDIMScheduler",
                "quality": "DPMSolverMultistepScheduler",
                "ultra": "EulerDiscreteScheduler"
            },
            "upscaling": {
                "fast": "DPMSolverMultistepScheduler",
                "balanced": "EulerDiscreteScheduler",
                "quality": "HeunDiscreteScheduler",
                "ultra": "HeunDiscreteScheduler"
            }
        }
        
        return recommendations.get(use_case, {}).get(quality, "DPMSolverMultistepScheduler")


# Global scheduler factory instance
_scheduler_factory = None


def get_scheduler_factory() -> SchedulerFactory:
    """Get the global scheduler factory instance."""
    global _scheduler_factory
    if _scheduler_factory is None:
        _scheduler_factory = SchedulerFactory()
    return _scheduler_factory
