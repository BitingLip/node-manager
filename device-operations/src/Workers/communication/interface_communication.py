"""
Communication Interface for SDXL Workers System
==============================================

Unified interface for communication management and protocol implementation.
Migrated from core/communication.py.
"""

import logging
from typing import Dict, Any, Optional


class CommunicationInterface:
    """
    Unified interface for communication management and protocol implementation.
    
    This interface provides a consistent API for communication operations
    and delegates to appropriate managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(f"{__name__}.{self.__class__.__name__}")
        self.communication_manager = None
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize communication interface and manager."""
        try:
            self.logger.info("Initializing communication interface...")
            
            # Import communication manager (lazy loading)
            from .managers.manager_communication import CommunicationManager
            
            # Create communication manager
            self.communication_manager = CommunicationManager(self.config)
            
            # Initialize manager
            if await self.communication_manager.initialize():
                self.initialized = True
                self.logger.info("Communication interface initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize communication manager")
                return False
                
        except Exception as e:
            self.logger.error(f"Communication interface initialization failed: {e}")
            return False
    
    async def send_message(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Send a message via the communication channel."""
        if not self.initialized:
            return {"success": False, "error": "Communication interface not initialized"}
        
        try:
            message_data = request.get("data", {})
            success = await self.communication_manager.send_message(message_data)
            return {
                "success": success,
                "data": {"sent": success},
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def receive_message(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Receive a message from the communication channel."""
        if not self.initialized:
            return {"success": False, "error": "Communication interface not initialized"}
        
        try:
            message = await self.communication_manager.receive_message()
            return {
                "success": True,
                "data": {"message": message},
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def send_response(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Send a standardized response."""
        if not self.initialized:
            return {"success": False, "error": "Communication interface not initialized"}
        
        try:
            response_data = request.get("data", {})
            success = response_data.get("success", True)
            data = response_data.get("data")
            error = response_data.get("error")
            request_id = response_data.get("request_id")
            
            result = await self.communication_manager.send_response(success, data, error, request_id)
            return {
                "success": result,
                "data": {"response_sent": result},
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def health_check(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Send health status."""
        if not self.initialized:
            return {"success": False, "error": "Communication interface not initialized"}
        
        try:
            status = request.get("data", {}).get("status", "healthy")
            success = await self.communication_manager.send_health_status(status)
            return {
                "success": success,
                "data": {"health_sent": success, "status": status},
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get communication interface status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            if self.communication_manager:
                manager_status = await self.communication_manager.get_status()
                return {
                    "status": "healthy",
                    "initialized": self.initialized,
                    "manager": manager_status
                }
            else:
                return {"status": "manager_not_available"}
                
        except Exception as e:
            return {"status": "error", "error": str(e)}
    
    async def cleanup(self) -> None:
        """Clean up communication interface resources."""
        try:
            self.logger.info("Cleaning up communication interface...")
            
            if self.communication_manager:
                await self.communication_manager.cleanup()
                self.communication_manager = None
            
            self.initialized = False
            self.logger.info("Communication interface cleanup complete")
            
        except Exception as e:
            self.logger.error(f"Communication interface cleanup error: {e}")


# Factory function for creating communication interface
def create_communication_interface(config: Optional[Dict[str, Any]] = None) -> CommunicationInterface:
    """
    Factory function to create a communication interface instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        CommunicationInterface instance
    """
    return CommunicationInterface(config or {})