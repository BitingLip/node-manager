"""
Conditioning Instructor for SDXL Workers System
==============================================

Coordinates conditioning tasks management and optimization.
Controls conditioning managers through the conditioning interface.
"""

import logging
from typing import Dict, Any, Optional
from .instructor_device import BaseInstructor


class ConditioningInstructor(BaseInstructor):
    """
    Conditioning tasks management coordinator.
    
    This instructor manages conditioning operations including prompt processing,
    ControlNet, and image-to-image conditioning by coordinating with conditioning managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        super().__init__(config)
        self.conditioning_interface = None
        
    async def initialize(self) -> bool:
        """Initialize conditioning instructor and interface."""
        try:
            self.logger.info("Initializing conditioning instructor...")
            
            # Import conditioning interface (lazy loading)
            from ..conditioning.interface_conditioning import ConditioningInterface
            
            # Create conditioning interface
            self.conditioning_interface = ConditioningInterface(self.config)
            
            # Initialize interface
            if await self.conditioning_interface.initialize():
                self.initialized = True
                self.logger.info("Conditioning instructor initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize conditioning interface")
                return False
                
        except Exception as e:
            self.logger.error(f"Conditioning instructor initialization failed: {e}")
            return False
    
    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Handle conditioning-related requests."""
        if not self.initialized:
            return {"success": False, "error": "Conditioning instructor not initialized"}
        
        try:
            request_type = request.get("type", "")
            request_id = request.get("request_id", "")
            
            self.logger.info(f"Handling conditioning request: {request_type}")
            
            # Route to conditioning interface
            if request_type == "conditioning.process_prompt":
                return await self.conditioning_interface.process_prompt(request)
            elif request_type == "conditioning.process_controlnet":
                return await self.conditioning_interface.process_controlnet(request)
            elif request_type == "conditioning.process_img2img":
                return await self.conditioning_interface.process_img2img(request)
            elif request_type == "conditioning.get_conditioning_info":
                return await self.conditioning_interface.get_conditioning_info(request)
            else:
                return {
                    "success": False,
                    "error": f"Unknown conditioning request type: {request_type}",
                    "request_id": request_id
                }
                
        except Exception as e:
            self.logger.error(f"Conditioning request handling failed: {e}")
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get conditioning instructor status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            # Get status from conditioning interface
            if self.conditioning_interface:
                interface_status = await self.conditioning_interface.get_status()
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
        """Clean up conditioning instructor resources."""
        try:
            self.logger.info("Cleaning up conditioning instructor...")
            
            if self.conditioning_interface:
                await self.conditioning_interface.cleanup()
                self.conditioning_interface = None
            
            self.initialized = False
            self.logger.info("Conditioning instructor cleanup complete")
            
        except Exception as e:
            self.logger.error(f"Conditioning instructor cleanup error: {e}")