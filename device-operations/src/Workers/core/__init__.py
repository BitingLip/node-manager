"""
Core Infrastructure Package
===========================

Core components for the SDXL workers system including base classes,
device management, and communication protocols.
"""

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

from .device_manager import (
    DeviceManager,
    DeviceInfo,
    DeviceType,
    get_device_manager,
    initialize_device_manager,
    cleanup_device_manager
)

from .communication import (
    CommunicationManager,
    MessageProtocol,
    StreamingResponse,
    create_communication_manager,
    setup_worker_communication
)

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
