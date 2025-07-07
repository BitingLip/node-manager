"""
Scheduler Manager - Phase 2 Implementation
Dynamic scheduler creation and management for SDXL generation.

Supports all major diffusion schedulers with proper configuration.
"""

import logging
from typing import Dict, Any, Optional, List, Union

logger = logging.getLogger(__name__)

class SchedulerManager:
    """
    Manages scheduler creation and configuration for diffusion pipelines.
    
    Provides:
    - Dynamic scheduler creation from string names
    - Scheduler configuration validation
    - Default parameter management
    - Scheduler-specific optimizations
    """
    
    # Supported scheduler names (will dynamically import classes)
    SUPPORTED_SCHEDULERS = [
        "DPMSolverMultistepScheduler",
        "DDIMScheduler", 
        "EulerDiscreteScheduler",
        "EulerAncestralDiscreteScheduler",
        "DPMSolverSinglestepScheduler",
        "KDPM2DiscreteScheduler",
        "KDPM2AncestralDiscreteScheduler",
        "HeunDiscreteScheduler",
        "LMSDiscreteScheduler",
        "UniPCMultistepScheduler"
    ]
    
    # Legacy name mappings for backward compatibility
    LEGACY_MAPPINGS: Dict[str, str] = {
        "DPMSolverMultistep": "DPMSolverMultistepScheduler",
        "DDIM": "DDIMScheduler",
        "Euler": "EulerDiscreteScheduler",
        "EulerA": "EulerAncestralDiscreteScheduler",
        "EulerAncestral": "EulerAncestralDiscreteScheduler",
        "DPMSolverSinglestep": "DPMSolverSinglestepScheduler",
        "KDPM2": "KDPM2DiscreteScheduler",
        "KDPM2Ancestral": "KDPM2AncestralDiscreteScheduler",
        "Heun": "HeunDiscreteScheduler",
        "LMS": "LMSDiscreteScheduler",
        "UniPC": "UniPCMultistepScheduler"
    }
    
    # Default configurations for each scheduler
    DEFAULT_CONFIGS: Dict[str, Dict[str, Any]] = {
        "DPMSolverMultistepScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "algorithm_type": "dpmsolver++",
            "solver_order": 2,
            "prediction_type": "epsilon"
        },
        "DDIMScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "prediction_type": "epsilon",
            "clip_sample": False
        },
        "EulerDiscreteScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "prediction_type": "epsilon"
        },
        "EulerAncestralDiscreteScheduler": {
            "num_train_timesteps": 1000,
            "beta_start": 0.00085,
            "beta_end": 0.012,
            "beta_schedule": "scaled_linear",
            "prediction_type": "epsilon"
        }
    }
    
    # Performance characteristics for each scheduler
    SCHEDULER_CHARACTERISTICS: Dict[str, Dict[str, Any]] = {
        "DPMSolverMultistepScheduler": {
            "speed": "fast",
            "quality": "high", 
            "min_steps": 10,
            "recommended_steps": 20,
            "good_for": ["general", "quality", "speed"]
        },
        "DDIMScheduler": {
            "speed": "medium",
            "quality": "medium",
            "min_steps": 20,
            "recommended_steps": 50,
            "good_for": ["general", "img2img"]
        },
        "EulerDiscreteScheduler": {
            "speed": "fast",
            "quality": "medium",
            "min_steps": 10,
            "recommended_steps": 30,
            "good_for": ["speed", "artistic"]
        },
        "EulerAncestralDiscreteScheduler": {
            "speed": "fast",
            "quality": "high",
            "min_steps": 15,
            "recommended_steps": 30,
            "good_for": ["artistic", "creative"]
        }
    }
    
    def __init__(self):
        """Initialize the scheduler manager."""
        self._scheduler_cache: Dict[str, Any] = {}
        self._scheduler_classes: Dict[str, Any] = {}
        logger.info(f"Scheduler Manager initialized with {len(self.SUPPORTED_SCHEDULERS)} supported schedulers")
    
    def _get_scheduler_class(self, scheduler_name: str):
        """Dynamically import and return scheduler class."""
        if scheduler_name in self._scheduler_classes:
            return self._scheduler_classes[scheduler_name]
        
        try:
            # Try main diffusers import first
            from diffusers import schedulers
            scheduler_class = getattr(schedulers, scheduler_name, None)
            
            if scheduler_class is None:
                # Try direct import from diffusers
                import diffusers
                scheduler_class = getattr(diffusers, scheduler_name, None)
            
            if scheduler_class is None:
                # Try importing by module name
                module_map = {
                    "DPMSolverMultistepScheduler": "diffusers.schedulers.scheduling_dpmsolver_multistep",
                    "DDIMScheduler": "diffusers.schedulers.scheduling_ddim",
                    "EulerDiscreteScheduler": "diffusers.schedulers.scheduling_euler_discrete",
                    "EulerAncestralDiscreteScheduler": "diffusers.schedulers.scheduling_euler_ancestral_discrete"
                }
                
                if scheduler_name in module_map:
                    import importlib
                    module = importlib.import_module(module_map[scheduler_name])
                    scheduler_class = getattr(module, scheduler_name)
            
            if scheduler_class is not None:
                self._scheduler_classes[scheduler_name] = scheduler_class
                return scheduler_class
            else:
                raise ImportError(f"Could not find scheduler class: {scheduler_name}")
                
        except Exception as e:
            logger.error(f"Failed to import scheduler {scheduler_name}: {e}")
            return None
    
    async def get_scheduler(self, scheduler_name: str, base_config: Optional[Dict] = None, custom_config: Optional[Dict] = None):
        """
        Get or create a scheduler instance.
        
        Args:
            scheduler_name: Name of the scheduler to create
            base_config: Base configuration from existing scheduler
            custom_config: Custom configuration overrides
            
        Returns:
            Configured scheduler instance
            
        Raises:
            ValueError: If scheduler is not supported
        """
        # Normalize scheduler name
        normalized_name = self._normalize_scheduler_name(scheduler_name)
        
        if normalized_name not in self.SUPPORTED_SCHEDULERS:
            raise ValueError(f"Unsupported scheduler: {scheduler_name}. Supported: {self.SUPPORTED_SCHEDULERS}")
        
        # Get scheduler class
        scheduler_class = self._get_scheduler_class(normalized_name)
        if scheduler_class is None:
            raise ValueError(f"Failed to load scheduler class: {normalized_name}")
        
        # Create cache key
        config_key = self._create_config_key(normalized_name, base_config, custom_config)
        
        # Check cache
        if config_key in self._scheduler_cache:
            logger.debug(f"Using cached scheduler: {normalized_name}")
            return self._scheduler_cache[config_key]
        
        # Create new scheduler
        scheduler = await self._create_scheduler(scheduler_class, normalized_name, base_config, custom_config)
        
        # Cache the scheduler
        self._scheduler_cache[config_key] = scheduler
        
        logger.info(f"Created scheduler: {normalized_name}")
        return scheduler
    
    def _normalize_scheduler_name(self, scheduler_name: str) -> str:
        """Normalize scheduler name, handling legacy mappings."""
        if scheduler_name in self.LEGACY_MAPPINGS:
            return self.LEGACY_MAPPINGS[scheduler_name]
        return scheduler_name
    
    def _create_config_key(self, scheduler_name: str, base_config: Optional[Dict], custom_config: Optional[Dict]) -> str:
        """Create a unique key for caching schedulers."""
        import hashlib
        import json
        
        config_data = {
            "scheduler": scheduler_name,
            "base": base_config or {},
            "custom": custom_config or {}
        }
        
        config_str = json.dumps(config_data, sort_keys=True)
        return hashlib.md5(config_str.encode()).hexdigest()
    
    async def _create_scheduler(self, scheduler_class, scheduler_name: str, base_config: Optional[Dict], custom_config: Optional[Dict]):
        """Create a new scheduler instance with proper configuration."""
        # Start with default configuration
        config = self.DEFAULT_CONFIGS.get(scheduler_name, {}).copy()
        
        # Merge base configuration (from existing scheduler)
        if base_config:
            config.update(base_config)
        
        # Apply custom configuration overrides
        if custom_config:
            config.update(custom_config)
        
        # Remove any None values
        config = {k: v for k, v in config.items() if v is not None}
        
        try:
            # Create scheduler instance
            scheduler = scheduler_class(**config)
            
            logger.debug(f"Created {scheduler_name} with config: {config}")
            return scheduler
            
        except Exception as e:
            logger.error(f"Failed to create scheduler {scheduler_name}: {str(e)}")
            logger.debug(f"Config that failed: {config}")
            
            # Fallback to minimal configuration
            try:
                minimal_config = {
                    "num_train_timesteps": 1000,
                    "beta_start": 0.00085,
                    "beta_end": 0.012,
                    "beta_schedule": "scaled_linear"
                }
                scheduler = scheduler_class(**minimal_config)
                logger.warning(f"Created {scheduler_name} with minimal config after failure")
                return scheduler
            except Exception as fallback_error:
                raise ValueError(f"Failed to create scheduler {scheduler_name}: {str(e)}. Fallback also failed: {str(fallback_error)}")
    
    def get_scheduler_info(self, scheduler_name: str) -> Dict[str, Any]:
        """
        Get information about a scheduler.
        
        Args:
            scheduler_name: Name of the scheduler
            
        Returns:
            Dictionary with scheduler characteristics and recommendations
        """
        normalized_name = self._normalize_scheduler_name(scheduler_name)
        
        if normalized_name not in self.SUPPORTED_SCHEDULERS:
            return {"error": f"Unsupported scheduler: {scheduler_name}"}
        
        info = {
            "name": normalized_name,
            "supported": True,
            "available": self._get_scheduler_class(normalized_name) is not None
        }
        
        # Add characteristics if available
        if normalized_name in self.SCHEDULER_CHARACTERISTICS:
            info.update(self.SCHEDULER_CHARACTERISTICS[normalized_name])
        
        # Add default configuration
        if normalized_name in self.DEFAULT_CONFIGS:
            info["default_config"] = self.DEFAULT_CONFIGS[normalized_name]
        
        return info
    
    def list_supported_schedulers(self) -> List[Dict[str, Any]]:
        """Get a list of all supported schedulers with their information."""
        schedulers = []
        
        for scheduler_name in self.SUPPORTED_SCHEDULERS:
            info = self.get_scheduler_info(scheduler_name)
            schedulers.append(info)
        
        return schedulers
    
    def recommend_scheduler(self, use_case: str, num_steps: int = 30) -> Dict[str, Any]:
        """
        Recommend a scheduler based on use case and number of steps.
        
        Args:
            use_case: Use case ("speed", "quality", "general", "artistic", etc.)
            num_steps: Number of inference steps planned
            
        Returns:
            Dictionary with recommended scheduler and reason
        """
        recommendations = []
        
        for scheduler_name in self.SUPPORTED_SCHEDULERS:
            if scheduler_name in self.SCHEDULER_CHARACTERISTICS:
                characteristics = self.SCHEDULER_CHARACTERISTICS[scheduler_name]
                score = 0
                reasons = []
                
                # Check if scheduler is good for the use case
                if use_case.lower() in characteristics.get("good_for", []):
                    score += 10
                    reasons.append(f"optimized for {use_case}")
                
                # Check step count suitability
                min_steps = characteristics.get("min_steps", 10)
                rec_steps = characteristics.get("recommended_steps", 30)
                
                if num_steps >= min_steps:
                    if abs(num_steps - rec_steps) <= 10:
                        score += 5
                        reasons.append("good step count match")
                    elif num_steps >= rec_steps:
                        score += 3
                        reasons.append("sufficient steps")
                    else:
                        score += 1
                        reasons.append("acceptable steps")
                else:
                    score -= 5
                    reasons.append(f"below minimum steps ({min_steps})")
                
                recommendations.append({
                    "scheduler": scheduler_name,
                    "score": score,
                    "reasons": reasons,
                    "characteristics": characteristics
                })
        
        # Sort by score
        recommendations.sort(key=lambda x: x["score"], reverse=True)
        
        # Return top recommendation
        if recommendations:
            best = recommendations[0]
            return {
                "recommended_scheduler": best["scheduler"],
                "score": best["score"],
                "reasons": best["reasons"],
                "characteristics": best["characteristics"],
                "alternatives": [r["scheduler"] for r in recommendations[1:3]]  # Top 2 alternatives
            }
        
        # Fallback
        return {
            "recommended_scheduler": "DPMSolverMultistepScheduler",
            "score": 0,
            "reasons": ["default fallback"],
            "characteristics": self.SCHEDULER_CHARACTERISTICS.get("DPMSolverMultistepScheduler", {}),
            "alternatives": []
        }
    
    def clear_cache(self) -> None:
        """Clear the scheduler cache."""
        self._scheduler_cache.clear()
        logger.info("Scheduler cache cleared")
    
    def get_cache_stats(self) -> Dict[str, Any]:
        """Get statistics about the scheduler cache."""
        return {
            "cached_schedulers": len(self._scheduler_cache),
            "supported_schedulers": len(self.SUPPORTED_SCHEDULERS),
            "loaded_classes": len(self._scheduler_classes),
            "cache_keys": list(self._scheduler_cache.keys())
        }
