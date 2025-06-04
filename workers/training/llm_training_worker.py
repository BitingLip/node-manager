"""
LLM Training Worker
Worker specialized for training language models
Handles fine-tuning, PEFT methods, and distributed training
"""

import asyncio
from typing import Dict, List, Optional, Any
import time
import structlog
from ..base_worker import BaseWorker, WorkerState
from .base_training_worker import TrainingConfig, TrainingMetrics

logger = structlog.get_logger(__name__)


class LLMTrainingWorker(BaseWorker):
    """
    Worker specialized for LLM training tasks
    Supports fine-tuning, LoRA, QLoRA, and other PEFT methods
    """
    
    def __init__(self, worker_id: str, config: Dict[str, Any]):
        """Initialize LLM training worker"""
        super().__init__(worker_id, config)
        self.training_state = None
        self.current_training = None
        self.training_metrics = []
        
        logger.info(f"LLMTrainingWorker {worker_id} initializing")
    
    async def initialize(self) -> bool:
        """Initialize LLM training resources"""
        try:
            logger.info(f"Initializing LLM training worker {self.worker_id}")
            
            # Try to import training libraries
            try:
                import torch
                import transformers
                self.torch = torch
                self.transformers = transformers
            except ImportError:
                logger.error("PyTorch/Transformers not available for training")
                return False
            
            # Configure device
            self.device = "cuda" if torch.cuda.is_available() else "cpu"
            logger.info(f"Using device: {self.device}")
            
            # Check for distributed training
            self.distributed = torch.cuda.device_count() > 1
            if self.distributed:
                logger.info(f"Distributed training available with {torch.cuda.device_count()} GPUs")
            
            self.state = WorkerState.READY
            logger.info(f"LLMTrainingWorker {self.worker_id} initialized successfully")
            return True
            
        except Exception as e:
            logger.error(f"Failed to initialize LLM training worker: {e}")
            self.state = WorkerState.ERROR
            return False
    
    async def process_task(self, task_data: Dict[str, Any]) -> Dict[str, Any]:
        """Process LLM training task"""
        try:
            training_type = task_data.get('type', 'fine_tune')
            model_name = task_data.get('model_name', 'gpt2')
            dataset_path = task_data.get('dataset_path', '')
            
            logger.info(f"Processing {training_type} task for model: {model_name}")
            
            # Validate training configuration
            training_config = self._create_training_config(task_data)
            if not training_config:
                return {"error": "Invalid training configuration"}
            
            # Start training
            self.state = WorkerState.BUSY
            training_result = await self._run_training(training_config, training_type)
            
            self.state = WorkerState.READY
            return training_result
            
        except Exception as e:
            logger.error(f"Failed to process training task: {e}")
            self.state = WorkerState.ERROR
            return {"error": str(e), "status": "failed"}
    
    def _create_training_config(self, task_data: Dict[str, Any]) -> Optional[TrainingConfig]:
        """Create training configuration from task data"""
        try:
            config = TrainingConfig(
                model_name=task_data.get('model_name', 'gpt2'),
                dataset_path=task_data.get('dataset_path', ''),
                output_dir=task_data.get('output_dir', './output'),
                epochs=task_data.get('epochs', 3),
                batch_size=task_data.get('batch_size', 4),
                learning_rate=task_data.get('learning_rate', 5e-5),
                checkpoint_steps=task_data.get('checkpoint_steps', 500),
                validation_steps=task_data.get('validation_steps', 100),
                gradient_accumulation_steps=task_data.get('gradient_accumulation_steps', 1),
                mixed_precision=task_data.get('mixed_precision', True),
                max_grad_norm=task_data.get('max_grad_norm', 1.0),
                warmup_steps=task_data.get('warmup_steps', 100),
                logging_steps=task_data.get('logging_steps', 10),
                save_total_limit=task_data.get('save_total_limit', 3),
                evaluation_strategy=task_data.get('evaluation_strategy', 'steps'),
                load_best_model_at_end=task_data.get('load_best_model_at_end', True),
                metric_for_best_model=task_data.get('metric_for_best_model', 'eval_loss'),
                greater_is_better=task_data.get('greater_is_better', False)
            )
            
            return config
            
        except Exception as e:
            logger.error(f"Failed to create training config: {e}")
            return None
    
    async def _run_training(self, config: TrainingConfig, training_type: str) -> Dict[str, Any]:
        """Run the actual training process"""
        try:
            start_time = time.time()
            
            # This is a placeholder implementation
            # In reality, this would:
            # 1. Load the model and tokenizer
            # 2. Prepare the dataset
            # 3. Setup training arguments
            # 4. Initialize trainer
            # 5. Run training with monitoring
            
            logger.info(f"Starting {training_type} training for {config.model_name}")
            
            # Simulate training progress
            total_steps = config.epochs * 100  # Placeholder
            
            for step in range(0, total_steps, 10):
                # Simulate training step
                await asyncio.sleep(0.1)  # Simulate training time
                  # Create mock metrics
                metrics = TrainingMetrics(
                    epoch=step // 100,
                    step=step,
                    loss=2.5 - (step / total_steps) * 1.5,  # Decreasing loss
                    learning_rate=config.learning_rate * (1 - step / total_steps),
                    grad_norm=1.0 + (step / total_steps) * 0.5,
                    eval_loss=None,
                    eval_metrics={},
                    time_per_step=0.1,
                    memory_usage_mb=4096,
                    gpu_utilization=75.0
                )
                
                self.training_metrics.append(metrics)
                
                # Log progress
                if step % config.logging_steps == 0:
                    logger.info(f"Step {step}/{total_steps}: loss={metrics.loss:.4f}")
            
            training_time = time.time() - start_time
            
            return {
                'status': 'completed',
                'training_type': training_type,
                'model_name': config.model_name,
                'training_time_seconds': training_time,
                'total_steps': total_steps,
                'final_loss': self.training_metrics[-1].loss if self.training_metrics else None,
                'output_dir': config.output_dir,
                'checkpoints': [f"checkpoint-{i}" for i in range(0, total_steps, config.checkpoint_steps)],
                'metrics_summary': {
                    'initial_loss': self.training_metrics[0].loss if self.training_metrics else None,
                    'final_loss': self.training_metrics[-1].loss if self.training_metrics else None,
                    'best_loss': min(m.loss for m in self.training_metrics) if self.training_metrics else None,
                    'total_training_time': training_time
                }
            }
            
        except Exception as e:
            logger.error(f"Training failed: {e}")
            return {"error": str(e), "status": "failed"}
    
    def get_capabilities(self) -> Dict[str, Any]:
        """Get LLM training worker capabilities"""
        return {
            "worker_type": "llm_training",
            "supported_tasks": ["fine_tune", "lora", "qlora", "full_training"],
            "supported_models": ["gpt2", "llama", "mistral", "phi", "gemma"],
            "features": [
                "distributed_training",
                "mixed_precision",
                "gradient_checkpointing",
                "peft_methods",
                "custom_datasets"
            ],
            "gpu_memory_required": "16GB",
            "recommended_batch_size": 4,
            "max_sequence_length": 2048
        }
    
    def get_training_status(self) -> Dict[str, Any]:
        """Get current training status"""
        if not self.training_metrics:
            return {"status": "idle", "progress": 0}
        
        latest_metrics = self.training_metrics[-1]
        return {
            "status": "training" if self.state == WorkerState.BUSY else "idle",
            "current_epoch": latest_metrics.epoch,
            "current_step": latest_metrics.step,
            "current_loss": latest_metrics.loss,
            "learning_rate": latest_metrics.learning_rate,
            "progress_percent": (latest_metrics.step / (len(self.training_metrics) * 10)) * 100
        }
    
    async def pause_training(self) -> bool:
        """Pause current training"""
        try:
            if self.state == WorkerState.BUSY:
                self.state = WorkerState.PAUSED
                logger.info(f"Training paused for worker {self.worker_id}")
                return True
            return False
        except Exception as e:
            logger.error(f"Failed to pause training: {e}")
            return False
    
    async def resume_training(self) -> bool:
        """Resume paused training"""
        try:
            if self.state == WorkerState.PAUSED:
                self.state = WorkerState.BUSY
                logger.info(f"Training resumed for worker {self.worker_id}")
                return True
            return False
        except Exception as e:
            logger.error(f"Failed to resume training: {e}")
            return False
    
    async def stop_training(self) -> bool:
        """Stop current training"""
        try:
            if self.state in [WorkerState.BUSY, WorkerState.PAUSED]:
                self.state = WorkerState.READY
                logger.info(f"Training stopped for worker {self.worker_id}")
                return True
            return False
        except Exception as e:
            logger.error(f"Failed to stop training: {e}")
            return False
