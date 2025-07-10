"""
SDXL Workers System
===================

Hierarchical SDXL inference workers with interface/instructor/manager/worker pattern.
Provides advanced controls for model components, schedulers, hyperparameters, 
conditioning modules, precision settings, and output processing.

This package provides a comprehensive system for SDXL inference with:
- Hierarchical architecture with instructors coordinating specialized domains
- Advanced model management with caching
- LoRA and textual inversion support
- Multiple scheduler options with custom configurations
- DirectML, CUDA, and CPU backend support
- Memory optimization and device management
- JSON-based communication protocol
- Streaming responses and progress tracking
- Batch processing and multi-stage workflows
"""

# Import new hierarchical components
try:
    # Main interface for unified access
    from .interface_main import MainInterface, create_main_interface, initialize_workers_system
    
    # Instructor components
    from .instructors import (
        DeviceInstructor,
        CommunicationInstructor,
        ModelInstructor,
        ConditioningInstructor,
        InferenceInstructor,
        SchedulerInstructor,
        PostprocessingInstructor
    )
    
    # Legacy components for backward compatibility
    try:
        from .core.base_worker import BaseWorker, WorkerRequest, WorkerResponse, ProcessingError
        from .core.communication import CommunicationManager, MessageProtocol
        from .core.device_manager import get_device_manager, initialize_device_manager
        _LEGACY_COMPONENTS_AVAILABLE = True
    except ImportError:
        _LEGACY_COMPONENTS_AVAILABLE = False
        
        # Create placeholder classes for missing legacy components
        class BaseWorker:
            def __init__(self, *args, **kwargs):
                raise ImportError("Legacy components require torch, diffusers, and other dependencies")
        class WorkerRequest:
            pass
        class WorkerResponse:
            pass
        class ProcessingError(Exception):
            pass
        class CommunicationManager:
            def __init__(self, *args, **kwargs):
                raise ImportError("Legacy components require torch, diffusers, and other dependencies")
        class MessageProtocol:
            pass
        def get_device_manager():
            raise ImportError("Legacy components require torch, diffusers, and other dependencies")
        def initialize_device_manager():
            return False
    
    _NEW_ARCHITECTURE_AVAILABLE = True
    
except ImportError as e:
    # Fallback if new architecture isn't available
    _NEW_ARCHITECTURE_AVAILABLE = False
    
    # Create placeholder classes for new architecture
    class MainInterface:
        def __init__(self, *args, **kwargs):
            raise ImportError(f"New architecture not available: {e}")
    
    def create_main_interface(*args, **kwargs):
        raise ImportError(f"New architecture not available: {e}")
    
    async def initialize_workers_system(*args, **kwargs):
        raise ImportError(f"New architecture not available: {e}")
    
    # Legacy fallback
    try:
        from .core.base_worker import BaseWorker, WorkerRequest, WorkerResponse, ProcessingError
        from .core.communication import CommunicationManager, MessageProtocol
        from .core.device_manager import get_device_manager, initialize_device_manager
        _LEGACY_COMPONENTS_AVAILABLE = True
    except ImportError:
        _LEGACY_COMPONENTS_AVAILABLE = False
        
        class BaseWorker:
            def __init__(self, *args, **kwargs):
                raise ImportError(f"Workers package not properly configured: {e}")
        class WorkerRequest:
            pass
        class WorkerResponse:
            pass
        class ProcessingError(Exception):
            pass
        class CommunicationManager:
            def __init__(self, *args, **kwargs):
                raise ImportError(f"Workers package not properly configured: {e}")
        class MessageProtocol:
            pass
        def get_device_manager():
            raise ImportError(f"Workers package not properly configured: {e}")
        def initialize_device_manager():
            return False

__version__ = "1.0.0"
__author__ = "SDXL Workers Development Team"

__all__ = [
    # New hierarchical architecture
    "MainInterface",
    "create_main_interface",
    "initialize_workers_system",
    
    # Instructors
    "DeviceInstructor",
    "CommunicationInstructor",
    "ModelInstructor",
    "ConditioningInstructor",
    "InferenceInstructor",
    "SchedulerInstructor",
    "PostprocessingInstructor",
    
    # Legacy components for backward compatibility
    "BaseWorker",
    "WorkerRequest",
    "WorkerResponse",
    "ProcessingError",
    "CommunicationManager",
    "MessageProtocol",
    "get_device_manager",
    "initialize_device_manager"
]
