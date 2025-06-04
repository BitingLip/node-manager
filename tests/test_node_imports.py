"""
Test script to verify node manager imports work correctly
"""

import sys
import os

# Add the parent directory to Python path for relative imports
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

def test_worker_imports():
    """Test that worker imports work"""
    try:
        from workers import BaseWorker, WorkerPool, PoolManager, WorkerRegistry
        print("✅ Worker imports successful")
        print(f"   - BaseWorker: {BaseWorker}")
        print(f"   - WorkerPool: {WorkerPool}")
        print(f"   - PoolManager: {PoolManager}")
        print(f"   - WorkerRegistry: {WorkerRegistry}")
        return True
    except ImportError as e:
        print(f"❌ Worker imports failed: {e}")
        return False

def test_core_imports():
    """Test that core imports work"""
    try:
        from core import NodeController, ResourceManager, WorkerManager, TaskDispatcher
        print("✅ Core imports successful")
        print(f"   - NodeController: {NodeController}")
        print(f"   - ResourceManager: {ResourceManager}")
        print(f"   - WorkerManager: {WorkerManager}")
        print(f"   - TaskDispatcher: {TaskDispatcher}")
        return True
    except ImportError as e:
        print(f"❌ Core imports failed: {e}")
        return False

def test_node_manager_import():
    """Test that the main node manager module can be imported"""
    try:
        # We need to go up one directory to import the node manager
        sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
        
        # Import should work but some components might be None
        import node_manager
        print("✅ Node manager module import successful")
        
        # Check what's available
        available_components = []
        unavailable_components = []
        
        for component in ['NodeController', 'ResourceManager', 'WorkerManager', 'TaskDispatcher',
                         'BaseWorker', 'WorkerPool', 'PoolManager', 'WorkerRegistry']:
            if hasattr(node_manager, component) and getattr(node_manager, component) is not None:
                available_components.append(component)
            else:
                unavailable_components.append(component)
        
        print(f"   Available components: {available_components}")
        if unavailable_components:
            print(f"   Unavailable components: {unavailable_components}")
        
        return True
    except ImportError as e:
        print(f"❌ Node manager import failed: {e}")
        return False

if __name__ == "__main__":
    print("Testing Node Manager Imports...")
    print("=" * 50)
    
    success_count = 0
    total_tests = 3
    
    if test_worker_imports():
        success_count += 1
    print()
    
    if test_core_imports():
        success_count += 1
    print()
    
    if test_node_manager_import():
        success_count += 1
    print()
    
    print("=" * 50)
    print(f"Test Results: {success_count}/{total_tests} passed")
    
    if success_count == total_tests:
        print("🎉 All imports working correctly!")
    else:
        print("⚠️  Some imports need attention")
