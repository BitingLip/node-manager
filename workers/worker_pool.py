"""
Worker Pool Management
Manages pools of workers for load balancing and resource optimization
"""

import asyncio
import time
from typing import Dict, List, Optional, Any, Type, Tuple, Union
from abc import ABC, abstractmethod
from dataclasses import dataclass, asdict
import structlog

from .base_worker import BaseWorker, WorkerState

logger = structlog.get_logger(__name__)


@dataclass
class PoolMetrics:
    """Worker pool metrics"""
    pool_id: str
    total_workers: int
    active_workers: int
    idle_workers: int
    failed_workers: int
    total_tasks_processed: int
    average_response_time_ms: float
    requests_per_second: float
    resource_utilization_percent: float
    timestamp: float


class WorkerPool:
    """
    Pool of workers of the same type for load balancing
    Distributes tasks across workers based on availability and load
    """
    
    def __init__(self, pool_id: str, worker_class: Type[BaseWorker], pool_config: Dict[str, Any]):
        """Initialize worker pool"""
        self.pool_id = pool_id
        self.worker_class = worker_class
        self.config = pool_config
        
        # Pool settings
        self.min_workers = pool_config.get("min_workers", 1)
        self.max_workers = pool_config.get("max_workers", 5)
        self.scale_threshold = pool_config.get("scale_threshold", 0.8)  # Scale up at 80% utilization
        self.scale_down_threshold = pool_config.get("scale_down_threshold", 0.3)  # Scale down at 30% utilization
        
        # Worker management
        self.workers: Dict[str, BaseWorker] = {}
        self.worker_loads: Dict[str, float] = {}  # Track load per worker
        self.round_robin_index = 0
        
        # Pool state
        self.is_running = False
        self.auto_scaling_enabled = pool_config.get("auto_scaling", True)
        
        # Metrics
        self.metrics = PoolMetrics(
            pool_id=pool_id,
            total_workers=0,
            active_workers=0,
            idle_workers=0,
            failed_workers=0,
            total_tasks_processed=0,
            average_response_time_ms=0.0,
            requests_per_second=0.0,
            resource_utilization_percent=0.0,
            timestamp=time.time()
        )
        
        # Management tasks
        self.management_task: Optional[asyncio.Task] = None
        self.scaling_task: Optional[asyncio.Task] = None
        
        logger.info("WorkerPool initialized", 
                   pool_id=pool_id,
                   min_workers=self.min_workers,
                   max_workers=self.max_workers)
    
    async def start(self):
        """Start the worker pool"""
        if self.is_running:
            logger.warning("Worker pool already running", pool_id=self.pool_id)
            return
        
        self.is_running = True
        
        # Create minimum workers
        for i in range(self.min_workers):
            worker_id = f"{self.pool_id}_worker_{i}"
            await self._create_worker(worker_id)
        
        # Start management tasks
        self.management_task = asyncio.create_task(self._management_loop())
        if self.auto_scaling_enabled:
            self.scaling_task = asyncio.create_task(self._auto_scaling_loop())
        
        logger.info("Worker pool started", 
                   pool_id=self.pool_id,
                   initial_workers=len(self.workers))
    
    async def stop(self):
        """Stop the worker pool"""
        if not self.is_running:
            return
        
        self.is_running = False
        
        # Cancel management tasks
        if self.management_task:
            self.management_task.cancel()
        if self.scaling_task:
            self.scaling_task.cancel()
        
        # Stop all workers
        for worker in self.workers.values():
            await worker.stop()
        
        self.workers.clear()
        self.worker_loads.clear()
        
        logger.info("Worker pool stopped", pool_id=self.pool_id)
    
    async def submit_task(self, task_data: Any, parameters: Optional[Dict[str, Any]] = None) -> str:
        """Submit task to best available worker"""
        if not self.workers:
            raise Exception(f"No workers available in pool {self.pool_id}")
        
        # Find best worker (lowest load)
        best_worker_id = min(self.worker_loads.keys(), key=lambda w: self.worker_loads[w])
        worker = self.workers[best_worker_id]
        
        # Submit task to worker
        if hasattr(worker, 'submit_request') and callable(getattr(worker, 'submit_request')):
            task_id = await worker.submit_request(task_data, parameters=parameters)  # type: ignore
        elif hasattr(worker, 'submit_task') and callable(getattr(worker, 'submit_task')):
            task_id = await worker.submit_task(task_data, parameters=parameters)  # type: ignore
        else:
            raise Exception(f"Worker {best_worker_id} does not support task submission")
        
        # Update worker load
        self.worker_loads[best_worker_id] += 1.0
        
        logger.debug("Task submitted to pool", 
                    pool_id=self.pool_id,
                    worker_id=best_worker_id,
                    task_id=task_id)
        
        return task_id
    
    async def get_task_result(self, task_id: str) -> Optional[Any]:
        """Get result from any worker in pool"""
        for worker in self.workers.values():
            if hasattr(worker, 'get_request_result') and callable(getattr(worker, 'get_request_result')):
                result = await worker.get_request_result(task_id)  # type: ignore
                if result:
                    return result
            elif hasattr(worker, 'get_task_result') and callable(getattr(worker, 'get_task_result')):
                result = await worker.get_task_result(task_id)  # type: ignore
                if result:
                    return result
        return None
    
    async def add_worker(self, worker_id: Optional[str] = None) -> bool:
        """Add new worker to pool"""
        if len(self.workers) >= self.max_workers:
            logger.warning("Cannot add worker, max workers reached", 
                          pool_id=self.pool_id,
                          max_workers=self.max_workers)
            return False
        
        if not worker_id:
            worker_id = f"{self.pool_id}_worker_{len(self.workers)}"
        
        return await self._create_worker(worker_id)
    
    async def remove_worker(self, worker_id: str) -> bool:
        """Remove worker from pool"""
        if len(self.workers) <= self.min_workers:
            logger.warning("Cannot remove worker, min workers required",
                          pool_id=self.pool_id,
                          min_workers=self.min_workers)
            return False
        
        if worker_id not in self.workers:
            return False
        
        # Stop and remove worker
        worker = self.workers[worker_id]
        await worker.stop()
        
        del self.workers[worker_id]
        del self.worker_loads[worker_id]
        
        logger.info("Worker removed from pool", 
                   pool_id=self.pool_id,
                   worker_id=worker_id)
        return True
    
    async def _create_worker(self, worker_id: str) -> bool:
        """Create new worker instance"""
        try:            # Create worker with configuration
            worker_config = self.config.copy()
            worker_config["worker_id"] = worker_id
            
            worker = self.worker_class(worker_id, worker_config)
            await worker.start()
            
            self.workers[worker_id] = worker
            self.worker_loads[worker_id] = 0.0
            
            logger.info("Worker created and started", 
                       pool_id=self.pool_id,
                       worker_id=worker_id)
            return True
            
        except Exception as e:
            logger.error("Failed to create worker", 
                        pool_id=self.pool_id,
                        worker_id=worker_id,
                        error=str(e))
            return False
    
    async def _management_loop(self):
        """Main management loop for worker health and metrics"""
        while self.is_running:
            try:
                await self._check_worker_health()
                await self._update_worker_loads()
                await self._update_metrics()
                
                await asyncio.sleep(10.0)  # Check every 10 seconds
                
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error("Error in management loop", 
                           pool_id=self.pool_id,
                           error=str(e))
                await asyncio.sleep(5.0)
    
    async def _auto_scaling_loop(self):
        """Auto-scaling loop based on utilization"""
        while self.is_running:
            try:
                utilization = self._calculate_utilization()
                
                # Scale up if utilization is high
                if utilization > self.scale_threshold and len(self.workers) < self.max_workers:
                    logger.info("Scaling up workers", 
                               pool_id=self.pool_id,
                               utilization=utilization,
                               current_workers=len(self.workers))
                    await self.add_worker()
                
                # Scale down if utilization is low
                elif (utilization < self.scale_down_threshold and 
                      len(self.workers) > self.min_workers):
                    logger.info("Scaling down workers", 
                               pool_id=self.pool_id,
                               utilization=utilization,
                               current_workers=len(self.workers))
                    # Remove least loaded worker
                    least_loaded_worker = min(self.worker_loads.keys(), 
                                            key=lambda w: self.worker_loads[w])
                    await self.remove_worker(least_loaded_worker)
                
                await asyncio.sleep(30.0)  # Check every 30 seconds
                
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error("Error in auto-scaling loop", 
                           pool_id=self.pool_id,
                           error=str(e))
                await asyncio.sleep(10.0)
    
    async def _update_metrics(self):
        """Update pool metrics"""
        self.metrics.total_workers = len(self.workers)
        self.metrics.active_workers = sum(load > 0.1 for load in self.worker_loads.values())
        self.metrics.idle_workers = self.metrics.total_workers - self.metrics.active_workers
        self.metrics.resource_utilization_percent = self._calculate_utilization() * 100
        self.metrics.timestamp = time.time()
    
    async def _check_worker_health(self):
        """Check health of all workers"""
        unhealthy_workers = []
        
        for worker_id, worker in self.workers.items():
            try:
                if hasattr(worker, 'get_worker_status') and callable(getattr(worker, 'get_worker_status')):
                    status = await worker.get_worker_status()  # type: ignore
                    if not status or status.get('is_running') == False:
                        unhealthy_workers.append(worker_id)
                elif hasattr(worker, 'state') and worker.state == WorkerState.ERROR:
                    unhealthy_workers.append(worker_id)
            except Exception as e:
                logger.warning("Worker health check failed", 
                              worker_id=worker_id,
                              error=str(e))
                unhealthy_workers.append(worker_id)
        
        # Restart unhealthy workers
        for worker_id in unhealthy_workers:
            logger.warning("Restarting unhealthy worker", 
                          pool_id=self.pool_id,
                          worker_id=worker_id)
            await self.remove_worker(worker_id)
            await self.add_worker(worker_id)
    
    async def _update_worker_loads(self):
        """Update worker load estimates"""
        for worker_id, worker in self.workers.items():
            try:                # Decay load over time
                self.worker_loads[worker_id] *= 0.9
                
                # Update based on queue size if available (with hasattr checks)
                if hasattr(worker, 'request_queue'):
                    request_queue = getattr(worker, 'request_queue', None)
                    if request_queue and hasattr(request_queue, 'qsize'):
                        queue_size = request_queue.qsize()
                        self.worker_loads[worker_id] += queue_size * 0.1
                elif hasattr(worker, 'task_queue'):
                    task_queue = getattr(worker, 'task_queue', None)
                    if task_queue and hasattr(task_queue, 'qsize'):
                        queue_size = task_queue.qsize()
                        self.worker_loads[worker_id] += queue_size * 0.1
                
            except Exception as e:
                logger.warning("Failed to update worker load", 
                              worker_id=worker_id,
                              error=str(e))
    
    def _calculate_utilization(self) -> float:
        """Calculate pool utilization"""
        if not self.workers:
            return 0.0
        
        total_load = sum(self.worker_loads.values())
        max_load = len(self.workers) * 10.0  # Assume max load of 10 per worker
        
        return min(1.0, total_load / max_load)
    
    async def get_pool_status(self) -> Dict[str, Any]:
        """Get detailed pool status"""
        await self._update_metrics()
        
        worker_statuses = {}
        for worker_id, worker in self.workers.items():
            try:
                if hasattr(worker, 'get_worker_status') and callable(getattr(worker, 'get_worker_status')):
                    worker_statuses[worker_id] = await worker.get_worker_status()  # type: ignore
                else:
                    worker_statuses[worker_id] = {
                        "worker_id": worker_id,
                        "load": self.worker_loads[worker_id]
                    }
            except Exception as e:
                logger.warning("Failed to get worker status", 
                              worker_id=worker_id,
                              error=str(e))
                worker_statuses[worker_id] = {"error": str(e)}
        
        return {
            "pool_id": self.pool_id,
            "metrics": asdict(self.metrics),
            "worker_statuses": worker_statuses,
            "config": self.config
        }
    
    def get_metrics(self) -> PoolMetrics:
        """Get current pool metrics"""
        return self.metrics


class PoolManager:
    """
    Manager for multiple worker pools
    Handles different types of workers and load balancing across pools
    """
    
    def __init__(self):
        """Initialize pool manager"""
        self.pools: Dict[str, WorkerPool] = {}
        self.is_running = False
        
        logger.info("PoolManager initialized")
    
    async def start(self):
        """Start pool manager"""
        if self.is_running:
            return
        
        self.is_running = True
        
        # Start all pools
        for pool in self.pools.values():
            await pool.start()
        
        logger.info("PoolManager started", total_pools=len(self.pools))
    
    async def stop(self):
        """Stop pool manager"""
        if not self.is_running:
            return
        
        self.is_running = False
        
        # Stop all pools
        for pool in self.pools.values():
            await pool.stop()
        
        logger.info("PoolManager stopped")
    
    def create_pool(self, pool_id: str, worker_class: Type[BaseWorker], pool_config: Dict[str, Any]) -> WorkerPool:
        """Create new worker pool"""
        if pool_id in self.pools:
            logger.warning("Pool already exists", pool_id=pool_id)
            return self.pools[pool_id]
        
        pool = WorkerPool(pool_id, worker_class, pool_config)
        self.pools[pool_id] = pool
        
        logger.info("Pool created", pool_id=pool_id)
        return pool
    
    async def submit_task(self, pool_id: str, task_data: Any, parameters: Optional[Dict[str, Any]] = None) -> str:
        """Submit task to specific pool"""
        if pool_id not in self.pools:
            raise Exception(f"Pool {pool_id} not found")
        
        return await self.pools[pool_id].submit_task(task_data, parameters)
    
    async def get_task_result(self, pool_id: str, task_id: str) -> Optional[Any]:
        """Get task result from specific pool"""
        if pool_id not in self.pools:
            return None
        
        return await self.pools[pool_id].get_task_result(task_id)
    
    def get_pool(self, pool_id: str) -> Optional[WorkerPool]:
        """Get pool by ID"""
        return self.pools.get(pool_id)
    
    async def get_status(self) -> Dict[str, Any]:
        """Get status of all pools"""
        pool_statuses = {}
        for pool_id, pool in self.pools.items():
            pool_statuses[pool_id] = await pool.get_pool_status()
        
        return {
            "is_running": self.is_running,
            "total_pools": len(self.pools),
            "pools": pool_statuses
        }
