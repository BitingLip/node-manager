"""
Simple __init__.py verification script
"""

import sys
import os
from pathlib import Path

def test_simple_import():
    """Test basic import functionality"""
    print("🧪 Simple __init__.py Verification")
    print("=" * 40)
    print()
    
    # Change to the environment directory
    original_cwd = os.getcwd()
    
    try:
        # Test 1: Check file existence
        init_files = [
            "__init__.py",
            "gpu/__init__.py", 
            "environment/__init__.py",
            "orchestrator/__init__.py",
            "environment/validation_scripts/__init__.py"
        ]
        
        print("📁 File Existence Check:")
        for init_file in init_files:
            if Path(init_file).exists():
                size = Path(init_file).stat().st_size
                print(f"  ✓ {init_file} ({size} bytes)")
            else:
                print(f"  ✗ {init_file} - NOT FOUND")
        
        print()
        
        # Test 2: Check content
        print("📝 Content Check:")
        for init_file in init_files:
            if Path(init_file).exists():
                with open(init_file, 'r') as f:
                    content = f.read()
                
                has_imports = 'from ' in content or 'import ' in content
                has_all = '__all__' in content
                has_docstring = '"""' in content or "'''" in content
                lines = len(content.split('\n'))
                
                print(f"  📄 {init_file}:")
                print(f"    Lines: {lines}")
                print(f"    Docstring: {'✓' if has_docstring else '✗'}")
                print(f"    Imports: {'✓' if has_imports else '✗'}")
                print(f"    Exports: {'✓' if has_all else '✗'}")
                print()
        
        # Test 3: Simple syntax check
        print("🔍 Syntax Check:")
        for init_file in init_files:
            if Path(init_file).exists():
                try:
                    with open(init_file, 'r') as f:
                        content = f.read()
                    compile(content, init_file, 'exec')
                    print(f"  ✓ {init_file} - Syntax OK")
                except SyntaxError as e:
                    print(f"  ✗ {init_file} - Syntax Error: {e}")
                except Exception as e:
                    print(f"  ⚠ {init_file} - Warning: {e}")
        
        print()
        print("✅ Basic __init__.py verification completed!")
        
    finally:
        os.chdir(original_cwd)

if __name__ == "__main__":
    test_simple_import()
