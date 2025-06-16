#!/usr/bin/env python3
"""
Worker Manager - Manages worker processes, registration, and communication
"""
import time
import threading
import queue
from typing import Dict, List, Optional, Any
from dataclasses import dataclass
from multiprocessing import Process, Manager
from pathlib import Path
import sys
import os


def worker_wrapper(device_id, shared_queues):
    """Wrapper function for worker processes to setup environment and imports"""
    # Add the project root to Python path for imports
    project_root = Path(__file__).parent.parent
    if str(project_root) not in sys.path:
        sys.path.insert(0, str(project_root))
    
    # Import and run worker main function
    from worker.worker import main as worker_main
    worker_main(device_id, shared_queues)

@dataclass 
class WorkerStatus:
    """Worker status information"""
    worker_id: str
    device_id: int
    status: str  # "idle", "busy", "loading", "error", "offline"
    current_task: Optional[str] = None
    last_activity: float = 0.0
    capabilities: Optional[Dict[str, Any]] = None
    vram_usage_mb: int = 0
    current_model: Optional[str] = None
    
    def __post_init__(self):
        if self.capabilities is None:
            self.capabilities = {}
        if self.last_activity == 0.0:
            self.last_activity = time.time()


class WorkerManager:
    """Manages worker processes, registration, and communication"""
    
    def __init__(self, database, logger, communication, config: Dict):
        self.database = database
        self.logger = logger
        self.communication = communication
        self.config = config
        
        # Worker tracking
        self.worker_status: Dict[str, WorkerStatus] = {}
        self.worker_processes: Dict[int, Process] = {}
        self.shared_queues: Dict[str, Any] = {}
        
        # Configuration
        self.max_workers = config.get("max_workers", 8)
        self.heartbeat_timeout = config.get("heartbeat_timeout", 30)
        self.worker_spawn_delay = config.get("worker_spawn_delay", 0.1)
        self.device_list = config.get("device_list", [0, 1, 2, 3, 4])
        self.auto_start_workers = config.get("auto_start_workers", True)
        self.parallel_worker_spawn = config.get("parallel_worker_spawn", True)
          # Initialize shared queues
        manager = Manager()
        
        # Create worker-specific instruction queues to avoid race conditions
        self.shared_queues = {
            "result_queue": manager.Queue(),
            "status_queue": manager.Queue()
        }
        
        # Create individual instruction queues for each worker
        for device_id in self.device_list:
            worker_id = f"worker_{device_id}"
            self.shared_queues[f"instruction_queue_{worker_id}"] = manager.Queue()
        
        self.logger.info(f"Created worker-specific instruction queues for devices: {self.device_list}")
        
        # Health monitoring
        self.health_check_thread = None
        self.monitoring_active = False
        
        self.logger.info("WorkerManager initialized")
        
    def register_worker(self, worker_id: str, device_id: int, capabilities: Optional[Dict] = None) -> bool:
        """Register a new worker"""
        try:
            # Create worker status
            worker_status = WorkerStatus(
                worker_id=worker_id,
                device_id=device_id,
                status="idle",
                capabilities=capabilities or {}
            )
            
            # Store in memory
            self.worker_status[worker_id] = worker_status
            
            # Register in database
            if self.database and self.database.connected:
                self.database.register_worker(worker_id, device_id)
              # Register with communication layer
            if self.communication:
                self.communication.register_worker(worker_id, capabilities or {})
            
            self.logger.info(f"Worker {worker_id} registered on device {device_id}")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to register worker {worker_id}: {e}")
            return False
            
    def update_worker_status(self, worker_id: str, status: str, current_task: Optional[str] = None):
        """Update worker status"""
        try:
            if worker_id in self.worker_status:
                worker = self.worker_status[worker_id]
                worker.status = status
                worker.current_task = current_task
                worker.last_activity = time.time()
                
                # Update database
                if self.database and self.database.connected:
                    self.database.update_worker_status(
                        worker_id, 
                        status, 
                        worker.current_model,
                        worker.vram_usage_mb
                    )
                
                self.logger.debug(f"Worker {worker_id} status updated to {status}")
            else:
                self.logger.warning(f"Attempted to update status for unknown worker {worker_id}")
                
        except Exception as e:
            self.logger.error(f"Failed to update worker {worker_id} status: {e}")
            
    def get_available_workers(self) -> List[str]:
        """Get list of available workers"""
        available = []
        try:
            current_time = time.time()
            
            for worker_id, worker in self.worker_status.items():
                # Check if worker is idle and recently active
                if (worker.status == "idle" and 
                    current_time - worker.last_activity < self.heartbeat_timeout):
                    available.append(worker_id)
                    
        except Exception as e:
            self.logger.error(f"Failed to get available workers: {e}")
            
        return available
        
    def find_optimal_worker(self, task, available_workers: List[str]) -> Optional[str]:
        """Find the best worker for a given task"""
        if not available_workers:
            return None
            
        try:
            # Simple strategy: pick the worker that was idle longest
            best_worker = None
            oldest_idle_time = float('inf')
            
            for worker_id in available_workers:
                worker = self.worker_status.get(worker_id)
                if worker and worker.status == "idle":
                    idle_time = time.time() - worker.last_activity
                    if idle_time < oldest_idle_time:
                        oldest_idle_time = idle_time
                        best_worker = worker_id
            
            return best_worker            
        except Exception as e:
            self.logger.error(f"Failed to find optimal worker: {e}")
            return available_workers[0] if available_workers else None
    
    def send_task_to_worker(self, task, worker_id: str) -> bool:
        """Send a task to a specific worker"""
        try:
            if worker_id not in self.worker_status:
                self.logger.error(f"Worker {worker_id} not found")
                return False
                
            worker = self.worker_status[worker_id]
            if worker.status != "idle":
                self.logger.warning(f"Worker {worker_id} is not idle (status: {worker.status})")
                return False
            
            # Use shared queue approach for direct communication
            return self.send_task_to_worker_via_queue(task, worker_id)
            
        except Exception as e:
            self.logger.error(f"Failed to send task to worker {worker_id}: {e}")
            return False
        
    def spawn_worker_process(self, device_id: int) -> bool:
        """Spawn a new worker process"""
        try:
            if device_id in self.worker_processes:
                if self.worker_processes[device_id].is_alive():
                    self.logger.warning(f"Worker process for device {device_id} already running")
                    return True
                else:
                    # Clean up dead process
                    self.worker_processes[device_id].join()
                    del self.worker_processes[device_id]
            
            # Create and start worker process using module-level wrapper
            worker_process = Process(
                target=worker_wrapper,
                args=(device_id, self.shared_queues),
                name=f"Worker-{device_id}"
            )
            
            worker_process.start()
            self.worker_processes[device_id] = worker_process
            
            self.logger.info(f"Worker process spawned for device {device_id}")
            
            # Add delay if specified
            if self.worker_spawn_delay > 0:
                time.sleep(self.worker_spawn_delay)
            
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to spawn worker for device {device_id}: {e}")
            return False
            
    def start_all_workers(self) -> bool:
        """Start all configured workers"""
        try:
            if not self.auto_start_workers:
                self.logger.info("Auto-start workers disabled")
                return True
            
            self.logger.info(f"Starting workers for devices: {self.device_list}")
            
            if self.parallel_worker_spawn:
                # Start all workers in parallel
                threads = []
                for device_id in self.device_list:
                    thread = threading.Thread(
                        target=self.spawn_worker_process,
                        args=(device_id,)
                    )
                    thread.start()
                    threads.append(thread)
                
                # Wait for all to complete
                for thread in threads:
                    thread.join()
            else:
                # Start workers sequentially
                for device_id in self.device_list:
                    self.spawn_worker_process(device_id)
            
            self.logger.info("All workers started")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to start all workers: {e}")
            return False
            
    def check_worker_health(self):
        """Check health of all workers"""
        try:
            current_time = time.time()
            stale_workers = []
            
            for worker_id, worker in self.worker_status.items():
                # Check if worker hasn't been active recently
                if current_time - worker.last_activity > self.heartbeat_timeout:
                    stale_workers.append(worker_id)
                    self.logger.warning(f"Worker {worker_id} appears stale (last activity: {worker.last_activity})")
            
            # Mark stale workers as offline
            for worker_id in stale_workers:
                self.update_worker_status(worker_id, "offline")
            
            # Check process health
            dead_processes = []
            for device_id, process in self.worker_processes.items():
                if not process.is_alive():
                    dead_processes.append(device_id)
                    self.logger.warning(f"Worker process for device {device_id} has died")
            
            # Clean up dead processes
            for device_id in dead_processes:
                try:
                    self.worker_processes[device_id].join(timeout=5)
                    del self.worker_processes[device_id]
                    
                    # Try to restart if auto-restart is enabled
                    if self.auto_start_workers:
                        self.logger.info(f"Attempting to restart worker for device {device_id}")
                        self.spawn_worker_process(device_id)
                        
                except Exception as e:
                    self.logger.error(f"Failed to clean up dead worker process for device {device_id}: {e}")
                    
        except Exception as e:
            self.logger.error(f"Worker health check failed: {e}")
            
    def start_health_monitoring(self):
        """Start health monitoring thread"""
        if self.monitoring_active:
            return
            
        self.monitoring_active = True
        self.health_check_thread = threading.Thread(target=self._health_monitoring_loop)
        self.health_check_thread.daemon = True
        self.health_check_thread.start()
        
        self.logger.info("Worker health monitoring started")
        
    def stop_health_monitoring(self):
        """Stop health monitoring"""
        self.monitoring_active = False
        if self.health_check_thread:
            self.health_check_thread.join(timeout=5)
        self.logger.info("Worker health monitoring stopped")
        
    def _health_monitoring_loop(self):
        """Health monitoring loop"""
        while self.monitoring_active:
            try:
                self.check_worker_health()
                time.sleep(self.heartbeat_timeout / 2)  # Check twice per timeout period
            except Exception as e:
                self.logger.error(f"Health monitoring loop error: {e}")
                time.sleep(5)  # Short delay before retrying
            
    def cleanup_worker(self, worker_id: str):
        """Clean up a worker and its resources"""
        try:
            # Remove from status tracking
            if worker_id in self.worker_status:
                worker = self.worker_status[worker_id]
                device_id = worker.device_id
                del self.worker_status[worker_id]
                
                # Clean up process if it exists
                if device_id in self.worker_processes:
                    process = self.worker_processes[device_id]
                    if process.is_alive():
                        process.terminate()
                        process.join(timeout=5)
                        if process.is_alive():
                            process.kill()
                    del self.worker_processes[device_id]
                
                self.logger.info(f"Worker {worker_id} cleaned up")
            else:
                self.logger.warning(f"Attempted to cleanup unknown worker {worker_id}")
                
        except Exception as e:
            self.logger.error(f"Failed to cleanup worker {worker_id}: {e}")
            
    def stop_all_workers(self):
        """Stop all worker processes"""
        try:
            self.logger.info("Stopping all workers...")
            
            # Stop health monitoring first
            self.stop_health_monitoring()
            
            # Terminate all processes
            for device_id, process in self.worker_processes.items():
                try:
                    if process.is_alive():
                        process.terminate()
                        process.join(timeout=5)
                        if process.is_alive():
                            process.kill()
                            process.join()
                except Exception as e:
                    self.logger.error(f"Failed to stop worker process for device {device_id}: {e}")
            
            # Clear all tracking
            self.worker_processes.clear()
            self.worker_status.clear()
            
            self.logger.info("All workers stopped")
            
        except Exception as e:
            self.logger.error(f"Failed to stop all workers: {e}")
            
    def get_worker_statistics(self) -> Dict[str, Any]:
        """Get worker manager statistics"""
        try:
            stats = {
                'total_workers': len(self.worker_status),
                'active_processes': len(self.worker_processes),
                'status_breakdown': {},
                'device_usage': {}
            }
            
            # Count by status
            for worker in self.worker_status.values():
                status = worker.status
                stats['status_breakdown'][status] = stats['status_breakdown'].get(status, 0) + 1
                
                # Track device usage
                device_id = worker.device_id
                if device_id not in stats['device_usage']:
                    stats['device_usage'][device_id] = {
                        'worker_id': worker.worker_id,
                        'status': status,
                        'current_task': worker.current_task,
                        'vram_usage_mb': worker.vram_usage_mb
                    }
            
            return stats
            
        except Exception as e:
            self.logger.error(f"Failed to get worker statistics: {e}")
            return {}
    
    def process_shared_queue_messages(self):
        """Process messages from workers via shared queues"""
        try:
            # Process status queue messages
            status_queue = self.shared_queues.get('status_queue')
            if status_queue:
                processed_count = 0
                while processed_count < 10:  # Limit to prevent blocking
                    try:
                        message = status_queue.get_nowait()
                        self._handle_worker_message(message)
                        processed_count += 1
                    except:
                        break
            
            # Process result queue messages
            result_queue = self.shared_queues.get('result_queue')
            if result_queue:
                processed_count = 0
                while processed_count < 10:  # Limit to prevent blocking
                    try:
                        message = result_queue.get_nowait()
                        self._handle_worker_result(message)
                        processed_count += 1
                    except:
                        break
                        
        except Exception as e:
            self.logger.error(f"Error processing shared queue messages: {e}")
    
    def _handle_worker_message(self, message: Dict[str, Any]):
        """Handle a message from a worker"""
        try:
            worker_id = message.get('worker_id')
            message_type = message.get('type', message.get('message_type'))
            
            if not worker_id:
                self.logger.warning(f"Message without worker_id: {message}")
                return
            
            if message_type == 'registration':
                self._handle_worker_registration(message)
            elif message_type == 'status':
                self._handle_worker_status_update(worker_id, message.get('status', {}))
            elif message_type == 'heartbeat':
                self._handle_worker_heartbeat(message)
            elif message_type == 'error':
                self._handle_worker_error(message)
            elif message_type == 'disconnect':
                self._handle_worker_disconnect(message)
            else:
                self.logger.debug(f"Unknown message type from {worker_id}: {message_type}")
                
        except Exception as e:
            self.logger.error(f"Error handling worker message: {e}")
    
    def _handle_worker_registration(self, message: Dict[str, Any]):
        """Handle worker registration message"""
        try:
            worker_id = message.get('worker_id')
            capabilities = message.get('capabilities', {})
            
            if not worker_id:
                self.logger.error("Worker registration without worker_id")
                return
            
            # Extract device_id from worker_id (format: worker_X)
            try:
                device_id = int(worker_id.split('_')[1])
            except (IndexError, ValueError):
                self.logger.error(f"Cannot extract device_id from worker_id: {worker_id}")
                return
            
            # Register the worker
            success = self.register_worker(worker_id, device_id, capabilities)
            if success:
                self.logger.info(f"Worker {worker_id} registered successfully via shared queue")
            else:
                self.logger.error(f"Failed to register worker {worker_id}")
                
        except Exception as e:
            self.logger.error(f"Error handling worker registration: {e}")
      
    def _handle_worker_heartbeat(self, message: Dict[str, Any]):
        """Handle worker heartbeat message"""
        try:
            worker_id = message.get('worker_id')
            
            if worker_id in self.worker_status:
                self.worker_status[worker_id].last_activity = time.time()
                self.logger.debug(f"Heartbeat received from worker {worker_id}")
            else:
                self.logger.warning(f"Heartbeat from unregistered worker: {worker_id}")
                
        except Exception as e:            self.logger.error(f"Error handling worker heartbeat: {e}")
    
    def _handle_worker_error(self, message: Dict[str, Any]):
        """Handle worker error message"""
        try:
            worker_id = message.get('worker_id')
            error = message.get('error', 'Unknown error')
            
            self.logger.error(f"Worker {worker_id} reported error: {error}")
              # Update worker status to error
            if worker_id in self.worker_status:
                self.worker_status[worker_id].status = "error"
                
        except Exception as e:
            self.logger.error(f"Error handling worker error: {e}")
    
    def _handle_worker_disconnect(self, message: Dict[str, Any]):
        """Handle worker disconnect message"""
        try:
            worker_id = message.get('worker_id')
            reason = message.get('reason', 'Unknown')
            
            if not worker_id:
                self.logger.error("Worker disconnect without worker_id")
                return
            
            self.logger.info(f"Worker {worker_id} disconnected: {reason}")
            
            # Clean up the worker
            self.cleanup_worker(worker_id)
            
        except Exception as e:
            self.logger.error(f"Error handling worker disconnect: {e}")
    
    def _handle_worker_result(self, message: Dict[str, Any]):
        """Handle worker result and status messages with centralized database updates"""
        try:
            worker_id = message.get('worker_id')
            message_type = message.get('message_type', 'result')
            
            if not worker_id:
                self.logger.warning(f"Message without worker_id: {message}")
                return
            
            # Handle different message types from workers
            if message_type == 'status':
                self._handle_worker_status_update(worker_id, message.get('status', {}))
            elif message_type == 'result':
                self._handle_worker_task_result(worker_id, message.get('result', {}))
            else:
                self.logger.warning(f"Unknown message type from worker {worker_id}: {message_type}")
                
        except Exception as e:
            self.logger.error(f"Error handling worker message: {e}")
    
    def _handle_worker_status_update(self, worker_id: str, status_data: Dict[str, Any]):
        """Handle worker status update messages"""
        try:
            status = status_data.get('status')
            task_id = status_data.get('task_id')
            
            self.logger.info(f"Status update from worker {worker_id}: {status} for task {task_id}")
              # Update worker status in memory
            if worker_id in self.worker_status:
                worker = self.worker_status[worker_id]
                
                if status == 'accepted':
                    # Worker accepted the task
                    worker.status = "busy"
                    worker.current_task = task_id
                    # Note: task status remains 'assigned' until 'processing_started'
                    
                elif status == 'processing_started':
                    # Worker started processing the task
                    worker.status = "busy"
                    worker.current_task = task_id
                    # Update task status to 'processing' in database
                    if self.database and task_id:
                        self.database.update_task_status(task_id, 'processing', worker_id)
                        
                elif status == 'completed':
                    # Worker completed the task successfully
                    worker.status = "busy"  # Still busy until ready status
                    # Update task status to 'completed' in database
                    if self.database and task_id:
                        self.database.update_task_status(task_id, 'completed', worker_id)
                    self.logger.info(f"Task {task_id} completed by worker {worker_id}")
                        
                elif status == 'ready':
                    # Worker completed cleanup and is ready for new tasks
                    worker.status = "idle"
                    worker.current_task = None
                    worker.current_model = None
                    worker.vram_usage_mb = 0
                    
                elif status == 'error':
                    # Worker encountered an error
                    worker.status = "error"
                    error_msg = status_data.get('error', 'Unknown error')
                    self.logger.error(f"Worker {worker_id} error: {error_msg}")
                    if self.database and task_id:
                        self.database.update_task_status(task_id, 'failed', worker_id, 
                                                       error_message=error_msg)
                    
                worker.last_activity = time.time()
                
                # Update worker status in database
                if self.database and self.database.connected:
                    self.database.update_worker_status(
                        worker_id, 
                        worker.status, 
                        worker.current_model,
                        worker.vram_usage_mb
                    )
                    
        except Exception as e:
            self.logger.error(f"Error handling worker status update: {e}")
    
    def _handle_worker_task_result(self, worker_id: str, result_data: Dict[str, Any]):
        """Handle worker task completion results"""
        try:
            task_id = result_data.get('task_id')
            success = result_data.get('success', False)
            output_path = result_data.get('output_path')
            error_message = result_data.get('error')
            processing_time = result_data.get('processing_time')
            
            self.logger.info(f"Task result from worker {worker_id}: {task_id} - {'SUCCESS' if success else 'FAILED'}")
            
            # Update task status in database
            if self.database and task_id:
                if success:
                    self.database.update_task_status(
                        task_id, 'completed', worker_id,
                        output_path=output_path,
                        processing_time=processing_time
                    )
                else:
                    self.database.update_task_status(
                        task_id, 'failed', worker_id,
                        error_message=error_message
                    )
                    
            # Note: Worker status update to 'ready' should come as a separate status message
            # This ensures proper cleanup completion before accepting new tasks
            
        except Exception as e:
            self.logger.error(f"Error handling worker task result: {e}")

    def send_task_to_worker_via_queue(self, task, worker_id: str) -> bool:
        """Send a task to a worker via shared queues"""
        try:
            if worker_id not in self.worker_status:
                self.logger.error(f"Cannot send task to unregistered worker: {worker_id}")
                return False
                
            worker = self.worker_status[worker_id]
            if worker.status != "idle":
                self.logger.warning(f"Worker {worker_id} is not idle (status: {worker.status})")
                return False
              # Debug: Log the task object we're trying to send
            self.logger.debug(f"Task object: {task}")
            self.logger.debug(f"Task type: {type(task)}")
            
            # Get task config
            task_config = task.to_dict() if hasattr(task, 'to_dict') else task
            task_id = getattr(task, 'task_id', task.get('task_id') if isinstance(task, dict) else 'unknown')
              # Look up model information from database
            model_name = task_config.get('model_name')
            if model_name:
                try:
                    self.logger.info(f"Looking up model info for: {model_name}")
                    model_info = self.database.get_model_info(model_name)
                    self.logger.info(f"Database returned model info: {model_info}")
                    if model_info:
                        self.logger.info(f"Found model info for {model_name}: {model_info}")
                        # Convert relative path to absolute path
                        model_path = model_info.get('model_path', '')
                        if model_path and not os.path.isabs(model_path):
                            # Convert relative to absolute path from project root
                            project_root = Path(__file__).parent.parent
                            absolute_path = project_root / model_path
                            model_info['model_path'] = str(absolute_path)
                            self.logger.info(f"Converted relative path to absolute: {absolute_path}")
                        # Add model info to task config
                        task_config['model'] = model_info
                    else:
                        self.logger.warning(f"No model info found for {model_name}")
                except Exception as e:
                    self.logger.error(f"Failed to get model info for {model_name}: {e}")
            else:
                self.logger.warning("No model_name found in task_config")
            
            # Prepare task instruction
            task_instruction = {
                'type': 'instruction',
                'worker_id': worker_id,
                'timestamp': time.time(),
                'data': {
                    'action': 'run_task',
                    'task_config': task_config,
                    'task_id': task_id
                }
            }
              # Send via worker-specific instruction queue
            worker_queue_key = f"instruction_queue_{worker_id}"
            instruction_queue = self.shared_queues.get(worker_queue_key)
            if instruction_queue:
                instruction_queue.put(task_instruction)
                
                # Update worker status
                self.update_worker_status(worker_id, "busy", task_instruction['data']['task_id'])
                
                self.logger.info(f"Task {task_instruction['data']['task_id']} sent to worker {worker_id} via worker-specific queue")
                return True
            else:
                self.logger.error(f"Worker-specific instruction queue not available for {worker_id}")
                return False
            
        except Exception as e:
            self.logger.error(f"Failed to send task to worker {worker_id} via queue: {e}")
            return False
