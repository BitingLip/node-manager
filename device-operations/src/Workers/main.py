"""
Main Worker Entry Point
=======================

Entry point for the modular SDXL workers system. Handles initialization,
communication setup, and request routing to appropriate workers.
"""

import sys
import asyncio
import logging
import argparse
from pathlib import Path
from typing import Dict, Any, Optional

# Add the Workers directory to the Python path
workers_dir = Path(__file__).parent
sys.path.insert(0, str(workers_dir))

from core.base_worker import setup_worker_logging
from core.communication import setup_worker_communication, MessageProtocol
from core.device_manager import initialize_device_manager
from inference.sdxl_worker import SDXLWorker
from inference.pipeline_manager import PipelineManager
from models.model_loader import ModelLoader


class WorkerOrchestrator:
    """
    Main orchestrator for the SDXL workers system.
    
    Manages multiple specialized workers and routes requests to appropriate handlers.
    """
    
    def __init__(self, config: Optional[Dict[str, Any]] = None):
        self.config = config or {}
        self.logger = logging.getLogger(__name__)
        
        # Workers
        self.workers: Dict[str, Any] = {}
        self.comm_manager = None
        
        # Default worker configuration
        self.default_worker = self.config.get("default_worker", "pipeline_manager")
        
    async def initialize(self) -> bool:
        """Initialize the worker orchestrator."""
        try:
            self.logger.info("Initializing SDXL Workers System...")
            
            # Initialize device manager
            if not initialize_device_manager():
                self.logger.error("Failed to initialize device manager")
                return False
            
            # Initialize workers based on configuration
            worker_configs = self.config.get("workers", {})
            
            # Initialize Pipeline Manager (default)
            if "pipeline_manager" in worker_configs or self.default_worker == "pipeline_manager":
                pipeline_config = worker_configs.get("pipeline_manager", {})
                pipeline_manager = PipelineManager("pipeline_manager", pipeline_config)
                if await pipeline_manager.initialize():
                    self.workers["pipeline_manager"] = pipeline_manager
                    self.logger.info("Pipeline Manager initialized")
                else:
                    self.logger.error("Failed to initialize Pipeline Manager")
                    return False
            
            # Initialize SDXL Worker (standalone)
            if "sdxl_worker" in worker_configs:
                sdxl_config = worker_configs.get("sdxl_worker", {})
                sdxl_worker = SDXLWorker("sdxl_worker", sdxl_config)
                if await sdxl_worker.initialize():
                    self.workers["sdxl_worker"] = sdxl_worker
                    self.logger.info("SDXL Worker initialized")
                else:
                    self.logger.error("Failed to initialize SDXL Worker")
            
            # Initialize Model Loader (standalone)
            if "model_loader" in worker_configs:
                model_config = worker_configs.get("model_loader", {})
                model_loader = ModelLoader("model_loader", model_config)
                if await model_loader.initialize():
                    self.workers["model_loader"] = model_loader
                    self.logger.info("Model Loader initialized")
                else:
                    self.logger.error("Failed to initialize Model Loader")
            
            # Setup communication
            schema_path = self.config.get("schema_path")
            self.comm_manager = setup_worker_communication(schema_path)
            
            # Register message handlers
            self._register_message_handlers()
            
            self.logger.info(f"Worker orchestrator initialized with {len(self.workers)} workers")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to initialize worker orchestrator: {str(e)}")
            return False
    
    def _register_message_handlers(self) -> None:
        """Register message handlers for different request types."""
        if self.comm_manager is None:
            return
            
        self.comm_manager.register_handler(
            MessageProtocol.INFERENCE_REQUEST,
            self._handle_inference_request
        )
        
        self.comm_manager.register_handler(
            MessageProtocol.MODEL_LOAD_REQUEST,
            self._handle_model_load_request
        )
        
        self.comm_manager.register_handler(
            MessageProtocol.STATUS_REQUEST,
            self._handle_status_request
        )
        
        self.comm_manager.register_handler(
            MessageProtocol.SHUTDOWN_REQUEST,
            self._handle_shutdown_request
        )
    
    async def _handle_inference_request(self, message_data: Dict[str, Any]) -> Dict[str, Any]:
        """Handle inference requests."""
        try:
            request_id = message_data.get("request_id", "unknown")
            data = message_data.get("data", {})
            
            # Determine which worker to use
            worker_type = data.get("worker_type", self.default_worker)
            
            if worker_type not in self.workers:
                return MessageProtocol.create_response(
                    request_id,
                    False,
                    error=f"Worker type '{worker_type}' not available"
                )
            
            # Create worker request
            from core.base_worker import WorkerRequest
            worker_request = WorkerRequest(
                request_id=request_id,
                worker_type=worker_type,
                data=data,
                priority=data.get("priority", "normal"),
                timeout=data.get("timeout", 300)
            )
            
            # Process request
            worker = self.workers[worker_type]
            response = await worker.process_request(worker_request)
            
            # Convert to message response
            return MessageProtocol.create_response(
                request_id,
                response.success,
                data=response.data,
                error=response.error
            )
            
        except Exception as e:
            self.logger.error(f"Error handling inference request: {str(e)}")
            return MessageProtocol.create_response(
                message_data.get("request_id", "unknown"),
                False,
                error=f"Request processing error: {str(e)}"
            )
    
    async def _handle_model_load_request(self, message_data: Dict[str, Any]) -> Dict[str, Any]:
        """Handle model loading requests."""
        try:
            request_id = message_data.get("request_id", "unknown")
            data = message_data.get("data", {})
            
            # Use model loader if available, otherwise use pipeline manager
            if "model_loader" in self.workers:
                worker = self.workers["model_loader"]
            elif "pipeline_manager" in self.workers:
                worker = self.workers["pipeline_manager"]
            else:
                return MessageProtocol.create_response(
                    request_id,
                    False,
                    error="No model loading worker available"
                )
            
            # Create worker request
            from core.base_worker import WorkerRequest
            worker_request = WorkerRequest(
                request_id=request_id,
                worker_type="model_loader",
                data=data
            )
            
            # Process request
            response = await worker.process_request(worker_request)
            
            return MessageProtocol.create_response(
                request_id,
                response.success,
                data=response.data,
                error=response.error
            )
            
        except Exception as e:
            self.logger.error(f"Error handling model load request: {str(e)}")
            return MessageProtocol.create_response(
                message_data.get("request_id", "unknown"),
                False,
                error=f"Model loading error: {str(e)}"
            )
    
    async def _handle_status_request(self, message_data: Dict[str, Any]) -> Dict[str, Any]:
        """Handle status requests."""
        try:
            request_id = message_data.get("request_id", "unknown")
            
            status_data = {
                "orchestrator": {
                    "workers": list(self.workers.keys()),
                    "default_worker": self.default_worker,
                    "active": True
                },
                "workers": {}
            }
            
            # Get status from each worker
            for worker_name, worker in self.workers.items():
                try:
                    status_data["workers"][worker_name] = worker.get_status()
                except Exception as e:
                    status_data["workers"][worker_name] = {"error": str(e)}
            
            return MessageProtocol.create_response(
                request_id,
                True,
                data=status_data
            )
            
        except Exception as e:
            self.logger.error(f"Error handling status request: {str(e)}")
            return MessageProtocol.create_response(
                message_data.get("request_id", "unknown"),
                False,
                error=f"Status request error: {str(e)}"
            )
    
    async def _handle_shutdown_request(self, message_data: Dict[str, Any]) -> Dict[str, Any]:
        """Handle shutdown requests."""
        try:
            request_id = message_data.get("request_id", "unknown")
            
            self.logger.info("Shutdown request received")
            
            # Clean up all workers
            for worker_name, worker in self.workers.items():
                try:
                    await worker.cleanup()
                    self.logger.info(f"Worker {worker_name} cleaned up")
                except Exception as e:
                    self.logger.error(f"Error cleaning up worker {worker_name}: {str(e)}")
            
            return MessageProtocol.create_response(
                request_id,
                True,
                data={"message": "Shutdown completed"}
            )
            
        except Exception as e:
            self.logger.error(f"Error handling shutdown request: {str(e)}")
            return MessageProtocol.create_response(
                message_data.get("request_id", "unknown"),
                False,
                error=f"Shutdown error: {str(e)}"
            )
    
    async def run(self) -> None:
        """Run the worker orchestrator."""
        try:
            self.logger.info("Starting SDXL Workers System...")
            
            # Start the communication loop
            if self.comm_manager:
                await self.comm_manager.start_message_loop()
            else:
                self.logger.error("Communication manager not initialized")
            
        except KeyboardInterrupt:
            self.logger.info("Received interrupt signal")
        except Exception as e:
            self.logger.error(f"Runtime error: {str(e)}")
        finally:
            await self.cleanup()
    
    async def cleanup(self) -> None:
        """Clean up orchestrator resources."""
        self.logger.info("Cleaning up SDXL Workers System...")
        
        # Clean up all workers
        for worker_name, worker in self.workers.items():
            try:
                await worker.cleanup()
                self.logger.info(f"Worker {worker_name} cleaned up")
            except Exception as e:
                self.logger.error(f"Error cleaning up worker {worker_name}: {str(e)}")
        
        self.workers.clear()
        self.logger.info("SDXL Workers System shutdown complete")


def load_config(config_path: Optional[str] = None) -> Dict[str, Any]:
    """Load configuration from file or use defaults."""
    if config_path and Path(config_path).exists():
        import json
        with open(config_path, 'r') as f:
            return json.load(f)
    
    # Default configuration
    return {
        "default_worker": "pipeline_manager",
        "workers": {
            "pipeline_manager": {
                "max_concurrent_tasks": 2,
                "task_timeout": 600,
                "sdxl_worker": {
                    "output_path": "./outputs",
                    "enable_safety_checker": False,
                    "max_batch_size": 4,
                    "enable_xformers": True,
                    "enable_compile": False
                },
                "model_loader": {
                    "max_cache_memory_gb": 8.0,
                    "max_cached_models": 5,
                    "models_path": "./models",
                    "loras_path": "./models/loras",
                    "textual_inversions_path": "./models/textual_inversions",
                    "vaes_path": "./models/vaes"
                }
            }
        },
        "logging": {
            "level": "INFO",
            "file": None
        }
    }


async def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(description="SDXL Workers System")
    parser.add_argument("--config", type=str, help="Configuration file path")
    parser.add_argument("--schema", type=str, help="JSON schema file path")
    parser.add_argument("--log-level", type=str, default="INFO", 
                       choices=["DEBUG", "INFO", "WARNING", "ERROR"],
                       help="Logging level")
    parser.add_argument("--log-file", type=str, help="Log file path")
    parser.add_argument("--worker", type=str, default="pipeline_manager",
                       choices=["pipeline_manager", "sdxl_worker", "model_loader"],
                       help="Default worker type")
    
    args = parser.parse_args()
    
    # Setup logging
    setup_worker_logging(args.log_level, args.log_file)
    logger = logging.getLogger(__name__)
    
    try:
        # Load configuration
        config = load_config(args.config)
        
        # Override default worker if specified
        if args.worker:
            config["default_worker"] = args.worker
        
        # Set schema path if specified
        if args.schema:
            config["schema_path"] = args.schema
        
        # Create and run orchestrator
        orchestrator = WorkerOrchestrator(config)
        
        if await orchestrator.initialize():
            await orchestrator.run()
        else:
            logger.error("Failed to initialize worker orchestrator")
            sys.exit(1)
            
    except Exception as e:
        logger.error(f"Fatal error: {str(e)}")
        sys.exit(1)


if __name__ == "__main__":
    asyncio.run(main())
