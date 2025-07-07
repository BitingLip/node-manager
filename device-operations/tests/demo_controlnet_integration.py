"""
ControlNet Worker Integration Demo

This demo showcases the complete ControlNet Worker integration with Enhanced SDXL Worker,
demonstrating various configuration formats, condition processing, and multi-ControlNet stacking.
"""

import asyncio
import tempfile
import logging
from pathlib import Path
from PIL import Image
import numpy as np
import sys
import os

# Add the src directory to the Python path
sys.path.append(os.path.join(os.path.dirname(__file__), 'src'))

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

def create_demo_image(size: tuple = (512, 512), pattern: str = "edge") -> Image.Image:
    """Create demo images with different patterns for ControlNet testing."""
    image_array = np.zeros((*size, 3), dtype=np.uint8)
    
    if pattern == "edge":
        # Create edge-like patterns for Canny ControlNet
        image_array[100:110, :, :] = 255  # Horizontal line
        image_array[:, 250:260, :] = 255  # Vertical line
        # Add diagonal lines
        for i in range(min(size)):
            if i < size[0] and i < size[1]:
                image_array[i, i, :] = 255
        
    elif pattern == "depth":
        # Create depth-like gradient for Depth ControlNet
        for y in range(size[0]):
            intensity = int((y / size[0]) * 255)
            image_array[y, :, :] = intensity
    
    elif pattern == "pose":
        # Create simple pose-like structure
        # Head (circle)
        center_x, center_y = size[1] // 2, size[0] // 4
        for y in range(size[0]):
            for x in range(size[1]):
                if (x - center_x) ** 2 + (y - center_y) ** 2 < 30 ** 2:
                    image_array[y, x, :] = 255
        
        # Body (line)
        body_start = size[0] // 4 + 30
        body_end = size[0] // 2 + 50
        image_array[body_start:body_end, center_x-5:center_x+5, :] = 255
    
    return Image.fromarray(image_array)

async def demo_controlnet_worker():
    """Demonstrate ControlNet Worker capabilities."""
    logger.info("🎬 Starting ControlNet Worker Integration Demo")
    logger.info("=" * 60)
    
    # Import ControlNet components
    sys.path.append(os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
    from controlnet_worker import ControlNetWorker, ControlNetConfiguration, ControlNetStackConfiguration
    
    # Demo 1: Basic ControlNet Worker
    logger.info("\n📋 Demo 1: Basic ControlNet Worker Initialization")
    config = {
        "memory_limit_mb": 1024,
        "enable_caching": True,
        "enable_memory_monitoring": True
    }
    
    worker = ControlNetWorker(config)
    success = await worker.initialize()
    logger.info(f"✅ ControlNet Worker initialized: {success}")
    
    # Demo 2: Single ControlNet Configuration
    logger.info("\n📋 Demo 2: Single ControlNet Model Loading")
    canny_config = ControlNetConfiguration(
        name="demo_canny",
        type="canny",
        model_path="diffusers/controlnet-canny-sdxl-1.0",
        conditioning_scale=0.8,
        control_guidance_start=0.1,
        control_guidance_end=0.9
    )
    
    logger.info(f"Loading ControlNet: {canny_config.name} ({canny_config.type})")
    success = await worker.load_controlnet_model(canny_config)
    logger.info(f"✅ Model loading result: {success}")
    
    if success:
        stats = worker.get_performance_stats()
        logger.info(f"📊 Performance Stats:")
        logger.info(f"   - Loaded ControlNets: {stats['loaded_controlnets']}")
        logger.info(f"   - Memory Usage: {stats['memory_usage_mb']:.1f}MB")
        logger.info(f"   - Average Load Time: {stats['avg_load_time_ms']:.1f}ms")
    
    # Demo 3: Condition Image Processing
    logger.info("\n📋 Demo 3: Condition Image Processing")
    
    # Create demo condition images
    edge_image = create_demo_image(pattern="edge")
    depth_image = create_demo_image(pattern="depth")
    pose_image = create_demo_image(pattern="pose")
    
    # Save to temporary files
    temp_files = []
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        # Save demo images
        edge_path = temp_path / "edge_demo.png"
        depth_path = temp_path / "depth_demo.png" 
        pose_path = temp_path / "pose_demo.png"
        
        edge_image.save(edge_path)
        depth_image.save(depth_path)
        pose_image.save(pose_path)
        
        # Test condition processing
        condition_configs = [
            ("Canny Edge", ControlNetConfiguration("edge_control", "canny", "test", condition_image=str(edge_path))),
            ("Depth Map", ControlNetConfiguration("depth_control", "depth", "test", condition_image=str(depth_path))),
            ("Pose Structure", ControlNetConfiguration("pose_control", "pose", "test", condition_image=str(pose_path)))
        ]
        
        for name, config in condition_configs:
            try:
                processed = await worker.process_condition_image(config)
                if processed:
                    logger.info(f"✅ {name}: Processed {config.condition_image} -> {processed.size}")
                else:
                    logger.info(f"⚠️ {name}: Processing skipped (dependencies missing)")
            except Exception as e:
                logger.warning(f"⚠️ {name}: Processing failed - {e}")
    
    # Demo 4: Multi-ControlNet Stack
    logger.info("\n📋 Demo 4: Multi-ControlNet Stack Configuration")
    
    stack_config = ControlNetStackConfiguration(max_adapters=3)
    
    # Add multiple adapters with different configurations
    adapters = [
        ControlNetConfiguration("stack_canny", "canny", "diffusers/controlnet-canny-sdxl-1.0", conditioning_scale=0.8),
        ControlNetConfiguration("stack_depth", "depth", "diffusers/controlnet-depth-sdxl-1.0", conditioning_scale=0.6),
    ]
    
    for adapter in adapters:
        success = stack_config.add_adapter(adapter)
        logger.info(f"✅ Added to stack: {adapter.name} ({adapter.type}) - Success: {success}")
    
    # Prepare the stack
    logger.info(f"Preparing ControlNet stack with {len(stack_config.adapters)} adapters...")
    try:
        success = await worker.prepare_controlnet_stack(stack_config)
        logger.info(f"✅ Stack preparation result: {success}")
        
        if success:
            final_stats = worker.get_performance_stats()
            logger.info(f"📊 Final Stack Stats:")
            logger.info(f"   - Current Stack Size: {final_stats['current_stack_size']}")
            logger.info(f"   - Total Loaded ControlNets: {final_stats['loaded_controlnets']}")
            logger.info(f"   - Total Memory Usage: {final_stats['memory_usage_mb']:.1f}MB")
    except Exception as e:
        logger.warning(f"⚠️ Stack preparation failed (expected for missing models): {e}")
    
    # Demo 5: Enhanced SDXL Worker Integration
    logger.info("\n📋 Demo 5: Enhanced SDXL Worker Integration")
    
    # Mock Enhanced SDXL Worker integration
    class MockEnhancedSDXLWorker:
        def __init__(self):
            self.controlnet_worker = worker
        
        async def configure_controlnet_demo(self, config):
            """Demo of Enhanced SDXL Worker ControlNet configuration."""
            
            # String format
            if isinstance(config, str):
                logger.info(f"   Processing string config: {config}")
                parts = config.split(':')
                controlnet_type = parts[0]
                scale = float(parts[1]) if len(parts) > 1 else 1.0
                logger.info(f"   Parsed: type={controlnet_type}, scale={scale}")
                return True
            
            # Object format
            elif isinstance(config, dict):
                logger.info(f"   Processing object config: {config.get('type', 'unknown')}")
                logger.info(f"   Scale: {config.get('conditioning_scale', 1.0)}")
                return True
            
            # List format
            elif isinstance(config, list):
                logger.info(f"   Processing list config with {len(config)} adapters")
                for i, item in enumerate(config):
                    logger.info(f"   Adapter {i}: {item}")
                return True
            
            return False
    
    mock_worker = MockEnhancedSDXLWorker()
    
    # Test different configuration formats
    test_configs = [
        ("String Format", "canny:0.8"),
        ("Object Format", {"type": "depth", "conditioning_scale": 0.6, "enabled": True}),
        ("List Format", ["canny:0.8", {"type": "depth", "conditioning_scale": 0.6}, "pose"])
    ]
    
    for name, config in test_configs:
        logger.info(f"Testing {name}:")
        success = await mock_worker.configure_controlnet_demo(config)
        logger.info(f"✅ {name} processing: {success}")
    
    # Demo 6: Performance Summary
    logger.info("\n📋 Demo 6: Performance Summary")
    
    final_performance = worker.get_performance_stats()
    logger.info("📊 Final Performance Report:")
    logger.info(f"   🔧 Supported ControlNet Types: {len(final_performance['supported_types'])}")
    logger.info(f"   📦 Total Model Loads: {final_performance['total_loads']}")
    logger.info(f"   ⚡ Cache Hits: {final_performance['cache_hits']}")
    logger.info(f"   💾 Current Memory Usage: {final_performance['memory_usage_mb']:.1f}MB")
    logger.info(f"   ⏱️ Average Load Time: {final_performance['avg_load_time_ms']:.1f}ms")
    logger.info(f"   🖼️ Processed Conditions: {final_performance['processed_conditions']}")
    
    logger.info(f"\n🔧 Supported ControlNet Types:")
    for controlnet_type in final_performance['supported_types']:
        logger.info(f"   - {controlnet_type}")
    
    # Cleanup
    logger.info("\n🧹 Cleanup")
    await worker.cleanup()
    logger.info("✅ ControlNet Worker cleanup completed")
    
    logger.info("\n" + "=" * 60)
    logger.info("🎉 ControlNet Worker Integration Demo COMPLETED SUCCESSFULLY!")
    logger.info("=" * 60)
    
    return True

async def main():
    """Run the ControlNet Worker integration demo."""
    try:
        success = await demo_controlnet_worker()
        if success:
            logger.info("\n✅ Demo completed successfully!")
            return True
        else:
            logger.error("\n❌ Demo failed!")
            return False
    except Exception as e:
        logger.error(f"\n💥 Demo error: {e}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == "__main__":
    success = asyncio.run(main())
    exit(0 if success else 1)
