#!/usr/bin/env python3
"""
Communication - Pure communication layer for worker interactions
"""
import time
import threading
from typing import Dict, Any, List, Optional


class Communication:
    """Communication manager for node-manager to worker interactions"""
    
    def __init__(self, config: Dict[str, Any], logger):
        self.config = config
        self.logger = logger
        
        # Worker tracking
        self.registered_workers: Dict[str, Dict[str, Any]] = {}
        self.worker_messages: Dict[str, List[Dict[str, Any]]] = {}
        self.worker_last_heartbeat: Dict[str, float] = {}
        
        # Configuration
        self.timeout = config.get("worker_timeout", 60)
        self.heartbeat_interval = config.get("heartbeat_interval", 10)
        self.retry_attempts = config.get("retry_attempts", 3)
        self.message_timeout = config.get("message_timeout", 30)
        
        # Message ID counter
        self._message_id_counter = 0
        self._message_lock = threading.Lock()
        
        self.logger.info("Communication manager initialized")
    
    def register_worker(self, worker_id: str, capabilities: Dict[str, Any]) -> bool:
        """Register a new worker"""
        try:
            self.registered_workers[worker_id] = {
                'worker_id': worker_id,
                'registration_time': time.time(),
                'last_seen': time.time(),
                'capabilities': capabilities,
                'status': 'idle'
            }
            
            self.worker_messages[worker_id] = []
            self.worker_last_heartbeat[worker_id] = time.time()
            
            self.logger.info(f"Worker {worker_id} registered with capabilities: {list(capabilities.keys())}")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to register worker {worker_id}: {e}")
            return False
    
    def update_worker_heartbeat(self, worker_id: str) -> bool:
        """Update worker heartbeat timestamp"""
        try:
            if worker_id in self.registered_workers:
                self.worker_last_heartbeat[worker_id] = time.time()
                self.registered_workers[worker_id]['last_seen'] = time.time()
                return True
            else:
                self.logger.warning(f"Heartbeat from unregistered worker: {worker_id}")
                return False
                
        except Exception as e:
            self.logger.error(f"Failed to update heartbeat for {worker_id}: {e}")
            return False
    
    def send_message_to_worker(self, worker_id: str, message: Dict[str, Any]) -> str:
        """Send a message to a specific worker"""
        try:
            if worker_id not in self.registered_workers:
                self.logger.error(f"Cannot send message to unregistered worker: {worker_id}")
                return ""
            
            # Generate unique message ID
            with self._message_lock:
                self._message_id_counter += 1
                message_id = f"msg_{self._message_id_counter}_{int(time.time())}"
            
            # Add metadata to message
            message_with_metadata = {
                'message_id': message_id,
                'timestamp': time.time(),
                'worker_id': worker_id,
                'type': message.get('type', 'instruction'),
                'data': message
            }
            
            # Store message in worker's queue
            if worker_id not in self.worker_messages:
                self.worker_messages[worker_id] = []
            
            self.worker_messages[worker_id].append(message_with_metadata)
            
            self.logger.debug(f"Message {message_id} sent to worker {worker_id}")
            return message_id
            
        except Exception as e:
            self.logger.error(f"Failed to send message to worker {worker_id}: {e}")
            return ""
    
    def get_worker_messages(self, worker_id: str) -> List[Dict[str, Any]]:
        """Get all pending messages for a worker"""
        try:
            if worker_id not in self.worker_messages:
                return []
            
            # Update heartbeat
            self.update_worker_heartbeat(worker_id)
            
            # Get all messages and clear the queue
            messages = self.worker_messages[worker_id].copy()
            self.worker_messages[worker_id].clear()
            
            self.logger.debug(f"Retrieved {len(messages)} messages for worker {worker_id}")
            return messages
            
        except Exception as e:
            self.logger.error(f"Failed to get messages for worker {worker_id}: {e}")
            return []
    
    def send_task_to_worker(self, worker_id: str, task_config: Dict[str, Any]) -> bool:
        """Send a task to a specific worker"""
        try:
            task_message = {
                'type': 'task',
                'action': 'process_task',
                'task_config': task_config,
                'timestamp': time.time()
            }
            
            message_id = self.send_message_to_worker(worker_id, task_message)
            
            if message_id:
                self.logger.info(f"Task {task_config.get('task_id', 'unknown')} sent to worker {worker_id}")
                return True
            else:
                self.logger.error(f"Failed to send task to worker {worker_id}")
                return False
                
        except Exception as e:
            self.logger.error(f"Failed to send task to worker {worker_id}: {e}")
            return False
    
    def send_instruction_to_worker(self, worker_id: str, instruction: Dict[str, Any]) -> bool:
        """Send an instruction to a worker"""
        try:
            instruction_message = {
                'type': 'instruction',
                'action': instruction.get('action', 'unknown'),
                'parameters': instruction.get('parameters', {}),
                'timestamp': time.time()
            }
            
            message_id = self.send_message_to_worker(worker_id, instruction_message)
            
            if message_id:
                self.logger.info(f"Instruction '{instruction.get('action')}' sent to worker {worker_id}")
                return True
            else:
                return False
                
        except Exception as e:
            self.logger.error(f"Failed to send instruction to worker {worker_id}: {e}")
            return False
    
    def send_model_load_to_worker(self, worker_id: str, model_name: str, model_path: str) -> bool:
        """Send model load instruction to worker"""
        try:
            instruction = {
                'action': 'load_model',
                'parameters': {
                    'model_name': model_name,
                    'model_path': model_path
                }
            }
            
            return self.send_instruction_to_worker(worker_id, instruction)
            
        except Exception as e:
            self.logger.error(f"Failed to send model load instruction to worker {worker_id}: {e}")
            return False
    
    def get_registered_workers(self) -> List[Dict[str, Any]]:
        """Get list of all registered workers"""
        return list(self.registered_workers.values())
    
    def get_active_workers(self) -> List[Dict[str, Any]]:
        """Get list of active workers (recently seen)"""
        active_workers = []
        current_time = time.time()
        
        for worker_id, worker_info in self.registered_workers.items():
            if current_time - worker_info['last_seen'] < self.timeout:
                active_workers.append(worker_info)
        
        return active_workers
    
    def is_worker_active(self, worker_id: str) -> bool:
        """Check if a worker is currently active"""
        if worker_id not in self.registered_workers:
            return False
        
        last_seen = self.registered_workers[worker_id]['last_seen']
        return time.time() - last_seen < self.timeout
    
    def get_worker_status(self, worker_id: str) -> Optional[Dict[str, Any]]:
        """Get status information for a specific worker"""
        if worker_id not in self.registered_workers:
            return None
        
        worker_info = self.registered_workers[worker_id].copy()
        worker_info['is_active'] = self.is_worker_active(worker_id)
        worker_info['pending_messages'] = len(self.worker_messages.get(worker_id, []))
        
        return worker_info
    
    def cleanup_inactive_workers(self):
        """Clean up workers that haven't been seen recently"""
        try:
            current_time = time.time()
            inactive_workers = []
            
            for worker_id, worker_info in self.registered_workers.items():
                if current_time - worker_info['last_seen'] > self.timeout * 2:  # Double timeout for cleanup
                    inactive_workers.append(worker_id)
            
            for worker_id in inactive_workers:
                self.logger.info(f"Cleaning up inactive worker: {worker_id}")
                del self.registered_workers[worker_id]
                
                if worker_id in self.worker_messages:
                    del self.worker_messages[worker_id]
                    
                if worker_id in self.worker_last_heartbeat:
                    del self.worker_last_heartbeat[worker_id]
            
            if inactive_workers:
                self.logger.info(f"Cleaned up {len(inactive_workers)} inactive workers")
                
        except Exception as e:
            self.logger.error(f"Failed to cleanup inactive workers: {e}")
    
    def get_communication_statistics(self) -> Dict[str, Any]:
        """Get communication system statistics"""
        try:
            current_time = time.time()
            
            # Count active vs inactive workers
            active_count = 0
            for worker_info in self.registered_workers.values():
                if current_time - worker_info['last_seen'] < self.timeout:
                    active_count += 1
            
            # Count total pending messages
            total_pending_messages = sum(len(messages) for messages in self.worker_messages.values())
            
            return {
                'total_registered_workers': len(self.registered_workers),
                'active_workers': active_count,
                'inactive_workers': len(self.registered_workers) - active_count,
                'total_pending_messages': total_pending_messages,
                'worker_queues': {worker_id: len(messages) 
                                for worker_id, messages in self.worker_messages.items()},
                'heartbeat_timeout': self.timeout,
                'message_timeout': self.message_timeout
            }
            
        except Exception as e:
            self.logger.error(f"Failed to get communication statistics: {e}")
            return {}
    
    def broadcast_message(self, message: Dict[str, Any], active_only: bool = True) -> int:
        """Broadcast a message to all workers"""
        try:
            sent_count = 0
            
            workers_to_send = self.get_active_workers() if active_only else self.get_registered_workers()
            
            for worker_info in workers_to_send:
                worker_id = worker_info['worker_id']
                message_id = self.send_message_to_worker(worker_id, message)
                
                if message_id:
                    sent_count += 1
            
            self.logger.info(f"Broadcast message sent to {sent_count} workers")
            return sent_count
            
        except Exception as e:
            self.logger.error(f"Failed to broadcast message: {e}")
            return 0
