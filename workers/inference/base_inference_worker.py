"""
Inference Worker Base Class
Base class for AI model inference workers
Handles model loading, request batching, and response generation
"""

import asyncio
import time
import torch
import numpy as np
from typing import Dict, List, Optional, Any, Union, Tuple
from dataclasses import dataclass, asdict
from abc import ABC, abstractmethod
import structlog

logger = structlog.get_logger(__name__)


@dataclass
class InferenceRequest:
    """Individual inference request"""
    request_id: str
    input_data: Any
    model_name: str
    parameters: Dict[str, Any]
    priority: int
    submitted_at: float
    started_at: Optional[float]
    completed_at: Optional[float]
    result: Optional[Any]
    error: Optional[str]
    metadata: Dict[str, Any]


@dataclass 
class InferenceResponse:
    """Inference response"""
    request_id: str
    success: bool
    result: Optional[Any]
    error: Optional[str]
    processing_time_ms: float
    model_used: str
    metadata: Dict[str, Any]


class BaseInferenceWorker(ABC):
    """
    Base class for inference workers
    Provides common inference infrastructure and batching
    """
    
    def __init__(self, worker_id: str, config: Dict[str, Any]):
        """Initialize inference worker"""
        self.worker_id = worker_id
        self.config = config
        
        # Model configuration
        self.model_name = config.get("model_name", "unknown")
        self.model_path = config.get("model_path")
        self.device = config.get("device", "cpu")
        self.max_batch_size = config.get("max_batch_size", 1)
        self.max_sequence_length = config.get("max_sequence_length", 512)
        
        # Request handling
        self.request_queue: asyncio.Queue = asyncio.Queue()
        self.active_requests: Dict[str, InferenceRequest] = {}
        self.completed_requests: List[InferenceRequest] = []
        
        # Model state
        self.model = None
        self.tokenizer = None
        self.is_loaded = False
        self.is_running = False
        
        # Performance tracking
        self.total_requests = 0
        self.total_processing_time = 0.0
        self.average_response_time = 0.0
        self.requests_per_second = 0.0
        
        # Worker task
        self.worker_task: Optional[asyncio.Task] = None
        
        logger.info("BaseInferenceWorker initialized", 
                   worker_id=worker_id,
                   model_name=self.model_name,
                   device=self.device)
    
    @abstractmethod
    async def load_model(self) -> bool:
        """Load the AI model and tokenizer"""
        pass
    
    @abstractmethod
    async def unload_model(self):
        """Unload the AI model and free resources"""
        pass
    
    @abstractmethod
    async def inference(self, input_data: Any, parameters: Dict[str, Any]) -> Any:
        """Run inference on input data"""
        pass
    
    @abstractmethod
    async def batch_inference(self, requests: List[InferenceRequest]) -> List[Any]:
        """Run batch inference (optional optimization)"""
        # Default implementation processes requests individually
        results = []
        for request in requests:
            result = await self.inference(request.input_data, request.parameters)
            results.append(result)
        return results
    
    async def start(self):
        """Start the inference worker"""
        if self.is_running:
            logger.warning("Inference worker already running", worker_id=self.worker_id)
            return
        
        # Load model
        if not self.is_loaded:
            success = await self.load_model()
            if not success:
                logger.error("Failed to load model", worker_id=self.worker_id)
                return False
        
        self.is_running = True
        self.worker_task = asyncio.create_task(self._inference_loop())
        
        logger.info("Inference worker started", worker_id=self.worker_id)
        return True
    
    async def stop(self):
        """Stop the inference worker"""
        if not self.is_running:
            return
        
        self.is_running = False
        
        if self.worker_task:
            self.worker_task.cancel()
            try:
                await self.worker_task
            except asyncio.CancelledError:
                pass
        
        # Unload model
        await self.unload_model()
        
        logger.info("Inference worker stopped", worker_id=self.worker_id)
    
    async def submit_request(self, 
                            input_data: Any,
                            model_name: Optional[str] = None,
                            parameters: Optional[Dict[str, Any]] = None,
                            priority: int = 0,
                            metadata: Optional[Dict[str, Any]] = None) -> str:
        """Submit inference request"""
        
        request_id = f"{self.worker_id}_{int(time.time() * 1000000)}"
        
        request = InferenceRequest(
            request_id=request_id,
            input_data=input_data,
            model_name=model_name or self.model_name,
            parameters=parameters or {},
            priority=priority,
            submitted_at=time.time(),
            started_at=None,
            completed_at=None,
            result=None,
            error=None,
            metadata=metadata or {}
        )
        
        await self.request_queue.put(request)
        logger.debug("Inference request submitted", request_id=request_id)
        
        return request_id
    
    async def get_request_result(self, request_id: str) -> Optional[InferenceRequest]:
        """Get result of specific request"""
        # Check active requests
        if request_id in self.active_requests:
            return self.active_requests[request_id]
        
        # Check completed requests
        for request in self.completed_requests:
            if request.request_id == request_id:
                return request
        
        return None
    
    async def wait_for_result(self, request_id: str, timeout: Optional[float] = None) -> Optional[InferenceResponse]:
        """Wait for request completion and return response"""
        start_time = time.time()
        
        while True:
            request = await self.get_request_result(request_id)
            if request and request.completed_at is not None:
                return InferenceResponse(
                    request_id=request_id,
                    success=request.error is None,
                    result=request.result,
                    error=request.error,
                    processing_time_ms=(request.completed_at - request.started_at) * 1000 if request.started_at else 0,
                    model_used=request.model_name,
                    metadata=request.metadata
                )
            
            if timeout and (time.time() - start_time) > timeout:
                return None
            
            await asyncio.sleep(0.01)
    
    async def _inference_loop(self):
        """Main inference processing loop"""
        logger.debug("Inference loop started")
        
        while self.is_running:
            try:
                # Get batch of requests
                requests = await self._get_batch_requests()
                if not requests:
                    continue
                
                # Process batch
                await self._process_request_batch(requests)
                
            except asyncio.CancelledError:
                logger.debug("Inference loop cancelled")
                break
            except Exception as e:
                logger.error("Inference loop error", error=str(e))
                await asyncio.sleep(1)
        
        logger.debug("Inference loop stopped")
    
    async def _get_batch_requests(self) -> List[InferenceRequest]:
        """Get batch of requests from queue"""
        requests = []
        
        try:
            # Get first request (blocking)
            request = await asyncio.wait_for(self.request_queue.get(), timeout=1.0)
            requests.append(request)
            
            # Get additional requests for batch (non-blocking)
            for _ in range(self.max_batch_size - 1):
                try:
                    request = self.request_queue.get_nowait()
                    requests.append(request)
                except asyncio.QueueEmpty:
                    break
            
        except asyncio.TimeoutError:
            pass
        
        return requests
    
    async def _process_request_batch(self, requests: List[InferenceRequest]):
        """Process batch of inference requests"""
        if not requests:
            return
        
        # Mark requests as started
        start_time = time.time()
        for request in requests:
            request.started_at = start_time
            self.active_requests[request.request_id] = request
        
        try:
            # Run batch inference
            results = await self.batch_inference(requests)
            
            # Update requests with results
            for request, result in zip(requests, results):
                request.completed_at = time.time()
                request.result = result
                
                # Move to completed
                self.completed_requests.append(request)
                del self.active_requests[request.request_id]
                
                # Update stats
                self.total_requests += 1
                if request.completed_at is not None and request.started_at is not None:
                    processing_time = request.completed_at - request.started_at
                else:
                    processing_time = 0.0
                self.total_processing_time += processing_time
                
                logger.debug("Request completed", 
                           request_id=request.request_id,
                           processing_time_ms=processing_time * 1000)
            
        except Exception as e:
            # Mark requests as failed
            for request in requests:
                request.completed_at = time.time()
                request.error = str(e)
                
                # Move to completed
                self.completed_requests.append(request)
                if request.request_id in self.active_requests:
                    del self.active_requests[request.request_id]
            
            logger.error("Batch inference failed", error=str(e))
        
        # Update performance metrics
        self._update_metrics()
    
    def _update_metrics(self):
        """Update performance metrics"""
        if self.total_requests > 0:
            self.average_response_time = self.total_processing_time / self.total_requests
        
        # Calculate requests per second (based on last minute)
        recent_requests = [
            req for req in self.completed_requests[-100:]  # Last 100 requests
            if req.completed_at and req.completed_at > (time.time() - 60)
        ]
        
        if recent_requests:
            completed_times = [req.completed_at for req in recent_requests if req.completed_at is not None]
            if completed_times:
                time_span = max(1, time.time() - min(completed_times))
                self.requests_per_second = len(recent_requests) / time_span
            else:
                self.requests_per_second = 0.0
    
    async def get_worker_status(self) -> Dict[str, Any]:
        """Get detailed worker status"""
        self._update_metrics()
        
        return {
            "worker_id": self.worker_id,
            "model_name": self.model_name,
            "device": self.device,
            "is_loaded": self.is_loaded,
            "is_running": self.is_running,
            "queue_size": self.request_queue.qsize(),
            "active_requests": len(self.active_requests),
            "completed_requests": len(self.completed_requests),
            "total_requests": self.total_requests,
            "average_response_time_ms": self.average_response_time * 1000,
            "requests_per_second": self.requests_per_second,
            "max_batch_size": self.max_batch_size,
            "max_sequence_length": self.max_sequence_length
        }
    
    async def get_model_info(self) -> Dict[str, Any]:
        """Get model information"""
        info = {
            "model_name": self.model_name,
            "model_path": self.model_path,
            "device": self.device,
            "is_loaded": self.is_loaded
        }
        
        if self.model is not None and hasattr(self.model, 'config'):
            # Add model-specific info
            info.update({
                "model_type": getattr(self.model.config, 'model_type', 'unknown'),
                "num_parameters": sum(p.numel() for p in self.model.parameters()),
                "vocab_size": getattr(self.model.config, 'vocab_size', None)
            })
        
        return info
    
    def clear_completed_requests(self, keep_recent: int = 100):
        """Clear old completed requests to save memory"""
        if len(self.completed_requests) > keep_recent:
            self.completed_requests = self.completed_requests[-keep_recent:]
            logger.debug(f"Cleared old requests, keeping {keep_recent}")
    
    async def warm_up(self, sample_inputs: List[Any]):
        """Warm up model with sample inputs"""
        logger.info("Warming up model", worker_id=self.worker_id)
        
        for i, sample_input in enumerate(sample_inputs):
            try:
                await self.inference(sample_input, {})
                logger.debug(f"Warm-up {i+1}/{len(sample_inputs)} completed")
            except Exception as e:
                logger.warning(f"Warm-up {i+1} failed", error=str(e))
        
        logger.info("Model warm-up completed", worker_id=self.worker_id)
    
    def get_memory_usage(self) -> Dict[str, float]:
        """Get current memory usage"""
        memory_info = {}
        
        if torch.cuda.is_available() and self.device.startswith('cuda'):
            device_id = int(self.device.split(':')[-1]) if ':' in self.device else 0
            memory_info['gpu_allocated_mb'] = torch.cuda.memory_allocated(device_id) / (1024**2)
            memory_info['gpu_reserved_mb'] = torch.cuda.memory_reserved(device_id) / (1024**2)
            memory_info['gpu_max_allocated_mb'] = torch.cuda.max_memory_allocated(device_id) / (1024**2)
        
        # Add system memory usage here if needed
        
        return memory_info
