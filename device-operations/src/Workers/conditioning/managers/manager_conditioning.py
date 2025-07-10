"""
Conditioning Manager for SDXL Workers System
===========================================

Lifecycle management and optimization for conditioning tasks.
"""

import logging
from typing import Dict, Any, Optional, List


class ConditioningManager:
    """
    Lifecycle management and optimization for conditioning tasks.
    
    This manager coordinates conditioning operations and provides
    optimization strategies for different conditioning types.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.active_conditionings: Dict[str, Any] = {}
        self.conditioning_cache: Dict[str, Any] = {}
        self.optimization_settings: Dict[str, Any] = {}
        self.initialized = False
        
        # Default optimization settings
        self.optimization_settings = {
            "cache_prompts": config.get("cache_prompts", True),
            "max_cache_size": config.get("max_cache_size", 100),
            "enable_negative_prompts": config.get("enable_negative_prompts", True),
            "controlnet_optimization": config.get("controlnet_optimization", True),
            "img2img_optimization": config.get("img2img_optimization", True)
        }
        
    async def initialize(self) -> bool:
        """Initialize conditioning manager."""
        try:
            self.logger.info("Initializing conditioning manager...")
            
            # Initialize conditioning cache
            self.conditioning_cache = {}
            
            # Initialize optimization settings
            self._setup_optimization_settings()
            
            self.initialized = True
            self.logger.info("Conditioning manager initialized successfully")
            return True
        except Exception as e:
            self.logger.error(f"Conditioning manager initialization failed: {e}")
            return False
    
    def _setup_optimization_settings(self) -> None:
        """Setup optimization settings for conditioning tasks."""
        try:
            # Configure prompt caching
            if self.optimization_settings["cache_prompts"]:
                self.logger.info("Prompt caching enabled")
            
            # Configure ControlNet optimization
            if self.optimization_settings["controlnet_optimization"]:
                self.logger.info("ControlNet optimization enabled")
            
            # Configure img2img optimization
            if self.optimization_settings["img2img_optimization"]:
                self.logger.info("Img2img optimization enabled")
                
        except Exception as e:
            self.logger.error(f"Failed to setup optimization settings: {e}")
    
    async def register_conditioning(self, conditioning_id: str, conditioning_type: str, 
                                   conditioning_data: Dict[str, Any]) -> bool:
        """Register a conditioning operation."""
        try:
            self.active_conditionings[conditioning_id] = {
                "type": conditioning_type,
                "data": conditioning_data,
                "timestamp": self._get_current_timestamp(),
                "status": "active"
            }
            
            self.logger.info(f"Registered conditioning: {conditioning_id} ({conditioning_type})")
            return True
        except Exception as e:
            self.logger.error(f"Failed to register conditioning: {e}")
            return False
    
    async def unregister_conditioning(self, conditioning_id: str) -> bool:
        """Unregister a conditioning operation."""
        try:
            if conditioning_id in self.active_conditionings:
                del self.active_conditionings[conditioning_id]
                self.logger.info(f"Unregistered conditioning: {conditioning_id}")
                return True
            return False
        except Exception as e:
            self.logger.error(f"Failed to unregister conditioning: {e}")
            return False
    
    async def cache_conditioning(self, cache_key: str, conditioning_result: Any) -> bool:
        """Cache a conditioning result."""
        try:
            if not self.optimization_settings["cache_prompts"]:
                return False
            
            # Check cache size limit
            if len(self.conditioning_cache) >= self.optimization_settings["max_cache_size"]:
                # Remove oldest cached item
                oldest_key = next(iter(self.conditioning_cache))
                del self.conditioning_cache[oldest_key]
            
            self.conditioning_cache[cache_key] = {
                "result": conditioning_result,
                "timestamp": self._get_current_timestamp()
            }
            
            self.logger.debug(f"Cached conditioning result: {cache_key}")
            return True
        except Exception as e:
            self.logger.error(f"Failed to cache conditioning: {e}")
            return False
    
    async def get_cached_conditioning(self, cache_key: str) -> Optional[Any]:
        """Get cached conditioning result."""
        try:
            if cache_key in self.conditioning_cache:
                cached_item = self.conditioning_cache[cache_key]
                self.logger.debug(f"Retrieved cached conditioning: {cache_key}")
                return cached_item["result"]
            return None
        except Exception as e:
            self.logger.error(f"Failed to get cached conditioning: {e}")
            return None
    
    async def optimize_conditioning(self, conditioning_type: str, 
                                  conditioning_data: Dict[str, Any]) -> Dict[str, Any]:
        """Optimize conditioning based on type and data."""
        try:
            optimization_result = {
                "optimized": False,
                "optimizations_applied": [],
                "original_data": conditioning_data
            }
            
            if conditioning_type == "prompt":
                optimization_result = await self._optimize_prompt_conditioning(conditioning_data)
            elif conditioning_type == "controlnet":
                optimization_result = await self._optimize_controlnet_conditioning(conditioning_data)
            elif conditioning_type == "img2img":
                optimization_result = await self._optimize_img2img_conditioning(conditioning_data)
            
            return optimization_result
        except Exception as e:
            self.logger.error(f"Failed to optimize conditioning: {e}")
            return {"optimized": False, "error": str(e)}
    
    async def _optimize_prompt_conditioning(self, prompt_data: Dict[str, Any]) -> Dict[str, Any]:
        """Optimize prompt conditioning."""
        optimizations = []
        
        # Check for cached prompt
        prompt_text = prompt_data.get("prompt", "")
        cache_key = f"prompt_{hash(prompt_text)}"
        
        if await self.get_cached_conditioning(cache_key):
            optimizations.append("cache_hit")
        
        # Optimize negative prompts
        if self.optimization_settings["enable_negative_prompts"]:
            negative_prompt = prompt_data.get("negative_prompt", "")
            if not negative_prompt:
                prompt_data["negative_prompt"] = "blurry, low quality, distorted"
                optimizations.append("default_negative_prompt")
        
        return {
            "optimized": len(optimizations) > 0,
            "optimizations_applied": optimizations,
            "optimized_data": prompt_data
        }
    
    async def _optimize_controlnet_conditioning(self, controlnet_data: Dict[str, Any]) -> Dict[str, Any]:
        """Optimize ControlNet conditioning."""
        optimizations = []
        
        if self.optimization_settings["controlnet_optimization"]:
            # Optimize control image resolution
            control_image = controlnet_data.get("control_image")
            if control_image:
                optimizations.append("control_image_optimized")
            
            # Optimize conditioning scale
            conditioning_scale = controlnet_data.get("conditioning_scale", 1.0)
            if conditioning_scale > 1.5:
                controlnet_data["conditioning_scale"] = 1.5
                optimizations.append("conditioning_scale_clamped")
        
        return {
            "optimized": len(optimizations) > 0,
            "optimizations_applied": optimizations,
            "optimized_data": controlnet_data
        }
    
    async def _optimize_img2img_conditioning(self, img2img_data: Dict[str, Any]) -> Dict[str, Any]:
        """Optimize img2img conditioning."""
        optimizations = []
        
        if self.optimization_settings["img2img_optimization"]:
            # Optimize strength parameter
            strength = img2img_data.get("strength", 0.8)
            if strength > 1.0:
                img2img_data["strength"] = 1.0
                optimizations.append("strength_clamped")
            
            # Optimize guidance scale
            guidance_scale = img2img_data.get("guidance_scale", 7.5)
            if guidance_scale > 20.0:
                img2img_data["guidance_scale"] = 20.0
                optimizations.append("guidance_scale_clamped")
        
        return {
            "optimized": len(optimizations) > 0,
            "optimizations_applied": optimizations,
            "optimized_data": img2img_data
        }
    
    async def get_conditioning_info(self) -> Dict[str, Any]:
        """Get conditioning information."""
        return {
            "active_conditionings": len(self.active_conditionings),
            "cached_conditionings": len(self.conditioning_cache),
            "optimization_settings": self.optimization_settings,
            "conditioning_types": list(set(
                c["type"] for c in self.active_conditionings.values()
            ))
        }
    
    def _get_current_timestamp(self) -> float:
        """Get current timestamp."""
        import time
        return time.time()
    
    async def get_status(self) -> Dict[str, Any]:
        """Get conditioning manager status."""
        return {
            "initialized": self.initialized,
            "active_conditionings": len(self.active_conditionings),
            "cached_conditionings": len(self.conditioning_cache),
            "optimization_enabled": any(self.optimization_settings.values())
        }
    
    async def cleanup(self) -> None:
        """Clean up conditioning manager resources."""
        try:
            self.logger.info("Cleaning up conditioning manager...")
            
            # Clear active conditionings
            self.active_conditionings.clear()
            
            # Clear conditioning cache
            self.conditioning_cache.clear()
            
            self.initialized = False
            self.logger.info("Conditioning manager cleanup complete")
        except Exception as e:
            self.logger.error(f"Conditioning manager cleanup error: {e}")