"""
Core Infrastructure Package
===========================

Core components for the SDXL workers system including base classes,
device management, and communication protocols.
"""

# Import base worker components (always available)
from .base_worker import (
    BaseWorker,
    WorkerRequest,
    WorkerResponse,
    WorkerError,
    ValidationError,
    InitializationError,
    ProcessingError,
    validate_request_schema,
    setup_worker_logging,
    create_request_id,
    serialize_response,
    deserialize_request
)

# Import communication (always available)
from .communication import (
    CommunicationManager,
    MessageProtocol,
    StreamingResponse,
    create_communication_manager,
    setup_worker_communication
)

# Conditionally import device manager (requires torch)
try:
    from .device_manager import (
        DeviceManager,
        DeviceInfo,
        DeviceType,
        get_device_manager,
        initialize_device_manager,
        cleanup_device_manager
    )
    _DEVICE_MANAGER_AVAILABLE = True
except ImportError:
    # Create dummy classes/functions for when torch is not available
    _DEVICE_MANAGER_AVAILABLE = False
    
    class DeviceManager:
        def __init__(self):
            raise ImportError("DeviceManager requires torch to be installed")
    
    class DeviceInfo:
        pass
        
    class DeviceType:
        pass
    
    def get_device_manager():
        raise ImportError("Device manager requires torch to be installed")
    
    def initialize_device_manager():
        return False
    
    def cleanup_device_manager():
        pass

__all__ = [
    # Base worker components
    "BaseWorker",
    "WorkerRequest", 
    "WorkerResponse",
    "WorkerError",
    "ValidationError",
    "InitializationError", 
    "ProcessingError",
    "validate_request_schema",
    "setup_worker_logging",
    "create_request_id",
    "serialize_response",
    "deserialize_request",
    
    # Device management
    "DeviceManager",
    "DeviceInfo",
    "DeviceType", 
    "get_device_manager",
    "initialize_device_manager",
    "cleanup_device_manager",
    
    # Communication
    "CommunicationManager",
    "MessageProtocol",
    "StreamingResponse",
    "create_communication_manager",
    "setup_worker_communication"
]
