"""
Communication Instructor for SDXL Workers System
==============================================

Manages worker communication protocols and coordination.
Controls communication managers through the communication interface.
"""

import logging
from typing import Dict, Any
from .instructor_device import BaseInstructor


class CommunicationInstructor(BaseInstructor):
    """
    Worker communication management coordinator.
    
    This instructor manages communication protocols including messaging,
    response handling, and inter-worker coordination through communication managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        super().__init__(config)
        self.communication_interface = None
        
    async def initialize(self) -> bool:
        """Initialize communication instructor and interface."""
        try:
            self.logger.info("Initializing communication instructor...")
            
            # Import communication interface (lazy loading)
            from ..communication.interface_communication import CommunicationInterface
            
            # Create communication interface
            self.communication_interface = CommunicationInterface(self.config)
            
            # Initialize interface
            if await self.communication_interface.initialize():
                self.initialized = True
                self.logger.info("Communication instructor initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize communication interface")
                return False
                
        except Exception as e:
            self.logger.error("Communication instructor initialization failed: %s", e)
            return False
    
    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Handle communication-related requests."""
        if not self.initialized:
            return {"success": False, "error": "Communication instructor not initialized"}
        
        try:
            request_type = request.get("type", "")
            request_id = request.get("request_id", "")
            
            self.logger.info("Handling communication request: %s", request_type)
            
            # Route to communication interface
            if request_type == "communication.send_message":
                return await self.communication_interface.send_message(request)
            elif request_type == "communication.receive_message":
                return await self.communication_interface.receive_message(request)
            elif request_type == "communication.send_response":
                return await self.communication_interface.send_response(request)
            elif request_type == "communication.health_check":
                return await self.communication_interface.health_check(request)
            else:
                return {
                    "success": False,
                    "error": f"Unknown communication request type: {request_type}",
                    "request_id": request_id
                }
                
        except Exception as e:
            self.logger.error("Communication request handling failed: %s", e)
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get communication status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            # Get status from communication interface
            if self.communication_interface:
                return await self.communication_interface.get_status()
            else:
                return {"status": "interface_not_available"}
                
        except Exception as e:
            return {"status": "error", "error": str(e)}
    
    async def cleanup(self) -> None:
        """Clean up communication resources."""
        try:
            self.logger.info("Cleaning up communication instructor...")
            
            if self.communication_interface:
                await self.communication_interface.cleanup()
            
            self.initialized = False
            self.logger.info("Communication instructor cleanup complete")
            
        except Exception as e:
            self.logger.error("Communication instructor cleanup error: %s", e)