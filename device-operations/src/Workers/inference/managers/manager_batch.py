"""
Batch Manager for SDXL Workers System
====================================

Migrated from inference/batch_manager.py
Manages batch processing and queue management for efficient inference request handling.
"""

import logging
import torch
import psutil
from typing import Dict, List, Optional, Any, Tuple, Callable
from dataclasses import dataclass
import time

logger = logging.getLogger(__name__)

@dataclass
class BatchConfiguration:
    """Configuration for batch generation."""
    total_images: int = 1
    preferred_batch_size: int = 1
    max_batch_size: int = 4
    min_batch_size: int = 1
    enable_dynamic_sizing: bool = True
    memory_threshold: float = 0.8  # 80% VRAM usage threshold
    progress_callback: Optional[Callable] = None
    parallel_processing: bool = False
    max_parallel_batches: int = 2

@dataclass
class BatchMetrics:
    """Metrics for batch processing."""
    total_batches: int
    completed_batches: int
    failed_batches: int
    total_images_generated: int
    average_batch_time: float
    memory_usage_peak: float
    memory_usage_average: float
    dynamic_adjustments: int
    start_time: float
    end_time: Optional[float] = None

class MemoryMonitor:
    """Monitors memory usage and provides recommendations for batch sizing."""
    
    def __init__(self, device: str = "cuda"):
        self.device = device
        self.is_cuda = device.startswith("cuda")
        self.memory_history: List[float] = []
        self.max_history_size = 100
    
    def get_memory_info(self) -> Dict[str, float]:
        """Get current memory information."""
        if self.is_cuda and torch.cuda.is_available():
            try:
                memory_allocated = torch.cuda.memory_allocated() / 1024**3  # GB
                memory_reserved = torch.cuda.memory_reserved() / 1024**3   # GB
                memory_total = torch.cuda.get_device_properties(0).total_memory / 1024**3  # GB
                
                return {
                    "allocated": memory_allocated,
                    "reserved": memory_reserved,
                    "total": memory_total,
                    "free": memory_total - memory_reserved,
                    "usage_ratio": memory_reserved / memory_total
                }
            except Exception as e:
                logger.warning("Failed to get CUDA memory info: %s", e)
        
        # Fallback to system memory
        system_memory = psutil.virtual_memory()
        return {
            "allocated": (system_memory.total - system_memory.available) / 1024**3,
            "reserved": (system_memory.total - system_memory.available) / 1024**3,
            "total": system_memory.total / 1024**3,
            "free": system_memory.available / 1024**3,
            "usage_ratio": system_memory.percent / 100
        }
    
    def update_memory_history(self) -> None:
        """Update memory usage history."""
        memory_info = self.get_memory_info()
        self.memory_history.append(memory_info["usage_ratio"])
        
        # Keep history size manageable
        if len(self.memory_history) > self.max_history_size:
            self.memory_history.pop(0)
    
    def recommend_batch_size(self, current_batch_size: int, max_batch_size: int, 
                           min_batch_size: int, memory_threshold: float = 0.8) -> int:
        """Recommend optimal batch size based on memory usage."""
        memory_info = self.get_memory_info()
        current_usage = memory_info["usage_ratio"]
        
        # If memory usage is high, reduce batch size
        if current_usage > memory_threshold:
            reduction_factor = min(0.5, (current_usage - memory_threshold) / 0.2)
            new_batch_size = max(min_batch_size, int(current_batch_size * (1 - reduction_factor)))
            logger.info("Memory usage high (%.1f%%), reducing batch size: %d → %d", current_usage * 100, current_batch_size, new_batch_size)
            return new_batch_size
        
        # If memory usage is low and we have room to grow
        elif current_usage < memory_threshold * 0.6 and current_batch_size < max_batch_size:
            # Gradually increase batch size
            new_batch_size = min(max_batch_size, current_batch_size + 1)
            logger.info("Memory usage low (%.1f%%), increasing batch size: %d → %d", current_usage * 100, current_batch_size, new_batch_size)
            return new_batch_size
        
        return current_batch_size
    
    def clear_cache(self) -> None:
        """Clear GPU memory cache."""
        if self.is_cuda and torch.cuda.is_available():
            torch.cuda.empty_cache()
            logger.debug("GPU memory cache cleared")

class BatchProgressTracker:
    """Tracks progress of batch generation with detailed metrics."""
    
    def __init__(self, total_images: int):
        self.total_images = total_images
        self.completed_images = 0
        self.failed_images = 0
        self.start_time = time.time()
        self.batch_times: List[float] = []
        self.memory_snapshots: List[Dict] = []
        self.callbacks: List[Callable] = []
    
    def add_callback(self, callback: Callable) -> None:
        """Add a progress callback function."""
        self.callbacks.append(callback)
    
    def update_progress(self, images_completed: int, batch_time: float, 
                       memory_info: Dict, batch_number: int, total_batches: int) -> None:
        """Update progress information."""
        self.completed_images += images_completed
        self.batch_times.append(batch_time)
        self.memory_snapshots.append(memory_info)
        
        # Calculate progress metrics
        progress_ratio = self.completed_images / self.total_images
        elapsed_time = time.time() - self.start_time
        
        if self.batch_times:
            avg_batch_time = sum(self.batch_times) / len(self.batch_times)
            estimated_remaining = avg_batch_time * (total_batches - batch_number)
        else:
            avg_batch_time = 0
            estimated_remaining = 0
        
        progress_info = {
            "completed_images": self.completed_images,
            "total_images": self.total_images,
            "progress_ratio": progress_ratio,
            "batch_number": batch_number,
            "total_batches": total_batches,
            "elapsed_time": elapsed_time,
            "estimated_remaining": estimated_remaining,
            "average_batch_time": avg_batch_time,
            "current_memory_usage": memory_info.get("usage_ratio", 0)
        }
        
        # Call registered callbacks
        for callback in self.callbacks:
            try:
                callback(progress_info)
            except Exception as e:
                logger.warning("Progress callback failed: %s", e)
    
    def get_final_metrics(self) -> Dict[str, Any]:
        """Get final batch processing metrics."""
        total_time = time.time() - self.start_time
        
        return {
            "total_images": self.total_images,
            "completed_images": self.completed_images,
            "failed_images": self.failed_images,
            "success_rate": self.completed_images / self.total_images if self.total_images > 0 else 0,
            "total_time": total_time,
            "average_batch_time": sum(self.batch_times) / len(self.batch_times) if self.batch_times else 0,
            "peak_memory_usage": max((s.get("usage_ratio", 0) for s in self.memory_snapshots), default=0),
            "average_memory_usage": sum(s.get("usage_ratio", 0) for s in self.memory_snapshots) / len(self.memory_snapshots) if self.memory_snapshots else 0,
            "total_batches": len(self.batch_times)
        }

class BatchManager:
    """
    Enhanced batch manager with sophisticated memory management and optimization.
    
    Provides:
    - Dynamic batch sizing based on available memory
    - Progress tracking and reporting
    - Memory monitoring and optimization
    - Parallel batch processing
    - Comprehensive error handling and recovery
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.device = config.get("device", "cuda")
        self.memory_monitor = MemoryMonitor(self.device)
        self.current_metrics: Optional[BatchMetrics] = None
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize batch manager."""
        try:
            self.logger.info("Initializing batch manager...")
            self.initialized = True
            self.logger.info("Batch manager initialized successfully")
            return True
        except Exception as e:
            self.logger.error("Batch manager initialization failed: %s", e)
            return False
    
    async def process_batch(self, batch_data: Dict[str, Any]) -> Dict[str, Any]:
        """Process a batch of inference requests."""
        try:
            batch_size = batch_data.get("batch_size", 1)
            requests = batch_data.get("requests", [])
            
            if not requests:
                return {"error": "No requests provided for batch processing"}
            
            # Process batches (simplified for this migration)
            results = []
            for request in requests:
                # Simulate processing
                result = {
                    "request_id": request.get("request_id", ""),
                    "status": "completed",
                    "processing_time": 1.0  # Placeholder
                }
                results.append(result)
            
            return {
                "batch_processed": True,
                "total_requests": len(requests),
                "results": results,
                "batch_size": batch_size
            }
        except Exception as e:
            self.logger.error("Failed to process batch: %s", e)
            return {"error": str(e)}
        
    async def process_batch_generation(self, 
                                     generation_function: Callable,
                                     batch_config: BatchConfiguration,
                                     generation_params: Dict[str, Any]) -> Tuple[List[Any], Dict[str, Any]]:
        """
        Process batch generation with advanced management.
        
        Args:
            generation_function: The function to call for generation
            batch_config: Batch configuration
            generation_params: Base parameters for generation
            
        Returns:
            Tuple of (generated_images, metrics)
        """
        logger.info("Starting enhanced batch generation: %d images", batch_config.total_images)
        
        # Initialize tracking
        progress_tracker = BatchProgressTracker(batch_config.total_images)
        if batch_config.progress_callback:
            progress_tracker.add_callback(batch_config.progress_callback)
        
        # Calculate batches
        batches = self._calculate_batches(batch_config)
        logger.info("Planned %d batches: %s", len(batches), [b['batch_size'] for b in batches])
        
        # Initialize metrics
        self.current_metrics = BatchMetrics(
            total_batches=len(batches),
            completed_batches=0,
            failed_batches=0,
            total_images_generated=0,
            average_batch_time=0,
            memory_usage_peak=0,
            memory_usage_average=0,
            dynamic_adjustments=0,
            start_time=time.time()
        )
        
        all_images = []
        
        try:
            # Process batches
            if batch_config.parallel_processing:
                all_images = await self._process_parallel_batches(
                    generation_function, batches, generation_params, 
                    progress_tracker, batch_config
                )
            else:
                all_images = await self._process_sequential_batches(
                    generation_function, batches, generation_params, 
                    progress_tracker, batch_config
                )
            
            # Finalize metrics
            self.current_metrics.end_time = time.time()
            self.current_metrics.total_images_generated = len(all_images)
            
            final_metrics = progress_tracker.get_final_metrics()
            
            logger.info("Batch generation completed: %d images in %.1fs", len(all_images), final_metrics['total_time'])
            
            return all_images, final_metrics
            
        except Exception as e:
            logger.error("Batch generation failed: %s", str(e))
            raise
    
    def _calculate_batches(self, config: BatchConfiguration) -> List[Dict[str, Any]]:
        """Calculate optimal batch distribution."""
        batches = []
        remaining_images = config.total_images
        current_batch_size = min(config.preferred_batch_size, config.max_batch_size)
        
        batch_number = 0
        while remaining_images > 0:
            # Determine batch size for this iteration
            actual_batch_size = min(current_batch_size, remaining_images)
            
            batches.append({
                "batch_number": batch_number,
                "batch_size": actual_batch_size,
                "start_image": config.total_images - remaining_images,
                "end_image": config.total_images - remaining_images + actual_batch_size
            })
            
            remaining_images -= actual_batch_size
            batch_number += 1
            
            # Dynamic sizing will be applied during execution
        
        return batches
    
    async def _process_sequential_batches(self, 
                                        generation_function: Callable,
                                        batches: List[Dict],
                                        base_params: Dict[str, Any],
                                        progress_tracker: BatchProgressTracker,
                                        config: BatchConfiguration) -> List[Any]:
        """Process batches sequentially with dynamic optimization."""
        all_images = []
        current_batch_size = config.preferred_batch_size
        
        for batch_info in batches:
            batch_start_time = time.time()
            
            try:
                # Update memory information
                self.memory_monitor.update_memory_history()
                memory_info = self.memory_monitor.get_memory_info()
                
                # Dynamic batch size adjustment
                if config.enable_dynamic_sizing:
                    recommended_size = self.memory_monitor.recommend_batch_size(
                        current_batch_size, config.max_batch_size, 
                        config.min_batch_size, config.memory_threshold
                    )
                    
                    if recommended_size != current_batch_size:
                        current_batch_size = recommended_size
                        if self.current_metrics:
                            self.current_metrics.dynamic_adjustments += 1
                        
                        # Recalculate this batch if size changed
                        actual_batch_size = min(current_batch_size, batch_info["batch_size"])
                        batch_info["batch_size"] = actual_batch_size
                
                # Prepare generation parameters for this batch
                batch_params = base_params.copy()
                batch_params["num_images_per_prompt"] = batch_info["batch_size"]
                
                # Generate batch
                logger.debug("Processing batch %d: %d images", batch_info['batch_number'] + 1, batch_info['batch_size'])
                
                with torch.inference_mode():
                    result = await generation_function(**batch_params)
                
                # Extract images from result
                if hasattr(result, 'images'):
                    batch_images = result.images
                else:
                    batch_images = result
                
                all_images.extend(batch_images)
                
                # Update metrics
                batch_time = time.time() - batch_start_time
                if self.current_metrics:
                    self.current_metrics.completed_batches += 1
                    self.current_metrics.total_images_generated += len(batch_images)
                
                # Update progress
                progress_tracker.update_progress(
                    len(batch_images), batch_time, memory_info,
                    batch_info["batch_number"] + 1, len(batches)
                )
                
                # Clear cache periodically
                if (batch_info["batch_number"] + 1) % 3 == 0:
                    self.memory_monitor.clear_cache()
                
                logger.debug("Batch %d completed in %.1fs", batch_info['batch_number'] + 1, batch_time)
                
            except Exception as e:
                logger.error("Batch %d failed: %s", batch_info['batch_number'] + 1, str(e))
                if self.current_metrics:
                    self.current_metrics.failed_batches += 1
                
                # Continue with next batch
                continue
        
        return all_images
    
    async def _process_parallel_batches(self, 
                                      generation_function: Callable,
                                      batches: List[Dict],
                                      base_params: Dict[str, Any],
                                      progress_tracker: BatchProgressTracker,
                                      config: BatchConfiguration) -> List[Any]:
        """Process batches in parallel (experimental)."""
        logger.warning("Parallel batch processing is experimental and may cause memory issues")
        
        # For now, fall back to sequential processing
        # Parallel processing would require careful memory management
        # and multiple pipeline instances
        return await self._process_sequential_batches(
            generation_function, batches, base_params, progress_tracker, config
        )
    
    def get_recommended_batch_size(self, max_batch_size: int = 4) -> int:
        """Get recommended batch size based on system capabilities."""
        memory_info = self.memory_monitor.get_memory_info()
        
        # Conservative recommendations based on available memory
        if memory_info["free"] > 8:  # 8GB+ free
            return min(max_batch_size, 4)
        elif memory_info["free"] > 4:  # 4-8GB free
            return min(max_batch_size, 2)
        else:  # <4GB free
            return 1
    
    def get_current_metrics(self) -> Optional[BatchMetrics]:
        """Get current batch processing metrics."""
        return self.current_metrics
    
    async def get_status(self) -> Dict[str, Any]:
        """Get batch manager status."""
        return {
            "initialized": self.initialized,
            "device": self.device,
            "current_metrics": self.current_metrics.__dict__ if self.current_metrics else None,
            "memory_info": self.memory_monitor.get_memory_info() if hasattr(self, 'memory_monitor') else None
        }
    
    async def cleanup(self) -> None:
        """Clean up batch manager resources."""
        try:
            self.logger.info("Cleaning up batch manager...")
            self.current_metrics = None
            self.initialized = False
            self.logger.info("Batch manager cleanup complete")
        except Exception as e:
            self.logger.error("Batch manager cleanup error: %s", e)
