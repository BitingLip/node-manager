"""
Simple Node Controller Test
Direct test without package imports
"""

import sys
from pathlib import Path

# Add current directory to path
current_dir = Path(__file__).parent.parent
sys.path.insert(0, str(current_dir))

# Import directly
from core.node_controller import NodeController


def test_node_controller_initialization():
    """Test basic node controller functionality"""
    controller = NodeController()
    
    print(f"✓ NodeController created successfully")
    print(f"  - Node ID: {controller.node_id}")
    print(f"  - Status: {controller.status}")
    print(f"  - Hostname: {controller.hostname}")
    
    # Test node ID format
    assert controller.node_id.startswith("node-"), f"Node ID should start with 'node-', got: {controller.node_id}"
    assert len(controller.node_id) == 13, f"Node ID should be 13 chars long, got: {len(controller.node_id)}"
    
    # Test initial status
    assert controller.status == "initializing", f"Initial status should be 'initializing', got: {controller.status}"
    
    # Test component managers are None initially
    assert controller.resource_manager is None
    assert controller.worker_manager is None
    assert controller.task_dispatcher is None
    assert controller.cluster_client is None
    assert controller.database is None
    
    print("✓ All assertions passed!")


def test_multiple_controllers_unique_ids():
    """Test that multiple controllers get unique IDs"""
    controller1 = NodeController()
    controller2 = NodeController()
    
    assert controller1.node_id != controller2.node_id, "Controllers should have unique node IDs"
    print(f"✓ Unique IDs verified: {controller1.node_id} != {controller2.node_id}")


def test_controller_methods_exist():
    """Test that required methods exist"""
    controller = NodeController()
    
    required_methods = ['start', 'stop', 'get_status']
    for method_name in required_methods:
        assert hasattr(controller, method_name), f"Controller should have {method_name} method"
        assert callable(getattr(controller, method_name)), f"{method_name} should be callable"
    
    print("✓ All required methods exist and are callable")


if __name__ == "__main__":
    print("Running Node Controller Tests...")
    print("=" * 50)
    
    try:
        test_node_controller_initialization()
        test_multiple_controllers_unique_ids()
        test_controller_methods_exist()
        print("=" * 50)
        print("🎉 All tests passed!")
    except Exception as e:
        print(f"❌ Test failed: {e}")
        sys.exit(1)
