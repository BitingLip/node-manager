"""
Inference Interface for SDXL Workers System
==========================================

Unified interface for inference operations.
"""

import logging
from typing import Dict, Any, Optional


class InferenceInterface:
    """
    Unified interface for inference operations.
    
    This interface provides a consistent API for inference operations
    and delegates to appropriate managers and workers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(f"{__name__}.{self.__class__.__name__}")
        
        # Manager and worker instances
        self.batch_manager = None
        self.pipeline_manager = None
        self.memory_manager = None
        self.sdxl_worker = None
        self.controlnet_worker = None
        self.lora_worker = None
        
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize inference interface and components."""
        try:
            self.logger.info("Initializing inference interface...")
            
            # Import components (lazy loading)
            from .managers.manager_batch import BatchManager
            from .managers.manager_pipeline import PipelineManager
            from .managers.manager_memory import MemoryManager
            from .workers.worker_sdxl import SDXLWorker
            from .workers.worker_controlnet import ControlNetWorker
            from .workers.worker_lora import LoRAWorker
            
            # Create components
            self.batch_manager = BatchManager(self.config)
            self.pipeline_manager = PipelineManager(self.config)
            self.memory_manager = MemoryManager(self.config)
            self.sdxl_worker = SDXLWorker(self.config)
            self.controlnet_worker = ControlNetWorker(self.config)
            self.lora_worker = LoRAWorker(self.config)
            
            # Initialize components
            components = [
                self.batch_manager,
                self.pipeline_manager,
                self.memory_manager,
                self.sdxl_worker,
                self.controlnet_worker,
                self.lora_worker
            ]
            
            for component in components:
                if not await component.initialize():
                    self.logger.error("Failed to initialize %s", component.__class__.__name__)
                    return False
                    
            self.initialized = True
            self.logger.info("Inference interface initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error("Inference interface initialization failed: %s", e)
            return False
    
    async def text2img(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process text-to-image inference request."""
        if not self.initialized:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            inference_data = request.get("data", {})
            inference_data["type"] = "text2img"
            result = await self.sdxl_worker.process_inference(inference_data)
            return {
                "success": True,
                "data": result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def img2img(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process image-to-image inference request."""
        if not self.initialized:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            inference_data = request.get("data", {})
            inference_data["type"] = "img2img"
            result = await self.sdxl_worker.process_inference(inference_data)
            return {
                "success": True,
                "data": result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def inpainting(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process inpainting inference request."""
        if not self.initialized:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            inference_data = request.get("data", {})
            inference_data["type"] = "inpainting"
            result = await self.sdxl_worker.process_inference(inference_data)
            return {
                "success": True,
                "data": result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def controlnet(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process ControlNet inference request."""
        if not self.initialized:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            controlnet_data = request.get("data", {})
            result = await self.controlnet_worker.process_controlnet(controlnet_data)
            return {
                "success": True,
                "data": result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def lora(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process LoRA inference request."""
        if not self.initialized:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            lora_data = request.get("data", {})
            result = await self.lora_worker.process_lora(lora_data)
            return {
                "success": True,
                "data": result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def batch_process(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process batch inference request."""
        if not self.initialized:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            batch_data = request.get("data", {})
            result = await self.batch_manager.process_batch(batch_data)
            return {
                "success": True,
                "data": result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_pipeline_info(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get pipeline information."""
        if not self.initialized:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            info = await self.pipeline_manager.get_pipeline_info()
            return {
                "success": True,
                "data": info,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get inference interface status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            status = {
                "status": "healthy",
                "initialized": self.initialized,
                "components": {}
            }
            
            # Collect status from all components
            components = [
                ("batch_manager", self.batch_manager),
                ("pipeline_manager", self.pipeline_manager),
                ("memory_manager", self.memory_manager),
                ("sdxl_worker", self.sdxl_worker),
                ("controlnet_worker", self.controlnet_worker),
                ("lora_worker", self.lora_worker)
            ]
            
            for name, component in components:
                if component:
                    try:
                        status["components"][name] = await component.get_status()
                    except Exception as e:
                        status["components"][name] = {"error": str(e)}
                        
            return status
            
        except Exception as e:
            return {"status": "error", "error": str(e)}
    
    async def cleanup(self) -> None:
        """Clean up inference interface resources."""
        try:
            self.logger.info("Cleaning up inference interface...")
            
            # Cleanup components
            components = [
                self.lora_worker,
                self.controlnet_worker,
                self.sdxl_worker,
                self.memory_manager,
                self.pipeline_manager,
                self.batch_manager
            ]
            
            for component in components:
                if component:
                    try:
                        await component.cleanup()
                    except Exception as e:
                        self.logger.warning("Error during component cleanup: %s", e)
            
            self.initialized = False
            self.logger.info("Inference interface cleanup complete")
            
        except Exception as e:
            self.logger.error("Inference interface cleanup error: %s", e)


# Factory function for creating inference interface
def create_inference_interface(config: Optional[Dict[str, Any]] = None) -> InferenceInterface:
    """
    Factory function to create an inference interface instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        InferenceInterface instance
    """
    return InferenceInterface(config or {})