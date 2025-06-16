#!/usr/bin/env python3
"""
Processing - Inference logic and processing for worker
Handles inference execution with pre-configured settings from node-manager
Includes OpenCLIP support for extended prompt processing (248+ tokens)
"""
import time
import random
from pathlib import Path
from typing import Dict, Any, Optional

# Import OpenCLIP processor
from .openclip_processor import create_openclip_processor, OPENCLIP_AVAILABLE


class Processing:
    """Inference processing and task execution with OpenCLIP support"""
    
    def __init__(self, device_id: int, memory, hardware, logger, config=None):
        self.device_id = device_id
        self.device = f"privateuseone:{device_id}"
        self.memory = memory
        self.hardware = hardware
        self.logger = logger
        self.config = config or {}
        
        # Processing state
        self.current_task = None
        self.is_processing = False
        self.last_result = None
        
        # Output directory
        self.output_dir = Path("outputs")
        self.output_dir.mkdir(exist_ok=True)
        
        # Initialize OpenCLIP processor if enabled
        self.openclip_processor = None
        self.use_openclip = self.config.get('clip_processor', {}).get('type') == 'openclip'
        
        if self.use_openclip and OPENCLIP_AVAILABLE:
            try:
                self.openclip_processor = create_openclip_processor(
                    config=self.config,
                    device=self.device,
                    logger=self.logger
                )
                self.logger.info(f"OpenCLIP processor initialized: {self.openclip_processor.get_model_info()}")
            except Exception as e:
                self.logger.warning(f"Failed to initialize OpenCLIP processor: {e}")
                self.use_openclip = False
        elif self.use_openclip:
            self.logger.warning("OpenCLIP requested but not available - falling back to standard CLIP")
            self.use_openclip = False
        
        self.logger.info(f"Processing engine initialized for GPU {device_id} (OpenCLIP: {self.use_openclip})")
    
    def get_supported_actions(self) -> list:
        """Return list of supported processing actions"""
        return [
            'run_inference',
            'start_inference', 
            'stop_inference',
            'get_inference_status'
        ]

    def validate_action(self, action: str) -> bool:
        """Validate if action is supported"""
        return action in self.get_supported_actions()
    
    def run_inference(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Run inference with pre-configured settings from node-manager"""
        # Ensure consistent task ID handling
        task_id = params.get('task_id')
        if not task_id:
            task_id = f'task_{int(time.time())}'
            params['task_id'] = task_id  # Update params for consistency
            
        config = params.get('config', {})
        
        if self.is_processing:
            return {
                'success': False,
                'error': 'Worker is already processing a task',
                'task_id': task_id
            }
        
        if self.memory.pipeline is None:
            return {
                'success': False,
                'error': 'No model loaded',
                'task_id': task_id
            }
        
        start_time = time.time()
        self.is_processing = True
        self.current_task = task_id
        
        self.logger.log_task_start(task_id, "inference")
        
        try:            # Extract inference parameters (pre-configured by node-manager)
            prompt = config.get("prompt", "")
            negative_prompt = config.get("negative_prompt", "blurry, low quality")
            width = config.get("width", 832)
            height = config.get("height", 1216)
            steps = config.get("steps", 20)
            guidance_scale = config.get("guidance_scale", 7.0)
            seed = config.get("seed")
            
            # Process prompts with OpenCLIP if available and enabled
            processed_prompt = self._preprocess_prompt(prompt)
            processed_negative = self._preprocess_prompt(negative_prompt) if negative_prompt else negative_prompt
            
            # Extract pre-configured optimization settings from node-manager
            attention_slicing = config.get("attention_slicing", "auto")
            vae_tile_size = config.get("vae_tile_size", None)
            scheduler_type = config.get("scheduler_type", "euler_a")
            enable_xformers = config.get("enable_xformers", True)
            cpu_offload = config.get("cpu_offload", False)
            
            if seed is None:
                seed = random.randint(0, 2**32-1)
            
            self.logger.info(f"Generating {width}x{height} image for task {task_id}")
            self.logger.info(f"Prompt: '{prompt[:50]}...'")
            self.logger.info(f"Using pre-configured settings from node-manager")
            
            # Apply the pre-configured optimization settings
            self._apply_inference_settings(
                attention_slicing=attention_slicing,
                vae_tile_size=vae_tile_size,
                scheduler_type=scheduler_type,
                enable_xformers=enable_xformers,
                cpu_offload=cpu_offload,
                width=width,
                height=height
            )
            
            # Set up generator
            import torch
            generator = torch.Generator(device=self.device).manual_seed(seed)
            
            # Pre-generation cleanup (light)
            import gc
            gc.collect()
              # Generate image with error handling for memory issues
            try:
                with torch.no_grad():
                    result = self.memory.pipeline(
                        prompt=processed_prompt,
                        negative_prompt=processed_negative,
                        width=width,
                        height=height,
                        num_inference_steps=steps,
                        guidance_scale=guidance_scale,
                        generator=generator
                    )
                    
                    image = result.images[0]
                    
            except RuntimeError as e:
                if "out of memory" in str(e).lower():
                    self.logger.error("Out of memory during inference")
                    # Emergency memory cleanup
                    self.memory.force_vram_cleanup()
                    raise e
                else:
                    raise e
            
            # Save image
            filename = f"gpu{self.device_id}_{task_id}_{width}x{height}_s{seed}.png"
            output_path = self.output_dir / filename
            image.save(output_path)
            
            # Post-inference cleanup (done by Memory module)
            cleanup_result = self.memory.clean_vram_residuals()
            
            generation_time = time.time() - start_time
            
            result_data = {
                'success': True,
                'task_id': task_id,
                'output_path': str(output_path),
                'generation_time': generation_time,
                'seed': seed,
                'resolution': f"{width}x{height}",
                'settings_applied': {
                    'attention_slicing': attention_slicing,
                    'vae_tile_size': vae_tile_size,
                    'scheduler_type': scheduler_type,
                    'cpu_offload': cpu_offload
                },
                'vram_cleaned_mb': cleanup_result.get('vram_cleaned_mb', 0)
            }
            
            self.last_result = result_data
            self.logger.log_task_complete(task_id, generation_time, True)
            
            return result_data
            
        except Exception as e:
            generation_time = time.time() - start_time
            self.logger.error(f"Inference failed for task {task_id}: {e}")
            
            # Force cleanup on error
            try:
                self.memory.clean_vram_residuals()
            except:
                pass
            
            error_result = {
                'success': False,
                'error': str(e),
                'generation_time': generation_time,
                'task_id': task_id
            }
            
            self.last_result = error_result
            self.logger.log_task_complete(task_id, generation_time, False)
            
            return error_result
            
        finally:
            self.is_processing = False
            self.current_task = None
    
    def _apply_inference_settings(self, attention_slicing: str, vae_tile_size: Optional[int], 
                                scheduler_type: str, enable_xformers: bool, 
                                cpu_offload: bool, width: int, height: int):
        """Apply pre-configured inference settings from node-manager"""
        try:
            pipeline = self.memory.pipeline
            if pipeline is None:
                return
            
            self.logger.info("Applying pre-configured inference settings...")
            
            # 1. Attention Slicing (if different from current)
            self._configure_attention_slicing(attention_slicing, width, height)
            
            # 2. VAE Tiling
            self._configure_vae_tiling(vae_tile_size, width, height)
            
            # 3. Scheduler
            self._configure_scheduler(scheduler_type)
            
            # 4. XFormers
            self._configure_xformers(enable_xformers)
            
            # 5. CPU Offloading
            if cpu_offload:
                self._configure_cpu_offload()
            
            self.logger.info("Inference settings applied successfully")
            
        except Exception as e:
            self.logger.warning(f"Some inference settings failed to apply: {e}")
    
    def _configure_attention_slicing(self, attention_slicing: str, width: int, height: int):
        """Configure attention slicing based on node-manager settings"""
        try:
            pipeline = self.memory.pipeline
            if not attention_slicing or attention_slicing == "None":
                pipeline.disable_attention_slicing()
                return
            
            if attention_slicing == "auto":
                # Let the system decide based on resolution
                total_pixels = width * height
                if total_pixels >= 1536 * 1536:
                    slice_size = 1
                elif total_pixels >= 1024 * 1024:
                    slice_size = 2
                else:
                    slice_size = "max"
            elif attention_slicing == "max":
                slice_size = "max"
            else:
                try:
                    slice_size = int(attention_slicing)
                except ValueError:
                    slice_size = "auto"
            
            if slice_size and pipeline is not None:
                pipeline.enable_attention_slicing(slice_size)
                self.logger.debug(f"Attention slicing: {slice_size}")
                    
        except Exception as e:
            self.logger.warning(f"Attention slicing setup failed: {e}")
    
    def _configure_vae_tiling(self, vae_tile_size: Optional[int], width: int, height: int):
        """Configure VAE tiling based on node-manager settings"""
        try:
            pipeline = self.memory.pipeline
            total_pixels = width * height
            should_enable_tiling = False
            
            if vae_tile_size is not None:
                should_enable_tiling = True
            else:
                # Auto-determine based on resolution
                should_enable_tiling = total_pixels >= 1024 * 1024
            
            if should_enable_tiling and pipeline and hasattr(pipeline, 'vae'):
                if hasattr(pipeline, 'enable_vae_tiling'):
                    pipeline.enable_vae_tiling()
                    self.logger.debug("VAE tiling enabled")
            else:
                if hasattr(pipeline, 'disable_vae_tiling'):
                    pipeline.disable_vae_tiling()
                
        except Exception as e:
            self.logger.warning(f"VAE tiling setup failed: {e}")
    
    def _configure_scheduler(self, scheduler_type: str):
        """Configure scheduler based on node-manager settings"""
        try:
            pipeline = self.memory.pipeline
            if pipeline is None:
                return
                
            from diffusers.schedulers.scheduling_euler_ancestral_discrete import EulerAncestralDiscreteScheduler
            from diffusers.schedulers.scheduling_lms_discrete import LMSDiscreteScheduler
            from diffusers.schedulers.scheduling_ddim import DDIMScheduler
            from diffusers.schedulers.scheduling_dpmsolver_multistep import DPMSolverMultistepScheduler
            from diffusers.schedulers.scheduling_pndm import PNDMScheduler
            
            scheduler_map = {
                "euler_a": EulerAncestralDiscreteScheduler,
                "lms": LMSDiscreteScheduler,
                "ddim": DDIMScheduler,
                "dpm_solver": DPMSolverMultistepScheduler,
                "pndm": PNDMScheduler
            }
            
            if scheduler_type in scheduler_map:
                try:
                    pipeline.scheduler = scheduler_map[scheduler_type].from_config(pipeline.scheduler.config)
                    self.logger.debug(f"Scheduler set to: {scheduler_type}")
                except Exception as e:
                    self.logger.warning(f"Failed to set scheduler {scheduler_type}: {e}")
                    
        except Exception as e:
            self.logger.warning(f"Scheduler configuration failed: {e}")
    
    def _configure_xformers(self, enable_xformers: bool):
        """Configure XFormers based on node-manager settings"""
        try:
            pipeline = self.memory.pipeline
            if pipeline is None:
                return
                
            if enable_xformers:
                try:
                    pipeline.enable_xformers_memory_efficient_attention()
                    self.logger.debug("XFormers enabled")
                except Exception as e:
                    self.logger.debug(f"XFormers not available: {e}")
            else:
                try:
                    pipeline.disable_xformers_memory_efficient_attention()
                    self.logger.debug("XFormers disabled")
                except Exception:
                    pass
                    
        except Exception as e:
            self.logger.warning(f"XFormers configuration failed: {e}")
    
    def _configure_cpu_offload(self):
        """Configure CPU offloading for extreme memory saving"""
        try:
            pipeline = self.memory.pipeline
            if pipeline is None:
                return
                
            # Sequential CPU offload - moves components to CPU when not needed
            if pipeline is not None and hasattr(pipeline, 'enable_sequential_cpu_offload'):
                pipeline.enable_sequential_cpu_offload()
                self.logger.debug("Sequential CPU offload enabled")
            elif pipeline is not None and hasattr(pipeline, 'enable_model_cpu_offload'):
                pipeline.enable_model_cpu_offload()
                self.logger.debug("Model CPU offload enabled")
            else:
                self.logger.debug("CPU offload not available")
                
        except Exception as e:
            self.logger.warning(f"CPU offload configuration failed: {e}")
    
    def start_inference(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Start inference process (same as run_inference for now)"""
        return self.run_inference(params)
    
    def stop_inference(self) -> Dict[str, Any]:
        """Stop current inference process"""
        try:
            if not self.is_processing:
                return {
                    'success': True,
                    'message': 'No inference running'
                }
            
            # Note: This is a simplified stop - in a real implementation,
            # you might need to interrupt the inference pipeline
            self.logger.warning("Inference stop requested")
            
            # For now, just mark as not processing
            # In a real implementation, you'd need to handle pipeline interruption
            self.is_processing = False
            self.current_task = None
            
            return {
                'success': True,
                'message': 'Inference stopped'
            }
            
        except Exception as e:
            self.logger.error(f"Failed to stop inference: {e}")
            return {
                'success': False,
                'error': str(e)
            }
    
    def get_inference_status(self) -> Dict[str, Any]:
        """Get current inference status"""
        return {
            'is_processing': self.is_processing,
            'current_task': self.current_task,
            'last_result': self.last_result,
            'device_id': self.device_id,
            'model_loaded': self.memory.pipeline is not None,
            'current_model': self.memory.get_current_model()
        }
    
    def get_status(self) -> Dict[str, Any]:
        """Get processing status"""
        return self.get_inference_status()
    
    def _preprocess_prompt(self, prompt: str) -> str:
        """
        Preprocess prompts using OpenCLIP if available, otherwise return original
        
        Args:
            prompt: Input text prompt
            
        Returns:
            Processed prompt (may be same as input if no OpenCLIP processing)
        """
        if not prompt or not prompt.strip():
            return prompt
            
        if self.use_openclip and self.openclip_processor:
            try:
                # Get token count estimation
                token_count = len(prompt.split()) * 1.3  # Rough estimation
                
                if token_count > 77:  # Standard CLIP limit
                    self.logger.info(f"Long prompt detected ({token_count:.0f} estimated tokens) - using OpenCLIP")
                    
                    # OpenCLIP can handle longer prompts natively
                    # Just validate the prompt can be processed
                    try:
                        # Test encoding to ensure prompt is valid
                        _ = self.openclip_processor.encode_prompt(prompt, truncate=False)
                        return prompt  # Return original if successful
                    except Exception as e:
                        self.logger.warning(f"OpenCLIP processing failed, using truncated prompt: {e}")
                        # Fallback to truncation
                        return self._truncate_prompt(prompt, 75)  # Leave room for special tokens
                else:
                    # Short enough for standard processing
                    return prompt
                    
            except Exception as e:
                self.logger.warning(f"Prompt preprocessing error: {e}")
                return prompt
        else:
            # No OpenCLIP - check if truncation needed
            token_count = len(prompt.split()) * 1.3
            if token_count > 75:
                self.logger.warning(f"Long prompt detected but OpenCLIP not available - truncating")
                return self._truncate_prompt(prompt, 75)
            return prompt
    
    def _truncate_prompt(self, prompt: str, max_words: int) -> str:
        """
        Truncate prompt to approximate token limit
        
        Args:
            prompt: Input prompt
            max_words: Maximum number of words (rough token approximation)
            
        Returns:
            Truncated prompt
        """
        words = prompt.split()
        if len(words) <= max_words:
            return prompt
            
        # Try to truncate at natural boundaries (commas, periods)
        truncated = " ".join(words[:max_words])
        
        # Find last comma or period before cutoff
        for delimiter in [', ', '. ', '; ']:
            last_delimiter = truncated.rfind(delimiter)
            if last_delimiter > len(truncated) * 0.7:  # At least 70% of content
                return truncated[:last_delimiter]
        
        # No good delimiter found, use word boundary
        return truncated
    
    def get_clip_info(self) -> Dict[str, Any]:
        """
        Get information about CLIP processor configuration
        
        Returns:
            Dictionary with CLIP processor information
        """
        info = {
            "openclip_available": OPENCLIP_AVAILABLE,
            "openclip_enabled": self.use_openclip,
            "fallback_truncation": not self.use_openclip
        }
        
        if self.openclip_processor:
            info.update(self.openclip_processor.get_model_info())
            
        return info
    
    def cleanup_openclip(self):
        """Clean up OpenCLIP resources"""
        if self.openclip_processor:
            self.openclip_processor.cleanup()
            self.openclip_processor = None
    
    def get_supported_actions(self) -> list:
        """Return list of supported processing actions"""
        return [
            'run_inference',
            'start_inference', 
            'stop_inference',
            'get_inference_status'
        ]

    def validate_action(self, action: str) -> bool:
        """Validate if action is supported"""
        return action in self.get_supported_actions()
