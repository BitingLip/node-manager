"""
Inference Interface for SDXL Workers System
==========================================

Unified interface for inference operations.
"""

import logging
from typing import Dict, Any, Optional, TYPE_CHECKING

if TYPE_CHECKING:
    from .managers.manager_batch import BatchManager
    from .managers.manager_pipeline_simple import PipelineManager
    from .managers.manager_memory import MemoryManager
    from .workers.worker_sdxl import SDXLWorker
    from .workers.worker_controlnet import ControlNetWorker
    from .workers.worker_lora import LoRAWorker


class InferenceInterface:
    """
    Unified interface for inference operations.
    
    This interface provides a consistent API for inference operations
    and delegates to appropriate managers and workers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(f"{__name__}.{self.__class__.__name__}")
        
        # Manager and worker instances with proper typing
        self.batch_manager: Optional['BatchManager'] = None
        self.pipeline_manager: Optional['PipelineManager'] = None
        self.memory_manager: Optional['MemoryManager'] = None
        self.sdxl_worker: Optional['SDXLWorker'] = None
        self.controlnet_worker: Optional['ControlNetWorker'] = None
        self.lora_worker: Optional['LoRAWorker'] = None
        
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize inference interface and components."""
        try:
            self.logger.info("Initializing inference interface...")
            
            # Import components (lazy loading)
            from .managers.manager_batch import BatchManager
            from .managers.manager_pipeline_simple import PipelineManager
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
        if not self.initialized or not self.sdxl_worker:
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
        if not self.initialized or not self.sdxl_worker:
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
        if not self.initialized or not self.sdxl_worker:
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
        if not self.initialized or not self.controlnet_worker:
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
        if not self.initialized or not self.lora_worker:
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
        if not self.initialized or not self.batch_manager:
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
        if not self.initialized or not self.pipeline_manager:
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

    async def get_capabilities(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get inference capabilities for system or specific device."""
        if not self.initialized or not self.pipeline_manager:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            device_id = request.get("data", {}).get("device_id")
            
            # Get device capabilities from device manager
            from ..device.interface_device import DeviceInterface
            device_interface = DeviceInterface(self.config)
            await device_interface.initialize()
            
            device_capabilities = await device_interface.get_device_capabilities({"device_id": device_id})
            
            # Get pipeline capabilities
            pipeline_info = await self.pipeline_manager.get_pipeline_info()
            
            capabilities = {
                "supported_inference_types": [
                    "text2img", "img2img", "inpainting", "controlnet", "lora"
                ],
                "supported_precisions": ["fp32", "fp16", "int8"],
                "max_batch_size": pipeline_info.get("max_batch_size", 8),
                "max_concurrent_inferences": pipeline_info.get("max_concurrent", 3),
                "max_resolution": {
                    "width": pipeline_info.get("max_width", 2048),
                    "height": pipeline_info.get("max_height", 2048)
                },
                "supported_models": pipeline_info.get("supported_models", [
                    "stable-diffusion-xl", "stable-diffusion-v1-5", "flux"
                ]),
                "device_info": device_capabilities.get("data", {}) if device_capabilities.get("success") else None
            }
            
            return {
                "success": True,
                "data": capabilities,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }

    async def get_supported_types(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get supported inference types for system or specific device."""
        if not self.initialized:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            device_id = request.get("data", {}).get("device_id")
            
            # Base supported types
            supported_types = [
                "text2img", "img2img", "inpainting", "controlnet", "lora"
            ]
            
            if device_id:
                # Get device-specific capabilities
                from ..device.interface_device import DeviceInterface
                device_interface = DeviceInterface(self.config)
                await device_interface.initialize()
                
                device_status = await device_interface.get_device_status({"device_id": device_id})
                if device_status.get("success"):
                    device_data = device_status.get("data", {})
                    # Filter based on device capabilities
                    if device_data.get("memory_available", 0) < 4000000000:  # Less than 4GB
                        supported_types = ["text2img", "img2img"]  # Basic inference only
            
            return {
                "success": True,
                "data": {
                    "supported_types": supported_types,
                    "device_id": device_id
                },
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }

    async def validate_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Validate inference request parameters."""
        if not self.initialized or not self.pipeline_manager:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            validation_data = request.get("data", {})
            inference_type = validation_data.get("inference_type", "")
            model_id = validation_data.get("model_id", "")
            parameters = validation_data.get("parameters", {})
            
            validation_result = {
                "valid": True,
                "errors": [],
                "warnings": []
            }
            
            # Validate inference type
            supported_types = ["text2img", "img2img", "inpainting", "controlnet", "lora"]
            if inference_type not in supported_types:
                validation_result["valid"] = False
                validation_result["errors"].append(f"Unsupported inference type: {inference_type}")
            
            # Validate required parameters
            if inference_type in ["text2img", "img2img", "inpainting"]:
                if not parameters.get("prompt"):
                    validation_result["valid"] = False
                    validation_result["errors"].append("Prompt is required for this inference type")
                
                # Validate resolution
                width = parameters.get("width", 512)
                height = parameters.get("height", 512)
                if width < 64 or width > 2048 or height < 64 or height > 2048:
                    validation_result["valid"] = False
                    validation_result["errors"].append("Resolution must be between 64x64 and 2048x2048")
                
                # Validate steps
                steps = parameters.get("num_inference_steps", 20)
                if steps < 1 or steps > 100:
                    validation_result["warnings"].append("Recommended inference steps: 10-50")
            
            # Validate model availability
            pipeline_info = await self.pipeline_manager.get_pipeline_info()
            supported_models = pipeline_info.get("supported_models", [])
            if model_id and model_id not in supported_models:
                validation_result["warnings"].append(f"Model {model_id} may not be available")
            
            return {
                "success": True,
                "data": validation_result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }

    async def get_session_status(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get status of a specific inference session."""
        if not self.initialized or not self.pipeline_manager:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            session_id = request.get("data", {}).get("session_id", "")
            
            if not session_id:
                return {
                    "success": False,
                    "error": "Session ID is required",
                    "request_id": request.get("request_id", "")
                }
            
            # Get session status from pipeline manager
            session_status = await self.pipeline_manager.get_session_status(session_id)
            
            return {
                "success": True,
                "data": session_status,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }

    async def cancel_session(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Cancel an active inference session."""
        if not self.initialized or not self.pipeline_manager:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            session_id = request.get("data", {}).get("session_id", "")
            reason = request.get("data", {}).get("reason", "user_requested")
            
            if not session_id:
                return {
                    "success": False,
                    "error": "Session ID is required",
                    "request_id": request.get("request_id", "")
                }
            
            # Cancel session through pipeline manager
            cancel_result = await self.pipeline_manager.cancel_session(session_id, reason)
            
            return {
                "success": True,
                "data": cancel_result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }

    async def get_active_sessions(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get list of active inference sessions."""
        if not self.initialized or not self.pipeline_manager:
            return {"success": False, "error": "Inference interface not initialized"}
        
        try:
            # Get active sessions from pipeline manager
            sessions = await self.pipeline_manager.get_active_sessions()
            
            return {
                "success": True,
                "data": {
                    "sessions": sessions,
                    "count": len(sessions)
                },
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