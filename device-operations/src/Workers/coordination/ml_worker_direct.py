#!/usr/bin/env python3
"""
Direct ML Worker
================

Simple, stateless execution endpoint for ML inference.
Communicates via stdin/stdout JSON - no HTTP server needed.
"""

import sys
import json
import logging
import asyncio
from pathlib import Path
from typing import Dict, Any

# Add the src directory to Python path
src_dir = Path(__file__).parent.parent
sys.path.insert(0, str(src_dir))

def setup_logging():
    """Setup logging to stderr (stdout is reserved for communication)."""
    logging.basicConfig(
        stream=sys.stderr,
        level=logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )

async def initialize_worker():
    """Initialize the SDXL worker."""
    try:
        # Import with conditional handling for missing ML dependencies
        from Workers.inference.sdxl_worker import SDXLWorker
        from Workers.core.base_worker import WorkerRequest
        
        # Create worker with proper configuration
        worker_config = {
            "output_path": str(Path(__file__).parent.parent.parent / "outputs"),
            "models_path": str(Path(__file__).parent.parent.parent / "models"),
            "enable_safety_checker": False,
            "max_batch_size": 4,
            "enable_xformers": True,
            "enable_compile": False
        }
        
        worker = SDXLWorker("direct_worker", worker_config)
        
        if await worker.initialize():
            return worker
        else:
            raise RuntimeError("Failed to initialize SDXL worker")
            
    except ImportError as e:
        # Graceful handling when ML dependencies aren't installed
        logging.error(f"ML dependencies not available: {e}")
        return None
    except Exception as e:
        logging.error(f"Worker initialization failed: {e}")
        return None

async def process_inference_request(worker, request_data: Dict[str, Any]) -> Dict[str, Any]:
    """Process a single inference request."""
    try:
        # Create worker request
        from Workers.core.base_worker import WorkerRequest
        
        worker_request = WorkerRequest(
            request_id=request_data.get("request_id", "direct_request"),
            worker_type="sdxl_worker",
            data=request_data
        )
        
        # Process the request
        response = await worker.process_request(worker_request)
        
        if response.success:
            return {
                "success": True,
                "request_id": worker_request.request_id,
                "data": response.data,
                "processing_time": getattr(response, 'processing_time', 0)
            }
        else:
            return {
                "success": False,
                "request_id": worker_request.request_id,
                "error": response.error
            }
            
    except Exception as e:
        logging.error(f"Inference processing failed: {str(e)}")
        return {
            "success": False,
            "request_id": request_data.get("request_id", "unknown"),
            "error": str(e)
        }

async def handle_health_check() -> Dict[str, Any]:
    """Handle health check request."""
    return {
        "success": True,
        "status": "healthy",
        "worker_type": "direct_ml_worker",
        "capabilities": ["text2img", "img2img", "inpainting"]
    }

async def handle_model_load(request_data: Dict[str, Any]) -> Dict[str, Any]:
    """Handle model loading request."""
    # For direct worker, models are loaded on-demand during inference
    return {
        "success": True,
        "message": "Models loaded on-demand during inference",
        "model_name": request_data.get("model_name", "default")
    }

async def main():
    """Main worker loop - processes requests from stdin."""
    setup_logging()
    logger = logging.getLogger(__name__)
    
    logger.info("Starting Direct ML Worker...")
    logger.info("Communicating via stdin/stdout JSON")
    
    # Initialize worker once at startup
    worker = await initialize_worker()
    
    if worker is None:
        # Send error response and exit
        error_response = {
            "success": False,
            "error": "Failed to initialize ML worker - check ML dependencies",
            "requires": ["torch", "diffusers", "transformers"]
        }
        print(json.dumps(error_response))
        sys.exit(1)
    
    logger.info("ML Worker initialized successfully")
    
    # Send ready signal
    ready_response = {
        "success": True,
        "status": "ready",
        "message": "Direct ML Worker ready for requests"
    }
    print(json.dumps(ready_response))
    sys.stdout.flush()
    
    # Process requests from stdin
    try:
        for line in sys.stdin:
            line = line.strip()
            if not line:
                continue
                
            try:
                # Parse request
                request = json.loads(line)
                request_type = request.get("type", "inference")
                
                logger.info(f"Processing request: {request_type}")
                
                # Route request based on type
                if request_type == "inference":
                    response = await process_inference_request(worker, request.get("data", {}))
                elif request_type == "health":
                    response = await handle_health_check()
                elif request_type == "load_model":
                    response = await handle_model_load(request.get("data", {}))
                else:
                    response = {
                        "success": False,
                        "error": f"Unknown request type: {request_type}"
                    }
                
                # Send response
                print(json.dumps(response))
                sys.stdout.flush()
                
            except json.JSONDecodeError as e:
                error_response = {
                    "success": False,
                    "error": f"Invalid JSON: {str(e)}"
                }
                print(json.dumps(error_response))
                sys.stdout.flush()
                
            except Exception as e:
                logger.error(f"Request processing error: {str(e)}")
                error_response = {
                    "success": False,
                    "error": str(e)
                }
                print(json.dumps(error_response))
                sys.stdout.flush()
                
    except KeyboardInterrupt:
        logger.info("Worker interrupted by user")
    except Exception as e:
        logger.error(f"Fatal error: {str(e)}")
    finally:
        # Cleanup
        if worker:
            await worker.cleanup()
        logger.info("Direct ML Worker shutdown complete")

if __name__ == "__main__":
    asyncio.run(main())