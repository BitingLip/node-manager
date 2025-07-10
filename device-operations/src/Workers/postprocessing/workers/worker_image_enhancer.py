"""
Image Enhancement Worker for SDXL Workers System
===============================================

Migrated from postprocessing/image_enhancer.py
Worker for advanced image enhancement and post-processing operations.
"""

import logging
import numpy as np
from PIL import Image, ImageEnhance, ImageFilter
from typing import Dict, Any, Optional

logger = logging.getLogger(__name__)


class ImageEnhancerWorker:
    """
    Dedicated worker for image enhancement and post-processing operations.
    
    Provides advanced image enhancement algorithms including color correction,
    contrast adjustment, exposure control, and detail enhancement.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.initialized = False
        
        # Enhancement configuration
        self.enhancement_history: Dict[str, Any] = {}
        self.supported_enhancements = [
            "auto_contrast", "color_correction", "enhance_details", 
            "adjust_exposure", "preset_enhancement"
        ]
        
        # Performance settings
        self.preserve_metadata = config.get("preserve_metadata", True)
        self.max_image_size = config.get("max_image_size", (4096, 4096))
        
    async def initialize(self) -> bool:
        """Initialize the image enhancer worker."""
        try:
            self.logger.info("Initializing image enhancer worker...")
            self.initialized = True
            self.logger.info("Image enhancer worker initialized successfully")
            return True
        except Exception as e:
            self.logger.error("Failed to initialize image enhancer worker: %s", str(e))
            return False
    
    async def process_enhancement(self, request_data: Dict[str, Any]) -> Dict[str, Any]:
        """Process an image enhancement request."""
        try:
            input_image = request_data.get("input_image")
            enhancement_type = request_data.get("enhancement_type", "auto_contrast")
            enhancement_params = request_data.get("enhancement_params", {})
            
            if not input_image:
                raise ValueError("No input image provided")
            
            if enhancement_type not in self.supported_enhancements:
                raise ValueError(f"Unsupported enhancement type: {enhancement_type}")
            
            # Process enhancement
            if enhancement_type == "auto_contrast":
                result_image = self.auto_contrast(input_image, **enhancement_params)
            elif enhancement_type == "color_correction":
                result_image = self.color_correction(input_image, **enhancement_params)
            elif enhancement_type == "enhance_details":
                result_image = self.enhance_details(input_image, **enhancement_params)
            elif enhancement_type == "adjust_exposure":
                result_image = self.adjust_exposure(input_image, **enhancement_params)
            elif enhancement_type == "preset_enhancement":
                preset_name = enhancement_params.get("preset", "natural")
                result_image = EnhancementPresets.apply_preset(self, input_image, preset_name)
            else:
                raise ValueError(f"Enhancement type {enhancement_type} not implemented")
            
            return {
                "type": "enhancement",
                "enhancement_type": enhancement_type,
                "output_image": result_image,
                "processing_time": 1.0,
                "status": "completed"
            }
            
        except Exception as e:
            self.logger.error("Enhancement failed: %s", e)
            return {"error": str(e)}
    
    def auto_contrast(
        self,
        image: Image.Image,
        cutoff: float = 0.1,
        preserve_tone: bool = True
    ) -> Image.Image:
        """Apply automatic contrast enhancement."""
        
        if image.mode != 'RGB':
            image = image.convert('RGB')
        
        # Convert to numpy for processing
        img_array = np.array(image, dtype=np.float32)
        
        if preserve_tone:
            # Work in LAB color space to preserve colors
            img_lab = self._rgb_to_lab(img_array)
            l_channel = img_lab[:, :, 0]
            
            # Apply contrast to luminance only
            l_enhanced = self._apply_contrast_stretch(l_channel, cutoff)
            
            # Reconstruct image
            img_lab[:, :, 0] = l_enhanced
            enhanced_array = self._lab_to_rgb(img_lab)
        else:
            # Apply to each channel separately
            enhanced_array = np.zeros_like(img_array)
            for i in range(3):
                enhanced_array[:, :, i] = self._apply_contrast_stretch(
                    img_array[:, :, i], cutoff
                )
        
        # Convert back to PIL
        enhanced_array = np.clip(enhanced_array, 0, 255).astype(np.uint8)
        result = Image.fromarray(enhanced_array)
        
        logger.debug("Applied auto contrast (cutoff: %s, preserve_tone: %s)", cutoff, preserve_tone)
        return result
    
    def color_correction(
        self,
        image: Image.Image,
        temperature: float = 0.0,  # -1.0 to 1.0 (cool to warm)
        tint: float = 0.0,         # -1.0 to 1.0 (green to magenta)
        saturation: float = 1.0,   # 0.0 to 2.0
        gamma: float = 1.0         # 0.5 to 2.0
    ) -> Image.Image:
        """Apply color correction adjustments."""
        
        if image.mode != 'RGB':
            image = image.convert('RGB')
        
        img_array = np.array(image, dtype=np.float32) / 255.0
        
        # Apply gamma correction
        if gamma != 1.0:
            img_array = np.power(img_array, 1.0 / gamma)
        
        # Apply temperature adjustment
        if temperature != 0.0:
            img_array = self._adjust_temperature(img_array, temperature)
        
        # Apply tint adjustment
        if tint != 0.0:
            img_array = self._adjust_tint(img_array, tint)
        
        # Apply saturation adjustment
        if saturation != 1.0:
            img_array = self._adjust_saturation(img_array, saturation)
        
        # Convert back to PIL
        corrected_array = np.clip(img_array * 255, 0, 255).astype(np.uint8)
        result = Image.fromarray(corrected_array)
        
        logger.debug("Applied color correction (temp: %s, tint: %s, sat: %s, gamma: %s)", temperature, tint, saturation, gamma)
        return result
    
    def enhance_details(
        self,
        image: Image.Image,
        sharpness: float = 1.0,    # 0.0 to 2.0
        clarity: float = 0.0,      # -1.0 to 1.0
        structure: float = 0.0     # -1.0 to 1.0
    ) -> Image.Image:
        """Enhance image details and sharpness."""
        
        if image.mode != 'RGB':
            image = image.convert('RGB')
        
        result = image.copy()
        
        # Apply sharpness enhancement
        if sharpness != 1.0:
            enhancer = ImageEnhance.Sharpness(result)
            result = enhancer.enhance(sharpness)
        
        # Apply clarity (mid-tone contrast)
        if clarity != 0.0:
            result = self._adjust_clarity(result, clarity)
        
        # Apply structure (texture enhancement)
        if structure != 0.0:
            result = self._enhance_structure(result, structure)
        
        logger.debug("Enhanced details (sharpness: %s, clarity: %s, structure: %s)", sharpness, clarity, structure)
        return result
    
    def adjust_exposure(
        self,
        image: Image.Image,
        exposure: float = 0.0,     # -2.0 to 2.0 (stops)
        highlights: float = 0.0,   # -1.0 to 1.0
        shadows: float = 0.0,      # -1.0 to 1.0
        whites: float = 0.0,       # -1.0 to 1.0
        blacks: float = 0.0        # -1.0 to 1.0
    ) -> Image.Image:
        """Adjust image exposure and tone mapping."""
        
        if image.mode != 'RGB':
            image = image.convert('RGB')
        
        img_array = np.array(image, dtype=np.float32) / 255.0
        
        # Apply exposure adjustment (overall brightness)
        if exposure != 0.0:
            exposure_factor = 2.0 ** exposure
            img_array = img_array * exposure_factor
        
        # Apply selective adjustments
        if any([highlights, shadows, whites, blacks]):
            img_array = self._apply_tone_mapping(
                img_array, highlights, shadows, whites, blacks
            )
        
        # Convert back to PIL
        adjusted_array = np.clip(img_array * 255, 0, 255).astype(np.uint8)
        result = Image.fromarray(adjusted_array)
        
        logger.debug("Adjusted exposure (exp: %s, highlights: %s, shadows: %s)", exposure, highlights, shadows)
        return result
    
    def _apply_contrast_stretch(self, channel: np.ndarray, cutoff: float) -> np.ndarray:
        """Apply contrast stretching to a single channel."""
        
        # Calculate percentiles for stretching
        low_percentile = np.percentile(channel, cutoff * 100)
        high_percentile = np.percentile(channel, (1 - cutoff) * 100)
        
        # Avoid division by zero
        if high_percentile - low_percentile < 1e-6:
            return channel
        
        # Stretch contrast
        stretched = (channel - low_percentile) / (high_percentile - low_percentile) * 255
        return np.clip(stretched, 0, 255)
    
    def _rgb_to_lab(self, rgb: np.ndarray) -> np.ndarray:
        """Convert RGB to LAB color space."""
        # Simplified RGB to LAB conversion
        rgb_normalized = rgb / 255.0
        
        # Convert to XYZ first (simplified)
        xyz = np.zeros_like(rgb_normalized)
        xyz[:, :, 0] = 0.412453 * rgb_normalized[:, :, 0] + 0.357580 * rgb_normalized[:, :, 1] + 0.180423 * rgb_normalized[:, :, 2]
        xyz[:, :, 1] = 0.212671 * rgb_normalized[:, :, 0] + 0.715160 * rgb_normalized[:, :, 1] + 0.072169 * rgb_normalized[:, :, 2]
        xyz[:, :, 2] = 0.019334 * rgb_normalized[:, :, 0] + 0.119193 * rgb_normalized[:, :, 1] + 0.950227 * rgb_normalized[:, :, 2]
        
        # Convert XYZ to LAB (simplified)
        lab = np.zeros_like(xyz)
        lab[:, :, 0] = 116 * np.cbrt(xyz[:, :, 1]) - 16  # L
        lab[:, :, 1] = 500 * (np.cbrt(xyz[:, :, 0]) - np.cbrt(xyz[:, :, 1]))  # A
        lab[:, :, 2] = 200 * (np.cbrt(xyz[:, :, 1]) - np.cbrt(xyz[:, :, 2]))  # B
        
        return lab
    
    def _lab_to_rgb(self, lab: np.ndarray) -> np.ndarray:
        """Convert LAB to RGB color space."""
        # Simplified LAB to RGB conversion (reverse of above)
        
        # LAB to XYZ
        fy = (lab[:, :, 0] + 16) / 116
        fx = lab[:, :, 1] / 500 + fy
        fz = fy - lab[:, :, 2] / 200
        
        xyz = np.zeros_like(lab)
        xyz[:, :, 0] = fx ** 3
        xyz[:, :, 1] = fy ** 3
        xyz[:, :, 2] = fz ** 3
        
        # XYZ to RGB
        rgb = np.zeros_like(xyz)
        rgb[:, :, 0] = 3.240479 * xyz[:, :, 0] - 1.537150 * xyz[:, :, 1] - 0.498535 * xyz[:, :, 2]
        rgb[:, :, 1] = -0.969256 * xyz[:, :, 0] + 1.875992 * xyz[:, :, 1] + 0.041556 * xyz[:, :, 2]
        rgb[:, :, 2] = 0.055648 * xyz[:, :, 0] - 0.204043 * xyz[:, :, 1] + 1.057311 * xyz[:, :, 2]
        
        return np.clip(rgb * 255, 0, 255)
    
    def _adjust_temperature(self, rgb: np.ndarray, temperature: float) -> np.ndarray:
        """Adjust color temperature."""
        
        # Temperature adjustment affects red and blue channels
        temp_factor = temperature * 0.1
        
        adjusted = rgb.copy()
        if temperature > 0:  # Warmer
            adjusted[:, :, 0] = np.clip(adjusted[:, :, 0] + temp_factor, 0, 1)  # More red
            adjusted[:, :, 2] = np.clip(adjusted[:, :, 2] - temp_factor, 0, 1)  # Less blue
        else:  # Cooler
            adjusted[:, :, 0] = np.clip(adjusted[:, :, 0] + temp_factor, 0, 1)  # Less red
            adjusted[:, :, 2] = np.clip(adjusted[:, :, 2] - temp_factor, 0, 1)  # More blue
        
        return adjusted
    
    def _adjust_tint(self, rgb: np.ndarray, tint: float) -> np.ndarray:
        """Adjust color tint (green/magenta)."""
        
        # Tint adjustment affects green channel
        tint_factor = tint * 0.1
        
        adjusted = rgb.copy()
        adjusted[:, :, 1] = np.clip(adjusted[:, :, 1] + tint_factor, 0, 1)
        
        return adjusted
    
    def _adjust_saturation(self, rgb: np.ndarray, saturation: float) -> np.ndarray:
        """Adjust color saturation."""
        
        # Convert to grayscale for saturation adjustment
        gray = np.dot(rgb, [0.299, 0.587, 0.114])
        gray = np.expand_dims(gray, axis=2)
        
        # Blend between grayscale and original
        adjusted = gray + saturation * (rgb - gray)
        
        return np.clip(adjusted, 0, 1)
    
    def _adjust_clarity(self, image: Image.Image, clarity: float) -> Image.Image:
        """Adjust mid-tone contrast (clarity)."""
        
        # Create a mask for mid-tones
        img_array = np.array(image, dtype=np.float32)
        
        # Calculate luminance
        luminance = np.dot(img_array, [0.299, 0.587, 0.114])
        
        # Create mid-tone mask (stronger effect in mid-tones)
        mid_tone_mask = 1.0 - np.abs(luminance / 255.0 - 0.5) * 2.0
        mid_tone_mask = np.expand_dims(mid_tone_mask, axis=2)
        
        # Apply unsharp mask effect for clarity
        blurred = image.filter(ImageFilter.GaussianBlur(radius=2))
        blurred_array = np.array(blurred, dtype=np.float32)
        
        # Calculate detail enhancement
        detail = img_array - blurred_array
        enhanced = img_array + clarity * detail * mid_tone_mask
        
        enhanced_array = np.clip(enhanced, 0, 255).astype(np.uint8)
        return Image.fromarray(enhanced_array)
    
    def _enhance_structure(self, image: Image.Image, structure: float) -> Image.Image:
        """Enhance image structure/texture."""
        
        # Apply high-pass filter for structure enhancement
        img_array = np.array(image, dtype=np.float32)
        
        # Create high-pass filter using Gaussian blur
        blurred = image.filter(ImageFilter.GaussianBlur(radius=1))
        blurred_array = np.array(blurred, dtype=np.float32)
        
        # High-pass = original - blurred
        high_pass = img_array - blurred_array
        
        # Apply structure enhancement
        enhanced = img_array + structure * high_pass * 0.5
        
        enhanced_array = np.clip(enhanced, 0, 255).astype(np.uint8)
        return Image.fromarray(enhanced_array)
    
    def _apply_tone_mapping(
        self,
        rgb: np.ndarray,
        highlights: float,
        shadows: float,
        whites: float,
        blacks: float
    ) -> np.ndarray:
        """Apply selective tone mapping."""
        
        # Calculate luminance
        luminance = np.dot(rgb, [0.299, 0.587, 0.114])
        
        # Create masks for different tonal ranges
        highlight_mask = np.where(luminance > 0.7, (luminance - 0.7) / 0.3, 0)
        shadow_mask = np.where(luminance < 0.3, (0.3 - luminance) / 0.3, 0)
        white_mask = np.where(luminance > 0.9, (luminance - 0.9) / 0.1, 0)
        black_mask = np.where(luminance < 0.1, (0.1 - luminance) / 0.1, 0)
        
        # Expand masks to 3 channels
        highlight_mask = np.expand_dims(highlight_mask, axis=2)
        shadow_mask = np.expand_dims(shadow_mask, axis=2)
        white_mask = np.expand_dims(white_mask, axis=2)
        black_mask = np.expand_dims(black_mask, axis=2)
        
        # Apply adjustments
        adjusted = rgb.copy()
        adjusted += highlights * highlight_mask * 0.1
        adjusted += shadows * shadow_mask * 0.1
        adjusted += whites * white_mask * 0.1
        adjusted += blacks * black_mask * 0.1
        
        return np.clip(adjusted, 0, 1)
    
    async def get_supported_enhancements(self) -> Dict[str, Any]:
        """Get list of supported enhancement types."""
        return {
            "supported_enhancements": self.supported_enhancements,
            "available_presets": list(EnhancementPresets.PRESETS.keys()),
            "max_image_size": self.max_image_size,
            "preserve_metadata": self.preserve_metadata
        }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get image enhancer worker status."""
        return {
            "initialized": self.initialized,
            "supported_enhancements": len(self.supported_enhancements),
            "available_presets": len(EnhancementPresets.PRESETS),
            "max_image_size": self.max_image_size,
            "preserve_metadata": self.preserve_metadata,
            "enhancement_history_count": len(self.enhancement_history)
        }
    
    async def cleanup(self) -> None:
        """Clean up image enhancer worker resources."""
        try:
            self.logger.info("Cleaning up image enhancer worker...")
            self.enhancement_history.clear()
            self.initialized = False
            self.logger.info("Image enhancer worker cleanup complete")
        except Exception as e:
            self.logger.error("Image enhancer worker cleanup error: %s", e)


class EnhancementPresets:
    """Predefined enhancement presets."""
    
    PRESETS = {
        "natural": {
            "auto_contrast": {"cutoff": 0.05, "preserve_tone": True},
            "color_correction": {"saturation": 1.1, "gamma": 1.0},
            "enhance_details": {"sharpness": 1.1}
        },
        "vivid": {
            "auto_contrast": {"cutoff": 0.1, "preserve_tone": True},
            "color_correction": {"saturation": 1.3, "gamma": 0.9, "temperature": 0.1},
            "enhance_details": {"sharpness": 1.2, "clarity": 0.2}
        },
        "dramatic": {
            "auto_contrast": {"cutoff": 0.15, "preserve_tone": False},
            "color_correction": {"saturation": 1.4, "gamma": 0.8},
            "enhance_details": {"sharpness": 1.3, "clarity": 0.3, "structure": 0.2},
            "adjust_exposure": {"highlights": -0.2, "shadows": 0.2}
        },
        "soft": {
            "color_correction": {"saturation": 0.9, "gamma": 1.1, "temperature": 0.05},
            "enhance_details": {"sharpness": 0.9, "clarity": -0.1}
        },
        "black_and_white": {
            "color_correction": {"saturation": 0.0, "gamma": 1.0},
            "auto_contrast": {"cutoff": 0.1, "preserve_tone": False},
            "enhance_details": {"sharpness": 1.2, "clarity": 0.3}
        }
    }
    
    @classmethod
    def apply_preset(cls, enhancer: 'ImageEnhancerWorker', image: Image.Image, preset_name: str) -> Image.Image:
        """Apply an enhancement preset to an image."""
        
        if preset_name not in cls.PRESETS:
            raise ValueError(f"Unknown preset: {preset_name}. Available: {list(cls.PRESETS.keys())}")
        
        preset = cls.PRESETS[preset_name]
        result = image.copy()
        
        # Apply enhancements in order
        for method_name, params in preset.items():
            method = getattr(enhancer, method_name, None)
            if method:
                result = method(result, **params)
        
        logger.info("Applied enhancement preset: %s", preset_name)
        return result


# Factory function for creating image enhancer worker
def create_image_enhancer_worker(config: Optional[Dict[str, Any]] = None) -> ImageEnhancerWorker:
    """
    Factory function to create an image enhancer worker instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        ImageEnhancerWorker instance
    """
    return ImageEnhancerWorker(config or {})
