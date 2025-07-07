"""
DDIM scheduler implementation for SDXL diffusion models.
Denoising Diffusion Implicit Models scheduler.
"""

import logging
import torch
from typing import Optional, Dict, Any, Union
from diffusers.schedulers.scheduling_ddim import DDIMScheduler
from .base_scheduler import BaseScheduler

logger = logging.getLogger(__name__)


class DDIMSchedulerWrapper(BaseScheduler):
    """DDIM scheduler wrapper with enhanced configuration."""
    
    def __init__(
        self,
        num_train_timesteps: int = 1000,
        beta_start: float = 0.0001,
        beta_end: float = 0.02,
        beta_schedule: str = "linear",
        trained_betas: Optional[torch.Tensor] = None,
        clip_sample: bool = False,
        set_alpha_to_one: bool = False,
        steps_offset: int = 0,
        prediction_type: str = "epsilon",
        thresholding: bool = False,
        dynamic_thresholding_ratio: float = 0.995,
        clip_sample_range: float = 1.0,
        sample_max_value: float = 1.0,
        **kwargs
    ):
        """Initialize DDIM scheduler."""
        scheduler = DDIMScheduler(
            num_train_timesteps=num_train_timesteps,
            beta_start=beta_start,
            beta_end=beta_end,
            beta_schedule=beta_schedule,
            trained_betas=trained_betas,
            clip_sample=clip_sample,
            set_alpha_to_one=set_alpha_to_one,
            steps_offset=steps_offset,
            prediction_type=prediction_type,
            thresholding=thresholding,
            dynamic_thresholding_ratio=dynamic_thresholding_ratio,
            clip_sample_range=clip_sample_range,
            sample_max_value=sample_max_value,
            **kwargs
        )
        
        super().__init__(scheduler)
        
        # DDIM specific parameters
        self.eta = 0.0  # Deterministic sampling by default
        self.use_clipped_model_output = False
        
        logger.info("DDIM scheduler initialized")
    
    def configure(
        self,
        eta: float = 0.0,
        use_clipped_model_output: bool = False,
        **kwargs
    ) -> None:
        """Configure DDIM specific parameters."""
        self.eta = eta
        self.use_clipped_model_output = use_clipped_model_output
        
        logger.debug(f"DDIM configured: eta={eta}, clipped_output={use_clipped_model_output}")
    
    def step(
        self,
        model_output: torch.Tensor,
        timestep: int,
        sample: torch.Tensor,
        eta: Optional[float] = None,
        use_clipped_model_output: Optional[bool] = None,
        generator: Optional[torch.Generator] = None,
        variance_noise: Optional[torch.Tensor] = None,
        return_dict: bool = True,
        **kwargs
    ) -> Any:
        """Perform DDIM step with eta parameter."""
        if eta is None:
            eta = self.eta
        if use_clipped_model_output is None:
            use_clipped_model_output = self.use_clipped_model_output
        
        return self.scheduler.step(  # type: ignore
            model_output=model_output,
            timestep=timestep,
            sample=sample,
            eta=eta,
            use_clipped_model_output=use_clipped_model_output,
            generator=generator,
            variance_noise=variance_noise,
            return_dict=return_dict,
            **kwargs
        )
    
    def get_scheduler_config(self) -> Dict[str, Any]:
        """Get DDIM scheduler configuration."""
        config = getattr(self.scheduler, 'config', {})
        return {
            "scheduler_type": "DDIM",
            "num_train_timesteps": getattr(config, 'num_train_timesteps', 1000),
            "beta_start": getattr(config, 'beta_start', 0.0001),
            "beta_end": getattr(config, 'beta_end', 0.02),
            "beta_schedule": getattr(config, 'beta_schedule', 'linear'),
            "prediction_type": getattr(config, 'prediction_type', 'epsilon'),
            "eta": self.eta,
            "use_clipped_model_output": self.use_clipped_model_output,
            "num_inference_steps": self.num_inference_steps
        }
    
    def set_eta(self, eta: float) -> None:
        """Set the eta parameter for stochastic sampling."""
        self.eta = max(0.0, min(1.0, eta))  # Clamp between 0 and 1
        logger.debug(f"DDIM eta set to {self.eta}")
    
    def enable_deterministic_sampling(self) -> None:
        """Enable deterministic sampling (eta=0)."""
        self.set_eta(0.0)
        logger.info("DDIM deterministic sampling enabled")
    
    def enable_stochastic_sampling(self, eta: float = 1.0) -> None:
        """Enable stochastic sampling with specified eta."""
        self.set_eta(eta)
        logger.info(f"DDIM stochastic sampling enabled with eta={eta}")
    
    def compute_variance(self, timestep: int, prev_timestep: int) -> torch.Tensor:
        """Compute variance for DDIM sampling."""
        # Use type ignore for scheduler attributes
        alphas_cumprod = getattr(self.scheduler, 'alphas_cumprod', None)
        final_alpha_cumprod = getattr(self.scheduler, 'final_alpha_cumprod', 1.0)
        
        if alphas_cumprod is None:
            return torch.tensor(0.0)
        
        alpha_prod_t = alphas_cumprod[timestep]
        alpha_prod_t_prev = alphas_cumprod[prev_timestep] if prev_timestep >= 0 else final_alpha_cumprod
        
        beta_prod_t = 1 - alpha_prod_t
        beta_prod_t_prev = 1 - alpha_prod_t_prev
        
        variance = (beta_prod_t_prev / beta_prod_t) * (1 - alpha_prod_t / alpha_prod_t_prev)
        return variance
    
    def get_guidance_scale_embedding(self, w: torch.Tensor, embedding_dim: int = 512) -> torch.Tensor:
        """Get guidance scale embedding for CFG."""
        assert len(w.shape) == 1
        w = w * 1000.0
        
        half_dim = embedding_dim // 2
        emb = torch.log(torch.tensor(10000.0)) / (half_dim - 1)
        emb = torch.exp(torch.arange(half_dim, dtype=torch.float32) * -emb)
        emb = w.to(dtype=torch.float32)[:, None] * emb[None, :]
        emb = torch.cat([torch.sin(emb), torch.cos(emb)], dim=1)
        
        if embedding_dim % 2 == 1:  # zero pad
            emb = torch.nn.functional.pad(emb, (0, 1))
        
        assert emb.shape == (w.shape[0], embedding_dim)
        return emb


def create_ddim_scheduler(
    quality_preset: str = "balanced",
    eta: float = 0.0,
    **kwargs
) -> DDIMSchedulerWrapper:
    """Create a DDIM scheduler with quality preset."""
    from .base_scheduler import SchedulerQualityPreset
    
    preset = SchedulerQualityPreset.get_preset(quality_preset)
    
    scheduler = DDIMSchedulerWrapper(**kwargs)
    scheduler.configure(eta=eta)
    
    logger.info(f"Created DDIM scheduler with {quality_preset} quality preset")
    return scheduler
