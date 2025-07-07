"""
Image-to-image conditioning module for SDXL diffusion models.
Handles image conditioning, strength control, and noise injection.
"""

import logging
import torch
import numpy as np
from PIL import Image
from typing import Optional, Union, List, Tuple, Dict, Any
import torchvision.transforms as transforms

logger = logging.getLogger(__name__)


class Img2ImgProcessor:
    """Image-to-image conditioning processor."""
    
    def __init__(
        self,
        device: torch.device,
        dtype: torch.dtype = torch.float16
    ):
        """Initialize img2img processor."""
        self.device = device
        self.dtype = dtype
        
        # Standard SDXL transforms
        self.transform = transforms.Compose([
            transforms.Resize((1024, 1024), interpolation=transforms.InterpolationMode.LANCZOS),
            transforms.ToTensor(),
            transforms.Normalize([0.5], [0.5])  # Normalize to [-1, 1]
        ])
        
        logger.info("Img2Img processor initialized")
    
    def preprocess_image(
        self,
        image: Union[str, Image.Image, torch.Tensor, np.ndarray],
        target_size: Tuple[int, int] = (1024, 1024)
    ) -> torch.Tensor:
        """Preprocess input image for conditioning."""
        
        # Convert to PIL Image if needed
        if isinstance(image, str):
            pil_image = Image.open(image).convert("RGB")
        elif isinstance(image, np.ndarray):
            pil_image = Image.fromarray(image).convert("RGB")
        elif isinstance(image, torch.Tensor):
            # Convert tensor to PIL
            if image.dim() == 4:
                image = image.squeeze(0)
            if image.dim() == 3 and image.shape[0] == 3:
                # CHW format
                image = image.permute(1, 2, 0)
            # Denormalize if in [-1, 1] range
            if image.min() < 0:
                image = (image + 1) / 2
            image = (image * 255).clamp(0, 255).to(torch.uint8)
            pil_image = Image.fromarray(image.cpu().numpy()).convert("RGB")
        elif isinstance(image, Image.Image):
            pil_image = image.convert("RGB")
        else:
            raise ValueError(f"Unsupported image type: {type(image)}")
        
        # Resize to target size
        pil_image = pil_image.resize(target_size, Image.Resampling.LANCZOS)
        
        # Convert to tensor
        try:
            image_tensor = self.transform(pil_image)
            if not isinstance(image_tensor, torch.Tensor):
                # Fallback tensor conversion
                image_tensor = transforms.ToTensor()(pil_image)
                image_tensor = transforms.Normalize([0.5], [0.5])(image_tensor)
            image_tensor = image_tensor.unsqueeze(0).to(self.device, dtype=self.dtype)
        except Exception as e:
            logger.warning(f"Transform failed, using manual conversion: {e}")
            # Manual conversion
            img_array = np.array(pil_image, dtype=np.float32) / 255.0
            img_array = (img_array - 0.5) / 0.5  # Normalize to [-1, 1]
            image_tensor = torch.from_numpy(img_array).permute(2, 0, 1)
            image_tensor = image_tensor.unsqueeze(0).to(self.device, dtype=self.dtype)
        
        logger.debug(f"Preprocessed image to shape {image_tensor.shape}")
        return image_tensor
    
    def encode_image(
        self,
        image: torch.Tensor,
        vae_encoder,
        strength: float = 0.75
    ) -> Tuple[torch.Tensor, int]:
        """Encode image to latent space with noise scheduling."""
        
        # Encode to latents
        with torch.no_grad():
            latents = vae_encoder.encode(image).latent_dist.sample()
            latents = latents * vae_encoder.config.scaling_factor
        
        # Calculate timestep based on strength
        # strength=1.0 means full noise (like text2img)
        # strength=0.0 means no noise (return original image)
        init_timestep = min(int(1000 * strength), 999)
        
        logger.debug(f"Encoded image to latents with strength {strength}, timestep {init_timestep}")
        return latents, init_timestep
    
    def add_noise(
        self,
        latents: torch.Tensor,
        noise: torch.Tensor,
        timestep: int,
        scheduler
    ) -> torch.Tensor:
        """Add noise to latents according to scheduler."""
        
        # Get the appropriate timestep tensor
        timesteps = torch.tensor([timestep], device=latents.device, dtype=torch.long)
        
        # Add noise according to scheduler
        noisy_latents = scheduler.add_noise(latents, noise, timesteps)
        
        logger.debug(f"Added noise at timestep {timestep}")
        return noisy_latents
    
    def prepare_img2img_latents(
        self,
        image: Union[str, Image.Image, torch.Tensor, np.ndarray],
        vae_encoder,
        scheduler,
        strength: float = 0.75,
        generator: Optional[torch.Generator] = None,
        batch_size: int = 1
    ) -> Tuple[torch.Tensor, int]:
        """Prepare latents for img2img generation."""
        
        # Preprocess image
        processed_image = self.preprocess_image(image)
        
        # Repeat for batch size
        if batch_size > 1:
            processed_image = processed_image.repeat(batch_size, 1, 1, 1)
        
        # Encode to latents
        init_latents, init_timestep = self.encode_image(
            processed_image, vae_encoder, strength
        )
        
        # Generate noise
        noise = torch.randn(
            init_latents.shape,
            generator=generator,
            device=init_latents.device,
            dtype=init_latents.dtype
        )
        
        # Add noise according to strength
        latents = self.add_noise(init_latents, noise, init_timestep, scheduler)
        
        return latents, init_timestep
    
    def get_img2img_steps(
        self,
        total_steps: int,
        strength: float
    ) -> int:
        """Calculate the number of denoising steps for img2img."""
        
        # The number of steps is reduced based on strength
        # strength=1.0 uses all steps, strength=0.5 uses half steps
        img2img_steps = int(total_steps * strength)
        
        logger.debug(f"Img2img steps: {img2img_steps} / {total_steps} (strength: {strength})")
        return max(1, img2img_steps)


class InpaintingProcessor:
    """Inpainting conditioning processor."""
    
    def __init__(
        self,
        device: torch.device,
        dtype: torch.dtype = torch.float16
    ):
        """Initialize inpainting processor."""
        self.device = device
        self.dtype = dtype
        
        logger.info("Inpainting processor initialized")
    
    def preprocess_mask(
        self,
        mask: Union[str, Image.Image, torch.Tensor, np.ndarray],
        target_size: Tuple[int, int] = (1024, 1024),
        blur_factor: int = 0
    ) -> torch.Tensor:
        """Preprocess mask for inpainting."""
        
        # Convert to PIL Image if needed
        if isinstance(mask, str):
            pil_mask = Image.open(mask).convert("L")
        elif isinstance(mask, np.ndarray):
            if mask.ndim == 3:
                mask = mask[:, :, 0]  # Take first channel
            pil_mask = Image.fromarray(mask).convert("L")
        elif isinstance(mask, torch.Tensor):
            if mask.dim() == 4:
                mask = mask.squeeze(0)
            if mask.dim() == 3:
                mask = mask[0]  # Take first channel
            mask = (mask * 255).clamp(0, 255).to(torch.uint8)
            pil_mask = Image.fromarray(mask.cpu().numpy()).convert("L")
        elif isinstance(mask, Image.Image):
            pil_mask = mask.convert("L")
        else:
            raise ValueError(f"Unsupported mask type: {type(mask)}")
        
        # Apply blur if specified
        if blur_factor > 0:
            from PIL import ImageFilter
            pil_mask = pil_mask.filter(ImageFilter.GaussianBlur(radius=blur_factor))
        
        # Resize to target size
        pil_mask = pil_mask.resize(target_size, Image.Resampling.LANCZOS)
        
        # Convert to tensor [0, 1]
        mask_tensor = transforms.ToTensor()(pil_mask)
        mask_tensor = mask_tensor.unsqueeze(0).to(self.device, dtype=self.dtype)
        
        logger.debug(f"Preprocessed mask to shape {mask_tensor.shape}")
        return mask_tensor
    
    def prepare_inpainting_latents(
        self,
        image: Union[str, Image.Image, torch.Tensor, np.ndarray],
        mask: Union[str, Image.Image, torch.Tensor, np.ndarray],
        vae_encoder,
        scheduler,
        generator: Optional[torch.Generator] = None,
        batch_size: int = 1,
        mask_blur: int = 0
    ) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        """Prepare latents for inpainting."""
        
        # Preprocess image and mask
        img_processor = Img2ImgProcessor(self.device, self.dtype)
        processed_image = img_processor.preprocess_image(image)
        processed_mask = self.preprocess_mask(mask, blur_factor=mask_blur)
        
        # Repeat for batch size
        if batch_size > 1:
            processed_image = processed_image.repeat(batch_size, 1, 1, 1)
            processed_mask = processed_mask.repeat(batch_size, 1, 1, 1)
        
        # Encode image to latents
        with torch.no_grad():
            init_latents = vae_encoder.encode(processed_image).latent_dist.sample()
            init_latents = init_latents * vae_encoder.config.scaling_factor
        
        # Resize mask to latent dimensions
        latent_height = init_latents.shape[2]
        latent_width = init_latents.shape[3]
        mask_latents = torch.nn.functional.interpolate(
            processed_mask,
            size=(latent_height, latent_width),
            mode="nearest"
        )
        
        # Generate noise for masked regions
        noise = torch.randn(
            init_latents.shape,
            generator=generator,
            device=init_latents.device,
            dtype=init_latents.dtype
        )
        
        # Apply mask to latents (masked regions will be generated)
        masked_latents = init_latents * (1 - mask_latents) + noise * mask_latents
        
        logger.debug(f"Prepared inpainting latents: image {init_latents.shape}, mask {mask_latents.shape}")
        return masked_latents, mask_latents, init_latents


def create_img2img_processor(
    device: torch.device,
    dtype: torch.dtype = torch.float16
) -> Img2ImgProcessor:
    """Create an img2img processor."""
    return Img2ImgProcessor(device, dtype)


def create_inpainting_processor(
    device: torch.device,
    dtype: torch.dtype = torch.float16
) -> InpaintingProcessor:
    """Create an inpainting processor."""
    return InpaintingProcessor(device, dtype)
