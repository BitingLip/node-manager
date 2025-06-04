"""
Task Dispatcher
Handles incoming tasks from cluster manager and routes them to appropriate workers
Manages task queue, prioritization, and execution coordination
"""

import asyncio
import logging
from typing import Dict, List, Optional, Any, Callable
from datetime import datetime, timedelta
from enum import Enum
import uuid
import structlog

logger = structlog.get_logger(__name__)


class TaskStatus(Enum):
    """Task execution status"""
    QUEUED = "queued"
    ASSIGNED = "assigned"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


class TaskPriority(Enum):
    """Task priority levels"""
    LOW = 1
    NORMAL = 2
    HIGH = 3
    URGENT = 4


class TaskInfo:
    """Information about a task"""
    
    def __init__(self, task_id: str, task_type: str, task_data: Dict[str, Any], priority: TaskPriority = TaskPriority.NORMAL):
        self.task_id = task_id
        self.task_type = task_type
        self.task_data = task_data
        self.priority = priority
        self.status = TaskStatus.QUEUED
        self.assigned_worker = None
        self.created_at = datetime.now()
        self.started_at: Optional[datetime] = None
        self.completed_at: Optional[datetime] = None
        self.result = None
        self.error = None
        self.retry_count = 0


class TaskDispatcher:
    """
    Manages task queue and dispatches tasks to appropriate workers
    Handles task prioritization, routing, and execution coordination
    """
    
    def __init__(self, worker_manager=None, node_id=None, database=None):
        """Initialize task dispatcher"""
        self.worker_manager = worker_manager
        self.node_id = node_id or f"node-{uuid.uuid4().hex[:8]}"
        self.database = database
        self.task_queue = asyncio.PriorityQueue()
        self.active_tasks = {}  # task_id -> TaskInfo
        self.completed_tasks = {}  # task_id -> TaskInfo
        
        # Configuration
        self.max_retries = 3
        self.task_timeout = 300  # 5 minutes default
        
        # Callbacks
        self.task_callbacks = {}  # task_id -> callback function
        
        logger.info(f"TaskDispatcher initialized for node {self.node_id}")
    
    def start(self):
        """Start the task dispatcher"""
        logger.info(f"TaskDispatcher started for node {self.node_id}")
        # Start background task processing if needed
        return True
    
    def stop(self):
        """Stop the task dispatcher"""
        logger.info(f"TaskDispatcher stopping for node {self.node_id}")
        # Clean up resources, stop background tasks
        return True
    
    async def receive_task(self, task_data: Dict[str, Any]) -> str:
        """Receive a new task from cluster manager"""
        try:
            # 1. Parse task data
            task_type = task_data.get('type', 'unknown')
            priority_str = task_data.get('priority', 'normal')
            
            # 2. Validate task format
            if not task_type or 'data' not in task_data:
                raise ValueError("Invalid task format: missing type or data")
            
            # 3. Generate task ID
            if 'task_id' not in task_data:
                task_data['task_id'] = f"task-{uuid.uuid4().hex[:12]}"
            
            task_id = task_data['task_id']
            
            # Map priority string to enum
            priority_map = {
                'low': TaskPriority.LOW,
                'normal': TaskPriority.NORMAL,
                'high': TaskPriority.HIGH,
                'urgent': TaskPriority.URGENT
            }
            priority = priority_map.get(priority_str.lower(), TaskPriority.NORMAL)
            
            # 4. Create TaskInfo
            task_info = TaskInfo(task_id, task_type, task_data, priority)
            
            # 5. Add to queue (priority queue uses negative priority for max-heap behavior)
            await self.task_queue.put((-priority.value, task_info.created_at.timestamp(), task_info))
            
            # Store in active tasks
            self.active_tasks[task_id] = task_info
            
            logger.info(f"Received task {task_id} (type: {task_type}, priority: {priority_str})")
            
            # 6. Return task ID
            return task_id
            
        except Exception as e:
            logger.error(f"Failed to receive task: {e}")
            raise
    
    async def dispatch_task(self, task_info: TaskInfo) -> bool:
        """Dispatch a task to an appropriate worker"""
        try:
            if not self.worker_manager:
                logger.error("No worker manager available for task dispatch")
                return False
            
            # 1. Find suitable worker based on task type
            required_worker_type = self._get_worker_type_for_task(task_info.task_type)
            available_workers = self.worker_manager.get_available_workers(required_worker_type)
            
            # 2. Check worker availability
            if not available_workers:
                logger.warning(f"No available workers for task {task_info.task_id} (type: {task_info.task_type})")
                return False
            
            # Select the best worker (simple: first available)
            selected_worker = available_workers[0]
            
            # 3. Assign task to worker
            success = self.worker_manager.assign_task_to_worker(
                selected_worker.worker_id, 
                task_info.task_data
            )
            
            if success:
                # 4. Update task status                task_info.status = TaskStatus.ASSIGNED
                task_info.assigned_worker = selected_worker.worker_id
                task_info.started_at = datetime.now()
                logger.info(f"Dispatched task {task_info.task_id} to worker {selected_worker.worker_id}")
                
                # 5. Start execution monitoring (placeholder - would set up monitoring)
                return True
            else:
                logger.error(f"Failed to assign task {task_info.task_id} to worker {selected_worker.worker_id}")
                return False
        
        except Exception as e:
            logger.error(f"Failed to dispatch task {task_info.task_id}: {e}")
            return False
    
    def _get_worker_type_for_task(self, task_type: str):
        """Map task type to required worker type"""
        from .worker_manager import WorkerType
        
        task_type_mapping = {
            'text_generation': WorkerType.LLM,
            'chat': WorkerType.LLM,
            'completion': WorkerType.LLM,
            'image_generation': WorkerType.STABLE_DIFFUSION,
            'txt2img': WorkerType.STABLE_DIFFUSION,
            'img2img': WorkerType.STABLE_DIFFUSION,
            'text_to_speech': WorkerType.TTS,
            'tts': WorkerType.TTS,
            'image_to_text': WorkerType.IMAGE_TO_TEXT,
            'ocr': WorkerType.IMAGE_TO_TEXT,
            'image_captioning': WorkerType.IMAGE_TO_TEXT
        }
        
        return task_type_mapping.get(task_type.lower(), WorkerType.GENERIC)
    async def process_task_queue(self):
        """Main task processing loop"""
        while True:
            try:
                # 1. Get next task from queue
                priority, timestamp, task_info = await self.task_queue.get()
                
                # 2. Find appropriate worker and dispatch task
                success = await self.dispatch_task(task_info)
                
                if not success:
                    # 3. Handle dispatch failure
                    logger.warning(f"Failed to dispatch task {task_info.task_id}, requeueing")
                    # Re-queue with lower priority to prevent busy loop
                    await asyncio.sleep(1)
                    await self.task_queue.put((priority + 1, timestamp, task_info))
                
                # Mark queue task as done
                self.task_queue.task_done()
                
            except Exception as e:
                logger.error(f"Error in task queue processing: {e}")
                await asyncio.sleep(1)  # Prevent busy loop on error
    
    def cancel_task(self, task_id: str) -> bool:
        """Cancel a task"""
        try:
            if task_id not in self.active_tasks:
                logger.warning(f"Task {task_id} not found")
                return False
            
            task_info = self.active_tasks[task_id]
            
            # 1. Check if task can be cancelled
            if task_info.status in [TaskStatus.COMPLETED, TaskStatus.FAILED, TaskStatus.CANCELLED]:
                logger.warning(f"Task {task_id} cannot be cancelled (status: {task_info.status})")
                return False
            
            # 2. Signal worker to stop if task is assigned
            if task_info.assigned_worker and self.worker_manager:
                # Signal worker to cancel task (placeholder implementation)
                logger.info(f"Signaling worker {task_info.assigned_worker} to cancel task {task_id}")
            
            # 3. Update task status
            task_info.status = TaskStatus.CANCELLED
            task_info.completed_at = datetime.now()
            
            # 4. Release resources
            if task_info.assigned_worker and self.worker_manager:
                # Free up the worker
                self.worker_manager.complete_task(task_info.assigned_worker, task_id)
            
            # Move to completed tasks
            self.completed_tasks[task_id] = task_info
            del self.active_tasks[task_id]
            
            logger.info(f"Cancelled task {task_id}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to cancel task {task_id}: {e}")
            return False
    
    def get_task_status(self, task_id: str) -> Optional[TaskInfo]:
        """Get status of a specific task"""
        # Check active tasks first
        if task_id in self.active_tasks:
            return self.active_tasks[task_id]
        
        # Check completed tasks
        if task_id in self.completed_tasks:
            return self.completed_tasks[task_id]
        
        return None
    
    def get_queue_status(self) -> Dict[str, Any]:
        """Get current queue status"""
        try:
            # Count tasks by priority in queue
            priority_counts = {
                'urgent': 0,
                'high': 0,
                'normal': 0,
                'low': 0
            }
            
            # Count active tasks by status
            status_counts = {
                'queued': 0,
                'assigned': 0,
                'running': 0,
                'completed': len(self.completed_tasks),
                'failed': 0,
                'cancelled': 0
            }
            
            for task_info in self.active_tasks.values():
                status_counts[task_info.status.value] = status_counts.get(task_info.status.value, 0) + 1
                
                if task_info.status == TaskStatus.QUEUED:
                    priority_counts[task_info.priority.name.lower()] += 1
            
            # Calculate average wait time (simplified)
            total_wait_time = 0
            wait_time_count = 0
            
            for task_info in self.active_tasks.values():
                if task_info.started_at:
                    wait_time = (task_info.started_at - task_info.created_at).total_seconds()
                    total_wait_time += wait_time
                    wait_time_count += 1
            
            avg_wait_time = total_wait_time / wait_time_count if wait_time_count > 0 else 0
            
            # Worker utilization
            worker_utilization = 0
            if self.worker_manager:
                worker_stats = self.worker_manager.get_worker_stats()
                total_workers = worker_stats.get('total_workers', 0)
                busy_workers = worker_stats.get('by_status', {}).get('busy', 0)
                worker_utilization = (busy_workers / total_workers * 100) if total_workers > 0 else 0
            
            return {
                'queue_length': self.task_queue.qsize(),
                'priority_counts': priority_counts,
                'status_counts': status_counts,
                'average_wait_time_seconds': avg_wait_time,
                'worker_utilization_percent': worker_utilization,
                'active_tasks': len(self.active_tasks),
                'completed_tasks': len(self.completed_tasks)
            }
            
        except Exception as e:
            logger.error(f"Failed to get queue status: {e}")
            return {
                'error': str(e),
                'queue_length': 0,
                'active_tasks': 0,
                'completed_tasks': 0
            }
    
    async def handle_task_completion(self, task_id: str, result: Any):
        """Handle task completion"""
        try:
            if task_id not in self.active_tasks:
                logger.warning(f"Task {task_id} not found in active tasks")
                return
            
            task_info = self.active_tasks[task_id]
            
            # 1. Update task status
            task_info.status = TaskStatus.COMPLETED
            task_info.completed_at = datetime.now()
            task_info.result = result
            
            # 2. Release worker
            if task_info.assigned_worker and self.worker_manager:
                self.worker_manager.complete_task(task_info.assigned_worker, task_id)
            
            # 3. Call completion callback
            if task_id in self.task_callbacks:
                try:
                    callback = self.task_callbacks[task_id]
                    callback(task_info)
                    del self.task_callbacks[task_id]
                except Exception as e:
                    logger.error(f"Error in task completion callback: {e}")
            
            # 4. Move to completed tasks
            self.completed_tasks[task_id] = task_info
            del self.active_tasks[task_id]
            
            logger.info(f"Task {task_id} completed successfully")
            
        except Exception as e:
            logger.error(f"Failed to handle task completion for {task_id}: {e}")
    
    async def handle_task_failure(self, task_id: str, error: Exception):
        """Handle task failure"""
        try:
            if task_id not in self.active_tasks:
                logger.warning(f"Task {task_id} not found in active tasks")
                return
            
            task_info = self.active_tasks[task_id]
            task_info.error = str(error)
            
            # 1. Check retry count
            if task_info.retry_count < self.max_retries:
                # 2. Retry task
                task_info.retry_count += 1
                task_info.status = TaskStatus.QUEUED
                task_info.assigned_worker = None
                
                logger.info(f"Retrying task {task_id} (attempt {task_info.retry_count + 1}/{self.max_retries + 1})")
                
                # Re-queue with same priority
                await self.task_queue.put((-task_info.priority.value, task_info.created_at.timestamp(), task_info))
            else:
                # 3. Mark as failed
                task_info.status = TaskStatus.FAILED
                task_info.completed_at = datetime.now()
                
                # 4. Release worker
                if task_info.assigned_worker and self.worker_manager:
                    self.worker_manager.complete_task(task_info.assigned_worker, task_id)
                
                # Move to completed tasks
                self.completed_tasks[task_id] = task_info
                del self.active_tasks[task_id]
                
                logger.error(f"Task {task_id} failed after {self.max_retries} retries: {error}")
            
        except Exception as e:
            logger.error(f"Failed to handle task failure for {task_id}: {e}")
    
    def register_task_callback(self, task_id: str, callback: Callable):
        """Register callback for task completion"""
        self.task_callbacks[task_id] = callback
        logger.debug(f"Registered callback for task {task_id}")
    
    def cleanup_completed_tasks(self, max_age_hours: int = 24):
        """Clean up old completed tasks"""
        try:
            current_time = datetime.now()
            cutoff_time = current_time - timedelta(hours=max_age_hours)
            
            tasks_to_remove = []
            for task_id, task_info in self.completed_tasks.items():
                if task_info.completed_at and task_info.completed_at < cutoff_time:
                    tasks_to_remove.append(task_id)
            
            for task_id in tasks_to_remove:
                del self.completed_tasks[task_id]
            
            if tasks_to_remove:
                logger.info(f"Cleaned up {len(tasks_to_remove)} old completed tasks")
                
        except Exception as e:
            logger.error(f"Failed to cleanup completed tasks: {e}")
