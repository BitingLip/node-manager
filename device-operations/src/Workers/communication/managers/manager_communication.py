"""
Communication Manager for SDXL Workers System
============================================

Migrated from core/communication.py
Handles communication protocols and message passing between workers and orchestrators.
"""

import json
import sys
import logging
from typing import Dict, Any, Optional, Union, List
from datetime import datetime
from enum import Enum

logger = logging.getLogger(__name__)


class MessageType(Enum):
    """Standard message types for worker communication."""
    INFERENCE_REQUEST = "inference_request"
    HEALTH_CHECK = "health"
    MODEL_LOAD = "load_model"
    WORKER_STATUS = "status"
    ERROR = "error"
    RESPONSE = "response"


class MessageProtocol:
    """Standard message protocol for worker communication."""
    
    @staticmethod
    def create_message(message_type: MessageType, data: Dict[str, Any], 
                      request_id: Optional[str] = None) -> Dict[str, Any]:
        """Create a standardized message."""
        return {
            "message_type": message_type.value,
            "request_id": request_id or f"msg_{datetime.now().timestamp()}",
            "timestamp": datetime.now().isoformat(),
            "data": data
        }
    
    @staticmethod
    def create_response(success: bool, data: Optional[Dict[str, Any]] = None,
                       error: Optional[str] = None, request_id: Optional[str] = None) -> Dict[str, Any]:
        """Create a standardized response message."""
        response = {
            "success": success,
            "timestamp": datetime.now().isoformat(),
            "request_id": request_id
        }
        
        if success and data:
            response["data"] = data
        elif not success and error:
            response["error"] = error
            
        return response
    
    @staticmethod
    def parse_message(message_json: str) -> Optional[Dict[str, Any]]:
        """Parse a JSON message safely."""
        try:
            return json.loads(message_json)
        except json.JSONDecodeError as e:
            logger.error(f"Failed to parse message: {e}")
            return None


class CommunicationManager:
    """Manages communication channels for workers."""
    
    def __init__(self, config: Optional[Dict[str, Any]] = None):
        self.config = config or {}
        self.use_stdin_stdout = self.config.get("use_stdin_stdout", True)
        self.logger = logging.getLogger("%s.%s" % (__name__, self.__class__.__name__))
        
    async def initialize(self) -> bool:
        """Initialize communication manager."""
        try:
            self.logger.info("Initializing communication manager...")
            self.logger.info("Communication manager initialized successfully")
            return True
        except Exception as e:
            self.logger.error(f"Communication manager initialization failed: {e}")
            return False
        
    async def send_message(self, message: Dict[str, Any]) -> bool:
        """Send a message via the configured channel."""
        try:
            if self.use_stdin_stdout:
                print(json.dumps(message))
                sys.stdout.flush()
                return True
            else:
                # Future: Add HTTP/WebSocket support
                self.logger.warning("Non-stdout communication not implemented")
                return False
        except Exception as e:
            self.logger.error(f"Failed to send message: {e}")
            return False
    
    async def receive_message(self) -> Optional[Dict[str, Any]]:
        """Receive a message via the configured channel."""
        try:
            if self.use_stdin_stdout:
                line = sys.stdin.readline().strip()
                if line:
                    return MessageProtocol.parse_message(line)
                return None
            else:
                # Future: Add HTTP/WebSocket support
                self.logger.warning("Non-stdin communication not implemented")
                return None
        except Exception as e:
            self.logger.error(f"Failed to receive message: {e}")
            return None
    
    async def send_response(self, success: bool, data: Optional[Dict[str, Any]] = None,
                           error: Optional[str] = None, request_id: Optional[str] = None) -> bool:
        """Send a standardized response."""
        response = MessageProtocol.create_response(success, data, error, request_id)
        return await self.send_message(response)
    
    async def send_error(self, error_message: str, request_id: Optional[str] = None) -> bool:
        """Send an error response."""
        return await self.send_response(False, error=error_message, request_id=request_id)
    
    async def send_health_status(self, status: str = "healthy") -> bool:
        """Send health status."""
        health_data = {
            "status": status,
            "timestamp": datetime.now().isoformat(),
            "worker_type": "sdxl_worker"
        }
        return await self.send_response(True, data=health_data)
    
    async def get_status(self) -> Dict[str, Any]:
        """Get communication manager status."""
        return {
            "use_stdin_stdout": self.use_stdin_stdout,
            "status": "active"
        }
    
    async def cleanup(self) -> None:
        """Clean up communication manager resources."""
        try:
            self.logger.info("Cleaning up communication manager...")
            self.logger.info("Communication manager cleanup complete")
        except Exception as e:
            self.logger.error(f"Communication manager cleanup error: {e}")


# Convenience functions for common operations
def create_inference_response(success: bool, images: Optional[List[str]] = None,
                            processing_time: float = 0, seed_used: Optional[int] = None,
                            error: Optional[str] = None, request_id: Optional[str] = None) -> Dict[str, Any]:
    """Create a standardized inference response."""
    if success:
        data = {
            "images": images or [],
            "processing_time": processing_time,
            "seed_used": seed_used,
            "generated_at": datetime.now().isoformat()
        }
        return MessageProtocol.create_response(True, data=data, request_id=request_id)
    else:
        return MessageProtocol.create_response(False, error=error, request_id=request_id)


def create_health_response(worker_type: str = "sdxl_worker") -> Dict[str, Any]:
    """Create a health check response."""
    data = {
        "status": "healthy",
        "worker_type": worker_type,
        "capabilities": ["text2img", "img2img", "inpainting"],
        "timestamp": datetime.now().isoformat()
    }
    return MessageProtocol.create_response(True, data=data)


class StreamingResponse:
    """Simple streaming response placeholder for worker communication."""
    
    def __init__(self, data: Dict[str, Any], partial: bool = False):
        self.data = data
        self.partial = partial
        self.timestamp = datetime.now()
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary format."""
        return {
            "data": self.data,
            "partial": self.partial,
            "timestamp": self.timestamp.isoformat()
        }


def create_communication_manager(use_stdin_stdout: bool = True) -> CommunicationManager:
    """Factory function to create a communication manager."""
    config = {"use_stdin_stdout": use_stdin_stdout}
    return CommunicationManager(config)


def setup_worker_communication(worker_name: str = "worker") -> CommunicationManager:
    """Setup communication for a worker with standard configuration."""
    config = {"use_stdin_stdout": True}
    manager = CommunicationManager(config)
    logger.info("Communication setup complete for %s", worker_name)
    return manager