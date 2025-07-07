"""
Upscaling modules for SDXL post-processing.
Supports Real-ESRGAN, GFPGAN, and other upscaling methods.
"""

import logging
import torch
import numpy as np
from PIL import Image
from typing import Optional, Union, List, Dict, Any, Tuple
import cv2
import os

logger = logging.getLogger(__name__)


class BaseUpscaler:
    """Base class for upscaling methods."""
    
    def __init__(
        self,
        device: torch.device,
        scale_factor: int = 4,
        model_path: Optional[str] = None
    ):
        """Initialize base upscaler."""
        self.device = device
        self.scale_factor = scale_factor
        self.model_path = model_path
        self.model = None
        self.is_loaded = False
        
    def load_model(self) -> None:
        """Load upscaling model."""
        raise NotImplementedError
    
    def unload_model(self) -> None:
        """Unload model from memory."""
        if self.model is not None:
            del self.model
            self.model = None
        self.is_loaded = False
        torch.cuda.empty_cache() if torch.cuda.is_available() else None
    
    def upscale(
        self,
        image: Union[Image.Image, np.ndarray, torch.Tensor],
        scale_factor: Optional[int] = None
    ) -> Image.Image:
        """Upscale image."""
        raise NotImplementedError
    
    def preprocess_image(
        self,
        image: Union[Image.Image, np.ndarray, torch.Tensor]
    ) -> np.ndarray:
        """Preprocess image for upscaling."""
        
        if isinstance(image, Image.Image):
            img_array = np.array(image)
        elif isinstance(image, torch.Tensor):
            if image.dim() == 4:
                image = image.squeeze(0)
            if image.dim() == 3 and image.shape[0] == 3:
                # CHW to HWC
                image = image.permute(1, 2, 0)
            # Denormalize if in [-1, 1] range
            if image.min() < 0:
                image = (image + 1) / 2
            img_array = (image.cpu().numpy() * 255).astype(np.uint8)
        elif isinstance(image, np.ndarray):
            img_array = image
        else:
            raise ValueError(f"Unsupported image type: {type(image)}")
        
        # Ensure RGB format
        if len(img_array.shape) == 3 and img_array.shape[2] == 3:
            img_array = cv2.cvtColor(img_array, cv2.COLOR_RGB2BGR)
        
        return img_array
    
    def postprocess_image(self, img_array: np.ndarray) -> Image.Image:
        """Postprocess upscaled image."""
        
        # Convert BGR to RGB
        if len(img_array.shape) == 3 and img_array.shape[2] == 3:
            img_array = cv2.cvtColor(img_array, cv2.COLOR_BGR2RGB)
        
        # Ensure uint8 format
        if img_array.dtype != np.uint8:
            img_array = np.clip(img_array, 0, 255).astype(np.uint8)
        
        return Image.fromarray(img_array)


class RealESRGANUpscaler(BaseUpscaler):
    """Real-ESRGAN upscaler implementation."""
    
    def __init__(
        self,
        device: torch.device,
        scale_factor: int = 4,
        model_name: str = "RealESRGAN_x4plus",
        model_path: Optional[str] = None
    ):
        """Initialize Real-ESRGAN upscaler."""
        super().__init__(device, scale_factor, model_path)
        self.model_name = model_name
        
        # Try to import Real-ESRGAN
        try:
            from realesrgan import RealESRGANer
            from basicsr.archs.rrdbnet_arch import RRDBNet
            self.RealESRGANer = RealESRGANer
            self.RRDBNet = RRDBNet
            self.available = True
        except ImportError:
            logger.warning("Real-ESRGAN not available, install with: pip install realesrgan")
            self.available = False
        
        logger.info(f"Real-ESRGAN upscaler initialized: {model_name}, scale: {scale_factor}")
    
    def load_model(self) -> None:
        """Load Real-ESRGAN model."""
        if not self.available:
            raise RuntimeError("Real-ESRGAN not available")
        
        if self.is_loaded:
            return
        
        try:
            # Model configurations
            model_configs = {
                "RealESRGAN_x4plus": {
                    "num_in_ch": 3,
                    "num_out_ch": 3,
                    "num_feat": 64,
                    "num_block": 23,
                    "num_grow_ch": 32,
                    "scale": 4
                },
                "RealESRGAN_x2plus": {
                    "num_in_ch": 3,
                    "num_out_ch": 3,
                    "num_feat": 64,
                    "num_block": 23,
                    "num_grow_ch": 32,
                    "scale": 2
                }
            }
            
            if self.model_name not in model_configs:
                raise ValueError(f"Unsupported model: {self.model_name}")
            
            config = model_configs[self.model_name]
            
            # Create model
            model = self.RRDBNet(**config)
            
            # Initialize upsampler
            self.model = self.RealESRGANer(
                scale=config["scale"],
                model_path=self.model_path,
                model=model,
                tile=512,  # Tile size for large images
                tile_pad=10,
                pre_pad=0,
                half=True if self.device.type == 'cuda' else False,
                device=self.device
            )
            
            self.is_loaded = True
            logger.info(f"Real-ESRGAN model loaded: {self.model_name}")
            
        except Exception as e:
            logger.error(f"Failed to load Real-ESRGAN model: {e}")
            raise
    
    def upscale(
        self,
        image: Union[Image.Image, np.ndarray, torch.Tensor],
        scale_factor: Optional[int] = None
    ) -> Image.Image:
        """Upscale image using Real-ESRGAN."""
        
        if not self.is_loaded:
            self.load_model()
        
        # Preprocess image
        img_array = self.preprocess_image(image)
        
        try:
            # Enhance with Real-ESRGAN
            enhanced_img, _ = self.model.enhance(img_array, outscale=scale_factor or self.scale_factor)
            
            # Postprocess and return
            return self.postprocess_image(enhanced_img)
            
        except Exception as e:
            logger.error(f"Real-ESRGAN upscaling failed: {e}")
            raise


class GFPGANUpscaler(BaseUpscaler):
    """GFPGAN face restoration upscaler."""
    
    def __init__(
        self,
        device: torch.device,
        scale_factor: int = 2,
        model_path: Optional[str] = None
    ):
        """Initialize GFPGAN upscaler."""
        super().__init__(device, scale_factor, model_path)
        
        # Try to import GFPGAN
        try:
            from gfpgan import GFPGANer
            self.GFPGANer = GFPGANer
            self.available = True
        except ImportError:
            logger.warning("GFPGAN not available, install with: pip install gfpgan")
            self.available = False
        
        logger.info(f"GFPGAN upscaler initialized, scale: {scale_factor}")
    
    def load_model(self) -> None:
        """Load GFPGAN model."""
        if not self.available:
            raise RuntimeError("GFPGAN not available")
        
        if self.is_loaded:
            return
        
        try:
            self.model = self.GFPGANer(
                model_path=self.model_path or 'GFPGANv1.4.pth',
                upscale=self.scale_factor,
                arch='clean',
                channel_multiplier=2,
                bg_upsampler=None,
                device=self.device
            )
            
            self.is_loaded = True
            logger.info("GFPGAN model loaded")
            
        except Exception as e:
            logger.error(f"Failed to load GFPGAN model: {e}")
            raise
    
    def upscale(
        self,
        image: Union[Image.Image, np.ndarray, torch.Tensor],
        scale_factor: Optional[int] = None
    ) -> Image.Image:
        """Upscale image using GFPGAN."""
        
        if not self.is_loaded:
            self.load_model()
        
        # Preprocess image
        img_array = self.preprocess_image(image)
        
        try:
            # Enhance with GFPGAN
            _, _, enhanced_img = self.model.enhance(
                img_array,
                has_aligned=False,
                only_center_face=False,
                paste_back=True
            )
            
            # Postprocess and return
            return self.postprocess_image(enhanced_img)
            
        except Exception as e:
            logger.error(f"GFPGAN upscaling failed: {e}")
            raise


class BilinearUpscaler(BaseUpscaler):
    """Simple bilinear upscaling fallback."""
    
    def __init__(
        self,
        device: torch.device,
        scale_factor: int = 4
    ):
        """Initialize bilinear upscaler."""
        super().__init__(device, scale_factor)
        self.available = True
        logger.info(f"Bilinear upscaler initialized, scale: {scale_factor}")
    
    def load_model(self) -> None:
        """No model loading required for bilinear."""
        self.is_loaded = True
    
    def upscale(
        self,
        image: Union[Image.Image, np.ndarray, torch.Tensor],
        scale_factor: Optional[int] = None
    ) -> Image.Image:
        """Upscale image using bilinear interpolation."""
        
        scale = scale_factor or self.scale_factor
        
        if isinstance(image, Image.Image):
            # Simple PIL resize
            new_size = (image.width * scale, image.height * scale)
            return image.resize(new_size, Image.Resampling.BILINEAR)
        
        # Convert to PIL and resize
        img_array = self.preprocess_image(image)
        pil_image = self.postprocess_image(img_array)
        new_size = (pil_image.width * scale, pil_image.height * scale)
        
        return pil_image.resize(new_size, Image.Resampling.BILINEAR)


class UpscalerManager:
    """Manager for multiple upscaling methods."""
    
    def __init__(self, device: torch.device):
        """Initialize upscaler manager."""
        self.device = device
        self.upscalers: Dict[str, BaseUpscaler] = {}
        self.current_upscaler: Optional[str] = None
        
        # Register available upscalers
        self._register_upscalers()
        
        logger.info(f"Upscaler manager initialized with {len(self.upscalers)} methods")
    
    def _register_upscalers(self) -> None:
        """Register available upscaling methods."""
        
        # Real-ESRGAN variants
        try:
            self.upscalers["realesrgan_x4"] = RealESRGANUpscaler(
                self.device, scale_factor=4, model_name="RealESRGAN_x4plus"
            )
            self.upscalers["realesrgan_x2"] = RealESRGANUpscaler(
                self.device, scale_factor=2, model_name="RealESRGAN_x2plus"
            )
        except Exception as e:
            logger.debug(f"Real-ESRGAN not available: {e}")
        
        # GFPGAN
        try:
            self.upscalers["gfpgan"] = GFPGANUpscaler(self.device, scale_factor=2)
        except Exception as e:
            logger.debug(f"GFPGAN not available: {e}")
        
        # Bilinear fallback (always available)
        self.upscalers["bilinear"] = BilinearUpscaler(self.device, scale_factor=4)
        
        # Set default
        if "realesrgan_x4" in self.upscalers:
            self.current_upscaler = "realesrgan_x4"
        else:
            self.current_upscaler = "bilinear"
    
    def list_upscalers(self) -> List[str]:
        """List available upscaling methods."""
        return list(self.upscalers.keys())
    
    def set_upscaler(self, method: str) -> None:
        """Set current upscaling method."""
        if method not in self.upscalers:
            raise ValueError(f"Unknown upscaler: {method}. Available: {self.list_upscalers()}")
        
        # Unload current model
        if self.current_upscaler and self.current_upscaler in self.upscalers:
            self.upscalers[self.current_upscaler].unload_model()
        
        self.current_upscaler = method
        logger.info(f"Set upscaler to: {method}")
    
    def upscale(
        self,
        image: Union[Image.Image, np.ndarray, torch.Tensor],
        method: Optional[str] = None,
        scale_factor: Optional[int] = None
    ) -> Image.Image:
        """Upscale image using specified or current method."""
        
        upscaler_name = method or self.current_upscaler
        if not upscaler_name or upscaler_name not in self.upscalers:
            raise ValueError(f"No valid upscaler available")
        
        upscaler = self.upscalers[upscaler_name]
        
        try:
            result = upscaler.upscale(image, scale_factor)
            logger.debug(f"Upscaled image using {upscaler_name}")
            return result
        except Exception as e:
            logger.error(f"Upscaling failed with {upscaler_name}: {e}")
            # Fallback to bilinear
            if upscaler_name != "bilinear" and "bilinear" in self.upscalers:
                logger.info("Falling back to bilinear upscaling")
                return self.upscalers["bilinear"].upscale(image, scale_factor)
            raise
    
    def cleanup(self) -> None:
        """Cleanup all loaded models."""
        for upscaler in self.upscalers.values():
            upscaler.unload_model()
        logger.info("Cleaned up all upscaler models")


def create_upscaler_manager(device: torch.device) -> UpscalerManager:
    """Create an upscaler manager instance."""
    return UpscalerManager(device)
