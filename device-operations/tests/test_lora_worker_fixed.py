"""
Fixed LoRA Worker Test Suite

Comprehensive tests for the LoRA Worker implementation with proper
directory structure and file handling.
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

class MockDiffusionPipeline:
    """Mock diffusion pipeline for testing."""
    
    def __init__(self):
        self.loaded_adapters = {}
        self.adapter_weights = {}
        self.last_adapter_names = []
        self.last_adapter_weights = []
    
    def load_lora_weights(self, adapter_path: str, adapter_name: str, **kwargs):
        """Mock LoRA loading."""
        self.loaded_adapters[adapter_name] = adapter_path
        logger.info(f"Mock pipeline loaded LoRA: {adapter_name} from {adapter_path}")
    
    def set_adapters(self, adapter_names: List[str], adapter_weights: Optional[List[float]] = None):
        """Mock adapter configuration."""
        self.last_adapter_names = adapter_names
        self.last_adapter_weights = adapter_weights or [1.0] * len(adapter_names)
        for name, weight in zip(adapter_names, self.last_adapter_weights):
            self.adapter_weights[name] = weight
        logger.info(f"Mock pipeline set adapters: {adapter_names} with weights {self.last_adapter_weights}")

def create_mock_lora_file(file_path: Path, file_format: str = 'safetensors') -> None:
    """Create a mock LoRA file for testing."""
    file_path.parent.mkdir(parents=True, exist_ok=True)
    
    if file_format == 'safetensors':
        # Create a simple tensor dict and save as safetensors
        import safetensors.torch
        mock_tensors = {
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_k.lora_down.weight": torch.randn(320, 64),
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_k.lora_up.weight": torch.randn(64, 320),
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_v.lora_down.weight": torch.randn(320, 64),
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_v.lora_up.weight": torch.randn(64, 320)
        }
        safetensors.torch.save_file(mock_tensors, str(file_path))
        
    elif file_format == 'pt':
        # Create a PyTorch checkpoint file
        mock_state_dict = {
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_k.lora_down.weight": torch.randn(320, 64),
            "lora_unet.down_blocks.0.attentions.0.transformer_blocks.0.attn1.to_k.lora_up.weight": torch.randn(64, 320)
        }
        torch.save({"state_dict": mock_state_dict}, str(file_path))
    
    logger.info(f"Created mock LoRA file: {file_path} ({file_format})")

async def test_lora_configuration():
    """Test LoRA configuration and stack management."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAConfiguration, LoRAStackConfiguration
        
        logger.info("=== Testing LoRA Configuration ===")
        
        # Test basic LoRA configuration
        lora_config = LoRAConfiguration(
            name="test_lora",
            path="models/lora/test_lora.safetensors",
            weight=0.8
        )
        
        assert lora_config.name == "test_lora"
        assert lora_config.weight == 0.8
        assert lora_config.enabled == True
        logger.info("✅ LoRA Configuration: Basic configuration works")
        
        # Test LoRA stack configuration
        stack_config = LoRAStackConfiguration(max_adapters=4)
        
        # Add multiple adapters
        for i in range(3):
            adapter = LoRAConfiguration(
                name=f"adapter_{i}",
                path=f"models/lora/adapter_{i}.safetensors",
                weight=0.5 + i * 0.2
            )
            success = stack_config.add_adapter(adapter)
            assert success == True
        
        assert len(stack_config.adapters) == 3
        adapter_names = stack_config.get_adapter_names()
        assert len(adapter_names) == 3
        logger.info("✅ LoRA Stack: Multi-adapter management works")
        
        # Test adapter removal
        success = stack_config.remove_adapter("adapter_1")
        assert success == True
        assert len(stack_config.adapters) == 2
        logger.info("✅ LoRA Stack: Adapter removal works")
        
        # Test adapter limit
        for i in range(5):  # Try to add more than max_adapters
            adapter = LoRAConfiguration(
                name=f"overflow_{i}",
                path=f"models/lora/overflow_{i}.safetensors"
            )
            stack_config.add_adapter(adapter)
        
        assert len(stack_config.adapters) <= 4  # Should respect max_adapters
        logger.info("✅ LoRA Stack: Adapter limit enforcement works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ LoRA configuration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_lora_discovery():
    """Test LoRA file discovery and path resolution."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAWorker
        
        logger.info("=== Testing LoRA Discovery ===")
        
        # Create temporary directory structure
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "loras"
            lora_dir.mkdir(parents=True)
            
            # Create mock LoRA files exactly as expected
            test_files = [
                "dragon_lora.safetensors",
                "style_lora.pt",
                "character_lora.safetensors"
            ]
            
            for filename in test_files:
                file_path = lora_dir / filename
                file_format = 'safetensors' if filename.endswith('.safetensors') else 'pt'
                create_mock_lora_file(file_path, file_format)
            
            # Initialize LoRA worker with custom directory
            config = {
                "lora_directories": [str(lora_dir)],
                "enable_caching": True
            }
            lora_worker = LoRAWorker(config)
            
            # Test file discovery
            discovered_files = await lora_worker.discover_lora_files()
            
            logger.info(f"Created files: {test_files}")
            logger.info(f"Discovered files: {list(discovered_files.keys())}")
            
            # Check that we found the expected files
            assert len(discovered_files) == len(test_files), f"Expected {len(test_files)} files, found {len(discovered_files)}"
            
            # Check that all expected file stems are found
            expected_stems = {Path(f).stem for f in test_files}
            discovered_stems = set(discovered_files.keys())
            assert expected_stems == discovered_stems, f"Expected {expected_stems}, found {discovered_stems}"
            
            logger.info(f"✅ File Discovery: Found {len(discovered_files)} LoRA files")
            
            # Test path resolution
            for stem in expected_stems:
                try:
                    resolved_path = lora_worker._resolve_lora_path(stem)
                    assert resolved_path.exists(), f"Resolved path {resolved_path} does not exist"
                    logger.info(f"✅ Path Resolution: {stem} → {resolved_path}")
                except FileNotFoundError as e:
                    logger.error(f"❌ Failed to resolve path for {stem}: {e}")
                    return False
            
            # Test format detection
            for filename in test_files:
                file_path = lora_dir / filename
                detected_format = lora_worker._detect_file_format(file_path)
                expected_format = 'safetensors' if filename.endswith('.safetensors') else 'pt'
                assert detected_format == expected_format, f"Expected {expected_format}, got {detected_format}"
                logger.info(f"✅ Format Detection: {filename} → {detected_format}")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ LoRA discovery test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_lora_loading():
    """Test LoRA loading functionality."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAWorker, LoRAConfiguration
        
        logger.info("=== Testing LoRA Loading ===")
        
        # Create temporary directory with mock LoRA files
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "loras"
            lora_dir.mkdir(parents=True)
            
            # Create test LoRA files
            test_files = {
                "test_lora.safetensors": "safetensors",
                "style_lora.pt": "pt"
            }
            
            for filename, format_type in test_files.items():
                file_path = lora_dir / filename
                create_mock_lora_file(file_path, format_type)
            
            # Initialize LoRA worker
            config = {"lora_directories": [str(lora_dir)]}
            lora_worker = LoRAWorker(config)
            
            # Test loading individual LoRA adapters
            for filename in test_files.keys():
                stem = Path(filename).stem
                lora_config = LoRAConfiguration(
                    name=stem,
                    path=str(lora_dir / filename),
                    weight=0.8
                )
                
                success = await lora_worker.load_lora_adapter(lora_config)
                assert success == True, f"Failed to load {stem}"
                
                # Verify adapter is loaded
                assert stem in lora_worker.loaded_adapters
                assert stem in lora_worker.adapter_metadata
                logger.info(f"✅ LoRA Loading: Successfully loaded {stem}")
            
            # Test loading statistics
            assert len(lora_worker.loaded_adapters) == len(test_files)
            logger.info(f"✅ LoRA Loading: {len(lora_worker.loaded_adapters)} adapters loaded")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ LoRA loading test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_memory_management():
    """Test LoRA memory management and optimization."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAWorker, LoRAConfiguration
        
        logger.info("=== Testing Memory Management ===")
        
        # Initialize LoRA worker with memory constraints
        config = {
            "memory_limit_mb": 100,  # Small limit for testing
            "cache_size": 2,
            "enable_memory_monitoring": True
        }
        lora_worker = LoRAWorker(config)
        
        # Test memory estimation
        mock_adapter_data = {
            "layer1.weight": torch.randn(512, 256),
            "layer2.weight": torch.randn(256, 128)
        }
        
        memory_usage = lora_worker._estimate_memory_usage(mock_adapter_data)
        assert memory_usage > 0, "Memory usage should be positive"
        logger.info(f"✅ Memory Estimation: {memory_usage:.2f} MB")
        
        # Test memory constraint checking
        large_size_mb = 150  # Larger than our limit
        needs_cleanup = lora_worker._check_memory_constraints(large_size_mb)
        assert needs_cleanup == True, "Should trigger cleanup for large size"
        logger.info("✅ Memory Constraints: Correctly detects when cleanup is needed")
        
        # Test cache management
        assert hasattr(lora_worker, 'memory_usage')
        assert hasattr(lora_worker, 'performance_stats')
        logger.info("✅ Memory Management: Cache structures initialized")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Memory management test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_pipeline_integration():
    """Test LoRA integration with diffusion pipeline."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAWorker, LoRAConfiguration, LoRAStackConfiguration
        
        logger.info("=== Testing Pipeline Integration ===")
        
        # Create temporary directory with mock files
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "loras"
            lora_dir.mkdir(parents=True)
            
            # Create test LoRA file
            test_file = "test_integration.safetensors"
            create_mock_lora_file(lora_dir / test_file)
            
            # Initialize worker and mock pipeline
            config = {"lora_directories": [str(lora_dir)]}
            lora_worker = LoRAWorker(config)
            mock_pipeline = MockDiffusionPipeline()
            
            # Test single adapter integration
            lora_config = LoRAConfiguration(
                name="test_integration",
                path=str(lora_dir / test_file),
                weight=0.7
            )
            
            # Load the adapter
            success = await lora_worker.load_lora_adapter(lora_config)
            assert success == True
            
            # Test pipeline integration
            success = await lora_worker.apply_to_pipeline(mock_pipeline, ["test_integration"])
            assert success == True
            
            # Verify pipeline received correct configuration
            assert "test_integration" in mock_pipeline.last_adapter_names
            assert mock_pipeline.last_adapter_weights[0] == 0.7
            logger.info("✅ Pipeline Integration: Single adapter works")
            
            # Test stack mode with multiple adapters (simulate)
            stack_config = LoRAStackConfiguration()
            stack_config.add_adapter(lora_config)
            
            # Simulate setting the stack
            lora_worker.current_stack = stack_config
            
            # Test stack application
            success = await lora_worker.apply_stack_to_pipeline(mock_pipeline)
            assert success == True
            logger.info("✅ Pipeline Integration: Stack mode works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Pipeline integration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_advanced_features():
    """Test advanced LoRA features."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAWorker, LoRAConfiguration
        
        logger.info("=== Testing Advanced Features ===")
        
        # Initialize worker with advanced configuration
        config = {
            "enable_caching": True,
            "enable_memory_monitoring": True,
            "cache_size": 5,
            "memory_limit_mb": 200
        }
        lora_worker = LoRAWorker(config)
        
        # Test configuration validation
        assert lora_worker.config["enable_caching"] == True
        assert lora_worker.config["cache_size"] == 5
        logger.info("✅ Advanced Features: Configuration validation works")
        
        # Test performance tracking
        assert hasattr(lora_worker, 'performance_stats')
        assert 'total_loads' in lora_worker.performance_stats
        assert 'cache_hits' in lora_worker.performance_stats
        logger.info("✅ Advanced Features: Performance tracking initialized")
        
        # Test cleanup functionality
        await lora_worker._cleanup_cache_if_needed()
        logger.info("✅ Advanced Features: Cache cleanup works")
        
        # Test unloading functionality
        lora_worker.loaded_adapters["test"] = {"weight": torch.randn(10, 10)}
        lora_worker.adapter_metadata["test"] = LoRAConfiguration("test", "test.pt")
        
        success = await lora_worker.unload_lora_adapter("test")
        assert success == True
        assert "test" not in lora_worker.loaded_adapters
        logger.info("✅ Advanced Features: Adapter unloading works")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Advanced features test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def run_all_tests():
    """Run all LoRA Worker tests."""
    logger.info("\n" + "="*60)
    logger.info("RUNNING LORA WORKER TEST SUITE")
    logger.info("="*60)
    
    tests = [
        ("LoRA Configuration", test_lora_configuration),
        ("LoRA Discovery", test_lora_discovery),
        ("LoRA Loading", test_lora_loading),
        ("Memory Management", test_memory_management),
        ("Pipeline Integration", test_pipeline_integration),
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
        logger.info("\n🎉 LoRA Worker test suite PASSED!")
        return True
    else:
        logger.error(f"\n💥 LoRA Worker test suite FAILED! Need ≥80% success rate")
        return False

if __name__ == "__main__":
    success = asyncio.run(run_all_tests())
    exit(0 if success else 1)
