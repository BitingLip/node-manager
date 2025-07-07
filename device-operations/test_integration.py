#!/usr/bin/env python3
"""
Integration Test Script
======================

Test the corrected import structure and basic worker initialization.
"""

import sys
import os
from pathlib import Path

# Add the src directory to Python path
src_dir = Path(__file__).parent / "src"
sys.path.insert(0, str(src_dir))

def test_basic_imports():
    """Test basic imports without ML dependencies."""
    print("🧪 Testing basic imports...")
    
    try:
        # Test core communication
        from Workers.core.communication import CommunicationManager, MessageProtocol
        print("✅ Communication module imports work")
        
        # Test basic worker structure (without torch dependencies)
        from Workers.core.base_worker import ProcessingError
        print("✅ Base worker module imports work")
        
        # Test enhanced orchestrator
        from Workers.core.enhanced_orchestrator import EnhancedRequest
        print("✅ Enhanced orchestrator imports work")
        
        return True
        
    except Exception as e:
        print(f"❌ Import test failed: {str(e)}")
        import traceback
        traceback.print_exc()
        return False

def test_worker_structure():
    """Test the worker package structure."""
    print("\n🧪 Testing worker package structure...")
    
    try:
        # Test package initialization
        import Workers
        print("✅ Workers package loads correctly")
        
        # Test if we can create basic components
        from Workers.core.communication import CommunicationManager
        comm_manager = CommunicationManager()
        print("✅ Communication manager can be instantiated")
        
        # Test message protocol
        from Workers.core.communication import MessageProtocol
        test_request = MessageProtocol.create_request("test", {"data": "test"})
        print(f"✅ Message protocol works: {test_request['message_type']}")
        
        return True
        
    except Exception as e:
        print(f"❌ Package structure test failed: {str(e)}")
        import traceback
        traceback.print_exc()
        return False

def test_file_structure():
    """Test if all required files exist."""
    print("\n🧪 Testing file structure...")
    
    base_path = Path(__file__).parent / "src" / "Workers"
    required_files = [
        "run_worker.py",
        "__init__.py",
        "main.py",
        "core/__init__.py",
        "core/base_worker.py",
        "core/communication.py",
        "core/enhanced_orchestrator.py",
        "models/__init__.py", 
        "models/model_loader.py",
        "inference/__init__.py",
        "inference/sdxl_worker.py"
    ]
    
    missing_files = []
    for file_path in required_files:
        full_path = base_path / file_path
        if not full_path.exists():
            missing_files.append(str(full_path))
            print(f"❌ Missing: {file_path}")
        else:
            print(f"✅ Found: {file_path}")
    
    if missing_files:
        print(f"\n❌ Missing {len(missing_files)} required files")
        return False
    else:
        print(f"\n✅ All {len(required_files)} required files found")
        return True

def main():
    """Run all tests."""
    print("🚀 Starting Integration Tests for Device Operations Workers\n")
    
    tests = [
        ("File Structure", test_file_structure),
        ("Basic Imports", test_basic_imports),
        ("Worker Structure", test_worker_structure)
    ]
    
    passed = 0
    total = len(tests)
    
    for test_name, test_func in tests:
        print(f"\n{'='*50}")
        print(f"Running: {test_name}")
        print('='*50)
        
        if test_func():
            passed += 1
            print(f"✅ {test_name} PASSED")
        else:
            print(f"❌ {test_name} FAILED")
    
    print(f"\n{'='*50}")
    print(f"Test Results: {passed}/{total} passed")
    print('='*50)
    
    if passed == total:
        print("🎉 All integration tests passed!")
        print("✅ The fix for import paths and package structure is working correctly.")
        print("🔧 Ready for ML dependencies (torch, diffusers, etc.) to be installed.")
    else:
        print("⚠️  Some tests failed. Review the errors above.")
        
    return passed == total

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)