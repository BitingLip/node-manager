#!/usr/bin/env python3
"""
Simple test runner for Node Manager components
Runs tests with correct import paths from node-manager directory
"""
import sys
import os
import asyncio

# Set up path for local imports
current_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, current_dir)

async def test_worker_imports():
    """Test worker imports"""
    print("🔧 Testing Worker Imports...")
    try:
        from workers import BaseWorker, WorkerPool, PoolManager, WorkerRegistry
        print("✅ Core worker classes imported successfully")
        
        from workers import BaseInferenceWorker
        print("✅ Inference worker classes imported successfully")
        
        return True
    except Exception as e:
        print(f"❌ Worker import failed: {e}")
        return False

async def test_core_imports():
    """Test core component imports"""
    print("\n🏗️ Testing Core Imports...")
    try:
        from core import NodeController, ResourceManager, WorkerManager, TaskDispatcher
        print("✅ Core components imported successfully")
        
        return True
    except Exception as e:
        print(f"❌ Core import failed: {e}")
        return False

async def test_monitoring_imports():
    """Test monitoring imports"""
    print("\n📊 Testing Monitoring Imports...")
    try:
        from monitoring import HealthMonitor, MetricsCollector
        print("✅ Monitoring components imported successfully")
        
        return True
    except Exception as e:
        print(f"❌ Monitoring import failed: {e}")
        return False

async def test_basic_functionality():
    """Test basic functionality of components"""
    print("\n⚙️ Testing Basic Functionality...")
    
    try:
        # Test metrics collector
        from monitoring import MetricsCollector
        collector = MetricsCollector(collection_interval=5)
        metrics = collector.collect_current_metrics()
        print(f"✅ MetricsCollector: CPU={metrics.cpu_usage:.1f}%, Memory={metrics.memory_used/(1024**3):.1f}GB")
        
        # Test health monitor
        from monitoring import HealthMonitor
        monitor = HealthMonitor()
        cpu_health = monitor._check_cpu_usage()
        print(f"✅ HealthMonitor: CPU health={cpu_health.status}")
        
        # Test worker pool
        from workers import WorkerPool
        # We can't fully test without actual worker class, but we can instantiate
        print("✅ WorkerPool: Class available for instantiation")
        
        return True
    except Exception as e:
        print(f"❌ Functionality test failed: {e}")
        return False

async def main():
    """Run all tests"""
    print("🧪 Node Manager Test Suite")
    print("=" * 50)
    
    tests = [
        test_worker_imports,
        test_core_imports, 
        test_monitoring_imports,
        test_basic_functionality
    ]
    
    passed = 0
    total = len(tests)
    
    for test in tests:
        try:
            if await test():
                passed += 1
            else:
                print("❌ Test failed")
        except Exception as e:
            print(f"❌ Test error: {e}")
    
    print("\n" + "=" * 50)
    print(f"Results: {passed}/{total} tests passed")
    
    if passed == total:
        print("🎉 All tests passed!")
        return True
    else:
        print("⚠️ Some tests failed")
        return False

if __name__ == "__main__":
    result = asyncio.run(main())
    sys.exit(0 if result else 1)
