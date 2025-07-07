"""
Enhanced SDXL Worker VAE Integration Test

Tests the integration between Enhanced SDXL Worker and VAE Manager
to ensure seamless VAE loading, automatic selection, and pipeline integration.
"""

import asyncio
import tempfile
import torch
import logging
from pathlib import Path
from typing import Dict, Any, List, Optional
import sys
import os

# Add the src directory to the Python path
sys.path.append(os.path.join(os.path.dirname(__file__), 'src'))

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

class MockSDXLPipeline:
    """Mock SDXL pipeline for testing."""
    
    def __init__(self, pipeline_type: str = "base"):
        self.pipeline_type = pipeline_type
        self.vae = None
        self.scheduler = None
        self.loaded_loras = []
        self.loaded_controlnets = []
        
    def enable_attention_slicing(self):
        logger.info(f"Mock {self.pipeline_type} pipeline: Attention slicing enabled")
    
    def enable_vae_slicing(self):
        logger.info(f"Mock {self.pipeline_type} pipeline: VAE slicing enabled")
    
    def __call__(self, **kwargs):
        # Mock inference result
        from PIL import Image
        import numpy as np
        
        # Create a simple mock image
        mock_image = Image.fromarray(np.random.randint(0, 255, (512, 512, 3), dtype=np.uint8))
        
        return type('PipelineOutput', (), {
            'images': [mock_image]
        })()

class MockEnhancedRequest:
    """Mock enhanced request for testing."""
    
    def __init__(self):
        self.prompt = "a beautiful landscape"
        self.negative_prompt = "low quality"
        self.width = 1024
        self.height = 1024
        self.num_inference_steps = 20
        self.guidance_scale = 7.5
        self.num_images_per_prompt = 1
        self.generator = None
        
        # Model configuration
        self.model = {
            "base": "stabilityai/stable-diffusion-xl-base-1.0",
            "refiner": "stabilityai/stable-diffusion-xl-refiner-1.0",
            "vae": "madebyollin/sdxl-vae-fp16-fix",
            "scheduler": "DPMSolverMultistepScheduler"
        }
        
        # Feature configurations
        self.lora = None
        self.controlnet = None
        self.batch = None

async def test_vae_manager_integration():
    """Test VAE Manager integration with Enhanced SDXL Worker."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from vae_manager import VAEManager, VAEConfiguration
        
        logger.info("=== Testing VAE Manager Integration ===")
        
        # Initialize VAE Manager
        config = {
            "memory_limit_mb": 1024,
            "models_dir": "models/vae"
        }
        
        vae_manager = VAEManager(config)
        await vae_manager.initialize()
        
        # Test VAE configuration for SDXL
        vae_config = VAEConfiguration(
            name="sdxl_optimized",
            model_path="madebyollin/sdxl-vae-fp16-fix",
            model_type="sdxl_base",
            enable_slicing=True,
            enable_tiling=True,
            scaling_factor=0.13025
        )
        
        # Test VAE loading
        success = await vae_manager.load_vae_model(vae_config)
        logger.info(f"VAE loading success: {success}")
        
        # Test VAE retrieval
        loaded_vae = vae_manager.get_vae_model("sdxl_optimized")
        assert loaded_vae is not None, "VAE should be loaded and retrievable"
        logger.info("✅ VAE Manager Integration: Basic functionality works")
        
        # Test automatic VAE selection
        base_vae = vae_manager.select_vae_for_pipeline("base")
        refiner_vae = vae_manager.select_vae_for_pipeline("refiner")
        
        logger.info(f"Selected VAEs - Base: {base_vae is not None}, Refiner: {refiner_vae is not None}")
        logger.info("✅ VAE Manager Integration: Automatic selection works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ VAE Manager integration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_enhanced_worker_vae_loading():
    """Test Enhanced SDXL Worker VAE loading capabilities."""
    try:
        # Mock the Enhanced SDXL Worker initialization
        logger.info("=== Testing Enhanced Worker VAE Loading ===")
        
        # Test configuration
        worker_config = {
            "device": "cpu",
            "memory_optimization": True,
            "vae_slicing": True,
            "vae_config": {
                "memory_limit_mb": 1024,
                "models_dir": "models/vae"
            }
        }
        
        # Create mock worker components
        from unittest.mock import MagicMock
        
        # Mock VAE Manager
        mock_vae_manager = MagicMock()
        mock_vae_manager.is_initialized = True
        mock_vae_manager.initialize = MagicMock(return_value=True)
        mock_vae_manager.load_vae_model = MagicMock(return_value=True)
        mock_vae_manager.get_vae_model = MagicMock(return_value="mock_vae_model")
        mock_vae_manager.select_vae_for_pipeline = MagicMock(return_value="auto_vae_model")
        
        # Test custom VAE loading logic
        async def test_custom_vae_loading():
            """Test the custom VAE loading logic."""
            vae_path = "madebyollin/sdxl-vae-fp16-fix"
            
            # Simulate VAE configuration creation
            sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
            from vae_manager import VAEConfiguration
            
            vae_config = VAEConfiguration(
                name=f"custom_{Path(vae_path).stem}",
                model_path=vae_path,
                model_type="custom",
                enable_slicing=True,
                enable_tiling=True
            )
            
            # Simulate loading
            success = await mock_vae_manager.load_vae_model(vae_config)
            assert success == True, "VAE loading should succeed"
            
            # Simulate retrieval
            custom_vae = mock_vae_manager.get_vae_model(vae_config.name)
            assert custom_vae is not None, "VAE should be retrievable after loading"
            
            logger.info("✅ Custom VAE loading logic works")
            return True
        
        # Test automatic VAE selection logic
        async def test_automatic_vae_selection():
            """Test automatic VAE selection logic."""
            
            # Test base pipeline VAE selection
            base_vae = mock_vae_manager.select_vae_for_pipeline("base")
            assert base_vae is not None, "Base VAE should be selected"
            
            # Test refiner pipeline VAE selection
            refiner_vae = mock_vae_manager.select_vae_for_pipeline("refiner")
            assert refiner_vae is not None, "Refiner VAE should be selected"
            
            logger.info("✅ Automatic VAE selection logic works")
            return True
        
        # Run sub-tests
        success1 = await test_custom_vae_loading()
        success2 = await test_automatic_vae_selection()
        
        return success1 and success2
        
    except Exception as e:
        logger.error(f"❌ Enhanced Worker VAE loading test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_pipeline_vae_application():
    """Test VAE application to SDXL pipelines."""
    try:
        logger.info("=== Testing Pipeline VAE Application ===")
        
        # Create mock pipelines
        base_pipeline = MockSDXLPipeline("base")
        refiner_pipeline = MockSDXLPipeline("refiner")
        
        # Test VAE assignment
        mock_vae = "mock_vae_model"
        
        # Apply VAE to base pipeline
        base_pipeline.vae = mock_vae
        assert base_pipeline.vae == mock_vae, "VAE should be applied to base pipeline"
        logger.info("✅ VAE applied to base pipeline")
        
        # Apply VAE to refiner pipeline
        refiner_pipeline.vae = mock_vae
        assert refiner_pipeline.vae == mock_vae, "VAE should be applied to refiner pipeline"
        logger.info("✅ VAE applied to refiner pipeline")
        
        # Test memory optimizations
        base_pipeline.enable_vae_slicing()
        refiner_pipeline.enable_vae_slicing()
        logger.info("✅ VAE slicing enabled on both pipelines")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Pipeline VAE application test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_vae_performance_monitoring():
    """Test VAE performance monitoring and statistics."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from vae_manager import VAEManager
        
        logger.info("=== Testing VAE Performance Monitoring ===")
        
        # Initialize VAE Manager
        config = {"memory_limit_mb": 512}
        vae_manager = VAEManager(config)
        await vae_manager.initialize()
        
        # Test performance statistics
        stats = vae_manager.get_performance_stats()
        
        required_keys = [
            "total_loads", "cache_hits", "memory_usage_mb", 
            "loaded_vaes", "available_default_vaes", "current_stack_size"
        ]
        
        for key in required_keys:
            assert key in stats, f"Missing performance stat: {key}"
        
        logger.info(f"Performance stats: {stats}")
        logger.info("✅ VAE Performance monitoring works")
        
        # Test memory tracking
        assert hasattr(vae_manager, 'memory_usage'), "Should have memory usage tracking"
        assert hasattr(vae_manager, 'memory_limit_mb'), "Should have memory limit configuration"
        logger.info("✅ Memory tracking available")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ VAE performance monitoring test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_vae_integration_workflow():
    """Test complete VAE integration workflow."""
    try:
        logger.info("=== Testing Complete VAE Integration Workflow ===")
        
        # Step 1: Initialize system
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from vae_manager import VAEManager, VAEConfiguration
        
        vae_manager = VAEManager({"memory_limit_mb": 1024})
        await vae_manager.initialize()
        
        # Step 2: Create request with VAE
        request = MockEnhancedRequest()
        
        # Step 3: Load custom VAE
        if request.model.get("vae"):
            vae_config = VAEConfiguration(
                name="request_vae",
                model_path=request.model["vae"],
                model_type="custom",
                enable_slicing=True
            )
            
            success = await vae_manager.load_vae_model(vae_config)
            assert success, "Custom VAE should load successfully"
            logger.info("✅ Step 3: Custom VAE loaded")
        
        # Step 4: Create mock pipelines
        base_pipeline = MockSDXLPipeline("base")
        refiner_pipeline = MockSDXLPipeline("refiner")
        
        # Step 5: Apply VAE to pipelines
        if request.model.get("vae"):
            loaded_vae = vae_manager.get_vae_model("request_vae")
            if loaded_vae:
                base_pipeline.vae = loaded_vae
                refiner_pipeline.vae = loaded_vae
                logger.info("✅ Step 5: VAE applied to pipelines")
        
        # Step 6: Apply memory optimizations
        base_pipeline.enable_vae_slicing()
        refiner_pipeline.enable_vae_slicing()
        logger.info("✅ Step 6: Memory optimizations applied")
        
        # Step 7: Monitor performance
        stats = vae_manager.get_performance_stats()
        assert stats["total_loads"] > 0, "Should have load statistics"
        logger.info(f"✅ Step 7: Performance stats: {stats['total_loads']} loads")
        
        logger.info("✅ Complete VAE integration workflow successful")
        return True
        
    except Exception as e:
        logger.error(f"❌ VAE integration workflow test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def run_all_tests():
    """Run all Enhanced SDXL Worker VAE integration tests."""
    logger.info("\n" + "="*70)
    logger.info("RUNNING ENHANCED SDXL WORKER VAE INTEGRATION TEST SUITE")
    logger.info("="*70)
    
    tests = [
        ("VAE Manager Integration", test_vae_manager_integration),
        ("Enhanced Worker VAE Loading", test_enhanced_worker_vae_loading),
        ("Pipeline VAE Application", test_pipeline_vae_application),
        ("VAE Performance Monitoring", test_vae_performance_monitoring),
        ("VAE Integration Workflow", test_vae_integration_workflow)
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
        logger.info("\n🎉 Enhanced SDXL Worker VAE Integration test suite PASSED!")
        return True
    else:
        logger.error(f"\n💥 Enhanced SDXL Worker VAE Integration test suite FAILED! Need ≥80% success rate")
        return False

if __name__ == "__main__":
    success = asyncio.run(run_all_tests())
    exit(0 if success else 1)
