#!/usr/bin/env python3
"""
Simple test script to verify worker module imports work correctly
"""
import sys
import os

# Add current directory to path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

def test_worker_imports():
    """Test worker imports"""
    try:
        # Test core worker imports
        from workers import BaseWorker, WorkerPool, PoolManager, WorkerRegistry
        print("✅ Core worker imports successful")
        print(f"   - BaseWorker: {BaseWorker}")
        print(f"   - WorkerPool: {WorkerPool}")
        print(f"   - PoolManager: {PoolManager}")
        print(f"   - WorkerRegistry: {WorkerRegistry}")
        
        # Test inference worker imports
        from workers import BaseInferenceWorker
        print("✅ Inference worker imports successful")
        print(f"   - BaseInferenceWorker: {BaseInferenceWorker}")
        
        return True
    except ImportError as e:
        print(f"❌ Worker import error: {e}")
        return False

def test_core_imports():
    """Test core imports"""
    try:
        from core import NodeController, ResourceManager, WorkerManager, TaskDispatcher
        print("✅ Core component imports successful")
        print(f"   - NodeController: {NodeController}")
        print(f"   - ResourceManager: {ResourceManager}")
        print(f"   - WorkerManager: {WorkerManager}")
        print(f"   - TaskDispatcher: {TaskDispatcher}")
        
        return True
    except ImportError as e:
        print(f"❌ Core import error: {e}")
        return False

def test_monitoring_imports():
    """Test monitoring imports"""
    try:
        from monitoring import HealthMonitor, MetricsCollector
        print("✅ Monitoring imports successful")
        print(f"   - HealthMonitor: {HealthMonitor}")
        print(f"   - MetricsCollector: {MetricsCollector}")
        
        return True
    except ImportError as e:
        print(f"❌ Monitoring import error: {e}")
        return False

if __name__ == "__main__":
    print("🧪 Node Manager Import Test")
    print("=" * 40)
    
    success_count = 0
    total_tests = 3
    
    if test_worker_imports():
        success_count += 1
    print()
    
    if test_core_imports():
        success_count += 1
    print()
    
    if test_monitoring_imports():
        success_count += 1
    print()
    
    print("=" * 40)
    print(f"Test Results: {success_count}/{total_tests} passed")
    
    if success_count == total_tests:
        print("🎉 All imports working correctly!")
        sys.exit(0)
    else:
        print("⚠️  Some imports failed")
        sys.exit(1)
