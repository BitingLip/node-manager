"""
Simple Communication Test
Basic verification that communication components work
"""

import asyncio
import sys
from pathlib import Path

# Add parent directories to path for imports
current_dir = Path(__file__).parent
sys.path.insert(0, str(current_dir))

try:
    from cluster_client import ClusterClient
    from message_queue import MessageQueue
    print("✓ Successfully imported communication components")
except ImportError as e:
    print(f"✗ Import error: {e}")
    sys.exit(1)


async def test_message_queue():
    """Test MessageQueue basic functionality"""
    print("\n=== Testing MessageQueue ===")
    
    try:
        mq = MessageQueue()
        print("✓ MessageQueue initialized")
        
        # Test message put/get
        test_message = {"type": "test", "data": "hello world"}
        result = await mq.put_message("tasks", test_message)
        print(f"✓ Put message result: {result}")
        
        message = await mq.get_message("tasks")
        if message:
            print(f"✓ Retrieved message: {message['data']['type']}")
        else:
            print("✗ No message retrieved")
        
        # Test queue status
        status = mq.get_all_queue_status()
        print(f"✓ Queue status: {len(status)} queues available")
        
        # Test pub/sub
        events = []
        def callback(topic, msg):
            events.append((topic, msg))
        
        mq.subscribe("test.topic", callback)
        await mq.publish("test.topic", {"event": "test"})
        print(f"✓ Pub/sub test: {len(events)} events received")
        
        return True
        
    except Exception as e:
        print(f"✗ MessageQueue test failed: {e}")
        return False


def test_cluster_client():
    """Test ClusterClient initialization"""
    print("\n=== Testing ClusterClient ===")
    
    try:
        client = ClusterClient("http://localhost:8005", "test-node-001")
        print(f"✓ Client initialized with node ID: {client.node_id}")
        print(f"✓ Cluster URL: {client.cluster_manager_url}")
        print(f"✓ Connected: {client.connected}")
        print(f"✓ Heartbeat interval: {client.heartbeat_interval}s")
        
        # Test header preparation
        headers = client._prepare_headers()
        print(f"✓ Headers prepared: {len(headers)} headers")
        
        return True
        
    except Exception as e:
        print(f"✗ ClusterClient test failed: {e}")
        return False


async def test_integration():
    """Test basic integration between components"""
    print("\n=== Testing Integration ===")
    
    try:
        # Initialize components
        mq = MessageQueue()
        client = ClusterClient("http://localhost:8005", "test-node")
        
        # Test communication flow simulation
        await mq.put_message("tasks", {
            "type": "cluster_communication",
            "action": "register_node",
            "data": {"capabilities": {"gpu": True}}
        })
        
        # Retrieve and process message
        message = await mq.get_message("tasks")
        if message and message['data']['action'] == "register_node":
            print("✓ Cluster communication message flow works")
        else:
            print("✗ Message flow failed")
            return False
        
        # Test status reporting flow
        await mq.put_message("status", {
            "task_id": "test-task-001",
            "status": "completed",
            "result": {"output": "success"}
        })
        
        status_msg = await mq.get_message("status")
        if status_msg and status_msg['data']['status'] == "completed":
            print("✓ Status reporting flow works")
        else:
            print("✗ Status reporting failed")
            return False
        
        return True
        
    except Exception as e:
        print(f"✗ Integration test failed: {e}")
        return False


async def main():
    """Run all tests"""
    print("🚀 Starting Communication Layer Tests")
    print("=" * 50)
    
    tests = [
        ("MessageQueue", test_message_queue()),
        ("ClusterClient", test_cluster_client()),
        ("Integration", test_integration())
    ]
    
    results = []
    for test_name, test_coro in tests:
        if asyncio.iscoroutine(test_coro):
            result = await test_coro
        else:
            result = test_coro
        results.append((test_name, result))
    
    print("\n" + "=" * 50)
    print("📊 Test Results:")
    
    passed = 0
    for test_name, result in results:
        status = "✓ PASS" if result else "✗ FAIL"
        print(f"  {test_name}: {status}")
        if result:
            passed += 1
    
    print(f"\nOverall: {passed}/{len(results)} tests passed")
    
    if passed == len(results):
        print("🎉 All communication layer tests passed!")
        print("\n💡 Communication layer is ready for integration!")
    else:
        print("⚠️  Some tests failed. Check the implementation.")
    
    return passed == len(results)


if __name__ == "__main__":
    success = asyncio.run(main())
    sys.exit(0 if success else 1)
