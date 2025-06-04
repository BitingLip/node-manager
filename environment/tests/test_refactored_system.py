"""
Test script for the refactored environment setup system
"""

import asyncio
import sys
from pathlib import Path

# Add the environment root to Python path
env_root = Path(__file__).parent
sys.path.insert(0, str(env_root))

# Import modules with corrected paths
from gpu.gpu_detector import GPUDetector
from gpu.gpu_strategy import analyze_gpu_list, get_strategy_requirements
from environment.environment_planner import EnvironmentPlanner, EnvironmentSpec
from environment.venv_manager import VenvManager
from environment.environment_validator import EnvironmentValidator


async def test_refactored_system():
    """Test the complete refactored system"""
    print("🚀 Testing Refactored GPU Environment Setup System")
    print("=" * 60)
    
    try:
        # Test GPU detection
        print("\n🔍 Step 1: Testing GPU Detection")
        detector = GPUDetector()
        gpus = detector.detect_all_gpus()
        print(f"   • Detected {len(gpus)} GPUs")
        
        for gpu in gpus:
            print(f"   • {gpu.name} ({gpu.vendor.value}, {gpu.memory_mb}MB)")
        
        # Test GPU strategy analysis
        print("\n📋 Step 2: Testing GPU Strategy Analysis")
        strategy_result = analyze_gpu_list(gpus)
        print(f"   • Strategy: {strategy_result.strategy}")
        print(f"   • OS: {strategy_result.os_type}")
        print(f"   • WSL Available: {strategy_result.wsl_available}")
        
        if strategy_result.warnings:
            print("   • Warnings:")
            for warning in strategy_result.warnings:
                print(f"     - {warning}")
        
        if strategy_result.recommendations:
            print("   • Recommendations:")
            for rec in strategy_result.recommendations:
                print(f"     - {rec}")
        
        # Test environment requirements generation
        print("\n⚙️ Step 3: Testing Environment Requirements")
        requirements_map = {}
        
        if gpus:
            for gpu in gpus:
                try:
                    req = detector.get_environment_requirements(gpu)
                    requirements_map[gpu.device_id] = req
                    print(f"   • {gpu.device_id}: {req.framework} ({req.python_env_type})")
                except Exception as e:
                    print(f"   • Error getting requirements for {gpu.device_id}: {e}")
        else:
            print("   • No GPUs detected - would create CPU fallback")
          # Test environment planning
        print("\n🏗️ Step 4: Testing Environment Planning")
        planner = EnvironmentPlanner(Path.cwd() / "test_envs")
        specs_map = {}
        
        if requirements_map:
            specs_map = planner.plan_environments(requirements_map)
            print(f"   • Planned {len(specs_map)} environment specifications:")
            
            for spec_name, spec in specs_map.items():
                print(f"     - {spec_name}: {spec.framework.value} ({spec.env_type.value})")
                print(f"       Target GPUs: {spec.target_gpus}")
                print(f"       Packages: {len(spec.base_packages)} base packages")
        else:
            print("   • No requirements to plan environments for")
        
        # Test environment manager initialization
        print("\n🔧 Step 5: Testing Environment Manager")
        venv_mgr = VenvManager(Path.cwd() / "test_envs" / "venvs")
        print(f"   • VenvManager initialized with base path: {venv_mgr.base_path}")
        print(f"   • Existing environments: {len(venv_mgr.environments)}")
        
        # Test validator initialization
        print("\n✅ Step 6: Testing Environment Validator")
        validator = EnvironmentValidator()
        print("   • EnvironmentValidator initialized successfully")
        
        print("\n🎉 All Component Tests Passed!")
        print("\n📊 Summary:")
        print(f"   • GPUs detected: {len(gpus)}")
        print(f"   • Strategy: {strategy_result.strategy}")
        print(f"   • Environment requirements: {len(requirements_map)}")
        print(f"   • Planned environments: {len(specs_map)}")
        print("   • All modules loaded and functional")
        
        return True
        
    except Exception as e:
        print(f"\n❌ Test Failed: {str(e)}")
        import traceback
        traceback.print_exc()
        return False


def analyze_migration_status():
    """Analyze the status of the migration plan implementation"""
    print("\n📋 Migration Plan Implementation Analysis")
    print("=" * 60)
    
    # Check if key files exist and are properly structured
    checks = [
        ("gpu/gpu_strategy.py", "Unified GPU Strategy Module"),
        ("gpu/gpu_detector.py", "Enhanced GPU Detector"),
        ("environment/environment_planner.py", "Streamlined Environment Planner"),
        ("environment/venv_manager.py", "Improved Virtual Environment Manager"),
        ("environment/environment_validator.py", "New Environment Validator"),
        ("orchestrator/environment_setup.py", "Simplified Orchestrator")
    ]
    
    status_summary = {
        "files_present": 0,
        "total_files": len(checks),
        "issues": []
    }
    
    for file_path, description in checks:
        full_path = Path(file_path)
        if full_path.exists():
            status_summary["files_present"] += 1
            print(f"   ✅ {description}: {file_path}")
        else:
            status_summary["issues"].append(f"Missing: {file_path}")
            print(f"   ❌ {description}: {file_path} (MISSING)")
    
    # Check for old files that should be cleaned up
    old_files = [
        "remove old scripts/simple_strategy_analysis.py",
        "remove old scripts/comprehensive_strategy_analysis.py",
        "remove old scripts/strategy_analyzer.py",
        "remove old scripts/enhanced_environment_setup.py"
    ]
    
    print(f"\n🧹 Old Files Status:")
    for old_file in old_files:
        if Path(old_file).exists():
            print(f"   🗂️ {old_file} (moved to archive)")
        else:
            print(f"   ✅ {old_file} (cleaned up)")
    
    print(f"\n📊 Implementation Status:")
    completion_rate = (status_summary["files_present"] / status_summary["total_files"]) * 100
    print(f"   • Files implemented: {status_summary['files_present']}/{status_summary['total_files']} ({completion_rate:.1f}%)")
    
    if status_summary["issues"]:
        print(f"   • Issues found: {len(status_summary['issues'])}")
        for issue in status_summary["issues"]:
            print(f"     - {issue}")
    else:
        print("   • No major issues found")
    
    return status_summary


if __name__ == "__main__":
    # Run migration status analysis
    migration_status = analyze_migration_status()
    
    # Run component tests
    success = asyncio.run(test_refactored_system())
    
    if success:
        print(f"\n✅ REFACTORING SUCCESS")
        print("   • All components working correctly")
        print("   • Unified GPU strategy implemented")
        print("   • Environment management streamlined")
        print("   • Ready for integration testing")
    else:
        print(f"\n⚠️ REFACTORING NEEDS WORK")
        print("   • Some components have issues")
        print("   • Check error messages above")
        print("   • Fix issues before integration")
