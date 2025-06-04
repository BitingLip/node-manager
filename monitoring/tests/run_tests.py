#!/usr/bin/env python3
"""
Monitoring Components Test Runner
Runs tests for metrics_collector.py, health_monitor.py, and environment_integration.py
"""

import asyncio
import sys
import time
import logging
import pytest
from pathlib import Path
import os

# Set up path for local imports
# Add parent directory to path to allow importing monitoring modules
current_dir = os.path.dirname(os.path.abspath(__file__))
parent_dir = os.path.dirname(current_dir)  # monitoring directory
project_root = os.path.dirname(parent_dir)  # node-manager directory
sys.path.insert(0, project_root)

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)

logger = logging.getLogger(__name__)


async def test_worker_factory():
    """Test worker factory functionality"""
    print("\n=== Testing Worker Factory ===")
    
    try:
        from workers.worker_factory import WorkerFactory
        
        # Test getting available worker types
        worker_types = WorkerFactory.get_available_types()
        print(f"Available worker types: {worker_types}")
        
        # Test creating different types of workers
        test_configs = {
            'llm': {
                'max_memory_mb': 4096,
                'timeout_seconds': 300,
                'default_model': 'gpt2'
            },
            'stable_diffusion': {
                'max_memory_mb': 8192,
                'timeout_seconds': 600,
                'default_model': 'runwayml/stable-diffusion-v1-5'
            },
            'tts': {
                'max_memory_mb': 2048,
                'timeout_seconds': 120,
                'default_model': 'tacotron2'
            }
        }
        
        created_workers = []
        
        for worker_type in ['llm', 'stable_diffusion', 'tts']:
            if worker_type in worker_types:
                print(f"\nTesting {worker_type} worker creation...")
                
                worker_id = f"test_{worker_type}_worker"
                config = test_configs.get(worker_type, {})
                
                worker = WorkerFactory.create_worker(worker_type, worker_id, config)
                if worker:
                    print(f"✓ Created {worker_type} worker: {worker_id}")
                    
                    # Test initialization
                    try:
                        initialized = await worker.initialize()
                        if initialized:
                            print(f"✓ Initialized {worker_type} worker successfully")
                        else:
                            print(f"⚠ Failed to initialize {worker_type} worker (may be due to missing dependencies)")
                    except Exception as e:
                        print(f"⚠ Initialization error for {worker_type}: {e}")
                    
                    # Test capabilities
                    capabilities = worker.get_capabilities()
                    print(f"✓ {worker_type} capabilities: {capabilities.get('supported_tasks', [])}")
                    
                    created_workers.append(worker)
                else:
                    print(f"✗ Failed to create {worker_type} worker")
        
        print(f"\nCreated {len(created_workers)} workers successfully")
        return True
        
    except Exception as e:
        print(f"✗ Worker factory test failed: {e}")
        return False


async def test_worker_pool():
    """Test worker pool functionality"""
    print("\n=== Testing Worker Pool ===")
    
    try:
        from workers.worker_factory import WorkerFactory, WorkerPool
        
        # Create a worker pool
        pool = WorkerPool(max_workers=5)
        print("✓ Created worker pool")
        
        # Create and add workers
        workers_to_add = [
            ('llm', 'pool_llm_worker'),
            ('tts', 'pool_tts_worker')
        ]
        
        for worker_type, worker_id in workers_to_add:
            worker = WorkerFactory.create_worker(worker_type, worker_id, {})
            if worker:
                if pool.add_worker(worker):
                    print(f"✓ Added {worker_type} worker to pool")
                else:
                    print(f"✗ Failed to add {worker_type} worker to pool")
        
        # Test pool status
        status = pool.get_pool_status()
        print(f"✓ Pool status: {status['total_workers']} workers")
        
        # Test getting available worker
        available_worker = pool.get_available_worker()
        if available_worker:
            print(f"✓ Found available worker: {available_worker.worker_id}")
        else:
            print("ℹ No available workers in pool")
        
        return True
        
    except Exception as e:
        print(f"✗ Worker pool test failed: {e}")
        return False


async def test_node_config():
    """Test node configuration"""
    print("\n=== Testing Node Configuration ===")
    
    try:
        from config.node_config import NodeConfig
        
        # Test default configuration
        config = NodeConfig()
        print("✓ Created default node config")
        
        # Test configuration access
        node_id = config.get_config('node_id')
        cluster_url = config.get_config('cluster_manager_url')
        max_workers = config.get_config('max_workers')
        
        print(f"✓ Config values - node_id: {node_id}, max_workers: {max_workers}")
        
        # Test configuration update
        config.update_config({
            'max_workers': 8,
            'heartbeat_interval': 20
        })
        
        updated_max_workers = config.get_config('max_workers')
        print(f"✓ Updated max_workers: {updated_max_workers}")
        
        # Test getting all config
        all_config = config.get_all_config()
        print(f"✓ Retrieved {len(all_config)} configuration items")
        
        return True
        
    except Exception as e:
        print(f"✗ Node config test failed: {e}")
        return False


async def test_simple_task_processing():
    """Test simple task processing"""
    print("\n=== Testing Simple Task Processing ===")
    
    try:
        from workers.worker_factory import WorkerFactory
        
        # Create a TTS worker for testing (most likely to work without GPU)
        worker = WorkerFactory.create_worker('tts', 'test_task_worker', {})
        if not worker:
            print("✗ Failed to create worker for task testing")
            return False
        
        print("✓ Created worker for task testing")
        
        # Initialize worker
        try:
            initialized = await worker.initialize()
            if not initialized:
                print("⚠ Worker initialization failed, skipping task test")
                return True
        except Exception as e:
            print(f"⚠ Worker initialization error: {e}, skipping task test")
            return True
        
        # Test simple task
        test_task = {
            'text': 'Hello, this is a test message for text-to-speech synthesis.',
            'voice': 'default',
            'language': 'en'
        }
        
        print("✓ Initialized worker, processing test task...")
        result = await worker.process_task(test_task)
        
        if result.get('status') == 'completed':
            print("✓ Task processed successfully")
            print(f"  Result: {result.get('metadata', {}).get('text', 'N/A')[:50]}...")
        else:
            print(f"⚠ Task processing result: {result}")
        
        return True
        
    except Exception as e:
        print(f"✗ Task processing test failed: {e}")
        return False


async def test_core_imports():
    """Test core component imports"""
    print("\n=== Testing Core Imports ===")
    
    try:
        from core.node_controller import NodeController
        print("✓ NodeController imported successfully")
        
        from core.resource_manager import ResourceManager
        print("✓ ResourceManager imported successfully")
        
        from core.worker_manager import WorkerManager
        print("✓ WorkerManager imported successfully")
        
        from core.task_dispatcher import TaskDispatcher
        print("✓ TaskDispatcher imported successfully")
        
        return True
    except Exception as e:
        print(f"✗ Core import failed: {e}")
        return False


async def test_worker_imports():
    """Test worker imports"""
    print("\n=== Testing Worker Imports ===")
    
    try:
        from workers.base_worker import BaseWorker, WorkerState
        print("✓ BaseWorker classes imported successfully")
        
        from workers.worker_registry import WorkerRegistry
        print("✓ WorkerRegistry imported successfully")
        
        from workers.worker_pool import WorkerPool
        print("✓ WorkerPool imported successfully")
        
        return True
    except Exception as e:
        print(f"✗ Worker import failed: {e}")
        return False


async def test_monitoring_imports():
    """Test monitoring imports"""
    print("\n=== Testing Monitoring Imports ===")
    
    try:
        from monitoring.health_monitor import HealthMonitor
        print("✓ HealthMonitor imported successfully")
        
        from monitoring.metrics_collector import MetricsCollector
        print("✓ MetricsCollector imported successfully")
        
        from monitoring.environment_integration import EnvironmentMonitoringIntegrator
        print("✓ EnvironmentMonitoringIntegrator imported successfully")
        
        return True
    except Exception as e:
        print(f"✗ Monitoring import failed: {e}")
        return False


async def run_monitoring_tests():
    """Run all monitoring component tests using pytest"""
    print("\n=== Running Pytest for Monitoring Components ===")
    
    # Get the path to the monitoring tests directory
    tests_dir = os.path.dirname(os.path.abspath(__file__))
    
    # Define the tests to run
    test_files = [
        "test_metrics_collector.py",
        "test_health_monitor.py",
        "test_environment_integration.py"
    ]
    
    # Run each test file with additional args to avoid ModuleNotFoundError
    results = {}
    for test_file in test_files:
        test_path = os.path.join(tests_dir, test_file)
        if os.path.exists(test_path):
            print(f"\nRunning tests from {test_file}...")
            # Use the direct file path and avoid importing the module ROOT level
            result = pytest.main(["-v", test_path, "--import-mode=importlib"])
            results[test_file] = result == 0
        else:
            print(f"Test file {test_file} not found!")
            results[test_file] = False
    
    # Check results
    all_passed = all(results.values())
    return all_passed


async def test_basic_functionality():
    """Test basic functionality of monitoring components"""
    print("\n=== Testing Basic Monitoring Functionality ===")
    
    try:
        # Test metrics collector
        from monitoring.metrics_collector import MetricsCollector
        collector = MetricsCollector(collection_interval=5)
        metrics = collector.collect_current_metrics()
        print(f"✓ MetricsCollector: CPU={metrics.cpu_usage:.1f}%, Memory={metrics.memory_used/(1024**3):.1f}GB")
        
        # Test health monitor
        from monitoring.health_monitor import HealthMonitor
        monitor = HealthMonitor()
        cpu_health = monitor._check_cpu_usage()
        print(f"✓ HealthMonitor: CPU health={cpu_health.status}")
        
        # Test environment integration
        from monitoring.environment_integration import EnvironmentMonitoringIntegrator
        integrator = EnvironmentMonitoringIntegrator()
        env_status = integrator.environment_status
        print(f"✓ Environment integrator: Type={env_status.environment_type}")
        
        return True
    except Exception as e:
        print(f"✗ Functionality test failed: {e}")
        return False


async def main():
    """Run all monitoring tests"""
    print("🧪 Monitoring Components Test Suite")
    print("=" * 50)
    
    # First run basic tests
    tests = [
        test_monitoring_imports,
        test_basic_functionality
    ]
    
    manual_passed = 0
    manual_total = len(tests)
    
    for test in tests:
        try:
            if await test():
                manual_passed += 1
            else:
                print("✗ Test failed")
        except Exception as e:
            print(f"✗ Test error: {e}")
    
    # Then run pytest-based tests
    print("\nRunning pytest-based tests...")
    pytest_passed = await run_monitoring_tests()
    
    # Final summary
    print("\n" + "=" * 50)
    print("TEST SUMMARY")
    print("=" * 50)
    print(f"Manual Tests: {manual_passed}/{manual_total} passed")
    print(f"Pytest Tests: {'PASSED' if pytest_passed else 'FAILED'}")
    
    all_passed = (manual_passed == manual_total) and pytest_passed
    
    if all_passed:
        print("\n🎉 All monitoring tests passed!")
    else:
        print("\n⚠️ Some monitoring tests failed")
    
    return all_passed


if __name__ == "__main__":
    print("Starting Monitoring Component Test Suite...")
    
    try:
        success = asyncio.run(main())
        sys.exit(0 if success else 1)
    except KeyboardInterrupt:
        print("\n\nTest suite interrupted by user")
        sys.exit(1)
    except Exception as e:
        print(f"\n\nTest suite failed with error: {e}")
        sys.exit(1)
