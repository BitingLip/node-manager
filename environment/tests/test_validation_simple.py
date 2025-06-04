"""
Simple test of validation scripts without Unicode emojis
"""

import subprocess
import sys
from pathlib import Path

def test_validation_script(script_name):
    """Test a single validation script"""
    script_path = Path("environment/validation_scripts") / script_name
    
    if not script_path.exists():
        return False, f"File not found: {script_path}"
    
    try:
        result = subprocess.run(
            [sys.executable, str(script_path)],
            capture_output=True,
            text=True,
            encoding='utf-8',
            errors='replace'
        )
        
        success = result.returncode == 0
        output = result.stdout if success else result.stderr
        
        return success, output.strip()[-200:]  # Last 200 chars
        
    except Exception as e:
        return False, str(e)

def main():
    """Test all validation scripts"""
    print("Testing Validation Scripts")
    print("=" * 50)
    
    scripts = [
        "validate_cpu.py",
        "validate_nvidia.py", 
        "validate_directml.py",
        "validate_rocm.py"
    ]
    
    results = {}
    
    for script in scripts:
        print(f"\nTesting {script}...")
        success, output = test_validation_script(script)
        results[script] = success
        
        status = "PASS" if success else "FAIL"
        print(f"  Status: {status}")
        print(f"  Output: {output[:100]}...")  # First 100 chars
    
    # Summary
    print(f"\nSummary:")
    print("=" * 50)
    
    passed = sum(1 for result in results.values() if result)
    total = len(results)
    
    for script, passed_test in results.items():
        status = "PASS" if passed_test else "FAIL"
        print(f"  {script:<25} {status}")
    
    print(f"\nOverall: {passed}/{total} scripts working")
    
    if passed >= 2:
        print("SUCCESS: Most validation scripts are working!")
    else:
        print("WARNING: Few validation scripts are working")

if __name__ == "__main__":
    main()
