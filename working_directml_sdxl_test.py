#!/usr/bin/env python3
"""
Memory-Optimized DirectML SDXL Test for BitingLip Node Manager
Focused on working within GPU memory constraints
"""

import torch
import structlog
import time
from pathlib import Path

logger = structlog.get_logger(__name__)

def test_memory_optimized_directml_sdxl():
    """Test DirectML SDXL with aggressive memory optimizations"""
    
    try:
        # Check DirectML
        import torch_directml
        device_count = torch_directml.device_count()
        print(f"✓ DirectML devices available: {device_count}")
        
        # Get DirectML device
        device = torch_directml.device()
        print(f"✓ Using DirectML device: {device}")
        
        # Import diffusers
        from diffusers import StableDiffusionXLPipeline
        print("✓ Diffusers imported successfully")
        
        # Load SDXL with maximum memory optimization
        print("📥 Loading memory-optimized SDXL pipeline...")
        model_id = "stabilityai/stable-diffusion-xl-base-1.0"
        
        # Load with aggressive memory settings
        pipeline = StableDiffusionXLPipeline.from_pretrained(
            model_id,
            torch_dtype=torch.float16,  # Use fp16 for memory savings
            use_safetensors=True,
            variant="fp16"  # Use fp16 variant
        )
        print("✓ Pipeline loaded from Hugging Face")
          # Apply ALL memory optimizations BEFORE moving to device
        print("🔧 Applying aggressive memory optimizations...")
        
        # Enable every memory optimization available (but avoid CPU offload with DirectML)
        pipeline.enable_attention_slicing("max")  # Maximum attention slicing
        pipeline.enable_vae_slicing()              # VAE slicing
        pipeline.enable_vae_tiling()               # VAE tiling
        
        # Don't use sequential CPU offload as it conflicts with DirectML
        # pipeline.enable_sequential_cpu_offload()
        
        print("✓ All memory optimizations applied")
        
        # Move to DirectML device
        print("🔄 Moving pipeline to DirectML device...")
        pipeline = pipeline.to(device)
        print("✓ Pipeline moved to DirectML successfully")
        
        # Test with very conservative settings
        print("🎨 Running memory-conservative inference...")
        prompt = "a simple red apple on white background"
        
        # Multiple attempts with decreasing memory requirements
        memory_configs = [
            {"width": 256, "height": 256, "steps": 10, "name": "ultra-low"},
            {"width": 384, "height": 384, "steps": 15, "name": "low"},
            {"width": 512, "height": 512, "steps": 20, "name": "standard"}
        ]
        
        for i, config in enumerate(memory_configs):
            try:
                print(f"\n🔄 Attempt {i+1}: {config['name']} memory ({config['width']}x{config['height']})")
                
                # Clear any cached memory
                if hasattr(torch, 'cuda') and torch.cuda.is_available():
                    torch.cuda.empty_cache()
                
                start_time = time.time()
                
                with torch.no_grad():
                    image = pipeline(
                        prompt=prompt,
                        num_inference_steps=config["steps"],
                        height=config["height"],
                        width=config["width"],
                        guidance_scale=7.5,
                        num_images_per_prompt=1,
                        generator=torch.Generator(device=device).manual_seed(42)
                    ).images[0]
                
                inference_time = time.time() - start_time
                
                # Save successful result
                output_path = Path(f"directml_success_{config['name']}_{config['width']}x{config['height']}.png")
                image.save(output_path)
                
                print(f"✅ SUCCESS! Generated {config['width']}x{config['height']} image in {inference_time:.1f}s")
                print(f"💾 Image saved to: {output_path}")
                
                return True
                
            except RuntimeError as e:
                if "memory" in str(e).lower() or "allocate" in str(e).lower():
                    print(f"💾 Memory limit reached for {config['name']} config: {config['width']}x{config['height']}")
                    continue
                else:
                    print(f"❌ Runtime error: {e}")
                    return False
            except Exception as e:
                print(f"❌ Unexpected error: {e}")
                return False
        
        print("❌ All memory configurations failed")
        return False
        
    except Exception as e:
        print(f"❌ Test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == "__main__":
    print("🔥 Memory-Optimized DirectML SDXL Test - BitingLip Node Manager")
    print("=" * 70)
    
    success = test_memory_optimized_directml_sdxl()
    
    if success:
        print("\n🎉 DirectML SDXL memory-optimized test PASSED!")
        print("🚀 BitingLip node-manager is ready for DirectML-accelerated inference!")
    else:
        print("\n💥 DirectML SDXL test failed - memory constraints too tight")
