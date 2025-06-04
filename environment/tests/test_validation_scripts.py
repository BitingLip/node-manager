"""
Test all validation scripts to confirm they're working properly
"""

import asyncio
import sys
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent))

async def test_all_validation_scripts():
    """Test all validation scripts"""
    print("🧪 Testing All Validation Scripts")
    print("=" * 60)
    
    scripts = [
        "validate_cpu.py",
        "validate_nvidia.py", 
        "validate_directml.py",
        "validate_rocm.py"
    ]
    
    results = {}
    
    for script in scripts:
        script_path = Path("environment/validation_scripts") / script
        
        if script_path.exists():
            print(f"\n🔍 Testing {script}...")
            
            try:
                proc = await asyncio.create_subprocess_exec(
                    sys.executable, str(script_path),
                    stdout=asyncio.subprocess.PIPE,
                    stderr=asyncio.subprocess.PIPE
                )
                out, err = await proc.communicate()
                
                status = "✅ PASS" if proc.returncode == 0 else "❌ FAIL"
                results[script] = proc.returncode == 0
                
                print(f"   Status: {status}")
                if proc.returncode == 0:
                    print(f"   Output: {out.decode().strip()[-100:]}")  # Last 100 chars
                else:
                    print(f"   Error: {err.decode().strip()[-100:]}")   # Last 100 chars
                    
            except Exception as e:
                results[script] = False
                print(f"   Status: ❌ EXCEPTION")
                print(f"   Error: {str(e)}")
        else:
            results[script] = False
            print(f"\n❌ {script} - FILE NOT FOUND")
    
    # Summary
    print(f"\n📊 Validation Scripts Test Summary")
    print("=" * 60)
    
    passed = sum(1 for result in results.values() if result)
    total = len(results)
    
    for script, passed_test in results.items():
        status = "✅ PASS" if passed_test else "❌ FAIL"
        print(f"   {script:<25} {status}")
    
    print(f"\n🎯 Overall: {passed}/{total} scripts working correctly")
    
    if passed == total:
        print("🎉 All validation scripts are working!")
    elif passed > 0:
        print("⚠️ Some validation scripts are working")
    else:
        print("💥 No validation scripts are working")
    
    return results

if __name__ == "__main__":
    results = asyncio.run(test_all_validation_scripts())
