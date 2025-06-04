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

from managers.node_manager.core.node_controller import NodeController


class TestNodeController:
    """Test cases for NodeController"""
    
    @pytest.fixture
    def node_controller(self):
        """Create test node controller instance"""
        return NodeController()
    
    def test_initialization(self, node_controller):
        """Test node controller initialization"""
        assert node_controller.status == "initializing"
        assert node_controller.node_id is None
        
    @pytest.mark.asyncio
    async def test_start_method_exists(self, node_controller):
        """Test that start method exists and can be called"""
        # Since start() is not implemented yet, just test it exists
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
