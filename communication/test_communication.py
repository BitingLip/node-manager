"""
Communication Layer Tests
Tests for the node manager communication components
"""

import asyncio
import pytest
import aiohttp
from unittest.mock import Mock, AsyncMock, patch
import json
from datetime import datetime

# Add project root to path
import sys
from pathlib import Path
project_root = Path(__file__).parent.parent.parent.parent
sys.path.insert(0, str(project_root))

from managers.node_manager.communication.cluster_client import ClusterClient
from managers.node_manager.communication.message_queue import MessageQueue
from managers.node_manager.communication.api_server import APIServer


class TestClusterClient:
    """Test ClusterClient functionality"""
    
    @pytest.fixture
    def cluster_client(self):
        """Create a cluster client for testing"""
        return ClusterClient(
            cluster_manager_url="http://localhost:8005",
            node_id="test-node-001",
            auth_token="test-token"
        )
    
    @pytest.mark.asyncio
    async def test_client_initialization(self, cluster_client):
        """Test client initialization"""
        assert cluster_client.node_id == "test-node-001"
        assert cluster_client.cluster_manager_url == "http://localhost:8005"
        assert cluster_client.auth_token == "test-token"
        assert not cluster_client.connected
        assert cluster_client.session is None
    
    @pytest.mark.asyncio
    async def test_connect_success(self, cluster_client):
        """Test successful connection to cluster manager"""
        with patch('aiohttp.ClientSession') as mock_session_class:
            mock_session = AsyncMock()
            mock_session_class.return_value = mock_session
            
            # Mock successful health check
            mock_response = AsyncMock()
            mock_response.status = 200
            mock_session.get.return_value.__aenter__.return_value = mock_response
            
            result = await cluster_client.connect()
            
            assert result is True
            assert cluster_client.connected is True
            assert cluster_client.session is not None
    
    @pytest.mark.asyncio
    async def test_connect_failure(self, cluster_client):
        """Test connection failure"""
        with patch('aiohttp.ClientSession') as mock_session_class:
            mock_session = AsyncMock()
            mock_session_class.return_value = mock_session
            
            # Mock failed health check
            mock_response = AsyncMock()
            mock_response.status = 500
            mock_session.get.return_value.__aenter__.return_value = mock_response
            
            result = await cluster_client.connect()
            
            assert result is False
            assert cluster_client.connected is False
    
    @pytest.mark.asyncio
    async def test_register_node(self, cluster_client):
        """Test node registration"""
        # Setup connected client
        cluster_client.connected = True
        cluster_client.session = AsyncMock()
        
        # Mock successful registration
        mock_response = AsyncMock()
        mock_response.status = 201
        cluster_client.session.post.return_value.__aenter__.return_value = mock_response
        
        # Mock heartbeat start
        with patch.object(cluster_client, 'start_heartbeat_loop') as mock_heartbeat:
            capabilities = {"gpu": True, "cpu_cores": 8}
            resources = {"memory_gb": 32, "storage_gb": 1000}
            
            result = await cluster_client.register_node(capabilities, resources)
            
            assert result is True
            mock_heartbeat.assert_called_once()
    
    @pytest.mark.asyncio
    async def test_send_heartbeat(self, cluster_client):
        """Test heartbeat sending"""
        # Setup connected client
        cluster_client.connected = True
        cluster_client.session = AsyncMock()
        
        # Mock successful heartbeat
        mock_response = AsyncMock()
        mock_response.status = 200
        cluster_client.session.put.return_value.__aenter__.return_value = mock_response
        
        result = await cluster_client.send_heartbeat()
        
        assert result is True
    
    @pytest.mark.asyncio
    async def test_disconnect(self, cluster_client):
        """Test disconnection"""
        # Setup connected client with heartbeat
        cluster_client.connected = True
        cluster_client.session = AsyncMock()
        cluster_client.heartbeat_task = AsyncMock()
        cluster_client.heartbeat_task.done.return_value = False
        
        await cluster_client.disconnect()
        
        assert cluster_client.connected is False
        assert cluster_client.session is None
        cluster_client.heartbeat_task.cancel.assert_called_once()


class TestMessageQueue:
    """Test MessageQueue functionality"""
    
    @pytest.fixture
    def message_queue(self):
        """Create a message queue for testing"""
        return MessageQueue()
    
    def test_initialization(self, message_queue):
        """Test message queue initialization"""
        assert 'tasks' in message_queue.queues
        assert 'status' in message_queue.queues
        assert 'errors' in message_queue.queues
        assert 'heartbeat' in message_queue.queues
        assert 'communication' in message_queue.queues
    
    @pytest.mark.asyncio
    async def test_put_message(self, message_queue):
        """Test putting a message in queue"""
        test_message = {"type": "test", "data": "hello world"}
        
        result = await message_queue.put_message("tasks", test_message, priority=1)
        
        assert result is True
        assert message_queue.queues['tasks'].qsize() == 1
    
    @pytest.mark.asyncio
    async def test_get_message(self, message_queue):
        """Test getting a message from queue"""
        test_message = {"type": "test", "data": "hello world"}
        
        # Put a message first
        await message_queue.put_message("tasks", test_message)
        
        # Get the message
        received = await message_queue.get_message("tasks")
        
        assert received is not None
        assert received['data'] == test_message
        assert 'id' in received
        assert 'timestamp' in received
    
    @pytest.mark.asyncio
    async def test_get_message_timeout(self, message_queue):
        """Test getting message with timeout"""
        # Try to get from empty queue with timeout
        result = await message_queue.get_message("tasks", timeout=0.1)
        
        assert result is None
    
    def test_subscribe_unsubscribe(self, message_queue):
        """Test subscription functionality"""
        callback = Mock()
        
        # Subscribe
        message_queue.subscribe("test.topic", callback)
        assert callback in message_queue.subscribers["test.topic"]
        
        # Unsubscribe
        message_queue.unsubscribe("test.topic", callback)
        assert callback not in message_queue.subscribers.get("test.topic", [])
    
    @pytest.mark.asyncio
    async def test_publish(self, message_queue):
        """Test message publishing"""
        callback = AsyncMock()
        test_message = {"type": "test", "data": "published"}
        
        # Subscribe to topic
        message_queue.subscribe("test.topic", callback)
        
        # Publish message
        await message_queue.publish("test.topic", test_message)
        
        # Verify callback was called
        callback.assert_called_once_with("test.topic", test_message)
    
    def test_queue_management(self, message_queue):
        """Test queue creation and management"""
        # Create new queue
        message_queue.create_queue("custom", maxsize=50)
        assert "custom" in message_queue.queues
        
        # Get queue status
        status = message_queue.get_queue_status("custom")
        assert status["queue_name"] == "custom"
        assert status["maxsize"] == 50
        assert status["size"] == 0
        
        # Clear queue
        message_queue.clear_queue("custom")
        assert message_queue.queues["custom"].qsize() == 0


class TestAPIServer:
    """Test APIServer functionality"""
    
    @pytest.fixture
    def mock_node_controller(self):
        """Create a mock node controller"""
        controller = AsyncMock()
        controller.get_health_status.return_value = {"status": "healthy"}
        controller.get_status.return_value = {"workers": [], "tasks": []}
        controller.submit_task.return_value = "task-123"
        controller.get_task_status.return_value = "running"
        controller.get_workers.return_value = [{"id": "worker-1", "status": "active"}]
        controller.get_metrics.return_value = {"cpu_usage": 45.2}
        return controller
    
    @pytest.fixture
    def api_server(self, mock_node_controller):
        """Create an API server for testing"""
        return APIServer(node_controller=mock_node_controller, port=8080)
    
    def test_initialization(self, api_server):
        """Test API server initialization"""
        assert api_server.port == 8080
        assert api_server.node_controller is not None
        assert api_server.app is not None
    
    @pytest.mark.asyncio
    async def test_health_endpoint(self, api_server):
        """Test health check endpoint"""
        from fastapi.testclient import TestClient
        
        client = TestClient(api_server.app)
        response = client.get("/health")
        
        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "healthy"
    
    @pytest.mark.asyncio
    async def test_status_endpoint(self, api_server):
        """Test status endpoint"""
        from fastapi.testclient import TestClient
        
        client = TestClient(api_server.app)
        response = client.get("/status")
        
        assert response.status_code == 200
        data = response.json()
        assert "workers" in data
        assert "tasks" in data
    
    @pytest.mark.asyncio
    async def test_execute_task_endpoint(self, api_server):
        """Test task execution endpoint"""
        from fastapi.testclient import TestClient
        
        client = TestClient(api_server.app)
        task_data = {"type": "inference", "model": "test-model"}
        
        response = client.post("/tasks/execute", json=task_data)
        
        assert response.status_code == 200
        data = response.json()
        assert data["task_id"] == "task-123"
        assert data["status"] == "queued"


class TestCommunicationIntegration:
    """Integration tests for communication components"""
    
    @pytest.mark.asyncio
    async def test_cluster_client_message_queue_integration(self):
        """Test integration between cluster client and message queue"""
        message_queue = MessageQueue()
        cluster_client = ClusterClient("http://localhost:8005", "test-node")
        
        # Subscribe to cluster events
        cluster_events = []
        
        def on_cluster_event(topic, message):
            cluster_events.append((topic, message))
        
        message_queue.subscribe("cluster.events", on_cluster_event)
        
        # Simulate cluster communication through message queue
        await message_queue.put_message("tasks", {
            "type": "cluster_communication",
            "action": "register_node",
            "data": {"capabilities": {"gpu": True}}
        })
        
        # Verify message was queued
        message = await message_queue.get_message("tasks")
        assert message is not None
        assert message["data"]["action"] == "register_node"
    
    @pytest.mark.asyncio
    async def test_full_communication_flow(self):
        """Test complete communication flow"""
        # Initialize components
        message_queue = MessageQueue()
        
        # Simulate task submission flow
        await message_queue.put_message("tasks", {
            "task_id": "test-task-001",
            "type": "inference",
            "model": "test-model",
            "input_data": {"text": "Hello world"}
        })
        
        # Simulate task processing
        task_message = await message_queue.get_message("tasks")
        assert task_message is not None
        
        # Simulate status update
        await message_queue.put_message("status", {
            "task_id": task_message["data"]["task_id"],
            "status": "completed",
            "result": {"output": "Processed: Hello world"}
        })
        
        # Verify status message
        status_message = await message_queue.get_message("status")
        assert status_message is not None
        assert status_message["data"]["status"] == "completed"


if __name__ == "__main__":
    # Run basic tests if executed directly
    async def run_basic_tests():
        """Run basic functionality tests"""
        print("Testing MessageQueue...")
        mq = MessageQueue()
        
        # Test basic message flow
        await mq.put_message("tasks", {"test": "message"})
        message = await mq.get_message("tasks")
        print(f"Message received: {message}")
        
        print("Testing ClusterClient initialization...")
        client = ClusterClient("http://localhost:8005", "test-node")
        print(f"Client initialized: {client.node_id}")
        
        print("Communication layer tests completed!")
    
    asyncio.run(run_basic_tests())
