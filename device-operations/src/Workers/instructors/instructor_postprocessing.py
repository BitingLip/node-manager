"""
Postprocessing Instructor for SDXL Workers System
================================================

Coordinates post-processing management and optimization for different model types and devices.
Controls postprocessing managers through the postprocessing interface.
"""

import logging
from typing import Dict, Any, Optional
from .instructor_device import BaseInstructor


class PostprocessingInstructor(BaseInstructor):
    """
    Post-processing management coordinator.
    
    This instructor manages post-processing operations including upscaling,
    image enhancement, and safety checking by coordinating with postprocessing managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        super().__init__(config)
        self.postprocessing_interface = None
        
    async def initialize(self) -> bool:
        """Initialize postprocessing instructor and interface."""
        try:
            self.logger.info("Initializing postprocessing instructor...")
            
            # Import postprocessing interface (lazy loading)
            from ..postprocessing.interface_postprocessing import PostprocessingInterface
            
            # Create postprocessing interface
            self.postprocessing_interface = PostprocessingInterface(self.config)
            
            # Initialize interface
            if await self.postprocessing_interface.initialize():
                self.initialized = True
                self.logger.info("Postprocessing instructor initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize postprocessing interface")
                return False
                
        except Exception as e:
            self.logger.error(f"Postprocessing instructor initialization failed: {e}")
            return False
    
    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Handle postprocessing-related requests."""
        if not self.initialized:
            return {"success": False, "error": "Postprocessing instructor not initialized"}
        
        try:
            request_type = request.get("type", "")
            request_id = request.get("request_id", "")
            
            self.logger.info(f"Handling postprocessing request: {request_type}")
            
            # Route to postprocessing interface
            if request_type == "postprocessing.upscale":
                return await self.postprocessing_interface.upscale_image(request)
            elif request_type == "postprocessing.enhance":
                return await self.postprocessing_interface.enhance_image(request)
            elif request_type == "postprocessing.safety_check":
                return await self.postprocessing_interface.check_safety(request)
            elif request_type == "postprocessing.pipeline":
                return await self.postprocessing_interface.process_pipeline(request)
            elif request_type == "postprocessing.get_processing_info":
                return await self.postprocessing_interface.get_postprocessing_info(request)
            else:
                return {
                    "success": False,
                    "error": f"Unknown postprocessing request type: {request_type}",
                    "request_id": request_id
                }
                
        except Exception as e:
            self.logger.error(f"Postprocessing request handling failed: {e}")
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get postprocessing instructor status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            # Get status from postprocessing interface
            if self.postprocessing_interface:
                interface_status = await self.postprocessing_interface.get_status()
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
        """Clean up postprocessing instructor resources."""
        try:
            self.logger.info("Cleaning up postprocessing instructor...")
            
            if self.postprocessing_interface:
                await self.postprocessing_interface.cleanup()
                self.postprocessing_interface = None
            
            self.initialized = False
            self.logger.info("Postprocessing instructor cleanup complete")
            
        except Exception as e:
            self.logger.error(f"Postprocessing instructor cleanup error: {e}")