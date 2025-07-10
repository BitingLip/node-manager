"""
Euler Worker for SDXL Workers System
====================================

Migrated from schedulers/euler.py
Euler scheduler worker for fast diffusion sampling with discrete stepping.
"""

import logging
import torch
from typing import Dict, Any, Optional
from diffusers.schedulers.scheduling_euler_discrete import EulerDiscreteScheduler
from ..interface_scheduler import BaseScheduler


class EulerWorker(BaseScheduler):
    """
    Euler scheduler worker for fast diffusion sampling.
    
    Provides Euler discrete scheduling functionality optimized for
    fast sampling with SDXL inference operations.
    """
    
    def __init__(self, config: Dict[str, Any]):
        super().__init__(config)
        self.scheduler: Optional[EulerDiscreteScheduler] = None
        
        # Euler-specific configuration
        self.num_train_timesteps = config.get("num_train_timesteps", 1000)
        self.beta_start = config.get("beta_start", 0.00085)
        self.beta_end = config.get("beta_end", 0.012)
        self.beta_schedule = config.get("beta_schedule", "scaled_linear")
        self.prediction_type = config.get("prediction_type", "epsilon")
        
        # Euler-specific parameters
        self.use_karras_sigmas = config.get("use_karras_sigmas", False)
        self.timestep_spacing = config.get("timestep_spacing", "leading")
        self.interpolation_type = config.get("interpolation_type", "linear")
        
    async def initialize(self) -> bool:
        """Initialize the Euler scheduler."""
        try:
            self.logger.info("Initializing Euler worker...")
            
            # Create Euler scheduler instance
            self.scheduler = EulerDiscreteScheduler(
                num_train_timesteps=self.num_train_timesteps,
                beta_start=self.beta_start,
                beta_end=self.beta_end,
                beta_schedule=self.beta_schedule,
                prediction_type=self.prediction_type,
                use_karras_sigmas=self.use_karras_sigmas,
                timestep_spacing=self.timestep_spacing,
                interpolation_type=self.interpolation_type
            )
            
            self.initialized = True
            self.logger.info("Euler worker initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to initialize Euler worker: {str(e)}")
            return False
    
    async def process_scheduling(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process an Euler scheduling request."""
        try:
            if not self.scheduler:
                raise ValueError("Euler scheduler not initialized")
            
            num_inference_steps = request.get("num_inference_steps", 20)
            device = request.get("device", "cuda")
            use_karras_sigmas = request.get("use_karras_sigmas", self.use_karras_sigmas)
            
            # Set timesteps
            await self.set_timesteps(num_inference_steps, torch.device(device))
            
            # Process Euler-specific parameters
            result = {
                "scheduler_type": "euler",
                "num_inference_steps": num_inference_steps,
                "use_karras_sigmas": use_karras_sigmas,
                "timestep_spacing": self.timestep_spacing,
                "interpolation_type": self.interpolation_type,
                "timesteps": self.timesteps.tolist() if self.timesteps is not None else [],
                "scheduler_config": await self.get_scheduler_config(),
                "processing_time": 0.1,  # Placeholder
                "status": "completed"
            }
            
            self.logger.info(f"Euler scheduling completed for {num_inference_steps} steps")
            return result
            
        except Exception as e:
            self.logger.error(f"Euler scheduling failed: {e}")
            return {"error": str(e)}
    
    async def configure(self, **kwargs) -> Dict[str, Any]:
        """Configure Euler scheduler parameters."""
        try:
            config_updates = {}
            
            # Update Karras sigmas
            if "use_karras_sigmas" in kwargs:
                self.use_karras_sigmas = bool(kwargs["use_karras_sigmas"])
                config_updates["use_karras_sigmas"] = self.use_karras_sigmas
            
            # Update timestep spacing
            if "timestep_spacing" in kwargs:
                spacing = kwargs["timestep_spacing"]
                if spacing in ["leading", "linspace", "trailing"]:
                    self.timestep_spacing = spacing
                    config_updates["timestep_spacing"] = self.timestep_spacing
                else:
                    raise ValueError(f"Invalid timestep spacing: {spacing}")
            
            # Update interpolation type
            if "interpolation_type" in kwargs:
                interp_type = kwargs["interpolation_type"]
                if interp_type in ["linear", "log_linear"]:
                    self.interpolation_type = interp_type
                    config_updates["interpolation_type"] = self.interpolation_type
                else:
                    raise ValueError(f"Invalid interpolation type: {interp_type}")
            
            self.logger.info(f"Euler configuration updated: {config_updates}")
            
            return {
                "configured": True,
                "updates": config_updates,
                "current_config": await self.get_scheduler_config()
            }
            
        except Exception as e:
            self.logger.error(f"Euler configuration failed: {e}")
            return {"error": str(e), "configured": False}
    
    async def get_scheduler_config(self) -> Dict[str, Any]:
        """Get Euler scheduler configuration."""
        return {
            "scheduler_type": "euler",
            "num_train_timesteps": self.num_train_timesteps,
            "beta_start": self.beta_start,
            "beta_end": self.beta_end,
            "beta_schedule": self.beta_schedule,
            "prediction_type": self.prediction_type,
            "use_karras_sigmas": self.use_karras_sigmas,
            "timestep_spacing": self.timestep_spacing,
            "interpolation_type": self.interpolation_type,
            "current_inference_steps": self.num_inference_steps
        }
    
    async def step(self, model_output: torch.Tensor, timestep: int, sample: torch.Tensor, **kwargs) -> torch.Tensor:
        """Perform an Euler scheduler step."""
        try:
            if not self.scheduler:
                raise ValueError("Euler scheduler not initialized")
            
            # Extract Euler-specific parameters
            use_karras_sigmas = kwargs.get("use_karras_sigmas", self.use_karras_sigmas)
            
            # Perform Euler step (placeholder implementation)
            # In actual implementation, this would call the scheduler's step method
            self.logger.debug(f"Euler step at timestep {timestep}")
            
            # Return the processed sample (placeholder)
            return sample
            
        except Exception as e:
            self.logger.error(f"Euler step failed: {e}")
            raise
    
    async def get_speed_recommendations(self) -> Dict[str, Any]:
        """Get Euler speed optimization recommendations."""
        recommendations = []
        
        # Recommend based on current configuration
        if not self.use_karras_sigmas:
            recommendations.append("Enable Karras sigmas for better quality with fewer steps")
        
        if self.timestep_spacing != "leading":
            recommendations.append("Use 'leading' timestep spacing for faster convergence")
        
        if self.interpolation_type != "linear":
            recommendations.append("Use 'linear' interpolation for fastest processing")
        
        # Recommend optimal step counts
        if self.num_inference_steps and self.num_inference_steps > 15:
            recommendations.append("Consider reducing steps to 10-15 for Euler scheduler")
        
        return {
            "recommendations": recommendations,
            "optimal_steps": "10-15",
            "current_speed_level": "fast" if self.num_inference_steps and self.num_inference_steps <= 15 else "medium"
        }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get Euler worker status."""
        base_status = await super().get_status()
        
        euler_status = {
            "scheduler_type": "euler",
            "use_karras_sigmas": self.use_karras_sigmas,
            "timestep_spacing": self.timestep_spacing,
            "interpolation_type": self.interpolation_type,
            "scheduler_created": self.scheduler is not None
        }
        
        base_status.update(euler_status)
        return base_status
    
    async def cleanup(self) -> None:
        """Clean up Euler worker resources."""
        try:
            await super().cleanup()
            self.scheduler = None
            self.logger.info("Euler worker cleanup complete")
        except Exception as e:
            self.logger.error(f"Euler worker cleanup error: {e}")


# Factory function for creating Euler worker
def create_euler_worker(config: Optional[Dict[str, Any]] = None) -> EulerWorker:
    """
    Factory function to create an Euler worker instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        EulerWorker instance
    """
    return EulerWorker(config or {})