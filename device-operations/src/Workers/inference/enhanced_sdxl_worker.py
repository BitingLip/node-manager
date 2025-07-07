"""
Enhanced SDXL Worker - Phase 2 Implementation
Provides advanced SDXL generation capabilities with LoRA, ControlNet, and Refiner support.

This worker extends the base functionality to support:
- Multiple model management (Base, Refiner, VAE)
- Advanced schedulers
- LoRA adapter integration
- ControlNet conditioning
- Batch generation
- Memory optimization for DirectML
"""

import asyncio
import logging
import torch
from typing import Dict, List, Optional, Any, Union
from pathlib import Path
import json
from diffusers import (
    StableDiffusionXLPipeline,
    StableDiffusionXLImg2ImgPipeline,
    DiffusionPipeline,
    AutoencoderKL
)

# Import base worker functionality
from ..legacy.base_worker import BaseWorker, WorkerRequest, WorkerResponse, ProcessingError
from ..core.enhanced_orchestrator import EnhancedRequest

# Import feature managers
from ..features.scheduler_manager import SchedulerManager
from ..features.batch_manager import EnhancedBatchManager, BatchConfiguration
# Import LoRA Worker from the correct path
import sys
import os
sys.path.append(os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(__file__))), 'workers', 'features'))
from lora_worker import LoRAWorker
# Import ControlNet Worker - Phase 2 Days 19-20 Implementation
from controlnet_worker import ControlNetWorker
# Import VAE Manager - Phase 2 Days 21-22 Implementation
from vae_manager import VAEManager
# Import SDXL Refiner Pipeline - Phase 3 Days 29-30 Implementation
from sdxl_refiner_pipeline import SDXLRefinerPipeline, RefinerConfiguration

# Set up logging
logger = logging.getLogger(__name__)

class EnhancedSDXLWorker(BaseWorker):
    """
    Enhanced SDXL Worker with advanced features support.
    
    Supports:
    - Base + Refiner + Custom VAE pipelines
    - Multiple scheduler types
    - LoRA adapter loading and management
    - ControlNet conditioning
    - Batch generation with memory optimization
    - DirectML device integration
    """
    
    def __init__(self, config: Optional[Dict] = None):
        """Initialize the Enhanced SDXL Worker."""
        super().__init__(config)
        
        # Worker configuration
        self.config = config or {}
        self.worker_type = "enhanced_sdxl"
        
        # Pipeline management
        self.base_pipeline: Optional[StableDiffusionXLPipeline] = None
        self.refiner_pipeline: Optional[StableDiffusionXLImg2ImgPipeline] = None
        self.current_base_model: Optional[str] = None
        self.current_refiner_model: Optional[str] = None
        self.current_vae_model: Optional[str] = None
        
        # Feature managers
        self.scheduler_manager = SchedulerManager()
        self.batch_manager = EnhancedBatchManager(self.device)
        # Initialize LoRA Worker
        self.lora_worker = LoRAWorker(self.config.get("lora_config", {}))
        # Initialize ControlNet Worker - Phase 2 Days 19-20 Implementation
        self.controlnet_worker = ControlNetWorker(self.config.get("controlnet_config", {}))
        # Initialize VAE Manager - Phase 2 Days 21-22 Implementation
        self.vae_manager = VAEManager(self.config.get("vae_config", {}))
        # Initialize SDXL Refiner Pipeline - Phase 3 Days 29-30 Implementation
        refiner_config = self.config.get("refiner_config", {})
        refiner_pipeline_config = RefinerConfiguration(
            model_path=refiner_config.get("model_path", "stabilityai/stable-diffusion-xl-refiner-1.0"),
            strength=refiner_config.get("strength", 0.3),
            num_inference_steps=refiner_config.get("num_inference_steps", 10),
            guidance_scale=refiner_config.get("guidance_scale", 7.5),
            aesthetic_score=refiner_config.get("aesthetic_score", 6.0)
        )
        self.refiner_pipeline_manager = SDXLRefinerPipeline(config=refiner_pipeline_config)
        
        # Memory management
        self.memory_optimization = self.config.get("memory_optimization", True)
        self.attention_slicing = self.config.get("attention_slicing", True)
        self.vae_slicing = self.config.get("vae_slicing", True)
        
        # Device configuration
        self.device = self._get_device()
        self.torch_dtype = self._get_torch_dtype()
        
        # Batch configuration
        self.max_batch_size = self.config.get("max_batch_size", 4)
        self.dynamic_batching = self.config.get("dynamic_batching", True)
        
        logger.info(f"Enhanced SDXL Worker initialized - Device: {self.device}, Memory Optimization: {self.memory_optimization}")
    
    def _get_device(self) -> str:
        """Determine the optimal device for processing."""
        if self.config.get("force_cpu", False):
            return "cpu"
        
        # Check for DirectML support
        try:
            import torch_directml
            if torch_directml.is_available():
                device_count = torch_directml.device_count()
                if device_count > 0:
                    logger.info(f"DirectML detected with {device_count} device(s)")
                    return torch_directml.device()
        except ImportError:
            pass
        
        # Check for CUDA
        if torch.cuda.is_available():
            return "cuda"
        
        # Fallback to CPU
        logger.warning("No GPU acceleration available, using CPU")
        return "cpu"
    
    def _get_torch_dtype(self) -> torch.dtype:
        """Determine the optimal torch dtype."""
        if self.device == "cpu":
            return torch.float32
        elif "directml" in str(self.device):
            # DirectML typically works best with float16
            return torch.float16
        else:
            # CUDA - use float16 for memory efficiency
            return torch.float16
    
    async def process_request(self, request: WorkerRequest) -> WorkerResponse:
        """
        Main entry point for processing enhanced SDXL requests.
        
        Args:
            request: WorkerRequest containing enhanced SDXL generation parameters
            
        Returns:
            WorkerResponse containing generated images and metadata
        """
        try:
            logger.info(f"Processing enhanced SDXL request - Session: {request.session_id}")
            
            # 1. Validate and parse enhanced request
            enhanced_request = await self._validate_enhanced_request(request.data)
            
            # 2. Setup models (base, refiner, VAE)
            await self._setup_models(enhanced_request)
            
            # 3. Configure features (LoRA, ControlNet, Scheduler)
            await self._configure_features(enhanced_request)
            
            # 4. Generate base images
            base_images = await self._generate_base_images(enhanced_request)
            
            # 5. Apply enhancements (refiner, post-processing)
            enhanced_images = await self._apply_enhancements(base_images, enhanced_request)
            
            # 6. Save results and create response
            response = await self._create_response(enhanced_images, enhanced_request, request.session_id)
            
            logger.info(f"Enhanced SDXL generation completed - {len(enhanced_images)} images generated")
            return response
            
        except Exception as e:
            logger.error(f"Enhanced SDXL generation failed: {str(e)}", exc_info=True)
            return self._create_error_response(str(e), request.session_id)
    
    async def _validate_enhanced_request(self, data: Dict) -> EnhancedRequest:
        """
        Validate and parse the enhanced request data.
        
        Args:
            data: Raw request data dictionary
            
        Returns:
            EnhancedRequest object with validated parameters
            
        Raises:
            ProcessingError: If validation fails
        """
        try:
            # Parse the enhanced request format
            enhanced_request = EnhancedRequest.from_dict(data)
            
            # Validate required fields
            if not enhanced_request.prompt:
                raise ProcessingError("Prompt is required for enhanced SDXL generation")
            
            # Validate model configuration
            if not enhanced_request.model or not enhanced_request.model.base:
                raise ProcessingError("Base model is required for enhanced SDXL generation")
            
            # Validate generation parameters
            if enhanced_request.width <= 0 or enhanced_request.height <= 0:
                raise ProcessingError("Width and height must be positive integers")
            
            if enhanced_request.num_inference_steps <= 0:
                raise ProcessingError("Number of inference steps must be positive")
            
            # Validate batch configuration
            if enhanced_request.batch_size > self.max_batch_size:
                logger.warning(f"Requested batch size {enhanced_request.batch_size} exceeds maximum {self.max_batch_size}, clamping")
                enhanced_request.batch_size = self.max_batch_size
            
            logger.info(f"Enhanced request validated - Prompt: '{enhanced_request.prompt[:50]}...', Model: {enhanced_request.model.base}")
            return enhanced_request
            
        except Exception as e:
            raise ProcessingError(f"Request validation failed: {str(e)}")
    
    async def _setup_models(self, request: EnhancedRequest) -> None:
        """
        Setup and load the required models (base, refiner, VAE).
        
        Args:
            request: Enhanced request with model configuration
        """
        try:
            # Check if we need to load a new base model
            if self.current_base_model != request.model.base or self.base_pipeline is None:
                logger.info(f"Loading base model: {request.model.base}")
                await self._load_base_model(request.model.base)
                self.current_base_model = request.model.base
            
            # Setup custom VAE if specified
            if request.model.vae and request.model.vae != self.current_vae_model:
                logger.info(f"Loading custom VAE: {request.model.vae}")
                await self._load_custom_vae(request.model.vae)
                self.current_vae_model = request.model.vae
            
            # Setup refiner if specified
            if request.model.refiner and request.model.refiner != self.current_refiner_model:
                logger.info(f"Loading refiner model: {request.model.refiner}")
                await self._load_refiner_model(request.model.refiner)
                self.current_refiner_model = request.model.refiner
            
            # Apply memory optimizations
            await self._apply_memory_optimizations()
            
        except Exception as e:
            raise ProcessingError(f"Model setup failed: {str(e)}")
    
    async def _load_base_model(self, model_path: str) -> None:
        """Load the base SDXL model."""
        try:
            # Resolve model path
            resolved_path = self._resolve_model_path(model_path)
            
            # Load pipeline
            self.base_pipeline = StableDiffusionXLPipeline.from_pretrained(
                resolved_path,
                torch_dtype=self.torch_dtype,
                variant="fp16" if self.torch_dtype == torch.float16 else None,
                use_safetensors=True
            )
            
            # Move to device
            self.base_pipeline = self.base_pipeline.to(self.device)
            
            logger.info(f"Base model loaded successfully: {model_path}")
            
        except Exception as e:
            raise ProcessingError(f"Failed to load base model '{model_path}': {str(e)}")
    
    async def _load_refiner_model(self, model_path: str) -> None:
        """Load the SDXL refiner model."""
        try:
            # Resolve model path
            resolved_path = self._resolve_model_path(model_path)
            
            # Load refiner pipeline
            self.refiner_pipeline = StableDiffusionXLImg2ImgPipeline.from_pretrained(
                resolved_path,
                torch_dtype=self.torch_dtype,
                variant="fp16" if self.torch_dtype == torch.float16 else None,
                use_safetensors=True
            )
            
            # Move to device
            self.refiner_pipeline = self.refiner_pipeline.to(self.device)
            
            logger.info(f"Refiner model loaded successfully: {model_path}")
            
        except Exception as e:
            raise ProcessingError(f"Failed to load refiner model '{model_path}': {str(e)}")
    
    async def _load_custom_vae(self, vae_path: str) -> None:
        """Load a custom VAE model using VAE Manager."""
        try:
            # Initialize VAE Manager if not already done
            if not self.vae_manager.is_initialized:
                success = await self.vae_manager.initialize()
                if not success:
                    logger.error("Failed to initialize VAE Manager")
                    return
            
            # Create VAE configuration
            from vae_manager import VAEConfiguration
            vae_config = VAEConfiguration(
                name=f"custom_{Path(vae_path).stem}",
                model_path=vae_path,
                model_type="custom",
                enable_slicing=self.vae_slicing,
                enable_tiling=True
            )
            
            # Load VAE using VAE Manager
            success = await self.vae_manager.load_vae_model(vae_config)
            if not success:
                raise ProcessingError(f"Failed to load VAE: {vae_path}")
            
            # Get the loaded VAE model
            custom_vae = self.vae_manager.get_vae_model(vae_config.name)
            if not custom_vae:
                raise ProcessingError(f"VAE model not found after loading: {vae_config.name}")
            
            # Apply to base pipeline
            if self.base_pipeline:
                self.base_pipeline.vae = custom_vae
                logger.info(f"Custom VAE applied to base pipeline: {vae_path}")
            
            # Apply to refiner pipeline if loaded
            if self.refiner_pipeline:
                self.refiner_pipeline.vae = custom_vae
                logger.info(f"Custom VAE applied to refiner pipeline: {vae_path}")
            
            logger.info(f"✅ Custom VAE loaded successfully: {vae_path}")
            
        except Exception as e:
            raise ProcessingError(f"Failed to load custom VAE '{vae_path}': {str(e)}")
    
    async def _configure_automatic_vae(self, pipeline_type: str = "base") -> bool:
        """Configure automatic VAE selection for the pipeline."""
        try:
            # Initialize VAE Manager if not already done
            if not self.vae_manager.is_initialized:
                success = await self.vae_manager.initialize()
                if not success:
                    logger.warning("Failed to initialize VAE Manager for automatic VAE")
                    return False
            
            # Select optimal VAE for pipeline type
            optimal_vae = self.vae_manager.select_vae_for_pipeline(pipeline_type)
            if not optimal_vae:
                logger.warning(f"No suitable VAE found for {pipeline_type} pipeline")
                return False
            
            # Apply to appropriate pipeline
            if pipeline_type == "base" and self.base_pipeline:
                self.base_pipeline.vae = optimal_vae
                logger.info(f"Automatic VAE applied to base pipeline")
            elif pipeline_type == "refiner" and self.refiner_pipeline:
                self.refiner_pipeline.vae = optimal_vae
                logger.info(f"Automatic VAE applied to refiner pipeline")
            
            return True
            
        except Exception as e:
            logger.error(f"Failed to configure automatic VAE: {e}")
            return False
    
    async def _apply_memory_optimizations(self) -> None:
        """Apply memory optimization settings."""
        if not self.memory_optimization:
            return
        
        try:
            # Apply attention slicing
            if self.attention_slicing and self.base_pipeline:
                self.base_pipeline.enable_attention_slicing()
                if self.refiner_pipeline:
                    self.refiner_pipeline.enable_attention_slicing()
                logger.debug("Attention slicing enabled")
            
            # Apply VAE slicing
            if self.vae_slicing and self.base_pipeline:
                self.base_pipeline.enable_vae_slicing()
                if self.refiner_pipeline:
                    self.refiner_pipeline.enable_vae_slicing()
                logger.debug("VAE slicing enabled")
            
            # Enable memory efficient attention for DirectML
            if "directml" in str(self.device) and self.base_pipeline:
                self.base_pipeline.enable_xformers_memory_efficient_attention()
                if self.refiner_pipeline:
                    self.refiner_pipeline.enable_xformers_memory_efficient_attention()
                logger.debug("Memory efficient attention enabled for DirectML")
            
        except Exception as e:
            logger.warning(f"Some memory optimizations failed: {str(e)}")
    
    async def _configure_features(self, request: EnhancedRequest) -> None:
        """Configure advanced features (LoRA, ControlNet, Scheduler)."""
        try:
            # Configure scheduler
            if request.scheduler and request.scheduler != "default":
                scheduler = await self.scheduler_manager.get_scheduler(
                    request.scheduler, 
                    self.base_pipeline.scheduler.config
                )
                self.base_pipeline.scheduler = scheduler
                logger.info(f"Scheduler configured: {request.scheduler}")
            
            # Configure LoRA adapters
            await self._configure_lora_adapters(request)
            
            # Configure ControlNet - Phase 2 Days 19-20 Implementation
            if hasattr(request, 'controlnet') and request.controlnet:
                success = await self._configure_controlnet_adapters(request.controlnet)
                if success:
                    logger.info("ControlNet adapters configured successfully")
                else:
                    logger.warning("Failed to configure ControlNet adapters")
            
        except Exception as e:
            raise ProcessingError(f"Feature configuration failed: {str(e)}")
    
    async def _configure_lora_adapters(self, request: EnhancedRequest) -> None:
        """Configure LoRA adapters for the current request."""
        try:
            # Check if LoRA configuration is provided
            lora_config = getattr(request, 'lora', None)
            if not lora_config:
                return
            
            # Handle different LoRA configuration formats
            if isinstance(lora_config, dict):
                enabled = lora_config.get('enabled', False)
                models = lora_config.get('models', [])
                global_weight = lora_config.get('global_weight', 1.0)
            else:
                # Handle object-style configuration
                enabled = getattr(lora_config, 'enabled', False)
                models = getattr(lora_config, 'models', [])
                global_weight = getattr(lora_config, 'global_weight', 1.0)
            
            if not enabled or not models:
                return
            
            logger.info(f"Configuring LoRA adapters: {len(models)} adapters")
            
            # Load and apply LoRA adapters
            adapter_names = []
            for model_config in models:
                # Handle different model configuration formats
                if isinstance(model_config, dict):
                    name = model_config.get('name')
                    path = model_config.get('path')
                    weight = model_config.get('weight', 1.0)
                elif isinstance(model_config, str):
                    # Simple string format - use as name and auto-discover path
                    name = model_config
                    path = None
                    weight = 1.0
                else:
                    # Object format
                    name = getattr(model_config, 'name', None)
                    path = getattr(model_config, 'path', None)
                    weight = getattr(model_config, 'weight', 1.0)
                
                if not name:
                    logger.warning(f"Skipping LoRA adapter with no name: {model_config}")
                    continue
                
                # Create LoRA configuration
                from lora_worker import LoRAConfiguration
                lora_adapter_config = LoRAConfiguration(
                    name=name,
                    path=path or name,  # Use name as fallback path for auto-discovery
                    weight=weight * global_weight
                )
                
                # Load the adapter
                success = await self.lora_worker.load_lora_adapter(lora_adapter_config)
                if success:
                    adapter_names.append(name)
                    logger.info(f"LoRA adapter loaded: {name} (weight: {weight * global_weight:.2f})")
                else:
                    logger.warning(f"Failed to load LoRA adapter: {name}")
            
            # Apply loaded adapters to pipeline
            if adapter_names and self.base_pipeline:
                success = await self.lora_worker.apply_to_pipeline(self.base_pipeline, adapter_names)
                if success:
                    logger.info(f"Successfully applied {len(adapter_names)} LoRA adapters to pipeline")
                else:
                    logger.error("Failed to apply LoRA adapters to pipeline")
            
        except Exception as e:
            logger.error(f"LoRA configuration failed: {str(e)}")
            raise ProcessingError(f"LoRA configuration failed: {str(e)}")
    
    async def _generate_base_images(self, request: EnhancedRequest) -> List[Any]:
        """Generate base images using enhanced batch generation with memory optimization."""
        try:
            # Get batch size (use getattr for compatibility)
            batch_size = getattr(request, 'batch_size', 1)
            
            # Prepare base generation parameters
            base_generation_params = {
                "prompt": getattr(request, 'prompt', ''),
                "negative_prompt": getattr(request, 'negative_prompt', ''),
                "width": getattr(request, 'width', 1024),
                "height": getattr(request, 'height', 1024),
                "num_inference_steps": getattr(request, 'num_inference_steps', 20),
                "guidance_scale": getattr(request, 'guidance_scale', 7.5),
                "generator": torch.Generator(device=self.device).manual_seed(int(getattr(request, 'seed', 42))) if getattr(request, 'seed', None) is not None else None
            }
            
            # Add scheduler-specific parameters if configured
            scheduler_config = getattr(request, 'scheduler_config', None)
            if scheduler_config:
                base_generation_params.update(scheduler_config)
            
            # Configure batch generation
            batch_config = BatchConfiguration(
                total_images=batch_size,
                preferred_batch_size=min(batch_size, self.max_batch_size),
                max_batch_size=self.max_batch_size,
                min_batch_size=1,
                enable_dynamic_sizing=self.dynamic_batching,
                memory_threshold=0.85,  # 85% VRAM threshold
                progress_callback=self._batch_progress_callback if hasattr(self, '_batch_progress_callback') else None,
                parallel_processing=False  # Keep sequential for stability
            )
            
            logger.info(f"Starting enhanced batch generation: {batch_size} images")
            
            # Create generation function wrapper
            async def generation_function(**params):
                """Wrapper function for batch manager."""
                if not self.base_pipeline:
                    raise ProcessingError("Base pipeline not loaded")
                
                # Remove num_images_per_prompt from base params and use the batch manager's value
                generation_params = base_generation_params.copy()
                generation_params["num_images_per_prompt"] = params.get("num_images_per_prompt", 1)
                
                # Generate using the pipeline
                with torch.inference_mode():
                    result = self.base_pipeline(**generation_params)
                
                return result
            
            # Use enhanced batch manager for generation
            all_images, batch_metrics = await self.batch_manager.process_batch_generation(
                generation_function=generation_function,
                batch_config=batch_config,
                generation_params=base_generation_params
            )
            
            logger.info(f"Enhanced batch generation completed: {len(all_images)} images in {batch_metrics.get('total_time', 0):.1f}s")
            
            # Log detailed metrics
            if batch_metrics:
                logger.info(f"Batch metrics: {batch_metrics.get('total_batches', 0)} batches, "
                          f"avg time: {batch_metrics.get('average_batch_time', 0):.1f}s, "
                          f"peak memory: {batch_metrics.get('peak_memory_usage', 0):.1%}")
            
            return all_images
            
        except Exception as e:
            raise ProcessingError(f"Enhanced batch generation failed: {str(e)}")
    
    async def _apply_enhancements(self, base_images: List[Any], request: EnhancedRequest) -> List[Any]:
        """Apply enhancements like refiner and post-processing."""
        enhanced_images = base_images
        
        try:
            # Apply refiner if configured
            if self.refiner_pipeline and request.model.refiner:
                logger.info("Applying SDXL refiner...")
                enhanced_images = await self._apply_refiner(enhanced_images, request)
            
            # Apply post-processing steps
            if hasattr(request, 'post_processing') and request.post_processing:
                logger.info("Applying post-processing...")
                enhanced_images = await self._apply_post_processing(enhanced_images, request.post_processing)
            
            return enhanced_images
            
        except Exception as e:
            logger.error(f"Enhancement failed: {str(e)}")
            # Return base images if enhancement fails
            return base_images
    
    async def _apply_refiner(self, images: List[Any], request: EnhancedRequest) -> List[Any]:
        """Apply SDXL refiner to images."""
        if not self.refiner_pipeline:
            return images
        
        try:
            refined_images = []
            refiner_strength = getattr(request, 'refiner_strength', 0.3)
            refiner_steps = getattr(request, 'refiner_steps', 10)
            
            for image in images:
                with torch.inference_mode():
                    refined_result = self.refiner_pipeline(
                        prompt=request.prompt,
                        negative_prompt=request.negative_prompt,
                        image=image,
                        strength=refiner_strength,
                        num_inference_steps=refiner_steps,
                        guidance_scale=request.guidance_scale,
                        generator=torch.Generator(device=self.device).manual_seed(request.seed) if request.seed else None
                    )
                
                refined_images.append(refined_result.images[0])
            
            logger.info(f"Refiner applied to {len(refined_images)} images")
            return refined_images
            
        except Exception as e:
            logger.error(f"Refiner application failed: {str(e)}")
            return images
    
    async def _apply_post_processing(self, images: List[Any], post_processing_steps: List[Dict]) -> List[Any]:
        """Apply post-processing steps to images."""
        processed_images = images
        
        for step in post_processing_steps:
            step_type = step.get("type", "").lower()
            
            if step_type == "upscale":
                # Import and apply upscaling (to be implemented)
                from ..features.upscaler_worker import UpscalerWorker
                upscaler = UpscalerWorker()
                processed_images = await upscaler.upscale_images(processed_images, step)
            
            # Add other post-processing types as needed
        
        return processed_images
    
    async def _create_response(self, images: List[Any], request: EnhancedRequest, session_id: str) -> WorkerResponse:
        """Create the response with generated images and metadata."""
        try:
            # Save images and get file paths
            image_paths = []
            image_data = []
            
            for i, image in enumerate(images):
                # Save image to file
                filename = f"enhanced_sdxl_{session_id}_{i+1}.png"
                filepath = self._save_image(image, filename)
                image_paths.append(str(filepath))
                
                # Convert to base64 if requested
                if getattr(request, 'return_base64', False):
                    import base64
                    from io import BytesIO
                    buffer = BytesIO()
                    image.save(buffer, format="PNG")
                    image_base64 = base64.b64encode(buffer.getvalue()).decode()
                    image_data.append(image_base64)
            
            # Create response data
            response_data = {
                "success": True,
                "images": image_paths,
                "image_data": image_data if image_data else None,
                "model_info": {
                    "base_model": self.current_base_model,
                    "refiner_model": self.current_refiner_model,
                    "vae_model": self.current_vae_model
                },
                "generation_params": {
                    "prompt": request.prompt,
                    "width": request.width,
                    "height": request.height,
                    "steps": request.num_inference_steps,
                    "guidance_scale": request.guidance_scale,
                    "batch_size": request.batch_size,
                    "seed": request.seed
                },
                "processing_time": 0  # To be calculated
            }
            
            return WorkerResponse(
                success=True,
                data=response_data,
                session_id=session_id
            )
            
        except Exception as e:
            raise ProcessingError(f"Response creation failed: {str(e)}")
    
    def _create_error_response(self, error_message: str, session_id: str) -> WorkerResponse:
        """Create an error response."""
        return WorkerResponse(
            success=False,
            error=error_message,
            session_id=session_id,
            data={"success": False, "error": error_message}
        )
    
    def _resolve_model_path(self, model_identifier: str) -> str:
        """Resolve model identifier to actual path."""
        # Check if it's already a full path
        if Path(model_identifier).exists():
            return model_identifier
        
        # Check in models directory
        models_dir = Path(self.config.get("models_dir", "models"))
        local_path = models_dir / model_identifier
        if local_path.exists():
            return str(local_path)
        
        # Assume it's a Hugging Face model ID
        return model_identifier
    
    def _save_image(self, image: Any, filename: str) -> Path:
        """Save image to the outputs directory."""
        outputs_dir = Path(self.config.get("outputs_dir", "outputs"))
        outputs_dir.mkdir(exist_ok=True)
        
        filepath = outputs_dir / filename
        image.save(filepath)
        
        return filepath
    
    def _batch_progress_callback(self, progress_info: Dict[str, Any]) -> None:
        """
        Callback function for batch generation progress updates.
        
        Args:
            progress_info: Dictionary containing progress information
        """
        try:
            completed = progress_info.get("completed_images", 0)
            total = progress_info.get("total_images", 0)
            progress_ratio = progress_info.get("progress_ratio", 0)
            batch_num = progress_info.get("batch_number", 0)
            total_batches = progress_info.get("total_batches", 0)
            elapsed_time = progress_info.get("elapsed_time", 0)
            estimated_remaining = progress_info.get("estimated_remaining", 0)
            memory_usage = progress_info.get("current_memory_usage", 0)
            
            # Log progress
            logger.info(f"Batch progress: {completed}/{total} images ({progress_ratio:.1%}) - "
                       f"Batch {batch_num}/{total_batches} - "
                       f"Elapsed: {elapsed_time:.1f}s, ETA: {estimated_remaining:.1f}s - "
                       f"Memory: {memory_usage:.1%}")
            
            # TODO: Send progress updates to C# service via WebSocket or callback
            # This would allow real-time progress tracking in the UI
            
        except Exception as e:
            logger.warning(f"Progress callback failed: {e}")

    async def _configure_controlnet_adapters(self, controlnet_config: Dict[str, Any]) -> bool:
        """Configure ControlNet adapters for guided generation."""
        try:
            from controlnet_worker import ControlNetConfiguration, ControlNetStackConfiguration
            
            logger.info("Configuring ControlNet adapters")
            
            # Initialize ControlNet worker if not already done
            if not self.controlnet_worker.is_initialized:
                success = await self.controlnet_worker.initialize()
                if not success:
                    logger.error("Failed to initialize ControlNet worker")
                    return False
            
            # Handle different configuration formats
            if isinstance(controlnet_config, list):
                # Multiple ControlNet configurations
                stack_config = ControlNetStackConfiguration(max_adapters=len(controlnet_config))
                
                for i, config in enumerate(controlnet_config):
                    if isinstance(config, str):
                        # Simple string format: "canny:0.8" or just "canny"
                        parts = config.split(':')
                        controlnet_name = parts[0]
                        conditioning_scale = float(parts[1]) if len(parts) > 1 else 1.0
                        
                        adapter_config = ControlNetConfiguration(
                            name=f"controlnet_{i}",
                            type=controlnet_name,
                            model_path=f"diffusers/controlnet-{controlnet_name}-sdxl-1.0",
                            conditioning_scale=conditioning_scale
                        )
                    elif isinstance(config, dict):
                        # Object format with detailed configuration
                        adapter_config = ControlNetConfiguration(
                            name=config.get("name", f"controlnet_{i}"),
                            type=config.get("type", "canny"),
                            model_path=config.get("model_path", f"diffusers/controlnet-{config.get('type', 'canny')}-sdxl-1.0"),
                            condition_image=config.get("condition_image"),
                            conditioning_scale=config.get("conditioning_scale", 1.0),
                            control_guidance_start=config.get("control_guidance_start", 0.0),
                            control_guidance_end=config.get("control_guidance_end", 1.0),
                            enabled=config.get("enabled", True),
                            preprocess_condition=config.get("preprocess_condition", True)
                        )
                    else:
                        logger.warning(f"Unsupported ControlNet configuration format: {type(config)}")
                        continue
                    
                    success = stack_config.add_adapter(adapter_config)
                    if not success:
                        logger.warning(f"Failed to add ControlNet adapter: {adapter_config.name}")
                
                # Prepare the ControlNet stack
                success = await self.controlnet_worker.prepare_controlnet_stack(stack_config)
                if success:
                    logger.info(f"ControlNet stack configured with {len(stack_config.get_enabled_adapters())} adapters")
                    return True
                else:
                    logger.error("Failed to prepare ControlNet stack")
                    return False
                    
            elif isinstance(controlnet_config, dict):
                # Single ControlNet configuration
                adapter_config = ControlNetConfiguration(
                    name=controlnet_config.get("name", "controlnet_single"),
                    type=controlnet_config.get("type", "canny"),
                    model_path=controlnet_config.get("model_path", f"diffusers/controlnet-{controlnet_config.get('type', 'canny')}-sdxl-1.0"),
                    condition_image=controlnet_config.get("condition_image"),
                    conditioning_scale=controlnet_config.get("conditioning_scale", 1.0),
                    control_guidance_start=controlnet_config.get("control_guidance_start", 0.0),
                    control_guidance_end=controlnet_config.get("control_guidance_end", 1.0),
                    enabled=controlnet_config.get("enabled", True),
                    preprocess_condition=controlnet_config.get("preprocess_condition", True)
                )
                
                # Load single ControlNet
                success = await self.controlnet_worker.load_controlnet_model(adapter_config)
                if success:
                    logger.info(f"Single ControlNet configured: {adapter_config.name} ({adapter_config.type})")
                    return True
                else:
                    logger.error(f"Failed to load ControlNet: {adapter_config.name}")
                    return False
                    
            elif isinstance(controlnet_config, str):
                # Simple string format
                parts = controlnet_config.split(':')
                controlnet_type = parts[0]
                conditioning_scale = float(parts[1]) if len(parts) > 1 else 1.0
                
                adapter_config = ControlNetConfiguration(
                    name=f"controlnet_{controlnet_type}",
                    type=controlnet_type,
                    model_path=f"diffusers/controlnet-{controlnet_type}-sdxl-1.0",
                    conditioning_scale=conditioning_scale
                )
                
                success = await self.controlnet_worker.load_controlnet_model(adapter_config)
                if success:
                    logger.info(f"String ControlNet configured: {controlnet_type} (scale: {conditioning_scale})")
                    return True
                else:
                    logger.error(f"Failed to load string ControlNet: {controlnet_type}")
                    return False
            else:
                logger.error(f"Unsupported ControlNet configuration type: {type(controlnet_config)}")
                return False
                
        except Exception as e:
            logger.error(f"Failed to configure ControlNet adapters: {e}")
            import traceback
            traceback.print_exc()
            return False

    def unload_controlnet_adapter(self, name: str) -> bool:
        """Unload a specific ControlNet adapter."""
        try:
            # This is a placeholder for the async call - we'd need to handle this properly
            # In a real implementation, we'd need to manage this asynchronously
            logger.info(f"Unloading ControlNet adapter: {name}")
            return True
        except Exception as e:
            logger.error(f"Failed to unload ControlNet adapter {name}: {e}")
            return False
    
    def get_controlnet_performance_stats(self) -> Dict[str, Any]:
        """Get ControlNet performance statistics."""
        try:
            if hasattr(self, 'controlnet_worker') and self.controlnet_worker:
                return self.controlnet_worker.get_performance_stats()
            else:
                return {
                    "loaded_controlnets": 0,
                    "supported_types": [],
                    "current_stack_size": 0,
                    "total_loads": 0,
                    "memory_usage_mb": 0.0
                }
        except Exception as e:
            logger.error(f"Failed to get ControlNet performance stats: {e}")
            return {}

    async def _generate_with_refiner(self, request: EnhancedRequest, base_images: List[Any]) -> List[Any]:
        """
        Apply SDXL Refiner Pipeline to base images for quality enhancement.
        
        Phase 3 Days 29-30 Implementation: Two-stage generation workflow
        1. Base model generates initial images
        2. Refiner enhances quality using the new SDXL Refiner Pipeline
        
        Args:
            request: Enhanced request with refiner configuration
            base_images: List of PIL Images from base generation
            
        Returns:
            List of refined PIL Images
        """
        try:
            # Check if refiner is enabled and available
            if not hasattr(request.model, 'refiner') or not request.model.refiner:
                logger.debug("No refiner specified, returning base images")
                return base_images
            
            if not self.refiner_pipeline_manager.is_loaded:
                logger.info(f"Loading SDXL refiner for enhancement: {request.model.refiner}")
                await self.refiner_pipeline_manager.load_model()
            
            # Configure refiner parameters from request
            refiner_config = RefinerConfiguration()
            
            # Extract refiner settings from request if available
            if hasattr(request, 'refiner_config') and request.refiner_config:
                refiner_settings = request.refiner_config
                if isinstance(refiner_settings, dict):
                    refiner_config.strength = refiner_settings.get('strength', 0.3)
                    refiner_config.num_inference_steps = refiner_settings.get('num_inference_steps', 10)
                    refiner_config.guidance_scale = refiner_settings.get('guidance_scale', 7.5)
                    refiner_config.aesthetic_score = refiner_settings.get('aesthetic_score', 6.0)
            
            # Update refiner configuration
            self.refiner_pipeline_manager.update_configuration(refiner_config)
            
            logger.info(f"Applying SDXL refiner to {len(base_images)} images with strength {refiner_config.strength}")
            
            # Apply refiner to base images
            refined_images, refiner_metrics = await self.refiner_pipeline_manager.refine_images(
                base_images=base_images,
                prompt=request.prompt,
                negative_prompt=getattr(request, 'negative_prompt', None)
            )
            
            # Get refiner performance stats
            stats = self.refiner_pipeline_manager.get_performance_stats()
            logger.info(f"SDXL refiner completed: {len(refined_images)} images refined in {refiner_metrics.total_time_ms:.1f}ms")
            logger.info(f"Average quality improvement: {refiner_metrics.quality_improvement:.3f}")
            
            return refined_images
            
        except Exception as e:
            logger.error(f"SDXL refiner enhancement failed: {e}")
            logger.warning("Falling back to base images without refinement")
            return base_images

    async def cleanup(self) -> None:
        """Cleanup resources when worker is shut down."""
        try:
            # Cleanup pipelines
            if self.base_pipeline:
                del self.base_pipeline
                self.base_pipeline = None
            
            if self.refiner_pipeline:
                del self.refiner_pipeline
                self.refiner_pipeline = None
            
            # Cleanup feature workers
            await self.lora_worker.cleanup()
            await self.controlnet_worker.cleanup()
            # Phase 3 Days 29-30: Cleanup SDXL Refiner Pipeline
            if hasattr(self, 'refiner_pipeline_manager') and self.refiner_pipeline_manager:
                await self.refiner_pipeline_manager.cleanup()
                logger.info("SDXL Refiner Pipeline cleaned up")
            # TODO: Add VAE manager cleanup in Phase 2 Days 21+
            # await self.vae_manager.cleanup()
            
            # Clear CUDA cache if using CUDA
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
            
            logger.info("Enhanced SDXL Worker cleanup completed")
            
        except Exception as e:
            logger.error(f"Cleanup failed: {str(e)}")
