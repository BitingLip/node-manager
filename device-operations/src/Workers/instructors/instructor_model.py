"""
Model Instructor for SDXL Workers System
========================================

Coordinates model management and optimization across different model types and devices.
Controls model managers through the model interface.
"""

import logging
from typing import Dict, Any, Optional
from .instructor_device import BaseInstructor


class ModelInstructor(BaseInstructor):
    """
    Model management and optimization coordinator.
    
    This instructor manages model loading, unloading, and optimization
    across different model types and devices by coordinating with model managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        super().__init__(config)
        self.model_interface = None
        
    async def initialize(self) -> bool:
        """Initialize model instructor and interface."""
        try:
            self.logger.info("Initializing model instructor...")
            
            # Import model interface (lazy loading)
            from ..models.interface_model import ModelInterface
            
            # Create model interface
            self.model_interface = ModelInterface(self.config)
            
            # Initialize interface
            if await self.model_interface.initialize():
                self.initialized = True
                self.logger.info("Model instructor initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize model interface")
                return False
                
        except Exception as e:
            self.logger.error(f"Model instructor initialization failed: {e}")
            return False
    
    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Handle model-related requests."""
        if not self.initialized:
            return {"success": False, "error": "Model instructor not initialized"}
        
        try:
            request_type = request.get("type", "")
            request_id = request.get("request_id", "")
            
            self.logger.info(f"Handling model request: {request_type}")
            
            # Route to model interface
            if request_type == "model.load_model":
                return await self.model_interface.load_model(request)
            elif request_type == "model.unload_model":
                return await self.model_interface.unload_model(request)
            elif request_type == "model.get_model_info":
                return await self.model_interface.get_model_info(request)
            elif request_type == "model.optimize_memory":
                return await self.model_interface.optimize_memory(request)
            elif request_type == "model.load_vae":
                return await self.model_interface.load_vae(request)
            elif request_type == "model.load_lora":
                return await self.model_interface.load_lora(request)
            elif request_type == "model.load_encoder":
                return await self.model_interface.load_encoder(request)
            elif request_type == "model.load_unet":
                return await self.model_interface.load_unet(request)
            else:
                return {
                    "success": False,
                    "error": f"Unknown model request type: {request_type}",
                    "request_id": request_id
                }
                
        except Exception as e:
            self.logger.error(f"Model request handling failed: {e}")
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get model instructor status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            # Get status from model interface
            if self.model_interface:
                interface_status = await self.model_interface.get_status()
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
        """Clean up model instructor resources."""
        try:
            self.logger.info("Cleaning up model instructor...")
            
            if self.model_interface:
                await self.model_interface.cleanup()
                self.model_interface = None
            
            self.initialized = False
            self.logger.info("Model instructor cleanup complete")
            
        except Exception as e:
            self.logger.error(f"Model instructor cleanup error: {e}")