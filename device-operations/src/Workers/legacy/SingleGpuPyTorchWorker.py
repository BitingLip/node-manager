#!/usr/bin/env python3
"""
Simplified PyTorch DirectML Worker
=================================

Single-GPU focused worker for PyTorch DirectML inference operations.
Designed to work as part of a GPU pool managed by C# orchestrator.

Key principles:
- Single GPU assignment
- Minimal responsibilities 
- Model loading from shared cache or file
- Simple inference operations
- Clean separation of concerns

Communication via JSON over stdin/stdout.
"""

import os
import sys
import json
import time
import logging
from pathlib import Path
from typing import Dict, Any, Optional

# Set environment variables before torch import
os.environ["DISABLE_TELEMETRY"] = "1"
os.environ["CUDA_VISIBLE_DEVICES"] = ""
os.environ["ROCM_PATH"] = ""
os.environ["HIP_VISIBLE_DEVICES"] = ""

# Configure logging
os.makedirs('logs', exist_ok=True)
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - GPU-WORKER - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stderr),  # Clean stdout for JSON communication
        logging.FileHandler('logs/gpu-worker.log')
    ]
)
logger = logging.getLogger(__name__)

class SingleGpuPyTorchWorker:
    """Simplified PyTorch DirectML worker for single GPU operations"""
    
    def __init__(self, gpu_id: str):
        """Initialize worker for specific GPU"""
        self.gpu_id = gpu_id
        self.device = None
        self.pipeline = None
        self.model_id = None
        self.model_info = {}
        self.is_initialized = False
        
    def initialize(self, device_index: int = 0) -> Dict[str, Any]:
        """Initialize DirectML for assigned GPU"""
        try:
            import torch
            import torch_directml
            
            device_count = torch_directml.device_count()
            logger.info(f"Worker {self.gpu_id}: Found {device_count} DirectML devices")
            
            if device_index >= device_count:
                device_index = 0
                logger.warning(f"Worker {self.gpu_id}: Device index {device_index} out of range, using 0")
            
            self.device = torch_directml.device(device_index)
            self.is_initialized = True
            
            logger.info(f"Worker {self.gpu_id}: Initialized on device {device_index}: {self.device}")
            
            return {
                "success": True,
                "gpu_id": self.gpu_id,
                "device": str(self.device),
                "device_index": device_index
            }
            
        except Exception as e:
            logger.error(f"Worker {self.gpu_id}: Failed to initialize: {e}")
            return {
                "success": False,
                "error": str(e)
            }
    
    def load_model(self, model_path: str, model_type: str = "SDXL", model_id: Optional[str] = None) -> Dict[str, Any]:
        """Load model from file to this GPU's VRAM"""
        try:
            if not self.is_initialized:
                return {
                    "success": False,
                    "error": "Worker not initialized. Call initialize first."
                }
            
            logger.info(f"Worker {self.gpu_id}: Loading model from {model_path}")
            
            if not Path(model_path).exists():
                return {
                    "success": False,
                    "error": f"Model file not found: {model_path}"
                }
            
            from diffusers.pipelines.stable_diffusion_xl.pipeline_stable_diffusion_xl import StableDiffusionXLPipeline
            import torch
            
            start_time = time.time()
            
            # Load model with memory optimization
            self.pipeline = StableDiffusionXLPipeline.from_single_file(
                model_path,
                torch_dtype=torch.float16,
                use_safetensors=True,
                variant="fp16",
                low_cpu_mem_usage=True
            )
            
            # Move to assigned GPU
            logger.info(f"Worker {self.gpu_id}: Moving pipeline to device {self.device}")
            self.pipeline = self.pipeline.to(self.device)
            
            # Enable optimizations
            self.pipeline.enable_attention_slicing()
            
            load_time = time.time() - start_time
            
            # Set model info
            file_info = Path(model_path)
            self.model_id = model_id or file_info.stem
            self.model_info = {
                "model_id": self.model_id,
                "model_path": model_path,
                "model_type": model_type,
                "model_size_bytes": file_info.stat().st_size,
                "load_time_seconds": load_time,
                "gpu_id": self.gpu_id,
                "device": str(self.device)
            }
            
            logger.info(f"Worker {self.gpu_id}: Model loaded successfully in {load_time:.2f}s")
            
            return {
                "success": True,
                "data": {
                    "ModelId": self.model_id,
                    "LoadTimeSeconds": load_time,
                    "ModelSizeBytes": file_info.stat().st_size,
                    "GpuId": self.gpu_id
                }
            }
            
        except Exception as e:
            logger.error(f"Worker {self.gpu_id}: Failed to load model: {e}")
            return {
                "success": False,
                "error": str(e)
            }
    
    def load_model_from_cache(self, model_id: str, cache_data: Dict[str, Any]) -> Dict[str, Any]:
        """Load model from shared cache data to this GPU's VRAM"""
        try:
            # This would be implemented to load from shared memory/cache
            # For now, just load from the original path if provided
            model_path = cache_data.get("model_path")
            model_type = cache_data.get("model_type", "SDXL")
            
            if not model_path:
                return {
                    "success": False,
                    "error": "No model path in cache data"
                }
            
            logger.info(f"Worker {self.gpu_id}: Loading model {model_id} from cache")
            return self.load_model(model_path, model_type, model_id)
            
        except Exception as e:
            logger.error(f"Worker {self.gpu_id}: Failed to load from cache: {e}")
            return {
                "success": False,
                "error": str(e)
            }
    
    def unload_model(self) -> Dict[str, Any]:
        """Unload model from VRAM"""
        try:
            if self.pipeline is not None:
                logger.info(f"Worker {self.gpu_id}: Starting model unload process")
                
                # Move all pipeline components to CPU first to free VRAM
                try:
                    import torch
                    if hasattr(self.pipeline, 'vae') and self.pipeline.vae is not None:
                        self.pipeline.vae = self.pipeline.vae.to('cpu')
                        logger.info(f"Worker {self.gpu_id}: Moved VAE to CPU")
                    
                    if hasattr(self.pipeline, 'unet') and self.pipeline.unet is not None:
                        self.pipeline.unet = self.pipeline.unet.to('cpu')
                        logger.info(f"Worker {self.gpu_id}: Moved UNet to CPU")
                    
                    if hasattr(self.pipeline, 'text_encoder') and self.pipeline.text_encoder is not None:
                        self.pipeline.text_encoder = self.pipeline.text_encoder.to('cpu')
                        logger.info(f"Worker {self.gpu_id}: Moved Text Encoder to CPU")
                    
                    if hasattr(self.pipeline, 'text_encoder_2') and self.pipeline.text_encoder_2 is not None:
                        self.pipeline.text_encoder_2 = self.pipeline.text_encoder_2.to('cpu')
                        logger.info(f"Worker {self.gpu_id}: Moved Text Encoder 2 to CPU")
                        
                except Exception as e:
                    logger.warning(f"Worker {self.gpu_id}: Warning during component CPU transfer: {e}")
                
                # Delete the pipeline
                del self.pipeline
                self.pipeline = None
                logger.info(f"Worker {self.gpu_id}: Pipeline deleted")
                
                # Aggressive garbage collection
                import gc
                gc.collect()
                logger.info(f"Worker {self.gpu_id}: Garbage collection completed")
                
                # Clear DirectML cache properly
                try:
                    import torch
                    # For DirectML, we need to clear the cache differently
                    if hasattr(torch, 'directml') and hasattr(torch.directml, 'empty_cache'):
                        torch.directml.empty_cache()
                        logger.info(f"Worker {self.gpu_id}: DirectML cache cleared")
                    elif hasattr(torch.cuda, 'empty_cache'):
                        # Fallback to CUDA cache clearing
                        torch.cuda.empty_cache()
                        logger.info(f"Worker {self.gpu_id}: CUDA cache cleared")
                    
                    # Additional DirectML memory cleanup
                    if hasattr(torch, 'directml'):
                        # Force DirectML to release all cached memory
                        try:
                            torch.directml.synchronize()
                            logger.info(f"Worker {self.gpu_id}: DirectML synchronized")
                        except:
                            pass
                    
                except Exception as e:
                    logger.warning(f"Worker {self.gpu_id}: Cache clear warning: {e}")
                
                # Final garbage collection
                gc.collect()
                logger.info(f"Worker {self.gpu_id}: Final garbage collection completed")
                
                logger.info(f"Worker {self.gpu_id}: Model unloaded successfully")
            else:
                logger.info(f"Worker {self.gpu_id}: No model to unload")
            
            self.model_id = None
            self.model_info = {}
            
            return {
                "success": True,
                "message": f"Model unloaded from {self.gpu_id}"
            }
            
        except Exception as e:
            logger.error(f"Worker {self.gpu_id}: Failed to unload model: {e}")
            return {
                "success": False,
                "error": str(e)
            }
    
    def generate_image(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Generate image using loaded model"""
        if self.pipeline is None:
            return {
                "success": False,
                "error": "No model loaded"
            }
        
        try:
            # Extract parameters
            prompt = request.get("prompt", "")
            negative_prompt = request.get("negative_prompt", "")
            width = request.get("width", 512)
            height = request.get("height", 512)
            steps = request.get("steps", 20)
            guidance_scale = request.get("guidance_scale", 7.5)
            seed = request.get("seed", int(time.time()))
            
            logger.info(f"Worker {self.gpu_id}: Generating {width}x{height} image")
            
            import torch
            
            start_time = time.time()
            
            # Generate with DirectML
            with torch.no_grad():
                result = self.pipeline(
                    prompt=prompt,
                    negative_prompt=negative_prompt,
                    width=width,
                    height=height,
                    num_inference_steps=steps,
                    guidance_scale=guidance_scale,
                    generator=torch.Generator(device="cpu").manual_seed(seed)
                )
            
            # Handle the result which could be a tuple or have .images attribute
            image = None
            if hasattr(result, 'images') and result.images and len(result.images) > 0:
                image = result.images[0]
            elif isinstance(result, (tuple, list)) and len(result) > 0:
                # If result is a tuple, take the first element
                image = result[0]
            else:
                image = result
            generation_time = time.time() - start_time
            
            # Save image to root device-manager outputs folder
            output_dir = Path("c:/Users/admin/Desktop/device-manager/outputs")
            output_dir.mkdir(exist_ok=True)
            
            timestamp = int(time.time())
            filename = f"{self.gpu_id}_{timestamp}_{seed}.png"
            output_path = output_dir / filename
            
            # Handle different image types for saving
            try:
                if hasattr(image, 'save') and callable(getattr(image, 'save')):
                    # PIL Image
                    image.save(output_path)
                else:
                    # Convert to PIL Image for saving
                    from PIL import Image as PILImage
                    import numpy as np
                    import torch
                    
                    if isinstance(image, torch.Tensor):
                        # PyTorch tensor
                        image_np = image.detach().cpu().numpy()
                        if image_np.ndim == 4:
                            image_np = image_np[0]  # Remove batch dimension
                        if image_np.ndim == 3 and image_np.shape[0] == 3:
                            image_np = np.transpose(image_np, (1, 2, 0))  # CHW to HWC
                        image_np = (image_np * 255).astype(np.uint8)
                        pil_image = PILImage.fromarray(image_np)
                        pil_image.save(output_path)
                    elif isinstance(image, np.ndarray):
                        # NumPy array
                        image_np = image
                        if image_np.max() <= 1.0:
                            image_np = (image_np * 255).astype(np.uint8)
                        pil_image = PILImage.fromarray(image_np)
                        pil_image.save(output_path)
                    else:
                        # Try to handle if image is a list or tuple of images
                        if isinstance(image, (list, tuple)) and len(image) > 0:
                            first_image = image[0]
                            if hasattr(first_image, 'save') and callable(getattr(first_image, 'save')):
                                first_image.save(output_path)
                            else:
                                raise ValueError(f"First element in image list/tuple is not a savable image: {type(first_image)}")
                        elif hasattr(image, 'save') and callable(getattr(image, 'save')):
                            image.save(output_path)
                        else:
                            raise ValueError(f"Unknown image type: {type(image)}")
            except Exception as e:
                logger.error(f"Error saving image: {e}")
                raise
            
            logger.info(f"Worker {self.gpu_id}: Generated in {generation_time:.2f}s")
            
            # Clean up memory after inference to prevent VRAM accumulation
            self._cleanup_inference_memory()
            
            return {
                "success": True,
                "output_path": str(output_path),
                "generation_time_seconds": generation_time,
                "seed": seed,
                "width": width,
                "height": height,
                "gpu_id": self.gpu_id
            }
            
        except Exception as e:
            # Clean up memory even on failure
            self._cleanup_inference_memory()
            
            logger.error(f"Worker {self.gpu_id}: Image generation failed: {e}")
            return {
                "success": False,
                "error": str(e)
            }
    
    def get_status(self) -> Dict[str, Any]:
        """Get worker status"""
        return {
            "success": True,
            "gpu_id": self.gpu_id,
            "is_initialized": self.is_initialized,
            "has_model_loaded": self.pipeline is not None,
            "current_model_id": self.model_id,
            "model_info": self.model_info,
            "device": str(self.device) if self.device else None
        }
    
    def get_model_info(self) -> Dict[str, Any]:
        """Get information about loaded model"""
        if self.model_id is None:
            return {
                "success": False,
                "error": "No model loaded"
            }
        
        return {
            "success": True,
            "model_id": self.model_id,
            "model_info": self.model_info
        }
    
    def cleanup_memory(self) -> Dict[str, Any]:
        """Clean up memory without unloading model"""
        try:
            # Use the internal cleanup method
            self._cleanup_inference_memory()
            
            logger.info(f"Worker {self.gpu_id}: Memory cleanup completed")
            
            return {
                "success": True,
                "message": "Memory cleanup completed"
            }
            
        except Exception as e:
            logger.error(f"Worker {self.gpu_id}: Memory cleanup failed: {e}")
            return {
                "success": False,
                "error": str(e)
            }

    def _cleanup_inference_memory(self):
        """Internal method to clean up memory after inference operations"""
        try:
            import gc
            import torch
            
            # Force garbage collection to clean up temporary tensors
            gc.collect()
            
            # For DirectML, we need to clear the cache differently
            # DirectML uses privateuse devices, not CUDA
            try:
                # Clear DirectML cache
                if hasattr(torch, 'directml') and hasattr(torch.directml, 'empty_cache'):
                    torch.directml.empty_cache()
                elif hasattr(torch.cuda, 'empty_cache'):
                    # Fallback to CUDA cache clearing
                    torch.cuda.empty_cache()
            except Exception as e:
                # Silent fallback - some DirectML versions may not have this
                pass
            
            # Additional cleanup for diffusion models
            if self.pipeline is not None:
                try:
                    # Clear any cached activations in the pipeline components
                    if hasattr(self.pipeline, 'vae') and hasattr(self.pipeline.vae, 'to'):
                        # Re-sync VAE to ensure clean state
                        pass
                    if hasattr(self.pipeline, 'unet') and hasattr(self.pipeline.unet, 'to'):
                        # Re-sync UNet to ensure clean state  
                        pass
                except:
                    pass
            
            # Final garbage collection
            gc.collect()
            
        except Exception as e:
            # Silent cleanup failure - don't break inference
            logger.warning(f"Worker {self.gpu_id}: Memory cleanup warning: {e}")
            pass

def process_command(worker: SingleGpuPyTorchWorker, command: Dict[str, Any]) -> Dict[str, Any]:
    """Process a command from C# service"""
    action = command.get("action")
    
    try:
        if action == "initialize":
            device_index = command.get("device_index", 0)
            return worker.initialize(device_index)
        
        elif action == "load_model":
            model_path = command.get("model_path")
            model_type = command.get("model_type", "SDXL")
            model_id = command.get("model_id")
            if model_path is None:
                return {"success": False, "error": "model_path is required"}
            return worker.load_model(model_path, model_type, model_id)
        
        elif action == "load_model_from_cache":
            model_id = command.get("model_id")
            cache_data = command.get("cache_data", {})
            if model_id is None:
                return {"success": False, "error": "model_id is required"}
            return worker.load_model_from_cache(model_id, cache_data)
        
        elif action == "unload_model":
            return worker.unload_model()
        
        elif action == "generate_image":
            return worker.generate_image(command.get("parameters", {}))
        
        elif action == "get_status":
            return worker.get_status()
        
        elif action == "get_model_info":
            return worker.get_model_info()
        
        elif action == "cleanup_memory":
            return worker.cleanup_memory()
        
        else:
            return {
                "success": False,
                "error": f"Unknown action: {action}"
            }
    
    except Exception as e:
        logger.error(f"Command processing failed: {e}")
        return {
            "success": False,
            "error": str(e)
        }

def main():
    """Main worker loop"""
    # Get GPU ID from command line argument
    if len(sys.argv) < 2:
        logger.error("GPU ID required as command line argument")
        sys.exit(1)
    
    gpu_id = sys.argv[1]
    logger.info(f"Starting GPU worker for {gpu_id}")
    
    worker = SingleGpuPyTorchWorker(gpu_id)
    
    try:
        # Main communication loop
        for line in sys.stdin:
            line = line.strip()
            if not line:
                continue
            
            try:
                # Parse command from C# service
                command = json.loads(line)
                logger.info(f"Worker {gpu_id}: Received command: {command.get('action', 'unknown')}")
                
                # Process command
                response = process_command(worker, command)
                
                # Send response back to C# service
                print(json.dumps(response), flush=True)
                
            except json.JSONDecodeError as e:
                error_response = {
                    "success": False,
                    "error": f"Invalid JSON: {e}"
                }
                print(json.dumps(error_response), flush=True)
            
            except Exception as e:
                error_response = {
                    "success": False,
                    "error": f"Command error: {e}"
                }
                print(json.dumps(error_response), flush=True)
    
    except KeyboardInterrupt:
        logger.info(f"Worker {gpu_id}: Interrupted by user")
    
    except Exception as e:
        logger.error(f"Worker {gpu_id}: Worker error: {e}")
    
    finally:
        # Cleanup
        worker.unload_model()
        logger.info(f"Worker {gpu_id}: GPU worker stopped")

if __name__ == "__main__":
    main()
