"""
Enhanced SDXL Worker LoRA Integration Test

Tests the integration between Enhanced SDXL Worker and LoRA Worker
for seamless LoRA adapter support in SDXL generation pipelines.
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
    """Mock diffusion pipeline for testing Enhanced SDXL Worker integration."""
    
    def __init__(self):
        self.loaded_adapters = {}
        self.adapter_weights = {}
        self.last_adapter_names = []
        self.last_adapter_weights = []
        self.scheduler = self
        self.config = {}
        
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

class MockEnhancedRequest:
    """Mock enhanced request for testing."""
    
    def __init__(self, **kwargs):
        self.prompt = kwargs.get('prompt', 'test prompt')
        self.scheduler = kwargs.get('scheduler', 'default')
        self.lora = kwargs.get('lora', None)
        
    @classmethod
    def from_dict(cls, data: Dict) -> 'MockEnhancedRequest':
        """Create from dictionary."""
        return cls(**data)

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

async def test_enhanced_worker_lora_basic_integration():
    """Test basic LoRA integration with Enhanced SDXL Worker."""
    try:
        # Mock the Enhanced SDXL Worker's LoRA configuration method
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAWorker, LoRAConfiguration
        
        logger.info("=== Testing Enhanced Worker LoRA Basic Integration ===")
        
        # Create temporary directory with mock LoRA files
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "loras"
            lora_dir.mkdir(parents=True)
            
            # Create test LoRA files
            test_file = "enhanced_test_lora.safetensors"
            create_mock_lora_file(lora_dir / test_file)
            
            # Initialize LoRA worker (simulating Enhanced Worker's initialization)
            config = {"lora_directories": [str(lora_dir)]}
            lora_worker = LoRAWorker(config)
            mock_pipeline = MockDiffusionPipeline()
            
            # Test LoRA configuration similar to Enhanced Worker's _configure_lora_adapters
            lora_config = {
                'enabled': True,
                'models': [
                    {
                        'name': 'enhanced_test_lora',
                        'path': str(lora_dir / test_file),
                        'weight': 0.8
                    }
                ],
                'global_weight': 1.2
            }
            
            # Simulate Enhanced Worker's LoRA configuration logic
            models = lora_config.get('models', [])
            global_weight = lora_config.get('global_weight', 1.0)
            adapter_names = []
            
            for model_config in models:
                name = model_config.get('name')
                path = model_config.get('path')
                weight = model_config.get('weight', 1.0)
                
                # Create LoRA configuration
                lora_adapter_config = LoRAConfiguration(
                    name=name,
                    path=path,
                    weight=weight * global_weight
                )
                
                # Load the adapter
                success = await lora_worker.load_lora_adapter(lora_adapter_config)
                assert success == True, f"Failed to load adapter {name}"
                adapter_names.append(name)
                logger.info(f"LoRA adapter loaded: {name} (weight: {weight * global_weight:.2f})")
            
            # Apply adapters to pipeline
            success = await lora_worker.apply_to_pipeline(mock_pipeline, adapter_names)
            assert success == True, "Failed to apply adapters to pipeline"
            
            # Verify pipeline integration
            assert "enhanced_test_lora" in mock_pipeline.last_adapter_names
            expected_weight = 0.8 * 1.2  # weight * global_weight
            assert abs(mock_pipeline.last_adapter_weights[0] - expected_weight) < 0.01
            
            logger.info("✅ Enhanced Worker LoRA Basic Integration: SUCCESS")
            return True
            
    except Exception as e:
        logger.error(f"❌ Enhanced Worker LoRA integration test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_enhanced_worker_lora_multiple_adapters():
    """Test multiple LoRA adapters integration."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAWorker, LoRAConfiguration
        
        logger.info("=== Testing Enhanced Worker Multiple LoRA Adapters ===")
        
        # Create temporary directory with multiple mock LoRA files
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "loras"
            lora_dir.mkdir(parents=True)
            
            # Create multiple test LoRA files
            test_files = {
                "style_lora.safetensors": "safetensors",
                "character_lora.pt": "pt",
                "lighting_lora.safetensors": "safetensors"
            }
            
            for filename, format_type in test_files.items():
                create_mock_lora_file(lora_dir / filename, format_type)
            
            # Initialize LoRA worker
            config = {"lora_directories": [str(lora_dir)]}
            lora_worker = LoRAWorker(config)
            mock_pipeline = MockDiffusionPipeline()
            
            # Test multiple LoRA configuration
            lora_config = {
                'enabled': True,
                'models': [
                    {'name': 'style_lora', 'weight': 0.7},
                    {'name': 'character_lora', 'weight': 0.9},
                    {'name': 'lighting_lora', 'weight': 0.5}
                ],
                'global_weight': 1.1
            }
            
            # Load and apply multiple adapters
            models = lora_config.get('models', [])
            global_weight = lora_config.get('global_weight', 1.0)
            adapter_names = []
            
            for model_config in models:
                name = model_config.get('name')
                weight = model_config.get('weight', 1.0)
                
                # Auto-discover path
                discovered_files = await lora_worker.discover_lora_files()
                if name not in discovered_files:
                    logger.warning(f"Adapter {name} not found in discovered files")
                    continue
                
                lora_adapter_config = LoRAConfiguration(
                    name=name,
                    path=str(discovered_files[name]),
                    weight=weight * global_weight
                )
                
                success = await lora_worker.load_lora_adapter(lora_adapter_config)
                assert success == True, f"Failed to load adapter {name}"
                adapter_names.append(name)
            
            # Apply all adapters to pipeline
            success = await lora_worker.apply_to_pipeline(mock_pipeline, adapter_names)
            assert success == True, "Failed to apply multiple adapters"
            
            # Verify all adapters were applied
            assert len(mock_pipeline.last_adapter_names) == 3
            assert len(mock_pipeline.last_adapter_weights) == 3
            
            # Verify weights are correctly calculated
            expected_weights = [0.7 * 1.1, 0.9 * 1.1, 0.5 * 1.1]
            for i, expected_weight in enumerate(expected_weights):
                actual_weight = mock_pipeline.last_adapter_weights[i]
                assert abs(actual_weight - expected_weight) < 0.01, f"Weight mismatch: {actual_weight} vs {expected_weight}"
            
            logger.info("✅ Enhanced Worker Multiple LoRA Adapters: SUCCESS")
            return True
            
    except Exception as e:
        logger.error(f"❌ Multiple LoRA adapters test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_enhanced_worker_lora_string_format():
    """Test LoRA configuration with simple string format."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAWorker, LoRAConfiguration
        
        logger.info("=== Testing Enhanced Worker LoRA String Format ===")
        
        # Create temporary directory with mock LoRA files
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "loras"
            lora_dir.mkdir(parents=True)
            
            # Create test LoRA file
            test_file = "simple_lora.safetensors"
            create_mock_lora_file(lora_dir / test_file)
            
            # Initialize LoRA worker
            config = {"lora_directories": [str(lora_dir)]}
            lora_worker = LoRAWorker(config)
            mock_pipeline = MockDiffusionPipeline()
            
            # Test simple string format configuration
            lora_config = {
                'enabled': True,
                'models': ['simple_lora'],  # Simple string format
                'global_weight': 1.0
            }
            
            # Process string format
            models = lora_config.get('models', [])
            global_weight = lora_config.get('global_weight', 1.0)
            adapter_names = []
            
            for model_config in models:
                if isinstance(model_config, str):
                    name = model_config
                    weight = 1.0
                else:
                    continue
                
                lora_adapter_config = LoRAConfiguration(
                    name=name,
                    path=name,  # Use name for auto-discovery
                    weight=weight * global_weight
                )
                
                success = await lora_worker.load_lora_adapter(lora_adapter_config)
                assert success == True, f"Failed to load adapter {name}"
                adapter_names.append(name)
            
            # Apply adapter to pipeline
            success = await lora_worker.apply_to_pipeline(mock_pipeline, adapter_names)
            assert success == True, "Failed to apply string format adapter"
            
            # Verify integration
            assert "simple_lora" in mock_pipeline.last_adapter_names
            assert mock_pipeline.last_adapter_weights[0] == 1.0
            
            logger.info("✅ Enhanced Worker LoRA String Format: SUCCESS")
            return True
            
    except Exception as e:
        logger.error(f"❌ LoRA string format test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def test_enhanced_worker_lora_error_handling():
    """Test LoRA error handling and graceful failures."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from lora_worker import LoRAWorker, LoRAConfiguration
        
        logger.info("=== Testing Enhanced Worker LoRA Error Handling ===")
        
        # Create temporary directory (no LoRA files)
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            lora_dir = temp_path / "loras"
            lora_dir.mkdir(parents=True)
            
            # Initialize LoRA worker
            config = {"lora_directories": [str(lora_dir)]}
            lora_worker = LoRAWorker(config)
            mock_pipeline = MockDiffusionPipeline()
            
            # Test configuration with non-existent LoRA
            lora_config = {
                'enabled': True,
                'models': [
                    {'name': 'nonexistent_lora', 'weight': 0.8}
                ],
                'global_weight': 1.0
            }
            
            # Process configuration with error handling
            models = lora_config.get('models', [])
            adapter_names = []
            
            for model_config in models:
                name = model_config.get('name')
                weight = model_config.get('weight', 1.0)
                
                lora_adapter_config = LoRAConfiguration(
                    name=name,
                    path=name,
                    weight=weight
                )
                
                try:
                    success = await lora_worker.load_lora_adapter(lora_adapter_config)
                    if success:
                        adapter_names.append(name)
                    else:
                        logger.warning(f"Failed to load LoRA adapter: {name}")
                except Exception as e:
                    logger.warning(f"Error loading LoRA adapter {name}: {e}")
            
            # Should handle empty adapter list gracefully
            if adapter_names:
                success = await lora_worker.apply_to_pipeline(mock_pipeline, adapter_names)
            else:
                success = True  # No adapters to apply is not an error
            
            assert success == True, "Error handling should not cause failures"
            assert len(adapter_names) == 0, "No adapters should have been loaded"
            
            logger.info("✅ Enhanced Worker LoRA Error Handling: SUCCESS")
            return True
            
    except Exception as e:
        logger.error(f"❌ LoRA error handling test failed: {e}")
        import traceback
        traceback.print_exc()
        return False

async def run_integration_tests():
    """Run all Enhanced SDXL Worker LoRA integration tests."""
    logger.info("\n" + "="*70)
    logger.info("RUNNING ENHANCED SDXL WORKER LORA INTEGRATION TESTS")
    logger.info("="*70)
    
    tests = [
        ("Basic LoRA Integration", test_enhanced_worker_lora_basic_integration),
        ("Multiple LoRA Adapters", test_enhanced_worker_lora_multiple_adapters),
        ("String Format Support", test_enhanced_worker_lora_string_format),
        ("Error Handling", test_enhanced_worker_lora_error_handling)
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
    logger.info("INTEGRATION TEST RESULTS SUMMARY")
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
        logger.info("\n🎉 Enhanced SDXL Worker LoRA Integration tests PASSED!")
        return True
    else:
        logger.error(f"\n💥 Enhanced SDXL Worker LoRA Integration tests FAILED! Need ≥80% success rate")
        return False

if __name__ == "__main__":
    success = asyncio.run(run_integration_tests())
    exit(0 if success else 1)
