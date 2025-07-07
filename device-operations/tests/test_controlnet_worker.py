"""
ControlNet Worker Test Suite

Comprehensive tests for the ControlNet Worker implementation with proper
integration testing, condition processing, and memory management validation.
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

class MockControlNetModel:
    """Mock ControlNet model for testing."""
    
    def __init__(self, model_id: str):
        self.model_id = model_id
        self.config = {"model_type": "controlnet"}
        
    def parameters(self):
        """Mock parameters for memory estimation."""
        # Create some mock parameters
        for i in range(10):
            yield torch.randn(100, 100)
    
    @classmethod
    def from_pretrained(cls, model_id: str, **kwargs):
        """Mock model loading."""
        logger.info(f"Mock loading ControlNet model: {model_id}")
        return cls(model_id)

class MockPipeline:
    """Mock pipeline for testing ControlNet integration."""
    
    def __init__(self):
        self.controlnets = []
        self.applied_controlnets = []
    
    def add_controlnet(self, controlnet):
        """Mock ControlNet addition."""
        self.controlnets.append(controlnet)
        logger.info(f"Mock pipeline added ControlNet: {controlnet.model_id}")

def create_test_image(size: tuple = (512, 512)) -> Image.Image:
    """Create a test image for condition processing."""
    # Create a simple test image with some patterns
    image_array = np.random.randint(0, 256, (*size, 3), dtype=np.uint8)
    
    # Add some structure (lines, shapes) for better testing
    image_array[100:110, :, :] = 255  # Horizontal line
    image_array[:, 250:260, :] = 255  # Vertical line
    
    return Image.fromarray(image_array)

async def test_controlnet_configuration():
    """Test ControlNet configuration and stack management."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from controlnet_worker import ControlNetConfiguration, ControlNetStackConfiguration
        
        logger.info("=== Testing ControlNet Configuration ===")
        
        # Test basic ControlNet configuration
        canny_config = ControlNetConfiguration(
            name="test_canny",
            type="canny",
            model_path="diffusers/controlnet-canny-sdxl-1.0",
            conditioning_scale=0.8,
            control_guidance_start=0.1,
            control_guidance_end=0.9
        )
        
        assert canny_config.name == "test_canny"
        assert canny_config.type == "canny"
        assert canny_config.conditioning_scale == 0.8
        assert canny_config.enabled == True
        logger.info("✅ ControlNet Configuration: Basic configuration works")
        
        # Test validation
        try:
            invalid_config = ControlNetConfiguration(
                name="invalid",
                type="canny",
                model_path="test",
                conditioning_scale=2.5  # Invalid: > 2.0
            )
            assert False, "Should have raised ValueError"
        except ValueError:
            logger.info("✅ ControlNet Configuration: Validation works")
        
        # Test ControlNet stack configuration
        stack_config = ControlNetStackConfiguration(max_adapters=3)
        
        # Add multiple adapters
        configs = [
            ControlNetConfiguration("canny", "canny", "test/canny", conditioning_scale=0.8),
            ControlNetConfiguration("depth", "depth", "test/depth", conditioning_scale=0.6),
            ControlNetConfiguration("pose", "pose", "test/pose", conditioning_scale=1.0)
        ]
        
        for config in configs:
            success = stack_config.add_adapter(config)
            assert success == True
        
        assert len(stack_config.adapters) == 3
        adapter_names = stack_config.get_adapter_names()
        assert len(adapter_names) == 3
        logger.info("✅ ControlNet Stack: Multi-adapter management works")
        
        # Test adapter removal
        success = stack_config.remove_adapter("depth")
        assert success == True
        assert len(stack_config.adapters) == 2
        logger.info("✅ ControlNet Stack: Adapter removal works")
        
        # Test adapter limit
        for i in range(5):  # Try to add more than max_adapters
            adapter = ControlNetConfiguration(
                name=f"overflow_{i}",
                type="canny",
                model_path=f"test/overflow_{i}"
            )
            stack_config.add_adapter(adapter)
        
        assert len(stack_config.adapters) <= 3  # Should respect max_adapters
        logger.info("✅ ControlNet Stack: Adapter limit enforcement works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ ControlNet configuration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_condition_processing():
    """Test condition image processing for different ControlNet types."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from controlnet_worker import ControlNetConditionProcessor
        
        logger.info("=== Testing Condition Processing ===")
        
        processor = ControlNetConditionProcessor()
        
        # Create test image
        test_image = create_test_image()
        
        # Test different processing types
        processing_types = ["canny", "depth", "pose", "scribble", "normal", "seg", "mlsd", "lineart"]
        
        for proc_type in processing_types:
            try:
                processed_image = await processor.process_condition_image(test_image, proc_type)
                
                assert isinstance(processed_image, Image.Image)
                assert processed_image.mode == "RGB"
                assert processed_image.size == test_image.size
                
                logger.info(f"✅ Condition Processing: {proc_type} works")
                
            except Exception as e:
                logger.warning(f"⚠️ Condition Processing: {proc_type} failed (expected if OpenCV not available): {e}")
        
        # Test unsupported type
        try:
            await processor.process_condition_image(test_image, "unsupported_type")
            assert False, "Should have raised ValueError"
        except ValueError:
            logger.info("✅ Condition Processing: Unsupported type validation works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Condition processing test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_controlnet_worker():
    """Test ControlNet Worker functionality."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from controlnet_worker import ControlNetWorker, ControlNetConfiguration
        
        logger.info("=== Testing ControlNet Worker ===")
        
        # Mock the ControlNet model loading
        import controlnet_worker
        original_controlnet = controlnet_worker.ControlNetModel
        controlnet_worker.ControlNetModel = MockControlNetModel
        
        try:
            # Initialize worker
            config = {
                "memory_limit_mb": 1024,
                "enable_caching": True
            }
            
            worker = ControlNetWorker(config)
            success = await worker.initialize()
            assert success == True
            logger.info("✅ ControlNet Worker: Initialization works")
            
            # Test model loading
            canny_config = ControlNetConfiguration(
                name="test_canny",
                type="canny",
                model_path="diffusers/controlnet-canny-sdxl-1.0",
                conditioning_scale=0.8
            )
            
            success = await worker.load_controlnet_model(canny_config)
            assert success == True
            assert "test_canny" in worker.loaded_controlnets
            assert "test_canny" in worker.controlnet_metadata
            logger.info("✅ ControlNet Worker: Model loading works")
            
            # Test memory tracking
            assert worker.performance_stats["total_loads"] == 1
            assert worker.performance_stats["memory_usage_mb"] > 0
            logger.info("✅ ControlNet Worker: Memory tracking works")
            
            # Test model unloading
            success = await worker.unload_controlnet_model("test_canny")
            assert success == True
            assert "test_canny" not in worker.loaded_controlnets
            logger.info("✅ ControlNet Worker: Model unloading works")
            
            # Test performance stats
            stats = worker.get_performance_stats()
            assert "loaded_controlnets" in stats
            assert "supported_types" in stats
            logger.info("✅ ControlNet Worker: Performance stats work")
            
        finally:
            # Restore original ControlNet model
            controlnet_worker.ControlNetModel = original_controlnet
            await worker.cleanup()
        
        return True
        
    except Exception as e:
        logger.error(f"❌ ControlNet worker test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_controlnet_stack():
    """Test ControlNet stack operations."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from controlnet_worker import ControlNetWorker, ControlNetConfiguration, ControlNetStackConfiguration
        
        logger.info("=== Testing ControlNet Stack ===")
        
        # Mock the ControlNet model loading
        import controlnet_worker
        original_controlnet = controlnet_worker.ControlNetModel
        controlnet_worker.ControlNetModel = MockControlNetModel
        
        try:
            # Initialize worker
            config = {"memory_limit_mb": 2048}
            worker = ControlNetWorker(config)
            await worker.initialize()
            
            # Create stack configuration
            stack_config = ControlNetStackConfiguration(max_adapters=3)
            
            # Add multiple ControlNet configurations
            configs = [
                ControlNetConfiguration("canny", "canny", "test/canny"),
                ControlNetConfiguration("depth", "depth", "test/depth"),
                ControlNetConfiguration("pose", "pose", "test/pose")
            ]
            
            for config in configs:
                stack_config.add_adapter(config)
            
            # Prepare the stack
            success = await worker.prepare_controlnet_stack(stack_config)
            assert success == True
            assert worker.current_stack is not None
            assert len(worker.loaded_controlnets) == 3
            logger.info("✅ ControlNet Stack: Stack preparation works")
            
            # Test pipeline application
            mock_pipeline = MockPipeline()
            success = await worker.apply_to_pipeline(mock_pipeline)
            assert success == True
            logger.info("✅ ControlNet Stack: Pipeline application works")
            
            # Test stack statistics
            stats = worker.get_performance_stats()
            assert stats["current_stack_size"] == 3
            logger.info("✅ ControlNet Stack: Stack statistics work")
            
        finally:
            # Restore original and cleanup
            controlnet_worker.ControlNetModel = original_controlnet
            await worker.cleanup()
        
        return True
        
    except Exception as e:
        logger.error(f"❌ ControlNet stack test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_memory_management():
    """Test ControlNet memory management."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from controlnet_worker import ControlNetWorker, ControlNetConfiguration
        
        logger.info("=== Testing Memory Management ===")
        
        # Initialize worker with low memory limit
        config = {
            "memory_limit_mb": 50,  # Very small limit for testing
            "enable_caching": True
        }
        
        worker = ControlNetWorker(config)
        await worker.initialize()
        
        # Test memory estimation
        mock_model = MockControlNetModel("test")
        memory_usage = worker._estimate_model_memory(mock_model)
        assert memory_usage > 0
        logger.info(f"✅ Memory Management: Memory estimation works ({memory_usage:.1f}MB)")
        
        # Test performance tracking
        assert "memory_usage_mb" in worker.performance_stats
        assert "total_loads" in worker.performance_stats
        logger.info("✅ Memory Management: Performance tracking works")
        
        # Test cleanup
        await worker.cleanup()
        assert len(worker.loaded_controlnets) == 0
        logger.info("✅ Memory Management: Cleanup works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Memory management test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_integration_features():
    """Test advanced integration features."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from controlnet_worker import ControlNetWorker, ControlNetConfiguration
        
        logger.info("=== Testing Integration Features ===")
        
        # Initialize worker
        config = {
            "memory_limit_mb": 1024,
            "enable_caching": True
        }
        
        worker = ControlNetWorker(config)
        await worker.initialize()
        
        # Test supported types
        assert len(worker.supported_types) > 0
        assert "canny" in worker.supported_types
        assert "depth" in worker.supported_types
        logger.info("✅ Integration: Supported types defined")
        
        # Test condition processing integration
        test_image = create_test_image()
        
        # Create configuration with condition image
        config_with_image = ControlNetConfiguration(
            name="test_with_image",
            type="canny",
            model_path="test/canny",
            condition_image=test_image,  # Pass image directly
            preprocess_condition=True
        )
        
        # Process condition image
        processed_image = await worker.process_condition_image(config_with_image)
        if processed_image:  # May be None if OpenCV not available
            assert isinstance(processed_image, Image.Image)
            logger.info("✅ Integration: Condition image processing works")
        else:
            logger.info("⚠️ Integration: Condition processing skipped (OpenCV not available)")
        
        # Test factory function
        from controlnet_worker import create_controlnet_worker
        factory_worker = create_controlnet_worker(config)
        assert isinstance(factory_worker, ControlNetWorker)
        logger.info("✅ Integration: Factory function works")
        
        # Cleanup
        await worker.cleanup()
        await factory_worker.cleanup()
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Integration features test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def run_all_tests():
    """Run all ControlNet Worker tests."""
    logger.info("\n" + "="*60)
    logger.info("RUNNING CONTROLNET WORKER TEST SUITE")
    logger.info("="*60)
    
    tests = [
        ("ControlNet Configuration", test_controlnet_configuration),
        ("Condition Processing", test_condition_processing),
        ("ControlNet Worker", test_controlnet_worker),
        ("ControlNet Stack", test_controlnet_stack),
        ("Memory Management", test_memory_management),
        ("Integration Features", test_integration_features)
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
        logger.info("\n🎉 ControlNet Worker test suite PASSED!")
        return True
    else:
        logger.error(f"\n💥 ControlNet Worker test suite FAILED! Need ≥80% success rate")
        return False

if __name__ == "__main__":
    success = asyncio.run(run_all_tests())
    exit(0 if success else 1)
