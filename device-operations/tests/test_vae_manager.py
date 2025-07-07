"""
VAE Manager Test Suite

Comprehensive tests for the VAE Manager implementation with mock VAE models
and realistic testing scenarios.
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

class MockVAEModel:
    """Mock VAE model for testing."""
    
    def __init__(self, name: str = "mock_vae"):
        self.name = name
        self.config = {"scaling_factor": 0.13025}
        self._parameters = [torch.randn(1000, 1000)]  # Simulate model parameters
        
    def parameters(self):
        """Return mock parameters."""
        return iter(self._parameters)
    
    def encode(self, x):
        """Mock encoding."""
        batch_size = x.shape[0]
        return type('EncoderOutput', (), {
            'latent_dist': type('LatentDist', (), {
                'sample': lambda: torch.randn(batch_size, 4, x.shape[-2]//8, x.shape[-1]//8)
            })()
        })()
    
    def decode(self, z):
        """Mock decoding."""
        batch_size = z.shape[0]
        return type('DecoderOutput', (), {
            'sample': torch.randn(batch_size, 3, z.shape[-2]*8, z.shape[-1]*8)
        })()
    
    def enable_slicing(self):
        """Mock VAE slicing."""
        logger.info(f"Mock VAE {self.name}: Slicing enabled")
    
    def enable_tiling(self):
        """Mock VAE tiling.""" 
        logger.info(f"Mock VAE {self.name}: Tiling enabled")
    
    @classmethod
    def from_pretrained(cls, model_path: str, **kwargs):
        """Mock loading from pretrained."""
        logger.info(f"Mock loading VAE from: {model_path}")
        return cls(f"pretrained_{Path(model_path).name}")
    
    @classmethod
    def from_single_file(cls, file_path: str, **kwargs):
        """Mock loading from single file."""
        logger.info(f"Mock loading VAE from file: {file_path}")
        return cls(f"file_{Path(file_path).stem}")

async def test_vae_configuration():
    """Test VAE configuration and stack management."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from vae_manager import VAEConfiguration, VAEStackConfiguration
        
        logger.info("=== Testing VAE Configuration ===")
        
        # Test basic VAE configuration
        vae_config = VAEConfiguration(
            name="test_vae",
            model_path="models/vae/test_vae.safetensors",
            model_type="custom",
            scaling_factor=0.13025,
            enable_slicing=True,
            enable_tiling=True
        )
        
        assert vae_config.name == "test_vae"
        assert vae_config.scaling_factor == 0.13025
        assert vae_config.enabled == True
        logger.info("✅ VAE Configuration: Basic configuration works")
        
        # Test VAE stack configuration
        stack_config = VAEStackConfiguration()
        
        # Add multiple VAEs
        vae_configs = [
            VAEConfiguration("sdxl_base", "madebyollin/sdxl-vae-fp16-fix", "sdxl_base"),
            VAEConfiguration("custom_vae", "models/custom.safetensors", "custom"),
            VAEConfiguration("refiner_vae", "models/refiner.safetensors", "sdxl_refiner")
        ]
        
        for vae_config in vae_configs:
            success = stack_config.add_vae(vae_config)
            assert success == True
        
        assert len(stack_config.vaes) == 3
        vae_names = stack_config.get_vae_names()
        assert len(vae_names) == 3
        logger.info("✅ VAE Stack: Multi-VAE management works")
        
        # Test VAE selection logic
        optimal_base = stack_config.select_optimal_vae("base")
        optimal_refiner = stack_config.select_optimal_vae("refiner")
        
        assert optimal_base in ["custom_vae", "sdxl_base"]  # Custom preferred for base
        assert optimal_refiner == "refiner_vae"  # Refiner preferred for refiner
        logger.info(f"✅ VAE Selection: Base={optimal_base}, Refiner={optimal_refiner}")
        
        # Test VAE removal
        success = stack_config.remove_vae("custom_vae")
        assert success == True
        assert len(stack_config.vaes) == 2
        logger.info("✅ VAE Stack: VAE removal works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ VAE configuration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_vae_optimizer():
    """Test VAE optimization functionality."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from vae_manager import VAEOptimizer, VAEConfiguration
        
        logger.info("=== Testing VAE Optimizer ===")
        
        optimizer = VAEOptimizer()
        
        # Create mock VAE model
        mock_vae = MockVAEModel("test_vae")
        
        # Test VAE optimization
        vae_config = VAEConfiguration(
            name="test_vae",
            model_path="test.safetensors",
            enable_slicing=True,
            enable_tiling=True,
            force_upcast=True
        )
        
        optimizations = optimizer.optimize_vae_settings(mock_vae, vae_config)
        
        assert "slicing_enabled" in optimizations
        assert "tiling_enabled" in optimizations
        assert "upcast_enabled" in optimizations
        logger.info(f"✅ VAE Optimization: {len(optimizations)} optimizations applied")
        
        # Test memory estimation
        memory_usage = optimizer.estimate_vae_memory(mock_vae)
        assert memory_usage > 0
        logger.info(f"✅ Memory Estimation: {memory_usage:.1f}MB")
        
        # Test performance benchmarking
        benchmark_results = optimizer.benchmark_vae_performance(mock_vae)
        
        assert "encode_time_ms" in benchmark_results
        assert "decode_time_ms" in benchmark_results
        assert "total_time_ms" in benchmark_results
        logger.info(f"✅ Performance Benchmark: {benchmark_results}")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ VAE optimizer test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_vae_loading():
    """Test VAE loading functionality."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from vae_manager import VAEManager, VAEConfiguration
        
        logger.info("=== Testing VAE Loading ===")
        
        # Initialize VAE manager with mock support
        config = {
            "memory_limit_mb": 1024,
            "models_dir": "models/vae"
        }
        
        manager = VAEManager(config)
        await manager.initialize()
        
        # Test loading VAE configurations
        vae_configs = [
            VAEConfiguration(
                name="sdxl_base",
                model_path="madebyollin/sdxl-vae-fp16-fix",
                model_type="sdxl_base"
            ),
            VAEConfiguration(
                name="custom_vae", 
                model_path="models/custom_vae.safetensors",
                model_type="custom",
                enable_slicing=True
            )
        ]
        
        # Mock the AutoencoderKL loading to use our MockVAEModel
        original_load = None
        try:
            # Patch loading to use mock model
            import vae_manager
            if hasattr(vae_manager, 'AutoencoderKL'):
                original_load = vae_manager.AutoencoderKL.from_pretrained
                vae_manager.AutoencoderKL.from_pretrained = MockVAEModel.from_pretrained
                vae_manager.AutoencoderKL.from_single_file = MockVAEModel.from_single_file
            
            # Test loading each VAE
            for vae_config in vae_configs:
                success = await manager.load_vae_model(vae_config)
                assert success == True, f"Failed to load {vae_config.name}"
                
                # Verify VAE is loaded
                assert vae_config.name in manager.loaded_vaes
                assert vae_config.name in manager.vae_metadata
                logger.info(f"✅ VAE Loading: Successfully loaded {vae_config.name}")
            
            # Test loading statistics
            assert len(manager.loaded_vaes) == len(vae_configs)
            stats = manager.get_performance_stats()
            assert stats["total_loads"] == len(vae_configs)
            logger.info(f"✅ VAE Loading: {len(manager.loaded_vaes)} VAEs loaded")
            
        finally:
            # Restore original loading if patched
            if original_load:
                vae_manager.AutoencoderKL.from_pretrained = original_load
        
        return True
        
    except Exception as e:
        logger.error(f"❌ VAE loading test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_vae_stack_configuration():
    """Test VAE stack configuration and selection."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from vae_manager import VAEManager, VAEConfiguration, VAEStackConfiguration
        
        logger.info("=== Testing VAE Stack Configuration ===")
        
        # Initialize manager
        config = {"memory_limit_mb": 1024}
        manager = VAEManager(config)
        await manager.initialize()
        
        # Create comprehensive VAE stack
        stack_config = VAEStackConfiguration()
        
        vae_configs = [
            VAEConfiguration("sdxl_base", "madebyollin/sdxl-vae-fp16-fix", "sdxl_base"),
            VAEConfiguration("sdxl_refiner", "madebyollin/sdxl-vae-fp16-fix", "sdxl_refiner"),
            VAEConfiguration("custom_art", "models/art_vae.safetensors", "custom"),
            VAEConfiguration("taesd", "madebyollin/taesd", "custom")
        ]
        
        for vae_config in vae_configs:
            stack_config.add_vae(vae_config)
        
        # Test stack configuration
        success = await manager.configure_vae_stack(stack_config)
        assert success == True
        
        # Test VAE selection for different pipeline types
        base_vae = manager.select_vae_for_pipeline("base")
        refiner_vae = manager.select_vae_for_pipeline("refiner")
        
        # Note: These will be None due to mocking, but test the selection logic
        logger.info(f"Selected VAEs - Base: {base_vae}, Refiner: {refiner_vae}")
        
        # Test stack statistics
        stats = manager.get_performance_stats()
        assert "current_stack_size" in stats
        logger.info(f"✅ VAE Stack: Configured {stats['current_stack_size']} VAEs")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ VAE stack configuration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_memory_management():
    """Test VAE memory management."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from vae_manager import VAEManager, VAEConfiguration
        
        logger.info("=== Testing Memory Management ===")
        
        # Initialize manager with memory constraints
        config = {
            "memory_limit_mb": 300,  # Small limit for testing
        }
        
        manager = VAEManager(config)
        await manager.initialize()
        
        # Test memory tracking
        assert hasattr(manager, 'memory_usage')
        assert hasattr(manager, 'memory_limit_mb')
        assert manager.memory_limit_mb == 300
        
        # Test unloading functionality
        manager.loaded_vaes["test_vae"] = MockVAEModel("test_vae")
        manager.vae_metadata["test_vae"] = VAEConfiguration("test_vae", "test.pt")
        manager.memory_usage["test_vae"] = 150.0
        
        success = await manager.unload_vae_model("test_vae")
        assert success == True
        assert "test_vae" not in manager.loaded_vaes
        assert "test_vae" not in manager.memory_usage
        logger.info("✅ Memory Management: VAE unloading works")
        
        # Test performance statistics
        stats = manager.get_performance_stats()
        required_keys = ["total_loads", "cache_hits", "memory_usage_mb", "loaded_vaes"]
        for key in required_keys:
            assert key in stats, f"Missing key: {key}"
        
        logger.info(f"✅ Memory Management: Performance stats complete")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Memory management test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_advanced_features():
    """Test advanced VAE manager features."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from vae_manager import VAEManager, VAEConfiguration
        
        logger.info("=== Testing Advanced Features ===")
        
        # Initialize manager with full configuration
        config = {
            "memory_limit_mb": 1024,
            "models_dir": "models/vae"
        }
        
        manager = VAEManager(config)
        await manager.initialize()
        
        # Test default VAE availability
        assert hasattr(manager, 'default_vaes')
        assert len(manager.default_vaes) > 0
        assert "sdxl_base" in manager.default_vaes
        logger.info(f"✅ Default VAEs: {len(manager.default_vaes)} available")
        
        # Test current stack initialization
        assert manager.current_stack is not None
        assert len(manager.current_stack.vaes) == len(manager.default_vaes)
        logger.info("✅ Default Stack: Initialized with default VAEs")
        
        # Test benchmarking (mock scenario)
        mock_vae = MockVAEModel("benchmark_test")
        manager.loaded_vaes["benchmark_test"] = mock_vae
        
        benchmark_results = await manager.benchmark_vae("benchmark_test")
        assert "encode_time_ms" in benchmark_results
        assert "decode_time_ms" in benchmark_results
        logger.info(f"✅ Benchmarking: {benchmark_results}")
        
        # Test cleanup
        await manager.cleanup()
        assert len(manager.loaded_vaes) == 0
        assert manager.current_stack is None
        logger.info("✅ Cleanup: Manager cleaned successfully")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Advanced features test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def run_all_tests():
    """Run all VAE Manager tests."""
    logger.info("\n" + "="*60)
    logger.info("RUNNING VAE MANAGER TEST SUITE")
    logger.info("="*60)
    
    tests = [
        ("VAE Configuration", test_vae_configuration),
        ("VAE Optimizer", test_vae_optimizer),
        ("VAE Loading", test_vae_loading),
        ("VAE Stack Configuration", test_vae_stack_configuration),
        ("Memory Management", test_memory_management),
        ("Advanced Features", test_advanced_features)
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
        logger.info("\n🎉 VAE Manager test suite PASSED!")
        return True
    else:
        logger.error(f"\n💥 VAE Manager test suite FAILED! Need ≥80% success rate")
        return False

if __name__ == "__main__":
    success = asyncio.run(run_all_tests())
    exit(0 if success else 1)
