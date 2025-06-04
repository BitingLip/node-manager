"""
Base Worker Class
Abstract base class for all worker process implementations
Defines common interface and functionality for all worker types
"""

import abc
import asyncio
import logging
import os
import signal
from typing import Dict, List, Optional, Any
from datetime import datetime
from enum import Enum
from dataclasses import dataclass
import structlog

logger = structlog.get_logger(__name__)


class WorkerState(Enum):
    """Worker process states"""
    INITIALIZING = "initializing"
    READY = "ready"
    BUSY = "busy"
    PROCESSING = "processing"
    PAUSED = "paused"
    ERROR = "error"
    STOPPING = "stopping"
    SHUTTING_DOWN = "shutting_down"
    STOPPED = "stopped"


@dataclass
class WorkerStatus:
    """Worker status information"""
    worker_id: str
    state: WorkerState
    current_task_id: Optional[str]
    last_heartbeat: datetime
    error_count: int
    uptime_seconds: float
    tasks_completed: int
    tasks_failed: int
    allocated_gpu_memory: int
    allocated_cpu_cores: int
    allocated_ram_mb: int


@dataclass
class WorkerMetrics:
    """Worker performance metrics"""
    worker_id: str
    tasks_per_minute: float
    average_task_duration: float
    error_rate: float
    memory_usage_mb: float
    cpu_usage_percent: float
    gpu_usage_percent: float
    queue_size: int
    timestamp: datetime


class BaseWorker(abc.ABC):
    """
    Abstract base class for all worker implementations
    Provides common functionality and interface for worker processes
    """
    
    def __init__(self, worker_id: str, config: Dict[str, Any]):
        """Initialize base worker"""
        self.worker_id = worker_id
        self.config = config
        self.state = WorkerState.INITIALIZING
        self.current_task_id = None
        self.last_heartbeat = datetime.now()
        self.error_count = 0
        
        # Resource allocation
        self.allocated_gpu_memory = 0
        self.allocated_cpu_cores = 0
        self.allocated_ram_mb = 0
        
        # Communication
        self.message_queue = None
        self.result_callback = None
        
        logger.info(f"BaseWorker {worker_id} initializing")
    
    @abc.abstractmethod
    async def initialize(self) -> bool:
        """Initialize worker-specific resources"""
        # TODO: Override in subclasses
        # 1. Load models
        # 2. Initialize GPU/CPU resources
        # 3. Set up worker-specific components
        # 4. Validate functionality
        pass
    
    @abc.abstractmethod
    async def process_task(self, task_data: Dict[str, Any]) -> Dict[str, Any]:
        """Process a task and return results"""
        # TODO: Override in subclasses
        # 1. Validate task data
        # 2. Load required models
        # 3. Execute inference
        # 4. Return results
        pass
    
    @abc.abstractmethod
    def get_capabilities(self) -> Dict[str, Any]:
        """Get worker capabilities and requirements"""        # TODO: Override in subclasses
        # Return supported models, resource requirements, etc.
        pass    
    async def start(self) -> bool:
        """Start the worker process"""
        try:
            logger.info(f"Starting worker {self.worker_id}")
            
            # 1. Initialize resources
            if not await self.initialize():
                logger.error(f"Failed to initialize worker {self.worker_id}")
                return False
            
            # 2. Update status to ready
            self.state = WorkerState.READY
            self.update_status(WorkerState.READY, {"message": "Worker started successfully"})
            
            # 3. Start message processing loop (would be in separate process/thread)
            # In a real implementation, this would start the worker event loop
            logger.info(f"Worker {self.worker_id} started and ready")
            
            # 4. Begin heartbeat (placeholder)
            self.send_heartbeat()
            
            return True
            
        except Exception as e:
            logger.error(f"Failed to start worker {self.worker_id}: {e}")
            self.state = WorkerState.ERROR
            self.report_error(e)
            return False
    
    async def stop(self, graceful: bool = True) -> bool:
        """Stop the worker process"""
        try:
            logger.info(f"Stopping worker {self.worker_id} (graceful: {graceful})")
            
            # 1. Update status to stopping
            self.state = WorkerState.STOPPING
            self.update_status(WorkerState.STOPPING)
            
            if graceful and self.current_task:
                # 2. Complete current task if graceful
                logger.info(f"Waiting for current task {self.current_task} to complete")
                # In real implementation, would wait for task completion
                
            # 3. Release resources (override in subclasses)
            await self._cleanup_resources()
            
            # 4. Update final status
            self.state = WorkerState.STOPPED
            self.update_status(WorkerState.STOPPED, {"message": "Worker stopped"})
            
            logger.info(f"Worker {self.worker_id} stopped successfully")
            return True
            
        except Exception as e:
            logger.error(f"Failed to stop worker {self.worker_id}: {e}")
            return False
    
    async def _cleanup_resources(self):
        """Clean up worker-specific resources (override in subclasses)"""
        # Base implementation - override in specific workers
        pass
    
    def send_heartbeat(self):
        """Send heartbeat to node manager"""
        try:
            heartbeat_data = {
                'worker_id': self.worker_id,
                'state': self.state.value,
                'current_task': self.current_task,
                'timestamp': datetime.now().isoformat(),
                'resource_usage': self._get_resource_usage()
            }
            
            # In real implementation, would send via IPC or HTTP
            logger.debug(f"Heartbeat from worker {self.worker_id}: {heartbeat_data}")
            
        except Exception as e:
            logger.error(f"Failed to send heartbeat from worker {self.worker_id}: {e}")
    
    def _get_resource_usage(self) -> Dict[str, Any]:
        """Get current resource usage (simplified)"""
        return {
            'cpu_percent': 0.0,  # Would get actual CPU usage
            'memory_mb': 0,      # Would get actual memory usage
            'gpu_utilization': 0.0  # Would get actual GPU usage
        }
    
    def report_error(self, error: Exception):
        """Report error to node manager"""
        try:
            error_data = {
                'worker_id': self.worker_id,
                'error_type': type(error).__name__,
                'error_message': str(error),
                'timestamp': datetime.now().isoformat(),
                'current_task': self.current_task
            }
            
            logger.error(f"Worker {self.worker_id} error: {error_data}")
            
            # In real implementation, would send error via IPC or HTTP
            
        except Exception as e:
            logger.error(f"Failed to report error from worker {self.worker_id}: {e}")
    
    def update_status(self, state: WorkerState, details: Optional[Dict[str, Any]] = None):
        """Update worker status"""
        try:
            self.state = state
            
            status_data = {
                'worker_id': self.worker_id,
                'state': state.value,
                'timestamp': datetime.now().isoformat(),
                'details': details or {}
            }
            
            logger.debug(f"Worker {self.worker_id} status update: {status_data}")
            
            # In real implementation, would send status via IPC or HTTP
            
        except Exception as e:
            logger.error(f"Failed to update status for worker {self.worker_id}: {e}")
    
    async def handle_shutdown_signal(self, signum, frame):
        """Handle shutdown signals gracefully"""
        logger.info(f"Worker {self.worker_id} received shutdown signal {signum}")
        await self.stop(graceful=True)
    
    async def execute_task(self, task_data: Dict[str, Any]) -> Dict[str, Any]:
        """Execute a task with proper state management"""
        task_id = task_data.get('task_id', 'unknown')
        
        try:
            logger.info(f"Worker {self.worker_id} executing task {task_id}")
            
            # Update state to processing
            self.current_task = task_id
            self.state = WorkerState.PROCESSING
            self.update_status(WorkerState.PROCESSING, {'task_id': task_id})
            
            # Process the task (calls the abstract method)
            result = await self.process_task(task_data)
            
            # Update state back to ready
            self.current_task = None
            self.state = WorkerState.READY
            self.update_status(WorkerState.READY, {'completed_task': task_id})
            
            logger.info(f"Worker {self.worker_id} completed task {task_id}")
            return result
            
        except Exception as e:
            # Handle task failure
            self.current_task = None
            self.state = WorkerState.ERROR
            self.update_status(WorkerState.ERROR, {'failed_task': task_id, 'error': str(e)})
            self.report_error(e)
            
            logger.error(f"Worker {self.worker_id} failed task {task_id}: {e}")
            raise
