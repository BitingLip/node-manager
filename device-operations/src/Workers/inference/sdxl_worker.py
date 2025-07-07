"""
SDXL Inference Worker
====================

Main worker for handling SDXL inference requests with comprehensive support for
text-to-image, image-to-image, inpainting, LoRA, ControlNet, and advanced features.
"""

import logging
import torch
import gc
import base64
import io
import time
from typing import Dict, Any, Optional, List, Union, Tuple
from pathlib import Path
from datetime import datetime
from PIL import Image
import numpy as np

from diffusers import (
    StableDiffusionXLPipeline,
    StableDiffusionXLImg2ImgPipeline,
    StableDiffusionXLInpaintPipeline,
    DiffusionPipeline
)
from diffusers.utils import logging as diffusers_logging

from ..core.base_worker import BaseWorker, WorkerRequest, WorkerResponse, ProcessingError
from ..core.device_manager import get_device_manager
from ..core.communication import StreamingResponse
from ..models.model_loader import ModelLoader
from ..models.lora_manager import LoRAManager
from ..schedulers.scheduler_factory import get_scheduler_factory


class SDXLWorker(BaseWorker):
    """
    Main worker for SDXL inference operations.
    
    Handles text-to-image, image-to-image, and inpainting with advanced features
    including LoRA, ControlNet, custom schedulers, and memory optimization.
    """
    
    def __init__(self, worker_id: str = "sdxl_worker", config: Optional[Dict[str, Any]] = None):
        super().__init__(worker_id, config)
        
        # Core components
        self.device_manager = None
        self.model_loader = None
        self.lora_manager = None
        self.scheduler_factory = None
        
        # Loaded pipelines
        self.pipelines: Dict[str, DiffusionPipeline] = {}
        self.current_pipeline: Optional[DiffusionPipeline] = None
        self.current_model_name: Optional[str] = None
        
        # Configuration
        self.output_path = Path(self.config.get("output_path", "./outputs"))
        self.enable_safety_checker = self.config.get("enable_safety_checker", False)
        self.max_batch_size = self.config.get("max_batch_size", 4)
        
        # Performance settings
        self.enable_xformers = self.config.get("enable_xformers", True)
        self.enable_compile = self.config.get("enable_compile", False)
        
        # Create output directory
        self.output_path.mkdir(parents=True, exist_ok=True)
    
    async def initialize(self) -> bool:
        """Initialize the SDXL worker."""
        try:
            self.logger.info("Initializing SDXL worker...")
            
            # Initialize core components
            self.device_manager = get_device_manager()
            self.scheduler_factory = get_scheduler_factory()
            
            # Initialize model loader
            model_loader_config = self.config.get("model_loader", {})
            self.model_loader = ModelLoader("model_loader", model_loader_config)
            await self.model_loader.initialize()
            
            # Initialize LoRA manager
            lora_path = self.config.get("lora_path", "./models/loras")
            self.lora_manager = LoRAManager(lora_path)
            
            # Set diffusers logging level
            diffusers_logging.set_verbosity_error()
            
            self.logger.info("SDXL worker initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to initialize SDXL worker: {str(e)}")
            return False
    
    async def process_request(self, request: WorkerRequest) -> WorkerResponse:
        """Process an SDXL inference request."""
        try:
            start_time = time.time()
            
            # Validate request
            self._validate_request(request.data)
            
            # Extract request parameters
            prompt = request.data["prompt"]
            model_name = request.data["model_name"]
            
            self.logger.info(f"Processing SDXL request: {request.request_id}")
            self.logger.info(f"Model: {model_name}, Prompt: {prompt[:100]}...")
            
            # Load model if needed
            await self._ensure_model_loaded(model_name, request.data)
            
            # Configure pipeline for request
            await self._configure_pipeline(request.data)
            
            # Perform inference
            result = await self._perform_inference(request.data, request.request_id)
            
            # Save outputs
            saved_files = await self._save_outputs(result, request.data, request.request_id)
            
            # Prepare response
            processing_time = time.time() - start_time
            
            response_data = {
                "images": saved_files,
                "model_used": model_name,
                "processing_time": processing_time,
                "inference_steps": request.data.get("hyperparameters", {}).get("num_inference_steps", 20),
                "guidance_scale": request.data.get("hyperparameters", {}).get("guidance_scale", 7.5),
                "seed": request.data.get("hyperparameters", {}).get("seed", -1),
                "scheduler": request.data.get("scheduler", "DPMSolverMultistepScheduler"),
                "dimensions": request.data.get("dimensions", {"width": 1024, "height": 1024})
            }
            
            self.logger.info(f"SDXL request completed in {processing_time:.2f}s")
            
            return WorkerResponse(
                request_id=request.request_id,
                success=True,
                data=response_data
            )
            
        except Exception as e:
            error_msg = f"SDXL inference failed: {str(e)}"
            self.logger.error(error_msg)
            return WorkerResponse(
                request_id=request.request_id,
                success=False,
                error=error_msg
            )
    
    def _validate_request(self, data: Dict[str, Any]) -> None:
        """Validate the inference request data."""
        required_fields = ["prompt", "model_name"]
        
        for field in required_fields:
            if field not in data:
                raise ProcessingError(f"Missing required field: {field}")
        
        # Validate dimensions
        dimensions = data.get("dimensions", {})
        if dimensions:
            width = dimensions.get("width", 1024)
            height = dimensions.get("height", 1024)
            
            if width % 8 != 0 or height % 8 != 0:
                raise ProcessingError("Width and height must be multiples of 8")
            
            if width < 256 or width > 2048 or height < 256 or height > 2048:
                raise ProcessingError("Dimensions must be between 256 and 2048 pixels")
        
        # Validate batch size
        batch_size = data.get("batch", {}).get("size", 1)
        if batch_size > self.max_batch_size:
            raise ProcessingError(f"Batch size {batch_size} exceeds maximum {self.max_batch_size}")
    
    async def _ensure_model_loaded(self, model_name: str, request_data: Dict[str, Any]) -> None:
        """Ensure the requested model is loaded."""
        if self.current_model_name == model_name and self.current_pipeline is not None:
            self.logger.debug(f"Model {model_name} already loaded")
            return
        
        # Determine pipeline type
        pipeline_type = self._determine_pipeline_type(request_data)
        
        # Load model through model loader
        load_request = {
            "model_name": model_name,
            "model_type": "base",
            "precision": request_data.get("precision", {}).get("dtype", "float16"),
            "cache_model": True
        }
        
        # Create a worker request for model loading
        model_request = WorkerRequest(
            request_id=f"load_{model_name}",
            worker_type="model_loader",
            data=load_request
        )
        
        # Load the model
        model_response = await self.model_loader.process_request(model_request)
        
        if not model_response.success:
            raise ProcessingError(f"Failed to load model: {model_response.error}")
        
        # Get the loaded pipeline from cache
        model_id = model_response.data["model_id"]
        pipeline = self.model_loader.cache.get(model_id)
        
        if pipeline is None:
            raise ProcessingError(f"Model {model_name} not found in cache")
        
        # Convert pipeline type if needed
        if pipeline_type != "text2img":
            pipeline = self._convert_pipeline_type(pipeline, pipeline_type)
        
        # Store pipeline
        self.current_pipeline = pipeline
        self.current_model_name = model_name
        self.pipelines[model_name] = pipeline
        
        # Set pipeline for LoRA manager
        self.lora_manager.set_pipeline(pipeline)
        
        self.logger.info(f"Model loaded: {model_name} ({pipeline_type})")
    
    def _determine_pipeline_type(self, request_data: Dict[str, Any]) -> str:
        """Determine the pipeline type needed for the request."""
        # Check for image-to-image
        if "init_image" in request_data or request_data.get("hyperparameters", {}).get("strength") is not None:
            return "img2img"
        
        # Check for inpainting
        if "mask_image" in request_data:
            return "inpainting"
        
        # Default to text-to-image
        return "text2img"
    
    def _convert_pipeline_type(self, base_pipeline: StableDiffusionXLPipeline, 
                             target_type: str) -> DiffusionPipeline:
        """Convert pipeline to the target type."""
        device = self.device_manager.get_device()
        
        if target_type == "img2img":
            return StableDiffusionXLImg2ImgPipeline(
                vae=base_pipeline.vae,
                text_encoder=base_pipeline.text_encoder,
                text_encoder_2=base_pipeline.text_encoder_2,
                tokenizer=base_pipeline.tokenizer,
                tokenizer_2=base_pipeline.tokenizer_2,
                unet=base_pipeline.unet,
                scheduler=base_pipeline.scheduler,
                force_zeros_for_empty_prompt=base_pipeline.config.force_zeros_for_empty_prompt,
                add_watermarker=base_pipeline.config.add_watermarker
            ).to(device)
        
        elif target_type == "inpainting":
            return StableDiffusionXLInpaintPipeline(
                vae=base_pipeline.vae,
                text_encoder=base_pipeline.text_encoder,
                text_encoder_2=base_pipeline.text_encoder_2,
                tokenizer=base_pipeline.tokenizer,
                tokenizer_2=base_pipeline.tokenizer_2,
                unet=base_pipeline.unet,
                scheduler=base_pipeline.scheduler,
                force_zeros_for_empty_prompt=base_pipeline.config.force_zeros_for_empty_prompt,
                add_watermarker=base_pipeline.config.add_watermarker
            ).to(device)
        
        return base_pipeline
    
    async def _configure_pipeline(self, request_data: Dict[str, Any]) -> None:
        """Configure the pipeline for the request."""
        if not self.current_pipeline:
            raise ProcessingError("No pipeline loaded")
        
        # Configure scheduler
        scheduler_name = request_data.get("scheduler", "DPMSolverMultistepScheduler")
        steps = request_data.get("hyperparameters", {}).get("num_inference_steps", 20)
        
        new_scheduler = self.scheduler_factory.create_from_pipeline(
            self.current_pipeline,
            scheduler_name,
            steps
        )
        self.current_pipeline.scheduler = new_scheduler
        
        # Apply memory optimizations
        self._apply_memory_optimizations()
        
        # Configure LoRA if specified
        lora_config = request_data.get("lora", {})
        if lora_config.get("enabled", False):
            await self._configure_lora(lora_config)
        
        # Configure precision settings
        precision_config = request_data.get("precision", {})
        self._apply_precision_settings(precision_config)
    
    def _apply_memory_optimizations(self) -> None:
        """Apply memory optimizations to the pipeline."""
        settings = self.device_manager.optimize_memory_settings()
        
        if settings.get("attention_slicing", True):
            self.current_pipeline.enable_attention_slicing()
        
        if settings.get("vae_slicing", True):
            self.current_pipeline.enable_vae_slicing()
        
        if settings.get("cpu_offload", False):
            self.current_pipeline.enable_model_cpu_offload()
        elif settings.get("sequential_cpu_offload", False):
            self.current_pipeline.enable_sequential_cpu_offload()
        
        # Enable xformers if available and configured
        if self.enable_xformers:
            try:
                self.current_pipeline.enable_xformers_memory_efficient_attention()
                self.logger.debug("XFormers memory efficient attention enabled")
            except Exception as e:
                self.logger.warning(f"Failed to enable XFormers: {str(e)}")
        
        # Compile model if configured (experimental)
        if self.enable_compile:
            try:
                self.current_pipeline.unet = torch.compile(self.current_pipeline.unet)
                self.logger.debug("Model compilation enabled")
            except Exception as e:
                self.logger.warning(f"Failed to compile model: {str(e)}")
    
    async def _configure_lora(self, lora_config: Dict[str, Any]) -> None:
        """Configure LoRA models for the pipeline."""
        lora_models = lora_config.get("models", [])
        
        if not lora_models:
            return
        
        # Unload existing LoRAs
        self.lora_manager.unload_all_loras()
        
        # Load and apply new LoRAs
        success = self.lora_manager.load_and_apply_loras(lora_models)
        
        if success:
            self.logger.info(f"Configured {len(lora_models)} LoRA models")
        else:
            self.logger.warning("Some LoRA models failed to load")
    
    def _apply_precision_settings(self, precision_config: Dict[str, Any]) -> None:
        """Apply precision settings to the pipeline."""
        # VAE precision handling
        vae_dtype = precision_config.get("vae_dtype", "float32")
        if vae_dtype == "float32" and self.current_pipeline.vae.dtype != torch.float32:
            self.current_pipeline.vae = self.current_pipeline.vae.to(torch.float32)
            self.logger.debug("VAE set to float32 precision")
    
    async def _perform_inference(self, request_data: Dict[str, Any], request_id: str) -> Dict[str, Any]:
        """Perform the actual inference."""
        # Prepare inference parameters
        inference_params = self._prepare_inference_params(request_data)
        
        # Create streaming response for progress updates
        streaming = StreamingResponse(request_id, self.comm_manager if hasattr(self, 'comm_manager') else None)
        
        try:
            # Perform inference based on pipeline type
            pipeline_type = self._determine_pipeline_type(request_data)
            
            if pipeline_type == "text2img":
                result = await self._text_to_image(inference_params, streaming)
            elif pipeline_type == "img2img":
                result = await self._image_to_image(inference_params, request_data, streaming)
            elif pipeline_type == "inpainting":
                result = await self._inpainting(inference_params, request_data, streaming)
            else:
                raise ProcessingError(f"Unsupported pipeline type: {pipeline_type}")
            
            return result
            
        finally:
            # Clean up GPU memory
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
            gc.collect()
    
    def _prepare_inference_params(self, request_data: Dict[str, Any]) -> Dict[str, Any]:
        """Prepare parameters for inference."""
        hyperparams = request_data.get("hyperparameters", {})
        dimensions = request_data.get("dimensions", {})
        batch = request_data.get("batch", {})
        
        params = {
            "prompt": request_data["prompt"],
            "negative_prompt": request_data.get("negative_prompt", ""),
            "num_inference_steps": hyperparams.get("num_inference_steps", 20),
            "guidance_scale": hyperparams.get("guidance_scale", 7.5),
            "width": dimensions.get("width", 1024),
            "height": dimensions.get("height", 1024),
            "num_images_per_prompt": batch.get("size", 1),
            "eta": 0.0,
            "output_type": "pil",
            "return_dict": True,
            "cross_attention_kwargs": None
        }
        
        # Handle seed
        seed = hyperparams.get("seed", -1)
        if seed != -1:
            generator = torch.Generator(device=self.device_manager.get_device())
            generator.manual_seed(seed)
            params["generator"] = generator
        
        # Handle strength for img2img
        if "strength" in hyperparams:
            params["strength"] = hyperparams["strength"]
        
        return params
    
    async def _text_to_image(self, params: Dict[str, Any], streaming: Optional[StreamingResponse]) -> Dict[str, Any]:
        """Perform text-to-image inference."""
        self.logger.info("Performing text-to-image inference")
        
        if streaming:
            streaming.send_progress(0, params["num_inference_steps"], "Starting text-to-image generation")
        
        # Add callback for progress updates
        def callback(step: int, timestep: int, latents: torch.FloatTensor):
            if streaming:
                streaming.send_progress(step + 1, params["num_inference_steps"], f"Inference step {step + 1}")
        
        params["callback"] = callback
        params["callback_steps"] = 1
        
        # Perform inference
        with torch.no_grad():
            result = self.current_pipeline(**params)
        
        if streaming:
            streaming.send_progress(params["num_inference_steps"], params["num_inference_steps"], "Generation complete")
        
        return {"images": result.images, "has_nsfw_content": getattr(result, 'nsfw_content_detected', None)}
    
    async def _image_to_image(self, params: Dict[str, Any], request_data: Dict[str, Any], 
                           streaming: Optional[StreamingResponse]) -> Dict[str, Any]:
        """Perform image-to-image inference."""
        self.logger.info("Performing image-to-image inference")
        
        # Load init image
        init_image = self._load_image_from_data(request_data.get("init_image"))
        if init_image is None:
            raise ProcessingError("No init_image provided for img2img")
        
        params["image"] = init_image
        
        if streaming:
            streaming.send_progress(0, params["num_inference_steps"], "Starting image-to-image generation")
        
        # Add callback for progress updates
        def callback(step: int, timestep: int, latents: torch.FloatTensor):
            if streaming:
                streaming.send_progress(step + 1, params["num_inference_steps"], f"Inference step {step + 1}")
        
        params["callback"] = callback
        params["callback_steps"] = 1
        
        # Perform inference
        with torch.no_grad():
            result = self.current_pipeline(**params)
        
        if streaming:
            streaming.send_progress(params["num_inference_steps"], params["num_inference_steps"], "Generation complete")
        
        return {"images": result.images, "has_nsfw_content": getattr(result, 'nsfw_content_detected', None)}
    
    async def _inpainting(self, params: Dict[str, Any], request_data: Dict[str, Any], 
                        streaming: Optional[StreamingResponse]) -> Dict[str, Any]:
        """Perform inpainting inference."""
        self.logger.info("Performing inpainting inference")
        
        # Load init image and mask
        init_image = self._load_image_from_data(request_data.get("image"))
        mask_image = self._load_image_from_data(request_data.get("mask_image"))
        
        if init_image is None or mask_image is None:
            raise ProcessingError("Both image and mask_image required for inpainting")
        
        params["image"] = init_image
        params["mask_image"] = mask_image
        
        if streaming:
            streaming.send_progress(0, params["num_inference_steps"], "Starting inpainting generation")
        
        # Add callback for progress updates
        def callback(step: int, timestep: int, latents: torch.FloatTensor):
            if streaming:
                streaming.send_progress(step + 1, params["num_inference_steps"], f"Inference step {step + 1}")
        
        params["callback"] = callback
        params["callback_steps"] = 1
        
        # Perform inference
        with torch.no_grad():
            result = self.current_pipeline(**params)
        
        if streaming:
            streaming.send_progress(params["num_inference_steps"], params["num_inference_steps"], "Generation complete")
        
        return {"images": result.images, "has_nsfw_content": getattr(result, 'nsfw_content_detected', None)}
    
    def _load_image_from_data(self, image_data: Any) -> Optional[Image.Image]:
        """Load image from various data formats."""
        if image_data is None:
            return None
        
        try:
            if isinstance(image_data, str):
                # Base64 encoded image
                if image_data.startswith("data:image/"):
                    # Remove data URL prefix
                    image_data = image_data.split(",", 1)[1]
                
                # Decode base64
                image_bytes = base64.b64decode(image_data)
                image = Image.open(io.BytesIO(image_bytes))
                return image.convert("RGB")
            
            elif isinstance(image_data, (bytes, bytearray)):
                # Raw image bytes
                image = Image.open(io.BytesIO(image_data))
                return image.convert("RGB")
            
            elif hasattr(image_data, 'read'):
                # File-like object
                image = Image.open(image_data)
                return image.convert("RGB")
            
            else:
                self.logger.warning(f"Unsupported image data type: {type(image_data)}")
                return None
                
        except Exception as e:
            self.logger.error(f"Failed to load image: {str(e)}")
            return None
    
    async def _save_outputs(self, result: Dict[str, Any], request_data: Dict[str, Any], 
                          request_id: str) -> List[str]:
        """Save generated images and return file paths."""
        saved_files = []
        
        output_config = request_data.get("output", {})
        output_format = output_config.get("format", "png")
        quality = output_config.get("quality", 95)
        custom_path = output_config.get("save_path")
        
        # Create timestamped directory
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_dir = Path(custom_path) if custom_path else self.output_path / timestamp
        output_dir.mkdir(parents=True, exist_ok=True)
        
        images = result.get("images", [])
        
        for i, image in enumerate(images):
            # Generate filename
            filename = f"{request_id}_{i:03d}.{output_format}"
            filepath = output_dir / filename
            
            # Save image
            save_kwargs = {}
            if output_format.lower() in ["jpg", "jpeg"]:
                save_kwargs["quality"] = quality
                save_kwargs["optimize"] = True
            elif output_format.lower() == "png":
                save_kwargs["optimize"] = True
            elif output_format.lower() == "webp":
                save_kwargs["quality"] = quality
                save_kwargs["method"] = 6
            
            image.save(str(filepath), **save_kwargs)
            saved_files.append(str(filepath))
            
            self.logger.debug(f"Saved image: {filepath}")
        
        return saved_files
    
    async def cleanup(self) -> None:
        """Clean up SDXL worker resources."""
        # Clean up pipelines
        for pipeline in self.pipelines.values():
            del pipeline
        
        self.pipelines.clear()
        self.current_pipeline = None
        self.current_model_name = None
        
        # Clean up components
        if self.model_loader:
            await self.model_loader.cleanup()
        
        if self.lora_manager:
            self.lora_manager.cleanup()
        
        # Clean up GPU memory
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
        
        gc.collect()
        self.logger.info("SDXL worker cleaned up")
    
    def get_status(self) -> Dict[str, Any]:
        """Get worker status information."""
        base_status = super().get_status()
        
        status = {
            **base_status,
            "loaded_models": list(self.pipelines.keys()),
            "current_model": self.current_model_name,
            "device_info": self.device_manager.get_device_info().__dict__ if self.device_manager else None,
            "memory_info": self.device_manager.get_memory_info() if self.device_manager else None,
            "lora_info": self.lora_manager.get_loaded_loras() if self.lora_manager else [],
            "cache_stats": self.model_loader.get_cache_stats() if self.model_loader else None
        }
        
        return status
