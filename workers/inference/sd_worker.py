"""
Stable Diffusion Worker
Worker process specialized for image generation using Stable Diffusion models
Handles text-to-image, image-to-image, and related tasks
"""

from typing import Dict, List, Optional, Any
import structlog
from ..base_worker import BaseWorker, WorkerState

logger = structlog.get_logger(__name__)


class StableDiffusionWorker(BaseWorker):
    """
    Worker specialized for Stable Diffusion image generation
    Supports various diffusion models and image generation tasks
    """
    
    def __init__(self, worker_id: str, config: Dict[str, Any]):
        """Initialize Stable Diffusion worker"""
        super().__init__(worker_id, config)
        self.loaded_pipelines = {}
        self.model_cache = {}        
        logger.info(f"StableDiffusionWorker {worker_id} initializing")
    async def initialize(self) -> bool:
        """Initialize Stable Diffusion resources"""
        try:
            logger.info(f"Initializing Stable Diffusion worker {self.worker_id}")
            
            # Import diffusers library with proper fallback
            try:
                import torch
                # Use correct imports for diffusers
                try:
                    from diffusers.pipelines.stable_diffusion.pipeline_stable_diffusion import StableDiffusionPipeline
                    from diffusers.pipelines.stable_diffusion.pipeline_stable_diffusion_img2img import StableDiffusionImg2ImgPipeline                
                except ImportError:
                    # Fallback to dynamic import if direct import fails
                    try:
                        import diffusers
                        # Try to get the classes from the diffusers module
                        StableDiffusionPipeline = getattr(diffusers, 'StableDiffusionPipeline', None)
                        StableDiffusionImg2ImgPipeline = getattr(diffusers, 'StableDiffusionImg2ImgPipeline', None)
                        if not StableDiffusionPipeline or not StableDiffusionImg2ImgPipeline:
                            raise ImportError("StableDiffusion pipelines not found in diffusers")
                    except (ImportError, AttributeError):
                        raise ImportError("Could not import diffusers pipelines")
                
                self.torch = torch
                self.StableDiffusionPipeline = StableDiffusionPipeline
                self.StableDiffusionImg2ImgPipeline = StableDiffusionImg2ImgPipeline
                
            except ImportError as e:
                logger.error(f"diffusers library not available: {e}")
                return False
            
            # Configure device
            self.device = "cuda" if torch.cuda.is_available() else "cpu"
            logger.info(f"Using device: {self.device}")
            
            # Load default model if specified
            if default_model := self.config.get('default_model', 'runwayml/stable-diffusion-v1-5'):
                await self._load_pipeline(default_model, "text2img")
            
            self.state = WorkerState.READY
            logger.info(f"StableDiffusionWorker {self.worker_id} initialized successfully")
            return True
            
        except Exception as e:
            logger.error(f"Failed to initialize SD worker: {e}")
            self.state = WorkerState.ERROR
            return False
    
    async def process_task(self, task_data: Dict[str, Any]) -> Dict[str, Any]:
        """Process image generation task"""
        try:
            task_type = task_data.get('type', 'text2img')
            prompt = task_data.get('prompt', '')
            model_id = task_data.get('model', 'runwayml/stable-diffusion-v1-5')
            
            logger.info(f"Processing {task_type} task with prompt: {prompt[:50]}...")
            
            # Load appropriate pipeline
            pipeline = await self._get_or_load_pipeline(model_id, task_type)
            if not pipeline:
                return {"error": "Failed to load pipeline"}
            
            # Generate parameters
            generation_params = {
                'prompt': prompt,
                'num_inference_steps': task_data.get('steps', 20),
                'guidance_scale': task_data.get('guidance_scale', 7.5),
                'width': task_data.get('width', 512),
                'height': task_data.get('height', 512),
                'num_images_per_prompt': task_data.get('num_images', 1)
            }
            
            # Add image input for img2img tasks
            if task_type == 'img2img' and 'input_image' in task_data:
                generation_params['image'] = task_data['input_image']
                generation_params['strength'] = task_data.get('strength', 0.75)
            # Generate image
            with self.torch.no_grad():
                result = pipeline(**generation_params)
                # Handle different result formats from diffusers
                if isinstance(result, tuple):
                    # diffusers pipelines sometimes return (images, ...)
                    images = result[0] if isinstance(result[0], list) else [result[0]]
                elif hasattr(result, 'images'):
                    images = result.images
                else:
                    images = result if isinstance(result, list) else [result]
            
            # Convert to base64 or save to disk
            image_data = []
            for i, image in enumerate(images):
                # For now, return image metadata
                image_info = {
                    'index': i,
                    'width': getattr(image, 'width', None),
                    'height': getattr(image, 'height', None),
                    'format': 'PNG'
                }
                image_data.append(image_info)
            
            return {
                'status': 'completed',
                'images': image_data,
                'metadata': {
                    'model': model_id,
                    'task_type': task_type,
                    'prompt': prompt,
                    'generation_params': generation_params
                }
            }
            
        except Exception as e:
            logger.error(f"Failed to process SD task: {e}")
            return {"error": str(e), "status": "failed"}
    
    async def _load_pipeline(self, model_id: str, pipeline_type: str):
        """Load a specific pipeline"""
        try:
            logger.info(f"Loading {pipeline_type} pipeline: {model_id}")
            
            if pipeline_type == "text2img":
                pipeline = self.StableDiffusionPipeline.from_pretrained(
                    model_id,
                    torch_dtype=self.torch.float16 if self.device == "cuda" else self.torch.float32
                ).to(self.device)
            elif pipeline_type == "img2img":
                pipeline = self.StableDiffusionImg2ImgPipeline.from_pretrained(
                    model_id,
                    torch_dtype=self.torch.float16 if self.device == "cuda" else self.torch.float32
                ).to(self.device)
            else:
                logger.error(f"Unsupported pipeline type: {pipeline_type}")
                return None
            
            # Cache the pipeline
            cache_key = f"{model_id}_{pipeline_type}"
            self.loaded_pipelines[cache_key] = pipeline
            
            logger.info(f"Pipeline loaded successfully: {cache_key}")
            return pipeline
            
        except Exception as e:
            logger.error(f"Failed to load pipeline {model_id}: {e}")
            return None
    
    async def _get_or_load_pipeline(self, model_id: str, pipeline_type: str):
        """Get existing pipeline or load new one"""
        cache_key = f"{model_id}_{pipeline_type}"
        
        if cache_key in self.loaded_pipelines:
            return self.loaded_pipelines[cache_key]
        
        return await self._load_pipeline(model_id, pipeline_type)
    
    async def unload_model(self, model_id: str):
        """Unload a specific model to free memory"""
        try:
            keys_to_remove = [k for k in self.loaded_pipelines.keys() if k.startswith(model_id)]
            for key in keys_to_remove:
                del self.loaded_pipelines[key]
                logger.info(f"Unloaded pipeline: {key}")
              # Force garbage collection
            if hasattr(self, 'torch') and self.torch.cuda.is_available():
                self.torch.cuda.empty_cache()
            
        except Exception as e:
            logger.error(f"Failed to unload model {model_id}: {e}")

    def get_capabilities(self) -> Dict[str, Any]:
        """Get Stable Diffusion worker capabilities"""
        # TODO: Return SD capabilities
        return {
            "worker_type": "stable_diffusion",
            "supported_tasks": ["text_to_image", "image_to_image", "inpainting"],
            "supported_models": ["sd_1_5", "sd_2_1", "sdxl", "lcm"],
            "max_resolution": "1024x1024",
            "gpu_memory_required": "8GB"
        }
