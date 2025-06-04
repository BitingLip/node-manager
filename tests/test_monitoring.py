"""
Test script for Node Manager Monitoring Components
"""

import asyncio
import sys
import os

# Add the parent directory to Python path for relative imports
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from monitoring import HealthMonitor, MetricsCollector

async def test_metrics_collector():
    """Test the MetricsCollector"""
    print("🔍 Testing MetricsCollector...")
    
    collector = MetricsCollector(collection_interval=5)
    
    # Test current metrics collection
    metrics = collector.collect_current_metrics()
    print(f"✅ Current metrics collected:")
    print(f"   CPU Usage: {metrics.cpu_usage:.1f}%")
    print(f"   Memory Used: {metrics.memory_used / (1024**3):.1f} GB / {metrics.memory_total / (1024**3):.1f} GB")
    print(f"   Disk Used: {metrics.disk_used / (1024**3):.1f} GB / {metrics.disk_total / (1024**3):.1f} GB")
    print(f"   Processes: {metrics.process_count}")
    
    # Test GPU metrics
    gpu_metrics = collector.collect_gpu_metrics()
    print(f"   GPU Metrics: {len(gpu_metrics)} devices detected")
    
    # Test worker metrics (should be empty without workers)
    worker_metrics = collector.collect_worker_metrics()
    print(f"   Worker Metrics: {len(worker_metrics)} workers")
    
    # Test export
    metrics_json = collector.export_metrics("json")
    print(f"   Export: {len(metrics_json)} characters")
    
    # Test collection start/stop
    await collector.start_collection()
    print("✅ Collection started")
    
    # Let it collect for a few seconds
    await asyncio.sleep(3)
    
    await collector.stop_collection()
    print("✅ Collection stopped")
    
    print(f"   History: {len(collector.metrics_history)} entries")
    
    return True

async def test_health_monitor():
    """Test the HealthMonitor"""
    print("\n🏥 Testing HealthMonitor...")
    
    monitor = HealthMonitor()
    
    # Test individual health checks
    cpu_health = monitor._check_cpu_usage()
    print(f"✅ CPU Health: {cpu_health.status} - {cpu_health.details.get('message', 'No message')}")
    
    memory_health = monitor._check_memory_usage()
    print(f"✅ Memory Health: {memory_health.status} - {memory_health.details.get('message', 'No message')}")
    
    disk_health = monitor._check_disk_usage()
    print(f"✅ Disk Health: {disk_health.status} - {disk_health.details.get('message', 'No message')}")
    
    worker_health = monitor._check_worker_health()
    print(f"✅ Worker Health: {worker_health.status} - {worker_health.details.get('message', 'No message')}")
    
    # Test overall health
    overall_health = monitor.get_overall_health()
    if overall_health:
        print(f"✅ Overall Health: {overall_health.status}")
    else:
        print("⚠️  Overall health not yet calculated")
    
    # Test monitoring start/stop
    await monitor.start_monitoring()
    print("✅ Health monitoring started")
    
    # Let it monitor for a few seconds
    await asyncio.sleep(3)
    
    await monitor.stop_monitoring()
    print("✅ Health monitoring stopped")
    
    return True

async def test_integration():
    """Test integration between components"""
    print("\n🔗 Testing Integration...")
    
    # Create both components
    collector = MetricsCollector(collection_interval=2)
    monitor = HealthMonitor()
    
    # Start both
    await collector.start_collection()
    await monitor.start_monitoring()
    
    print("✅ Both components running...")
    
    # Let them run together
    await asyncio.sleep(5)
    
    # Check results
    metrics = collector.collect_current_metrics()
    health = monitor.get_overall_health()
    
    print(f"   Metrics timestamp: {metrics.timestamp}")
    if health:
        print(f"   Health status: {health.status}")
    
    # Stop both
    await collector.stop_collection()
    await monitor.stop_monitoring()
    
    print("✅ Integration test completed")
    
    return True

async def main():
    """Run all tests"""
    print("🚀 Node Manager Monitoring Test Suite")
    print("=" * 50)
    
    success_count = 0
    total_tests = 3
    
    try:
        if await test_metrics_collector():
            success_count += 1
    except Exception as e:
        print(f"❌ MetricsCollector test failed: {e}")
    
    try:
        if await test_health_monitor():
            success_count += 1
    except Exception as e:
        print(f"❌ HealthMonitor test failed: {e}")
    
    try:
        if await test_integration():
            success_count += 1
    except Exception as e:
        print(f"❌ Integration test failed: {e}")
    
    print("\n" + "=" * 50)
    print(f"Test Results: {success_count}/{total_tests} passed")
    
    if success_count == total_tests:
        print("🎉 All monitoring tests passed!")
        return True
    else:
        print("⚠️  Some tests failed - monitoring needs attention")
        return False

if __name__ == "__main__":
    result = asyncio.run(main())
    sys.exit(0 if result else 1)
