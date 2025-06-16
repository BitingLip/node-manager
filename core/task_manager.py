#!/usr/bin/env python3
"""
Task Manager - Handles task submission, queuing, assignment, and completion
"""
import time
import uuid
import queue
import threading
import random
import json
from typing import Dict, List, Optional, Any
from dataclasses import dataclass, asdict
from datetime import datetime, timedelta

@dataclass
class TaskConfig:
    """Configuration for a generation task with advanced VRAM optimization options"""
    prompt: str
    negative_prompt: str = "blurry, low quality, distorted"
    width: int = 832
    height: int = 1216
    steps: int = 15
    guidance_scale: float = 7.0
    seed: Optional[int] = None
    task_id: str = ""
    timestamp: float = 0.0
    model_name: str = "cyberrealistic_pony_v110"
    
    # Advanced VRAM Optimization Options
    use_sequential_cpu_offload: bool = True
    use_model_cpu_offload: bool = False
    use_attention_slicing: bool = True
    use_vae_slicing: bool = True
    use_torch_compile: bool = False
    use_channels_last: bool = True
    use_reduced_precision: bool = True
    enable_tiling: bool = True
    enable_freeu: bool = False
    batch_size: int = 1
    
    def __post_init__(self):
        """Set default values after initialization"""
        if not self.task_id:
            self.task_id = str(uuid.uuid4())
        if self.timestamp == 0.0:
            self.timestamp = time.time()
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization"""
        return asdict(self)


class TaskManager:
    """Manages task submission, queuing, assignment, and completion"""
    
    def __init__(self, database, logger, config: Dict):
        self.database = database
        self.logger = logger
        self.task_queue: queue.Queue = queue.Queue()
        self.active_tasks: Dict[str, Dict[str, Any]] = {}
        self.completed_tasks: Dict[str, Dict] = {}
        self.task_timeout = config.get("task_timeout", 300)
        self.scheduler_interval = config.get("scheduler_interval", 0.1)
        
        # Database monitoring
        self.db_monitoring_active = False
        self.db_monitor_thread = None
        self.db_check_interval = 5  # Check database every 5 seconds
        
        self.logger.info("TaskManager initialized")
        
    def submit_task(self, task_config: TaskConfig) -> str:
        """Submit a new task for processing with improved ID management"""
        try:
            # Generate unique task ID if not provided or if duplicate exists
            original_task_id = task_config.task_id
            if not original_task_id:
                task_config.task_id = f"task_{int(time.time())}_{random.randint(1000, 9999)}"
            
            # Ensure timestamp is set
            if task_config.timestamp == 0.0:
                task_config.timestamp = time.time()
            
            # Check for existing task ID and handle duplicates
            attempts = 0
            while attempts < 5:  # Prevent infinite loop
                try:
                    # Store task in database first
                    if self.database and self.database.connected:
                        success = self.database.create_task_record(task_config.to_dict())
                        if success:
                            self.logger.info(f"Task {task_config.task_id} stored in database")
                            break
                        else:
                            # If storage failed, it might be a duplicate
                            timestamp = int(time.time() * 1000)
                            task_config.task_id = f"{original_task_id}_{timestamp}_{attempts}"
                            attempts += 1
                            self.logger.warning(f"Task storage failed, retrying with: {task_config.task_id}")
                            continue
                    else:
                        # No database, just break and continue with in-memory processing
                        break
                        
                except Exception as e:
                    if "duplicate key" in str(e).lower():
                        # Handle duplicate by modifying task ID
                        timestamp = int(time.time() * 1000)
                        task_config.task_id = f"{original_task_id}_{timestamp}_{attempts}"
                        attempts += 1
                        self.logger.warning(f"Duplicate task ID detected, retrying with: {task_config.task_id}")
                    else:
                        raise e
            
            if attempts >= 5:
                raise Exception(f"Failed to create unique task ID after {attempts} attempts")
            
            # Add to queue
            self.task_queue.put(task_config)
            
            # Track as pending
            self.active_tasks[task_config.task_id] = {
                'config': task_config,
                'status': 'queued',
                'submitted_at': task_config.timestamp,
                'worker_id': None,
                'started_at': None,
                'completed_at': None
            }
            
            self.logger.info(f"Task {task_config.task_id} submitted successfully")
            return task_config.task_id
            
        except Exception as e:
            self.logger.error(f"Failed to submit task: {e}")
            return ""
        
    def get_task_status(self, task_id: str) -> Optional[Dict]:
        """Get status of a specific task"""
        if task_id in self.active_tasks:
            return self.active_tasks[task_id]
        elif task_id in self.completed_tasks:
            return self.completed_tasks[task_id]
        else:
            # Check database
            if self.database and self.database.connected:
                try:
                    db_tasks = self.database.get_task_status(task_id)
                    if db_tasks:
                        return db_tasks[0]
                except Exception as e:
                    self.logger.error(f"Database query failed: {e}")
            return None
        
    def cancel_task(self, task_id: str) -> bool:
        """Cancel a pending or active task"""
        try:
            if task_id in self.active_tasks:
                task_info = self.active_tasks[task_id]
                
                if task_info['status'] == 'queued':
                    # Remove from queue (this is tricky with queue.Queue)
                    # For now, mark as cancelled
                    task_info['status'] = 'cancelled'
                    task_info['completed_at'] = time.time()
                    
                    # Move to completed
                    self.completed_tasks[task_id] = task_info
                    del self.active_tasks[task_id]
                    
                    # Update database
                    if self.database and self.database.connected:
                        self.database.update_task_status(task_id, 'cancelled')
                    
                    self.logger.info(f"Task {task_id} cancelled")
                    return True
                else:
                    self.logger.warning(f"Cannot cancel task {task_id} in status {task_info['status']}")
                    return False
            else:
                self.logger.warning(f"Task {task_id} not found for cancellation")
                return False
                
        except Exception as e:
            self.logger.error(f"Failed to cancel task {task_id}: {e}")
            return False
        
    def assign_task_to_worker(self, task: TaskConfig, worker_id: str) -> bool:
        """Assign a task to a specific worker"""
        try:
            if task.task_id in self.active_tasks:
                task_info = self.active_tasks[task.task_id]
                task_info['status'] = 'assigned'
                task_info['worker_id'] = worker_id
                task_info['started_at'] = time.time()
                
                # Update database
                if self.database and self.database.connected:
                    self.database.update_task_status(task.task_id, 'assigned', worker_id)
                
                self.logger.info(f"Task {task.task_id} assigned to worker {worker_id}")
                return True
            else:
                self.logger.error(f"Task {task.task_id} not found for assignment")
                return False
                
        except Exception as e:
            self.logger.error(f"Failed to assign task {task.task_id} to worker {worker_id}: {e}")
            return False
        
    def handle_task_completion(self, task_id: str, result: Dict, worker_id: str):
        """Handle task completion from worker"""
        try:
            if task_id in self.active_tasks:
                task_info = self.active_tasks[task_id]
                task_info['status'] = 'completed' if result.get('success') else 'failed'
                task_info['completed_at'] = time.time()
                task_info['result'] = result
                
                # Calculate processing time
                if task_info.get('started_at'):
                    processing_time = task_info['completed_at'] - task_info['started_at']
                    task_info['processing_time'] = processing_time
                else:
                    processing_time = None
                
                # Move to completed
                self.completed_tasks[task_id] = task_info
                del self.active_tasks[task_id]
                
                # Update database
                if self.database and self.database.connected:
                    self.database.update_task_status(
                        task_id, 
                        task_info['status'], 
                        worker_id,
                        result.get('output_path'),
                        result.get('error'),
                        processing_time
                    )
                
                self.logger.info(f"Task {task_id} completed by worker {worker_id}")
            else:
                self.logger.warning(f"Received completion for unknown task {task_id}")
                
        except Exception as e:
            self.logger.error(f"Failed to handle task completion for {task_id}: {e}")
                
    def get_pending_tasks(self) -> List[TaskConfig]:
        """Get list of pending tasks"""
        pending_tasks = []
        try:
            # Create a temporary list to avoid modifying queue during iteration
            temp_items = []
            while not self.task_queue.empty():
                try:
                    task = self.task_queue.get_nowait()
                    temp_items.append(task)
                    pending_tasks.append(task)
                except queue.Empty:
                    break
            
            # Put items back in queue
            for task in temp_items:
                self.task_queue.put(task)
                
        except Exception as e:
            self.logger.error(f"Failed to get pending tasks: {e}")
            
        return pending_tasks
        
    def cleanup_old_tasks(self, max_age_hours: int = 24):
        """Clean up old completed tasks"""
        try:
            current_time = time.time()
            cutoff_time = current_time - (max_age_hours * 3600)
            
            # Clean up completed tasks
            to_remove = []
            for task_id, task_info in self.completed_tasks.items():
                if task_info.get('completed_at', 0) < cutoff_time:
                    to_remove.append(task_id)
            
            for task_id in to_remove:
                del self.completed_tasks[task_id]
                self.logger.debug(f"Cleaned up old task {task_id}")
              # Clean up database records
            if self.database and self.database.connected:
                self.database.cleanup_old_records(max_age_hours // 24)
            
            if to_remove:
                self.logger.info(f"Cleaned up {len(to_remove)} old tasks")
                
        except Exception as e:
            self.logger.error(f"Failed to cleanup old tasks: {e}")
    
    def get_next_task(self) -> Optional[TaskConfig]:
        """Get the next task from the queue"""
        try:
            task = self.task_queue.get_nowait()
            
            # Add to active tasks tracking
            if task and task.task_id not in self.active_tasks:
                self.active_tasks[task.task_id] = {
                    'task': task,
                    'status': 'queued',
                    'worker_id': None,
                    'created_at': time.time(),
                    'started_at': None,
                    'completed_at': None,
                    'result': None
                }
                self.logger.debug(f"Task {task.task_id} added to active tracking")
            
            return task
        except queue.Empty:
            return None
    
    def get_statistics(self) -> Dict[str, Any]:
        """Get task manager statistics"""
        return {
            'queued_tasks': self.task_queue.qsize(),
            'active_tasks': len(self.active_tasks),
            'completed_tasks': len(self.completed_tasks),
            'total_processed': len(self.completed_tasks)
        }
    
    def start_database_monitoring(self):
        """Start monitoring database for new tasks"""
        if self.db_monitoring_active:
            return
            
        self.db_monitoring_active = True
        self.db_monitor_thread = threading.Thread(target=self._database_monitoring_loop)
        self.db_monitor_thread.daemon = True
        self.db_monitor_thread.start()
        
        self.logger.info("Database task monitoring started")
    
    def stop_database_monitoring(self):
        """Stop database monitoring"""
        self.db_monitoring_active = False
        if self.db_monitor_thread:
            self.db_monitor_thread.join(timeout=10)
        self.logger.info("Database task monitoring stopped")
    
    def _database_monitoring_loop(self):
        """Monitor database for pending tasks"""
        while self.db_monitoring_active:
            try:
                # Get pending tasks from database
                pending_tasks = self._get_pending_tasks_from_db()
                
                for task_data in pending_tasks:
                    try:
                        # Convert to TaskConfig and process
                        task_config = self._create_task_config_from_db(task_data)
                        
                        # Check if already in processing queue or active
                        if not self._is_task_in_queue(task_config.task_id):
                            self.task_queue.put(task_config)
                            self.logger.info(f"Added database task {task_config.task_id} to processing queue")
                            
                            # Mark as assigned in database
                            if self.database:
                                self.database.update_task_status(task_config.task_id, "assigned")
                                
                    except Exception as e:
                        self.logger.error(f"Failed to process database task {task_data.get('task_id', 'unknown')}: {e}")
                
                time.sleep(self.db_check_interval)
                
            except Exception as e:
                self.logger.error(f"Database monitoring loop error: {e}")
                time.sleep(self.db_check_interval)
    
    def _get_pending_tasks_from_db(self) -> List[Dict[str, Any]]:
        """Get pending tasks from database"""
        if not self.database or not self.database.connected:
            return []
            
        try:
            # Query for tasks with status 'queued' or 'submitted'
            return self.database.get_pending_tasks()
        except Exception as e:
            self.logger.error(f"Failed to get pending tasks: {e}")
            return []
    
    def _create_task_config_from_db(self, task_data: Dict[str, Any]) -> TaskConfig:
        """Convert database task record to TaskConfig"""
        return TaskConfig(
            task_id=task_data['task_id'],
            prompt=task_data.get('prompt', ''),
            negative_prompt=task_data.get('negative_prompt', ''),
            width=task_data.get('width', 832),
            height=task_data.get('height', 1216),
            steps=task_data.get('steps', 15),
            guidance_scale=task_data.get('guidance_scale', 7.0),
            seed=task_data.get('seed'),
            model_name=task_data.get('model_name', 'cyberrealistic_pony_v110'),
            timestamp=task_data.get('submit_time', time.time()).timestamp() if hasattr(task_data.get('submit_time', time.time()), 'timestamp') else time.time()
        )
    
    def _is_task_in_queue(self, task_id: str) -> bool:
        """Check if task is already in processing queue or active"""
        # Check if already in active tasks
        if task_id in self.active_tasks:
            return True
            
        # For a more thorough check, we could iterate through the queue
        # but that's expensive. For now, check active tasks only.
        return False
