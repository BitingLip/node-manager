#!/usr/bin/env python3
"""
Simple Node Manager Test Runner
"""

import asyncio
import sys
import logging
from pathlib import Path

# Add the project root to the path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

# Configure logging
logging.basicConfig(level=logging.INFO)


async def test_imports():
    """Test core imports"""
    print("\n=== Testing Core Imports ===")
    
    try:
        from workers.worker_factory import WorkerFactory, WorkerPool
        print("✓ WorkerFactory and WorkerPool imported")
        
        from workers.base_worker import BaseWorker, WorkerState
        print("✓ BaseWorker and WorkerState imported")
        
        from config.node_config import NodeConfig
        print("✓ NodeConfig imported")
        
        return True
    except Exception as e:
        print(f"✗ Import failed: {e}")
        return False


async def test_worker_factory():
    """Test worker factory"""
    print("\n=== Testing Worker Factory ===")
    
    try:
        from workers.worker_factory import WorkerFactory
        
        # Test getting available types
        types = WorkerFactory.get_available_types()
        print(f"✓ Available worker types: {types}")
        
        # Test creating a worker
        if 'tts' in types:
            if worker := WorkerFactory.create_worker('tts', 'test_tts', {'max_memory_mb': 2048}):
                print("✓ Created TTS worker successfully")
                caps = worker.get_capabilities()
                print(f"✓ Worker capabilities: {caps.get('supported_tasks', [])}")
            else:
                print("✗ Failed to create TTS worker")
                return False
        
        return True
    except Exception as e:
        print(f"✗ Worker factory test failed: {e}")
        return False


async def test_config():
    """Test configuration"""
    print("\n=== Testing Configuration ===")
    
    try:
        from config.node_config import NodeConfig
        
        config = NodeConfig()
        print("✓ Created NodeConfig")
        
        # Test config access
        node_id = config.get_config('node_id')
        print(f"✓ Node ID: {node_id}")
        
        return True
    except Exception as e:
        print(f"✗ Config test failed: {e}")
        return False


async def run_tests():
    """Run all tests"""
    print("Node Manager Test Suite")
    print("=" * 50)
    
    tests = [
        ("Imports", test_imports),
        ("Worker Factory", test_worker_factory),
        ("Configuration", test_config),
    ]
    
    results = []
    for test_name, test_func in tests:
        try:
            result = await test_func()
            results.append((test_name, result))
        except Exception as e:
            print(f"\n✗ {test_name} failed: {e}")
            results.append((test_name, False))
    
    print("\n" + "=" * 50)
    print("TEST SUMMARY")
    print("=" * 50)
    
    passed = [result for _, result in results].count(True)
    total = len(results)
    
    for test_name, result in results:
        status = "✓ PASS" if result else "✗ FAIL"
        print(f"{status:<8} {test_name}")
    
    print(f"\nRESULTS: {passed}/{total} tests passed")
    return passed == total


if __name__ == "__main__":
    try:
        success = asyncio.run(run_tests())
        sys.exit(0 if success else 1)
    except Exception as e:
        print(f"Test failed: {e}")
        sys.exit(1)
