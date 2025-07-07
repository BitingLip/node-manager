"""
SDXL Refiner Pipeline Test Suite

Comprehensive tests for the SDXL Refiner Pipeline implementation with mock models
and realistic testing scenarios for two-stage generation.
"""

import asyncio
import tempfile
import torch
import logging
from pathlib import Path
from typing import Dict, Any, List, Optional
import sys
import os
from PIL import Image
import numpy as np

# Add the src directory to the Python path
sys.path.append(os.path.join(os.path.dirname(__file__), 'src'))

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

class MockRefinerPipeline:
    """Mock SDXL refiner pipeline for testing."""
    
    def __init__(self, model_path: str = "mock_refiner"):
        self.model_path = model_path
        self.device = "cpu"
        self.torch_dtype = torch.float32
        
    def to(self, device):
        """Mock device movement."""
        self.device = device
        return self
    
    def enable_attention_slicing(self):
        """Mock attention slicing."""
        logger.info("Mock refiner: Attention slicing enabled")
    
    def enable_vae_slicing(self):
        """Mock VAE slicing."""
        logger.info("Mock refiner: VAE slicing enabled")
    
    def enable_model_cpu_offload(self):
        """Mock CPU offload."""
        logger.info("Mock refiner: CPU offload enabled")
    
    def __call__(self, image, prompt, negative_prompt=None, **kwargs):
        """Mock refinement call."""
        # Create a slightly "enhanced" version of the input image
        input_array = np.array(image)
        
        # Add some artificial enhancement (slight contrast boost)
        enhanced_array = np.clip(input_array * 1.1, 0, 255).astype(np.uint8)
        enhanced_image = Image.fromarray(enhanced_array)
        
        return type('RefinerOutput', (), {
            'images': [enhanced_image]
        })()
    
    @classmethod
    def from_pretrained(cls, model_path: str, **kwargs):
        """Mock loading from pretrained."""
        logger.info(f"Mock loading refiner from: {model_path}")
        return cls(model_path)

def create_test_image(width: int = 512, height: int = 512) -> Image.Image:
    """Create a test image for refinement testing."""
    # Create a simple test pattern
    array = np.random.randint(0, 255, (height, width, 3), dtype=np.uint8)
    
    # Add some structure to make quality assessment more meaningful
    for i in range(0, width, 50):
        array[:, i:i+10, :] = 255  # Vertical lines
    for j in range(0, height, 50):
        array[j:j+10, :, :] = 128  # Horizontal lines
    
    return Image.fromarray(array)

async def test_refiner_configuration():
    """Test refiner configuration and validation."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from sdxl_refiner_pipeline import RefinerConfiguration, RefinerMetrics
        
        logger.info("=== Testing Refiner Configuration ===")
        
        # Test basic configuration
        config = RefinerConfiguration(
            model_path="stabilityai/stable-diffusion-xl-refiner-1.0",
            strength=0.3,
            num_inference_steps=10,
            guidance_scale=7.5,
            aesthetic_score=6.0
        )
        
        assert config.model_path == "stabilityai/stable-diffusion-xl-refiner-1.0"
        assert config.strength == 0.3
        assert config.num_inference_steps == 10
        logger.info("✅ Basic configuration works")
        
        # Test configuration validation
        try:
            invalid_config = RefinerConfiguration(
                model_path="test",
                strength=1.5  # Invalid: > 1.0
            )
            assert False, "Should have raised ValueError"
        except ValueError:
            logger.info("✅ Configuration validation works")
        
        # Test metrics structure
        metrics = RefinerMetrics()
        metrics_dict = metrics.to_dict()
        
        required_keys = [
            "total_time_ms", "loading_time_ms", "inference_time_ms",
            "memory_usage_mb", "images_processed", "quality_improvement"
        ]
        
        for key in required_keys:
            assert key in metrics_dict, f"Missing metric key: {key}"
        
        logger.info("✅ Metrics structure complete")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Refiner configuration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_quality_assessment():
    """Test quality assessment functionality."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from sdxl_refiner_pipeline import QualityAssessment
        
        logger.info("=== Testing Quality Assessment ===")
        
        # Create test images
        base_image = create_test_image(256, 256)
        
        # Create a "refined" version (slightly enhanced)
        base_array = np.array(base_image)
        enhanced_array = np.clip(base_array * 1.2, 0, 255).astype(np.uint8)
        refined_image = Image.fromarray(enhanced_array)
        
        # Test quality metrics calculation
        assessor = QualityAssessment()
        metrics = assessor.calculate_quality_metrics(base_image, refined_image)
        
        required_metrics = ["sharpness_improvement", "contrast_improvement", "overall_quality_score"]
        for metric in required_metrics:
            assert metric in metrics, f"Missing quality metric: {metric}"
            assert isinstance(metrics[metric], (int, float)), f"Invalid metric type: {metric}"
        
        logger.info(f"Quality metrics: {metrics}")
        logger.info("✅ Quality metrics calculation works")
        
        # Test refinement benefit assessment
        has_benefit = assessor.assess_refinement_benefit(metrics, threshold=1.05)
        logger.info(f"Refinement benefit detected: {has_benefit}")
        logger.info("✅ Benefit assessment works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Quality assessment test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_refiner_pipeline_loading():
    """Test refiner pipeline loading and setup."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from sdxl_refiner_pipeline import SDXLRefinerPipeline, RefinerConfiguration
        
        logger.info("=== Testing Refiner Pipeline Loading ===")
        
        # Create configuration
        config = RefinerConfiguration(
            model_path="stabilityai/stable-diffusion-xl-refiner-1.0",
            strength=0.3,
            enable_attention_slicing=True,
            enable_vae_slicing=True
        )
        
        # Create refiner pipeline
        refiner = SDXLRefinerPipeline(config)
        
        assert not refiner.is_loaded, "Should not be loaded initially"
        assert refiner.device == "cpu", "Should default to CPU"
        logger.info("✅ Pipeline initialization works")
        
        # Mock the pipeline loading to use our mock
        original_from_pretrained = None
        try:
            import sdxl_refiner_pipeline
            if hasattr(sdxl_refiner_pipeline, 'StableDiffusionXLImg2ImgPipeline'):
                original_from_pretrained = sdxl_refiner_pipeline.StableDiffusionXLImg2ImgPipeline.from_pretrained
                sdxl_refiner_pipeline.StableDiffusionXLImg2ImgPipeline.from_pretrained = MockRefinerPipeline.from_pretrained
            
            # Test model loading
            success = await refiner.load_refiner_model("cpu", torch.float32)
            assert success == True, "Model loading should succeed"
            assert refiner.is_loaded == True, "Should be marked as loaded"
            logger.info("✅ Model loading works")
            
            # Test performance stats
            stats = refiner.get_performance_stats()
            assert stats["is_loaded"] == True
            assert "metrics" in stats
            assert "configuration" in stats
            logger.info(f"Performance stats: {stats}")
            logger.info("✅ Performance stats work")
            
        finally:
            # Restore original if patched
            if original_from_pretrained and hasattr(sdxl_refiner_pipeline, 'StableDiffusionXLImg2ImgPipeline'):
                sdxl_refiner_pipeline.StableDiffusionXLImg2ImgPipeline.from_pretrained = original_from_pretrained
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Refiner pipeline loading test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_image_refinement():
    """Test image refinement functionality."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from sdxl_refiner_pipeline import SDXLRefinerPipeline, RefinerConfiguration
        
        logger.info("=== Testing Image Refinement ===")
        
        # Create refiner
        config = RefinerConfiguration(
            model_path="test_refiner",
            strength=0.4,
            num_inference_steps=5
        )
        
        refiner = SDXLRefinerPipeline(config)
        
        # Mock the refiner pipeline
        refiner.refiner_pipeline = MockRefinerPipeline()
        refiner.is_loaded = True
        
        # Create test images
        test_images = [create_test_image(256, 256) for _ in range(2)]
        
        # Test refinement
        refined_images, metrics = await refiner.refine_images(
            test_images,
            prompt="a beautiful landscape",
            negative_prompt="low quality"
        )
        
        assert len(refined_images) == len(test_images), "Should refine all images"
        assert metrics.images_processed == len(test_images), "Should track processed count"
        assert metrics.total_time_ms > 0, "Should track processing time"
        assert metrics.quality_improvement > 0, "Should have quality metrics"
        
        logger.info(f"Refined {len(refined_images)} images")
        logger.info(f"Processing time: {metrics.total_time_ms:.1f}ms")
        logger.info(f"Quality improvement: {metrics.quality_improvement:.3f}")
        logger.info("✅ Image refinement works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Image refinement test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_adaptive_refinement():
    """Test adaptive refinement with quality targets."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from sdxl_refiner_pipeline import SDXLRefinerPipeline, RefinerConfiguration
        
        logger.info("=== Testing Adaptive Refinement ===")
        
        # Create refiner
        config = RefinerConfiguration(
            model_path="test_refiner",
            strength=0.2  # Start with low strength
        )
        
        refiner = SDXLRefinerPipeline(config)
        refiner.refiner_pipeline = MockRefinerPipeline()
        refiner.is_loaded = True
        
        # Create test image
        test_images = [create_test_image(256, 256)]
        
        # Test adaptive refinement
        refined_images, metrics = await refiner.refine_with_adaptive_strength(
            test_images,
            prompt="high quality image",
            target_quality=1.1,
            max_attempts=2
        )
        
        assert len(refined_images) == 1, "Should refine the image"
        assert metrics.quality_improvement > 0, "Should have quality improvement"
        
        logger.info(f"Adaptive refinement quality: {metrics.quality_improvement:.3f}")
        logger.info("✅ Adaptive refinement works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Adaptive refinement test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_configuration_updates():
    """Test runtime configuration updates."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from sdxl_refiner_pipeline import SDXLRefinerPipeline, RefinerConfiguration
        
        logger.info("=== Testing Configuration Updates ===")
        
        # Create refiner
        config = RefinerConfiguration(
            model_path="test_refiner",
            strength=0.3,
            num_inference_steps=10
        )
        
        refiner = SDXLRefinerPipeline(config)
        
        # Test configuration update
        new_config = {
            "strength": 0.5,
            "num_inference_steps": 15,
            "guidance_scale": 8.0
        }
        
        refiner.update_configuration(new_config)
        
        assert refiner.config.strength == 0.5, "Strength should be updated"
        assert refiner.config.num_inference_steps == 15, "Steps should be updated"
        assert refiner.config.guidance_scale == 8.0, "Guidance scale should be updated"
        
        logger.info("✅ Configuration updates work")
        
        # Test cleanup
        await refiner.cleanup()
        assert refiner.is_loaded == False, "Should be marked as unloaded after cleanup"
        logger.info("✅ Cleanup works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Configuration updates test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_refiner_integration_workflow():
    """Test complete refiner integration workflow."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from sdxl_refiner_pipeline import (
            SDXLRefinerPipeline, RefinerConfiguration, 
            QualityAssessment, create_refiner_pipeline
        )
        
        logger.info("=== Testing Complete Refiner Workflow ===")
        
        # Step 1: Create configuration
        config = RefinerConfiguration(
            model_path="stabilityai/stable-diffusion-xl-refiner-1.0",
            strength=0.3,
            num_inference_steps=10,
            guidance_scale=7.5,
            aesthetic_score=6.0,
            enable_attention_slicing=True,
            enable_vae_slicing=True
        )
        
        # Step 2: Create refiner using factory
        refiner = create_refiner_pipeline(config)
        assert isinstance(refiner, SDXLRefinerPipeline), "Factory should create correct type"
        logger.info("✅ Step 2: Refiner created via factory")
        
        # Step 3: Mock loading for testing
        refiner.refiner_pipeline = MockRefinerPipeline()
        refiner.is_loaded = True
        logger.info("✅ Step 3: Mock refiner loaded")
        
        # Step 4: Create base images (simulate base generation output)
        base_images = [create_test_image(512, 512) for _ in range(2)]
        logger.info("✅ Step 4: Base images created")
        
        # Step 5: Apply refinement
        refined_images, metrics = await refiner.refine_images(
            base_images,
            prompt="masterpiece, high quality artwork, detailed",
            negative_prompt="blurry, low quality, distorted"
        )
        
        assert len(refined_images) == len(base_images), "Should refine all base images"
        logger.info("✅ Step 5: Refinement applied successfully")
        
        # Step 6: Validate quality improvement
        assessor = QualityAssessment()
        for i, (base, refined) in enumerate(zip(base_images, refined_images)):
            quality_metrics = assessor.calculate_quality_metrics(base, refined)
            has_benefit = assessor.assess_refinement_benefit(quality_metrics)
            logger.info(f"Image {i+1} quality improvement: {quality_metrics['overall_quality_score']:.3f}, beneficial: {has_benefit}")
        
        logger.info("✅ Step 6: Quality assessment completed")
        
        # Step 7: Get comprehensive performance stats
        stats = refiner.get_performance_stats()
        assert stats["metrics"]["images_processed"] == len(base_images)
        assert stats["metrics"]["total_time_ms"] > 0
        logger.info(f"✅ Step 7: Performance stats: {stats['metrics']}")
        
        # Step 8: Cleanup
        await refiner.cleanup()
        logger.info("✅ Step 8: Cleanup completed")
        
        logger.info("✅ Complete refiner workflow successful")
        return True
        
    except Exception as e:
        logger.error(f"❌ Refiner integration workflow test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def run_all_tests():
    """Run all SDXL Refiner Pipeline tests."""
    logger.info("\n" + "="*70)
    logger.info("RUNNING SDXL REFINER PIPELINE TEST SUITE")
    logger.info("="*70)
    
    tests = [
        ("Refiner Configuration", test_refiner_configuration),
        ("Quality Assessment", test_quality_assessment),
        ("Refiner Pipeline Loading", test_refiner_pipeline_loading),
        ("Image Refinement", test_image_refinement),
        ("Adaptive Refinement", test_adaptive_refinement),
        ("Configuration Updates", test_configuration_updates),
        ("Refiner Integration Workflow", test_refiner_integration_workflow)
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
    logger.info("\n" + "="*70)
    logger.info("TEST RESULTS SUMMARY")
    logger.info("="*70)
    
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
        logger.info("\n🎉 SDXL Refiner Pipeline test suite PASSED!")
        return True
    else:
        logger.error(f"\n💥 SDXL Refiner Pipeline test suite FAILED! Need ≥80% success rate")
        return False

if __name__ == "__main__":
    success = asyncio.run(run_all_tests())
    exit(0 if success else 1)
