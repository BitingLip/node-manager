"""
Test fixtures for monitoring tests
"""
import pytest
import sys
import os
from unittest.mock import MagicMock

# Add project root to path for imports
current_dir = os.path.dirname(os.path.abspath(__file__))
parent_dir = os.path.dirname(current_dir)  # monitoring directory
project_root = os.path.dirname(parent_dir)  # node-manager directory
sys.path.insert(0, project_root)

# We don't require pytest_asyncio for these basic tests
# Remove the dependency to avoid installation requirements

@pytest.fixture
def dummy_node_controller():
    """Fixture providing a dummy node controller for tests"""
    node_controller = MagicMock()
    worker_manager = MagicMock()
    node_controller.worker_manager = worker_manager
    return node_controller

@pytest.fixture
def dummy_worker_manager():
    """Fixture providing a worker manager for tests"""
    manager = MagicMock()
    manager.get_worker_status.return_value = {
        "worker1": {"state": "ready", "tasks_completed": 5},
        "worker2": {"state": "busy", "tasks_completed": 3},
        "worker3": {"state": "error", "error": "Connection failed"}
    }
    return manager
