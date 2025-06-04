"""
Unified Worker Registry
Central registry for all worker types (inference, training, utility, compute)
Provides discovery, instantiation, and lifecycle management
"""

import asyncio
import time
import importlib
from typing import Dict, List, Optional, Any, Type, Union
from pathlib import Path
from dataclasses import dataclass, asdict
from abc import ABC, abstractmethod
import structlog

logger = structlog.get_logger(__name__)


@dataclass
class WorkerSpec:
    """Worker specification"""
    worker_id: str
    worker_type: str  # "inference", "training", "utility", "compute"
    worker_class: str
    config: Dict[str, Any]
    dependencies: List[str]
    resource_requirements: Dict[str, Any]
    status: str  # "registered", "initializing", "ready", "running", "stopped", "failed"
    created_at: float
    updated_at: float


@dataclass
class WorkerInstance:
    """Active worker instance"""
    spec: WorkerSpec
    instance: Any
    pid: Optional[int]
    start_time: Optional[float]
    last_heartbeat: float
    metrics: Dict[str, Any]


class WorkerRegistry:
    """
    Central registry for all worker types
    Manages worker discovery, lifecycle, and resource allocation
    """
    
    def __init__(self):
        """Initialize worker registry"""
        self.registered_workers: Dict[str, WorkerSpec] = {}
        self.active_workers: Dict[str, WorkerInstance] = {}
        self.worker_classes: Dict[str, Type] = {}
        
        # Load paths for worker discovery
        self.worker_paths = {
            "inference": Path(__file__).parent / "inference",
            "training": Path(__file__).parent / "training", 
            "utility": Path(__file__).parent / "utility",
            "compute": Path(__file__).parent / "compute"
        }
        
        # Resource tracking
        self.resource_usage: Dict[str, Any] = {
            "total_workers": 0,
            "workers_by_type": {},
            "memory_allocated_mb": 0,
            "gpu_devices_allocated": [],
            "cpu_cores_allocated": 0
        }
        
        logger.info("WorkerRegistry initialized")
    
    async def discover_workers(self):
        """Discover all available worker classes"""
        discovered = 0
        
        for worker_type, worker_path in self.worker_paths.items():
            if not worker_path.exists():
                logger.warning(f"Worker path not found: {worker_path}")
                continue
            
            try:
                # Import worker modules
                for py_file in worker_path.glob("*.py"):
                    if py_file.name.startswith("__"):
                        continue
                    
                    module_name = f"managers.node_manager.workers.{worker_type}.{py_file.stem}"
                    
                    try:
                        module = importlib.import_module(module_name)
                        
                        # Look for worker classes
                        for attr_name in dir(module):
                            attr = getattr(module, attr_name)
                            
                            if (isinstance(attr, type) and 
                                hasattr(attr, '__bases__') and
                                any('Worker' in base.__name__ for base in attr.__bases__)):
                                
                                class_key = f"{worker_type}.{attr_name}"
                                self.worker_classes[class_key] = attr
                                discovered += 1
                                
                                logger.debug(f"Discovered worker class: {class_key}")
                    
                    except Exception as e:
                        logger.warning(f"Failed to import {module_name}: {e}")
            
            except Exception as e:
                logger.error(f"Failed to discover workers in {worker_path}: {e}")
        
        logger.info(f"Discovered {discovered} worker classes")
        return discovered
    
    async def register_worker(self, 
                            worker_id: str,
                            worker_type: str,
                            worker_class: str,
                            config: Dict[str, Any],
                            dependencies: Optional[List[str]] = None,
                            resource_requirements: Optional[Dict[str, Any]] = None) -> bool:
        """Register a worker specification"""
        
        if worker_id in self.registered_workers:
            logger.warning(f"Worker already registered: {worker_id}")
            return False
        
        # Validate worker class exists
        class_key = f"{worker_type}.{worker_class}"
        if class_key not in self.worker_classes:
            logger.error(f"Worker class not found: {class_key}")
            return False
        
        # Create worker spec
        spec = WorkerSpec(
            worker_id=worker_id,
            worker_type=worker_type,
            worker_class=worker_class,
            config=config,
            dependencies=dependencies or [],
            resource_requirements=resource_requirements or {},
            status="registered",
            created_at=time.time(),
            updated_at=time.time()
        )
        
        self.registered_workers[worker_id] = spec
        
        logger.info(f"Registered worker: {worker_id} ({worker_type}.{worker_class})")
        return True
    
    async def create_worker(self, worker_id: str) -> bool:
        """Create and initialize a worker instance"""
        
        if worker_id not in self.registered_workers:
            logger.error(f"Worker not registered: {worker_id}")
            return False
        
        if worker_id in self.active_workers:
            logger.warning(f"Worker already active: {worker_id}")
            return False
        
        spec = self.registered_workers[worker_id]
        spec.status = "initializing"
        spec.updated_at = time.time()
        
        try:
            # Get worker class
            class_key = f"{spec.worker_type}.{spec.worker_class}"
            worker_class = self.worker_classes[class_key]
            
            # Check dependencies
            if not await self._check_dependencies(spec.dependencies):
                logger.error(f"Dependencies not met for worker: {worker_id}")
                spec.status = "failed"
                return False
            
            # Check resource requirements
            if not await self._check_resources(spec.resource_requirements):
                logger.error(f"Resource requirements not met for worker: {worker_id}")
                spec.status = "failed"
                return False
            
            # Create worker instance
            worker_instance = worker_class(worker_id, spec.config)
            
            # Create worker instance record
            instance = WorkerInstance(
                spec=spec,
                instance=worker_instance,
                pid=None,
                start_time=None,
                last_heartbeat=time.time(),
                metrics={}
            )
            
            self.active_workers[worker_id] = instance
            spec.status = "ready"
            
            logger.info(f"Created worker instance: {worker_id}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to create worker {worker_id}: {e}")
            spec.status = "failed"
            return False
    
    async def start_worker(self, worker_id: str) -> bool:
        """Start a worker instance"""
        
        if worker_id not in self.active_workers:
            logger.error(f"Worker instance not found: {worker_id}")
            return False
        
        instance = self.active_workers[worker_id]
        
        if instance.spec.status == "running":
            logger.warning(f"Worker already running: {worker_id}")
            return True
        
        try:
            # Start the worker
            if hasattr(instance.instance, 'start'):
                await instance.instance.start()
            
            # Update status
            instance.spec.status = "running"
            instance.spec.updated_at = time.time()
            instance.start_time = time.time()
            instance.last_heartbeat = time.time()
            
            # Update resource tracking
            await self._allocate_resources(worker_id, instance.spec.resource_requirements)
            
            logger.info(f"Started worker: {worker_id}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to start worker {worker_id}: {e}")
            instance.spec.status = "failed"
            return False
    
    async def stop_worker(self, worker_id: str) -> bool:
        """Stop a worker instance"""
        
        if worker_id not in self.active_workers:
            logger.error(f"Worker instance not found: {worker_id}")
            return False
        
        instance = self.active_workers[worker_id]
        
        try:
            # Stop the worker
            if hasattr(instance.instance, 'stop'):
                await instance.instance.stop()
            
            # Update status
            instance.spec.status = "stopped"
            instance.spec.updated_at = time.time()
            
            # Release resources
            await self._release_resources(worker_id, instance.spec.resource_requirements)
            
            logger.info(f"Stopped worker: {worker_id}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to stop worker {worker_id}: {e}")
            return False
    
    async def remove_worker(self, worker_id: str) -> bool:
        """Remove worker instance and unregister"""
        
        # Stop if running
        if worker_id in self.active_workers:
            await self.stop_worker(worker_id)
            del self.active_workers[worker_id]
        
        # Unregister
        if worker_id in self.registered_workers:
            del self.registered_workers[worker_id]
            logger.info(f"Removed worker: {worker_id}")
            return True
        
        return False
    
    async def get_worker_status(self, worker_id: str) -> Optional[Dict[str, Any]]:
        """Get detailed worker status"""
        
        if worker_id not in self.registered_workers:
            return None
        
        spec = self.registered_workers[worker_id]
        result = {
            "spec": asdict(spec),
            "instance": None
        }
        
        if worker_id in self.active_workers:
            instance = self.active_workers[worker_id]
            
            # Get instance metrics if available
            instance_metrics = {}
            if hasattr(instance.instance, 'get_status'):
                try:
                    instance_metrics = await instance.instance.get_status()
                except Exception as e:
                    logger.warning(f"Failed to get worker metrics: {e}")
            
            result["instance"] = {
                "pid": instance.pid,
                "start_time": instance.start_time,
                "uptime_seconds": time.time() - instance.start_time if instance.start_time else 0,
                "last_heartbeat": instance.last_heartbeat,
                "metrics": instance_metrics
            }
        
        return result
    
    async def list_workers(self, worker_type: Optional[str] = None, status: Optional[str] = None) -> List[Dict[str, Any]]:
        """List workers with optional filtering"""
        
        results = []
        
        for worker_id, spec in self.registered_workers.items():
            # Apply filters
            if worker_type and spec.worker_type != worker_type:
                continue
            if status and spec.status != status:
                continue
            
            worker_status = await self.get_worker_status(worker_id)
            if worker_status:
                results.append(worker_status)
        
        return results
    
    async def get_registry_status(self) -> Dict[str, Any]:
        """Get overall registry status"""
        
        # Count workers by type and status
        type_counts = {}
        status_counts = {}
        
        for spec in self.registered_workers.values():
            type_counts[spec.worker_type] = type_counts.get(spec.worker_type, 0) + 1
            status_counts[spec.status] = status_counts.get(spec.status, 0) + 1
        
        return {
            "total_registered": len(self.registered_workers),
            "total_active": len(self.active_workers),
            "workers_by_type": type_counts,
            "workers_by_status": status_counts,
            "available_classes": len(self.worker_classes),
            "resource_usage": self.resource_usage
        }
    
    async def heartbeat_worker(self, worker_id: str) -> bool:
        """Update worker heartbeat"""
        
        if worker_id in self.active_workers:
            self.active_workers[worker_id].last_heartbeat = time.time()
            return True
        
        return False
    
    async def check_worker_health(self) -> List[str]:
        """Check health of all active workers"""
        
        unhealthy_workers = []
        current_time = time.time()
        heartbeat_timeout = 300  # 5 minutes
        
        for worker_id, instance in self.active_workers.items():
            if (current_time - instance.last_heartbeat) > heartbeat_timeout:
                unhealthy_workers.append(worker_id)
                logger.warning(f"Worker unhealthy (no heartbeat): {worker_id}")
        
        return unhealthy_workers
    
    async def restart_worker(self, worker_id: str) -> bool:
        """Restart a worker (stop and start)"""
        
        logger.info(f"Restarting worker: {worker_id}")
        
        success = await self.stop_worker(worker_id)
        if success:
            await asyncio.sleep(2)  # Brief delay
            success = await self.start_worker(worker_id)
        
        return success
    
    async def _check_dependencies(self, dependencies: List[str]) -> bool:
        """Check if worker dependencies are available"""
        
        for dep in dependencies:
            if dep not in self.active_workers:
                logger.error(f"Dependency not available: {dep}")
                return False
            
            if self.active_workers[dep].spec.status != "running":
                logger.error(f"Dependency not running: {dep}")
                return False
        
        return True
    
    async def _check_resources(self, requirements: Dict[str, Any]) -> bool:
        """Check if resource requirements can be met"""
        
        # Check memory
        required_memory = requirements.get("memory_mb", 0)
        available_memory = 8192  # This should come from actual system info
        if self.resource_usage["memory_allocated_mb"] + required_memory > available_memory:
            logger.error(f"Insufficient memory: need {required_memory}MB")
            return False
        
        # Check GPU
        required_gpu = requirements.get("gpu_device_id")
        if required_gpu is not None:
            if required_gpu in self.resource_usage["gpu_devices_allocated"]:
                logger.error(f"GPU device already allocated: {required_gpu}")
                return False
        
        return True
    
    async def _allocate_resources(self, worker_id: str, requirements: Dict[str, Any]):
        """Allocate resources to worker"""
        
        # Allocate memory
        required_memory = requirements.get("memory_mb", 0)
        self.resource_usage["memory_allocated_mb"] += required_memory
        
        # Allocate GPU
        required_gpu = requirements.get("gpu_device_id")
        if required_gpu is not None:
            self.resource_usage["gpu_devices_allocated"].append(required_gpu)
        
        # Update totals
        self.resource_usage["total_workers"] += 1
        
        logger.debug(f"Allocated resources for {worker_id}: {requirements}")
    
    async def _release_resources(self, worker_id: str, requirements: Dict[str, Any]):
        """Release resources from worker"""
        
        # Release memory
        required_memory = requirements.get("memory_mb", 0)
        self.resource_usage["memory_allocated_mb"] -= required_memory
        
        # Release GPU
        required_gpu = requirements.get("gpu_device_id")
        if required_gpu is not None and required_gpu in self.resource_usage["gpu_devices_allocated"]:
            self.resource_usage["gpu_devices_allocated"].remove(required_gpu)
        
        # Update totals
        self.resource_usage["total_workers"] -= 1
        
        logger.debug(f"Released resources for {worker_id}: {requirements}")


# Global registry instance
worker_registry = WorkerRegistry()


async def initialize_worker_registry():
    """Initialize the global worker registry"""
    await worker_registry.discover_workers()
    logger.info("Worker registry initialized")


async def get_worker_registry() -> WorkerRegistry:
    """Get the global worker registry instance"""
    return worker_registry
