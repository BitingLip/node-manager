"""
Inference Instructor for SDXL Workers System
===========================================

Coordinates inference management and optimization for different model types and devices.
Controls inference managers through the inference interface.
"""

import logging
from typing import Dict, Any, Optional, TYPE_CHECKING
from .instructor_device import BaseInstructor

if TYPE_CHECKING:
    from ..inference.interface_inference import InferenceInterface


class InferenceInstructor(BaseInstructor):
    """
    Inference management and optimization coordinator.
    
    This instructor manages inference operations including SDXL, ControlNet, and LoRA
    inference by coordinating with inference managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        super().__init__(config)
        self.inference_interface: Optional["InferenceInterface"] = None
        
    async def initialize(self) -> bool:
        """Initialize inference instructor and interface."""
        try:
            self.logger.info("Initializing inference instructor...")
            
            # Import inference interface (lazy loading)
            from ..inference.interface_inference import InferenceInterface
            
            # Create inference interface
            self.inference_interface = InferenceInterface(self.config)
            
            # Initialize interface
            if await self.inference_interface.initialize():
                self.initialized = True
                self.logger.info("Inference instructor initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize inference interface")
                self.inference_interface = None
                return False
                
        except Exception as e:
            self.logger.error(f"Inference instructor initialization failed: {e}")
            self.inference_interface = None
            return False
    
    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Handle inference-related requests."""
        if not self.initialized:
            return {"success": False, "error": "Inference instructor not initialized"}
        
        if self.inference_interface is None:
            return {"success": False, "error": "Inference interface not available"}
        
        try:
            request_type = request.get("type", "")
            request_id = request.get("request_id", "")
            
            self.logger.info(f"Handling inference request: {request_type}")
            
            # Route to inference interface
            if request_type == "inference.text2img":
                return await self.inference_interface.text2img(request)
            elif request_type == "inference.img2img":
                return await self.inference_interface.img2img(request)
            elif request_type == "inference.inpainting":
                return await self.inference_interface.inpainting(request)
            elif request_type == "inference.controlnet":
                return await self.inference_interface.controlnet(request)
            elif request_type == "inference.lora":
                return await self.inference_interface.lora(request)
            elif request_type == "inference.batch_process":
                return await self.inference_interface.batch_process(request)
            elif request_type == "inference.get_pipeline_info":
                return await self.inference_interface.get_pipeline_info(request)
            elif request_type == "inference.get_capabilities":
                return await self.inference_interface.get_capabilities(request)
            elif request_type == "inference.get_supported_types":
                return await self.inference_interface.get_supported_types(request)
            elif request_type == "inference.validate_request":
                return await self.inference_interface.validate_request(request)
            elif request_type == "inference.get_session_status":
                return await self.inference_interface.get_session_status(request)
            elif request_type == "inference.cancel_session":
                return await self.inference_interface.cancel_session(request)
            elif request_type == "inference.get_active_sessions":
                return await self.inference_interface.get_active_sessions(request)
            else:
                return {
                    "success": False,
                    "error": f"Unknown inference request type: {request_type}",
                    "request_id": request_id
                }
                
        except Exception as e:
            self.logger.error(f"Inference request handling failed: {e}")
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get inference instructor status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            # Get status from inference interface
            if self.inference_interface:
                interface_status = await self.inference_interface.get_status()
                return {
                    "status": "healthy",
                    "initialized": self.initialized,
                    "interface": interface_status
                }
            else:
                return {"status": "interface_not_available"}
                
        except Exception as e:
            return {"status": "error", "error": str(e)}
    
    async def cleanup(self) -> None:
        """Clean up inference instructor resources."""
        try:
            self.logger.info("Cleaning up inference instructor...")
            
            if self.inference_interface:
                await self.inference_interface.cleanup()
                self.inference_interface = None
            
            self.initialized = False
            self.logger.info("Inference instructor cleanup complete")
            
        except Exception as e:
            self.logger.error(f"Inference instructor cleanup error: {e}")