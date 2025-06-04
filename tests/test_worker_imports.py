#!/usr/bin/env python3
"""
Test script to verify worker module imports
"""
import sys
import os

# Add the project root to the Python path
project_root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(__file__))))
sys.path.insert(0, project_root)

try:
    # Test core imports
    from managers.node_manager.workers import BaseWorker, WorkerPool, PoolManager
    print("✅ Core imports successful")
    
    # Test inference imports
    from managers.node_manager.workers import BaseInferenceWorker
    print("✅ Inference imports successful")
    
    # Test registry imports
    from managers.node_manager.workers import WorkerRegistry
    print("✅ Registry imports successful")
    
    print("🎉 All imports working correctly!")
    
except ImportError as e:
    print(f"❌ Import error: {e}")
    
except Exception as e:
    print(f"❌ Unexpected error: {e}")
