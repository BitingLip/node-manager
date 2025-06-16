#!/usr/bin/env python3
# DO NOT USE EMOJI's
"""
Memory - Memory management and model loading for worker processes
Handles RAM staging, VRAM management, and cleanup operations
"""
import gc
import time
from pathlib import Path
from typing import Dict, Any, Optional


class Memory:
    """Memory management for model loading and VRAM optimization"""
    
    def __init__(self, device_id: int, logger, hardware):
        self.device_id = device_id
        self.device = f"privateuseone:{device_id}"
        self.logger = logger
        self.hardware = hardware
          # Memory state
        self.current_model = None
        self.model_in_ram = None
        self.pipeline = None
        self.ram_staging_data = None
          # DirectML/PyTorch setup
        self.torch = None
        self.torch_directml = None
        self._init_torch()
        
        self.logger.info(f"Memory manager initialized for GPU {device_id}")
        
    def _init_torch(self):
        """Initialize PyTorch and DirectML"""
        try:
            # Apply DirectML patch if available (only log about our specific device)
            try:
                from . import directml  # Import DirectML patch (applies automatically)
                
                # Only log patch info once for device 0 to avoid spam
                if self.device_id == 0:
                    self.logger.info("DirectML multi-GPU patch applied")
                    device_count = directml._dml_patch.device_count
                    self.logger.info(f"DirectML devices available: {device_count}")
                    for i in range(device_count):
                        device = directml._dml_patch.devices[i]
                        self.logger.info(f"DirectML Device {i}: {device}")
                    self.logger.info(f"CUDA calls will be distributed across {device_count} DirectML devices")
                else:
                    # For other devices, just log that the patch is available
                    self.logger.info(f"DirectML multi-GPU patch already applied (worker assigned to device {self.device_id})")
                
            except ImportError:
                self.logger.warning("DirectML patch not found, using standard DirectML")
            
            import torch
            import torch_directml
            self.torch = torch
            self.torch_directml = torch_directml
            
            # Verify device availability
            device_count = torch_directml.device_count()
            if self.device_id >= device_count:
                raise ValueError(f"Device {self.device_id} not available (only {device_count} devices)")
              # Test device access
            test_tensor = torch.randn(2, 2, dtype=torch.float16, device=self.device)
            del test_tensor
            gc.collect()
            
            self.logger.info(f"DirectML initialized for device {self.device_id}")
            
        except Exception as e:
            self.logger.error(f"Failed to initialize DirectML: {e}")
            raise
    
    def load_model_to_ram(self, model_info: Dict[str, Any]) -> Dict[str, Any]:
        """Load model from disk to RAM (staging area)"""
        start_time = time.time()
        
        try:
            model_name = model_info.get('name')
            model_path = model_info.get('path')
            
            # Convert to Path object and resolve relative paths
            if model_path:
                model_path_obj = Path(model_path)
                # If path is relative, resolve it relative to the parent directory (node-manager root)
                if not model_path_obj.is_absolute():
                    model_path_obj = Path(__file__).parent.parent.parent / model_path
                model_path = str(model_path_obj.resolve())
            
            if not model_path or not Path(model_path).exists():
                return {
                    'success': False,
                    'error': f'Model path not found: {model_path}'
                }
            
            self.logger.info(f"Loading model {model_name} to RAM...")
            self.logger.info(f"Model path: {model_path}")
            self.logger.info(f"Model file size: {Path(model_path).stat().st_size / (1024*1024*1024):.2f} GB")
            
            # Clear any existing RAM staging
            self.clear_ram()
            
            # Import diffusers
            self.logger.info("Importing StableDiffusionXLPipeline...")
            from diffusers.pipelines.stable_diffusion_xl.pipeline_stable_diffusion_xl import StableDiffusionXLPipeline

            # Ensure torch is initialized
            if self.torch is None:
                import torch
                self.torch = torch

            # Get RAM usage before
            before_metrics = self.hardware.get_current_metrics()
            before_ram = before_metrics.get('cpu_ram_mb', 0)
            
            # Load model to CPU/RAM (staging)
            self.logger.info("Starting model loading...")
            with self.torch.no_grad():
                self.model_in_ram = StableDiffusionXLPipeline.from_single_file(
                    model_path,
                    torch_dtype=self.torch.float16,
                    use_safetensors=True,
                    variant="fp8",
                    device_map=None  # Keep on CPU
                )
            
            # Get RAM usage after
            after_metrics = self.hardware.get_current_metrics()
            after_ram = after_metrics.get('cpu_ram_mb', 0)
            duration = time.time() - start_time
            
            self.logger.info(f"Model loaded to RAM in {duration:.2f} seconds!")
            self.logger.info(f"RAM usage: {after_ram - before_ram:.0f} MB")
            
            # Set the current model
            self.current_model = model_name
            self.logger.info(f"Current model set to: {self.current_model}")
            
            return {
                'success': True,
                'model_name': model_name,
                'ram_usage_mb': after_ram - before_ram,
                'duration': duration
            }
        except Exception as e:
            self.logger.error(f"Failed to load model to RAM: {e}")
            self.clear_ram()  # Cleanup on failure
            return {
                'success': False,
                'error': str(e)
            }

    def load_model_from_ram_to_vram(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Load model from RAM to VRAM (DirectML device) with immediate RAM cleanup"""
        start_time = time.time()
        
        try:
            if self.model_in_ram is None:
                return {
                    'success': False,
                    'error': 'No model in RAM to transfer to VRAM'
                }

            if self.torch is None:
                raise RuntimeError("PyTorch is not initialized. Cannot move model to VRAM.")

            self.logger.info("Moving model from RAM to VRAM...")
            
            # Get VRAM usage before
            before_metrics = self.hardware.get_current_metrics()
            before_vram = before_metrics.get('gpu_vram_mb', 0)
            
            # Move to DirectML device with immediate RAM cleanup
            with self.torch.no_grad():
                self.logger.info(f"Moving pipeline to device {self.device}...")
                self.pipeline = self.model_in_ram.to(self.device)
                
                # Immediately clear RAM reference and force cleanup (like old gpu_worker.py)
                del self.model_in_ram
                self.model_in_ram = None
                gc.collect()
                
                self.logger.info("RAM staging cleared after GPU transfer")
              # Enable VRAM optimizations (like old gpu_worker.py)
            self.logger.info("Enabling VRAM optimizations...")
            
            # Enable attention slicing for memory efficiency (but not max to use more VRAM)
            self.pipeline.enable_attention_slicing("auto")  # Changed from "max" to "auto"
            
            # Enable XFormers if available
            try:
                self.pipeline.enable_xformers_memory_efficient_attention()
                self.logger.info("XFormers memory efficient attention enabled")
            except Exception as e:
                self.logger.info(f"XFormers not available: {e}")
            
            # Enable VAE optimizations
            if hasattr(self.pipeline, 'enable_vae_slicing'):
                self.pipeline.enable_vae_slicing()
                self.logger.info("VAE slicing enabled")
            
            if hasattr(self.pipeline, 'enable_vae_tiling'):
                self.pipeline.enable_vae_tiling()
                self.logger.info("VAE tiling enabled")
            
            # Don't enable CPU offload during initial load to use full VRAM
            # This allows the model to use more VRAM for better performance
            self.logger.info("CPU offload disabled for full VRAM utilization")
            
            # Get VRAM usage after
            after_metrics = self.hardware.get_current_metrics()
            after_vram = after_metrics.get('gpu_vram_mb', 0)
            
            # Estimate VRAM usage if hardware metrics don't show it
            vram_usage = after_vram - before_vram
            if vram_usage < 1000:  # If less than 1GB detected, use estimate
                vram_usage = 5500  # SDXL typically uses 4-6GB for FP16
                self.logger.info(f"Using estimated VRAM usage: {vram_usage}MB")
            
            duration = time.time() - start_time
            
            # Final cleanup
            gc.collect()
            
            self.logger.info(f"Model moved to VRAM in {duration:.2f} seconds!")
            self.logger.info(f"VRAM usage: {vram_usage:.0f} MB")
            self.logger.info("RAM staging was immediately cleared")
            
            return {
                'success': True,
                'vram_usage_mb': vram_usage,
                'duration': duration
            }
            
        except Exception as e:
            self.logger.error(f"Failed to move model to VRAM: {e}")
            # Cleanup on failure
            if hasattr(self, 'model_in_ram') and self.model_in_ram is not None:
                del self.model_in_ram
                self.model_in_ram = None
            gc.collect()
            return {
                'success': False,
                'error': str(e)
            }
    
    def clear_ram(self) -> Dict[str, Any]:
        """Clear RAM staging area"""
        try:
            if self.model_in_ram is not None:
                del self.model_in_ram
                self.model_in_ram = None
                self.logger.info("RAM staging cleared")
            
            if self.ram_staging_data is not None:
                del self.ram_staging_data
                self.ram_staging_data = None
            
            # Force garbage collection
            gc.collect()
            
            return {'success': True}
            
        except Exception as e:
            self.logger.error(f"Failed to clear RAM: {e}")
            return {'success': False, 'error': str(e)}
    
    def clear_vram(self) -> Dict[str, Any]:
        """Clear VRAM and reset pipeline"""
        try:
            if self.pipeline is not None:
                del self.pipeline
                self.pipeline = None
                self.logger.info("VRAM cleared")
            
            # Force garbage collection
            gc.collect()
            
            return {'success': True}
            
        except Exception as e:
            self.logger.error(f"Failed to clear VRAM: {e}")
            return {'success': False, 'error': str(e)}
    
    def clean_vram_residuals(self) -> Dict[str, Any]:
        """Comprehensive VRAM cleanup after inference to prevent memory buildup"""
        try:
            if self.torch is None:
                return {'success': False, 'error': 'PyTorch not available'}
            
            # Get VRAM before cleanup
            before_metrics = self.hardware.get_current_metrics()
            before_vram = before_metrics.get('gpu_vram_mb', 0)
            
            self.logger.info("Starting VRAM residual cleanup...")
            
            # Clear CUDA cache (DirectML equivalent)
            if hasattr(self.torch, 'cuda') and hasattr(self.torch.cuda, 'empty_cache'):
                self.torch.cuda.empty_cache()
            
            # Clear any intermediate tensors and cached data
            if self.pipeline is not None:
                # Clear any cached computations in the pipeline
                if hasattr(self.pipeline, 'vae') and hasattr(self.pipeline.vae, 'clear_cache'):
                    self.pipeline.vae.clear_cache()
                
                # Force pipeline to clear any internal caches
                if hasattr(self.pipeline, '_clear_cache'):
                    self.pipeline._clear_cache()
            
            # Force multiple garbage collection cycles
            for _ in range(3):
                gc.collect()
              # Additional DirectML-specific cleanup
            if self.torch_directml is not None:
                try:
                    # DirectML doesn't have empty_cache like CUDA, but we can try other cleanup
                    # Force garbage collection with DirectML context
                    gc.collect()
                except Exception as e:
                    self.logger.debug(f"DirectML cleanup not available: {e}")
            
            # Get VRAM after cleanup
            after_metrics = self.hardware.get_current_metrics()
            after_vram = after_metrics.get('gpu_vram_mb', 0)
            vram_cleaned = before_vram - after_vram
            
            if vram_cleaned > 0:
                self.logger.info(f"VRAM cleanup freed {vram_cleaned} MB")
            else:
                self.logger.debug("VRAM cleanup completed (no change detected)")
            
            return {
                'success': True, 
                'vram_cleaned_mb': max(0, vram_cleaned),
                'before_vram_mb': before_vram,
                'after_vram_mb': after_vram
            }
        except Exception as e:
            self.logger.error(f"VRAM residual cleanup failed: {e}")
            return {'success': False, 'error': str(e)}
    
    def force_vram_cleanup(self) -> Dict[str, Any]:
        """Emergency VRAM cleanup - more aggressive than residual cleanup"""
        try:
            self.logger.warning("Performing emergency VRAM cleanup...")
            
            # Get VRAM before cleanup
            before_metrics = self.hardware.get_current_metrics()
            before_vram = before_metrics.get('gpu_vram_mb', 0)
            
            # Temporarily move model to CPU if needed for emergency cleanup
            if self.pipeline is not None:
                try:
                    # Move components to CPU temporarily to free VRAM
                    self.logger.info("Moving pipeline to CPU for emergency cleanup...")
                    self.pipeline = self.pipeline.to('cpu')
                    gc.collect()
                    
                    # Then move back to DirectML device
                    self.logger.info("Moving pipeline back to DirectML device...")
                    self.pipeline = self.pipeline.to(self.device)
                    
                except Exception as e:
                    self.logger.error(f"Emergency CPU offload failed: {e}")
            
            # Comprehensive cleanup
            for _ in range(5):
                gc.collect()
            
            # Clear any CUDA/DirectML cache
            if self.torch is not None and hasattr(self.torch, 'cuda'):
                try:
                    self.torch.cuda.empty_cache()
                except:
                    pass
            
            # Get VRAM after cleanup
            after_metrics = self.hardware.get_current_metrics()
            after_vram = after_metrics.get('gpu_vram_mb', 0)
            vram_cleaned = before_vram - after_vram
            
            self.logger.info(f"Emergency VRAM cleanup completed - freed {vram_cleaned} MB")
            
            return {
                'success': True,
                'vram_cleaned_mb': max(0, vram_cleaned),
                'before_vram_mb': before_vram,
                'after_vram_mb': after_vram
            }
            
        except Exception as e:
            self.logger.error(f"Emergency VRAM cleanup failed: {e}")
            return {'success': False, 'error': str(e)}

    def enable_aggressive_vram_optimizations(self):
        """Enable aggressive VRAM optimizations for better memory usage"""
        if self.pipeline is None:
            return
        
        try:
            self.logger.info("Enabling aggressive VRAM optimizations...")
            
            # Maximum attention slicing
            self.pipeline.enable_attention_slicing("max")
            
            # Enable all VAE optimizations
            if hasattr(self.pipeline, 'enable_vae_slicing'):
                self.pipeline.enable_vae_slicing()
            if hasattr(self.pipeline, 'enable_vae_tiling'):
                self.pipeline.enable_vae_tiling()
            
            # Enable sequential CPU offload for memory efficiency
            if hasattr(self.pipeline, 'enable_sequential_cpu_offload'):
                self.pipeline.enable_sequential_cpu_offload()
                self.logger.info("Sequential CPU offload enabled")
            
            # Enable model CPU offload if available
            if hasattr(self.pipeline, 'enable_model_cpu_offload'):
                self.pipeline.enable_model_cpu_offload()
                self.logger.info("Model CPU offload enabled")
            
            self.logger.info("Aggressive VRAM optimizations enabled")
            
        except Exception as e:
            self.logger.error(f"Failed to enable aggressive VRAM optimizations: {e}")
    
    def get_current_model(self) -> Optional[str]:
        """Get the name of the currently loaded model"""
        return self.current_model
    
    def get_memory_status(self) -> Dict[str, Any]:
        """Get current memory status"""
        return {
            'current_model': self.current_model,
            'has_ram_model': self.model_in_ram is not None,
            'has_vram_model': self.pipeline is not None,
            'device': self.device
        }
    
    def prepare_vram_for_inference(self, estimated_vae_memory_mb: int = 5000) -> bool:
        """Prepare VRAM by ensuring enough space for both inference and VAE decoding"""
        try:
            current_metrics = self.hardware.get_current_metrics()
            current_vram = current_metrics.get('gpu_vram_mb', 0)
            available_vram = 16000 - current_vram  # Assuming 16GB cards
            
            # Calculate total memory needed (inference + VAE buffer)
            total_needed = estimated_vae_memory_mb
            
            if available_vram < total_needed:
                self.logger.warning(f"Insufficient VRAM for VAE decoding. Available: {available_vram}MB, Needed: {total_needed}MB")
                
                # Enable aggressive optimizations before inference
                self.enable_aggressive_vram_optimizations()
                
                # Force comprehensive cleanup
                self.comprehensive_vram_cleanup()
                
            return True
        except Exception as e:
            self.logger.error(f"VRAM preparation failed: {e}")
            return False

    def prepare_for_vae_decoding(self):
        """Prepare VRAM specifically for VAE decoding by freeing inference artifacts"""
        try:
            self.logger.info("Preparing VRAM for VAE decoding...")
            
            # Clear inference-specific caches
            if hasattr(self.pipeline, 'unet'):
                # Move UNet to CPU temporarily to free VRAM for VAE
                if hasattr(self.pipeline.unet, 'to'):
                    self.pipeline.unet = self.pipeline.unet.to('cpu')
                    self.logger.info("UNet moved to CPU for VAE decoding")
            
            # Clear text encoder caches (not needed during VAE)
            if hasattr(self.pipeline, 'text_encoder'):
                if hasattr(self.pipeline.text_encoder, 'to'):
                    self.pipeline.text_encoder = self.pipeline.text_encoder.to('cpu')
            
            if hasattr(self.pipeline, 'text_encoder_2'):
                if hasattr(self.pipeline.text_encoder_2, 'to'):
                    self.pipeline.text_encoder_2 = self.pipeline.text_encoder_2.to('cpu')
            
            # Force immediate cleanup
            gc.collect()
            
            # Clear DirectML cache
            if self.torch and hasattr(self.torch, 'cuda'):
                try:
                    self.torch.cuda.empty_cache()
                except:
                    pass
                    
            self.logger.info("VRAM prepared for VAE decoding")
            return True
            
        except Exception as e:
            self.logger.error(f"VAE preparation failed: {e}")
            return False

    def restore_after_vae_decoding(self):
        """Restore components to GPU after VAE decoding"""
        try:
            self.logger.info("Restoring components after VAE decoding...")
            
            # Move components back to GPU for next inference
            if hasattr(self.pipeline, 'unet'):
                self.pipeline.unet = self.pipeline.unet.to(self.device)
            
            if hasattr(self.pipeline, 'text_encoder'):
                self.pipeline.text_encoder = self.pipeline.text_encoder.to(self.device)
                
            if hasattr(self.pipeline, 'text_encoder_2'):
                self.pipeline.text_encoder_2 = self.pipeline.text_encoder_2.to(self.device)
            
            self.logger.info("Components restored to GPU")
            
        except Exception as e:
            self.logger.warning(f"Component restoration failed: {e}")

    def comprehensive_vram_cleanup(self) -> Dict[str, Any]:
        """Most thorough VRAM cleanup for maximum memory recovery"""
        try:
            self.logger.info("Starting comprehensive VRAM cleanup...")
            
            before_metrics = self.hardware.get_current_metrics()
            before_vram = before_metrics.get('gpu_vram_mb', 0)
            
            # Step 1: Clear all cached computations
            if self.pipeline is not None:
                # Clear VAE cache
                if hasattr(self.pipeline, 'vae'):
                    if hasattr(self.pipeline.vae, 'clear_cache'):
                        self.pipeline.vae.clear_cache()
                        
                # Clear UNet cache
                if hasattr(self.pipeline, 'unet'):
                    if hasattr(self.pipeline.unet, 'clear_cache'):
                        self.pipeline.unet.clear_cache()
            
            # Step 2: Multiple garbage collection cycles
            for i in range(5):
                gc.collect()
                
            # Step 3: Clear DirectML/CUDA cache
            if self.torch and hasattr(self.torch, 'cuda'):
                try:
                    self.torch.cuda.empty_cache()
                except:
                    pass
            
            # Step 4: Force DirectML cleanup (if available)
            try:
                # Some DirectML implementations have cleanup methods
                if hasattr(self.torch_directml, 'empty_cache'):
                    self.torch_directml.empty_cache()
            except:
                pass
            
            # Step 5: Clear any pipeline internal caches
            if self.pipeline is not None:
                if hasattr(self.pipeline, '_clear_cache'):
                    self.pipeline._clear_cache()
                if hasattr(self.pipeline, 'maybe_free_model_hooks'):
                    self.pipeline.maybe_free_model_hooks()
            
            # Final garbage collection
            for _ in range(3):
                gc.collect()
            
            after_metrics = self.hardware.get_current_metrics()
            after_vram = after_metrics.get('gpu_vram_mb', 0)
            vram_cleaned = before_vram - after_vram
            
            self.logger.info(f"Comprehensive cleanup freed {vram_cleaned} MB VRAM")
            
            return {
                'success': True,
                'vram_cleaned_mb': max(0, vram_cleaned),
                'before_vram_mb': before_vram,
                'after_vram_mb': after_vram
            }
            
        except Exception as e:
            self.logger.error(f"Comprehensive VRAM cleanup failed: {e}")
            return {'success': False, 'error': str(e)}

    def emergency_vram_recovery(self):
        """Emergency VRAM recovery for out-of-memory situations"""
        try:
            self.logger.warning("Performing emergency VRAM recovery...")
            
            # Move entire pipeline to CPU temporarily
            if self.pipeline is not None:
                self.pipeline = self.pipeline.to('cpu')
                
            # Aggressive cleanup
            self.comprehensive_vram_cleanup()
            
            # Wait a moment
            import time
            time.sleep(1)
            
            # Move back to GPU
            if self.pipeline is not None:
                self.pipeline = self.pipeline.to(self.device)
                
            self.logger.info("Emergency VRAM recovery completed")
            
        except Exception as e:
            self.logger.error(f"Emergency VRAM recovery failed: {e}")
