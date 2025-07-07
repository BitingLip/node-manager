"""
Base scheduler implementation for SDXL diffusion models.
Provides common functionality for all scheduler types.
"""

import logging
import torch
from typing import Optional, Dict, Any, Union, List
from abc import ABC, abstractmethod
from diffusers.schedulers.scheduling_utils import SchedulerMixin

logger = logging.getLogger(__name__)


class BaseScheduler(ABC):
    """Abstract base class for all SDXL schedulers."""
    
    def __init__(self, scheduler: SchedulerMixin):
        self.scheduler = scheduler
        self.num_inference_steps: Optional[int] = None
        self.timesteps: Optional[torch.Tensor] = None
        
    @abstractmethod
    def configure(self, **kwargs) -> None:
        """Configure scheduler parameters."""
        pass
    
    @abstractmethod
    def get_scheduler_config(self) -> Dict[str, Any]:
        """Get scheduler configuration."""
        pass
    
    def set_timesteps(self, num_inference_steps: int, device: Optional[torch.device] = None) -> None:
        """Set the timesteps for the scheduler."""
        self.num_inference_steps = num_inference_steps
        if hasattr(self.scheduler, 'set_timesteps'):
            self.scheduler.set_timesteps(num_inference_steps, device=device)  # type: ignore
        if hasattr(self.scheduler, 'timesteps'):
            self.timesteps = self.scheduler.timesteps  # type: ignore
        logger.debug(f"Set {num_inference_steps} timesteps for {self.__class__.__name__}")
    
    def step(
        self,
        model_output: torch.Tensor,
        timestep: int,
        sample: torch.Tensor,
        **kwargs
    ) -> Any:
        """Perform a single denoising step."""
        return self.scheduler.step(model_output, timestep, sample, **kwargs)  # type: ignore
    
    def scale_model_input(self, sample: torch.Tensor, timestep: int) -> torch.Tensor:
        """Scale the model input according to the scheduler."""
        return self.scheduler.scale_model_input(sample, timestep)  # type: ignore
    
    def add_noise(
        self,
        original_samples: torch.Tensor,
        noise: torch.Tensor,
        timesteps: torch.Tensor
    ) -> torch.Tensor:
        """Add noise to the original samples."""
        return self.scheduler.add_noise(original_samples, noise, timesteps)  # type: ignore
    
    def get_variance(self, timestep: int, prev_timestep: int) -> torch.Tensor:
        """Get the variance for the current timestep."""
        if hasattr(self.scheduler, 'get_variance'):
            return self.scheduler.get_variance(timestep, prev_timestep)  # type: ignore
        else:
            # Fallback for schedulers without explicit variance method
            return torch.tensor(0.0)
    
    def optimize_for_inference(self) -> None:
        """Optimize scheduler for inference."""
        # Set scheduler to eval mode if applicable
        if hasattr(self.scheduler, 'eval'):
            self.scheduler.eval()  # type: ignore
        
        # Disable gradient computation
        if hasattr(self.scheduler, 'requires_grad_'):
            self.scheduler.requires_grad_(False)  # type: ignore
        
        logger.debug(f"Optimized {self.__class__.__name__} for inference")
    
    def get_init_noise_sigma(self) -> float:
        """Get the initial noise sigma value."""
        if hasattr(self.scheduler, 'init_noise_sigma'):
            return self.scheduler.init_noise_sigma  # type: ignore
        else:
            return 1.0
    
    def get_step_ratio(self, current_step: int) -> float:
        """Get the progress ratio for the current step."""
        if self.num_inference_steps is None:
            return 0.0
        return current_step / self.num_inference_steps
    
    def predict_noise_residual(
        self,
        model_output: torch.Tensor,
        timestep: int,
        sample: torch.Tensor
    ) -> torch.Tensor:
        """Predict the noise residual for the current timestep."""
        # This is a generic implementation
        # Specific schedulers may override this method
        return model_output
    
    def compute_previous_sample(
        self,
        model_output: torch.Tensor,
        timestep: int,
        sample: torch.Tensor
    ) -> torch.Tensor:
        """Compute the previous sample from the model output."""
        step_result = self.step(model_output, timestep, sample)
        
        if hasattr(step_result, 'prev_sample'):
            return step_result.prev_sample
        else:
            return step_result
    
    def to(self, device: torch.device) -> 'BaseScheduler':
        """Move scheduler to device if applicable."""
        if hasattr(self.scheduler, 'to'):
            self.scheduler.to(device)  # type: ignore
        
        if self.timesteps is not None:
            self.timesteps = self.timesteps.to(device)
        
        return self
    
    def __str__(self) -> str:
        """String representation of the scheduler."""
        return f"{self.__class__.__name__}(steps={self.num_inference_steps})"
    
    def __repr__(self) -> str:
        """Detailed representation of the scheduler."""
        config = self.get_scheduler_config()
        config_str = ", ".join([f"{k}={v}" for k, v in config.items()])
        return f"{self.__class__.__name__}({config_str})"


class SchedulerQualityPreset:
    """Predefined quality presets for schedulers."""
    
    FAST = {
        "num_inference_steps": 20,
        "guidance_scale": 7.5,
        "eta": 0.0
    }
    
    BALANCED = {
        "num_inference_steps": 30,
        "guidance_scale": 7.5,
        "eta": 0.0
    }
    
    HIGH_QUALITY = {
        "num_inference_steps": 50,
        "guidance_scale": 7.5,
        "eta": 0.0
    }
    
    ULTRA_QUALITY = {
        "num_inference_steps": 100,
        "guidance_scale": 7.5,
        "eta": 0.0
    }
    
    @classmethod
    def get_preset(cls, preset_name: str) -> Dict[str, Any]:
        """Get a quality preset by name."""
        presets = {
            "fast": cls.FAST,
            "balanced": cls.BALANCED,
            "high": cls.HIGH_QUALITY,
            "ultra": cls.ULTRA_QUALITY
        }
        
        preset = presets.get(preset_name.lower())
        if preset is None:
            logger.warning(f"Unknown preset '{preset_name}', using 'balanced'")
            return cls.BALANCED
        
        return preset.copy()


class SchedulerMetrics:
    """Track scheduler performance metrics."""
    
    def __init__(self):
        self.step_times: List[float] = []
        self.total_steps: int = 0
        self.current_step: int = 0
        
    def start_step(self) -> None:
        """Mark the start of a denoising step."""
        import time
        self.step_start_time = time.time()
        
    def end_step(self) -> None:
        """Mark the end of a denoising step."""
        import time
        if hasattr(self, 'step_start_time'):
            step_time = time.time() - self.step_start_time
            self.step_times.append(step_time)
            self.current_step += 1
    
    def get_average_step_time(self) -> float:
        """Get the average time per step."""
        if not self.step_times:
            return 0.0
        return sum(self.step_times) / len(self.step_times)
    
    def get_estimated_remaining_time(self) -> float:
        """Estimate remaining time based on current progress."""
        if not self.step_times or self.total_steps == 0:
            return 0.0
        
        avg_time = self.get_average_step_time()
        remaining_steps = self.total_steps - self.current_step
        return avg_time * remaining_steps
    
    def reset(self) -> None:
        """Reset metrics for a new inference run."""
        self.step_times.clear()
        self.current_step = 0
    
    def get_progress_info(self) -> Dict[str, Any]:
        """Get comprehensive progress information."""
        return {
            "current_step": self.current_step,
            "total_steps": self.total_steps,
            "progress_percentage": (self.current_step / self.total_steps * 100) if self.total_steps > 0 else 0,
            "average_step_time": self.get_average_step_time(),
            "estimated_remaining_time": self.get_estimated_remaining_time(),
            "total_elapsed_time": sum(self.step_times)
        }
