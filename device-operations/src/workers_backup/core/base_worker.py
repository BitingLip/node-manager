"""
Core Infrastructure Module for SDXL Workers
===========================================

This module provides the foundational infrastructure for the modular SDXL workers system.
It includes base classes, device management, communication protocols, and shared utilities.
"""

import logging
import json
import traceback
from abc import ABC, abstractmethod
from typing import Dict, Any, Optional, Union, List
from dataclasses import dataclass, asdict
from datetime import datetime
import uuid


@dataclass
class WorkerRequest:
    """Standard request format for all workers."""
    request_id: str
    worker_type: str
    data: Dict[str, Any]
    priority: str = "normal"
    timeout: int = 300
    timestamp: Optional[datetime] = None
    
    def __post_init__(self):
        if self.timestamp is None:
            self.timestamp = datetime.utcnow()


@dataclass
class WorkerResponse:
    """Standard response format for all workers."""
    request_id: str
    success: bool
    data: Optional[Dict[str, Any]] = None
    error: Optional[str] = None
    warnings: Optional[List[str]] = None
    execution_time: Optional[float] = None
    timestamp: Optional[datetime] = None
    
    def __post_init__(self):
        if self.timestamp is None:
            self.timestamp = datetime.utcnow()


class BaseWorker(ABC):
    """
    Abstract base class for all SDXL workers.
    
    Provides common functionality including:
    - Request/response handling
    - Error management
    - Logging
    - Resource cleanup
    """
    
    def __init__(self, worker_id: str, config: Optional[Dict[str, Any]] = None):
        self.worker_id = worker_id
        self.config = config or {}
        self.logger = self._setup_logger()
        self.is_initialized = False
        self._resources = {}
        
    def _setup_logger(self) -> logging.Logger:
        """Set up worker-specific logger."""
        logger = logging.getLogger(f"worker.{self.worker_id}")
        logger.setLevel(logging.INFO)
        
        if not logger.handlers:
            handler = logging.StreamHandler()
            formatter = logging.Formatter(
                '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
            )
            handler.setFormatter(formatter)
            logger.addHandler(handler)
            
        return logger
    
    @abstractmethod
    async def initialize(self) -> bool:
        """Initialize worker resources."""
        pass
    
    @abstractmethod
    async def process_request(self, request: WorkerRequest) -> WorkerResponse:
        """Process a worker request."""
        pass
    
    @abstractmethod
    async def cleanup(self) -> None:
        """Clean up worker resources."""
        pass
    
    async def handle_request(self, request_data: Dict[str, Any]) -> Dict[str, Any]:
        """
        Main entry point for handling requests.
        
        Args:
            request_data: Raw request data
            
        Returns:
            Response data dictionary
        """
        start_time = datetime.utcnow()
        
        try:
            # Parse request
            request = WorkerRequest(**request_data)
            self.logger.info(f"Processing request {request.request_id}")
            
            # Initialize if needed
            if not self.is_initialized:
                await self.initialize()
                self.is_initialized = True
            
            # Process request
            response = await self.process_request(request)
            
            # Calculate execution time
            execution_time = (datetime.utcnow() - start_time).total_seconds()
            response.execution_time = execution_time
            
            self.logger.info(
                f"Request {request.request_id} completed in {execution_time:.2f}s"
            )
            
            return asdict(response)
            
        except Exception as e:
            execution_time = (datetime.utcnow() - start_time).total_seconds()
            error_msg = f"Error processing request: {str(e)}"
            self.logger.error(f"{error_msg}\n{traceback.format_exc()}")
            
            # Create error response
            error_response = WorkerResponse(
                request_id=request_data.get('request_id', str(uuid.uuid4())),
                success=False,
                error=error_msg,
                execution_time=execution_time
            )
            
            return asdict(error_response)
    
    def get_status(self) -> Dict[str, Any]:
        """Get worker status information."""
        return {
            "worker_id": self.worker_id,
            "is_initialized": self.is_initialized,
            "config": self.config,
            "resources": list(self._resources.keys())
        }


class WorkerError(Exception):
    """Base exception for worker errors."""
    pass


class ValidationError(WorkerError):
    """Raised when request validation fails."""
    pass


class InitializationError(WorkerError):
    """Raised when worker initialization fails."""
    pass


class ProcessingError(WorkerError):
    """Raised when request processing fails."""
    pass


def validate_request_schema(request_data: Dict[str, Any], required_fields: List[str]) -> None:
    """
    Validate that request contains required fields.
    
    Args:
        request_data: Request data to validate
        required_fields: List of required field names
        
    Raises:
        ValidationError: If validation fails
    """
    missing_fields = []
    
    for field in required_fields:
        if field not in request_data:
            missing_fields.append(field)
    
    if missing_fields:
        raise ValidationError(f"Missing required fields: {missing_fields}")


def setup_worker_logging(log_level: str = "INFO", log_file: Optional[str] = None) -> None:
    """
    Set up logging configuration for workers.
    
    Args:
        log_level: Logging level (DEBUG, INFO, WARNING, ERROR)
        log_file: Optional log file path
    """
    logging.basicConfig(
        level=getattr(logging, log_level.upper()),
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        handlers=[
            logging.StreamHandler(),
            *([logging.FileHandler(log_file)] if log_file else [])
        ]
    )


def create_request_id() -> str:
    """Generate a unique request ID."""
    return f"req_{uuid.uuid4().hex[:8]}_{int(datetime.utcnow().timestamp())}"


def serialize_response(response: Union[WorkerResponse, Dict[str, Any]]) -> str:
    """
    Serialize response to JSON string.
    
    Args:
        response: Response object or dictionary
        
    Returns:
        JSON string representation
    """
    if isinstance(response, WorkerResponse):
        response_dict = asdict(response)
    else:
        response_dict = response
    
    # Handle datetime serialization
    def datetime_serializer(obj):
        if isinstance(obj, datetime):
            return obj.isoformat()
        raise TypeError(f"Object of type {type(obj)} is not JSON serializable")
    
    return json.dumps(response_dict, default=datetime_serializer, indent=2)


def deserialize_request(request_json: str) -> Dict[str, Any]:
    """
    Deserialize JSON request string.
    
    Args:
        request_json: JSON string
        
    Returns:
        Request dictionary
    """
    try:
        return json.loads(request_json)
    except json.JSONDecodeError as e:
        raise ValidationError(f"Invalid JSON format: {str(e)}")


# Configuration constants
DEFAULT_TIMEOUT = 300
MAX_TIMEOUT = 3600
DEFAULT_PRIORITY = "normal"
VALID_PRIORITIES = ["low", "normal", "high"]

# Worker types
WORKER_TYPES = {
    "sdxl_inference": "SDXL Inference Worker",
    "model_loader": "Model Loading Worker", 
    "scheduler_factory": "Scheduler Factory Worker",
    "conditioning": "Conditioning Worker",
    "postprocessing": "Postprocessing Worker"
}
