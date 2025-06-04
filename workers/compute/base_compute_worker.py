"""
Compute Worker Base Class
Base class for compute-intensive processing workers
Handles batch processing, data parallelism, and resource optimization
"""

import asyncio
import time
import json
import multiprocessing as mp
from typing import Dict, List, Optional, Any, Callable, Iterator
from pathlib import Path
from dataclasses import dataclass, asdict
from abc import ABC, abstractmethod
from concurrent.futures import ThreadPoolExecutor, ProcessPoolExecutor
import structlog

logger = structlog.get_logger(__name__)


@dataclass
class ComputeTask:
    """Individual compute task"""
    task_id: str
    input_data: Any
    priority: int
    submitted_at: float
    started_at: Optional[float]
    completed_at: Optional[float]
    result: Optional[Any]
    error: Optional[str]
    metadata: Dict[str, Any]


@dataclass
class BatchConfig:
    """Batch processing configuration"""
    batch_size: int
    max_workers: int
    timeout_seconds: int
    retry_attempts: int
    queue_size: int
    use_multiprocessing: bool
    chunk_size: Optional[int]


@dataclass
class ComputeMetrics:
    """Compute worker metrics"""
    tasks_processed: int
    tasks_failed: int
    total_processing_time: float
    average_task_time: float
    throughput_per_second: float
    memory_usage_mb: float
    cpu_usage_percent: float
    queue_size: int
    active_workers: int


class BaseComputeWorker(ABC):
    """
    Base class for compute workers
    Provides batch processing, parallelization, and resource management
    """
    
    def __init__(self, worker_id: str, config: Dict[str, Any]):
        """Initialize compute worker"""
        self.worker_id = worker_id
        self.config = config
        
        # Batch configuration
        self.batch_config = BatchConfig(
            batch_size=config.get("batch_size", 32),
            max_workers=config.get("max_workers", mp.cpu_count()),
            timeout_seconds=config.get("timeout_seconds", 300),
            retry_attempts=config.get("retry_attempts", 3),
            queue_size=config.get("queue_size", 1000),
            use_multiprocessing=config.get("use_multiprocessing", True),
            chunk_size=config.get("chunk_size", None)
        )
        
        # Task management
        self.task_queue: asyncio.Queue = asyncio.Queue(maxsize=self.batch_config.queue_size)
        self.active_tasks: Dict[str, ComputeTask] = {}
        self.completed_tasks: List[ComputeTask] = []
        self.processing_lock = asyncio.Lock()
        
        # Worker pool
        self.executor: Optional[Any] = None
        self.worker_tasks: List[asyncio.Task] = []
        
        # Metrics
        self.metrics = ComputeMetrics(
            tasks_processed=0,
            tasks_failed=0,
            total_processing_time=0.0,
            average_task_time=0.0,
            throughput_per_second=0.0,
            memory_usage_mb=0.0,
            cpu_usage_percent=0.0,
            queue_size=0,
            active_workers=0
        )
        
        # Callbacks
        self.progress_callbacks: List[Callable] = []
        self.completion_callbacks: List[Callable] = []
        
        # State
        self.is_running = False
        self.start_time: Optional[float] = None
        
        logger.info("BaseComputeWorker initialized", 
                   worker_id=worker_id,
                   batch_size=self.batch_config.batch_size,
                   max_workers=self.batch_config.max_workers)
    
    @abstractmethod
    async def process_task(self, task: ComputeTask) -> Any:
        """Process a single compute task"""
        pass
    
    @abstractmethod
    async def process_batch(self, tasks: List[ComputeTask]) -> List[Any]:
        """Process a batch of tasks (optional optimization)"""
        # Default implementation processes tasks individually
        results = []
        for task in tasks:
            result = await self.process_task(task)
            results.append(result)
        return results
    
    async def start(self):
        """Start the compute worker"""
        if self.is_running:
            logger.warning("Compute worker already running", worker_id=self.worker_id)
            return
        
        self.is_running = True
        self.start_time = time.time()
        
        # Initialize executor
        if self.batch_config.use_multiprocessing:
            self.executor = ProcessPoolExecutor(max_workers=self.batch_config.max_workers)
        else:
            self.executor = ThreadPoolExecutor(max_workers=self.batch_config.max_workers)
        
        # Start worker tasks
        for i in range(self.batch_config.max_workers):
            task = asyncio.create_task(self._worker_loop(f"worker_{i}"))
            self.worker_tasks.append(task)
        
        logger.info("Compute worker started", worker_id=self.worker_id)
    
    async def stop(self):
        """Stop the compute worker"""
        if not self.is_running:
            return
        
        self.is_running = False
        
        # Cancel worker tasks
        for task in self.worker_tasks:
            task.cancel()
        
        # Wait for tasks to complete
        await asyncio.gather(*self.worker_tasks, return_exceptions=True)
        self.worker_tasks.clear()
        
        # Shutdown executor
        if self.executor:
            self.executor.shutdown(wait=True)
            self.executor = None
        
        logger.info("Compute worker stopped", worker_id=self.worker_id)
    
    async def submit_task(self, task_data: Any, priority: int = 0, metadata: Optional[Dict[str, Any]] = None) -> str:
        """Submit a task for processing"""
        task_id = f"{self.worker_id}_{int(time.time() * 1000000)}"
        
        task = ComputeTask(
            task_id=task_id,
            input_data=task_data,
            priority=priority,
            submitted_at=time.time(),
            started_at=None,
            completed_at=None,
            result=None,
            error=None,
            metadata=metadata or {}
        )
        
        try:
            await self.task_queue.put(task)
            logger.debug("Task submitted", task_id=task_id, priority=priority)
            return task_id
        except asyncio.QueueFull:
            logger.error("Task queue full", worker_id=self.worker_id)
            raise Exception("Task queue is full")
    
    async def submit_batch(self, task_data_list: List[Any], priority: int = 0) -> List[str]:
        """Submit multiple tasks as a batch"""
        task_ids = []
        for task_data in task_data_list:
            task_id = await self.submit_task(task_data, priority)
            task_ids.append(task_id)
        return task_ids
    
    async def get_task_result(self, task_id: str) -> Optional[ComputeTask]:
        """Get result of a specific task"""
        # Check active tasks
        if task_id in self.active_tasks:
            return self.active_tasks[task_id]
        
        # Check completed tasks
        for task in self.completed_tasks:
            if task.task_id == task_id:
                return task
        
        return None
    
    async def wait_for_task(self, task_id: str, timeout: Optional[float] = None) -> Optional[ComputeTask]:
        """Wait for a task to complete"""
        start_time = time.time()
        
        while True:
            task = await self.get_task_result(task_id)
            if task and task.completed_at is not None:
                return task
            
            if timeout and (time.time() - start_time) > timeout:
                return None
            
            await asyncio.sleep(0.1)
    
    async def _worker_loop(self, worker_name: str):
        """Main worker processing loop"""
        logger.debug(f"Worker {worker_name} started")
        
        while self.is_running:
            try:
                # Get tasks from queue
                tasks = await self._get_batch_tasks()
                if not tasks:
                    continue
                
                # Process tasks
                await self._process_task_batch(tasks, worker_name)
                
            except asyncio.CancelledError:
                logger.debug(f"Worker {worker_name} cancelled")
                break
            except Exception as e:
                logger.error(f"Worker {worker_name} error", error=str(e))
                await asyncio.sleep(1)
        
        logger.debug(f"Worker {worker_name} stopped")
    
    async def _get_batch_tasks(self) -> List[ComputeTask]:
        """Get a batch of tasks from the queue"""
        tasks = []
        
        try:
            # Get first task (blocking)
            task = await asyncio.wait_for(self.task_queue.get(), timeout=1.0)
            tasks.append(task)
            
            # Get additional tasks for batch (non-blocking)
            for _ in range(self.batch_config.batch_size - 1):
                try:
                    task = self.task_queue.get_nowait()
                    tasks.append(task)
                except asyncio.QueueEmpty:
                    break
            
        except asyncio.TimeoutError:
            pass
        
        return tasks
    
    async def _process_task_batch(self, tasks: List[ComputeTask], worker_name: str):
        """Process a batch of tasks"""
        if not tasks:
            return
        
        # Mark tasks as started
        start_time = time.time()
        async with self.processing_lock:
            for task in tasks:
                task.started_at = start_time
                self.active_tasks[task.task_id] = task
        
        try:
            # Process batch
            results = await self.process_batch(tasks)
            
            # Update tasks with results
            async with self.processing_lock:
                for task, result in zip(tasks, results):
                    task.completed_at = time.time()
                    task.result = result
                    
                    # Move to completed
                    self.completed_tasks.append(task)
                    del self.active_tasks[task.task_id]
                    
                    # Update metrics
                    self.metrics.tasks_processed += 1
                    processing_time = task.completed_at - task.started_at
                    self.metrics.total_processing_time += processing_time
                    
                    # Notify callbacks
                    for callback in self.completion_callbacks:
                        try:
                            await callback(task)
                        except Exception as e:
                            logger.warning("Completion callback failed", error=str(e))
            
            logger.debug(f"Processed {len(tasks)} tasks", worker=worker_name)
            
        except Exception as e:
            # Mark tasks as failed
            async with self.processing_lock:
                for task in tasks:
                    task.completed_at = time.time()
                    task.error = str(e)
                    
                    # Move to completed
                    self.completed_tasks.append(task)
                    if task.task_id in self.active_tasks:
                        del self.active_tasks[task.task_id]
                    
                    # Update metrics
                    self.metrics.tasks_failed += 1
            
            logger.error(f"Batch processing failed", worker=worker_name, error=str(e))
        
        # Update metrics
        await self._update_metrics()
    
    async def _update_metrics(self):
        """Update performance metrics"""
        current_time = time.time()
        
        # Calculate average task time
        if self.metrics.tasks_processed > 0:
            self.metrics.average_task_time = self.metrics.total_processing_time / self.metrics.tasks_processed
        
        # Calculate throughput
        if self.start_time:
            elapsed_time = current_time - self.start_time
            if elapsed_time > 0:
                self.metrics.throughput_per_second = self.metrics.tasks_processed / elapsed_time
        
        # Update queue size
        self.metrics.queue_size = self.task_queue.qsize()
        self.metrics.active_workers = len(self.active_tasks)
        
        # Notify progress callbacks
        for callback in self.progress_callbacks:
            try:
                await callback(self.metrics)
            except Exception as e:
                logger.warning("Progress callback failed", error=str(e))
    
    async def get_metrics(self) -> ComputeMetrics:
        """Get current performance metrics"""
        await self._update_metrics()
        return self.metrics
    
    async def get_status(self) -> Dict[str, Any]:
        """Get detailed worker status"""
        metrics = await self.get_metrics()
        
        return {
            "worker_id": self.worker_id,
            "is_running": self.is_running,
            "start_time": self.start_time,
            "uptime_seconds": time.time() - self.start_time if self.start_time else 0,
            "batch_config": asdict(self.batch_config),
            "metrics": asdict(metrics),
            "queue_size": self.task_queue.qsize(),
            "active_tasks": len(self.active_tasks),
            "completed_tasks": len(self.completed_tasks)
        }
    
    def add_progress_callback(self, callback: Callable):
        """Add progress update callback"""
        self.progress_callbacks.append(callback)
    
    def add_completion_callback(self, callback: Callable):
        """Add task completion callback"""
        self.completion_callbacks.append(callback)
    
    async def clear_completed_tasks(self, keep_recent: int = 100):
        """Clear old completed tasks to save memory"""
        async with self.processing_lock:
            if len(self.completed_tasks) > keep_recent:
                self.completed_tasks = self.completed_tasks[-keep_recent:]
                logger.debug(f"Cleared old completed tasks, keeping {keep_recent}")


class ComputeWorkerPool:
    """
    Pool of compute workers for load balancing
    Distributes tasks across multiple workers based on load
    """
    
    def __init__(self, worker_configs: List[Dict[str, Any]]):
        """Initialize compute worker pool"""
        self.workers: Dict[str, BaseComputeWorker] = {}
        self.round_robin_index = 0
        
        # Create workers from configs
        for i, config in enumerate(worker_configs):
            worker_id = f"compute_worker_{i}"
            # Worker creation will be handled by subclasses
            logger.info(f"Configured worker: {worker_id}")
    
    async def start_all(self):
        """Start all workers in the pool"""
        for worker in self.workers.values():
            await worker.start()
        logger.info(f"Started {len(self.workers)} compute workers")
    
    async def stop_all(self):
        """Stop all workers in the pool"""
        for worker in self.workers.values():
            await worker.stop()
        logger.info("Stopped all compute workers")
    
    async def submit_task(self, task_data: Any, priority: int = 0) -> Tuple[str, str]:
        """Submit task to best available worker"""
        if not self.workers:
            raise Exception("No workers available")
        
        # Simple round-robin selection (can be improved with load balancing)
        worker_ids = list(self.workers.keys())
        worker_id = worker_ids[self.round_robin_index % len(worker_ids)]
        self.round_robin_index += 1
        
        worker = self.workers[worker_id]
        task_id = await worker.submit_task(task_data, priority)
        
        return worker_id, task_id
    
    async def get_pool_status(self) -> Dict[str, Any]:
        """Get status of all workers in pool"""
        status = {}
        for worker_id, worker in self.workers.items():
            status[worker_id] = await worker.get_status()
        return status
