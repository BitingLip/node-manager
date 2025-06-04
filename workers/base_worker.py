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
    ERROR = "error"
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
        # TODO: Implement worker startup
        # 1. Initialize resources
        # 2. Start message processing loop
        # 3. Send ready signal
        # 4. Begin heartbeat
        return True
    
    async def stop(self, graceful: bool = True) -> bool:
        """Stop the worker process"""
        # TODO: Implement worker shutdown
        # 1. Complete current task if graceful
        # 2. Release resources
        # 3. Close connections
        # 4. Clean up
        return True
    
    def send_heartbeat(self):
        """Send heartbeat to node manager"""
        # TODO: Implement heartbeat mechanism
        pass
    
    def report_error(self, error: Exception):
        """Report error to node manager"""
        # TODO: Implement error reporting
        pass
    
    def update_status(self, state: WorkerState, details: Optional[Dict[str, Any]] = None):
        """Update worker status"""
        # TODO: Update status and notify node manager
        pass
    
    async def handle_shutdown_signal(self, signum, frame):
        """Handle shutdown signals gracefully"""
        # TODO: Implement graceful shutdown on signals
        pass
