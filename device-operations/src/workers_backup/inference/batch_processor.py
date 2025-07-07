"""
Batch processor for handling multiple SDXL generation requests efficiently.
Supports batching, queuing, and resource optimization.
"""

import logging
import asyncio
import torch
from typing import List, Dict, Any, Optional, Union, Callable
from dataclasses import dataclass, field
from datetime import datetime
import uuid
from concurrent.futures import ThreadPoolExecutor
from queue import Queue, PriorityQueue
import threading

logger = logging.getLogger(__name__)


@dataclass
class BatchRequest:
    """Batch generation request."""
    id: str = field(default_factory=lambda: str(uuid.uuid4()))
    prompts: List[str] = field(default_factory=list)
    parameters: Dict[str, Any] = field(default_factory=dict)
    priority: int = 0  # Higher number = higher priority
    created_at: datetime = field(default_factory=datetime.now)
    callback: Optional[Callable] = None
    user_id: Optional[str] = None
    
    def __lt__(self, other):
        """For priority queue ordering."""
        return self.priority > other.priority


@dataclass
class BatchResult:
    """Batch generation result."""
    request_id: str
    results: List[Any]
    success: bool
    error: Optional[str] = None
    processing_time: float = 0.0
    memory_used: float = 0.0


class BatchProcessor:
    """Batch processor for SDXL generation."""
    
    def __init__(
        self,
        max_batch_size: int = 4,
        max_queue_size: int = 100,
        worker_threads: int = 2,
        device: torch.device = torch.device("cpu"),
        dtype: torch.dtype = torch.float16
    ):
        """Initialize batch processor."""
        self.max_batch_size = max_batch_size
        self.max_queue_size = max_queue_size
        self.worker_threads = worker_threads
        self.device = device
        self.dtype = dtype
        
        # Processing queue (priority queue)
        self.request_queue: PriorityQueue = PriorityQueue(maxsize=max_queue_size)
        self.result_queue: Queue = Queue()
        
        # Worker management
        self.executor = ThreadPoolExecutor(max_workers=worker_threads)
        self.workers_running = False
        self.worker_tasks: List[threading.Thread] = []
        
        # Statistics
        self.total_processed = 0
        self.total_errors = 0
        self.current_batch_size = 0
        self.processing_lock = threading.Lock()
        
        # Pipeline reference (set externally)
        self.pipeline = None
        
        logger.info(f"Batch processor initialized: max_batch={max_batch_size}, workers={worker_threads}")
    
    def start_workers(self) -> None:
        """Start worker threads."""
        if self.workers_running:
            return
        
        self.workers_running = True
        
        for i in range(self.worker_threads):
            worker = threading.Thread(
                target=self._worker_loop,
                name=f"BatchWorker-{i}",
                daemon=True
            )
            worker.start()
            self.worker_tasks.append(worker)
        
        logger.info(f"Started {self.worker_threads} batch workers")
    
    def stop_workers(self) -> None:
        """Stop worker threads."""
        self.workers_running = False
        
        # Add sentinel values to wake up workers
        for _ in range(self.worker_threads):
            try:
                self.request_queue.put(None, timeout=1.0)
            except:
                pass
        
        # Wait for workers to finish
        for worker in self.worker_tasks:
            worker.join(timeout=5.0)
        
        self.worker_tasks.clear()
        logger.info("Stopped batch workers")
    
    def submit_batch(
        self,
        prompts: List[str],
        parameters: Dict[str, Any],
        priority: int = 0,
        user_id: Optional[str] = None,
        callback: Optional[Callable] = None
    ) -> str:
        """Submit a batch request."""
        
        if not prompts:
            raise ValueError("Prompts list cannot be empty")
        
        if len(prompts) > self.max_batch_size:
            raise ValueError(f"Batch size {len(prompts)} exceeds maximum {self.max_batch_size}")
        
        request = BatchRequest(
            prompts=prompts,
            parameters=parameters,
            priority=priority,
            user_id=user_id,
            callback=callback
        )
        
        try:
            self.request_queue.put(request, timeout=5.0)
            logger.info(f"Submitted batch request {request.id} with {len(prompts)} prompts")
            return request.id
        except:
            raise RuntimeError("Request queue is full")
    
    def get_result(self, timeout: Optional[float] = None) -> Optional[BatchResult]:
        """Get a completed result."""
        try:
            return self.result_queue.get(timeout=timeout)
        except:
            return None
    
    def get_queue_size(self) -> int:
        """Get current queue size."""
        return self.request_queue.qsize()
    
    def get_statistics(self) -> Dict[str, Any]:
        """Get processing statistics."""
        return {
            "total_processed": self.total_processed,
            "total_errors": self.total_errors,
            "queue_size": self.get_queue_size(),
            "current_batch_size": self.current_batch_size,
            "workers_running": self.workers_running,
            "max_batch_size": self.max_batch_size,
            "worker_threads": self.worker_threads
        }
    
    def _worker_loop(self) -> None:
        """Worker thread main loop."""
        logger.info(f"Batch worker {threading.current_thread().name} started")
        
        while self.workers_running:
            try:
                # Get next request
                request = self.request_queue.get(timeout=1.0)
                if request is None:  # Sentinel value
                    break
                
                # Process the batch
                result = self._process_batch(request)
                
                # Store result
                self.result_queue.put(result)
                
                # Call callback if provided
                if request.callback:
                    try:
                        request.callback(result)
                    except Exception as e:
                        logger.error(f"Callback error for request {request.id}: {e}")
                
                # Update statistics
                with self.processing_lock:
                    self.total_processed += 1
                    if not result.success:
                        self.total_errors += 1
                
            except Exception as e:
                if self.workers_running:  # Only log if not shutting down
                    logger.error(f"Worker error: {e}")
        
        logger.info(f"Batch worker {threading.current_thread().name} stopped")
    
    def _process_batch(self, request: BatchRequest) -> BatchResult:
        """Process a single batch request."""
        start_time = datetime.now()
        
        try:
            with self.processing_lock:
                self.current_batch_size = len(request.prompts)
            
            if self.pipeline is None:
                raise RuntimeError("No pipeline configured for batch processing")
            
            # Prepare batch parameters
            batch_params = request.parameters.copy()
            batch_params['prompt'] = request.prompts
            
            # Ensure batch size consistency
            if 'batch_size' not in batch_params:
                batch_params['batch_size'] = len(request.prompts)
            
            # Monitor memory before processing
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
                memory_before = torch.cuda.memory_allocated(self.device)
            else:
                memory_before = 0
            
            # Generate images
            logger.debug(f"Processing batch {request.id} with {len(request.prompts)} prompts")
            
            # Call the pipeline
            results = self.pipeline(**batch_params)
            
            # Extract images from results
            if hasattr(results, 'images'):
                images = results.images
            elif isinstance(results, (list, tuple)):
                images = results
            else:
                images = [results]
            
            # Monitor memory after processing
            if torch.cuda.is_available():
                memory_after = torch.cuda.memory_allocated(self.device)
                memory_used = (memory_after - memory_before) / (1024 ** 2)  # MB
            else:
                memory_used = 0
            
            processing_time = (datetime.now() - start_time).total_seconds()
            
            logger.info(f"Batch {request.id} completed in {processing_time:.2f}s, memory: {memory_used:.1f}MB")
            
            return BatchResult(
                request_id=request.id,
                results=images,
                success=True,
                processing_time=processing_time,
                memory_used=memory_used
            )
            
        except Exception as e:
            processing_time = (datetime.now() - start_time).total_seconds()
            error_msg = str(e)
            
            logger.error(f"Batch {request.id} failed after {processing_time:.2f}s: {error_msg}")
            
            return BatchResult(
                request_id=request.id,
                results=[],
                success=False,
                error=error_msg,
                processing_time=processing_time
            )
        
        finally:
            with self.processing_lock:
                self.current_batch_size = 0


class BatchManager:
    """High-level batch management interface."""
    
    def __init__(self, batch_processor: BatchProcessor):
        """Initialize batch manager."""
        self.processor = batch_processor
        self.active_requests: Dict[str, BatchRequest] = {}
        self.completed_results: Dict[str, BatchResult] = {}
        self.result_retention_limit = 100  # Max completed results to keep
        
        logger.info("Batch manager initialized")
    
    async def submit_async(
        self,
        prompts: List[str],
        parameters: Dict[str, Any],
        priority: int = 0,
        user_id: Optional[str] = None
    ) -> BatchResult:
        """Submit batch and wait for result asynchronously."""
        
        # Submit the request
        request_id = self.processor.submit_batch(
            prompts=prompts,
            parameters=parameters,
            priority=priority,
            user_id=user_id
        )
        
        # Wait for result
        while True:
            result = self.processor.get_result(timeout=1.0)
            if result and result.request_id == request_id:
                self.completed_results[request_id] = result
                self._cleanup_old_results()
                return result
            await asyncio.sleep(0.1)
    
    def get_completed_result(self, request_id: str) -> Optional[BatchResult]:
        """Get a completed result by ID."""
        return self.completed_results.get(request_id)
    
    def cleanup_result(self, request_id: str) -> bool:
        """Remove a completed result from memory."""
        return self.completed_results.pop(request_id, None) is not None
    
    def _cleanup_old_results(self) -> None:
        """Clean up old completed results."""
        if len(self.completed_results) > self.result_retention_limit:
            # Keep only the most recent results
            sorted_results = sorted(
                self.completed_results.items(),
                key=lambda x: getattr(x[1], 'processing_time', 0),
                reverse=True
            )
            
            # Keep only the retention limit
            self.completed_results = dict(sorted_results[:self.result_retention_limit])
            
            logger.debug(f"Cleaned up old batch results, keeping {len(self.completed_results)}")


def create_batch_processor(
    max_batch_size: int = 4,
    max_queue_size: int = 100,
    worker_threads: int = 2,
    device: torch.device = torch.device("cpu"),
    dtype: torch.dtype = torch.float16
) -> BatchProcessor:
    """Create a batch processor instance."""
    processor = BatchProcessor(
        max_batch_size=max_batch_size,
        max_queue_size=max_queue_size,
        worker_threads=worker_threads,
        device=device,
        dtype=dtype
    )
    processor.start_workers()
    return processor
