"""
Training Worker Base Class
Base class for AI model training workers
Handles training lifecycle, checkpointing, and monitoring
"""

import asyncio
import time
import json
import os
from typing import Dict, List, Optional, Any, Callable
from pathlib import Path
from dataclasses import dataclass, asdict
from abc import ABC, abstractmethod
import structlog

logger = structlog.get_logger(__name__)


@dataclass
class TrainingConfig:
    """Training configuration"""
    model_name: str
    dataset_path: str
    output_dir: str
    epochs: int
    batch_size: int
    learning_rate: float
    checkpoint_steps: int
    validation_steps: int
    gradient_accumulation_steps: int
    mixed_precision: bool
    max_grad_norm: float
    warmup_steps: int
    logging_steps: int
    save_total_limit: int
    evaluation_strategy: str
    load_best_model_at_end: bool
    metric_for_best_model: str
    greater_is_better: bool


@dataclass
class TrainingMetrics:
    """Training metrics"""
    epoch: int
    step: int
    loss: float
    learning_rate: float
    grad_norm: Optional[float]
    eval_loss: Optional[float]
    eval_metrics: Dict[str, float]
    time_per_step: float
    memory_usage_mb: float
    gpu_utilization: float


@dataclass
class TrainingState:
    """Current training state"""
    status: str  # "idle", "training", "paused", "completed", "failed"
    current_epoch: int
    current_step: int
    total_steps: int
    progress_percent: float
    start_time: Optional[float]
    estimated_completion: Optional[float]
    last_checkpoint: Optional[str]
    best_model_path: Optional[str]
    recent_metrics: List[TrainingMetrics]


class BaseTrainingWorker(ABC):
    """
    Base class for training workers
    Provides common training infrastructure and monitoring
    """
    
    def __init__(self, worker_id: str, config: Dict[str, Any]):
        """Initialize training worker"""
        self.worker_id = worker_id
        self.config = config
        
        # Training state
        self.training_state = TrainingState(
            status="idle",
            current_epoch=0,
            current_step=0,
            total_steps=0,
            progress_percent=0.0,
            start_time=None,
            estimated_completion=None,
            last_checkpoint=None,
            best_model_path=None,
            recent_metrics=[]
        )
        
        # Callbacks
        self.progress_callbacks: List[Callable] = []
        self.checkpoint_callbacks: List[Callable] = []
        self.completion_callbacks: List[Callable] = []
        
        # Resource tracking
        self.device_id = config.get("device_id", "cpu")
        self.max_memory_mb = config.get("max_memory_mb", 8192)
        
        logger.info("BaseTrainingWorker initialized", 
                   worker_id=worker_id, 
                   device=self.device_id)
    
    @abstractmethod
    async def setup_training(self, training_config: TrainingConfig) -> bool:
        """Setup training environment and load model"""
        pass
    
    @abstractmethod
    async def start_training(self) -> bool:
        """Start training process"""
        pass
    
    @abstractmethod
    async def pause_training(self) -> bool:
        """Pause training process"""
        pass
    
    @abstractmethod
    async def resume_training(self) -> bool:
        """Resume paused training"""
        pass
    
    @abstractmethod
    async def stop_training(self) -> bool:
        """Stop training and cleanup"""
        pass
    
    @abstractmethod
    async def save_checkpoint(self, checkpoint_dir: str) -> str:
        """Save training checkpoint"""
        pass
    
    @abstractmethod
    async def load_checkpoint(self, checkpoint_path: str) -> bool:
        """Load training checkpoint"""
        pass
    
    async def get_training_state(self) -> TrainingState:
        """Get current training state"""
        return self.training_state
    
    async def get_recent_metrics(self, limit: int = 100) -> List[TrainingMetrics]:
        """Get recent training metrics"""
        return self.training_state.recent_metrics[-limit:]
    
    def add_progress_callback(self, callback: Callable):
        """Add progress update callback"""
        self.progress_callbacks.append(callback)
    
    def add_checkpoint_callback(self, callback: Callable):
        """Add checkpoint save callback"""
        self.checkpoint_callbacks.append(callback)
    
    def add_completion_callback(self, callback: Callable):
        """Add training completion callback"""
        self.completion_callbacks.append(callback)
    
    async def _update_training_state(self, **kwargs):
        """Update training state"""
        for key, value in kwargs.items():
            if hasattr(self.training_state, key):
                setattr(self.training_state, key, value)
        
        # Notify progress callbacks
        for callback in self.progress_callbacks:
            try:
                await callback(self.training_state)
            except Exception as e:
                logger.warning("Progress callback failed", error=str(e))
    
    async def _record_metrics(self, metrics: TrainingMetrics):
        """Record training metrics"""
        self.training_state.recent_metrics.append(metrics)
        
        # Keep only recent metrics (last 1000)
        if len(self.training_state.recent_metrics) > 1000:
            self.training_state.recent_metrics = self.training_state.recent_metrics[-1000:]
        
        # Update state
        await self._update_training_state(
            current_epoch=metrics.epoch,
            current_step=metrics.step,
            progress_percent=min(100.0, (metrics.step / self.training_state.total_steps) * 100)
        )
    
    async def _save_checkpoint_callback(self, checkpoint_path: str):
        """Handle checkpoint save"""
        await self._update_training_state(last_checkpoint=checkpoint_path)
        
        # Notify checkpoint callbacks
        for callback in self.checkpoint_callbacks:
            try:
                await callback(checkpoint_path)
            except Exception as e:
                logger.warning("Checkpoint callback failed", error=str(e))
    
    async def _training_completed(self, success: bool, final_model_path: Optional[str] = None):
        """Handle training completion"""
        status = "completed" if success else "failed"
        await self._update_training_state(
            status=status,
            progress_percent=100.0 if success else self.training_state.progress_percent,
            best_model_path=final_model_path
        )
        
        # Notify completion callbacks
        for callback in self.completion_callbacks:
            try:
                await callback(success, final_model_path)
            except Exception as e:
                logger.warning("Completion callback failed", error=str(e))
    
    def _estimate_completion_time(self) -> Optional[float]:
        """Estimate training completion time"""
        if self.training_state.current_step == 0 or not self.training_state.start_time:
            return None
        
        elapsed_time = time.time() - self.training_state.start_time
        steps_per_second = self.training_state.current_step / elapsed_time
        
        if steps_per_second > 0:
            remaining_steps = self.training_state.total_steps - self.training_state.current_step
            remaining_time = remaining_steps / steps_per_second
            return time.time() + remaining_time
        
        return None
    
    async def get_resource_usage(self) -> Dict[str, Any]:
        """Get current resource usage"""
        # This should be implemented by subclasses with actual monitoring
        return {
            "device_id": self.device_id,
            "memory_allocated_mb": 0,
            "memory_reserved_mb": 0,
            "gpu_utilization_percent": 0,
            "temperature_c": 0
        }
    
    async def validate_model(self, validation_dataset: str) -> Dict[str, float]:
        """Validate current model"""
        # This should be implemented by subclasses
        return {}
    
    def save_training_log(self, log_path: str):
        """Save training log to file"""
        log_data = {
            "worker_id": self.worker_id,
            "config": self.config,
            "training_state": asdict(self.training_state),
            "metrics": [asdict(m) for m in self.training_state.recent_metrics]
        }
        
        with open(log_path, 'w') as f:
            json.dump(log_data, f, indent=2, default=str)
        
        logger.info(f"Training log saved to {log_path}")


class TrainingManager:
    """
    Manages multiple training workers
    Handles scheduling, resource allocation, and monitoring
    """
    
    def __init__(self):
        """Initialize training manager"""
        self.active_workers: Dict[str, BaseTrainingWorker] = {}
        self.training_queue: List[Dict[str, Any]] = []
        self.completed_trainings: List[Dict[str, Any]] = []
        
        logger.info("TrainingManager initialized")
    
    async def submit_training_job(self, job_config: Dict[str, Any]) -> str:
        """Submit training job to queue"""
        job_id = f"training_{int(time.time())}_{len(self.training_queue)}"
        
        job_data = {
            "job_id": job_id,
            "config": job_config,
            "submitted_at": time.time(),
            "status": "queued"
        }
        
        self.training_queue.append(job_data)
        logger.info(f"Training job submitted: {job_id}")
        
        return job_id
    
    async def start_next_training(self) -> Optional[str]:
        """Start next training job from queue"""
        if not self.training_queue:
            return None
        
        job_data = self.training_queue.pop(0)
        job_id = job_data["job_id"]
        
        # Create appropriate worker based on job type
        worker_class = self._get_worker_class(job_data["config"]["worker_type"])
        if not worker_class:
            logger.error(f"Unknown worker type: {job_data['config']['worker_type']}")
            return None
        
        # Create and start worker
        worker = worker_class(job_id, job_data["config"])
        self.active_workers[job_id] = worker
        
        # Setup progress monitoring
        worker.add_completion_callback(self._on_training_completed)
        
        logger.info(f"Starting training job: {job_id}")
        return job_id
    
    def _get_worker_class(self, worker_type: str):
        """Get worker class for type"""
        # This will be implemented with specific worker imports
        worker_classes = {
            "llm_training": None,  # Will be LLMTrainingWorker
            "stable_diffusion_training": None,  # Will be SDTrainingWorker
            "custom_training": None  # Will be CustomTrainingWorker
        }
        return worker_classes.get(worker_type)
    
    async def _on_training_completed(self, success: bool, final_model_path: Optional[str]):
        """Handle training completion"""
        # Move completed job to completed list
        logger.info(f"Training completed", success=success, model_path=final_model_path)
    
    async def get_training_status(self) -> Dict[str, Any]:
        """Get status of all training jobs"""
        return {
            "active_workers": len(self.active_workers),
            "queued_jobs": len(self.training_queue),
            "completed_jobs": len(self.completed_trainings),
            "worker_status": {
                worker_id: await worker.get_training_state()
                for worker_id, worker in self.active_workers.items()
            }
        }
    
    async def stop_training(self, job_id: str) -> bool:
        """Stop specific training job"""
        if job_id in self.active_workers:
            worker = self.active_workers[job_id]
            success = await worker.stop_training()
            if success:
                del self.active_workers[job_id]
            return success
        return False
    
    async def pause_training(self, job_id: str) -> bool:
        """Pause specific training job"""
        if job_id in self.active_workers:
            worker = self.active_workers[job_id]
            return await worker.pause_training()
        return False
    
    async def resume_training(self, job_id: str) -> bool:
        """Resume specific training job"""
        if job_id in self.active_workers:
            worker = self.active_workers[job_id]
            return await worker.resume_training()
        return False
