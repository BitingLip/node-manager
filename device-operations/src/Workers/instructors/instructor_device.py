"""
Device Instructor for SDXL Workers System
==========================================

Coordinates device management and optimization for DirectML, CUDA, and CPU backends.
Controls device managers through the device interface.
"""

import logging
from typing import Dict, Any
from abc import ABC, abstractmethod


class BaseInstructor(ABC):
    """Base class for all instructors."""
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(f"{__name__}.{self.__class__.__name__}")
        self.initialized = False
        
    @abstractmethod
    async def initialize(self) -> bool:
        """Initialize the instructor."""
        pass
    
    @abstractmethod
    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Handle a request."""
        pass
    
    @abstractmethod
    async def get_status(self) -> Dict[str, Any]:
        """Get instructor status."""
        pass
    
    @abstractmethod
    async def cleanup(self) -> None:
        """Clean up resources."""
        pass


class DeviceInstructor(BaseInstructor):
    """
    Device management and optimization coordinator.
    
    This instructor manages device detection, selection, and optimization
    for DirectML, CUDA, and CPU backends by coordinating with device managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        super().__init__(config)
        self.device_interface = None
        
    async def initialize(self) -> bool:
        """Initialize device instructor and interface."""
        try:
            self.logger.info("Initializing device instructor...")
            
            # Import device interface (lazy loading)
            from ..devices.interface_device import DeviceInterface
            
            # Create device interface
            self.device_interface = DeviceInterface(self.config)
            
            # Initialize interface
            if await self.device_interface.initialize():
                self.initialized = True
                self.logger.info("Device instructor initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize device interface")
                return False
                
        except Exception as e:
            self.logger.error("Device instructor initialization failed: %s", e)
            return False
    
    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Handle device-related requests."""
        if not self.initialized:
            return {"success": False, "error": "Device instructor not initialized"}
        
        try:
            request_type = request.get("type", "")
            request_id = request.get("request_id", "")
            
            self.logger.info("Handling device request: %s", request_type)
            
            # Route to device interface
            if request_type == "device.get_info":
                return await self.device_interface.get_device_info(request)
            elif request_type == "device.list_devices":
                return await self.device_interface.list_devices(request)
            elif request_type == "device.set_device":
                return await self.device_interface.set_device(request)
            elif request_type == "device.get_memory_info":
                return await self.device_interface.get_memory_info(request)
            elif request_type == "device.optimize_settings":
                return await self.device_interface.optimize_settings(request)
            else:
                return {
                    "success": False,
                    "error": f"Unknown device request type: {request_type}",
                    "request_id": request_id
                }
                
        except Exception as e:
            self.logger.error("Device request handling failed: %s", e)
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get device instructor status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            # Get status from device interface
            if self.device_interface:
                interface_status = await self.device_interface.get_status()
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
        """Clean up device instructor resources."""
        try:
            self.logger.info("Cleaning up device instructor...")
            
            if self.device_interface:
                await self.device_interface.cleanup()
                self.device_interface = None
            
            self.initialized = False
            self.logger.info("Device instructor cleanup complete")
            
        except Exception as e:
            self.logger.error("Device instructor cleanup error: %s", e)