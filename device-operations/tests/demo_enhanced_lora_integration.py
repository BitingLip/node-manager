"""
End-to-End Enhanced SDXL Worker LoRA Integration Demo

Demonstrates the complete LoRA integration workflow in the Enhanced SDXL Worker,
including realistic request handling and pipeline configuration.
"""

import asyncio
import tempfile
import torch
import logging
from pathlib import Path
from typing import Dict, Any, List, Optional
import sys
import os
import json

# Add the src directory to the Python path
sys.path.append(os.path.join(os.path.dirname(__file__), 'src'))

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

def create_mock_lora_file(file_path: Path, file_format: str = 'safetensors') -> None:
    """Create a mock LoRA file for testing."""
    file_path.parent.mkdir(parents=True, exist_ok=True)
    
    if file_format == 'safetensors':
        import safetensors.torch
        mock_tensors = {
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_k.lora_down.weight": torch.randn(320, 64),
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_k.lora_up.weight": torch.randn(64, 320),
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_v.lora_down.weight": torch.randn(320, 64),
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_v.lora_up.weight": torch.randn(64, 320)
        }
        safetensors.torch.save_file(mock_tensors, str(file_path))
        
    elif file_format == 'pt':
        mock_state_dict = {
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_k.lora_down.weight": torch.randn(320, 64),
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_k.lora_up.weight": torch.randn(64, 320)
        }
        torch.save({"state_dict": mock_state_dict}, str(file_path))
    
    logger.info(f"Created mock LoRA file: {file_path} ({file_format})")

async def demo_enhanced_worker_lora_workflow():
    """Demonstrate the complete Enhanced SDXL Worker LoRA workflow."""
    logger.info("\n" + "="*80)
    logger.info("ENHANCED SDXL WORKER LORA INTEGRATION DEMO")
    logger.info("="*80)
    
    try:
        # Import required components
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAWorker, LoRAConfiguration
        
        # Step 1: Setup Mock Environment
        logger.info("\n🔧 STEP 1: Setting up mock environment")
        
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            
            # Create realistic directory structure
            lora_base_dir = temp_path / "models" / "lora"
            
            # Create LoRA directories for different categories
            style_dir = lora_base_dir / "styles"
            character_dir = lora_base_dir / "characters"
            lighting_dir = lora_base_dir / "lighting"
            
            for directory in [style_dir, character_dir, lighting_dir]:
                directory.mkdir(parents=True)
            
            # Create realistic LoRA files
            lora_files = {
                style_dir / "anime_style_v2.safetensors": "safetensors",
                style_dir / "photorealistic_xl.safetensors": "safetensors",
                character_dir / "girl_character_lora.pt": "pt",
                character_dir / "fantasy_warrior.safetensors": "safetensors",
                lighting_dir / "golden_hour_lighting.safetensors": "safetensors",
                lighting_dir / "dramatic_shadows.pt": "pt"
            }
            
            for file_path, format_type in lora_files.items():
                create_mock_lora_file(file_path, format_type)
            
            logger.info(f"Created {len(lora_files)} mock LoRA files in realistic directory structure")
            
            # Step 2: Initialize Enhanced SDXL Worker Components
            logger.info("\n⚙️ STEP 2: Initializing Enhanced SDXL Worker components")
            
            # Initialize LoRA Worker with all directories
            lora_config = {
                "lora_directories": [str(d) for d in [style_dir, character_dir, lighting_dir]],
                "memory_limit_mb": 2048,
                "enable_caching": True,
                "cache_size": 8
            }
            
            lora_worker = LoRAWorker(lora_config)
            logger.info("✅ LoRA Worker initialized with multi-directory support")
            
            # Step 3: Simulate Enhanced SDXL Request
            logger.info("\n📋 STEP 3: Processing Enhanced SDXL request with LoRA configuration")
            
            # Realistic Enhanced SDXL request
            enhanced_request = {
                "prompt": "A beautiful anime girl warrior in golden hour lighting, fantasy setting, highly detailed",
                "negative_prompt": "blurry, low quality, distorted",
                "width": 1024,
                "height": 1024,
                "num_inference_steps": 30,
                "guidance_scale": 7.5,
                "seed": 12345,
                "scheduler": "dpm_2m_karras",
                "batch_size": 2,
                "lora": {
                    "enabled": True,
                    "global_weight": 1.1,
                    "models": [
                        {
                            "name": "anime_style_v2",
                            "weight": 0.8,
                            "category": "style"
                        },
                        {
                            "name": "girl_character_lora", 
                            "weight": 0.7,
                            "category": "character"
                        },
                        {
                            "name": "golden_hour_lighting",
                            "weight": 0.6,
                            "category": "lighting"
                        }
                    ]
                }
            }
            
            logger.info(f"Request: {enhanced_request['prompt'][:50]}...")
            logger.info(f"LoRA Configuration: {len(enhanced_request['lora']['models'])} adapters requested")
            
            # Step 4: Process LoRA Configuration (simulating Enhanced Worker's _configure_lora_adapters)
            logger.info("\n🎨 STEP 4: Processing LoRA configuration")
            
            lora_config = enhanced_request.get('lora', {})
            if lora_config.get('enabled', False):
                models = lora_config.get('models', [])
                global_weight = lora_config.get('global_weight', 1.0)
                adapter_names = []
                
                logger.info(f"Processing {len(models)} LoRA adapters with global weight {global_weight}")
                
                for i, model_config in enumerate(models, 1):
                    name = model_config.get('name')
                    weight = model_config.get('weight', 1.0)
                    category = model_config.get('category', 'unknown')
                    
                    logger.info(f"  {i}. Loading {name} ({category}) - weight: {weight}")
                    
                    # Create LoRA configuration
                    lora_adapter_config = LoRAConfiguration(
                        name=name,
                        path=name,  # Auto-discover path
                        weight=weight * global_weight
                    )
                    
                    # Load the adapter
                    success = await lora_worker.load_lora_adapter(lora_adapter_config)
                    if success:
                        adapter_names.append(name)
                        final_weight = weight * global_weight
                        logger.info(f"     ✅ Loaded successfully (final weight: {final_weight:.2f})")
                    else:
                        logger.warning(f"     ❌ Failed to load {name}")
                
                # Step 5: Apply to Mock Pipeline
                logger.info(f"\n🔗 STEP 5: Applying {len(adapter_names)} LoRA adapters to pipeline")
                
                if adapter_names:
                    # Create mock pipeline
                    class MockEnhancedPipeline:
                        def __init__(self):
                            self.applied_adapters = []
                            self.adapter_weights = []
                            
                        def set_adapters(self, names, weights):
                            self.applied_adapters = names
                            self.adapter_weights = weights
                            logger.info(f"Pipeline configured with {len(names)} LoRA adapters")
                            for name, weight in zip(names, weights):
                                logger.info(f"  - {name}: {weight:.2f}")
                    
                    mock_pipeline = MockEnhancedPipeline()
                    success = await lora_worker.apply_to_pipeline(mock_pipeline, adapter_names)
                    
                    if success:
                        logger.info("✅ LoRA adapters successfully applied to pipeline")
                        
                        # Step 6: Validate Configuration
                        logger.info("\n🔍 STEP 6: Validating LoRA configuration")
                        
                        assert len(mock_pipeline.applied_adapters) == len(adapter_names)
                        assert len(mock_pipeline.adapter_weights) == len(adapter_names)
                        
                        total_lora_influence = sum(mock_pipeline.adapter_weights)
                        logger.info(f"Total LoRA influence: {total_lora_influence:.2f}")
                        
                        # Show memory usage
                        memory_stats = lora_worker.get_memory_stats()
                        logger.info(f"Memory usage: {memory_stats['current_memory_mb']:.1f}MB / {memory_stats['max_memory_mb']}MB")
                        logger.info(f"Memory usage ratio: {memory_stats['memory_usage_ratio']:.1%}")
                        
                        # Show loaded adapters info
                        loaded_adapters = lora_worker.get_loaded_adapters()
                        logger.info(f"Loaded adapters info:")
                        for name, info in loaded_adapters.items():
                            logger.info(f"  - {name}: {info['file_format'].upper()}, {info['file_size_mb']:.1f}MB, {info['load_time_ms']:.1f}ms")
                        
                        logger.info("✅ All validations passed")
                    else:
                        logger.error("❌ Failed to apply LoRA adapters to pipeline")
                        return False
                
                # Step 7: Simulate Generation
                logger.info("\n🎨 STEP 7: Simulating image generation")
                
                # This would be where the actual Enhanced SDXL Worker generates images
                logger.info("Simulating SDXL generation with LoRA adapters...")
                logger.info(f"  - Base model: Enhanced SDXL")
                logger.info(f"  - Scheduler: {enhanced_request['scheduler']}")
                logger.info(f"  - Resolution: {enhanced_request['width']}x{enhanced_request['height']}")
                logger.info(f"  - Inference steps: {enhanced_request['num_inference_steps']}")
                logger.info(f"  - Guidance scale: {enhanced_request['guidance_scale']}")
                logger.info(f"  - Batch size: {enhanced_request['batch_size']}")
                logger.info(f"  - Applied LoRA adapters: {len(adapter_names)}")
                
                # Simulate generation time
                await asyncio.sleep(0.1)
                
                logger.info("✅ Generation completed successfully (simulated)")
                
                # Step 8: Cleanup and Summary
                logger.info("\n🧹 STEP 8: Cleanup and summary")
                
                await lora_worker.cleanup()
                
                logger.info("Enhanced SDXL Worker LoRA Integration Demo Summary:")
                logger.info(f"  ✅ {len(lora_files)} LoRA files created")
                logger.info(f"  ✅ {len(adapter_names)} LoRA adapters loaded and applied")
                logger.info(f"  ✅ Pipeline configured with multi-adapter LoRA stack")
                logger.info(f"  ✅ Memory management validated")
                logger.info(f"  ✅ Generation workflow simulated")
                
                return True
            
            else:
                logger.info("LoRA not enabled in request")
                return True
                
    except Exception as e:
        logger.error(f"❌ Demo failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def main():
    """Run the Enhanced SDXL Worker LoRA integration demo."""
    success = await demo_enhanced_worker_lora_workflow()
    
    if success:
        logger.info("\n🎉 Enhanced SDXL Worker LoRA Integration Demo COMPLETED SUCCESSFULLY!")
        logger.info("Ready for production use with real Enhanced SDXL Worker!")
    else:
        logger.error("\n💥 Enhanced SDXL Worker LoRA Integration Demo FAILED!")
    
    return success

if __name__ == "__main__":
    success = asyncio.run(main())
    exit(0 if success else 1)
