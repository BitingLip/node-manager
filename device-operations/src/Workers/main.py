#!/usr/bin/env python3
"""
Main Worker Entry Point for GPU Pool Workers
============================================

Real SDXL inference worker supporting actual model loading and generation.
Uses DirectML for AMD GPU acceleration with new hierarchical structure.
"""

import sys
import asyncio
import logging
import json
import os
import time
from pathlib import Path
from typing import Dict, Any, Optional, List

# Add the src directory to Python path so we can import Workers
script_dir = Path(__file__).parent  # Workers directory
src_dir = script_dir.parent         # src directory
project_root = src_dir.parent       # project root

# Add multiple potential paths to ensure Workers can be imported
sys.path.insert(0, str(src_dir))           # For import Workers
sys.path.insert(0, str(script_dir))        # For direct worker imports
sys.path.insert(0, str(project_root))      # For project-level imports

def setup_logging():
    """Setup logging to stderr (stdout is reserved for communication)."""
    logging.basicConfig(
        stream=sys.stderr,
        level=logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )

# Debug path information after logging setup
setup_logging()
logger = logging.getLogger(__name__)
logger.info("GPU Pool Worker starting with new hierarchical structure...")
logger.info("Script location: %s", Path(__file__))
logger.info("Workers directory: %s", script_dir)

async def initialize_workers_interface():
    """Initialize the new Workers interface."""
    config = {}  # Initialize config variable
    
    try:
        # Apply DirectML patches first if available
        try:
            # Try multiple patch locations based on new structure
            try:
                from utilities.dml_patch import apply_dml_patches
                apply_dml_patches()
                logger.info("DirectML patches applied successfully")
            except ImportError:
                try:
                    from Workers.utilities.dml_patch import apply_dml_patches
                    apply_dml_patches()
                    logger.info("DirectML patches applied successfully (Workers path)")
                except ImportError:
                    logger.warning("DirectML patches not available - continuing without patches")
        except Exception as e:
            logger.warning("DirectML patch error: %s - continuing without patches", e)
        
        # Load configuration - try config folder first, then local fallback
        config_file = script_dir.parent.parent / "config" / "workers_config.json"
        if not config_file.exists():
            # Fallback to local workers_config.json
            config_file = script_dir / "workers_config.json"
            
        if config_file.exists():
            with open(config_file, 'r') as f:
                config = json.load(f)
        else:
            config = {
                "workers": {
                    "main_interface": {"enabled": True, "log_level": "INFO"},
                    "instructors": {
                        "device": {"enabled": True, "auto_detect": True},
                        "inference": {"enabled": True, "batch_size": 1},
                        "model": {"enabled": True, "cache_size": "1024MB"}
                    }
                }
            }
        
        # Initialize main interface with new structure
        from interface_main import WorkersInterface
        logger.info("Initializing WorkersInterface...")
        interface = WorkersInterface(config)
        await interface.initialize()
        
        logger.info("GPU worker initialization completed using new hierarchical structure")
        return interface
        
    except ImportError as e:
        logger.error("Failed to import WorkersInterface: %s", e)
        logger.info("Attempting compatibility layer...")
        
        # Fallback to compatibility imports
        try:
            from compatibility import WorkersInterface as CompatInterface
            interface = CompatInterface(config)
            await interface.initialize()
            logger.info("Compatibility layer initialized successfully")
            return interface
        except Exception as compat_e:
            logger.error("Compatibility layer also failed: %s", compat_e)
            return None
        
    except Exception as e:
        logger.error("Failed to initialize Workers interface: %s", e)
        return None

async def process_inference_request(interface, request_data: Dict[str, Any]) -> Dict[str, Any]:
    """Process a single inference request through the new interface."""
    try:
        # Process the request through the new hierarchical interface
        response = await interface.process_request({
            "request_id": request_data.get("request_id", "main_request"),
            "worker_type": "inference",
            "operation": "generate",
            "data": request_data
        })
        
        if response.get("status") == "success":
            return {
                "success": True,
                "request_id": request_data.get("request_id", "main_request"),
                "data": response.get("data", {}),
                "worker_info": "new_hierarchical_structure",
                "timestamp": time.time()
            }
        else:
            return {
                "success": False,
                "request_id": request_data.get("request_id", "main_request"),
                "error": response.get("error", "Unknown error"),
                "worker_info": "new_hierarchical_structure",
                "timestamp": time.time()
            }
            
    except Exception as e:
        logger.error("Request processing failed: %s", e)
        return {
            "success": False,
            "request_id": request_data.get("request_id", "main_request"),
            "error": f"Processing error: {str(e)}",
            "worker_info": "new_hierarchical_structure",
            "timestamp": time.time()
        }

async def handle_communication():
    """Handle stdin/stdout communication with new interface."""
    logger.info("Starting communication handler with new interface...")
    
    # Initialize the workers interface
    interface = await initialize_workers_interface()
    if not interface:
        logger.error("Failed to initialize interface - exiting")
        return False
    
    logger.info("Ready to process requests through new hierarchical interface")
    
    try:
        while True:
            # Read request from stdin
            line = await asyncio.get_event_loop().run_in_executor(None, sys.stdin.readline)
            if not line:
                break
            
            line = line.strip()
            if not line:
                continue
            
            try:
                # Parse JSON request
                request_data = json.loads(line)
                logger.info("Processing request: %s", request_data.get("request_id", "unknown"))
                
                # Process through new interface
                response = await process_inference_request(interface, request_data)
                
                # Send JSON response to stdout
                print(json.dumps(response), flush=True)
                
            except json.JSONDecodeError as e:
                logger.error("Invalid JSON request: %s", e)
                error_response = {
                    "success": False,
                    "error": f"Invalid JSON: {str(e)}",
                    "worker_info": "new_hierarchical_structure",
                    "timestamp": time.time()
                }
                print(json.dumps(error_response), flush=True)
                
            except Exception as e:
                logger.error("Request processing error: %s", e)
                error_response = {
                    "success": False,
                    "error": f"Processing error: {str(e)}",
                    "worker_info": "new_hierarchical_structure", 
                    "timestamp": time.time()
                }
                print(json.dumps(error_response), flush=True)
    
    except KeyboardInterrupt:
        logger.info("Received interrupt signal - shutting down gracefully")
    except Exception as e:
        logger.error("Communication handler error: %s", e)
        return False
    finally:
        # Cleanup
        if interface:
            try:
                await interface.cleanup()
                logger.info("Interface cleanup completed")
            except Exception as e:
                logger.error("Cleanup error: %s", e)
    
    return True

def main():
    """Main entry point."""
    try:
        logger.info("Starting main GPU pool worker with new hierarchical structure")
        
        # Run the communication handler
        success = asyncio.run(handle_communication())
        
        if success:
            logger.info("Worker completed successfully")
            sys.exit(0)
        else:
            logger.error("Worker failed")
            sys.exit(1)
            
    except KeyboardInterrupt:
        logger.info("Received interrupt - exiting")
        sys.exit(0)
    except Exception as e:
        logger.error("Main process error: %s", e)
        sys.exit(1)

if __name__ == "__main__":
    main()
