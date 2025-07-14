"""
Device Instructor for SDXL Workers System
==========================================

Coordinates device management and optimization for DirectML, CUDA, and CPU backends.
Controls device managers through the device interface.
"""

import logging
from typing import Dict, Any, TYPE_CHECKING, Optional

if TYPE_CHECKING:
    from ..device.interface_device import DeviceInterface
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
        self.device_interface: Optional['DeviceInterface'] = None
        self.device_interface: Optional['DeviceInterface'] = None
        
    async def initialize(self) -> bool:
        """Initialize device instructor and interface."""
        try:
            self.logger.info("Initializing device instructor...")
            
            # Import device interface (lazy loading)
            from ..device.interface_device import DeviceInterface
            
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
        """
        Enhanced request handling with structured error management and new device actions
        Based on Phase 4 requirement: Enhanced error propagation and missing action implementation
        """
        if not self.initialized or not self.device_interface:
            return {"success": False, "error": "Device instructor not initialized"}
        
        try:
            action = request.get("action", "")
            request_id = request.get("request_id", "")
            
            self.logger.info("Handling device action: %s", action)
            
            # Validate action
            if not action:
                return {
                    "success": False,
                    "error": "Action is required",
                    "error_code": "INVALID_ACTION",
                    "request_id": request_id
                }
            
            # Route to appropriate handler
            if action == "list_devices":
                return await self.device_interface.list_devices(request)
            elif action == "get_device":
                return await self.device_interface.get_device_info(request)
            elif action == "set_device":
                return await self.device_interface.set_device(request)
            elif action == "get_memory_info":
                return await self.device_interface.get_memory_info(request)
            elif action == "optimize_device":
                return await self.device_interface.optimize_settings(request)
            
            # NEW Phase 4 Week 1 Actions: Device capabilities discovery and status monitoring
            elif action == "get_capabilities":
                device_id = request.get("data", {}).get("device_id")
                if not device_id:
                    return {
                        "success": False,
                        "error": "Device ID required for capabilities",
                        "error_code": "INVALID_DEVICE_ID",
                        "request_id": request_id
                    }
                return await self.device_interface.get_device_capabilities(request)
                
            elif action == "get_device_status":
                device_id = request.get("data", {}).get("device_id")
                if not device_id:
                    return {
                        "success": False,
                        "error": "Device ID required for status",
                        "error_code": "INVALID_DEVICE_ID", 
                        "request_id": request_id
                    }
                return await self.device_interface.get_device_status(request)
            
            # Legacy request type support (for backward compatibility)
            elif action == "device.get_info":
                return await self.device_interface.get_device_info(request)
            elif action == "device.list_devices":
                return await self.device_interface.list_devices(request)
            elif action == "device.set_device":
                return await self.device_interface.set_device(request)
            elif action == "device.get_memory_info":
                return await self.device_interface.get_memory_info(request)
            elif action == "device.optimize_settings":
                return await self.device_interface.optimize_settings(request)
            else:
                return {
                    "success": False,
                    "error": f"Unknown device action: {action}",
                    "error_code": "UNKNOWN_ACTION",
                    "request_id": request_id
                }
                
        except Exception as e:
            self.logger.error("Device request handling failed: %s", e)
            return {
                "success": False,
                "error": f"Unexpected device operation error: {str(e)}",
                "error_code": "UNKNOWN_ERROR",
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