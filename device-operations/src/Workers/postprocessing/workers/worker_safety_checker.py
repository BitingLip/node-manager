"""
Safety Checker Worker for SDXL Workers System
=============================================

Migrated from postprocessing/safety_checker.py
Worker for content safety checking and NSFW filtering operations.
"""

import logging
import torch
import numpy as np
from PIL import Image
from typing import List, Dict, Any, Optional, Union, Tuple
import hashlib
import time

logger = logging.getLogger(__name__)


class SafetyCheckerWorker:
    """
    Dedicated worker for safety checking and content filtering operations.
    
    Provides NSFW filtering, content analysis, and safety assessment
    capabilities for generated images.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.initialized = False
        
        # Safety configuration
        self.device = config.get("device", torch.device("cuda" if torch.cuda.is_available() else "cpu"))
        self.dtype = config.get("dtype", torch.float16)
        self.enable_nsfw_filter = config.get("enable_nsfw_filter", True)
        self.enable_content_analysis = config.get("enable_content_analysis", True)
        self.strict_mode = config.get("strict_mode", False)
        
        # Components
        self.safety_checker: Optional[SafetyChecker] = None
        self.content_analyzer: Optional[ContentAnalyzer] = None
        
        # Statistics
        self.total_checked = 0
        self.total_flagged = 0
        self.flagged_history: List[Dict[str, Any]] = []
        
    async def initialize(self) -> bool:
        """Initialize the safety checker worker."""
        try:
            self.logger.info("Initializing safety checker worker...")
            
            # Initialize components
            self.safety_checker = SafetyChecker(self.device, self.dtype, self.enable_nsfw_filter)
            self.content_analyzer = ContentAnalyzer(self.device)
            
            self.initialized = True
            self.logger.info("Safety checker worker initialized successfully")
            return True
        except Exception as e:
            self.logger.error("Failed to initialize safety checker worker: %s", str(e))
            return False
    
    async def process_safety_check(self, request_data: Dict[str, Any]) -> Dict[str, Any]:
        """Process a safety check request."""
        try:
            images = request_data.get("images", [])
            filter_unsafe = request_data.get("filter_unsafe", True)
            analyze_content = request_data.get("analyze_content", True)
            
            if not images:
                raise ValueError("No images provided for safety check")
            
            # Ensure images is a list
            if not isinstance(images, list):
                images = [images]
            
            start_time = time.time()
            results = {
                "total_images": len(images),
                "safe_images": [],
                "unsafe_indices": [],
                "safety_scores": [],
                "content_analysis": [],
                "processing_time": 0.0,
                "filtered": filter_unsafe,
                "status": "completed"
            }
            
            # Safety checking
            if self.enable_nsfw_filter and self.safety_checker:
                safety_result = self.safety_checker.check_images(images, return_scores=True)
                
                # Handle both single bool and tuple returns
                if isinstance(safety_result, tuple):
                    safe_flags, safety_scores = safety_result
                else:
                    safe_flags = safety_result
                    safety_scores = [0.0] * len(images)
                
                # Ensure lists for iteration
                if not isinstance(safe_flags, list):
                    safe_flags = [safe_flags] if isinstance(safe_flags, bool) else list(safe_flags)
                if not isinstance(safety_scores, list):
                    safety_scores = [safety_scores] if isinstance(safety_scores, (int, float)) else list(safety_scores)
                
                results["safety_scores"] = safety_scores
                
                # Track unsafe images
                for i, (is_safe, score) in enumerate(zip(safe_flags, safety_scores)):
                    if not is_safe:
                        results["unsafe_indices"].append(i)
                        self._log_unsafe_image(i, score)
                
                # Filter if requested
                if filter_unsafe:
                    filtered_images = self.safety_checker.filter_images(images)
                    results["safe_images"] = filtered_images
                else:
                    results["safe_images"] = images
            else:
                results["safe_images"] = images
                results["safety_scores"] = [1.0] * len(images)
            
            # Content analysis
            if self.enable_content_analysis and analyze_content and self.content_analyzer:
                for i, image in enumerate(images):
                    analysis = self.content_analyzer.analyze_content(image)
                    results["content_analysis"].append(analysis)
            
            # Update statistics
            self.total_checked += len(images)
            self.total_flagged += len(results["unsafe_indices"])
            
            results["processing_time"] = time.time() - start_time
            
            self.logger.info("Processed %s images, %s flagged", len(images), len(results['unsafe_indices']))
            return results
            
        except Exception as e:
            self.logger.error("Safety check failed: %s", e)
            return {"error": str(e)}
    
    def _log_unsafe_image(self, index: int, score: float) -> None:
        """Log unsafe image detection."""
        log_entry = {
            "timestamp": time.time(),
            "image_index": index,
            "safety_score": score,
            "strict_mode": self.strict_mode
        }
        
        self.flagged_history.append(log_entry)
        
        # Keep only recent entries
        if len(self.flagged_history) > 100:
            self.flagged_history.pop(0)
        
        self.logger.warning("Image %s flagged as unsafe (score: %.3f)", index, score)
    
    async def get_safety_statistics(self) -> Dict[str, Any]:
        """Get safety processing statistics."""
        return {
            "total_checked": self.total_checked,
            "total_flagged": self.total_flagged,
            "flag_rate": self.total_flagged / max(self.total_checked, 1),
            "recent_flags": len([f for f in self.flagged_history if time.time() - f["timestamp"] < 3600]),
            "settings": {
                "nsfw_filter_enabled": self.enable_nsfw_filter,
                "content_analysis_enabled": self.enable_content_analysis,
                "strict_mode": self.strict_mode
            }
        }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get safety checker worker status."""
        return {
            "initialized": self.initialized,
            "nsfw_filter_enabled": self.enable_nsfw_filter,
            "content_analysis_enabled": self.enable_content_analysis,
            "strict_mode": self.strict_mode,
            "total_checked": self.total_checked,
            "total_flagged": self.total_flagged,
            "flag_rate": self.total_flagged / max(self.total_checked, 1),
            "device": str(self.device),
            "dtype": str(self.dtype)
        }
    
    async def cleanup(self) -> None:
        """Clean up safety checker worker resources."""
        try:
            self.logger.info("Cleaning up safety checker worker...")
            
            if self.safety_checker:
                self.safety_checker.unload_model()
            
            self.flagged_history.clear()
            self.initialized = False
            self.logger.info("Safety checker worker cleanup complete")
        except Exception as e:
            self.logger.error("Safety checker worker cleanup error: %s", e)


class SafetyChecker:
    """Base safety checker class."""
    
    def __init__(
        self,
        device: torch.device,
        dtype: torch.dtype = torch.float16,
        enabled: bool = True
    ):
        """Initialize safety checker."""
        self.device = device
        self.dtype = dtype
        self.enabled = enabled
        self.model = None
        self.feature_extractor = None
        self.is_loaded = False
        
        logger.info("Safety checker initialized (enabled: %s)", enabled)
    
    def load_model(self) -> None:
        """Load safety checking model."""
        if not self.enabled:
            return
        
        try:
            from transformers.models.clip import CLIPFeatureExtractor
            from diffusers.pipelines.stable_diffusion.safety_checker import StableDiffusionSafetyChecker
            
            # Load CLIP vision model for feature extraction
            self.feature_extractor = CLIPFeatureExtractor.from_pretrained(
                "openai/clip-vit-base-patch32",
                cache_dir=None
            )
            
            # Load safety checker model
            safety_model = StableDiffusionSafetyChecker.from_pretrained(
                "CompVis/stable-diffusion-safety-checker",
                torch_dtype=self.dtype,
                cache_dir=None
            )
            self.model = safety_model.to(self.device)
            
            self.is_loaded = True
            logger.info("Safety checker model loaded")
            
        except Exception as e:
            logger.warning("Failed to load safety checker: %s", e)
            self.enabled = False
    
    def unload_model(self) -> None:
        """Unload safety model from memory."""
        if self.model is not None:
            del self.model
            self.model = None
        if self.feature_extractor is not None:
            del self.feature_extractor
            self.feature_extractor = None
        
        self.is_loaded = False
        torch.cuda.empty_cache() if torch.cuda.is_available() else None
        logger.debug("Safety checker model unloaded")
    
    def check_images(
        self,
        images: List[Image.Image],
        return_scores: bool = False
    ) -> Union[List[bool], Tuple[List[bool], List[float]]]:
        """Check images for NSFW content."""
        
        if not self.enabled:
            # If disabled, consider all images safe
            safe_flags = [True] * len(images)
            if return_scores:
                return safe_flags, [0.0] * len(images)
            return safe_flags
        
        if not self.is_loaded:
            self.load_model()
        
        if not self.is_loaded:
            # Fallback if model couldn't load
            logger.warning("Safety checker not available, considering all images safe")
            safe_flags = [True] * len(images)
            if return_scores:
                return safe_flags, [0.0] * len(images)
            return safe_flags
        
        try:
            # Prepare images for safety checking
            numpy_images = []
            for img in images:
                if isinstance(img, Image.Image):
                    numpy_images.append(np.array(img))
                else:
                    numpy_images.append(img)
            
            # Extract features
            safety_checker_input = self.feature_extractor(
                numpy_images,
                return_tensors="pt"
            ).to(self.device)
            
            # Run safety check
            with torch.no_grad():
                images_tensor = safety_checker_input.pixel_values.to(self.dtype)
                safety_output = self.model(
                    clip_input=images_tensor,
                    images=images_tensor
                )
            
            # Extract results
            has_nsfw_concept = safety_output.has_nsfw_concepts
            nsfw_scores = getattr(safety_output, 'special_scores', None)
            
            # Convert to safe flags (invert NSFW detection)
            safe_flags = [not nsfw for nsfw in has_nsfw_concept]
            
            if return_scores:
                scores = nsfw_scores if nsfw_scores is not None else [0.0] * len(images)
                return safe_flags, scores
            
            return safe_flags
            
        except Exception as e:
            logger.error("Safety check failed: %s", e)
            # On error, consider all images safe to avoid blocking
            safe_flags = [True] * len(images)
            if return_scores:
                return safe_flags, [0.0] * len(images)
            return safe_flags
    
    def filter_images(
        self,
        images: List[Image.Image],
        replacement_image: Optional[Image.Image] = None
    ) -> List[Image.Image]:
        """Filter images, replacing unsafe ones."""
        
        safe_flags = self.check_images(images)
        filtered_images = []
        
        for i, (image, is_safe) in enumerate(zip(images, safe_flags)):
            if is_safe:
                filtered_images.append(image)
            else:
                logger.warning("Image %s flagged as unsafe, replacing", i)
                if replacement_image:
                    filtered_images.append(replacement_image)
                else:
                    # Create a simple black replacement
                    replacement = Image.new('RGB', image.size, color='black')
                    filtered_images.append(replacement)
        
        return filtered_images


class ContentAnalyzer:
    """Content analysis and classification."""
    
    def __init__(self, device: torch.device):
        """Initialize content analyzer."""
        self.device = device
        self.concept_scores: Dict[str, float] = {}
        
    def analyze_content(self, image: Image.Image) -> Dict[str, Any]:
        """Analyze image content for various concepts."""
        
        analysis = {
            "timestamp": time.time(),
            "image_hash": self._get_image_hash(image),
            "dimensions": image.size,
            "concepts": {},
            "safety_score": 1.0,  # 1.0 = safe, 0.0 = unsafe
            "content_type": "unknown"
        }
        
        # Basic image analysis
        analysis.update(self._analyze_basic_properties(image))
        
        # Color analysis
        analysis.update(self._analyze_colors(image))
        
        return analysis
    
    def _get_image_hash(self, image: Image.Image) -> str:
        """Get hash of image for identification."""
        image_bytes = image.tobytes()
        return hashlib.md5(image_bytes).hexdigest()
    
    def _analyze_basic_properties(self, image: Image.Image) -> Dict[str, Any]:
        """Analyze basic image properties."""
        
        width, height = image.size
        aspect_ratio = width / height
        
        # Determine content type based on aspect ratio
        if 0.9 <= aspect_ratio <= 1.1:
            content_type = "square"
        elif aspect_ratio > 1.5:
            content_type = "landscape"
        elif aspect_ratio < 0.67:
            content_type = "portrait"
        else:
            content_type = "standard"
        
        return {
            "content_type": content_type,
            "aspect_ratio": aspect_ratio,
            "pixel_count": width * height,
            "is_large": width > 1024 or height > 1024
        }
    
    def _analyze_colors(self, image: Image.Image) -> Dict[str, Any]:
        """Analyze color distribution in image."""
        
        # Convert to RGB if needed
        if image.mode != 'RGB':
            image = image.convert('RGB')
        
        # Get color statistics
        img_array = np.array(image)
        
        # Calculate color means
        color_means = np.mean(img_array, axis=(0, 1))
        
        # Calculate color distribution
        brightness = np.mean(color_means)
        contrast = np.std(img_array)
        
        # Determine dominant colors
        dominant_channel = np.argmax(color_means)
        channel_names = ['red', 'green', 'blue']
        
        return {
            "color_analysis": {
                "brightness": float(brightness),
                "contrast": float(contrast),
                "dominant_color": channel_names[dominant_channel],
                "color_means": color_means.tolist(),
                "is_monochrome": contrast < 10,
                "is_high_contrast": contrast > 100
            }
        }


class SafetyManager:
    """High-level safety management."""
    
    def __init__(
        self,
        device: torch.device,
        enable_nsfw_filter: bool = True,
        enable_content_analysis: bool = True,
        strict_mode: bool = False
    ):
        """Initialize safety manager."""
        self.device = device
        self.enable_nsfw_filter = enable_nsfw_filter
        self.enable_content_analysis = enable_content_analysis
        self.strict_mode = strict_mode
        
        # Initialize components
        self.safety_checker = SafetyChecker(device, enabled=enable_nsfw_filter)
        self.content_analyzer = ContentAnalyzer(device)
        
        # Statistics
        self.total_checked = 0
        self.total_flagged = 0
        self.flagged_history: List[Dict[str, Any]] = []
        
        logger.info("Safety manager initialized (NSFW filter: %s, strict: %s)", enable_nsfw_filter, strict_mode)
    
    def process_images(
        self,
        images: List[Image.Image],
        filter_unsafe: bool = True,
        analyze_content: bool = True
    ) -> Dict[str, Any]:
        """Process images through safety pipeline."""
        
        start_time = time.time()
        results = {
            "total_images": len(images),
            "safe_images": [],
            "unsafe_indices": [],
            "safety_scores": [],
            "content_analysis": [],
            "processing_time": 0.0,
            "filtered": filter_unsafe
        }
        
        # Safety checking
        if self.enable_nsfw_filter:
            safety_result = self.safety_checker.check_images(
                images, return_scores=True
            )
            
            # Handle both single bool and tuple returns
            if isinstance(safety_result, tuple):
                safe_flags, safety_scores = safety_result
            else:
                safe_flags = safety_result
                safety_scores = [0.0] * len(images)
            
            # Ensure lists for iteration
            if not isinstance(safe_flags, list):
                safe_flags = [safe_flags] if isinstance(safe_flags, bool) else list(safe_flags)
            if not isinstance(safety_scores, list):
                safety_scores = [safety_scores] if isinstance(safety_scores, (int, float)) else list(safety_scores)
            
            results["safety_scores"] = safety_scores
            
            # Track unsafe images
            for i, (is_safe, score) in enumerate(zip(safe_flags, safety_scores)):
                if not is_safe:
                    results["unsafe_indices"].append(i)
                    self._log_unsafe_image(i, score)
            
            # Filter if requested
            if filter_unsafe:
                filtered_images = self.safety_checker.filter_images(images)
                results["safe_images"] = filtered_images
            else:
                results["safe_images"] = images
        else:
            results["safe_images"] = images
            results["safety_scores"] = [1.0] * len(images)
        
        # Content analysis
        if self.enable_content_analysis and analyze_content:
            for i, image in enumerate(images):
                analysis = self.content_analyzer.analyze_content(image)
                results["content_analysis"].append(analysis)
        
        # Update statistics
        self.total_checked += len(images)
        self.total_flagged += len(results["unsafe_indices"])
        
        results["processing_time"] = time.time() - start_time
        
        logger.info("Processed %s images, %s flagged", len(images), len(results['unsafe_indices']))
        return results
    
    def _log_unsafe_image(self, index: int, score: float) -> None:
        """Log unsafe image detection."""
        log_entry = {
            "timestamp": time.time(),
            "image_index": index,
            "safety_score": score,
            "strict_mode": self.strict_mode
        }
        
        self.flagged_history.append(log_entry)
        
        # Keep only recent entries
        if len(self.flagged_history) > 100:
            self.flagged_history.pop(0)
        
        logger.warning("Image %s flagged as unsafe (score: %.3f)", index, score)
    
    def get_statistics(self) -> Dict[str, Any]:
        """Get safety processing statistics."""
        return {
            "total_checked": self.total_checked,
            "total_flagged": self.total_flagged,
            "flag_rate": self.total_flagged / max(self.total_checked, 1),
            "recent_flags": len([f for f in self.flagged_history if time.time() - f["timestamp"] < 3600]),
            "settings": {
                "nsfw_filter_enabled": self.enable_nsfw_filter,
                "content_analysis_enabled": self.enable_content_analysis,
                "strict_mode": self.strict_mode
            }
        }
    
    def cleanup(self) -> None:
        """Cleanup safety models."""
        self.safety_checker.unload_model()
        logger.info("Safety manager cleanup completed")


# Factory functions for creating safety checker worker and components
def create_safety_checker_worker(config: Optional[Dict[str, Any]] = None) -> SafetyCheckerWorker:
    """
    Factory function to create a safety checker worker instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        SafetyCheckerWorker instance
    """
    return SafetyCheckerWorker(config or {})


def create_safety_manager(
    device: torch.device,
    enable_nsfw_filter: bool = True,
    enable_content_analysis: bool = True,
    strict_mode: bool = False
) -> SafetyManager:
    """Create a safety manager instance."""
    return SafetyManager(
        device=device,
        enable_nsfw_filter=enable_nsfw_filter,
        enable_content_analysis=enable_content_analysis,
        strict_mode=strict_mode
    )
