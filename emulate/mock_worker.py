"""
Mock Worker Implementation
Simulates worker processes for testing and development
"""

import asyncio
import random
import time
import json
from typing import Dict, List, Optional, Any, Callable
from datetime import datetime, timedelta
from enum import Enum
import structlog
import uuid

# Import the real types from core
import sys
from pathlib import Path
sys.path.append(str(Path(__file__).parent.parent))

from core.worker_manager import WorkerStatus, WorkerType
from core.task_dispatcher import TaskStatus, TaskPriority, TaskInfo

logger = structlog.get_logger(__name__)


class MockWorkerType(Enum):
    """Extended worker types for testing"""
    LLM_SMALL = "llm_small"
    LLM_LARGE = "llm_large"
    STABLE_DIFFUSION = "stable_diffusion"
    TTS_FAST = "tts_fast"
    TTS_QUALITY = "tts_quality"
    IMAGE_TO_TEXT = "image_to_text"
    GENERIC = "generic"
    HEAVY_COMPUTE = "heavy_compute"


class MockWorker:
    """
    Mock worker that simulates different AI workloads
    """
    
    def __init__(self, worker_id: str, worker_type: MockWorkerType, 
                 config: Optional[Dict[str, Any]] = None):
        self.worker_id = worker_id
        self.worker_type = worker_type
        self.config = config or {}
        self.status = WorkerStatus.STARTING
        self.current_task: Optional[TaskInfo] = None
        self.capabilities = self._get_capabilities()
        self.resource_requirements = self._get_resource_requirements()
        self.performance_profile = self._get_performance_profile()
        self.error_rate = self.config.get('error_rate', 0.05)  # 5% default error rate
        self.startup_time = self.config.get('startup_time', 2.0)  # 2s startup
        self.last_heartbeat = datetime.now()
        self.total_tasks = 0
        self.failed_tasks = 0
        
    def _get_capabilities(self) -> Dict[str, Any]:
        """Get worker capabilities based on type"""
        capabilities = {
            MockWorkerType.LLM_SMALL: {
                'max_tokens': 4096,
                'models': ['gpt-3.5-turbo', 'claude-3-haiku'],
                'tasks': ['text_generation', 'summarization', 'qa']
            },
            MockWorkerType.LLM_LARGE: {
                'max_tokens': 32768,
                'models': ['gpt-4', 'claude-3-opus', 'llama-70b'],
                'tasks': ['text_generation', 'reasoning', 'code_generation']
            },
            MockWorkerType.STABLE_DIFFUSION: {
                'models': ['sd-1.5', 'sd-xl', 'sd-3'],
                'tasks': ['text_to_image', 'image_to_image', 'inpainting'],
                'max_resolution': '1024x1024'
            },
            MockWorkerType.TTS_FAST: {
                'voices': ['en-US-neural', 'en-GB-neural'],
                'tasks': ['text_to_speech'],
                'quality': 'standard'
            },
            MockWorkerType.TTS_QUALITY: {
                'voices': ['en-US-premium', 'en-GB-premium', 'multilingual'],
                'tasks': ['text_to_speech'],
                'quality': 'premium'
            },
            MockWorkerType.IMAGE_TO_TEXT: {
                'models': ['blip-2', 'llava', 'gpt-4-vision'],
                'tasks': ['image_captioning', 'image_qa', 'ocr']
            },
            MockWorkerType.GENERIC: {
                'tasks': ['data_processing', 'file_conversion', 'validation']
            },
            MockWorkerType.HEAVY_COMPUTE: {
                'tasks': ['training', 'fine_tuning', 'batch_processing'],
                'gpu_required': True
            }
        }
        return capabilities.get(self.worker_type, {})
    
    def _get_resource_requirements(self) -> Dict[str, Any]:
        """Get resource requirements based on worker type"""
        requirements = {
            MockWorkerType.LLM_SMALL: {'cpu_cores': 2, 'memory_mb': 4096, 'gpu_memory': {}},
            MockWorkerType.LLM_LARGE: {'cpu_cores': 4, 'memory_mb': 16384, 'gpu_memory': {0: 8192}},
            MockWorkerType.STABLE_DIFFUSION: {'cpu_cores': 2, 'memory_mb': 8192, 'gpu_memory': {0: 6144}},
            MockWorkerType.TTS_FAST: {'cpu_cores': 1, 'memory_mb': 2048, 'gpu_memory': {}},
            MockWorkerType.TTS_QUALITY: {'cpu_cores': 2, 'memory_mb': 4096, 'gpu_memory': {}},
            MockWorkerType.IMAGE_TO_TEXT: {'cpu_cores': 2, 'memory_mb': 4096, 'gpu_memory': {0: 4096}},
            MockWorkerType.GENERIC: {'cpu_cores': 1, 'memory_mb': 1024, 'gpu_memory': {}},
            MockWorkerType.HEAVY_COMPUTE: {'cpu_cores': 8, 'memory_mb': 32768, 'gpu_memory': {0: 16384, 1: 16384}}
        }
        return requirements.get(self.worker_type, {'cpu_cores': 1, 'memory_mb': 512, 'gpu_memory': {}})
    
    def _get_performance_profile(self) -> Dict[str, Any]:
        """Get performance characteristics for simulation"""
        profiles = {
            MockWorkerType.LLM_SMALL: {'avg_time': 2.0, 'variance': 0.5},
            MockWorkerType.LLM_LARGE: {'avg_time': 8.0, 'variance': 2.0},
            MockWorkerType.STABLE_DIFFUSION: {'avg_time': 15.0, 'variance': 5.0},
            MockWorkerType.TTS_FAST: {'avg_time': 1.0, 'variance': 0.2},
            MockWorkerType.TTS_QUALITY: {'avg_time': 5.0, 'variance': 1.0},
            MockWorkerType.IMAGE_TO_TEXT: {'avg_time': 3.0, 'variance': 1.0},
            MockWorkerType.GENERIC: {'avg_time': 0.5, 'variance': 0.1},
            MockWorkerType.HEAVY_COMPUTE: {'avg_time': 120.0, 'variance': 30.0}
        }
        return profiles.get(self.worker_type, {'avg_time': 1.0, 'variance': 0.2})
    
    async def start(self) -> bool:
        """Start the mock worker"""
        logger.info("Starting mock worker", worker_id=self.worker_id, type=self.worker_type.value)
        
        # Simulate startup time
        await asyncio.sleep(self.startup_time)
        
        # Random startup failure
        if random.random() < self.config.get('startup_failure_rate', 0.1):
            self.status = WorkerStatus.ERROR
            logger.error("Mock worker failed to start", worker_id=self.worker_id)
            return False
        
        self.status = WorkerStatus.READY
        logger.info("Mock worker started successfully", worker_id=self.worker_id)
        return True
    
    async def stop(self) -> bool:
        """Stop the mock worker"""
        logger.info("Stopping mock worker", worker_id=self.worker_id)
        self.status = WorkerStatus.STOPPING
        
        # Simulate shutdown time
        await asyncio.sleep(0.5)
        
        self.status = WorkerStatus.STOPPED
        logger.info("Mock worker stopped", worker_id=self.worker_id)
        return True
    
    async def execute_task(self, task: TaskInfo) -> Dict[str, Any]:
        """Execute a task and return results"""
        if self.status != WorkerStatus.READY:
            raise RuntimeError(f"Worker {self.worker_id} not ready (status: {self.status})")
        
        self.current_task = task
        self.status = WorkerStatus.BUSY
        self.total_tasks += 1
        
        logger.info("Mock worker executing task", 
                   worker_id=self.worker_id, 
                   task_id=task.task_id,
                   task_type=task.task_type)
        
        try:
            # Simulate task execution time
            execution_time = max(0.1, random.gauss(
                self.performance_profile['avg_time'],
                self.performance_profile['variance']
            ))
            
            await asyncio.sleep(execution_time)
            
            # Simulate random failures
            if random.random() < self.error_rate:
                self.failed_tasks += 1
                error_msg = f"Simulated failure in {self.worker_type.value} worker"
                logger.warning("Mock worker task failed", 
                             worker_id=self.worker_id,
                             task_id=task.task_id,
                             error=error_msg)
                return {
                    'success': False,
                    'error': error_msg,
                    'execution_time': execution_time
                }
            
            # Generate mock result based on task type
            result = self._generate_mock_result(task)
            result['execution_time'] = execution_time
            result['worker_id'] = self.worker_id
            result['success'] = True
            
            logger.info("Mock worker task completed", 
                       worker_id=self.worker_id,
                       task_id=task.task_id,
                       execution_time=execution_time)
            
            return result
            
        finally:
            self.current_task = None
            self.status = WorkerStatus.READY
            self.last_heartbeat = datetime.now()
    
    def _generate_mock_result(self, task: TaskInfo) -> Dict[str, Any]:
        """Generate realistic mock results based on task type"""
        task_type = task.task_type
        
        if task_type == 'text_generation':
            return {
                'generated_text': f"Mock generated text for prompt: {task.task_data.get('prompt', '')[:50]}...",
                'tokens_generated': random.randint(50, 500),
                'model_used': random.choice(self.capabilities.get('models', ['mock-model']))
            }
        
        elif task_type == 'text_to_image':
            return {
                'image_url': f"mock://generated-image-{uuid.uuid4().hex[:8]}.png",
                'resolution': self.capabilities.get('max_resolution', '512x512'),
                'model_used': random.choice(self.capabilities.get('models', ['mock-sd']))
            }
        
        elif task_type == 'text_to_speech':
            return {
                'audio_url': f"mock://generated-audio-{uuid.uuid4().hex[:8]}.wav",
                'duration_seconds': random.uniform(5.0, 30.0),
                'voice_used': random.choice(self.capabilities.get('voices', ['mock-voice']))
            }
        
        elif task_type == 'image_to_text':
            return {
                'caption': f"Mock caption for image: {task.task_data.get('image_url', 'unknown')}",
                'confidence': random.uniform(0.8, 0.99),
                'model_used': random.choice(self.capabilities.get('models', ['mock-vision']))
            }
        
        else:
            return {
                'result': f"Mock result for {task_type}",
                'data': task.task_data,
                'processed': True
            }
    
    def get_status(self) -> Dict[str, Any]:
        """Get current worker status"""
        return {
            'worker_id': self.worker_id,
            'worker_type': self.worker_type.value,
            'status': self.status.value,
            'current_task': self.current_task.task_id if self.current_task else None,
            'capabilities': self.capabilities,
            'resource_requirements': self.resource_requirements,
            'total_tasks': self.total_tasks,
            'failed_tasks': self.failed_tasks,
            'success_rate': (self.total_tasks - self.failed_tasks) / max(1, self.total_tasks),
            'last_heartbeat': self.last_heartbeat.isoformat()
        }
    
    def get_metrics(self) -> Dict[str, Any]:
        """Get worker performance metrics"""
        return {
            'total_tasks_completed': self.total_tasks,
            'failed_tasks': self.failed_tasks,
            'success_rate': (self.total_tasks - self.failed_tasks) / max(1, self.total_tasks),
            'error_rate': self.error_rate,
            'avg_execution_time': self.performance_profile['avg_time'],
            'uptime': (datetime.now() - self.last_heartbeat).total_seconds()
        }
