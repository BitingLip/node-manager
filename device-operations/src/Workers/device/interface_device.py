"""
Device Interface for SDXL Workers System
=======================================

Unified interface for device management and optimization.
Migrated from core/device_manager.py.
"""

import logging
from typing import Dict, Any, Optional, TYPE_CHECKING

if TYPE_CHECKING:
    from .managers.manager_device import DeviceManager


class DeviceInterface:
    """
    Unified interface for device management and optimization.
    
    This interface provides a consistent API for device operations
    and delegates to appropriate managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(f"{__name__}.{self.__class__.__name__}")
        self.device_manager: Optional['DeviceManager'] = None
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize device interface and manager."""
        try:
            self.logger.info("Initializing device interface...")
            
            # Import device manager (lazy loading)
            from .managers.manager_device import DeviceManager
            
            # Create device manager
            self.device_manager = DeviceManager(self.config)
            
            # Initialize manager
            if await self.device_manager.initialize():
                self.initialized = True
                self.logger.info("Device interface initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize device manager")
                return False
                
        except Exception as e:
            self.logger.error("Device interface initialization failed: %s", e)
            return False
    
    async def get_device_info(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get current device information."""
        if not self.initialized:
            return {"success": False, "error": "Device interface not initialized"}
        
        try:
            device_info = await self.device_manager.get_device_info()
            return {
                "success": True,
                "data": device_info,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def list_devices(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """List all available devices."""
        if not self.initialized:
            return {"success": False, "error": "Device interface not initialized"}
        
        try:
            devices = await self.device_manager.list_devices()
            return {
                "success": True,
                "data": {"devices": devices},
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def set_device(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Set the current device."""
        if not self.initialized:
            return {"success": False, "error": "Device interface not initialized"}
        
        try:
            device_id = request.get("data", {}).get("device_id")
            if not device_id:
                return {
                    "success": False,
                    "error": "device_id is required",
                    "request_id": request.get("request_id", "")
                }
            
            success = await self.device_manager.set_device(device_id)
            return {
                "success": success,
                "data": {"device_id": device_id, "set": success},
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_memory_info(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get memory information for the current device."""
        if not self.initialized:
            return {"success": False, "error": "Device interface not initialized"}
        
        try:
            memory_info = await self.device_manager.get_memory_info()
            return {
                "success": True,
                "data": memory_info,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def optimize_settings(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get optimized memory settings for the current device."""
        if not self.initialized:
            return {"success": False, "error": "Device interface not initialized"}
        
        try:
            settings = await self.device_manager.optimize_settings()
            return {
                "success": True,
                "data": settings,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get device interface status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            if self.device_manager:
                manager_status = await self.device_manager.get_status()
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
        """Clean up device interface resources."""
        try:
            self.logger.info("Cleaning up device interface...")
            
            if self.device_manager:
                await self.device_manager.cleanup()
                self.device_manager = None
            
            self.initialized = False
            self.logger.info("Device interface cleanup complete")
            
        except Exception as e:
            self.logger.error("Device interface cleanup error: %s", e)

    async def get_device_capabilities(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get device capabilities through manager layer"""
        if not self.initialized:
            return {"success": False, "error": "Device interface not initialized"}
        
        try:
            device_id = request.get("data", {}).get("device_id")
            if not device_id:
                return {
                    "success": False,
                    "error": "device_id is required",
                    "request_id": request.get("request_id", "")
                }
            
            capabilities_response = await self.device_manager.get_device_capabilities(device_id)
            
            # Handle standardized response format
            if capabilities_response.get("success"):
                return {
                    "success": True,
                    "data": capabilities_response.get("data"),
                    "request_id": request.get("request_id", "")
                }
            else:
                return {
                    "success": False,
                    "error": capabilities_response.get("error_message", "Unknown capabilities error"),
                    "error_code": capabilities_response.get("error_code"),
                    "request_id": request.get("request_id", "")
                }
                
        except Exception as e:
            return {
                "success": False,
                "error": f"Device capabilities interface error: {str(e)}",
                "request_id": request.get("request_id", "")
            }

    async def get_device_status(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get device status through manager layer"""
        if not self.initialized:
            return {"success": False, "error": "Device interface not initialized"}
        
        try:
            device_id = request.get("data", {}).get("device_id")
            if not device_id:
                return {
                    "success": False,
                    "error": "device_id is required",
                    "request_id": request.get("request_id", "")
                }
            
            status_response = await self.device_manager.get_device_status(device_id)
            
            # Handle standardized response format
            if status_response.get("success"):
                return {
                    "success": True,
                    "data": status_response.get("data"),
                    "request_id": request.get("request_id", "")
                }
            else:
                return {
                    "success": False,
                    "error": status_response.get("error_message", "Unknown status error"),
                    "error_code": status_response.get("error_code"),
                    "request_id": request.get("request_id", "")
                }
                
        except Exception as e:
            return {
                "success": False,
                "error": f"Device status interface error: {str(e)}",
                "request_id": request.get("request_id", "")
            }


# Factory function for creating device interface
def create_device_interface(config: Optional[Dict[str, Any]] = None) -> DeviceInterface:
    """
    Factory function to create a device interface instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        DeviceInterface instance
    """
    return DeviceInterface(config or {})