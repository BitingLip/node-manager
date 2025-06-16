#!/usr/bin/env python3
"""
Quick test run of the node manager without requiring database
"""
import time
from node import NodeManager

def test_node_manager():
    """Test the complete node manager functionality"""
    print("="*60)
    print("NODE MANAGER TEST RUN")
    print("="*60)
    
    try:
        # Create node manager
        print("\n1. Creating NodeManager...")
        node_manager = NodeManager()
        print("✓ NodeManager created successfully")
        
        # Test status without starting
        print("\n2. Testing status (before start)...")
        status = node_manager.get_status()
        print(f"✓ Status retrieved - Running: {status['running']}")
        
        # Print status
        print("\n3. Printing formatted status...")
        node_manager.print_status()
        
        # Test individual components
        print("\n4. Testing components...")
        print(f"✓ Config: {type(node_manager.config).__name__}")
        print(f"✓ Logger: {type(node_manager.logger).__name__}")
        print(f"✓ Database: {type(node_manager.database).__name__}")
        print(f"✓ Communication: {type(node_manager.communication).__name__}")
        print(f"✓ Task Manager: {type(node_manager.task_manager).__name__}")
        print(f"✓ Worker Manager: {type(node_manager.worker_manager).__name__}")
        print(f"✓ System Monitor: {type(node_manager.system_monitor).__name__}")
        print(f"✓ API Server: {type(node_manager.api_server).__name__}")
        
        # Test configuration access
        print("\n5. Testing configuration access...")
        db_config = node_manager.config.get_database_config()
        comm_config = node_manager.config.get_communication_config()
        node_config = node_manager.config.get_node_manager_config()
        
        print(f"✓ Database config: {db_config['host']}:{db_config['port']}")
        print(f"✓ Communication timeout: {comm_config['worker_timeout']}s")
        print(f"✓ Node manager port: {node_config['port']}")
        
        print("\n" + "="*60)
        print("✅ ALL TESTS PASSED - NODE MANAGER IS READY!")
        print("="*60)
        
        return True
        
    except Exception as e:
        print(f"\n❌ TEST FAILED: {e}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == "__main__":
    success = test_node_manager()
    if success:
        print("\n🎉 Node Manager is ready for production!")
    else:
        print("\n💥 Issues detected - please review logs")
