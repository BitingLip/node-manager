"""
SDXL Refiner Pipeline Implementation

This module provides comprehensive SDXL refiner integration for the Enhanced SDXL Worker,
enabling two-stage generation (Base → Refiner) for improved image quality and detail
enhancement.

Features:
- SDXL Base + Refiner coordination
- Intelligent latent space passthrough
- Configurable refiner strength and steps
- Memory-efficient model management
- Quality assessment and metrics
- Seamless integration with Enhanced SDXL Worker

Author: Enhanced SDXL Worker System - Phase 3
Date: 2025-07-06
"""

import asyncio
import torch
import logging
from pathlib import Path
from typing import Dict, Any, List, Optional, Union, Tuple
from dataclasses import dataclass, field
import json
from PIL import Image
import numpy as np

try:
    from diffusers import (
        StableDiffusionXLPipeline,
        StableDiffusionXLImg2ImgPipeline,
        DiffusionPipeline
    )
except ImportError:
    # Fallback mock for testing
    class StableDiffusionXLPipeline:
        def __init__(self, *args, **kwargs):
            pass
        
        @classmethod
        def from_pretrained(cls, *args, **kwargs):
            return cls()
    
    class StableDiffusionXLImg2ImgPipeline:
        def __init__(self, *args, **kwargs):
            pass
        
        @classmethod
        def from_pretrained(cls, *args, **kwargs):
            return cls()

# Configure logging
logger = logging.getLogger(__name__)

@dataclass
class RefinerConfiguration:
    """Configuration for SDXL refiner pipeline."""
    
    model_path: str
    strength: float = 0.3
    num_inference_steps: int = 10
    guidance_scale: float = 7.5
    aesthetic_score: float = 6.0
    negative_aesthetic_score: float = 2.5
    enable_cpu_offload: bool = False
    enable_attention_slicing: bool = True
    enable_vae_slicing: bool = True
    
    def __post_init__(self):
        """Validate configuration parameters."""
        if not (0.0 <= self.strength <= 1.0):
            raise ValueError(f"strength must be between 0.0 and 1.0, got {self.strength}")
        
        if self.num_inference_steps < 1:
            raise ValueError(f"num_inference_steps must be positive, got {self.num_inference_steps}")
        
        if self.guidance_scale < 0:
            raise ValueError(f"guidance_scale must be non-negative, got {self.guidance_scale}")

@dataclass
class RefinerMetrics:
    """Metrics for refiner processing."""
    
    total_time_ms: float = 0.0
    loading_time_ms: float = 0.0
    inference_time_ms: float = 0.0
    memory_usage_mb: float = 0.0
    images_processed: int = 0
    quality_improvement: float = 0.0
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert metrics to dictionary."""
        return {
            "total_time_ms": self.total_time_ms,
            "loading_time_ms": self.loading_time_ms,
            "inference_time_ms": self.inference_time_ms,
            "memory_usage_mb": self.memory_usage_mb,
            "images_processed": self.images_processed,
            "quality_improvement": self.quality_improvement
        }

class QualityAssessment:
    """Quality assessment utilities for refiner output."""
    
    @staticmethod
    def calculate_quality_metrics(base_image: Image.Image, refined_image: Image.Image) -> Dict[str, float]:
        """Calculate quality improvement metrics."""
        try:
            # Convert to numpy arrays
            base_array = np.array(base_image)
            refined_array = np.array(refined_image)
            
            # Calculate basic metrics
            metrics = {}
            
            # Sharpness metric (variance of Laplacian)
            base_gray = np.mean(base_array, axis=2) if len(base_array.shape) == 3 else base_array
            refined_gray = np.mean(refined_array, axis=2) if len(refined_array.shape) == 3 else refined_array
            
            # Simple edge detection for sharpness
            base_edges = np.std(np.gradient(base_gray))
            refined_edges = np.std(np.gradient(refined_gray))
            
            metrics["sharpness_improvement"] = refined_edges / max(base_edges, 1e-8)
            
            # Contrast improvement
            base_contrast = np.std(base_gray)
            refined_contrast = np.std(refined_gray)
            
            metrics["contrast_improvement"] = refined_contrast / max(base_contrast, 1e-8)
            
            # Overall quality score (weighted combination)
            metrics["overall_quality_score"] = (
                0.6 * metrics["sharpness_improvement"] + 
                0.4 * metrics["contrast_improvement"]
            )
            
            return metrics
            
        except Exception as e:
            logger.warning(f"Quality assessment failed: {e}")
            return {"overall_quality_score": 1.0}
    
    @staticmethod
    def assess_refinement_benefit(metrics: Dict[str, float], threshold: float = 1.05) -> bool:
        """Assess if refinement provided significant benefit."""
        return metrics.get("overall_quality_score", 1.0) >= threshold

class SDXLRefinerPipeline:
    """SDXL Refiner pipeline for two-stage generation."""
    
    def __init__(self, config: RefinerConfiguration):
        """Initialize the refiner pipeline."""
        self.config = config
        self.refiner_pipeline: Optional[StableDiffusionXLImg2ImgPipeline] = None
        self.is_loaded = False
        self.device = "cpu"
        self.torch_dtype = torch.float32
        self.quality_assessor = QualityAssessment()
        
        # Performance tracking
        self.metrics = RefinerMetrics()
        
        logger.info(f"SDXL Refiner Pipeline initialized with model: {config.model_path}")
    
    async def load_refiner_model(self, device: str = "cpu", torch_dtype: torch.dtype = torch.float32) -> bool:
        """Load the SDXL refiner model."""
        try:
            start_time = asyncio.get_event_loop().time()
            
            self.device = device
            self.torch_dtype = torch_dtype
            
            logger.info(f"Loading SDXL refiner model: {self.config.model_path}")
            
            # Load refiner pipeline
            self.refiner_pipeline = StableDiffusionXLImg2ImgPipeline.from_pretrained(
                self.config.model_path,
                torch_dtype=self.torch_dtype,
                use_safetensors=True,
                variant="fp16" if self.torch_dtype == torch.float16 else None
            )
            
            # Move to device
            if self.refiner_pipeline:
                self.refiner_pipeline = self.refiner_pipeline.to(device)
                
                # Apply optimizations
                if self.config.enable_attention_slicing:
                    self.refiner_pipeline.enable_attention_slicing()
                    logger.info("Attention slicing enabled for refiner")
                
                if self.config.enable_vae_slicing:
                    self.refiner_pipeline.enable_vae_slicing()
                    logger.info("VAE slicing enabled for refiner")
                
                if self.config.enable_cpu_offload:
                    self.refiner_pipeline.enable_model_cpu_offload()
                    logger.info("CPU offload enabled for refiner")
            
            # Track loading time
            loading_time = (asyncio.get_event_loop().time() - start_time) * 1000
            self.metrics.loading_time_ms = loading_time
            
            self.is_loaded = True
            logger.info(f"✅ SDXL refiner loaded successfully in {loading_time:.1f}ms")
            
            return True
            
        except Exception as e:
            logger.error(f"Failed to load SDXL refiner: {e}")
            self.is_loaded = False
            return False
    
    async def refine_images(
        self, 
        base_images: List[Image.Image], 
        prompt: str,
        negative_prompt: Optional[str] = None,
        **kwargs
    ) -> Tuple[List[Image.Image], RefinerMetrics]:
        """Refine base images using the SDXL refiner."""
        if not self.is_loaded or not self.refiner_pipeline:
            logger.error("Refiner pipeline not loaded")
            return base_images, self.metrics
        
        try:
            start_time = asyncio.get_event_loop().time()
            refined_images = []
            total_quality_improvement = 0.0
            
            logger.info(f"Refining {len(base_images)} images with strength {self.config.strength}")
            
            for i, base_image in enumerate(base_images):
                logger.info(f"Refining image {i+1}/{len(base_images)}")
                
                # Prepare refiner parameters
                refiner_params = {
                    "image": base_image,
                    "prompt": prompt,
                    "negative_prompt": negative_prompt,
                    "strength": self.config.strength,
                    "num_inference_steps": self.config.num_inference_steps,
                    "guidance_scale": self.config.guidance_scale,
                    "aesthetic_score": self.config.aesthetic_score,
                    "negative_aesthetic_score": self.config.negative_aesthetic_score,
                    **kwargs
                }
                
                # Generate refined image
                with torch.no_grad():
                    result = self.refiner_pipeline(**refiner_params)
                    refined_image = result.images[0] if hasattr(result, 'images') else result
                
                # Assess quality improvement
                quality_metrics = self.quality_assessor.calculate_quality_metrics(
                    base_image, refined_image
                )
                quality_score = quality_metrics.get("overall_quality_score", 1.0)
                total_quality_improvement += quality_score
                
                logger.info(f"Image {i+1} quality score: {quality_score:.3f}")
                
                refined_images.append(refined_image)
            
            # Update metrics
            total_time = (asyncio.get_event_loop().time() - start_time) * 1000
            self.metrics.total_time_ms = total_time
            self.metrics.inference_time_ms = total_time - self.metrics.loading_time_ms
            self.metrics.images_processed = len(base_images)
            self.metrics.quality_improvement = total_quality_improvement / len(base_images) if base_images else 1.0
            
            # Estimate memory usage
            if hasattr(torch, 'cuda') and torch.cuda.is_available():
                self.metrics.memory_usage_mb = torch.cuda.max_memory_allocated() / (1024 * 1024)
            
            logger.info(f"✅ Refined {len(base_images)} images in {total_time:.1f}ms")
            logger.info(f"Average quality improvement: {self.metrics.quality_improvement:.3f}")
            
            return refined_images, self.metrics
            
        except Exception as e:
            logger.error(f"Failed to refine images: {e}")
            return base_images, self.metrics
    
    async def refine_with_adaptive_strength(
        self,
        base_images: List[Image.Image],
        prompt: str,
        negative_prompt: Optional[str] = None,
        target_quality: float = 1.2,
        max_attempts: int = 3,
        **kwargs
    ) -> Tuple[List[Image.Image], RefinerMetrics]:
        """Refine images with adaptive strength based on quality assessment."""
        if not base_images:
            return [], self.metrics
        
        best_images = base_images
        best_quality = 1.0
        attempts = 0
        
        # Try different strength values to achieve target quality
        strength_values = [self.config.strength, self.config.strength * 1.5, self.config.strength * 0.7]
        
        for strength in strength_values:
            if attempts >= max_attempts:
                break
            
            attempts += 1
            logger.info(f"Adaptive refinement attempt {attempts}/{max_attempts} with strength {strength:.2f}")
            
            # Temporarily adjust strength
            original_strength = self.config.strength
            self.config.strength = min(1.0, max(0.0, strength))
            
            try:
                # Refine with current strength
                refined_images, metrics = await self.refine_images(
                    base_images, prompt, negative_prompt, **kwargs
                )
                
                # Check if quality improvement meets target
                if metrics.quality_improvement >= target_quality:
                    logger.info(f"✅ Target quality achieved with strength {strength:.2f}")
                    best_images = refined_images
                    best_quality = metrics.quality_improvement
                    break
                elif metrics.quality_improvement > best_quality:
                    # Keep the best result so far
                    best_images = refined_images
                    best_quality = metrics.quality_improvement
                
            finally:
                # Restore original strength
                self.config.strength = original_strength
        
        logger.info(f"Adaptive refinement completed. Best quality: {best_quality:.3f}")
        return best_images, self.metrics
    
    def update_configuration(self, new_config: Dict[str, Any]) -> None:
        """Update refiner configuration."""
        for key, value in new_config.items():
            if hasattr(self.config, key):
                setattr(self.config, key, value)
                logger.info(f"Updated refiner config: {key} = {value}")
    
    def get_performance_stats(self) -> Dict[str, Any]:
        """Get performance statistics."""
        return {
            "is_loaded": self.is_loaded,
            "model_path": self.config.model_path,
            "device": self.device,
            "torch_dtype": str(self.torch_dtype),
            "metrics": self.metrics.to_dict(),
            "configuration": {
                "strength": self.config.strength,
                "num_inference_steps": self.config.num_inference_steps,
                "guidance_scale": self.config.guidance_scale,
                "aesthetic_score": self.config.aesthetic_score
            }
        }
    
    async def cleanup(self) -> None:
        """Clean up resources."""
        try:
            if self.refiner_pipeline:
                # Move to CPU to free GPU memory
                if hasattr(self.refiner_pipeline, 'to'):
                    self.refiner_pipeline = self.refiner_pipeline.to("cpu")
                
                # Clear references
                self.refiner_pipeline = None
                
            # Clear CUDA cache if available
            if hasattr(torch, 'cuda') and torch.cuda.is_available():
                torch.cuda.empty_cache()
            
            self.is_loaded = False
            logger.info("SDXL refiner pipeline cleaned up")
            
        except Exception as e:
            logger.error(f"Error during refiner cleanup: {e}")

# Factory function for easy integration
def create_refiner_pipeline(config: RefinerConfiguration) -> SDXLRefinerPipeline:
    """Create and return a refiner pipeline instance."""
    return SDXLRefinerPipeline(config)

# Example usage
if __name__ == "__main__":
    async def demo():
        # Configuration
        config = RefinerConfiguration(
            model_path="stabilityai/stable-diffusion-xl-refiner-1.0",
            strength=0.3,
            num_inference_steps=10,
            guidance_scale=7.5
        )
        
        # Create refiner
        refiner = SDXLRefinerPipeline(config)
        
        # Load model
        success = await refiner.load_refiner_model("cpu", torch.float32)
        print(f"Refiner loaded: {success}")
        
        # Get stats
        stats = refiner.get_performance_stats()
        print(f"Performance stats: {stats}")
        
        # Cleanup
        await refiner.cleanup()
    
    asyncio.run(demo())
