#!/usr/bin/env python3
"""
Test script for the restructured Node Manager
"""
import time
from core.task_manager import TaskConfig
from node import NodeManager

def test_basic_functionality():
    """Test basic functionality of the new architecture"""
    print("="*60)
    print("TESTING RESTRUCTURED NODE MANAGER")
    print("="*60)
    
    # Test 1: Create NodeManager
    print("\n1. Creating NodeManager...")
    try:
        node_manager = NodeManager()
        print("✓ NodeManager created successfully")
    except Exception as e:
        print(f"✗ Failed to create NodeManager: {e}")
        return False
    
    # Test 2: Test configuration
    print("\n2. Testing configuration...")
    try:
        db_config = node_manager.config.get_database_config()
        comm_config = node_manager.config.get_communication_config()
        print(f"✓ Database config loaded: {db_config.get('host', 'N/A')}:{db_config.get('port', 'N/A')}")
        print(f"✓ Communication config loaded: {comm_config.get('worker_timeout', 'N/A')}s timeout")
    except Exception as e:
        print(f"✗ Configuration test failed: {e}")
    
    # Test 3: Test task creation
    print("\n3. Testing task creation...")
    try:
        task_config = TaskConfig(
            prompt="A beautiful landscape with mountains and trees",
            width=1024,
            height=768,
            steps=20
        )
        task_id = node_manager.submit_task(task_config)
        print(f"✓ Task created with ID: {task_id}")
        
        # Check task status
        status = node_manager.task_manager.get_task_status(task_id)
        if status:
            print(f"✓ Task status retrieved: {status['status']}")
        else:
            print("✗ Could not retrieve task status")
    except Exception as e:
        print(f"✗ Task creation failed: {e}")
    
    # Test 4: Test component status
    print("\n4. Testing component status...")
    try:
        status = node_manager.get_status()
        print(f"✓ System status retrieved")
        print(f"  - Tasks: {status['tasks']['queued_tasks']} queued, {status['tasks']['active_tasks']} active")
        print(f"  - Workers: {status['workers']['total_workers']} total")
        print(f"  - Communication: {status['communication']['total_registered_workers']} registered workers")
        print(f"  - API Server: {'Running' if status['api_server']['running'] else 'Stopped'}")
    except Exception as e:
        print(f"✗ Status test failed: {e}")
    
    # Test 5: Test worker registration (simulation)
    print("\n5. Testing worker registration...")
    try:
        success = node_manager.communication.register_worker(
            "test_worker_0", 
            {"device": "cpu", "memory": "8GB"}
        )
        if success:
            print("✓ Worker registration successful")
            
            # Test message sending
            message_id = node_manager.communication.send_message_to_worker(
                "test_worker_0",
                {"type": "test", "message": "Hello worker!"}
            )
            if message_id:
                print(f"✓ Message sent to worker: {message_id}")
            else:
                print("✗ Message sending failed")
        else:
            print("✗ Worker registration failed")
    except Exception as e:
        print(f"✗ Worker registration test failed: {e}")
    
    # Test 6: Test API server info
    print("\n6. Testing API server...")
    try:
        api_info = node_manager.api_server.get_server_info()
        print(f"✓ API server info retrieved")
        print(f"  - Host: {api_info['host']}")
        print(f"  - Port: {api_info['port']}")
        print(f"  - Routes: {len(api_info['routes'])} endpoints")
    except Exception as e:
        print(f"✗ API server test failed: {e}")
    
    print("\n" + "="*60)
    print("ARCHITECTURE TEST COMPLETED")
    print("="*60)
    
    return True

if __name__ == "__main__":
    test_basic_functionality()
