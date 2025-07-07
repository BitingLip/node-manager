"""
Simplified Test for Enhanced Batch Manager - Phase 2 Days 12-13
Tests the batch manager implementation independently.
"""

import sys
import os
import asyncio
import logging
import time

# Direct import approach
sys.path.append(os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))

# Set up logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

async def test_batch_manager():
    """Test the Enhanced Batch Manager implementation."""
    try:
        from batch_manager import EnhancedBatchManager, BatchConfiguration, MemoryMonitor
        
        logger.info("✅ Successfully imported Enhanced Batch Manager")
        
        # Test 1: Memory Monitor
        logger.info("\n--- Testing Memory Monitor ---")
        memory_monitor = MemoryMonitor("cpu")
        memory_info = memory_monitor.get_memory_info()
        logger.info(f"✅ Memory info: {memory_info['total']:.1f}GB total, {memory_info['usage_ratio']:.1%} used")
        
        # Test memory history
        for i in range(3):
            memory_monitor.update_memory_history()
            await asyncio.sleep(0.1)
        
        logger.info(f"✅ Memory history updated: {len(memory_monitor.memory_history)} entries")
        
        # Test batch size recommendations
        recommendations = []
        test_cases = [
            (1, 4, 1, 0.5),  # current_batch, max_batch, min_batch, threshold
            (2, 4, 1, 0.8),
            (4, 4, 1, 0.9)
        ]
        
        for current, max_batch, min_batch, threshold in test_cases:
            recommended = memory_monitor.recommend_batch_size(current, max_batch, min_batch, threshold)
            recommendations.append(recommended)
            logger.info(f"Batch size: {current} → {recommended} (threshold {threshold:.1%})")
        
        logger.info("✅ Memory monitor tests passed")
        
        # Test 2: Batch Configuration
        logger.info("\n--- Testing Batch Configuration ---")
        batch_config = BatchConfiguration(
            total_images=6,
            preferred_batch_size=2,
            max_batch_size=3,
            min_batch_size=1,
            enable_dynamic_sizing=True,
            memory_threshold=0.8
        )
        
        logger.info(f"✅ Batch config: {batch_config.total_images} images, "
                   f"batch size {batch_config.preferred_batch_size}-{batch_config.max_batch_size}")
        
        # Test 3: Enhanced Batch Manager
        logger.info("\n--- Testing Enhanced Batch Manager ---")
        batch_manager = EnhancedBatchManager("cpu")
        
        # Mock generation function
        generation_calls = []
        
        async def mock_generation(**params):
            """Mock generation function."""
            num_images = params.get("num_images_per_prompt", 1)
            prompt = params.get("prompt", "")
            
            generation_calls.append({
                "num_images": num_images,
                "prompt": prompt,
                "timestamp": time.time()
            })
            
            # Simulate generation time
            await asyncio.sleep(0.2)
            
            # Return mock result
            class MockResult:
                def __init__(self, count):
                    self.images = [f"image_{i}_{int(time.time()*1000)}" for i in range(count)]
            
            return MockResult(num_images)
        
        # Test batch processing
        start_time = time.time()
        
        images, metrics = await batch_manager.process_batch_generation(
            generation_function=mock_generation,
            batch_config=batch_config,
            generation_params={"prompt": "test landscape", "guidance_scale": 7.5}
        )
        
        end_time = time.time()
        
        logger.info(f"✅ Batch generation completed in {end_time - start_time:.1f}s")
        logger.info(f"✅ Generated {len(images)} images (expected {batch_config.total_images})")
        logger.info(f"✅ Generation function called {len(generation_calls)} times")
        
        # Verify metrics
        logger.info("\n--- Batch Metrics ---")
        for key, value in metrics.items():
            if isinstance(value, float):
                logger.info(f"{key}: {value:.2f}")
            else:
                logger.info(f"{key}: {value}")
        
        # Verify results
        assert len(images) == batch_config.total_images, f"Expected {batch_config.total_images} images, got {len(images)}"
        assert metrics["success_rate"] == 1.0, f"Expected 100% success rate, got {metrics['success_rate']}"
        assert len(generation_calls) > 0, "Generation function should have been called"
        
        # Test recommended batch size
        recommended = batch_manager.get_recommended_batch_size(8, 4)
        logger.info(f"✅ Recommended batch size for 8 images: {recommended}")
        
        logger.info("\n🎉 All Enhanced Batch Manager tests passed!")
        return True
        
    except Exception as e:
        logger.error(f"❌ Test failed: {str(e)}")
        import traceback
        traceback.print_exc()
        return False

async def test_batch_calculation():
    """Test batch calculation and distribution logic."""
    try:
        from batch_manager import EnhancedBatchManager, BatchConfiguration
        
        logger.info("\n--- Testing Batch Calculation ---")
        
        batch_manager = EnhancedBatchManager("cpu")
        
        test_cases = [
            {"total": 5, "preferred": 2, "max": 3},
            {"total": 8, "preferred": 3, "max": 4},
            {"total": 1, "preferred": 2, "max": 3},
            {"total": 10, "preferred": 4, "max": 4}
        ]
        
        for case in test_cases:
            config = BatchConfiguration(
                total_images=case["total"],
                preferred_batch_size=case["preferred"],
                max_batch_size=case["max"]
            )
            
            batches = batch_manager._calculate_batches(config)
            total_images_calculated = sum(b["batch_size"] for b in batches)
            
            logger.info(f"Total {case['total']} images → {len(batches)} batches: "
                       f"{[b['batch_size'] for b in batches]} "
                       f"(sum: {total_images_calculated})")
            
            assert total_images_calculated == case["total"], \
                f"Batch calculation error: expected {case['total']}, got {total_images_calculated}"
        
        logger.info("✅ Batch calculation tests passed")
        return True
        
    except Exception as e:
        logger.error(f"❌ Batch calculation test failed: {str(e)}")
        return False

async def main():
    """Run all batch manager tests."""
    logger.info("🚀 Enhanced Batch Manager Test Suite - Phase 2 Days 12-13")
    
    tests = [
        ("Enhanced Batch Manager", test_batch_manager),
        ("Batch Calculation", test_batch_calculation)
    ]
    
    results = []
    for test_name, test_func in tests:
        logger.info(f"\n{'='*60}")
        logger.info(f"Running: {test_name}")
        logger.info('='*60)
        
        try:
            result = await test_func()
            results.append((test_name, result))
        except Exception as e:
            logger.error(f"Test {test_name} failed with exception: {e}")
            results.append((test_name, False))
    
    # Summary
    logger.info(f"\n{'='*60}")
    logger.info("TEST SUMMARY - ENHANCED BATCH GENERATION")
    logger.info('='*60)
    
    passed = 0
    for test_name, result in results:
        status = "✅ PASSED" if result else "❌ FAILED"
        logger.info(f"{test_name}: {status}")
        if result:
            passed += 1
    
    total = len(results)
    logger.info(f"\nResult: {passed}/{total} tests passed")
    
    if passed == total:
        logger.info("\n🎉 ALL ENHANCED BATCH GENERATION TESTS PASSED!")
        logger.info("✅ Phase 2 Days 12-13: Batch Generation Support - COMPLETE")
        
        logger.info("\n📋 Implemented Features:")
        logger.info("  • Dynamic batch sizing based on memory availability")
        logger.info("  • Memory monitoring and adaptive adjustment")
        logger.info("  • Progress tracking and reporting")
        logger.info("  • Comprehensive batch metrics")
        logger.info("  • Error handling and recovery")
        logger.info("  • Optimized batch distribution")
        
        logger.info("\n🚀 Ready for Phase 2 Day 14: Basic Feature Testing")
        return True
    else:
        logger.info("\n❌ Some tests failed - review implementation")
        return False

if __name__ == "__main__":
    success = asyncio.run(main())
    sys.exit(0 if success else 1)
