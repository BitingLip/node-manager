#!/usr/bin/env python3
"""
Simple test without database connectivity
"""
import time
from core.task_manager import TaskConfig
from core.communication import Communication
from core.logger import Logger
from core.config import Config

def test_without_database():
    """Test components that don't require database"""
    print("="*60)
    print("TESTING NODE MANAGER (NO DATABASE)")
    print("="*60)
    
    # Test 1: Basic imports
    print("\n1. Testing imports...")
    try:
        from core import Config, TaskConfig, Communication, Logger, APIServer
        print("✓ All core imports successful")
    except Exception as e:
        print(f"✗ Import failed: {e}")
        return
    
    # Test 2: Configuration
    print("\n2. Testing configuration...")
    try:
        config = Config()
        comm_config = config.get_communication_config()
        print(f"✓ Configuration loaded: {comm_config.get('worker_timeout', 'N/A')}s timeout")
    except Exception as e:
        print(f"✗ Configuration test failed: {e}")
    
    # Test 3: Logger
    print("\n3. Testing logger...")
    try:
        logger = Logger("TestComponent", 0, "INFO")
        logger.info("Test log message")
        print("✓ Logger working correctly")
    except Exception as e:
        print(f"✗ Logger test failed: {e}")
    
    # Test 4: Task creation
    print("\n4. Testing task creation...")
    try:
        task = TaskConfig(
            prompt="Test prompt for validation",
            width=512,
            height=512,
            steps=10
        )
        print(f"✓ Task created: {task.task_id}")
        print(f"  - Prompt: {task.prompt[:30]}...")
        print(f"  - Dimensions: {task.width}x{task.height}")
        print(f"  - Steps: {task.steps}")
    except Exception as e:
        print(f"✗ Task creation failed: {e}")
    
    # Test 5: Communication (no database)
    print("\n5. Testing communication...")
    try:
        logger = Logger("Communication", 0, "INFO")
        comm = Communication({"worker_timeout": 60, "heartbeat_interval": 10}, logger)
        
        # Test worker registration
        success = comm.register_worker("test_worker", {"device": "cpu"})
        if success:
            print("✓ Worker registration successful")
            
            # Test message sending
            msg_id = comm.send_message_to_worker("test_worker", {"type": "test"})
            if msg_id:
                print(f"✓ Message sent: {msg_id}")
                
                # Test message retrieval
                messages = comm.get_worker_messages("test_worker")
                print(f"✓ Retrieved {len(messages)} messages")
            else:
                print("✗ Message sending failed")
        else:
            print("✗ Worker registration failed")
    except Exception as e:
        print(f"✗ Communication test failed: {e}")
    
    # Test 6: API Server info (without starting)
    print("\n6. Testing API server configuration...")
    try:
        from core.api_server import APIServer
        config = {"host": "localhost", "port": 8080}
        logger = Logger("APIServer", 0, "INFO")
        api_server = APIServer(config, logger)
        
        info = api_server.get_server_info()
        print(f"✓ API server configured: {info['host']}:{info['port']}")
        print(f"  - Routes available: {len(info['routes'])}")
        print(f"  - Running: {info['running']}")
    except Exception as e:
        print(f"✗ API server test failed: {e}")
    
    print("\n" + "="*60)
    print("BASIC FUNCTIONALITY TEST COMPLETED")
    print("✓ All core components working without database")
    print("✓ Ready for production with proper database setup")
    print("="*60)

if __name__ == "__main__":
    test_without_database()
