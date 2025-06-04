"""
Test suite for monitoring/health_monitor.py
"""
import pytest
import sys
import os

# Add project root to path for imports
current_dir = os.path.dirname(os.path.abspath(__file__))
parent_dir = os.path.dirname(current_dir)  # monitoring directory
project_root = os.path.dirname(parent_dir)  # node-manager directory
sys.path.insert(0, project_root)

# Import directly instead of using relative imports
from monitoring.health_monitor import HealthMonitor, HealthStatus

class DummyWorkerManager:
    def get_worker_status(self):
        return {
            'w1': {'state': 'ready'},
            'w2': {'state': 'error'},
            'w3': {'state': 'ready'}
        }

class DummyNodeController:
    def __init__(self):
        self.worker_manager = DummyWorkerManager()

def test_health_monitor_init():
    hm = HealthMonitor(node_controller=DummyNodeController())
    assert hm.node_controller is not None
    assert 'cpu_usage' in hm.health_checks
    assert 'memory_usage' in hm.health_checks
    assert 'disk_usage' in hm.health_checks
    assert 'worker_health' in hm.health_checks
    assert isinstance(hm.health_status['cpu_usage'], HealthStatus)

def test_register_health_check():
    hm = HealthMonitor()
    def dummy_check():
        return HealthStatus('healthy', {'msg': 'ok'})
    assert hm.register_health_check('dummy', dummy_check)
    assert 'dummy' in hm.health_checks
    assert isinstance(hm.health_status['dummy'], HealthStatus)
    def bad_check():
        return None
    assert not hm.register_health_check('bad', bad_check)

def test_run_health_check():
    hm = HealthMonitor(node_controller=DummyNodeController())
    # For testing, we'll directly call the check rather than the async run_health_check
    status = hm._check_cpu_usage()
    assert isinstance(status, HealthStatus)

def test_get_health_status():
    hm = HealthMonitor(node_controller=DummyNodeController())
    # Just check that the methods exist and return the right types
    hm.health_status['cpu_usage'] = HealthStatus('healthy', {"message": "test"})
    status = hm.get_component_health('cpu_usage')
    assert isinstance(status, HealthStatus)
    assert status.status == 'healthy'

def test_get_overall_health():
    hm = HealthMonitor(node_controller=DummyNodeController())
    # Set up statuses
    hm.health_status['cpu_usage'] = HealthStatus('healthy', {})
    hm.health_status['memory_usage'] = HealthStatus('warning', {})
    hm.health_status['disk_usage'] = HealthStatus('healthy', {})
    hm.health_status['worker_health'] = HealthStatus('critical', {})
    overall = hm.get_overall_health()
    assert overall.status == 'critical'

def test_get_component_health():
    hm = HealthMonitor(node_controller=DummyNodeController())
    status = hm.get_component_health('cpu_usage')
    assert isinstance(status, HealthStatus)
    assert hm.get_component_health('not_exist') is None

def test_register_alert_callback_and_trigger():
    hm = HealthMonitor(node_controller=DummyNodeController())
    called = []
    def cb(alert):
        called.append(alert)
    hm.register_alert_callback(cb)
    assert cb in hm.alert_callbacks
    # Simulate alert
    status = HealthStatus('critical', {'message': 'fail'})
    hm._trigger_alert('cpu_usage', status)
    assert called
