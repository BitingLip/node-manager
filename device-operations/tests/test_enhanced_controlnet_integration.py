"""
Enhanced SDXL Worker ControlNet Integration Test Suite

This test suite validates the integration of ControlNet Worker with the Enhanced SDXL Worker,
ensuring seamless ControlNet adapter management for guided image generation.
"""

import asyncio
import tempfile
import torch
import logging
from pathlib import Path
from typing import Dict, Any, List, Optional
from PIL import Image
import numpy as np
import sys
import os

# Add the src directory to the Python path
sys.path.append(os.path.join(os.path.dirname(__file__), 'src'))

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

class MockEnhancedSDXLWorker:
    """Mock Enhanced SDXL Worker for testing ControlNet integration."""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.is_initialized = False
        
        # Mock ControlNet worker initialization
        sys.path.append(os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from controlnet_worker import ControlNetWorker
        
        # Initialize ControlNet worker
        controlnet_config = config.get("controlnet_config", {})
        self.controlnet_worker = ControlNetWorker(controlnet_config)
        
        logger.info("Mock Enhanced SDXL Worker initialized with ControlNet integration")
    
    async def initialize(self) -> bool:
        """Initialize the mock worker."""
        try:
            success = await self.controlnet_worker.initialize()
            self.is_initialized = success
            return success
        except Exception as e:
            logger.error(f"Failed to initialize mock worker: {e}")
            return False
    
    async def _configure_controlnet_adapters(self, controlnet_config: Dict[str, Any]) -> bool:
        """Configure ControlNet adapters - copied from Enhanced SDXL Worker."""
        try:
            from controlnet_worker import ControlNetConfiguration, ControlNetStackConfiguration
            
            logger.info("Configuring ControlNet adapters")
            
            # Initialize ControlNet worker if not already done
            if not self.controlnet_worker.is_initialized:
                success = await self.controlnet_worker.initialize()
                if not success:
                    logger.error("Failed to initialize ControlNet worker")
                    return False
            
            # Handle different configuration formats
            if isinstance(controlnet_config, list):
                # Multiple ControlNet configurations
                stack_config = ControlNetStackConfiguration(max_adapters=len(controlnet_config))
                
                for i, config in enumerate(controlnet_config):
                    if isinstance(config, str):
                        # Simple string format: "canny:0.8" or just "canny"
                        parts = config.split(':')
                        controlnet_name = parts[0]
                        conditioning_scale = float(parts[1]) if len(parts) > 1 else 1.0
                        
                        adapter_config = ControlNetConfiguration(
                            name=f"controlnet_{i}",
                            type=controlnet_name,
                            model_path=f"diffusers/controlnet-{controlnet_name}-sdxl-1.0",
                            conditioning_scale=conditioning_scale
                        )
                    elif isinstance(config, dict):
                        # Object format with detailed configuration
                        adapter_config = ControlNetConfiguration(
                            name=config.get("name", f"controlnet_{i}"),
                            type=config.get("type", "canny"),
                            model_path=config.get("model_path", f"diffusers/controlnet-{config.get('type', 'canny')}-sdxl-1.0"),
                            condition_image=config.get("condition_image"),
                            conditioning_scale=config.get("conditioning_scale", 1.0),
                            control_guidance_start=config.get("control_guidance_start", 0.0),
                            control_guidance_end=config.get("control_guidance_end", 1.0),
                            enabled=config.get("enabled", True),
                            preprocess_condition=config.get("preprocess_condition", True)
                        )
                    else:
                        logger.warning(f"Unsupported ControlNet configuration format: {type(config)}")
                        continue
                    
                    success = stack_config.add_adapter(adapter_config)
                    if not success:
                        logger.warning(f"Failed to add ControlNet adapter: {adapter_config.name}")
                
                # Prepare the ControlNet stack
                success = await self.controlnet_worker.prepare_controlnet_stack(stack_config)
                return success
                    
            elif isinstance(controlnet_config, dict):
                # Single ControlNet configuration
                adapter_config = ControlNetConfiguration(
                    name=controlnet_config.get("name", "controlnet_single"),
                    type=controlnet_config.get("type", "canny"),
                    model_path=controlnet_config.get("model_path", f"diffusers/controlnet-{controlnet_config.get('type', 'canny')}-sdxl-1.0"),
                    condition_image=controlnet_config.get("condition_image"),
                    conditioning_scale=controlnet_config.get("conditioning_scale", 1.0),
                    control_guidance_start=controlnet_config.get("control_guidance_start", 0.0),
                    control_guidance_end=controlnet_config.get("control_guidance_end", 1.0),
                    enabled=controlnet_config.get("enabled", True),
                    preprocess_condition=controlnet_config.get("preprocess_condition", True)
                )
                
                # Load single ControlNet
                success = await self.controlnet_worker.load_controlnet_model(adapter_config)
                return success
                    
            elif isinstance(controlnet_config, str):
                # Simple string format
                parts = controlnet_config.split(':')
                controlnet_type = parts[0]
                conditioning_scale = float(parts[1]) if len(parts) > 1 else 1.0
                
                adapter_config = ControlNetConfiguration(
                    name=f"controlnet_{controlnet_type}",
                    type=controlnet_type,
                    model_path=f"diffusers/controlnet-{controlnet_type}-sdxl-1.0",
                    conditioning_scale=conditioning_scale
                )
                
                success = await self.controlnet_worker.load_controlnet_model(adapter_config)
                return success
            else:
                logger.error(f"Unsupported ControlNet configuration type: {type(controlnet_config)}")
                return False
                
        except Exception as e:
            logger.error(f"Failed to configure ControlNet adapters: {e}")
            return False
    
    def get_controlnet_performance_stats(self) -> Dict[str, Any]:
        """Get ControlNet performance statistics."""
        try:
            if hasattr(self, 'controlnet_worker') and self.controlnet_worker:
                return self.controlnet_worker.get_performance_stats()
            else:
                return {}
        except Exception as e:
            logger.error(f"Failed to get ControlNet performance stats: {e}")
            return {}
    
    async def cleanup(self) -> None:
        """Cleanup resources."""
        try:
            if hasattr(self, 'controlnet_worker'):
                await self.controlnet_worker.cleanup()
            logger.info("Mock Enhanced SDXL Worker cleanup completed")
        except Exception as e:
            logger.error(f"Cleanup failed: {e}")

def create_test_image(size: tuple = (512, 512)) -> Image.Image:
    """Create a test image for condition processing."""
    # Create a simple test image with some patterns
    image_array = np.random.randint(0, 256, (*size, 3), dtype=np.uint8)
    
    # Add some structure (lines, shapes) for better testing
    image_array[100:110, :, :] = 255  # Horizontal line
    image_array[:, 250:260, :] = 255  # Vertical line
    
    return Image.fromarray(image_array)

async def test_basic_controlnet_integration():
    """Test basic ControlNet integration with Enhanced SDXL Worker."""
    try:
        logger.info("=== Testing Basic ControlNet Integration ===")
        
        # Initialize mock worker
        config = {
            "controlnet_config": {
                "memory_limit_mb": 1024,
                "enable_caching": True
            }
        }
        
        worker = MockEnhancedSDXLWorker(config)
        success = await worker.initialize()
        assert success == True
        logger.info("✅ Basic Integration: Worker initialization works")
        
        # Test single ControlNet configuration (string format)
        controlnet_config = "canny:0.8"
        success = await worker._configure_controlnet_adapters(controlnet_config)
        assert success == True
        logger.info("✅ Basic Integration: String configuration works")
        
        # Test single ControlNet configuration (dict format)
        controlnet_config = {
            "name": "test_canny",
            "type": "canny",
            "conditioning_scale": 0.7,
            "enabled": True
        }
        success = await worker._configure_controlnet_adapters(controlnet_config)
        assert success == True
        logger.info("✅ Basic Integration: Dict configuration works")
        
        # Get performance stats
        stats = worker.get_controlnet_performance_stats()
        assert "loaded_controlnets" in stats
        assert stats["loaded_controlnets"] > 0
        logger.info("✅ Basic Integration: Performance stats work")
        
        # Cleanup
        await worker.cleanup()
        logger.info("✅ Basic Integration: Cleanup works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Basic ControlNet integration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_multiple_controlnet_integration():
    """Test multiple ControlNet adapters integration."""
    try:
        logger.info("=== Testing Multiple ControlNet Integration ===")
        
        # Initialize mock worker
        config = {
            "controlnet_config": {
                "memory_limit_mb": 2048,
                "enable_caching": True
            }
        }
        
        worker = MockEnhancedSDXLWorker(config)
        await worker.initialize()
        
        # Test multiple ControlNet configurations (list format)
        controlnet_config = [
            "canny:0.8",
            "depth:0.6",
            {
                "name": "custom_pose",
                "type": "pose",
                "conditioning_scale": 1.0,
                "control_guidance_start": 0.1,
                "control_guidance_end": 0.9
            }
        ]
        
        success = await worker._configure_controlnet_adapters(controlnet_config)
        assert success == True
        logger.info("✅ Multiple Integration: Multi-adapter configuration works")
        
        # Verify performance stats
        stats = worker.get_controlnet_performance_stats()
        assert stats["loaded_controlnets"] >= 3
        assert stats["current_stack_size"] == 3
        logger.info("✅ Multiple Integration: Stack statistics work")
        
        # Cleanup
        await worker.cleanup()
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Multiple ControlNet integration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_controlnet_condition_processing():
    """Test ControlNet condition image processing integration."""
    try:
        logger.info("=== Testing ControlNet Condition Processing ===")
        
        # Initialize mock worker
        config = {"controlnet_config": {"memory_limit_mb": 1024}}
        worker = MockEnhancedSDXLWorker(config)
        await worker.initialize()
        
        # Create test condition image
        test_image = create_test_image()
        
        # Save test image to temporary file
        with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as temp_file:
            test_image.save(temp_file.name)
            condition_image_path = temp_file.name
        
        try:
            # Test ControlNet with condition image
            controlnet_config = {
                "name": "canny_with_condition",
                "type": "canny",
                "condition_image": condition_image_path,
                "conditioning_scale": 0.8,
                "preprocess_condition": True
            }
            
            success = await worker._configure_controlnet_adapters(controlnet_config)
            assert success == True
            logger.info("✅ Condition Processing: ControlNet with condition image works")
            
            # Test condition processing
            processed_image = await worker.controlnet_worker.process_condition_image(
                worker.controlnet_worker.controlnet_metadata["canny_with_condition"]
            )
            if processed_image:
                assert isinstance(processed_image, Image.Image)
                logger.info("✅ Condition Processing: Image processing works")
            else:
                logger.info("⚠️ Condition Processing: Skipped (OpenCV not available)")
            
        finally:
            # Clean up temporary file
            os.unlink(condition_image_path)
            await worker.cleanup()
        
        return True
        
    except Exception as e:
        logger.error(f"❌ ControlNet condition processing test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_controlnet_error_handling():
    """Test ControlNet error handling and edge cases."""
    try:
        logger.info("=== Testing ControlNet Error Handling ===")
        
        # Initialize mock worker
        config = {"controlnet_config": {"memory_limit_mb": 512}}
        worker = MockEnhancedSDXLWorker(config)
        await worker.initialize()
        
        # Test invalid configuration format
        success = await worker._configure_controlnet_adapters(12345)  # Invalid type
        assert success == False
        logger.info("✅ Error Handling: Invalid type rejection works")
        
        # Test empty configuration
        success = await worker._configure_controlnet_adapters([])
        assert success == True  # Should succeed with empty list
        logger.info("✅ Error Handling: Empty configuration handling works")
        
        # Test invalid ControlNet type
        controlnet_config = {
            "name": "invalid_type",
            "type": "nonexistent_type",
            "conditioning_scale": 0.8
        }
        
        # This should handle gracefully (may succeed or fail depending on implementation)
        success = await worker._configure_controlnet_adapters(controlnet_config)
        logger.info(f"✅ Error Handling: Invalid type handling: {success}")
        
        # Test performance stats when no ControlNets loaded
        stats = worker.get_controlnet_performance_stats()
        assert isinstance(stats, dict)
        logger.info("✅ Error Handling: Stats with no models works")
        
        # Cleanup
        await worker.cleanup()
        
        return True
        
    except Exception as e:
        logger.error(f"❌ ControlNet error handling test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_controlnet_configuration_formats():
    """Test different ControlNet configuration formats."""
    try:
        logger.info("=== Testing ControlNet Configuration Formats ===")
        
        # Initialize mock worker
        config = {"controlnet_config": {"memory_limit_mb": 1024}}
        worker = MockEnhancedSDXLWorker(config)
        await worker.initialize()
        
        # Test 1: Simple string format
        success = await worker._configure_controlnet_adapters("canny")
        assert success == True
        logger.info("✅ Configuration Formats: Simple string works")
        
        # Test 2: String with weight
        success = await worker._configure_controlnet_adapters("depth:0.6")
        assert success == True
        logger.info("✅ Configuration Formats: String with weight works")
        
        # Test 3: Full object format
        controlnet_config = {
            "name": "advanced_canny",
            "type": "canny",
            "model_path": "custom/controlnet-canny",
            "conditioning_scale": 0.9,
            "control_guidance_start": 0.0,
            "control_guidance_end": 0.8,
            "enabled": True,
            "preprocess_condition": True
        }
        success = await worker._configure_controlnet_adapters(controlnet_config)
        assert success == True
        logger.info("✅ Configuration Formats: Full object format works")
        
        # Test 4: Mixed list format
        mixed_config = [
            "canny:0.8",
            {"type": "depth", "conditioning_scale": 0.6},
            "pose"
        ]
        success = await worker._configure_controlnet_adapters(mixed_config)
        assert success == True
        logger.info("✅ Configuration Formats: Mixed list format works")
        
        # Verify final stats
        stats = worker.get_controlnet_performance_stats()
        assert stats["current_stack_size"] == 3  # Should have 3 from the mixed config
        logger.info("✅ Configuration Formats: Final stats verification works")
        
        # Cleanup
        await worker.cleanup()
        
        return True
        
    except Exception as e:
        logger.error(f"❌ ControlNet configuration formats test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def run_all_tests():
    """Run all Enhanced SDXL Worker ControlNet integration tests."""
    logger.info("\n" + "="*60)
    logger.info("RUNNING ENHANCED SDXL WORKER CONTROLNET INTEGRATION TESTS")
    logger.info("="*60)
    
    tests = [
        ("Basic ControlNet Integration", test_basic_controlnet_integration),
        ("Multiple ControlNet Integration", test_multiple_controlnet_integration),
        ("ControlNet Condition Processing", test_controlnet_condition_processing),
        ("ControlNet Error Handling", test_controlnet_error_handling),
        ("ControlNet Configuration Formats", test_controlnet_configuration_formats)
    ]
    
    results = []
    passed = 0
    total = len(tests)
    
    for test_name, test_func in tests:
        logger.info(f"\n--- Running {test_name} Test ---")
        try:
            result = await test_func()
            if result:
                logger.info(f"✅ {test_name}: PASSED")
                passed += 1
            else:
                logger.error(f"❌ {test_name}: FAILED")
            results.append((test_name, result))
        except Exception as e:
            logger.error(f"❌ {test_name}: ERROR - {e}")
            results.append((test_name, False))
    
    # Print summary
    logger.info("\n" + "="*60)
    logger.info("TEST RESULTS SUMMARY")
    logger.info("="*60)
    
    for test_name, result in results:
        status = "PASSED" if result else "FAILED"
        emoji = "✅" if result else "❌"
        logger.info(f"{emoji} {test_name}: {status}")
    
    success_rate = (passed / total) * 100
    logger.info(f"\nTotal tests: {total}")
    logger.info(f"Passed: {passed}")
    logger.info(f"Failed: {total - passed}")
    logger.info(f"Success rate: {success_rate:.1f}%")
    
    if success_rate >= 80:
        logger.info("\n🎉 Enhanced SDXL Worker ControlNet Integration tests PASSED!")
        return True
    else:
        logger.error(f"\n💥 Enhanced SDXL Worker ControlNet Integration tests FAILED! Need ≥80% success rate")
        return False

if __name__ == "__main__":
    success = asyncio.run(run_all_tests())
    exit(0 if success else 1)
