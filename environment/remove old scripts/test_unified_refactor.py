"""
Test the unified GPU environment setup refactoring
"""

import asyncio
import logging
from pathlib import Path
import sys
import os

# Add the environment path to sys.path
env_path = Path(__file__).parent
sys.path.insert(0, str(env_path))

# Set up logging
logging.basicConfig(level=logging.INFO)

async def test_unified_setup():
    """Test the refactored environment setup orchestrator"""
    
    print("🚀 Testing Unified GPU Environment Setup")
    print("=" * 50)
    
    try:
        # Import the refactored components
        from environment_setup import EnvironmentSetupOrchestrator
        
        # Create test directory
        test_dir = Path.cwd() / "test_environments"
        test_dir.mkdir(exist_ok=True)
        
        # Initialize orchestrator
        print("📋 Initializing EnvironmentSetupOrchestrator...")
        orchestrator = EnvironmentSetupOrchestrator(base_path=test_dir)
        
        # Run complete setup
        print("🔧 Running complete environment setup...")
        summary = await orchestrator.setup_all(force_recreate=False)
        
        # Display results
        print("\n📊 Setup Summary:")
        print(f"  • Total GPUs detected: {summary.total_gpus}")
        print(f"  • Environments created: {summary.environments_created}")
        print(f"  • Environments successful: {summary.environments_successful}")
        print(f"  • Setup time: {summary.setup_time_seconds:.2f}s")
        
        if summary.gpu_assignments:
            print("\n🎯 GPU Assignments:")
            for env_name, gpu_ids in summary.gpu_assignments.items():
                print(f"  • {env_name}: {gpu_ids}")
        
        if summary.val_results:
            print("\n✅ Validation Results:")
            for env_name, val_result in summary.val_results.items():
                status = "✅ PASS" if val_result.overall_success else "❌ FAIL"
                print(f"  • {env_name}: {status}")
        
        if summary.warnings:
            print("\n⚠️ Warnings:")
            for warning in summary.warnings:
                print(f"  • {warning}")
        
        if summary.errors:
            print("\n❌ Errors:")
            for error in summary.errors:
                print(f"  • {error}")
        
        # Test individual components
        print("\n🧪 Testing Individual Components:")
        
        # Test GPU detection
        print("  📡 GPU Detection...")
        try:
            gpus = orchestrator.gpu_detector.detect_all_gpus()
            print(f"    ✅ Detected {len(gpus)} GPU(s)")
            for gpu in gpus:
                print(f"      - {gpu.name} ({gpu.vendor.value}, {gpu.memory_mb}MB)")
        except Exception as e:
            print(f"    ❌ GPU detection failed: {e}")
        
        # Test environment requirements
        print("  📋 Environment Requirements...")
        try:
            if hasattr(orchestrator.gpu_detector, 'get_environment_requirements_all'):
                reqs = orchestrator.gpu_detector.get_environment_requirements_all()
                print(f"    ✅ Generated {len(reqs)} requirements")
                for gpu_id, req in reqs.items():
                    print(f"      - {gpu_id}: {req.framework} ({req.python_env_type})")
            else:
                print("    ⚠️ get_environment_requirements_all method not found")
        except Exception as e:
            print(f"    ❌ Requirements generation failed: {e}")
        
        print("\n🎉 Test completed!")
        
        # Cleanup test directory
        import shutil
        try:
            shutil.rmtree(test_dir)
            print("🧹 Cleaned up test directory")
        except Exception as e:
            print(f"⚠️ Cleanup warning: {e}")
        
        return summary
        
    except ImportError as e:
        print(f"❌ Import failed: {e}")
        print("💡 This is expected during refactoring - imports need to be fixed")
        return None
    except Exception as e:
        print(f"❌ Test failed: {e}")
        import traceback
        traceback.print_exc()
        return None

def test_gpu_strategy():
    """Test the unified GPU strategy component"""
    print("\n🧪 Testing GPU Strategy Component:")
    
    try:
        from gpu_strategy import analyze_gpu_list, detect_os_type, detect_wsl_available
        
        # Test OS detection
        print("  🖥️ OS Detection...")
        os_type = detect_os_type()
        wsl_available = detect_wsl_available()
        print(f"    ✅ OS Type: {os_type}")
        print(f"    ✅ WSL Available: {wsl_available}")
        
        # Test with empty GPU list (CPU fallback)
        print("  💻 CPU Fallback Strategy...")
        strategy = analyze_gpu_list([])
        print(f"    ✅ Strategy: {strategy.strategy}")
        print(f"    ✅ Reason: {strategy.reason}")
        
        return True
        
    except ImportError as e:
        print(f"    ❌ Import failed: {e}")
        return False
    except Exception as e:
        print(f"    ❌ Strategy test failed: {e}")
        return False

if __name__ == "__main__":
    print("🔬 Testing Unified GPU Environment Refactoring")
    print("=" * 60)
    
    # Test GPU strategy component
    strategy_ok = test_gpu_strategy()
    
    # Test full setup
    summary = asyncio.run(test_unified_setup())
    
    print("\n📋 Final Summary:")
    print(f"  • GPU Strategy Test: {'✅ PASS' if strategy_ok else '❌ FAIL'}")
    print(f"  • Full Setup Test: {'✅ PASS' if summary else '❌ FAIL'}")
    
    if summary:
        success_rate = summary.environments_successful / max(1, summary.environments_created)
        print(f"  • Environment Success Rate: {success_rate:.1%}")
    
    print("\n🎯 Refactoring Progress:")
    print("  ✅ Created unified gpu_strategy.py")
    print("  ✅ Simplified environment_setup.py orchestrator")
    print("  ✅ Updated environment_validator.py")
    print("  ✅ Enhanced venv_manager.py")
    print("  🔄 Working on import fixes and integration")
    print("  🔄 Removing duplicate/overlapping methods")
    
    print("\n🚀 Ready for next iteration!")
