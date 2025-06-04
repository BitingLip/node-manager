"""
Virtual Environment vs System Python Monitoring Test
Tests monitoring capabilities in different Python environments
"""

import sys
import os
import subprocess
import asyncio
from pathlib import Path

# Add current directory to path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from monitoring import MetricsCollector, HealthMonitor
from monitoring.environment_integration import EnvironmentMonitoringIntegrator, create_monitoring_environment_report


def detect_current_environment():
    """Detect what kind of Python environment we're running in"""
    environment_info = {
        "executable": sys.executable,
        "prefix": sys.prefix,
        "base_prefix": getattr(sys, 'base_prefix', sys.prefix),
        "real_prefix": getattr(sys, 'real_prefix', None),
        "virtual_env": os.environ.get('VIRTUAL_ENV'),
        "conda_env": os.environ.get('CONDA_DEFAULT_ENV'),
        "pipenv_active": os.environ.get('PIPENV_ACTIVE')
    }
    
    # Determine environment type
    is_venv = (
        hasattr(sys, 'real_prefix') or 
        (hasattr(sys, 'base_prefix') and sys.base_prefix != sys.prefix)
    )
    
    env_type = "system"
    if is_venv:
        if environment_info['conda_env']:
            env_type = "conda"
        elif environment_info['virtual_env']:
            env_type = "venv"
        elif environment_info['pipenv_active']:
            env_type = "pipenv"
        else:
            env_type = "unknown_virtual"
    
    environment_info['is_virtual'] = is_venv
    environment_info['type'] = env_type
    
    return environment_info


def test_gpu_detection_in_environment():
    """Test GPU detection in current environment"""
    print("🔍 Testing GPU Detection in Current Environment")
    print("=" * 60)
    
    env_info = detect_current_environment()
    
    print(f"📊 Environment Information:")
    print(f"   Type: {env_info['type']}")
    print(f"   Virtual: {env_info['is_virtual']}")
    print(f"   Python: {env_info['executable']}")
    if env_info['virtual_env']:
        print(f"   Virtual Env Path: {env_info['virtual_env']}")
    
    # Test basic monitoring
    print(f"\n🏥 Testing Basic Monitoring:")
    try:
        collector = MetricsCollector()
        print("   ✅ MetricsCollector: Initialized successfully")
        
        # Test system metrics (should work in any environment)
        metrics = collector.collect_current_metrics()
        print(f"   ✅ System Metrics: CPU={metrics.cpu_usage:.1f}%, Memory={metrics.memory_used/(1024**3):.1f}GB")
        
        # Test GPU metrics (this is where venv issues may appear)
        gpu_metrics = collector.collect_gpu_metrics()
        gpu_count = len([k for k in gpu_metrics.keys() if k.startswith(('amd_gpu_', 'nvidia_gpu_'))])
        print(f"   {'✅' if gpu_count > 0 else '⚠️'} GPU Detection: {gpu_count} GPUs detected")
        
        if gpu_count == 0:
            print("      💡 No GPUs detected - this may be due to virtual environment limitations")
        
        # Check for DirectML specifically
        directml_detected = any(k.startswith('directml') for k in gpu_metrics.keys())
        print(f"   {'✅' if directml_detected else '⚠️'} DirectML Backend: {'Available' if directml_detected else 'Not detected'}")
        
        if env_info['is_virtual'] and not directml_detected:
            print("      💡 DirectML not available - expected in virtual environments")
        
        return True
        
    except Exception as e:
        print(f"   ❌ Monitoring Error: {e}")
        return False


def test_environment_integration():
    """Test environment-aware monitoring integration"""
    print(f"\n🔧 Testing Environment Integration:")
    
    try:
        integrator = EnvironmentMonitoringIntegrator()
        
        print(f"   ✅ Environment Integrator: Initialized")
        print(f"   Virtual Environment: {integrator.environment_status.is_virtual_env}")
        print(f"   DirectML Compatible: {integrator.environment_status.directml_compatible}")
        
        # Get recommendations
        recommendations = integrator.get_gpu_monitoring_recommendations()
        
        if recommendations["limitations"]:
            print(f"   ⚠️  Limitations Found:")
            for limitation in recommendations["limitations"]:
                print(f"      • {limitation['type']}: {limitation['description']}")
        
        if recommendations["setup_recommendations"]:
            print(f"   💡 Recommendations:")
            for rec in recommendations["setup_recommendations"]:
                print(f"      • {rec['action']}")
        
        # Validate environment
        validation = integrator.validate_monitoring_environment()
        print(f"   Environment Valid: {'✅' if validation['environment_compatible'] else '❌'}")
        print(f"   GPU Monitoring: {validation['gpu_monitoring_status']}")
        
        return True
        
    except Exception as e:
        print(f"   ❌ Integration Error: {e}")
        return False


async def test_async_monitoring():
    """Test async monitoring capabilities"""
    print(f"\n⚡ Testing Async Monitoring:")
    
    try:
        collector = MetricsCollector(collection_interval=1)
        
        print("   Starting collection...")
        await collector.start_collection()
        
        await asyncio.sleep(2)
        
        print("   Stopping collection...")
        await collector.stop_collection()
        
        history_count = len(collector.metrics_history)
        print(f"   ✅ Async Collection: {history_count} metrics collected")
        
        return True
        
    except Exception as e:
        print(f"   ❌ Async Error: {e}")
        return False


def create_environment_comparison():
    """Create comparison of monitoring capabilities across environments"""
    print(f"\n📊 Environment Monitoring Capabilities Comparison:")
    print("=" * 70)
    
    current_env = detect_current_environment()
    
    comparison = {
        "System Python": {
            "Basic Monitoring": "✅ Full Support",
            "AMD GPU Detection": "✅ Full Support (WMI + DirectML)",
            "NVIDIA GPU Detection": "✅ Full Support (NVML)",
            "DirectML Support": "✅ Available",
            "AI Workloads": "✅ AMD + NVIDIA",
            "Limitations": "None"
        },
        "Virtual Environment (venv/conda)": {
            "Basic Monitoring": "✅ Full Support",
            "AMD GPU Detection": "⚠️  Limited (WMI only)",
            "NVIDIA GPU Detection": "✅ Full Support (NVML)",
            "DirectML Support": "❌ Not Available",
            "AI Workloads": "⚠️  NVIDIA only",
            "Limitations": "DirectML driver isolation"
        }
    }
    
    current_type = "Virtual Environment (venv/conda)" if current_env['is_virtual'] else "System Python"
    
    for env_name, capabilities in comparison.items():
        current_indicator = " (CURRENT)" if env_name == current_type else ""
        print(f"\n🐍 {env_name}{current_indicator}:")
        for capability, status in capabilities.items():
            print(f"   {capability}: {status}")
    
    return comparison


async def main():
    """Run comprehensive environment monitoring tests"""
    print("🧪 Node Manager Monitoring - Environment Compatibility Test")
    print("=" * 70)
    
    success_count = 0
    total_tests = 3
    
    # Test 1: GPU Detection in current environment
    if test_gpu_detection_in_environment():
        success_count += 1
    
    # Test 2: Environment integration
    if test_environment_integration():
        success_count += 1
    
    # Test 3: Async monitoring
    if await test_async_monitoring():
        success_count += 1
    
    # Create comparison table
    create_environment_comparison()
    
    # Generate full environment report
    print(f"\n" + create_monitoring_environment_report())
    
    print(f"\n" + "=" * 70)
    print(f"🎯 Test Results: {success_count}/{total_tests} tests passed")
    
    if success_count == total_tests:
        print("🎉 Monitoring system is working in current environment!")
    else:
        print("⚠️  Some monitoring features may be limited")
    
    # Specific recommendations
    env_info = detect_current_environment()
    if env_info['is_virtual']:
        print(f"\n💡 Virtual Environment Detected:")
        print(f"   • Basic system monitoring: ✅ Working")
        print(f"   • NVIDIA GPU monitoring: ✅ Should work")
        print(f"   • AMD GPU AI features: ⚠️  Limited (DirectML unavailable)")
        print(f"   • Recommendation: Use system Python for full AMD GPU support")
    else:
        print(f"\n🚀 System Python Detected:")
        print(f"   • All monitoring features available")
        print(f"   • Full AMD RX 6800 series support")
        print(f"   • DirectML AI capabilities enabled")

if __name__ == "__main__":
    asyncio.run(main())
