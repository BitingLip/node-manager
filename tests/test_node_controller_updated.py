"""
Test Node Controller
Unit tests for the main node controller component
"""

import pytest
import asyncio
from unittest.mock import Mock, patch, AsyncMock
import sys
from pathlib import Path

# Add project root to path
project_root = Path(__file__).parent.parent.parent.parent
sys.path.insert(0, str(project_root))
node_manager_root = Path(__file__).parent.parent
sys.path.insert(0, str(node_manager_root))

from core.node_controller import NodeController


class TestNodeController:
    """Test cases for NodeController"""
    
    @pytest.fixture
    def node_controller(self):
        """Create test node controller instance"""
        return NodeController()
    
    def test_initialization(self, node_controller):
        """Test node controller initialization"""
        assert node_controller.status == "initializing"
        assert node_controller.node_id is not None
        assert node_controller.node_id.startswith("node-")
        assert len(node_controller.node_id) == 13  # "node-" + 8 hex chars
        assert hasattr(node_controller, 'hostname')
        assert hasattr(node_controller, 'config')
        
    def test_node_id_generation(self, node_controller):
        """Test that node_id is properly generated and unique"""
        controller2 = NodeController()
        # Should generate different IDs
        assert node_controller.node_id != controller2.node_id
        # Both should follow the pattern
        assert node_controller.node_id.startswith("node-")
        assert controller2.node_id.startswith("node-")
        
    @pytest.mark.asyncio
    async def test_start_method_exists(self, node_controller):
        """Test that start method exists and can be called"""
        assert hasattr(node_controller, 'start')
        assert callable(node_controller.start)
    
    def test_stop_method_exists(self, node_controller):
        """Test that stop method exists"""
        assert hasattr(node_controller, 'stop')
        assert callable(node_controller.stop)
    
    def test_get_status_method_exists(self, node_controller):
        """Test that get_status method exists"""
        assert hasattr(node_controller, 'get_status')
        assert callable(node_controller.get_status)
        
    def test_component_managers_initialized(self, node_controller):
        """Test that component managers are properly initialized"""
        # These should be None initially until start() is called
        assert node_controller.resource_manager is None
        assert node_controller.worker_manager is None
        assert node_controller.task_dispatcher is None
        assert node_controller.cluster_client is None
        assert node_controller.database is None
