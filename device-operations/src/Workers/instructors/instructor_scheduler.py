"""
Scheduler Instructor for SDXL Workers System
===========================================

Coordinates scheduler management and optimization for different model types and devices.
Controls scheduler managers through the scheduler interface.
"""

import logging
from typing import Dict, Any, Optional
from .instructor_device import BaseInstructor


class SchedulerInstructor(BaseInstructor):
    """
    Scheduler management and optimization coordinator.
    
    This instructor manages scheduler operations including DDIM, DPM++, and Euler
    schedulers by coordinating with scheduler managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        super().__init__(config)
        self.scheduler_interface = None
        
    async def initialize(self) -> bool:
        """Initialize scheduler instructor and interface."""
        try:
            self.logger.info("Initializing scheduler instructor...")
            
            # Import scheduler interface (lazy loading)
            from ..schedulers.interface_scheduler import SchedulerInterface
            
            # Create scheduler interface
            self.scheduler_interface = SchedulerInterface(self.config)
            
            # Initialize interface
            if await self.scheduler_interface.initialize():
                self.initialized = True
                self.logger.info("Scheduler instructor initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize scheduler interface")
                return False
                
        except Exception as e:
            self.logger.error(f"Scheduler instructor initialization failed: {e}")
            return False
    
    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Handle scheduler-related requests."""
        if not self.initialized:
            return {"success": False, "error": "Scheduler instructor not initialized"}
        
        try:
            request_type = request.get("type", "")
            request_id = request.get("request_id", "")
            
            self.logger.info(f"Handling scheduler request: {request_type}")
            
            # Route to scheduler interface
            if request_type == "scheduler.create_scheduler":
                return await self.scheduler_interface.create_scheduler(request)
            elif request_type == "scheduler.get_scheduler_info":
                return await self.scheduler_interface.get_scheduler_info(request)
            elif request_type == "scheduler.list_schedulers":
                return await self.scheduler_interface.list_schedulers(request)
            elif request_type == "scheduler.configure_scheduler":
                return await self.scheduler_interface.configure_scheduler(request)
            elif request_type == "scheduler.ddim":
                return await self.scheduler_interface.ddim_scheduler(request)
            elif request_type == "scheduler.dpm_plus_plus":
                return await self.scheduler_interface.dpm_plus_plus_scheduler(request)
            elif request_type == "scheduler.euler":
                return await self.scheduler_interface.euler_scheduler(request)
            else:
                return {
                    "success": False,
                    "error": f"Unknown scheduler request type: {request_type}",
                    "request_id": request_id
                }
                
        except Exception as e:
            self.logger.error(f"Scheduler request handling failed: {e}")
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get scheduler instructor status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            # Get status from scheduler interface
            if self.scheduler_interface:
                interface_status = await self.scheduler_interface.get_status()
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
        """Clean up scheduler instructor resources."""
        try:
            self.logger.info("Cleaning up scheduler instructor...")
            
            if self.scheduler_interface:
                await self.scheduler_interface.cleanup()
                self.scheduler_interface = None
            
            self.initialized = False
            self.logger.info("Scheduler instructor cleanup complete")
            
        except Exception as e:
            self.logger.error(f"Scheduler instructor cleanup error: {e}")