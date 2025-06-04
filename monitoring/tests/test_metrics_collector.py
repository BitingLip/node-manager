"""
Test suite for monitoring/metrics_collector.py
"""
import pytest
import types
import sys
import os
from datetime import datetime, timedelta

# Add project root to path for imports
current_dir = os.path.dirname(os.path.abspath(__file__))
parent_dir = os.path.dirname(current_dir)  # monitoring directory
project_root = os.path.dirname(parent_dir)  # node-manager directory
sys.path.insert(0, project_root)

# Import directly instead of using relative imports
from monitoring.metrics_collector import MetricsCollector, SystemMetrics

class DummyNodeController:
    pass

def test_metrics_collector_init():
    mc = MetricsCollector(collection_interval=10, node_controller=DummyNodeController())
    assert mc.collection_interval == 10
    assert mc.node_controller is not None
    assert mc.metrics_history == []
    assert mc.max_history_size == 1440
    assert mc.metrics_callbacks == []
    assert mc.running is False

def test_collect_current_metrics_returns_systemmetrics(monkeypatch):
    mc = MetricsCollector()
    # Patch psutil and dependencies
    monkeypatch.setattr("psutil.cpu_percent", lambda interval: 10.0)
    monkeypatch.setattr("psutil.virtual_memory", lambda: types.SimpleNamespace(total=1000, used=500, available=500))
    monkeypatch.setattr("psutil.disk_usage", lambda path: types.SimpleNamespace(total=1000, used=400, free=600))
    monkeypatch.setattr("psutil.net_io_counters", lambda: types.SimpleNamespace(bytes_sent=100, bytes_recv=200))
    monkeypatch.setattr("psutil.pids", lambda: [1,2,3])
    monkeypatch.setattr("psutil.getloadavg", lambda: (1.0, 0, 0))
    mc.collect_gpu_metrics = lambda: {"gpu": {"name": "FakeGPU"}}
    metrics = mc.collect_current_metrics()
    assert isinstance(metrics, SystemMetrics)
    assert metrics.cpu_usage == 10.0
    assert metrics.memory_total == 1000
    assert metrics.disk_used == 400
    assert metrics.network_sent == 100
    assert metrics.gpu_metrics["gpu"]["name"] == "FakeGPU"

def test_metrics_history_and_cleanup():
    mc = MetricsCollector()
    mc.max_history_size = 3
    now = datetime.now()
    for i in range(5):
        mc.metrics_history.append(SystemMetrics(
            timestamp=now - timedelta(minutes=i),
            cpu_usage=0.0, memory_total=0, memory_used=0, memory_available=0,
            disk_total=0, disk_used=0, disk_available=0,
            network_sent=0, network_received=0, gpu_metrics={},
            process_count=0, load_average=0.0
        ))
    mc._cleanup_old_metrics()
    assert len(mc.metrics_history) == 3

def test_get_metrics_summary_and_trends():
    mc = MetricsCollector()
    now = datetime.now()
    for i in range(10):
        mc.metrics_history.append(SystemMetrics(
            timestamp=now - timedelta(minutes=10-i),
            cpu_usage=float(i), memory_total=100, memory_used=10*i, memory_available=100-10*i,
            disk_total=0, disk_used=0, disk_available=0,
            network_sent=0, network_received=0, gpu_metrics={},
            process_count=0, load_average=0.0
        ))
    summary = mc.get_metrics_summary(hours=1)
    assert "cpu_usage" in summary
    trends = mc.get_resource_trends()
    assert "cpu_usage_trend" in trends

def test_export_metrics_json_and_csv():
    mc = MetricsCollector()
    now = datetime.now()
    for _ in range(2):
        mc.metrics_history.append(SystemMetrics(
            timestamp=now,
            cpu_usage=1.0, memory_total=100, memory_used=50, memory_available=50,
            disk_total=100, disk_used=50, disk_available=50,
            network_sent=10, network_received=20, gpu_metrics={},
            process_count=1, load_average=0.0
        ))
    json_data = mc.export_metrics(format="json")
    assert json_data.startswith("{")
    csv_data = mc.export_metrics(format="csv")
    assert "timestamp" in csv_data

def test_register_metrics_callback_and_call():
    mc = MetricsCollector()
    called = []
    def cb(metrics):
        called.append(metrics)
    mc.register_metrics_callback(cb)
    assert cb in mc.metrics_callbacks
    # Simulate callback call
    if mc.metrics_callbacks:
        mc.metrics_callbacks[0](SystemMetrics(
            timestamp=datetime.now(), cpu_usage=0, memory_total=0, memory_used=0, memory_available=0,
            disk_total=0, disk_used=0, disk_available=0, network_sent=0, network_received=0, gpu_metrics={},
            process_count=0, load_average=0.0
        ))
    assert called
