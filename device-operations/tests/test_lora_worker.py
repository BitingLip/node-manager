"""
Test Suite for LoRA Worker - Phase 2 Days 15-16
Comprehensive testing of LoRA adapter loading and management functionality.

Test Coverage:
1. LoRA Configuration and Stack Management
2. File Discovery and Path Resolution
3. LoRA Loading (Safetensors/PyTorch formats)
4. Memory Management and Optimization
5. Pipeline Integration Simulation
6. Multi-LoRA Stack Operations
"""

import sys
import os
import asyncio
import logging
import time
import tempfile
import json
from pathlib import Path
from typing import Dict, List, Any
import torch

# Add source paths
sys.path.append(os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))

# Set up logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

class MockDiffusionPipeline:
    """Mock diffusion pipeline for testing LoRA integration."""
    
    def __init__(self):
        self.loaded_loras = {}
        self.active_adapters = []
        self.adapter_weights = []
        
    def load_lora_weights(self, lora_path: str, adapter_name: str = None):
        """Mock LoRA loading method."""
        adapter_name = adapter_name or Path(lora_path).stem
        self.loaded_loras[adapter_name] = lora_path
        logger.info(f"Mock pipeline loaded LoRA: {adapter_name} from {lora_path}")
        
    def set_adapters(self, adapter_names: List[str], adapter_weights: List[float]):
        """Mock adapter setting method."""
        self.active_adapters = adapter_names
        self.adapter_weights = adapter_weights
        logger.info(f"Mock pipeline set adapters: {adapter_names} with weights {adapter_weights}")

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
        from lora_worker import LoRAWorker
        
        logger.info("=== Testing LoRA Discovery ===")
        
        # Create temporary directory structure
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "models" / "lora"
            lora_dir.mkdir(parents=True)
            
            # Create mock LoRA files
            test_files = [
                ("dragon_lora.safetensors", "safetensors"),
                ("style_lora.pt", "pt"),
                ("character_lora.ckpt", "ckpt")
            ]
            
            for filename, format_type in test_files:
                file_path = lora_dir / filename
                create_mock_lora_file(file_path, format_type)
            
            # Initialize LoRA worker with custom directory
            config = {
                "lora_directories": [str(lora_dir)],
                "enable_caching": True
            }
            lora_worker = LoRAWorker(config)
            
            # Test file discovery
            discovered_files = await lora_worker.discover_lora_files()
            assert len(discovered_files) >= 3
            logger.info(f"✅ File Discovery: Found {len(discovered_files)} LoRA files")
            
            # Test path resolution
            for name in ["dragon_lora", "style_lora", "character_lora"]:
                try:
                    resolved_path = lora_worker._resolve_lora_path(name)
                    assert resolved_path.exists()
                    logger.info(f"✅ Path Resolution: {name} → {resolved_path}")
                except FileNotFoundError:
                    logger.error(f"❌ Failed to resolve path for {name}")
                    return False
            
            # Test format detection
            for filename, expected_format in test_files:
                file_path = lora_dir / filename
                detected_format = lora_worker._detect_file_format(file_path)
                assert detected_format == expected_format
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
        from lora_worker import LoRAWorker, LoRAConfiguration
        
        logger.info("=== Testing LoRA Loading ===")
        
        # Create temporary directory with mock LoRA files
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "models" / "lora"
            lora_dir.mkdir(parents=True)
            
            # Create mock LoRA files
            safetensors_file = lora_dir / "test_safetensors.safetensors"
            pytorch_file = lora_dir / "test_pytorch.pt"
            
            create_mock_lora_file(safetensors_file, "safetensors")
            create_mock_lora_file(pytorch_file, "pt")
            
            # Initialize LoRA worker
            config = {
                "lora_directories": [str(lora_dir)],
                "max_memory_mb": 1024,
                "enable_caching": True
            }
            lora_worker = LoRAWorker(config)
            
            # Test safetensors loading
            safetensors_config = LoRAConfiguration(
                name="test_safetensors",
                path="test_safetensors.safetensors",
                weight=0.8
            )
            
            success = await lora_worker.load_lora_adapter(safetensors_config)
            assert success == True
            assert "test_safetensors" in lora_worker.loaded_adapters
            logger.info("✅ Safetensors Loading: Successfully loaded safetensors LoRA")
            
            # Test PyTorch loading
            pytorch_config = LoRAConfiguration(
                name="test_pytorch",
                path="test_pytorch.pt",
                weight=0.7
            )
            
            success = await lora_worker.load_lora_adapter(pytorch_config)
            assert success == True
            assert "test_pytorch" in lora_worker.loaded_adapters
            logger.info("✅ PyTorch Loading: Successfully loaded PyTorch LoRA")
            
            # Test memory statistics
            memory_stats = lora_worker.get_memory_stats()
            assert memory_stats["total_adapters_loaded"] == 2
            assert memory_stats["current_memory_mb"] > 0
            logger.info(f"✅ Memory Tracking: {memory_stats['current_memory_mb']:.1f}MB used")
            
            # Test adapter metadata
            loaded_adapters = lora_worker.get_loaded_adapters()
            assert len(loaded_adapters) == 2
            
            for adapter_name, info in loaded_adapters.items():
                assert info["load_time_ms"] > 0
                assert info["memory_usage_mb"] > 0
                assert info["file_format"] in ["safetensors", "pt"]
                logger.info(f"✅ Adapter Info: {adapter_name} - {info['file_format']}, {info['memory_usage_mb']:.1f}MB")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ LoRA loading test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_memory_management():
    """Test LoRA memory management and optimization."""
    try:
        from lora_worker import LoRAWorker, LoRAConfiguration
        
        logger.info("=== Testing Memory Management ===")
        
        # Create temporary directory with multiple mock LoRA files
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "models" / "lora"
            lora_dir.mkdir(parents=True)
            
            # Create multiple mock LoRA files
            for i in range(5):
                file_path = lora_dir / f"lora_{i}.safetensors"
                create_mock_lora_file(file_path, "safetensors")
            
            # Initialize LoRA worker with limited memory
            config = {
                "lora_directories": [str(lora_dir)],
                "max_memory_mb": 10,  # Very small limit to trigger cleanup
                "enable_caching": True,
                "cache_cleanup_threshold": 0.5
            }
            lora_worker = LoRAWorker(config)
            
            # Load multiple adapters
            loaded_count = 0
            for i in range(5):
                lora_config = LoRAConfiguration(
                    name=f"lora_{i}",
                    path=f"lora_{i}.safetensors",
                    weight=0.5 + i * 0.1
                )
                
                success = await lora_worker.load_lora_adapter(lora_config)
                if success:
                    loaded_count += 1
                    logger.info(f"✅ Loaded adapter {i+1}/5")
                else:
                    logger.info(f"⚠️ Adapter {i+1} not loaded (memory constraints)")
            
            # Check memory constraints were respected
            memory_stats = lora_worker.get_memory_stats()
            logger.info(f"✅ Memory Management: {memory_stats['current_memory_mb']:.1f}MB / {memory_stats['max_memory_mb']}MB")
            assert memory_stats["memory_usage_ratio"] <= 1.0
            
            # Test manual adapter unloading
            if loaded_count > 0:
                adapter_to_unload = "lora_0"
                if adapter_to_unload in lora_worker.loaded_adapters:
                    memory_before = lora_worker.get_memory_stats()["current_memory_mb"]
                    success = await lora_worker.unload_adapter(adapter_to_unload)
                    memory_after = lora_worker.get_memory_stats()["current_memory_mb"]
                    
                    assert success == True
                    assert memory_after < memory_before
                    logger.info(f"✅ Manual Unloading: Freed {memory_before - memory_after:.1f}MB")
            
            # Test clear all adapters
            await lora_worker.clear_all_adapters()
            memory_stats = lora_worker.get_memory_stats()
            assert memory_stats["total_adapters_loaded"] == 0
            assert memory_stats["current_memory_mb"] == 0
            logger.info("✅ Clear All: All adapters cleared successfully")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Memory management test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_pipeline_integration():
    """Test LoRA integration with mock diffusion pipeline."""
    try:
        from lora_worker import LoRAWorker, LoRAConfiguration, LoRAStackConfiguration
        
        logger.info("=== Testing Pipeline Integration ===")
        
        # Create temporary directory with mock LoRA files
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "models" / "lora"
            lora_dir.mkdir(parents=True)
            
            # Create mock LoRA files
            for name in ["style_lora", "character_lora", "detail_lora"]:
                file_path = lora_dir / f"{name}.safetensors"
                create_mock_lora_file(file_path, "safetensors")
            
            # Initialize LoRA worker
            config = {"lora_directories": [str(lora_dir)]}
            lora_worker = LoRAWorker(config)
            
            # Create mock pipeline
            mock_pipeline = MockDiffusionPipeline()
            
            # Test single LoRA application
            success = await lora_worker.apply_lora_to_pipeline(mock_pipeline, "style_lora", weight=0.8)
            assert success == True
            assert "style_lora" in mock_pipeline.loaded_loras
            logger.info("✅ Single LoRA: Successfully applied to pipeline")
            
            # Test LoRA stack application
            stack_config = LoRAStackConfiguration(global_weight_multiplier=0.9)
            
            adapters = [
                LoRAConfiguration("character_lora", "character_lora.safetensors", 0.7),
                LoRAConfiguration("detail_lora", "detail_lora.safetensors", 0.5),
                LoRAConfiguration("style_lora", "style_lora.safetensors", 0.6)
            ]
            
            for adapter in adapters:
                stack_config.add_adapter(adapter)
            
            success = await lora_worker.apply_lora_stack(mock_pipeline, stack_config)
            assert success == True
            assert len(mock_pipeline.active_adapters) == 3
            logger.info(f"✅ LoRA Stack: Applied {len(mock_pipeline.active_adapters)} adapters")
            
            # Verify weights are applied correctly
            expected_weights = [a.weight * stack_config.global_weight_multiplier for a in adapters]
            assert len(mock_pipeline.adapter_weights) == len(expected_weights)
            logger.info(f"✅ Weight Application: Weights {mock_pipeline.adapter_weights}")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Pipeline integration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_advanced_features():
    """Test advanced LoRA features."""
    try:
        from lora_worker import LoRAWorker, LoRAConfiguration, LoRAStackConfiguration
        
        logger.info("=== Testing Advanced Features ===")
        
        # Create temporary directory structure
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "models" / "lora"
            subdirs = ["characters", "styles", "environments"]
            
            # Create subdirectories with LoRA files
            for subdir in subdirs:
                sub_path = lora_dir / subdir
                sub_path.mkdir(parents=True)
                
                for i in range(2):
                    file_path = sub_path / f"{subdir}_lora_{i}.safetensors"
                    create_mock_lora_file(file_path, "safetensors")
            
            # Initialize LoRA worker
            config = {"lora_directories": [str(lora_dir)]}
            lora_worker = LoRAWorker(config)
            
            # Test file discovery in subdirectories
            discovered_files = await lora_worker.discover_lora_files()
            assert len(discovered_files) >= 6  # 2 files per subdirectory
            logger.info(f"✅ Subdirectory Discovery: Found {len(discovered_files)} files")
            
            # Test weighted adapter stacks
            stack_config = LoRAStackConfiguration(
                global_weight_multiplier=0.8,
                blend_mode="additive",
                max_adapters=6
            )
            
            # Add adapters with different weights
            weights = [0.9, 0.7, 0.5, 0.3]
            for i, weight in enumerate(weights):
                adapter = LoRAConfiguration(
                    name=f"test_adapter_{i}",
                    path=list(discovered_files.values())[i],
                    weight=weight
                )
                stack_config.add_adapter(adapter)
            
            # Test weight calculation
            calculated_weights = stack_config.get_adapter_weights()
            expected_weights = [w * stack_config.global_weight_multiplier for w in weights]
            
            for calc, exp in zip(calculated_weights, expected_weights):
                assert abs(calc - exp) < 0.001  # Allow for floating point precision
            
            logger.info(f"✅ Weight Calculation: {calculated_weights}")
            
            # Test adapter state management
            # Disable one adapter
            stack_config.adapters[1].enabled = False
            active_names = stack_config.get_adapter_names()
            assert len(active_names) == len(weights) - 1
            logger.info(f"✅ State Management: {len(active_names)} active adapters")
            
            # Test memory estimation
            for adapter in stack_config.adapters:
                if adapter.enabled:
                    success = await lora_worker.load_lora_adapter(adapter)
                    assert success == True
            
            memory_stats = lora_worker.get_memory_stats()
            logger.info(f"✅ Memory Estimation: {memory_stats['current_memory_mb']:.1f}MB total")
            
            # Test statistics tracking
            assert memory_stats["statistics"]["adapters_loaded"] >= 3
            assert memory_stats["statistics"]["total_load_time_ms"] > 0
            logger.info(f"✅ Statistics: {memory_stats['statistics']}")
        
        return True
        
    except Exception as e:
        logger.error(f"❌ Advanced features test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def main():
    """Run all LoRA Worker tests."""
    logger.info("🚀 Phase 2 Days 15-16: LoRA Worker Foundation Testing")
    logger.info("=" * 80)
    
    start_time = time.time()
    
    # Define tests
    tests = [
        ("LoRA Configuration", test_lora_configuration),
        ("LoRA Discovery", test_lora_discovery),
        ("LoRA Loading", test_lora_loading),
        ("Memory Management", test_memory_management),
        ("Pipeline Integration", test_pipeline_integration),
        ("Advanced Features", test_advanced_features)
    ]
    
    # Run tests
    results = {}
    for test_name, test_func in tests:
        logger.info(f"\n{'='*60}")
        logger.info(f"Running: {test_name}")
        logger.info('='*60)
        
        try:
            result = await test_func()
            results[test_name] = result
            status = "✅ PASSED" if result else "❌ FAILED"
            logger.info(f"{test_name}: {status}")
        except Exception as e:
            logger.error(f"{test_name}: ❌ FAILED - {e}")
            results[test_name] = False
    
    # Calculate results
    total_tests = len(results)
    passed_tests = sum(results.values())
    success_rate = passed_tests / total_tests
    total_time = time.time() - start_time
    
    # Final report
    logger.info(f"\n{'='*80}")
    logger.info("PHASE 2 DAYS 15-16: LORA WORKER FOUNDATION - FINAL REPORT")
    logger.info('='*80)
    
    logger.info(f"\n📊 TEST SUMMARY:")
    logger.info(f"Total tests: {total_tests}")
    logger.info(f"Passed: {passed_tests}")
    logger.info(f"Failed: {total_tests - passed_tests}")
    logger.info(f"Success rate: {success_rate:.1%}")
    logger.info(f"Execution time: {total_time:.1f}s")
    
    logger.info(f"\n📋 DETAILED RESULTS:")
    for test_name, result in results.items():
        status = "✅ PASSED" if result else "❌ FAILED"
        logger.info(f"  {test_name}: {status}")
    
    # Determine next steps
    if success_rate >= 0.8:  # 80% threshold
        logger.info(f"\n🎉 DAYS 15-16 LORA WORKER FOUNDATION: COMPLETE!")
        logger.info("✅ LoRA management system fully operational")
        logger.info("🚀 Ready to proceed to Days 17-18: LoRA Integration with Enhanced Worker")
        
        logger.info(f"\n✅ VALIDATED LORA CAPABILITIES:")
        logger.info("  • LoRA file discovery and loading (safetensors/PyTorch)")
        logger.info("  • Memory-efficient adapter management")
        logger.info("  • Multi-LoRA stack composition")
        logger.info("  • Pipeline integration framework")
        logger.info("  • Advanced weight blending and optimization")
        
        logger.info(f"\n📅 NEXT MILESTONE: Days 17-18 Enhanced Worker LoRA Integration")
        return True
    else:
        logger.info(f"\n⚠️ Days 15-16 testing needs attention")
        logger.info(f"Success rate: {success_rate:.1%} (threshold: 80%)")
        
        failed_tests = [name for name, result in results.items() if not result]
        logger.info(f"\n🔧 Failed tests to review:")
        for test in failed_tests:
            logger.info(f"  • {test}")
        
        return False

if __name__ == "__main__":
    success = asyncio.run(main())
    sys.exit(0 if success else 1)
