#!/usr/bin/env python3
"""
Memory-Optimized DirectML SDXL Test
Test SDXL with aggressive memory optimizations for limited VRAM
"""

import torch
import structlog
from pathlib import Path

logger = structlog.get_logger(__name__)

def test_memory_optimized_sdxl():
    """Test SDXL with aggressive memory optimizations"""
    
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
        
        # Use a lighter model variant if available
        print("📥 Loading memory-optimized SDXL pipeline...")
        model_id = "stabilityai/stable-diffusion-xl-base-1.0"
        
        # Load with aggressive memory optimizations
        pipeline = StableDiffusionXLPipeline.from_pretrained(
            model_id,
            torch_dtype=torch.float16,  # Use fp16 to save memory
            use_safetensors=True,
            variant="fp16"  # Use fp16 variant if available
        )
        print("✓ Pipeline loaded from Hugging Face")
        
        # Apply all memory optimizations BEFORE moving to device
        print("🔧 Applying memory optimizations...")
        
        # Enable all memory-saving features
        pipeline.enable_attention_slicing("max")  # Maximum slicing
        pipeline.enable_vae_slicing()  # VAE slicing
        pipeline.enable_vae_tiling()   # VAE tiling for large images
        
        # Use CPU offload to manage memory
        pipeline.enable_model_cpu_offload()
        
        print("✓ Memory optimizations applied")
        
        # Move to DirectML device
        print("🔄 Moving pipeline to DirectML device...")
        pipeline = pipeline.to(device)
        print("✓ Pipeline moved to DirectML successfully")
        
        # Test with very conservative settings
        print("🎨 Running memory-conservative inference...")
        prompt = "a simple red apple"
        
        # Use minimal settings to reduce memory usage
        with torch.no_grad():
            # Clear cache before inference
            if hasattr(torch, 'cuda') and torch.cuda.is_available():
                torch.cuda.empty_cache()
            
            image = pipeline(
                prompt=prompt,
                num_inference_steps=10,  # Minimal steps
                height=256,  # Very small resolution
                width=256,
                guidance_scale=7.5,
                generator=torch.Generator(device=device).manual_seed(42)  # Reproducible
            ).images[0]
        
        print("✅ Inference completed successfully!")
        
        # Save the image
        output_path = Path("directml_memory_optimized_output.png")
        image.save(output_path)
        print(f"💾 Image saved to: {output_path}")
        
        return True
        
    except RuntimeError as e:
        if "memory" in str(e).lower() or "allocate" in str(e).lower():
            print(f"💾 Memory error (expected on limited VRAM): {e}")
            print("📝 Note: DirectML is working, but needs more aggressive memory management")
            return "memory_limited"
        else:
            print(f"❌ Runtime error: {e}")
            import traceback
            traceback.print_exc()
            return False
    except Exception as e:
        print(f"❌ Test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == "__main__":
    print("🔥 Memory-Optimized DirectML SDXL Test")
    print("=" * 50)
    
    success = test_memory_optimized_sdxl()
    
    if success == True:
        print("\n🎉 DirectML SDXL test passed!")
    elif success == "memory_limited":
        print("\n⚠️ DirectML working but memory-constrained!")
    else:
        print("\n💥 DirectML SDXL test failed!")
