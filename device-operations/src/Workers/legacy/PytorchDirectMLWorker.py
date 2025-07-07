#!/usr/bin/env python3
"""
PyTorch DirectML Worker
======================

Python worker for PyTorch DirectML inference operations.
Orchestrated by the C# device-operations service.

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
os.makedirs('logs', exist_ok=True)  # Create logs directory if it doesn't exist
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - PYTORCH-WORKER - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stderr),  # Use stderr so stdout is clean for JSON communication
        logging.FileHandler('logs/pytorch-worker.log')
    ]
)
logger = logging.getLogger(__name__)

class PyTorchDirectMLWorker:
    """PyTorch DirectML worker for inference operations"""
    
    def __init__(self):
        self.devices = []  # List of all available devices
        self.primary_device = None  # Primary device for coordination
        self.pipeline = None
        self.model_id = None
        self.model_info = {}
        self.multi_gpu_enabled = False
        self.device_pipelines = {}  # Pipeline replicas on each device
        
    def initialize_directml(self, device_id: int = 1, enable_multi_gpu: bool = False) -> Dict[str, Any]:
        """Initialize DirectML device(s)"""
        try:
            import torch
            import torch_directml
            
            device_count = torch_directml.device_count()
            logger.info(f"Found {device_count} DirectML devices")
            
            if enable_multi_gpu and device_count > 1:
                # Initialize all available devices
                logger.info("Initializing multi-GPU setup...")
                self.multi_gpu_enabled = True
                
                for i in range(device_count):
                    device = torch_directml.device(i)
                    self.devices.append(device)
                    logger.info(f"Initialized DirectML device {i}: {device}")
                
                # Set primary device (usually device 0 or the specified one)
                primary_idx = 0 if device_id >= device_count else device_id
                self.primary_device = self.devices[primary_idx]
                
                logger.info(f"Multi-GPU setup complete. Primary device: {self.primary_device}")
                
                return {
                    "success": True,
                    "multi_gpu": True,
                    "device_count": device_count,
                    "primary_device": str(self.primary_device),
                    "all_devices": [str(dev) for dev in self.devices]
                }
            else:
                # Single GPU mode
                logger.info(f"Initializing single DirectML device {device_id}")
                
                if device_id < device_count:
                    self.primary_device = torch_directml.device(device_id)
                else:
                    self.primary_device = torch_directml.device(0)
                    device_id = 0
                
                self.devices = [self.primary_device]
                
                logger.info(f"DirectML initialized: {self.primary_device}")
                
                return {
                    "success": True,
                    "multi_gpu": False,
                    "device": str(self.primary_device),
                    "device_id": device_id,
                    "device_count": device_count
                }
                
        except Exception as e:
            logger.error(f"Failed to initialize DirectML: {e}")
            return {
                "success": False,
                "error": str(e)
            }
    
    def load_model(self, model_path: str, model_type: str = "SDXL") -> Dict[str, Any]:
        """Load model to VRAM with optional multi-GPU support"""
        try:
            logger.info(f"Loading model to VRAM: {model_path}")
            
            if not Path(model_path).exists():
                return {
                    "success": False,
                    "error": f"Model file not found: {model_path}"
                }
            
            if not self.primary_device:
                return {
                    "success": False,
                    "error": "DirectML not initialized. Call initialize_directml first."
                }
            
            from diffusers import StableDiffusionXLPipeline
            import torch
            import psutil
            
            # Log initial memory usage
            process = psutil.Process()
            initial_memory = process.memory_info().rss / 1024 / 1024  # MB
            logger.info(f"Initial system memory usage: {initial_memory:.1f} MB")
            
            start_time = time.time()
            
            if self.multi_gpu_enabled:
                return self._load_model_multi_gpu(model_path, model_type, start_time, process)
            else:
                return self._load_model_single_gpu(model_path, model_type, start_time, process)
                
        except Exception as e:
            logger.error(f"Failed to load model: {e}")
            return {
                "success": False,
                "error": str(e)
            }
    
    def _load_model_single_gpu(self, model_path: str, model_type: str, start_time: float, process) -> Dict[str, Any]:
        """Load model to single GPU"""
        from diffusers import StableDiffusionXLPipeline
        import torch
        
        # Try to load directly to VRAM first, fallback to CPU loading if needed
        logger.info("Attempting direct VRAM loading...")
        try:
            # Method 1: Load with minimal CPU usage, then explicitly move to DirectML
            self.pipeline = StableDiffusionXLPipeline.from_single_file(
                model_path,
                torch_dtype=torch.float16,
                use_safetensors=True,
                variant="fp16",
                low_cpu_mem_usage=True,
                offload_folder=None  # Disable CPU offloading
            )
            
            # Explicitly move pipeline to DirectML device (VRAM)
            logger.info(f"Moving pipeline components to DirectML device: {self.primary_device}")
            self.pipeline = self.pipeline.to(self.primary_device)
            
            direct_load_time = time.time() - start_time
            direct_memory = process.memory_info().rss / 1024 / 1024  # MB
            logger.info(f"Pipeline loaded and moved to VRAM in {direct_load_time:.2f}s, memory: {direct_memory:.1f} MB")
            
        except Exception as e:
            logger.warning(f"Direct VRAM loading failed: {e}")
            logger.info("Falling back to CPU loading then transfer...")
            
            # Fallback: Load to CPU first, then move to DirectML device (VRAM)
            self.pipeline = StableDiffusionXLPipeline.from_single_file(
                model_path,
                torch_dtype=torch.float16,
                use_safetensors=True,
                variant="fp16",
                low_cpu_mem_usage=True
            )
            
            cpu_load_time = time.time() - start_time
            cpu_memory = process.memory_info().rss / 1024 / 1024  # MB
            logger.info(f"Pipeline loaded to CPU in {cpu_load_time:.2f}s, memory: {cpu_memory:.1f} MB")
            
            # Move pipeline to DirectML device (VRAM)
            logger.info(f"Moving pipeline to DirectML device (VRAM): {self.primary_device}")
            vram_start = time.time()
            self.pipeline = self.pipeline.to(self.primary_device)
            vram_load_time = time.time() - vram_start
            
            # Check memory after VRAM transfer
            vram_memory = process.memory_info().rss / 1024 / 1024  # MB
            logger.info(f"Pipeline moved to VRAM in {vram_load_time:.2f}s, memory: {vram_memory:.1f} MB")
        
        return self._finalize_model_loading(model_path, model_type, start_time)
    
    def _load_model_multi_gpu(self, model_path: str, model_type: str, start_time: float, process) -> Dict[str, Any]:
        """Load model across multiple GPUs with shared weights"""
        from diffusers import StableDiffusionXLPipeline
        import torch
        
        logger.info(f"Loading model across {len(self.devices)} GPUs with shared weights...")
        
        try:
            # First, load the model to CPU with shared memory optimization
            logger.info("Loading shared model to CPU...")
            base_pipeline = StableDiffusionXLPipeline.from_single_file(
                model_path,
                torch_dtype=torch.float16,
                use_safetensors=True,
                variant="fp16",
                low_cpu_mem_usage=True,
                offload_folder=None
            )
            
            cpu_load_time = time.time() - start_time
            cpu_memory = process.memory_info().rss / 1024 / 1024  # MB
            logger.info(f"Base pipeline loaded to CPU in {cpu_load_time:.2f}s, memory: {cpu_memory:.1f} MB")
            
            # Now distribute the model across all GPUs
            logger.info("Distributing model across all GPUs...")
            
            # Set primary pipeline (device 0)
            self.pipeline = base_pipeline.to(self.primary_device)
            self.device_pipelines[str(self.primary_device)] = self.pipeline
            logger.info(f"Primary pipeline loaded to {self.primary_device}")
            
            # Create replicas on other devices (sharing weights when possible)
            for i, device in enumerate(self.devices[1:], 1):
                logger.info(f"Loading replica to device {i}: {device}")
                
                # Clone the pipeline to the new device
                device_pipeline = StableDiffusionXLPipeline(
                    vae=base_pipeline.vae.to(device),
                    text_encoder=base_pipeline.text_encoder.to(device),
                    text_encoder_2=base_pipeline.text_encoder_2.to(device),
                    tokenizer=base_pipeline.tokenizer,
                    tokenizer_2=base_pipeline.tokenizer_2,
                    unet=base_pipeline.unet.to(device),
                    scheduler=base_pipeline.scheduler
                )
                
                self.device_pipelines[str(device)] = device_pipeline
                logger.info(f"Replica loaded to {device}")
            
            multi_gpu_load_time = time.time() - start_time
            final_memory = process.memory_info().rss / 1024 / 1024  # MB
            logger.info(f"Multi-GPU loading completed in {multi_gpu_load_time:.2f}s, memory: {final_memory:.1f} MB")
            logger.info(f"Model distributed across {len(self.device_pipelines)} GPUs")
            
            # Clean up base pipeline from CPU
            del base_pipeline
            import gc
            gc.collect()
            
        except Exception as e:
            logger.error(f"Multi-GPU loading failed: {e}")
            # Fallback to single GPU
            logger.info("Falling back to single GPU mode...")
            self.multi_gpu_enabled = False
            return self._load_model_single_gpu(model_path, model_type, start_time, process)
        
        return self._finalize_model_loading(model_path, model_type, start_time, is_multi_gpu=True)
    
    def _finalize_model_loading(self, model_path: str, model_type: str, start_time: float, is_multi_gpu: bool = False) -> Dict[str, Any]:
        """Finalize model loading with optimizations and verification"""
        # Enable optimizations on all pipelines
        if is_multi_gpu:
            for device_id, pipeline in self.device_pipelines.items():
                pipeline.enable_attention_slicing()
                logger.info(f"Optimizations enabled for device {device_id}")
        else:
            self.pipeline.enable_attention_slicing()
            logger.info("Optimizations enabled")
        
        # Verify pipeline device placement
        self._verify_device_placement(is_multi_gpu)
        
        load_time = time.time() - start_time
        
        # Get model info
        file_info = Path(model_path)
        model_size = file_info.stat().st_size
        
        self.model_id = file_info.stem
        self.model_info = {
            "model_path": model_path,
            "model_type": model_type,
            "model_size_bytes": model_size,
            "load_time_seconds": load_time,
            "multi_gpu": is_multi_gpu,
            "device_count": len(self.devices),
            "primary_device": str(self.primary_device)
        }
        
        logger.info(f"Model loaded successfully in {load_time:.2f}s")
        
        return {
            "success": True,
            "model_id": self.model_id,
            "load_time_seconds": load_time,
            "model_size_bytes": model_size,
            "multi_gpu": is_multi_gpu,
            "device_count": len(self.devices),
            "primary_device": str(self.primary_device)
        }
    
    def _verify_device_placement(self, is_multi_gpu: bool):
        """Verify pipeline device placement"""
        logger.info("Verifying pipeline device placement...")
        
        if is_multi_gpu:
            for device_id, pipeline in self.device_pipelines.items():
                try:
                    logger.info(f"Device {device_id}:")
                    if hasattr(pipeline.unet, 'device'):
                        logger.info(f"  UNet device: {pipeline.unet.device}")
                    if hasattr(pipeline.vae, 'device'):
                        logger.info(f"  VAE device: {pipeline.vae.device}")
                    if hasattr(pipeline.text_encoder, 'device'):
                        logger.info(f"  Text Encoder device: {pipeline.text_encoder.device}")
                    if hasattr(pipeline.text_encoder_2, 'device'):
                        logger.info(f"  Text Encoder 2 device: {pipeline.text_encoder_2.device}")
                except Exception as e:
                    logger.warning(f"Could not verify device placement for {device_id}: {e}")
        else:
            try:
                if hasattr(self.pipeline.unet, 'device'):
                    logger.info(f"UNet device: {self.pipeline.unet.device}")
                if hasattr(self.pipeline.vae, 'device'):
                    logger.info(f"VAE device: {self.pipeline.vae.device}")
                if hasattr(self.pipeline.text_encoder, 'device'):
                    logger.info(f"Text Encoder device: {self.pipeline.text_encoder.device}")
                if hasattr(self.pipeline.text_encoder_2, 'device'):
                    logger.info(f"Text Encoder 2 device: {self.pipeline.text_encoder_2.device}")
            except Exception as e:
                logger.warning(f"Could not verify device placement: {e}")
    
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
            
            logger.info(f"Generating {width}x{height} image")
            logger.info(f"Prompt: {prompt[:50]}...")
            
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
            
            image = result.images[0]
            generation_time = time.time() - start_time
            
            # Save image
            output_dir = Path("outputs")
            output_dir.mkdir(exist_ok=True)
            
            timestamp = int(time.time())
            filename = f"pytorch_worker_{timestamp}_{seed}.png"
            output_path = output_dir / filename
            image.save(output_path)
            
            logger.info(f"Generated in {generation_time:.2f}s")
            
            return {
                "success": True,
                "output_path": str(output_path),
                "generation_time_seconds": generation_time,
                "seed": seed,
                "width": width,
                "height": height
            }
            
        except Exception as e:
            logger.error(f"Image generation failed: {e}")
            return {
                "success": False,
                "error": str(e)
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
    
    def unload_model(self) -> Dict[str, Any]:
        """Unload model from VRAM"""
        try:
            if self.pipeline is not None:
                del self.pipeline
                self.pipeline = None
                
                # Force garbage collection
                import gc
                gc.collect()
                
                # Clear CUDA cache if available
                try:
                    import torch
                    if hasattr(torch.cuda, 'empty_cache'):
                        torch.cuda.empty_cache()
                except:
                    pass
                
                logger.info("Model unloaded from VRAM")
            
            self.model_id = None
            self.model_info = {}
            
            return {
                "success": True,
                "message": "Model unloaded successfully"
            }
            
        except Exception as e:
            logger.error(f"Failed to unload model: {e}")
            return {
                "success": False,
                "error": str(e)
            }
    
    def cleanup_memory(self) -> Dict[str, Any]:
        """Clean up memory residuals while keeping model loaded"""
        try:
            import gc
            import torch
            
            # Garbage collection
            for _ in range(3):
                gc.collect()
            
            # Clear cached tensors
            try:
                if hasattr(torch.cuda, 'empty_cache'):
                    torch.cuda.empty_cache()
            except:
                pass
            
            logger.info("Memory cleanup completed")
            
            return {
                "success": True,
                "message": "Memory cleanup completed"
            }
            
        except Exception as e:
            logger.error(f"Memory cleanup failed: {e}")
            return {
                "success": False,
                "error": str(e)
            }

def process_command(worker: PyTorchDirectMLWorker, command: Dict[str, Any]) -> Dict[str, Any]:
    """Process a command from C# service"""
    action = command.get("action")
    
    try:
        if action == "initialize":
            device_id = command.get("device_id", 1)
            enable_multi_gpu = command.get("enable_multi_gpu", False)
            return worker.initialize_directml(device_id, enable_multi_gpu)
        
        elif action == "load_model":
            model_path = command.get("model_path")
            model_type = command.get("model_type", "SDXL")
            return worker.load_model(model_path, model_type)
        
        elif action == "generate_image":
            return worker.generate_image(command.get("parameters", {}))
        
        elif action == "get_model_info":
            return worker.get_model_info()
        
        elif action == "unload_model":
            return worker.unload_model()
        
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
    logger.info("PyTorch DirectML Worker started")
    
    worker = PyTorchDirectMLWorker()
    
    try:
        # Main communication loop
        for line in sys.stdin:
            line = line.strip()
            if not line:
                continue
            
            try:
                # Parse command from C# service
                command = json.loads(line)
                logger.info(f"Received command: {command.get('action', 'unknown')}")
                
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
        logger.info("Worker interrupted by user")
    
    except Exception as e:
        logger.error(f"Worker error: {e}")
    
    finally:
        # Cleanup
        worker.unload_model()
        logger.info("PyTorch DirectML Worker stopped")

if __name__ == "__main__":
    main()
