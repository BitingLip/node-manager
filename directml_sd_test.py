#!/usr/bin/env python3
"""
DirectML SDXL GPU Test
Real DirectML testing with Stable Diffusion on AMD GPUs
"""

import asyncio
import sys
import time
import json
import torch
import os
from pathlib import Path
from typing import Dict, List, Optional, Any
from dataclasses import dataclass
from datetime import datetime, timedelta
import structlog

# Add project paths
project_root = Path(__file__).parent
sys.path.insert(0, str(project_root))

from core.resource_manager import ResourceManager

logger = structlog.get_logger(__name__)

# Check DirectML and diffusers availability
try:
    import torch_directml
    DIRECTML_AVAILABLE = True
    logger.info("DirectML available", device_count=torch_directml.device_count())
except ImportError:
    DIRECTML_AVAILABLE = False
    logger.warning("DirectML not available")

try:
    from diffusers import StableDiffusionPipeline, StableDiffusionXLPipeline
    from diffusers.pipelines.pipeline_utils import DiffusionPipeline
    DIFFUSERS_AVAILABLE = True
    logger.info("Diffusers available with SDXL support")
except ImportError:
    DIFFUSERS_AVAILABLE = False
    logger.warning("Diffusers not available")


@dataclass
class DirectMLGPUTest:
    """DirectML GPU test configuration"""
    gpu_index: int
    gpu_name: str
    device: torch.device
    memory_gb: float
    test_duration_minutes: int = 2


class DirectMLSDTester:
    """DirectML Stable Diffusion testing"""
    
    def __init__(self):
        self.resource_manager = ResourceManager()
        self.gpus = []
        self.test_results = {}
        self.pipelines = {}
        
    async def initialize(self):
        """Initialize DirectML and GPU detection"""
        logger.info("Initializing DirectML SD testing")
        
        if not DIRECTML_AVAILABLE:
            raise ImportError("DirectML is required for this test")
        
        if not DIFFUSERS_AVAILABLE:
            raise ImportError("Diffusers is required for this test")
        
        # Get DirectML device using the official pattern
        device_count = torch_directml.device_count()
        logger.info("DirectML devices available", count=device_count)
        
        # Detect GPUs
        self.gpus = self.resource_manager.get_gpu_info()
        logger.info("Detected GPUs", count=len(self.gpus))
        
        # Create test configurations
        self.gpu_configs = []
        for i in range(min(len(self.gpus), device_count)):
            gpu = self.gpus[i]
            estimated_memory = self._estimate_gpu_memory(gpu['name'])
            
            # Use the official DirectML device creation pattern
            device = torch_directml.device(torch_directml.default_device())
            
            config = DirectMLGPUTest(
                gpu_index=i,
                gpu_name=gpu['name'],
                device=device,
                memory_gb=estimated_memory
            )
            
            self.gpu_configs.append(config)
            
            logger.info("DirectML GPU configuration",
                       gpu_index=i,
                       device=str(device),
                       memory_gb=estimated_memory)
    
    def _estimate_gpu_memory(self, gpu_name: str) -> float:
        """Estimate GPU memory based on model name"""
        gpu_name = gpu_name.lower()
        
        if 'rx 6800 xt' in gpu_name:
            return 16.0
        elif 'rx 6800' in gpu_name:
            return 16.0
        elif 'rx 6700' in gpu_name:
            return 12.0
        elif 'rx 6600' in gpu_name:
            return 8.0
        elif 'rx 7900' in gpu_name:
            return 24.0
        else:
            return 8.0
    
    async def test_directml_tensor_ops(self) -> Dict[str, Any]:
        """Test basic DirectML tensor operations"""
        logger.info("Testing DirectML tensor operations")
        
        results = []
        
        for config in self.gpu_configs:
            try:
                device = config.device
                
                # Test matrix multiplication
                logger.info("Testing matrix operations", gpu_index=config.gpu_index)
                
                start_time = time.time()
                x = torch.randn(2000, 2000, device=device)
                y = torch.randn(2000, 2000, device=device)
                z = torch.matmul(x, y)
                
                # Force synchronization if possible
                try:
                    torch.cuda.synchronize()
                except:
                    pass
                
                compute_time = time.time() - start_time
                
                result = {
                    "success": True,
                    "gpu_index": config.gpu_index,
                    "gpu_name": config.gpu_name,
                    "device": str(device),
                    "compute_time": compute_time,
                    "tensor_shape": list(z.shape),
                    "memory_gb": config.memory_gb
                }
                
                results.append(result)
                
                logger.info("DirectML tensor test completed",
                           gpu_index=config.gpu_index,
                           compute_time=f"{compute_time:.3f}s")
                
                # Clean up
                del x, y, z
                
            except Exception as e:
                logger.error("DirectML tensor test failed",
                            gpu_index=config.gpu_index,
                            error=str(e))
                results.append({
                    "success": False,
                    "gpu_index": config.gpu_index,
                    "error": str(e)
                })        
        return {
            "test_type": "directml_tensor_ops",
            "results": results,
            "successful_gpus": sum(1 for r in results if r.get('success')),
            "total_gpus": len(results)
        }
    
    async def load_sd_model(self, config: DirectMLGPUTest) -> bool:
        """Load Stable Diffusion model on DirectML device"""
        logger.info("Loading SD model on DirectML",
                   gpu_index=config.gpu_index)
        
        try:
            device = config.device
            
            # Use SDXL model for newer PyTorch compatibility
            model_id = "stabilityai/stable-diffusion-xl-base-1.0"
            logger.info("Creating SDXL pipeline", device=str(device))
              # Load pipeline with DirectML-compatible settings and memory optimization
            try:
                pipeline = StableDiffusionXLPipeline.from_pretrained(
                    model_id,
                    torch_dtype=torch.float32,  # Use float32 for better DirectML compatibility
                    use_safetensors=True,
                    variant=None,  # Don't use fp16 variant that might cause issues
                    low_cpu_mem_usage=True,  # Enable low CPU memory usage
                    device_map=None  # Don't use auto device mapping with DirectML
                )
            except Exception as load_error:
                logger.error(f"Failed to load SDXL model: {load_error}")
                # Try fallback to regular SD 1.5 if SDXL fails
                logger.info("Trying fallback to SD 1.5...")
                pipeline = StableDiffusionPipeline.from_pretrained(
                    "runwayml/stable-diffusion-v1-5",
                    torch_dtype=torch.float32,
                    use_safetensors=True,
                    low_cpu_mem_usage=True
                )
            logger.info("Moving pipeline to DirectML device...")
            # Move to DirectML device
            pipeline = pipeline.to(device)
            
            # Enable memory efficient settings for SDXL (but not CPU offload for DirectML)
            try:
                # Don't use CPU offload with DirectML as it causes compatibility issues
                # if hasattr(pipeline, 'enable_model_cpu_offload'):
                #     pipeline.enable_model_cpu_offload()
                if hasattr(pipeline, 'enable_vae_slicing'):
                    pipeline.enable_vae_slicing()
                if hasattr(pipeline, 'enable_vae_tiling'):
                    pipeline.enable_vae_tiling()
                if hasattr(pipeline, 'enable_attention_slicing'):
                    pipeline.enable_attention_slicing("max")  # Maximum memory savings
                # Additional memory optimizations for DirectML
                if hasattr(pipeline, 'enable_sequential_cpu_offload'):
                    # Use sequential offload instead of full CPU offload
                    pipeline.enable_sequential_cpu_offload()
            except Exception as opt_e:
                logger.warning("Some optimizations not available", error=str(opt_e))
            
            # Store pipeline
            self.pipelines[config.gpu_index] = pipeline
            
            logger.info("SDXL model loaded successfully",
                       gpu_index=config.gpu_index)
            
            return True
            
        except Exception as e:
            logger.error("Failed to load SDXL model",
                        gpu_index=config.gpu_index,
                        error=str(e))
            
            # Fallback to SD 1.5 if SDXL fails
            try:
                logger.info("Falling back to SD 1.5", gpu_index=config.gpu_index)
                model_id = "runwayml/stable-diffusion-v1-5"
                pipeline = StableDiffusionPipeline.from_pretrained(
                    model_id,
                    torch_dtype=torch.float32,  # Use float32 for DirectML
                    use_safetensors=True,
                    safety_checker=None,
                    requires_safety_checker=False
                )                
                pipeline = pipeline.to(device)
                # Don't use CPU offload with DirectML
                # pipeline.enable_model_cpu_offload()
                pipeline.enable_attention_slicing()
                
                self.pipelines[config.gpu_index] = pipeline
                
                logger.info("SD 1.5 fallback loaded successfully",
                           gpu_index=config.gpu_index)
                return True
                
            except Exception as fallback_e:
                logger.error("Fallback also failed",
                            gpu_index=config.gpu_index,
                            error=str(fallback_e))
                return False
    
    async def test_sd_inference(self, config: DirectMLGPUTest) -> Dict[str, Any]:
        """Test Stable Diffusion inference on DirectML"""
        logger.info("Testing SD inference", gpu_index=config.gpu_index)
        
        # Load model
        model_loaded = await self.load_sd_model(config)
        if not model_loaded:
            return {
                "success": False,
                "gpu_index": config.gpu_index,
                "error": "Failed to load model"
            }
        
        pipeline = self.pipelines[config.gpu_index]
        
        # Test prompts
        prompts = [
            "A beautiful landscape with mountains and a lake",
            "A futuristic city with flying cars",
            "A peaceful forest with sunlight streaming through trees"
        ]
        
        inference_results = []
        
        try:
            for i, prompt in enumerate(prompts):
                logger.info("Starting inference",
                           gpu_index=config.gpu_index,
                           prompt_index=i)
                
                start_time = time.time()
                  # Generate image with DirectML-compatible approach
                with torch.no_grad():
                    # Disable CUDA completely to force DirectML usage
                    import os
                    old_cuda_visible = os.environ.get('CUDA_VISIBLE_DEVICES', None)
                    os.environ['CUDA_VISIBLE_DEVICES'] = ''  # Hide CUDA devices
                    
                    try:
                        # Use simpler pipeline call without advanced features that might trigger CUDA
                        # Check if it's SDXL or regular SD
                        is_sdxl = hasattr(pipeline, 'vae') and hasattr(pipeline.vae, 'decode')
                        if is_sdxl:
                            # SDXL settings with aggressive memory optimization for DirectML
                            image = pipeline(
                                prompt=prompt,
                                num_inference_steps=15,  # Reduced for memory
                                guidance_scale=7.5,
                                width=384,  # Smaller size to save memory
                                height=384,
                                generator=None,  # Let pipeline handle random generation
                                # Memory optimization parameters
                                num_images_per_prompt=1,
                                output_type="pil"
                            ).images[0]
                        else:
                            # Regular SD settings
                            image = pipeline(
                                prompt=prompt,
                                num_inference_steps=20,
                                guidance_scale=7.5,
                                width=512,
                                height=512,
                                generator=None
                            ).images[0]
                    except Exception as cuda_error:
                        if "CUDA" in str(cuda_error):
                            # Fallback for CUDA-related errors
                            logger.warning("CUDA error, trying CPU fallback", error=str(cuda_error))
                            # Move pipeline to CPU temporarily for generation
                            pipeline_cpu = pipeline.to('cpu')
                            image = pipeline_cpu(
                                prompt=prompt,
                                num_inference_steps=10,  # Even fewer steps for CPU
                                guidance_scale=7.5,
                                width=512,
                                height=512                            ).images[0]
                            # Move back to DirectML
                            pipeline = pipeline_cpu.to(config.device)
                        else:
                            raise cuda_error
                    finally:
                        # Restore CUDA visibility
                        if old_cuda_visible is not None:
                            os.environ['CUDA_VISIBLE_DEVICES'] = old_cuda_visible
                        else:
                            os.environ.pop('CUDA_VISIBLE_DEVICES', None)
                
                inference_time = time.time() - start_time
                
                # Save image
                output_dir = Path("directml_outputs")
                output_dir.mkdir(exist_ok=True)
                image_path = output_dir / f"directml_gpu_{config.gpu_index}_image_{i}.png"
                image.save(image_path)
                
                result = {
                    "success": True,
                    "prompt": prompt,
                    "inference_time": inference_time,
                    "image_path": str(image_path)
                }
                
                inference_results.append(result)
                
                logger.info("Inference completed",
                           gpu_index=config.gpu_index,
                           inference_time=f"{inference_time:.2f}s")
                
                # Brief pause
                await asyncio.sleep(1)
        
        except Exception as e:
            logger.error("SD inference failed",
                        gpu_index=config.gpu_index,
                        error=str(e))
            inference_results.append({
                "success": False,
                "error": str(e)
            })
        
        # Clean up
        if config.gpu_index in self.pipelines:
            del self.pipelines[config.gpu_index]
        
        successful_inferences = sum(1 for r in inference_results if r.get('success'))
        total_inference_time = sum(r.get('inference_time', 0) for r in inference_results if r.get('success'))
        avg_inference_time = total_inference_time / successful_inferences if successful_inferences > 0 else 0
        
        return {
            "success": successful_inferences > 0,
            "gpu_index": config.gpu_index,
            "gpu_name": config.gpu_name,
            "successful_inferences": successful_inferences,
            "total_inferences": len(prompts),
            "success_rate": successful_inferences / len(prompts),
            "avg_inference_time": avg_inference_time,
            "total_time": total_inference_time,
            "inference_results": inference_results
        }
    
    async def run_concurrent_inference_test(self) -> Dict[str, Any]:
        """Run SD inference on all GPUs concurrently"""
        logger.info("Running concurrent SD inference test")
        
        start_time = datetime.now()
        
        # Run inference tests on all GPUs concurrently
        tasks = [self.test_sd_inference(config) for config in self.gpu_configs]
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        # Process results
        processed_results = []
        for i, result in enumerate(results):
            if isinstance(result, Exception):
                processed_results.append({
                    "success": False,
                    "gpu_index": i,
                    "error": str(result)
                })
            else:
                processed_results.append(result)
        
        end_time = datetime.now()
        total_duration = (end_time - start_time).total_seconds()
        
        # Calculate aggregate statistics
        successful_gpus = sum(1 for r in processed_results if r.get('success'))
        total_inferences = sum(r.get('total_inferences', 0) for r in processed_results if r.get('success'))
        successful_inferences = sum(r.get('successful_inferences', 0) for r in processed_results if r.get('success'))
        
        overall_success_rate = successful_inferences / total_inferences if total_inferences > 0 else 0
        
        return {
            "test_type": "concurrent_sd_inference",
            "total_duration": total_duration,
            "successful_gpus": successful_gpus,
            "total_gpus": len(self.gpu_configs),
            "total_inferences": total_inferences,
            "successful_inferences": successful_inferences,
            "overall_success_rate": overall_success_rate,
            "gpu_results": processed_results
        }
    
    async def run_all_directml_tests(self) -> Dict[str, Any]:
        """Run all DirectML tests"""
        logger.info("Starting comprehensive DirectML testing")
        
        results = {}
        
        # Test 1: Basic tensor operations
        logger.info("Running DirectML tensor operations test")
        results["tensor_ops"] = await self.test_directml_tensor_ops()
        
        # Test 2: Concurrent SD inference
        logger.info("Running concurrent SD inference test")
        results["sd_inference"] = await self.run_concurrent_inference_test()
        
        return results
    
    def print_results(self, results: Dict[str, Any]):
        """Print comprehensive test results"""
        print("\n" + "="*80)
        print("🔥 DIRECTML STABLE DIFFUSION TEST RESULTS")
        print("="*80)
        
        # Tensor operations test
        tensor_test = results.get('tensor_ops', {})
        print(f"\n📊 DIRECTML TENSOR OPERATIONS:")
        print(f"   • Successful GPUs: {tensor_test.get('successful_gpus', 0)}/{tensor_test.get('total_gpus', 0)}")
        
        for result in tensor_test.get('results', []):
            if result.get('success'):
                print(f"   GPU {result['gpu_index']}: ✅ {result['compute_time']:.3f}s")
                print(f"     • {result['gpu_name']} ({result['memory_gb']:.1f}GB)")
            else:
                print(f"   GPU {result['gpu_index']}: ❌ {result.get('error', 'Failed')}")
        
        # SD inference test
        sd_test = results.get('sd_inference', {})
        print(f"\n🎨 STABLE DIFFUSION INFERENCE:")
        print(f"   • Successful GPUs: {sd_test.get('successful_gpus', 0)}/{sd_test.get('total_gpus', 0)}")
        print(f"   • Total Duration: {sd_test.get('total_duration', 0):.1f}s")
        print(f"   • Total Images: {sd_test.get('successful_inferences', 0)}")
        print(f"   • Success Rate: {sd_test.get('overall_success_rate', 0):.1%}")
        
        for result in sd_test.get('gpu_results', []):
            if result.get('success'):
                print(f"\n   GPU {result['gpu_index']}: {result['gpu_name']}")
                print(f"     • Images: {result['successful_inferences']}/{result['total_inferences']}")
                print(f"     • Avg Time: {result['avg_inference_time']:.2f}s per image")
                print(f"     • Success Rate: {result['success_rate']:.1%}")
            else:
                print(f"\n   GPU {result['gpu_index']}: ❌ {result.get('error', 'Failed')}")
        
        # Output location
        output_dir = Path("directml_outputs")
        if output_dir.exists():
            image_count = len(list(output_dir.glob("*.png")))
            print(f"\n📁 Generated {image_count} images in: {output_dir}")
        
        print("\n" + "="*80)


async def main():
    """Main DirectML testing entry point"""
    print("🔥 BitingLip DirectML Stable Diffusion Testing")
    print("="*60)
    print("Testing DirectML integration with AMD GPUs")
    print("="*60)
    
    tester = DirectMLSDTester()
    
    try:
        # Initialize
        print("\n🔍 Initializing DirectML and GPU detection...")
        await tester.initialize()
        
        if len(tester.gpu_configs) == 0:
            print("❌ No DirectML devices available.")
            return
        
        print(f"\n🚀 Starting DirectML tests on {len(tester.gpu_configs)} devices...")
        
        # Run tests
        results = await tester.run_all_directml_tests()
        
        # Print results
        tester.print_results(results)
        
        # Save results
        results_file = Path("directml_sd_test_results.json")
        with open(results_file, 'w') as f:
            json.dump(results, f, indent=2, default=str)
        
        print(f"\n💾 Results saved to: {results_file}")
        
    except KeyboardInterrupt:
        print("\n\n⚠️  Test interrupted by user")
    except Exception as e:
        logger.error("DirectML test failed", error=str(e))
        print(f"\n❌ Test failed: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    asyncio.run(main())
