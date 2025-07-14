"""
Main Interface for SDXL Workers System
=====================================

Unified interface for all worker types, providing a hierarchical structure
with instructors managing specialized domains.
"""

import logging
from typing import Dict, Any, Optional
from dataclasses import dataclass
from enum import Enum


class WorkerType(Enum):
    """Supported worker types."""
    DEVICE = "device"
    COMMUNICATION = "communication"
    MEMORY = "memory"
    MODEL = "model"
    CONDITIONING = "conditioning"
    INFERENCE = "inference"
    SCHEDULER = "scheduler"
    POSTPROCESSING = "postprocessing"


@dataclass
class WorkerConfig:
    """Configuration for worker initialization."""
    worker_type: WorkerType
    config_data: Dict[str, Any]
    enable_logging: bool = True
    log_level: str = "INFO"


class WorkersInterface:
    """
    Main interface for coordinating all worker instructors.
    
    This class provides a unified entry point for all worker operations,
    delegating to appropriate instructors based on the request type.
    """
    
    def __init__(self, config: Optional[Dict[str, Any]] = None):
        """
        Initialize the main interface.
        
        Args:
            config: Optional configuration dictionary
        """
        self.config = config or {}
        self.logger = logging.getLogger(__name__)
        
        # Instructor instances (will be initialized in setup)
        self.device_instructor = None
        self.communication_instructor = None
        self.memory_instructor = None
        self.model_instructor = None
        self.conditioning_instructor = None
        self.inference_instructor = None
        self.scheduler_instructor = None
        self.postprocessing_instructor = None
        
        # System state
        self.initialized = False
        self.active_workers = {}
        
    async def initialize(self) -> bool:
        """
        Initialize all instructors and the main interface.
        
        Returns:
            True if initialization successful
        """
        try:
            self.logger.info("Initializing main interface...")
            
            # Initialize instructors in dependency order
            success = await self._initialize_instructors()
            
            if success:
                self.initialized = True
                self.logger.info("Main interface initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize instructors")
                return False
                
        except Exception as e:
            self.logger.error("Main interface initialization failed: %s", e)
            return False
    
    async def _initialize_instructors(self) -> bool:
        """Initialize all instructor components."""
        try:
            # Import instructors (lazy loading to avoid circular dependencies)
            from .instructors.instructor_device import DeviceInstructor
            from .instructors.instructor_communication import CommunicationInstructor
            from .instructors.instructor_memory import MemoryInstructor
            from .instructors.instructor_model import ModelInstructor
            from .instructors.instructor_conditioning import ConditioningInstructor
            from .instructors.instructor_inference import InferenceInstructor
            from .instructors.instructor_scheduler import SchedulerInstructor
            from .instructors.instructor_postprocessing import PostprocessingInstructor
            
            # Initialize instructors
            self.device_instructor = DeviceInstructor(self.config.get("device", {}))
            self.communication_instructor = CommunicationInstructor(self.config.get("communication", {}))
            self.memory_instructor = MemoryInstructor(self.config.get("memory", {}))
            self.model_instructor = ModelInstructor(self.config.get("model", {}))
            self.conditioning_instructor = ConditioningInstructor(self.config.get("conditioning", {}))
            self.inference_instructor = InferenceInstructor(self.config.get("inference", {}))
            self.scheduler_instructor = SchedulerInstructor(self.config.get("scheduler", {}))
            self.postprocessing_instructor = PostprocessingInstructor(self.config.get("postprocessing", {}))
            
            # Initialize in dependency order
            instructors = [
                self.device_instructor,
                self.communication_instructor,
                self.memory_instructor,
                self.model_instructor,
                self.conditioning_instructor,
                self.inference_instructor,
                self.scheduler_instructor,
                self.postprocessing_instructor
            ]
            
            for instructor in instructors:
                if not await instructor.initialize():
                    self.logger.error("Failed to initialize %s", instructor.__class__.__name__)
                    return False
                    
            return True
            
        except Exception as e:
            self.logger.error("Instructor initialization failed: %s", e)
            return False
    
    async def process_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """
        Process a request by routing to appropriate instructor.
        
        Args:
            request: Request data containing type and parameters
            
        Returns:
            Response dictionary with results or error
        """
        if not self.initialized:
            return {"success": False, "error": "Interface not initialized"}
        
        try:
            request_type = request.get("type", "")
            request_id = request.get("request_id", "")
            
            self.logger.info("Processing request: %s (ID: %s)", request_type, request_id)
            
            # Route to appropriate instructor
            if request_type.startswith("device"):
                return await self.device_instructor.handle_request(request)
            elif request_type.startswith("communication"):
                return await self.communication_instructor.handle_request(request)
            elif request_type.startswith("memory"):
                return await self.memory_instructor.handle_request(request)
            elif request_type.startswith("model"):
                return await self.model_instructor.handle_request(request)
            elif request_type.startswith("conditioning"):
                return await self.conditioning_instructor.handle_request(request)
            elif request_type.startswith("inference"):
                return await self.inference_instructor.handle_request(request)
            elif request_type.startswith("scheduler"):
                return await self.scheduler_instructor.handle_request(request)
            elif request_type.startswith("postprocessing"):
                return await self.postprocessing_instructor.handle_request(request)
            else:
                return {
                    "success": False,
                    "error": f"Unknown request type: {request_type}",
                    "request_id": request_id
                }
                
        except Exception as e:
            self.logger.error("Request processing failed: %s", e)
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """
        Get overall system status.
        
        Returns:
            Status dictionary with system information
        """
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            # Collect status from all instructors
            status = {
                "status": "healthy",
                "initialized": self.initialized,
                "active_workers": len(self.active_workers),
                "instructors": {}
            }
            
            instructors = [
                ("device", self.device_instructor),
                ("communication", self.communication_instructor),
                ("memory", self.memory_instructor),
                ("model", self.model_instructor),
                ("conditioning", self.conditioning_instructor),
                ("inference", self.inference_instructor),
                ("scheduler", self.scheduler_instructor),
                ("postprocessing", self.postprocessing_instructor)
            ]
            
            for name, instructor in instructors:
                if instructor:
                    try:
                        status["instructors"][name] = await instructor.get_status()
                    except Exception as e:
                        status["instructors"][name] = {"error": str(e)}
                        
            return status
            
        except Exception as e:
            return {"status": "error", "error": str(e)}
    
    async def cleanup(self) -> None:
        """Clean up all resources."""
        try:
            self.logger.info("Cleaning up main interface...")
            
            # Cleanup instructors
            instructors = [
                self.postprocessing_instructor,
                self.scheduler_instructor,
                self.inference_instructor,
                self.conditioning_instructor,
                self.model_instructor,
                self.memory_instructor,
                self.communication_instructor,
                self.device_instructor
            ]
            
            for instructor in instructors:
                if instructor:
                    try:
                        await instructor.cleanup()
                    except Exception as e:
                        self.logger.warning("Error during instructor cleanup: %s", e)
            
            # Clear state
            self.active_workers.clear()
            self.initialized = False
            
            self.logger.info("Main interface cleanup complete")
            
        except Exception as e:
            self.logger.error("Cleanup error: %s", e)


# Factory function for creating main interface
def create_main_interface(config: Optional[Dict[str, Any]] = None) -> WorkersInterface:
    """
    Factory function to create a main interface instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        WorkersInterface instance
    """
    return WorkersInterface(config)


# Convenience functions for common operations
async def initialize_workers_system(config: Optional[Dict[str, Any]] = None) -> WorkersInterface:
    """
    Initialize the complete workers system.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        Initialized WorkersInterface instance
    """
    interface = create_main_interface(config)
    
    if await interface.initialize():
        return interface
    else:
        raise RuntimeError("Failed to initialize workers system")


async def process_worker_request(interface: WorkersInterface, request: Dict[str, Any]) -> Dict[str, Any]:
    """
    Process a worker request through the main interface.
    
    Args:
        interface: WorkersInterface instance
        request: Request data
        
    Returns:
        Response dictionary
    """
    return await interface.process_request(request)