"""
Communication Module for Worker IPC
===================================

Handles inter-process communication between C# host and Python workers.
Provides JSON-based message passing, request validation, and response formatting.
"""

import json
import sys
import logging
import asyncio
from typing import Dict, Any, Optional, Callable, Awaitable
from datetime import datetime
import jsonschema
from pathlib import Path


class CommunicationManager:
    """
    Manages communication between C# host and Python workers.
    
    Handles JSON message parsing, schema validation, and response formatting
    for seamless IPC communication.
    """
    
    def __init__(self, schema_path: Optional[str] = None):
        self.logger = logging.getLogger(__name__)
        self.schema = None
        self.message_handlers: Dict[str, Callable] = {}
        
        # Load JSON schema if provided
        if schema_path:
            self.load_schema(schema_path)
    
    def load_schema(self, schema_path: str) -> bool:
        """
        Load JSON schema for request validation.
        
        Args:
            schema_path: Path to JSON schema file
            
        Returns:
            True if schema loaded successfully
        """
        try:
            schema_file = Path(schema_path)
            if not schema_file.exists():
                self.logger.error(f"Schema file not found: {schema_path}")
                return False
            
            with open(schema_file, 'r', encoding='utf-8') as f:
                self.schema = json.load(f)
            
            self.logger.info(f"Schema loaded from {schema_path}")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to load schema: {str(e)}")
            return False
    
    def validate_request(self, request_data: Dict[str, Any]) -> tuple[bool, Optional[str]]:
        """
        Validate request against loaded schema.
        
        Args:
            request_data: Request data to validate
            
        Returns:
            Tuple of (is_valid, error_message)
        """
        if not self.schema:
            self.logger.warning("No schema loaded for validation")
            return True, None
        
        try:
            jsonschema.validate(request_data, self.schema)
            return True, None
            
        except jsonschema.ValidationError as e:
            error_msg = f"Validation error: {e.message}"
            self.logger.error(error_msg)
            return False, error_msg
        
        except Exception as e:
            error_msg = f"Validation failed: {str(e)}"
            self.logger.error(error_msg)
            return False, error_msg
    
    def register_handler(self, message_type: str, handler: Callable[[Dict[str, Any]], Awaitable[Dict[str, Any]]]):
        """
        Register a message handler for a specific message type.
        
        Args:
            message_type: Type of message to handle
            handler: Async function to handle the message
        """
        self.message_handlers[message_type] = handler
        self.logger.info(f"Handler registered for message type: {message_type}")
    
    async def process_stdin_message(self) -> Optional[Dict[str, Any]]:
        """
        Process a single message from stdin.
        
        Returns:
            Processed response or None if no message
        """
        try:
            # Read from stdin (blocking)
            line = sys.stdin.readline().strip()
            
            if not line:
                return None
            
            # Parse JSON
            try:
                request_data = json.loads(line)
            except json.JSONDecodeError as e:
                error_response = {
                    "success": False,
                    "error": f"Invalid JSON: {str(e)}",
                    "timestamp": datetime.utcnow().isoformat()
                }
                return error_response
            
            # Validate request
            is_valid, error_msg = self.validate_request(request_data)
            if not is_valid:
                error_response = {
                    "success": False,
                    "error": error_msg,
                    "timestamp": datetime.utcnow().isoformat()
                }
                return error_response
            
            # Determine message type
            message_type = request_data.get('message_type', 'default')
            
            # Find handler
            handler = self.message_handlers.get(message_type)
            if not handler:
                error_response = {
                    "success": False,
                    "error": f"No handler for message type: {message_type}",
                    "timestamp": datetime.utcnow().isoformat()
                }
                return error_response
            
            # Process with handler
            response = await handler(request_data)
            return response
            
        except Exception as e:
            self.logger.error(f"Error processing stdin message: {str(e)}")
            error_response = {
                "success": False,
                "error": f"Processing error: {str(e)}",
                "timestamp": datetime.utcnow().isoformat()
            }
            return error_response
    
    def send_response(self, response: Dict[str, Any]) -> None:
        """
        Send response to stdout for C# host.
        
        Args:
            response: Response data to send
        """
        try:
            # Ensure timestamp is included
            if 'timestamp' not in response:
                response['timestamp'] = datetime.utcnow().isoformat()
            
            # Serialize to JSON
            response_json = json.dumps(response, default=self._json_serializer)
            
            # Send to stdout
            print(response_json, flush=True)
            
            self.logger.debug(f"Response sent: {response.get('request_id', 'unknown')}")
            
        except Exception as e:
            self.logger.error(f"Failed to send response: {str(e)}")
            # Try to send error response
            try:
                error_response = {
                    "success": False,
                    "error": f"Response serialization failed: {str(e)}",
                    "timestamp": datetime.utcnow().isoformat()
                }
                print(json.dumps(error_response), flush=True)
            except:
                pass  # Give up if we can't even send error response
    
    async def start_message_loop(self) -> None:
        """
        Start the main message processing loop.
        
        Continuously processes messages from stdin until EOF or error.
        """
        self.logger.info("Starting message processing loop")
        
        try:
            while True:
                response = await self.process_stdin_message()
                
                if response is None:
                    # EOF or no message
                    break
                
                self.send_response(response)
                
        except KeyboardInterrupt:
            self.logger.info("Message loop interrupted by user")
        except Exception as e:
            self.logger.error(f"Message loop error: {str(e)}")
        finally:
            self.logger.info("Message processing loop ended")
    
    def _json_serializer(self, obj):
        """Custom JSON serializer for special types."""
        if isinstance(obj, datetime):
            return obj.isoformat()
        elif hasattr(obj, '__dict__'):
            return obj.__dict__
        else:
            raise TypeError(f"Object of type {type(obj)} is not JSON serializable")


class MessageProtocol:
    """
    Defines the message protocol between C# and Python workers.
    """
    
    # Message types
    INFERENCE_REQUEST = "inference_request"
    MODEL_LOAD_REQUEST = "model_load_request"
    STATUS_REQUEST = "status_request"
    SHUTDOWN_REQUEST = "shutdown_request"
    
    # Response types
    INFERENCE_RESPONSE = "inference_response"
    MODEL_LOAD_RESPONSE = "model_load_response"
    STATUS_RESPONSE = "status_response"
    ERROR_RESPONSE = "error_response"
    
    @staticmethod
    def create_request(message_type: str, data: Dict[str, Any], request_id: Optional[str] = None) -> Dict[str, Any]:
        """
        Create a standardized request message.
        
        Args:
            message_type: Type of message
            data: Request data
            request_id: Optional request identifier
            
        Returns:
            Formatted request message
        """
        if request_id is None:
            import uuid
            request_id = str(uuid.uuid4())
        
        return {
            "message_type": message_type,
            "request_id": request_id,
            "timestamp": datetime.utcnow().isoformat(),
            "data": data
        }
    
    @staticmethod
    def create_response(request_id: str, success: bool, data: Optional[Dict[str, Any]] = None, 
                       error: Optional[str] = None) -> Dict[str, Any]:
        """
        Create a standardized response message.
        
        Args:
            request_id: Request identifier
            success: Whether the request was successful
            data: Response data (if successful)
            error: Error message (if unsuccessful)
            
        Returns:
            Formatted response message
        """
        response = {
            "request_id": request_id,
            "success": success,
            "timestamp": datetime.utcnow().isoformat()
        }
        
        if success and data is not None:
            response["data"] = data
        elif not success and error is not None:
            response["error"] = error
        
        return response


class StreamingResponse:
    """
    Handles streaming responses for long-running operations.
    """
    
    def __init__(self, request_id: str, comm_manager: CommunicationManager):
        self.request_id = request_id
        self.comm_manager = comm_manager
        self.step_count = 0
    
    def send_progress(self, step: int, total_steps: int, message: str = "") -> None:
        """
        Send progress update.
        
        Args:
            step: Current step number
            total_steps: Total number of steps
            message: Optional progress message
        """
        progress_data = {
            "type": "progress",
            "step": step,
            "total_steps": total_steps,
            "percentage": (step / total_steps) * 100 if total_steps > 0 else 0,
            "message": message
        }
        
        response = MessageProtocol.create_response(
            self.request_id, 
            True, 
            progress_data
        )
        
        self.comm_manager.send_response(response)
    
    def send_intermediate_result(self, result_type: str, data: Dict[str, Any]) -> None:
        """
        Send intermediate result during processing.
        
        Args:
            result_type: Type of intermediate result
            data: Result data
        """
        intermediate_data = {
            "type": "intermediate",
            "result_type": result_type,
            "data": data
        }
        
        response = MessageProtocol.create_response(
            self.request_id,
            True,
            intermediate_data
        )
        
        self.comm_manager.send_response(response)
    
    def send_final_result(self, data: Dict[str, Any]) -> None:
        """
        Send final result.
        
        Args:
            data: Final result data
        """
        final_data = {
            "type": "final",
            "data": data
        }
        
        response = MessageProtocol.create_response(
            self.request_id,
            True,
            final_data
        )
        
        self.comm_manager.send_response(response)


def create_communication_manager(schema_path: Optional[str] = None) -> CommunicationManager:
    """
    Create a new communication manager instance.
    
    Args:
        schema_path: Optional path to JSON schema file
        
    Returns:
        CommunicationManager instance
    """
    return CommunicationManager(schema_path)


def setup_worker_communication(schema_path: Optional[str] = None) -> CommunicationManager:
    """
    Set up communication for a worker process.
    
    Args:
        schema_path: Optional path to JSON schema file
        
    Returns:
        Configured CommunicationManager
    """
    # Set up logging to stderr (stdout is used for communication)
    logging.basicConfig(
        stream=sys.stderr,
        level=logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )
    
    # Create communication manager
    comm_manager = CommunicationManager(schema_path)
    
    return comm_manager
