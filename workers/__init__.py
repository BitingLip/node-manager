"""
Node Manager Workers
Comprehensive worker system for distributed AI task execution
Organized into categories: inference, training, utility, and compute
"""

# Base worker classes
from .base_worker import BaseWorker, WorkerStatus, WorkerMetrics
from .worker_pool import WorkerPool, PoolManager
from .worker_registry import WorkerRegistry, WorkerSpec, WorkerInstance, worker_registry, initialize_worker_registry

# Inference workers (AI model inference tasks)
from .inference.base_inference_worker import BaseInferenceWorker, InferenceRequest, InferenceResponse

# Training workers (AI model training tasks)  
from .training.base_training_worker import BaseTrainingWorker, TrainingConfig, TrainingMetrics, TrainingState, TrainingManager

# Utility workers (system and data processing tasks)
from .utility.base_utility_worker import BaseUtilityWorker, UtilityTask, SystemMetrics, FileProcessingWorker, DataProcessingWorker

# Compute workers (intensive processing tasks)
from .compute.base_compute_worker import BaseComputeWorker, ComputeTask, BatchConfig, ComputeMetrics, ComputeWorkerPool

# Legacy worker imports for backwards compatibility
try:
    from .inference.llm_worker import LLMWorker
    from .inference.sd_worker import StableDiffusionWorker
    from .inference.tts_worker import TTSWorker
    from .inference.worker_factory import WorkerFactory
except ImportError:
    # These will be available once we move the files
    LLMWorker = None
    StableDiffusionWorker = None
    TTSWorker = None
    WorkerFactory = None

__all__ = [
    # Base classes
    "BaseWorker",
    "WorkerStatus", 
    "WorkerMetrics",
    "WorkerPool",
    "PoolManager",
    
    # Registry
    "WorkerRegistry",
    "WorkerSpec",
    "WorkerInstance", 
    "worker_registry",
    "initialize_worker_registry",
    
    # Inference workers
    "BaseInferenceWorker",
    "InferenceRequest",
    "InferenceResponse",
    "LLMWorker",
    "StableDiffusionWorker",
    "TTSWorker",
    "WorkerFactory",
    
    # Training workers
    "BaseTrainingWorker",
    "TrainingConfig",
    "TrainingMetrics", 
    "TrainingState",
    "TrainingManager",
    
    # Utility workers
    "BaseUtilityWorker",
    "UtilityTask",
    "SystemMetrics",
    "FileProcessingWorker",
    "DataProcessingWorker",
    
    # Compute workers
    "BaseComputeWorker", 
    "ComputeTask",
    "BatchConfig",
    "ComputeMetrics",
    "ComputeWorkerPool"
]
