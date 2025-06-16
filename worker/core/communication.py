#!/usr/bin/env python3
"""
Communication - Direct queue-based communication with node-manager
Handles shared memory queue communication for local worker-manager interaction
"""
import time
import queue
from typing import Dict, Any, Optional
from datetime import datetime


class Communication:
    """Direct queue-based communication manager for worker-to-node-manager interactions"""
    
    def __init__(self, worker_id: str, instruction_queue: queue.Queue, 
                 result_queue: queue.Queue, status_queue: queue.Queue, logger, 
                 shared_queues: Optional[Dict[str, Any]] = None):
        self.worker_id = worker_id
        self.instruction_queue = instruction_queue
        self.result_queue = result_queue
        self.status_queue = status_queue
        self.logger = logger
        
        # Shared queues for direct communication with node manager
        self.shared_queues = shared_queues
        self.using_shared_queues = shared_queues is not None
        
        # Connection state
        self.connected = False
        self.last_heartbeat = 0
        if self.using_shared_queues:
            self.logger.info(f"Communication manager initialized for {worker_id} using shared queues")
        else:
            self.logger.info(f"Communication manager initialized for {worker_id} using fallback mode")
    
    def register_with_node_manager(self) -> bool:
        """Register this worker with the node-manager via shared queues"""
        if not self.using_shared_queues:
            self.logger.warning("Cannot register - no shared queues available")
            return False
        
        try:
            registration_data = {
                'type': 'registration',
                'worker_id': self.worker_id,
                'timestamp': time.time(),
                'capabilities': {
                    'gpu_inference': True,
                    'model_types': ['stable_diffusion_xl'],
                    'max_resolution': '2048x2048'
                },
                'status': 'idle'
            }
            
            self.logger.info(f"Registering worker {self.worker_id} via shared queue")
            # Send registration to node manager via shared status queue
            if self.shared_queues and (shared_status_queue := self.shared_queues.get('status_queue')):
                shared_status_queue.put(registration_data)
                self.connected = True
                self.logger.info("Successfully registered with node-manager via shared queue")
                return True
            else:
                self.logger.error("Shared status queue not available for registration")
                return False
                
        except Exception as e:
            self.logger.error(f"Registration failed: {e}")
            return False

    def handle_incoming_messages(self):
        """Check for and handle incoming messages from node-manager via worker-specific queue"""
        if not self.using_shared_queues:
            self.logger.debug(f"Worker {self.worker_id} - Not using shared queues, skipping message handling")
            return
        
        # Log every call to see if this method is being called
        if not hasattr(self, '_last_call_log') or time.time() - self._last_call_log > 5:
            self.logger.info(f"Worker {self.worker_id} - handle_incoming_messages called")
            self._last_call_log = time.time()
        
        try:
            # Get messages from worker-specific instruction queue
            worker_queue_key = f"instruction_queue_{self.worker_id}"
              # Debug: Log available queues periodically
            if not hasattr(self, '_last_queue_debug') or time.time() - self._last_queue_debug > 10:
                available_queues = list(self.shared_queues.keys()) if self.shared_queues else []
                self.logger.info(f"Worker {self.worker_id} - Available queues: {available_queues}")
                self.logger.info(f"Worker {self.worker_id} - Looking for queue: {worker_queue_key}")
                self._last_queue_debug = time.time()
            
            if self.shared_queues is not None and (instruction_queue := self.shared_queues.get(worker_queue_key)):
                try:
                    # Check for messages in this worker's specific queue (non-blocking)
                    message = instruction_queue.get_nowait()
                    
                    # Process message - no need to check worker_id since this is our specific queue
                    self.logger.info(f"Worker {self.worker_id} received message: {message.get('type', 'unknown')}")
                    self._process_message(message)
                    
                except queue.Empty:
                    # No messages available - this is normal
                    pass
            else:                # Log once if the queue is missing (but don't spam)
                if not hasattr(self, '_logged_missing_queue'):
                    self.logger.warning(f"Worker-specific instruction queue not found: {worker_queue_key}")
                    available_queues = list(self.shared_queues.keys()) if self.shared_queues else []
                    self.logger.warning(f"Available queues: {available_queues}")
                    self._logged_missing_queue = True
                    
        except Exception as e:
            self.logger.error(f"Error handling messages: {e}")

    def _process_message(self, message: Dict[str, Any]):
        """Process a received message from node manager"""
        try:
            message_type = message.get('type', message.get('message_type'))
            
            if message_type == 'instruction':
                self._handle_instruction(message)
            elif message_type == 'ping':
                self._handle_ping(message)
            elif message_type == 'shutdown':
                self._handle_shutdown(message)
            elif message_type == 'status_request':
                self._handle_status_request(message)
            else:
                self.logger.warning(f"Unknown message type: {message_type}")
                
        except Exception as e:
            self.logger.error(f"Error processing message: {e}")

    def _handle_instruction(self, message: Dict[str, Any]):
        """Handle instruction message from node-manager"""
        try:
            self.logger.debug(f"Received message: {message}")
            instruction = message.get('data', message)
            instruction['id'] = message.get('data', {}).get('task_id') or message.get('id', message.get('task_id'))
            instruction['received_at'] = time.time()
            
            self.logger.debug(f"Processed instruction: {instruction}")
            
            # Queue instruction for action thread
            self.instruction_queue.put(instruction)
            
            self.logger.info(f"Queued instruction: {instruction.get('action', 'task')}")
            
        except Exception as e:
            self.logger.error(f"Error handling instruction: {e}")
    
    def _handle_ping(self, message: Dict[str, Any]):
        """Handle ping message from node-manager"""
        self.logger.debug("Ping received from node manager")
    
    def _handle_shutdown(self, message: Dict[str, Any]):
        """Handle shutdown message from node-manager"""
        self.logger.info("Shutdown command received from node-manager")
        
        # Queue a special shutdown instruction
        shutdown_instruction = {
            'id': message.get('id'),
            'action': 'shutdown',
            'params': {},
            'received_at': time.time()
        }
        
        self.instruction_queue.put(shutdown_instruction)
    
    def _handle_status_request(self, message: Dict[str, Any]):
        """Handle status request from node-manager"""
        self.logger.debug("Status request received from node manager")
    
    def send_result(self, result: Dict[str, Any]):
        """Send result to node-manager via shared queue"""
        if not self.using_shared_queues:
            self.logger.warning("Cannot send result - no shared queues available")
            return
        
        try:
            result_message = {
                'worker_id': self.worker_id,
                'timestamp': time.time(),
                'message_type': 'result',
                'result': result
            }
            
            if self.shared_queues is not None and (result_queue := self.shared_queues.get('result_queue')):
                result_queue.put(result_message)
                self.logger.info(f"Sent result for task: {result.get('task_id')}")
            else:
                self.logger.error("Result queue not found in shared queues")
                
        except Exception as e:
            self.logger.error(f"Error sending result: {e}")
    
    def send_status(self, status: Dict[str, Any]):
        """Send status update to node-manager via shared queue"""
        if not self.using_shared_queues:
            return
        
        try:
            status_message = {
                'worker_id': self.worker_id,
                'timestamp': time.time(),
                'message_type': 'status',
                'status': status
            }
            
            if self.shared_queues is not None and (status_queue := self.shared_queues.get('status_queue')):
                status_queue.put(status_message)
                self.logger.debug("Status update sent to node manager")
            else:
                self.logger.warning("Status queue not found in shared queues")
                
        except Exception as e:
            self.logger.error(f"Error sending status: {e}")
    
    def send_heartbeat(self):
        """Send heartbeat to node-manager via shared queue"""
        if not self.using_shared_queues:
            return
        
        try:
            current_time = time.time()
            
            # Send heartbeat every 10 seconds
            if current_time - self.last_heartbeat < 10:
                return
            
            heartbeat_data = {
                'worker_id': self.worker_id,
                'timestamp': current_time,
                'message_type': 'heartbeat',
                'status': 'alive'
            }
            
            if self.shared_queues is not None and (status_queue := self.shared_queues.get('status_queue')):
                status_queue.put(heartbeat_data)
                self.last_heartbeat = current_time
                self.logger.debug("Heartbeat sent")
            
        except Exception as e:
            self.logger.warning(f"Heartbeat error: {e}")
    
    def send_error_report(self, error: str, context: Optional[Dict[str, Any]] = None):
        """Send error report to node-manager via shared queue"""
        if not self.using_shared_queues:
            return
        
        try:
            error_data = {
                'worker_id': self.worker_id,
                'timestamp': time.time(),
                'message_type': 'error',
                'error': error,
                'context': context or {}
            }
            
            if self.shared_queues is not None and (status_queue := self.shared_queues.get('status_queue')):
                status_queue.put(error_data)
                self.logger.info(f"Error report sent: {error[:50]}")
            
        except Exception as e:
            self.logger.error(f"Error sending error report: {e}")
    
    def disconnect(self):
        """Disconnect from node-manager"""
        try:
            if self.connected and self.using_shared_queues:
                disconnect_data = {
                    'worker_id': self.worker_id,
                    'timestamp': time.time(),
                    'message_type': 'disconnect',
                    'reason': 'normal_shutdown'
                }
                
                if self.shared_queues is not None and (status_queue := self.shared_queues.get('status_queue')):
                    status_queue.put(disconnect_data)
                    self.logger.info("Disconnected from node-manager")
            
            self.connected = False
            
        except Exception as e:
            self.logger.error(f"Error during disconnect: {e}")
    
    def is_connected(self) -> bool:
        """Check if connected to node-manager"""
        return self.connected and self.using_shared_queues
    
    def get_connection_status(self) -> Dict[str, Any]:
        """Get detailed connection status"""
        return {
            'connected': self.connected,
            'using_shared_queues': self.using_shared_queues,
            'last_heartbeat': self.last_heartbeat,
            'worker_id': self.worker_id
        }
