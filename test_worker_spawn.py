#!/usr/bin/env python3
"""
Test script to verify multiprocessing worker spawn works correctly
"""
import sys
import time
from pathlib import Path

# Add the project root to Python path to ensure core modules can be imported
project_root = Path(__file__).parent
sys.path.insert(0, str(project_root))

from core.worker_manager import WorkerManager
from core.logger import Logger
from core.config import Config

def test_worker_spawn():
    """Test that worker processes can be spawned without import errors"""
    print("Testing worker spawn functionality...")
      # Initialize components
    config = Config("node_config.json")
    logger = Logger("TestWorkerSpawn", 0)
    
    # Create worker manager
    worker_manager = WorkerManager(
        database=None,  # Don't need database for this test
        logger=logger,
        communication=None,  # Don't need communication for this test
        config=config.config_data.get("workers", {})
    )
    
    # Try to spawn a single worker process
    print("Attempting to spawn worker for device 0...")
    success = worker_manager.spawn_worker_process(0)
    
    if success:
        print("✓ Worker spawned successfully!")
        
        # Wait a moment then check if process is alive
        time.sleep(2)
        
        if 0 in worker_manager.worker_processes:
            process = worker_manager.worker_processes[0]
            if process.is_alive():
                print("✓ Worker process is running!")
                
                # Clean up
                process.terminate()
                process.join(timeout=5)
                print("✓ Worker process terminated cleanly")
                return True
            else:
                print("✗ Worker process is not alive")
                return False
        else:
            print("✗ Worker process not found in tracking")
            return False
    else:
        print("✗ Failed to spawn worker")
        return False

if __name__ == "__main__":
    success = test_worker_spawn()
    sys.exit(0 if success else 1)
