"""
Scheduler Interface for SDXL Workers System
==========================================

Migrated from schedulers/base_scheduler.py
Unified interface for scheduler management and optimization.
"""

import logging
import torch
from typing import Dict, Any, Optional
from abc import ABC, abstractmethod


class SchedulerInterface:
    """
    Unified interface for scheduler management and optimization.
    
    This interface provides a consistent API for scheduler operations
    and delegates to appropriate managers and workers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(f"{__name__}.{self.__class__.__name__}")
        
        # Manager and worker instances
        self.factory_manager = None
        self.scheduler_manager = None
        self.ddim_worker = None
        self.dpm_plus_plus_worker = None
        self.euler_worker = None
        
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize scheduler interface and components."""
        try:
            self.logger.info("Initializing scheduler interface...")
            
            # Import components (lazy loading)
            from .managers.manager_factory import FactoryManager
            from .managers.manager_scheduler import SchedulerManager
            from .workers.worker_ddim import DDIMWorker
            from .workers.worker_dpm_plus_plus import DPMPlusPlusWorker
            from .workers.worker_euler import EulerWorker
            
            # Create components
            self.factory_manager = FactoryManager(self.config)
            self.scheduler_manager = SchedulerManager(self.config)
            self.ddim_worker = DDIMWorker(self.config)
            self.dpm_plus_plus_worker = DPMPlusPlusWorker(self.config)
            self.euler_worker = EulerWorker(self.config)
            
            # Initialize components
            components = [
                self.factory_manager,
                self.scheduler_manager,
                self.ddim_worker,
                self.dpm_plus_plus_worker,
                self.euler_worker
            ]
            
            for component in components:
                if not await component.initialize():
                    self.logger.error("Failed to initialize %s", component.__class__.__name__)
                    return False
                    
            self.initialized = True
            self.logger.info("Scheduler interface initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error("Scheduler interface initialization failed: %s", e)
            return False
    
    async def create_scheduler(self, scheduler_type: str, config: Dict[str, Any]) -> Dict[str, Any]:
        """Create a scheduler instance."""
        if not self.initialized:
            return {"success": False, "error": "Scheduler interface not initialized"}
        
        try:
            result = await self.factory_manager.create_scheduler(scheduler_type, config)
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def configure_scheduler(self, scheduler_id: str, config: Dict[str, Any]) -> Dict[str, Any]:
        """Configure a scheduler instance."""
        if not self.initialized:
            return {"success": False, "error": "Scheduler interface not initialized"}
        
        try:
            result = await self.scheduler_manager.configure_scheduler(scheduler_id, config)
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def process_ddim(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process DDIM scheduler request."""
        if not self.initialized:
            return {"success": False, "error": "Scheduler interface not initialized"}
        
        try:
            result = await self.ddim_worker.process_scheduling(request)
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def process_dpm_plus_plus(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process DPM++ scheduler request."""
        if not self.initialized:
            return {"success": False, "error": "Scheduler interface not initialized"}
        
        try:
            result = await self.dpm_plus_plus_worker.process_scheduling(request)
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def process_euler(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process Euler scheduler request."""
        if not self.initialized:
            return {"success": False, "error": "Scheduler interface not initialized"}
        
        try:
            result = await self.euler_worker.process_scheduling(request)
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def get_available_schedulers(self) -> Dict[str, Any]:
        """Get list of available schedulers."""
        if not self.initialized:
            return {"success": False, "error": "Scheduler interface not initialized"}
        
        try:
            result = await self.factory_manager.get_available_schedulers()
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def get_scheduler_info(self, scheduler_id: str) -> Dict[str, Any]:
        """Get information about a specific scheduler."""
        if not self.initialized:
            return {"success": False, "error": "Scheduler interface not initialized"}
        
        try:
            result = await self.scheduler_manager.get_scheduler_info(scheduler_id)
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get scheduler interface status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            status = {
                "status": "healthy",
                "initialized": self.initialized,
                "components": {}
            }
            
            # Collect status from all components
            components = [
                ("factory_manager", self.factory_manager),
                ("scheduler_manager", self.scheduler_manager),
                ("ddim_worker", self.ddim_worker),
                ("dpm_plus_plus_worker", self.dpm_plus_plus_worker),
                ("euler_worker", self.euler_worker)
            ]
            
            for name, component in components:
                if component:
                    try:
                        status["components"][name] = await component.get_status()
                    except Exception as e:
                        status["components"][name] = {"error": str(e)}
                        
            return status
            
        except Exception as e:
            return {"status": "error", "error": str(e)}
    
    async def cleanup(self) -> None:
        """Clean up scheduler interface resources."""
        try:
            self.logger.info("Cleaning up scheduler interface...")
            
            # Cleanup components
            components = [
                self.euler_worker,
                self.dpm_plus_plus_worker,
                self.ddim_worker,
                self.scheduler_manager,
                self.factory_manager
            ]
            
            for component in components:
                if component:
                    try:
                        await component.cleanup()
                    except Exception as e:
                        self.logger.warning("Error during component cleanup: %s", e)
            
            self.initialized = False
            self.logger.info("Scheduler interface cleanup complete")
            
        except Exception as e:
            self.logger.error("Scheduler interface cleanup error: %s", e)


class BaseScheduler(ABC):
    """
    Abstract base class for all SDXL schedulers.
    
    Migrated from base_scheduler.py to maintain compatibility.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.num_inference_steps: Optional[int] = None
        self.timesteps: Optional[torch.Tensor] = None
        self.initialized = False
        
    @abstractmethod
    async def initialize(self) -> bool:
        """Initialize the scheduler."""
        return NotImplemented
    
    @abstractmethod
    async def configure(self, **kwargs) -> Dict[str, Any]:
        """Configure scheduler parameters."""
        return NotImplemented
    
    @abstractmethod
    async def get_scheduler_config(self) -> Dict[str, Any]:
        """Get scheduler configuration."""
        return NotImplemented
    
    async def set_timesteps(self, num_inference_steps: int) -> bool:
        """Set the timesteps for the scheduler."""
        try:
            self.num_inference_steps = num_inference_steps
            self.logger.debug("Set %d timesteps for %s", num_inference_steps, self.__class__.__name__)
            return True
        except Exception as e:
            self.logger.error("Failed to set timesteps: %s", e)
            return False
    
    async def step(self, model_output: torch.Tensor, timestep: int, sample: torch.Tensor) -> torch.Tensor:
        """Perform a scheduler step."""
        # Placeholder implementation
        return sample
    
    async def get_status(self) -> Dict[str, Any]:
        """Get scheduler status."""
        return {
            "initialized": self.initialized,
            "num_inference_steps": self.num_inference_steps,
            "has_timesteps": self.timesteps is not None
        }
    
    async def cleanup(self) -> None:
        """Clean up scheduler resources."""
        try:
            self.logger.info("Cleaning up %s...", self.__class__.__name__)
            self.timesteps = None
            self.num_inference_steps = None
            self.initialized = False
            self.logger.info("%s cleanup complete", self.__class__.__name__)
        except Exception as e:
            self.logger.error("%s cleanup error: %s", self.__class__.__name__, e)


# Factory function for creating scheduler interface
def create_scheduler_interface(config: Optional[Dict[str, Any]] = None) -> SchedulerInterface:
    """
    Factory function to create a scheduler interface instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        SchedulerInterface instance
    """
    return SchedulerInterface(config or {})