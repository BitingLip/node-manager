"""
Test suite for monitoring/environment_integration.py
"""
import pytest
import sys
import os
from unittest.mock import MagicMock, patch

# Add project root to path for imports
current_dir = os.path.dirname(os.path.abspath(__file__))
parent_dir = os.path.dirname(current_dir)  # monitoring directory
project_root = os.path.dirname(parent_dir)  # node-manager directory
sys.path.insert(0, project_root)

# Import directly instead of using relative imports
from monitoring.environment_integration import EnvironmentMonitoringIntegrator, create_monitoring_environment_report


def test_environment_detection_basic():
    integrator = EnvironmentMonitoringIntegrator()
    status = integrator.environment_status
    assert hasattr(status, 'is_virtual_env')
    assert hasattr(status, 'python_executable')
    assert status.environment_type in ["system", "venv", "conda", "pipenv", "unknown_venv"]

def test_gpu_monitoring_recommendations():
    integrator = EnvironmentMonitoringIntegrator()
    recs = integrator.get_gpu_monitoring_recommendations()
    assert "environment_status" in recs
    assert "monitoring_adjustments" in recs
    assert "setup_recommendations" in recs
    assert "limitations" in recs

def test_validate_monitoring_environment():
    integrator = EnvironmentMonitoringIntegrator()
    val = integrator.validate_monitoring_environment()
    assert "environment_compatible" in val
    assert "gpu_monitoring_status" in val
    assert "warnings" in val
    assert "errors" in val

def test_get_environment_specific_gpu_metrics(monkeypatch):
    integrator = EnvironmentMonitoringIntegrator()
    # Patch MetricsCollector to avoid real hardware calls
    class DummyCollector:
        def collect_gpu_metrics(self):
            return {"dummy_gpu": {"name": "FakeGPU"}}
    monkeypatch.setattr("monitoring.environment_integration.MetricsCollector", DummyCollector)
    metrics = integrator.get_environment_specific_gpu_metrics()
    assert "environment_info" in metrics
    assert "gpu_devices" in metrics

def test_create_monitoring_environment_report():
    report = create_monitoring_environment_report()
    assert "Node Manager Monitoring Environment Report" in report
    assert "Environment Status" in report
    assert "GPU Compatibility" in report
    assert "Monitoring Validation" in report
