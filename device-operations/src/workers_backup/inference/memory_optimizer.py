"""
Memory optimization strategies for SDXL inference.
Handles VRAM management, model offloading, and memory-efficient generation.
"""

import logging
import torch
import gc
from typing import Optional, Dict, Any, List, Callable, Union
from contextlib import contextmanager
from dataclasses import dataclass
import psutil
import time

logger = logging.getLogger(__name__)


@dataclass
class MemoryStats:
    """Memory usage statistics."""
    gpu_allocated: float  # GB
    gpu_reserved: float   # GB
    gpu_free: float      # GB
    gpu_total: float     # GB
    cpu_memory: float    # GB
    cpu_percent: float   # %
    timestamp: float


class MemoryOptimizer:
    """Memory optimization for SDXL inference."""
    
    def __init__(
        self,
        device: torch.device,
        enable_cpu_offload: bool = True,
        enable_sequential_offload: bool = False,
        enable_attention_slicing: bool = True,
        enable_vae_slicing: bool = True,
        attention_slice_size: Optional[int] = None,
        max_memory_gb: Optional[float] = None
    ):
        """Initialize memory optimizer."""
        self.device = device
        self.enable_cpu_offload = enable_cpu_offload
        self.enable_sequential_offload = enable_sequential_offload
        self.enable_attention_slicing = enable_attention_slicing
        self.enable_vae_slicing = enable_vae_slicing
        self.attention_slice_size = attention_slice_size
        self.max_memory_gb = max_memory_gb
        
        # Memory monitoring
        self.memory_history: List[MemoryStats] = []
        self.peak_memory = 0.0
        
        # Optimization state
        self.models_on_cpu: Dict[str, torch.nn.Module] = {}
        self.current_model_on_gpu: Optional[str] = None
        
        logger.info(f"Memory optimizer initialized for {device}")
        self._log_memory_config()
    
    def _log_memory_config(self) -> None:
        """Log current memory configuration."""
        config = {
            "cpu_offload": self.enable_cpu_offload,
            "sequential_offload": self.enable_sequential_offload,
            "attention_slicing": self.enable_attention_slicing,
            "vae_slicing": self.enable_vae_slicing,
            "attention_slice_size": self.attention_slice_size,
            "max_memory_gb": self.max_memory_gb
        }
        logger.info(f"Memory optimization config: {config}")
    
    def get_memory_stats(self) -> MemoryStats:
        """Get current memory statistics."""
        stats = MemoryStats(
            gpu_allocated=0.0,
            gpu_reserved=0.0,
            gpu_free=0.0,
            gpu_total=0.0,
            cpu_memory=0.0,
            cpu_percent=0.0,
            timestamp=time.time()
        )
        
        # GPU memory (if available)
        if torch.cuda.is_available() and self.device.type == 'cuda':
            gpu_allocated = torch.cuda.memory_allocated(self.device) / (1024**3)
            gpu_reserved = torch.cuda.memory_reserved(self.device) / (1024**3)
            gpu_total = torch.cuda.get_device_properties(self.device).total_memory / (1024**3)
            
            stats.gpu_allocated = gpu_allocated
            stats.gpu_reserved = gpu_reserved
            stats.gpu_total = gpu_total
            stats.gpu_free = gpu_total - gpu_reserved
            
            # Update peak memory
            self.peak_memory = max(self.peak_memory, gpu_allocated)
        
        # CPU memory
        memory_info = psutil.virtual_memory()
        stats.cpu_memory = memory_info.used / (1024**3)
        stats.cpu_percent = memory_info.percent
        
        # Store in history
        self.memory_history.append(stats)
        if len(self.memory_history) > 100:  # Keep last 100 stats
            self.memory_history.pop(0)
        
        return stats
    
    def clear_cache(self) -> None:
        """Clear GPU and Python caches."""
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
        gc.collect()
        logger.debug("Cleared memory caches")
    
    @contextmanager
    def memory_efficient_inference(self):
        """Context manager for memory-efficient inference."""
        initial_stats = self.get_memory_stats()
        logger.debug(f"Starting inference - GPU: {initial_stats.gpu_allocated:.2f}GB allocated")
        
        try:
            # Pre-inference cleanup
            self.clear_cache()
            yield
        finally:
            # Post-inference cleanup
            self.clear_cache()
            final_stats = self.get_memory_stats()
            logger.debug(f"Inference complete - GPU: {final_stats.gpu_allocated:.2f}GB allocated")
    
    def optimize_pipeline(self, pipeline) -> None:
        """Apply memory optimizations to a pipeline."""
        
        # Enable attention slicing
        if self.enable_attention_slicing:
            try:
                if self.attention_slice_size is not None:
                    pipeline.enable_attention_slicing(self.attention_slice_size)
                else:
                    pipeline.enable_attention_slicing("auto")
                logger.debug("Enabled attention slicing")
            except Exception as e:
                logger.warning(f"Could not enable attention slicing: {e}")
        
        # Enable VAE slicing
        if self.enable_vae_slicing:
            try:
                pipeline.enable_vae_slicing()
                logger.debug("Enabled VAE slicing")
            except Exception as e:
                logger.warning(f"Could not enable VAE slicing: {e}")
        
        # Enable CPU offload
        if self.enable_cpu_offload:
            try:
                if self.enable_sequential_offload:
                    pipeline.enable_sequential_cpu_offload()
                    logger.debug("Enabled sequential CPU offload")
                else:
                    pipeline.enable_model_cpu_offload()
                    logger.debug("Enabled model CPU offload")
            except Exception as e:
                logger.warning(f"Could not enable CPU offload: {e}")
        
        # Enable XFormers if available
        try:
            pipeline.enable_xformers_memory_efficient_attention()
            logger.debug("Enabled XFormers memory efficient attention")
        except Exception as e:
            logger.debug(f"XFormers not available: {e}")
    
    def move_model_to_gpu(self, model_name: str, model: torch.nn.Module) -> None:
        """Move a specific model to GPU."""
        
        # Move current model to CPU if different
        if self.current_model_on_gpu and self.current_model_on_gpu != model_name:
            self._move_current_model_to_cpu()
        
        # Move target model to GPU
        if model_name in self.models_on_cpu:
            model = self.models_on_cpu.pop(model_name)
        
        model.to(self.device)
        self.current_model_on_gpu = model_name
        
        logger.debug(f"Moved {model_name} to GPU")
    
    def move_model_to_cpu(self, model_name: str, model: torch.nn.Module) -> None:
        """Move a specific model to CPU."""
        model.to('cpu')
        self.models_on_cpu[model_name] = model
        
        if self.current_model_on_gpu == model_name:
            self.current_model_on_gpu = None
        
        logger.debug(f"Moved {model_name} to CPU")
    
    def _move_current_model_to_cpu(self) -> None:
        """Move current GPU model to CPU."""
        if self.current_model_on_gpu and self.current_model_on_gpu in self.models_on_cpu:
            model = self.models_on_cpu[self.current_model_on_gpu]
            model.to('cpu')
            logger.debug(f"Moved {self.current_model_on_gpu} to CPU")
    
    def check_memory_pressure(self) -> bool:
        """Check if memory pressure is high."""
        stats = self.get_memory_stats()
        
        # Check GPU memory
        if stats.gpu_total > 0:
            gpu_usage_percent = (stats.gpu_allocated / stats.gpu_total) * 100
            if gpu_usage_percent > 85:  # 85% threshold
                return True
        
        # Check against max memory limit
        if self.max_memory_gb and stats.gpu_allocated > self.max_memory_gb:
            return True
        
        # Check CPU memory
        if stats.cpu_percent > 90:  # 90% threshold
            return True
        
        return False
    
    def get_memory_recommendations(self) -> List[str]:
        """Get memory optimization recommendations."""
        recommendations = []
        stats = self.get_memory_stats()
        
        if stats.gpu_total > 0:
            gpu_usage_percent = (stats.gpu_allocated / stats.gpu_total) * 100
            
            if gpu_usage_percent > 80:
                recommendations.append("Consider enabling CPU offload")
                recommendations.append("Enable attention and VAE slicing")
                recommendations.append("Reduce batch size")
                
            if gpu_usage_percent > 90:
                recommendations.append("Enable sequential CPU offload")
                recommendations.append("Use lower precision (fp16/bf16)")
        
        if not self.enable_attention_slicing:
            recommendations.append("Enable attention slicing for memory efficiency")
        
        if not self.enable_vae_slicing:
            recommendations.append("Enable VAE slicing for large images")
        
        if not self.enable_cpu_offload and stats.gpu_total < 12:  # Less than 12GB
            recommendations.append("Enable CPU offload for better memory management")
        
        return recommendations
    
    def get_optimization_report(self) -> Dict[str, Any]:
        """Get comprehensive optimization report."""
        current_stats = self.get_memory_stats()
        
        return {
            "current_memory": {
                "gpu_allocated_gb": current_stats.gpu_allocated,
                "gpu_reserved_gb": current_stats.gpu_reserved,
                "gpu_free_gb": current_stats.gpu_free,
                "gpu_total_gb": current_stats.gpu_total,
                "gpu_usage_percent": (current_stats.gpu_allocated / current_stats.gpu_total * 100) if current_stats.gpu_total > 0 else 0,
                "cpu_memory_gb": current_stats.cpu_memory,
                "cpu_percent": current_stats.cpu_percent
            },
            "peak_memory_gb": self.peak_memory,
            "optimization_settings": {
                "cpu_offload": self.enable_cpu_offload,
                "sequential_offload": self.enable_sequential_offload,
                "attention_slicing": self.enable_attention_slicing,
                "vae_slicing": self.enable_vae_slicing,
                "attention_slice_size": self.attention_slice_size,
                "max_memory_gb": self.max_memory_gb
            },
            "model_distribution": {
                "current_gpu_model": self.current_model_on_gpu,
                "cpu_models": list(self.models_on_cpu.keys()),
                "total_models": len(self.models_on_cpu) + (1 if self.current_model_on_gpu else 0)
            },
            "memory_pressure": self.check_memory_pressure(),
            "recommendations": self.get_memory_recommendations()
        }


class MemoryEfficiencyMonitor:
    """Monitor memory efficiency during inference."""
    
    def __init__(self, optimizer: MemoryOptimizer):
        """Initialize memory monitor."""
        self.optimizer = optimizer
        self.inference_sessions: List[Dict[str, Any]] = []
    
    @contextmanager
    def monitor_inference(self, session_id: str):
        """Monitor a single inference session."""
        start_stats = self.optimizer.get_memory_stats()
        start_time = time.time()
        
        session_data = {
            "session_id": session_id,
            "start_time": start_time,
            "start_memory": start_stats.gpu_allocated,
            "peak_memory": start_stats.gpu_allocated,
            "end_memory": 0.0,
            "duration": 0.0,
            "memory_efficiency": 0.0
        }
        
        try:
            yield session_data
        finally:
            end_stats = self.optimizer.get_memory_stats()
            end_time = time.time()
            
            session_data.update({
                "end_memory": end_stats.gpu_allocated,
                "duration": end_time - start_time,
                "peak_memory": self.optimizer.peak_memory
            })
            
            # Calculate memory efficiency
            memory_delta = session_data["peak_memory"] - session_data["start_memory"]
            if session_data["duration"] > 0:
                session_data["memory_efficiency"] = memory_delta / session_data["duration"]
            
            self.inference_sessions.append(session_data)
            
            # Keep only recent sessions
            if len(self.inference_sessions) > 50:
                self.inference_sessions.pop(0)
    
    def get_efficiency_report(self) -> Dict[str, Any]:
        """Get memory efficiency report."""
        if not self.inference_sessions:
            return {"sessions": 0, "average_efficiency": 0.0}
        
        total_efficiency = sum(s["memory_efficiency"] for s in self.inference_sessions)
        avg_efficiency = total_efficiency / len(self.inference_sessions)
        
        recent_sessions = self.inference_sessions[-10:]  # Last 10 sessions
        recent_efficiency = sum(s["memory_efficiency"] for s in recent_sessions) / len(recent_sessions)
        
        return {
            "total_sessions": len(self.inference_sessions),
            "average_efficiency": avg_efficiency,
            "recent_efficiency": recent_efficiency,
            "sessions": self.inference_sessions[-5:]  # Last 5 sessions for detail
        }


def create_memory_optimizer(
    device: torch.device,
    enable_cpu_offload: bool = True,
    enable_sequential_offload: bool = False,
    enable_attention_slicing: bool = True,
    enable_vae_slicing: bool = True,
    max_memory_gb: Optional[float] = None
) -> MemoryOptimizer:
    """Create a memory optimizer instance."""
    return MemoryOptimizer(
        device=device,
        enable_cpu_offload=enable_cpu_offload,
        enable_sequential_offload=enable_sequential_offload,
        enable_attention_slicing=enable_attention_slicing,
        enable_vae_slicing=enable_vae_slicing,
        max_memory_gb=max_memory_gb
    )
