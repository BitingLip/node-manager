#!/usr/bin/env python3
"""
Comprehensive __init__.py verification script
Tests all package imports and exports
"""

import sys
import traceback
from pathlib import Path

def test_package_import(package_path, package_name):
    """Test importing a package and check its exports"""
    old_path = sys.path[:]
    try:
        # Add the package directory to path temporarily
        sys.path.insert(0, str(package_path))
        
        # Import the package
        import importlib.util
        init_file = package_path / "__init__.py"
        spec = importlib.util.spec_from_file_location("test_package", init_file)
        
        if spec is None or spec.loader is None:
            raise ImportError(f"Could not load spec from {init_file}")
            
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        
        # Check exports
        exports = getattr(module, '__all__', [])
        version = getattr(module, '__version__', 'unknown')
        
        print(f"✓ {package_name}")
        print(f"  Exports: {len(exports)} items")
        print(f"  Items: {', '.join(exports)}")
        if hasattr(module, '__version__'):
            print(f"  Version: {version}")
        print()
        
        return True
        
    except Exception as e:
        print(f"✗ {package_name}")
        print(f"  Error: {str(e)}")
        print()
        return False
    finally:
        # Restore path
        sys.path[:] = old_path

def main():
    """Main test function"""
    print("🧪 Comprehensive __init__.py Verification")
    print("=" * 50)
    print()
    
    # Define packages to test
    packages = [
        (Path("."), "Main Environment Package"),
        (Path("gpu"), "GPU Detection Package"),
        (Path("environment"), "Environment Management Package"),
        (Path("orchestrator"), "Orchestrator Package"),
        (Path("environment/validation_scripts"), "Validation Scripts Package"),
    ]
    
    results = []
    
    for package_path, package_name in packages:
        if (package_path / "__init__.py").exists():
            success = test_package_import(package_path, package_name)
            results.append((package_name, success))
        else:
            print(f"✗ {package_name}")
            print(f"  Error: __init__.py not found at {package_path}")
            print()
            results.append((package_name, False))
    
    # Summary
    print("📊 Summary")
    print("=" * 30)
    
    total = len(results)
    passed = sum(1 for _, success in results if success)
    
    for package_name, success in results:
        status = "✓ PASS" if success else "✗ FAIL"
        print(f"  {package_name:<30} {status}")
    
    print()
    print(f"📈 Results: {passed}/{total} packages verified successfully")
    
    if passed == total:
        print("🎉 All __init__.py files are working correctly!")
    elif passed > 0:
        print("⚠️  Some packages have issues")
    else:
        print("💥 All packages failed verification")

if __name__ == "__main__":
    main()
