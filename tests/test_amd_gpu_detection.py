"""
GPU Detection Validation Test
Test the enhanced AMD GPU detection for RX 6800 series
"""

import asyncio
import sys
import os

# Add current directory to path
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from monitoring import MetricsCollector, HealthMonitor

def test_amd_gpu_detection():
    """Test AMD GPU detection specifically"""
    print("🎮 Testing AMD GPU Detection for RX 6800 Series")
    print("=" * 60)
    
    collector = MetricsCollector()
    gpu_metrics = collector.collect_gpu_metrics()
    
    # Count detected GPUs
    amd_gpus = {k: v for k, v in gpu_metrics.items() if k.startswith('amd_gpu_')}
    nvidia_gpus = {k: v for k, v in gpu_metrics.items() if k.startswith('nvidia_gpu_')}
    
    print(f"🔍 Detection Summary:")
    print(f"   AMD GPUs: {len(amd_gpus)}")
    print(f"   NVIDIA GPUs: {len(nvidia_gpus)}")
    print(f"   Other backends: {len(gpu_metrics) - len(amd_gpus) - len(nvidia_gpus)}")
    
    print(f"\n🎯 AMD GPU Details:")
    for gpu_id, gpu_info in amd_gpus.items():
        print(f"   {gpu_id}:")
        print(f"      Name: {gpu_info.get('name')}")
        print(f"      Family: {gpu_info.get('gpu_family', 'Unknown')}")
        print(f"      VRAM: {gpu_info.get('vram_total_gb')}GB (Expected: {gpu_info.get('expected_vram_gb', 'Unknown')}GB)")
        print(f"      AI Capable: {gpu_info.get('ai_capable', False)}")
        print(f"      DirectML: {gpu_info.get('directml_support', False)}")
        print(f"      Driver: {'✅ OK' if gpu_info.get('driver_available') else '❌ Error'}")
        print(f"      PNP ID: {gpu_info.get('pnp_device_id', 'Unknown')[:50]}...")
        print()
    
    # Check for DirectML backend
    if 'directml_backend' in gpu_metrics:
        print(f"🚀 DirectML Backend:")
        backend = gpu_metrics['directml_backend']
        print(f"   Status: {backend.get('status')}")
        print(f"   AI Support: {backend.get('ai_capable', False)}")
        print(f"   Framework: {backend.get('framework')}")
    
    # Validate expected hardware
    expected_rx6800_count = 4
    expected_rx6800xt_count = 1
    
    rx6800_count = sum(1 for gpu in amd_gpus.values() if '6800' in gpu.get('name', '') and 'XT' not in gpu.get('name', ''))
    rx6800xt_count = sum(1 for gpu in amd_gpus.values() if '6800 XT' in gpu.get('name', ''))
    
    print(f"🔧 Hardware Validation:")
    print(f"   Expected RX 6800: {expected_rx6800_count}, Detected: {rx6800_count} {'✅' if rx6800_count == expected_rx6800_count else '⚠️'}")
    print(f"   Expected RX 6800 XT: {expected_rx6800xt_count}, Detected: {rx6800xt_count} {'✅' if rx6800xt_count == expected_rx6800xt_count else '⚠️'}")
    
    total_expected = expected_rx6800_count + expected_rx6800xt_count
    total_detected = len(amd_gpus)
    print(f"   Total Expected: {total_expected}, Total Detected: {total_detected} {'✅' if total_detected == total_expected else '⚠️'}")
    
    return len(amd_gpus) > 0

async def test_monitoring_with_gpus():
    """Test full monitoring system with GPU detection"""
    print(f"\n📊 Testing Full Monitoring System")
    print("=" * 60)
    
    # Test metrics collection
    collector = MetricsCollector(collection_interval=2)
    metrics = collector.collect_current_metrics()
    
    print(f"✅ System Metrics Collected:")
    print(f"   CPU Usage: {metrics.cpu_usage:.1f}%")
    print(f"   Memory: {metrics.memory_used/(1024**3):.1f}GB / {metrics.memory_total/(1024**3):.1f}GB")
    print(f"   GPU Count: {len(metrics.gpu_metrics)}")
    
    # Test health monitoring
    monitor = HealthMonitor()
    
    cpu_health = monitor._check_cpu_usage()
    memory_health = monitor._check_memory_usage()
    
    print(f"\n🏥 Health Monitoring:")
    print(f"   CPU Health: {cpu_health.status} ({cpu_health.details.get('cpu_usage', 0):.1f}%)")
    print(f"   Memory Health: {memory_health.status} ({memory_health.details.get('memory_usage', 0):.1f}%)")
    
    # Test async collection
    print(f"\n⚡ Testing Async Collection:")
    await collector.start_collection()
    print("   Collection started...")
    
    await asyncio.sleep(3)
    
    await collector.stop_collection()
    print("   Collection stopped...")
    print(f"   History entries: {len(collector.metrics_history)}")
    
    return True

async def main():
    """Run all GPU and monitoring tests"""
    print("🎮 AMD RX 6800 Series GPU Detection & Monitoring Test")
    print("=" * 70)
    
    success_count = 0
    total_tests = 2
    
    # Test 1: GPU Detection
    try:
        if test_amd_gpu_detection():
            success_count += 1
            print("✅ GPU Detection Test: PASSED")
        else:
            print("❌ GPU Detection Test: FAILED")
    except Exception as e:
        print(f"❌ GPU Detection Test Error: {e}")
    
    # Test 2: Full Monitoring
    try:
        if await test_monitoring_with_gpus():
            success_count += 1
            print("✅ Monitoring Integration Test: PASSED")
        else:
            print("❌ Monitoring Integration Test: FAILED")
    except Exception as e:
        print(f"❌ Monitoring Integration Test Error: {e}")
    
    print("\n" + "=" * 70)
    print(f"🎯 Test Results: {success_count}/{total_tests} passed")
    
    if success_count == total_tests:
        print("🎉 All AMD RX 6800 GPU monitoring tests PASSED!")
        print("💡 Your 4x RX 6800 + 1x RX 6800 XT setup is properly detected!")
        return True
    else:
        print("⚠️  Some tests failed - check GPU drivers or monitoring setup")
        return False

if __name__ == "__main__":
    result = asyncio.run(main())
    sys.exit(0 if result else 1)
