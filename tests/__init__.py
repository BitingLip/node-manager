"""
Test Suite for Node Manager Core Components
"""

import pytest
import asyncio
from unittest.mock import Mock, patch

# Import test modules
from .test_node_controller import *
from .test_resource_manager import *
from .test_worker_manager import *
from .test_task_dispatcher import *

# Test configuration
@pytest.fixture
def test_config():
    """Test configuration fixture"""
    return {
        "node_id": "test-node-001",
        "cluster_manager_url": "http://localhost:8000",
        "api_port": 8080,
        "max_workers": 2,
        "database": {
            "host": "localhost",
            "port": 5432,
            "database": "test_bitinglip_nodes",
            "user": "test_user",
            "password": "test_password"
        }
    }

@pytest.fixture
def event_loop():
    """Create event loop for async tests"""
    loop = asyncio.new_event_loop()
    yield loop
    loop.close()
