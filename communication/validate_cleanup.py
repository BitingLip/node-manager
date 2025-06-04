"""
Communication Layer Validation
Validates that all components are properly organized and functional
"""

import sys
import importlib
from pathlib import Path

def validate_imports():
    """Validate that all components can be imported"""
    print("🔍 Validating imports...")
    
    components = [
        'cluster_client.ClusterClient',
        'message_queue.MessageQueue', 
        'api_server.APIServer',
        'communication_coordinator.CommunicationCoordinator'
    ]
    
    for component in components:
        try:
            module_name, class_name = component.split('.')
            module = importlib.import_module(module_name)
            getattr(module, class_name)
            print(f"  ✅ {component}")
        except Exception as e:
            print(f"  ❌ {component}: {e}")
            return False
    
    return True

def validate_module_exports():
    """Validate that __init__.py exports all components"""
    print("\n🔍 Validating module exports...")
    
    try:
        import communication
        expected_exports = ['ClusterClient', 'APIServer', 'MessageQueue', 'CommunicationCoordinator']
        
        for export in expected_exports:
            if hasattr(communication, export):
                print(f"  ✅ {export}")
            else:
                print(f"  ❌ {export} not exported")
                return False
        
        return True
    except Exception as e:
        print(f"  ❌ Module import failed: {e}")
        return False

def validate_file_structure():
    """Validate directory structure"""
    print("\n🔍 Validating file structure...")
    
    current_dir = Path('.')
    expected_files = [
        '__init__.py',
        'cluster_client.py',
        'message_queue.py', 
        'api_server.py',
        'communication_coordinator.py',
        'README.md',
        'IMPLEMENTATION_SUMMARY.md',
        'COMPLETION_REPORT.md',
        'simple_working_test.py',
        'test_communication.py'
    ]
    
    for file_name in expected_files:
        file_path = current_dir / file_name
        if file_path.exists():
            print(f"  ✅ {file_name}")
        else:
            print(f"  ❌ {file_name} missing")
            return False
    
    # Check for unwanted files
    unwanted_patterns = ['*backup*', '*clean*', '*fixed*', '*new*', '__pycache__']
    unwanted_found = []
    
    for pattern in unwanted_patterns:
        unwanted_found.extend(current_dir.glob(pattern))
    
    if unwanted_found:
        print(f"  ⚠️  Found unwanted files: {[f.name for f in unwanted_found]}")
        return False
    
    return True

def validate_documentation():
    """Validate that documentation files exist and are not empty"""
    print("\n🔍 Validating documentation...")
    
    doc_files = ['README.md', 'IMPLEMENTATION_SUMMARY.md', 'COMPLETION_REPORT.md']
    
    for doc_file in doc_files:
        try:
            with open(doc_file, 'r', encoding='utf-8') as f:
                content = f.read().strip()
                if content:
                    print(f"  ✅ {doc_file} ({len(content)} chars)")
                else:
                    print(f"  ❌ {doc_file} is empty")
                    return False
        except Exception as e:
            print(f"  ❌ {doc_file}: {e}")
            return False
    
    return True

def main():
    """Run all validations"""
    print("🧹 Communication Layer Cleanup Validation")
    print("=" * 50)
    
    validations = [
        ("File Structure", validate_file_structure),
        ("Component Imports", validate_imports),
        ("Module Exports", validate_module_exports),
        ("Documentation", validate_documentation)
    ]
    
    results = []
    for name, validation_func in validations:
        try:
            result = validation_func()
            results.append((name, result))
        except Exception as e:
            print(f"  ❌ {name} validation failed: {e}")
            results.append((name, False))
    
    print("\n" + "=" * 50)
    print("📊 Validation Results:")
    
    passed = 0
    for name, result in results:
        status = "✅ PASS" if result else "❌ FAIL"
        print(f"  {name}: {status}")
        if result:
            passed += 1
    
    print(f"\nOverall: {passed}/{len(results)} validations passed")
    
    if passed == len(results):
        print("\n🎉 Communication layer is properly organized and ready!")
        print("💡 All components validated successfully!")
    else:
        print("\n⚠️  Some validations failed. Please check the issues above.")
    
    return passed == len(results)

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)
