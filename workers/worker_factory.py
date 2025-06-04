"""
Worker Factory
Factory pattern for creating different types of workers
Provides unified interface for worker instantiation and management
"""

from typing import Dict, List, Optional, Any, Type
import structlog
from pathlib import Path

from .base_worker import BaseWorker
from .inference.llm_worker import LLMWorker  
from .inference.sd_worker import StableDiffusionWorker
from .inference.tts_worker import TTSWorker

logger = structlog.get_logger(__name__)


class WorkerFactory:
    """
    Factory for creating worker instances
    Handles worker type registration and instantiation
    """
    
    # Registry of available worker types
    WORKER_TYPES = {
        # Inference workers
        'llm': LLMWorker,
        'stable_diffusion': StableDiffusionWorker,
        'tts': TTSWorker,
        
        # Training workers (placeholders for future implementation)
        # 'llm_training': LLMTrainingWorker,
        # 'sd_training': SDTrainingWorker,
        
        # Utility workers (placeholders)
        # 'file_processor': FileProcessorWorker,
        # 'data_augmentation': DataAugmentationWorker,
        
        # Compute workers (placeholders)
        # 'general_compute': GeneralComputeWorker,
        # 'gpu_compute': GPUComputeWorker,
    }
    
    @classmethod
    def create_worker(cls, worker_type: str, worker_id: str, config: Dict[str, Any]) -> Optional[BaseWorker]:
        """
        Create a worker instance of the specified type
        
        Args:
            worker_type: Type of worker to create
            worker_id: Unique identifier for the worker
            config: Configuration dictionary for the worker
            
        Returns:
            Worker instance or None if creation failed
        """
        try:
            if worker_type not in cls.WORKER_TYPES:
                logger.error(f"Unknown worker type: {worker_type}")
                logger.info(f"Available worker types: {list(cls.WORKER_TYPES.keys())}")
                return None
            
            worker_class = cls.WORKER_TYPES[worker_type]
            worker_instance = worker_class(worker_id, config)
            
            logger.info(f"Created {worker_type} worker: {worker_id}")
            return worker_instance
            
        except Exception as e:
            logger.error(f"Failed to create {worker_type} worker {worker_id}: {e}")
            return None
    
    @classmethod
    def get_available_types(cls) -> List[str]:
        """Get list of available worker types"""
        return list(cls.WORKER_TYPES.keys())
    
    @classmethod
    def get_worker_capabilities(cls, worker_type: str) -> Dict[str, Any]:
        """Get capabilities for a specific worker type"""
        try:
            if worker_type not in cls.WORKER_TYPES:
                return {}
            
            # Create a temporary instance to get capabilities
            temp_worker = cls.WORKER_TYPES[worker_type]("temp", {})
            return temp_worker.get_capabilities()
            
        except Exception as e:
            logger.error(f"Failed to get capabilities for {worker_type}: {e}")
            return {}
    
    @classmethod
    def register_worker_type(cls, worker_type: str, worker_class: Type[BaseWorker]):
        """Register a new worker type"""
        try:
            if not issubclass(worker_class, BaseWorker):
                raise ValueError("Worker class must inherit from BaseWorker")
            
            cls.WORKER_TYPES[worker_type] = worker_class
            logger.info(f"Registered worker type: {worker_type}")
            
        except Exception as e:
            logger.error(f"Failed to register worker type {worker_type}: {e}")
    
    @classmethod
    def validate_config(cls, worker_type: str, config: Dict[str, Any]) -> bool:
        """Validate configuration for a worker type"""
        try:
            if worker_type not in cls.WORKER_TYPES:
                logger.error(f"Unknown worker type: {worker_type}")
                return False
            
            # Basic validation - can be extended per worker type
            required_fields = ['max_memory_mb', 'timeout_seconds']
            for field in required_fields:
                if field not in config:
                    logger.warning(f"Missing recommended config field: {field}")
            
            return True
            
        except Exception as e:
            logger.error(f"Config validation failed for {worker_type}: {e}")
            return False
    
    @classmethod
    def get_resource_requirements(cls, worker_type: str, config: Dict[str, Any]) -> Dict[str, Any]:
        """Get resource requirements for a worker type"""
        try:
            if worker_type not in cls.WORKER_TYPES:
                return {}
            
            # Default resource requirements
            base_requirements = {
                'cpu_cores': 1,
                'memory_mb': 1024,
                'gpu_required': False,
                'gpu_memory_mb': 0
            }
            
            # Worker-specific requirements
            worker_requirements = {
                'llm': {
                    'cpu_cores': 2,
                    'memory_mb': 4096,
                    'gpu_required': True,
                    'gpu_memory_mb': 4096
                },
                'stable_diffusion': {
                    'cpu_cores': 2,
                    'memory_mb': 8192,
                    'gpu_required': True,
                    'gpu_memory_mb': 8192
                },
                'tts': {
                    'cpu_cores': 1,
                    'memory_mb': 2048,
                    'gpu_required': False,
                    'gpu_memory_mb': 2048
                }
            }
            
            requirements = base_requirements.copy()
            if worker_type in worker_requirements:
                requirements.update(worker_requirements[worker_type])
            
            # Override with config if provided
            config_requirements = config.get('resource_requirements', {})
            requirements.update(config_requirements)
            
            return requirements
            
        except Exception as e:
            logger.error(f"Failed to get resource requirements for {worker_type}: {e}")
            return {}


class WorkerPool:
    """
    Pool for managing multiple worker instances
    Provides load balancing and health monitoring
    """
    
    def __init__(self, max_workers: int = 10):
        self.max_workers = max_workers
        self.workers: Dict[str, BaseWorker] = {}
        self.worker_assignments: Dict[str, List[str]] = {}  # worker_id -> [task_ids]
        
        logger.info(f"WorkerPool initialized with max_workers={max_workers}")
    
    def add_worker(self, worker: BaseWorker) -> bool:
        """Add a worker to the pool"""
        try:
            if len(self.workers) >= self.max_workers:
                logger.warning(f"Worker pool is full ({self.max_workers} workers)")
                return False
            
            worker_id = worker.worker_id
            if worker_id in self.workers:
                logger.warning(f"Worker {worker_id} already in pool")
                return False
            
            self.workers[worker_id] = worker
            self.worker_assignments[worker_id] = []
            
            logger.info(f"Added worker {worker_id} to pool")
            return True
            
        except Exception as e:
            logger.error(f"Failed to add worker to pool: {e}")
            return False
    
    def remove_worker(self, worker_id: str) -> bool:
        """Remove a worker from the pool"""
        try:
            if worker_id not in self.workers:
                logger.warning(f"Worker {worker_id} not in pool")
                return False
            
            # Check if worker has active tasks
            if self.worker_assignments.get(worker_id, []):
                logger.warning(f"Worker {worker_id} has active tasks, cannot remove")
                return False
            
            del self.workers[worker_id]
            del self.worker_assignments[worker_id]
            
            logger.info(f"Removed worker {worker_id} from pool")
            return True
            
        except Exception as e:
            logger.error(f"Failed to remove worker {worker_id}: {e}")
            return False
    
    def get_available_worker(self, worker_type: Optional[str] = None) -> Optional[BaseWorker]:
        """Get an available worker, optionally of specific type"""
        try:
            for worker_id, worker in self.workers.items():
                # Check if worker is available
                if worker.state.value not in ['ready', 'idle']:
                    continue
                
                # Check worker type if specified
                if worker_type and worker.get_capabilities().get('worker_type') != worker_type:
                    continue
                
                # Check if worker has capacity for more tasks
                current_assignments = len(self.worker_assignments.get(worker_id, []))
                if current_assignments < worker.config.get('max_concurrent_tasks', 1):
                    return worker
            
            return None
            
        except Exception as e:
            logger.error(f"Failed to get available worker: {e}")
            return None
    
    def assign_task(self, worker_id: str, task_id: str) -> bool:
        """Assign a task to a worker"""
        try:
            if worker_id not in self.worker_assignments:
                return False
            
            self.worker_assignments[worker_id].append(task_id)
            logger.debug(f"Assigned task {task_id} to worker {worker_id}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to assign task {task_id} to worker {worker_id}: {e}")
            return False
    
    def complete_task(self, worker_id: str, task_id: str) -> bool:
        """Mark a task as completed for a worker"""
        try:
            if worker_id not in self.worker_assignments:
                return False
            
            if task_id in self.worker_assignments[worker_id]:
                self.worker_assignments[worker_id].remove(task_id)
                logger.debug(f"Completed task {task_id} for worker {worker_id}")
                return True
            
            return False
            
        except Exception as e:
            logger.error(f"Failed to complete task {task_id} for worker {worker_id}: {e}")
            return False
    
    def get_pool_status(self) -> Dict[str, Any]:
        """Get status of the worker pool"""
        return {
            'total_workers': len(self.workers),
            'max_workers': self.max_workers,
            'workers': {
                worker_id: {
                    'state': worker.state.value,
                    'active_tasks': len(self.worker_assignments.get(worker_id, [])),
                    'capabilities': worker.get_capabilities()
                }
                for worker_id, worker in self.workers.items()
            }
        }
