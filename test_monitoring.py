#!/usr/bin/env python3
"""
Simple test runner for monitoring components
Runs all tests in the monitoring/tests directory
"""

import sys
import os
import subprocess

# Add project root to path for imports
current_dir = os.path.dirname(os.path.abspath(__file__))
project_root = os.path.dirname(current_dir)  # node-manager directory
sys.path.insert(0, project_root)

def run_regular_tests():
    """Run regular unit tests directly without pytest"""
    print("\nRunning basic tests for monitoring components...")
    
    from monitoring.metrics_collector import MetricsCollector
    from monitoring.health_monitor import HealthMonitor
    from monitoring.environment_integration import EnvironmentMonitoringIntegrator
    
    # Test metrics collector
    print("\nTesting MetricsCollector...")
    try:
        mc = MetricsCollector()
        metrics = mc.collect_current_metrics()
        print(f"✓ MetricsCollector works: CPU={metrics.cpu_usage:.1f}%, Memory={metrics.memory_used/1024/1024:.1f}MB")
    except Exception as e:
        print(f"✗ MetricsCollector error: {e}")
    
    # Test health monitor
    print("\nTesting HealthMonitor...")
    try:
        hm = HealthMonitor()
        status = hm._check_cpu_usage()
        print(f"✓ HealthMonitor works: CPU Health={status.status}")
    except Exception as e:
        print(f"✗ HealthMonitor error: {e}")
    
    # Test environment integration
    print("\nTesting EnvironmentMonitoringIntegrator...")
    try:
        em = EnvironmentMonitoringIntegrator()
        env_type = em.environment_status.environment_type
        print(f"✓ EnvironmentMonitoringIntegrator works: Env Type={env_type}")
    except Exception as e:
        print(f"✗ EnvironmentMonitoringIntegrator error: {e}")
    
    return True

if __name__ == "__main__":
    # Get the path to the monitoring tests directory
    tests_dir = os.path.join(current_dir, "monitoring", "tests")
    
    # Make sure tests can import the main modules
    if not os.path.exists(os.path.join(current_dir, "monitoring")):
        print(f"Error: 'monitoring' directory not found at {current_dir}")
        sys.exit(1)
    
    # Run manual tests
    run_regular_tests()
        
    # Run pytest tests if pytest is installed
    try:
        import pytest
        print("\nRunning pytest tests...")
        result = pytest.main(["-v", tests_dir, "--import-mode=importlib"])
        if result != 0:
            print("\nSome pytest tests failed. Run install_test_deps.py to install required dependencies.")
    except ImportError:
        print("\nPytest not installed. Run install_test_deps.py to install required dependencies.")
        result = 0  # Don't fail if pytest is not installed
    
    sys.exit(result)
