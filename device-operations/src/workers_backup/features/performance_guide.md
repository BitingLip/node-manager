# Performance Optimization Guide

## Overview
Comprehensive guide for optimizing performance in the Enhanced SDXL pipeline with upscaling and post-processing features.

## Table of Contents
- [System Requirements](#system-requirements)
- [Hardware Optimization](#hardware-optimization)
- [Memory Management](#memory-management)
- [Model Optimization](#model-optimization)
- [Batch Processing](#batch-processing)
- [Performance Monitoring](#performance-monitoring)
- [Optimization Strategies](#optimization-strategies)

## System Requirements

### Minimum Requirements
- **GPU**: 6GB VRAM (DirectML compatible)
- **RAM**: 16GB system memory
- **Storage**: 50GB available space for models
- **CPU**: 8-core processor (Intel i5/AMD Ryzen 5 or better)

### Recommended Requirements
- **GPU**: 12GB+ VRAM (RTX 3080/4070 or better)
- **RAM**: 32GB system memory
- **Storage**: 100GB SSD for models and cache
- **CPU**: 16-core processor (Intel i7/AMD Ryzen 7 or better)

### Optimal Requirements
- **GPU**: 24GB+ VRAM (RTX 4090/A6000 or better)
- **RAM**: 64GB system memory
- **Storage**: 500GB NVMe SSD
- **CPU**: 32-core processor (Intel i9/AMD Ryzen 9 or better)

## Hardware Optimization

### GPU Configuration

#### DirectML Optimization
```python
# Optimal DirectML settings
import torch_directml

# Enable memory efficient attention
torch_directml.device_count()
torch_directml.get_device_name(0)

# Configure device settings
device_config = {
    "enable_memory_efficient_attention": True,
    "enable_flash_attention": True,
    "memory_fraction": 0.9,  # Use 90% of available VRAM
    "allow_growth": True
}
```

#### VRAM Optimization
```python
# VRAM usage optimization
optimization_config = {
    "enable_model_cpu_offload": True,  # Offload unused models to CPU
    "enable_sequential_cpu_offload": True,  # Sequential processing
    "enable_attention_slicing": True,  # Reduce attention memory usage
    "enable_vae_slicing": True,  # Reduce VAE memory usage
    "max_split_size_mb": 512  # Control memory fragmentation
}
```

### CPU Optimization

#### Thread Configuration
```python
import torch
import os

# Optimize CPU threading
torch.set_num_threads(16)  # Set based on CPU cores
os.environ["OMP_NUM_THREADS"] = "16"
os.environ["MKL_NUM_THREADS"] = "16"

# Enable CPU optimizations
torch.backends.mkldnn.enabled = True
torch.backends.mkldnn.verbose = 0
```

#### Process Priority
```python
import psutil
import os

# Set high priority for processing
process = psutil.Process(os.getpid())
process.nice(psutil.HIGH_PRIORITY_CLASS)  # Windows
# process.nice(-10)  # Linux/Mac
```

## Memory Management

### VRAM Management

#### Dynamic Memory Allocation
```python
class VRAMManager:
    def __init__(self, max_vram_gb=8):
        self.max_vram_bytes = max_vram_gb * 1024 * 1024 * 1024
        self.current_usage = 0
        
    def check_available_memory(self):
        """Check available VRAM"""
        import torch
        if torch.cuda.is_available():
            return torch.cuda.get_device_properties(0).total_memory - torch.cuda.memory_allocated()
        return self.max_vram_bytes - self.current_usage
    
    def optimize_batch_size(self, base_batch_size=4):
        """Dynamically adjust batch size based on available memory"""
        available_memory = self.check_available_memory()
        memory_per_image = 1.5 * 1024 * 1024 * 1024  # 1.5GB per image estimate
        
        optimal_batch_size = min(base_batch_size, int(available_memory / memory_per_image))
        return max(1, optimal_batch_size)
    
    def cleanup_memory(self):
        """Force memory cleanup"""
        import torch
        import gc
        
        gc.collect()
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
            torch.cuda.synchronize()
```

#### Memory-Efficient Model Loading
```python
class EfficientModelLoader:
    def __init__(self):
        self.loaded_models = {}
        self.memory_tracker = VRAMManager()
    
    async def load_model_efficiently(self, model_path, model_type):
        """Load models with memory optimization"""
        
        # Check if model already loaded
        if model_path in self.loaded_models:
            return self.loaded_models[model_path]
        
        # Free memory if needed
        available_memory = self.memory_tracker.check_available_memory()
        if available_memory < 2 * 1024 * 1024 * 1024:  # Less than 2GB
            await self._offload_unused_models()
        
        # Load with optimizations
        model = self._load_with_optimizations(model_path, model_type)
        self.loaded_models[model_path] = model
        
        return model
    
    def _load_with_optimizations(self, model_path, model_type):
        """Load model with memory optimizations"""
        from diffusers import DiffusionPipeline
        
        # Configure loading options
        loading_options = {
            "torch_dtype": torch.float16,  # Use half precision
            "device_map": "auto",  # Automatic device mapping
            "low_cpu_mem_usage": True,  # Reduce CPU memory usage
            "use_safetensors": True,  # Use safetensors format
            "variant": "fp16"  # Use fp16 variant if available
        }
        
        if model_type == "upscaler":
            # Specific optimizations for upscaling models
            loading_options.update({
                "enable_attention_slicing": True,
                "enable_vae_slicing": True
            })
        
        return DiffusionPipeline.from_pretrained(model_path, **loading_options)
```

### System Memory Optimization

#### Garbage Collection Management
```python
import gc
import psutil

class MemoryOptimizer:
    def __init__(self, auto_cleanup_threshold=0.8):
        self.auto_cleanup_threshold = auto_cleanup_threshold
        
    def monitor_memory_usage(self):
        """Monitor system memory usage"""
        memory = psutil.virtual_memory()
        return {
            "total": memory.total,
            "available": memory.available,
            "percent": memory.percent,
            "used": memory.used
        }
    
    def auto_cleanup_if_needed(self):
        """Automatically cleanup if memory usage is high"""
        memory_info = self.monitor_memory_usage()
        if memory_info["percent"] > self.auto_cleanup_threshold * 100:
            self.force_cleanup()
            return True
        return False
    
    def force_cleanup(self):
        """Force garbage collection and memory cleanup"""
        gc.collect()  # Python garbage collection
        
        # Clear unnecessary caches
        import torch
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
        
        # Force immediate cleanup
        gc.set_threshold(1, 1, 1)
        gc.collect()
        gc.set_threshold(700, 10, 10)  # Reset to default
```

## Model Optimization

### Model Loading Strategies

#### Lazy Loading
```python
class LazyModelLoader:
    def __init__(self):
        self.model_cache = {}
        self.load_queue = []
        
    async def load_on_demand(self, model_name):
        """Load model only when needed"""
        if model_name not in self.model_cache:
            model = await self._load_model(model_name)
            self.model_cache[model_name] = model
            
        # Update access time for LRU cache
        self._update_access_time(model_name)
        return self.model_cache[model_name]
    
    def _update_access_time(self, model_name):
        """Update model access time for cache management"""
        import time
        if hasattr(self.model_cache[model_name], '_last_access'):
            self.model_cache[model_name]._last_access = time.time()
        else:
            setattr(self.model_cache[model_name], '_last_access', time.time())
    
    async def cleanup_unused_models(self, max_age_seconds=300):
        """Remove models not used recently"""
        import time
        current_time = time.time()
        
        models_to_remove = []
        for model_name, model in self.model_cache.items():
            last_access = getattr(model, '_last_access', current_time)
            if current_time - last_access > max_age_seconds:
                models_to_remove.append(model_name)
        
        for model_name in models_to_remove:
            del self.model_cache[model_name]
            gc.collect()
```

#### Model Quantization
```python
class ModelQuantizer:
    @staticmethod
    def quantize_model(model, quantization_type="int8"):
        """Quantize model to reduce memory usage"""
        import torch
        
        if quantization_type == "int8":
            quantized_model = torch.quantization.quantize_dynamic(
                model, {torch.nn.Linear}, dtype=torch.qint8
            )
        elif quantization_type == "fp16":
            quantized_model = model.half()
        else:
            quantized_model = model
            
        return quantized_model
    
    @staticmethod
    def optimize_for_inference(model):
        """Optimize model for inference"""
        import torch
        
        # Set to evaluation mode
        model.eval()
        
        # Enable inference optimizations
        torch.jit.optimized_execution(True)
        
        # Fuse operations if possible
        if hasattr(torch.backends, 'mkldnn') and torch.backends.mkldnn.enabled:
            model = torch.jit.optimize_for_inference(model)
            
        return model
```

## Batch Processing

### Intelligent Batch Sizing
```python
class IntelligentBatchProcessor:
    def __init__(self, vram_manager):
        self.vram_manager = vram_manager
        self.performance_history = []
        
    def calculate_optimal_batch_size(self, image_count, base_batch_size=4):
        """Calculate optimal batch size based on available resources"""
        
        # Memory-based calculation
        memory_batch_size = self.vram_manager.optimize_batch_size(base_batch_size)
        
        # Performance-based calculation
        perf_batch_size = self._calculate_performance_based_batch_size()
        
        # Take the minimum for safety
        optimal_batch_size = min(memory_batch_size, perf_batch_size, image_count)
        
        return max(1, optimal_batch_size)
    
    def _calculate_performance_based_batch_size(self):
        """Calculate batch size based on performance history"""
        if not self.performance_history:
            return 4  # Default
            
        # Analyze recent performance
        recent_performance = self.performance_history[-10:]  # Last 10 batches
        avg_time_per_image = sum(p['time_per_image'] for p in recent_performance) / len(recent_performance)
        
        # Adjust batch size based on performance
        if avg_time_per_image < 15:  # Fast processing
            return 6
        elif avg_time_per_image < 25:  # Medium processing
            return 4
        else:  # Slow processing
            return 2
    
    async def process_batch_intelligently(self, images, process_function):
        """Process images in intelligent batches"""
        import time
        
        batch_size = self.calculate_optimal_batch_size(len(images))
        results = []
        
        for i in range(0, len(images), batch_size):
            batch = images[i:i + batch_size]
            
            start_time = time.time()
            batch_results = await process_function(batch)
            end_time = time.time()
            
            # Record performance
            batch_time = end_time - start_time
            time_per_image = batch_time / len(batch)
            
            self.performance_history.append({
                'batch_size': len(batch),
                'total_time': batch_time,
                'time_per_image': time_per_image,
                'timestamp': time.time()
            })
            
            # Keep only recent history
            if len(self.performance_history) > 50:
                self.performance_history = self.performance_history[-50:]
            
            results.extend(batch_results)
            
            # Cleanup between batches
            self.vram_manager.cleanup_memory()
        
        return results
```

### Parallel Processing
```python
import asyncio
import concurrent.futures

class ParallelProcessor:
    def __init__(self, max_workers=2):
        self.max_workers = max_workers
        self.executor = concurrent.futures.ThreadPoolExecutor(max_workers=max_workers)
        
    async def process_parallel_batches(self, image_batches, process_function):
        """Process multiple batches in parallel"""
        
        # Create tasks for parallel processing
        tasks = []
        for batch in image_batches:
            task = asyncio.create_task(self._process_single_batch(batch, process_function))
            tasks.append(task)
        
        # Wait for all batches to complete
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        # Handle exceptions and combine results
        combined_results = []
        for result in results:
            if isinstance(result, Exception):
                print(f"Batch processing error: {result}")
                continue
            combined_results.extend(result)
        
        return combined_results
    
    async def _process_single_batch(self, batch, process_function):
        """Process a single batch"""
        try:
            return await process_function(batch)
        except Exception as e:
            print(f"Error processing batch: {e}")
            return []
```

## Performance Monitoring

### Real-time Performance Tracking
```python
import time
import psutil
import threading
from dataclasses import dataclass
from typing import Dict, List

@dataclass
class PerformanceMetrics:
    timestamp: float
    cpu_usage: float
    memory_usage: float
    vram_usage: float
    processing_time: float
    throughput: float
    queue_size: int

class PerformanceMonitor:
    def __init__(self, monitoring_interval=1.0):
        self.monitoring_interval = monitoring_interval
        self.metrics_history: List[PerformanceMetrics] = []
        self.is_monitoring = False
        self.monitor_thread = None
        
    def start_monitoring(self):
        """Start real-time performance monitoring"""
        if not self.is_monitoring:
            self.is_monitoring = True
            self.monitor_thread = threading.Thread(target=self._monitor_loop, daemon=True)
            self.monitor_thread.start()
    
    def stop_monitoring(self):
        """Stop performance monitoring"""
        self.is_monitoring = False
        if self.monitor_thread:
            self.monitor_thread.join()
    
    def _monitor_loop(self):
        """Main monitoring loop"""
        while self.is_monitoring:
            metrics = self._collect_metrics()
            self.metrics_history.append(metrics)
            
            # Keep only recent metrics (last hour)
            if len(self.metrics_history) > 3600:
                self.metrics_history = self.metrics_history[-3600:]
            
            time.sleep(self.monitoring_interval)
    
    def _collect_metrics(self) -> PerformanceMetrics:
        """Collect current performance metrics"""
        import torch
        
        # CPU and memory usage
        cpu_usage = psutil.cpu_percent()
        memory_info = psutil.virtual_memory()
        memory_usage = memory_info.percent
        
        # GPU memory usage
        vram_usage = 0
        if torch.cuda.is_available():
            vram_total = torch.cuda.get_device_properties(0).total_memory
            vram_used = torch.cuda.memory_allocated()
            vram_usage = (vram_used / vram_total) * 100
        
        return PerformanceMetrics(
            timestamp=time.time(),
            cpu_usage=cpu_usage,
            memory_usage=memory_usage,
            vram_usage=vram_usage,
            processing_time=0,  # Will be updated during processing
            throughput=0,  # Will be calculated based on recent processing
            queue_size=0  # Will be updated based on queue status
        )
    
    def get_performance_summary(self, duration_minutes=10) -> Dict:
        """Get performance summary for the last N minutes"""
        cutoff_time = time.time() - (duration_minutes * 60)
        recent_metrics = [m for m in self.metrics_history if m.timestamp > cutoff_time]
        
        if not recent_metrics:
            return {}
        
        return {
            "avg_cpu_usage": sum(m.cpu_usage for m in recent_metrics) / len(recent_metrics),
            "avg_memory_usage": sum(m.memory_usage for m in recent_metrics) / len(recent_metrics),
            "avg_vram_usage": sum(m.vram_usage for m in recent_metrics) / len(recent_metrics),
            "max_cpu_usage": max(m.cpu_usage for m in recent_metrics),
            "max_memory_usage": max(m.memory_usage for m in recent_metrics),
            "max_vram_usage": max(m.vram_usage for m in recent_metrics),
            "sample_count": len(recent_metrics)
        }
```

### Performance Benchmarking
```python
class PerformanceBenchmark:
    def __init__(self):
        self.benchmark_results = {}
        
    async def run_upscaling_benchmark(self, test_images, configurations):
        """Benchmark upscaling performance with different configurations"""
        from upscaler_worker import UpscalerWorker
        
        worker = UpscalerWorker()
        results = {}
        
        for config_name, config in configurations.items():
            print(f"Running benchmark: {config_name}")
            
            start_time = time.time()
            result = await worker.process_upscale_request({
                "images": test_images,
                **config
            })
            end_time = time.time()
            
            if result["success"]:
                total_time = end_time - start_time
                images_per_second = len(test_images) / total_time
                
                results[config_name] = {
                    "total_time": total_time,
                    "images_per_second": images_per_second,
                    "avg_time_per_image": total_time / len(test_images),
                    "memory_usage": result["metrics"]["memory_usage"],
                    "quality_score": result["metrics"]["quality_score"]
                }
            else:
                results[config_name] = {"error": result.get("error", "Unknown error")}
        
        return results
    
    def generate_performance_report(self, benchmark_results):
        """Generate a comprehensive performance report"""
        report = []
        report.append("# Performance Benchmark Report")
        report.append(f"Generated at: {time.strftime('%Y-%m-%d %H:%M:%S')}")
        report.append("")
        
        for config_name, results in benchmark_results.items():
            report.append(f"## Configuration: {config_name}")
            
            if "error" in results:
                report.append(f"**Error**: {results['error']}")
            else:
                report.append(f"- **Total Time**: {results['total_time']:.2f}s")
                report.append(f"- **Images/Second**: {results['images_per_second']:.2f}")
                report.append(f"- **Avg Time/Image**: {results['avg_time_per_image']:.2f}s")
                report.append(f"- **Quality Score**: {results.get('quality_score', 'N/A')}")
                report.append(f"- **Memory Usage**: {results.get('memory_usage', 'N/A')}")
            
            report.append("")
        
        return "\n".join(report)
```

## Optimization Strategies

### Automatic Performance Tuning
```python
class AutoPerformanceTuner:
    def __init__(self, performance_monitor):
        self.performance_monitor = performance_monitor
        self.optimization_history = []
        
    async def auto_tune_performance(self, current_config):
        """Automatically tune performance based on current metrics"""
        
        # Get current performance
        current_perf = self.performance_monitor.get_performance_summary(5)
        
        # Analyze performance and suggest optimizations
        optimizations = self._analyze_and_optimize(current_perf, current_config)
        
        # Apply optimizations
        optimized_config = self._apply_optimizations(current_config, optimizations)
        
        return optimized_config
    
    def _analyze_and_optimize(self, performance_metrics, config):
        """Analyze performance and suggest optimizations"""
        optimizations = []
        
        # High VRAM usage
        if performance_metrics.get("avg_vram_usage", 0) > 85:
            optimizations.append({
                "type": "reduce_batch_size",
                "reason": "High VRAM usage detected",
                "action": "Reduce batch size by 50%"
            })
        
        # High CPU usage
        if performance_metrics.get("avg_cpu_usage", 0) > 90:
            optimizations.append({
                "type": "reduce_threads",
                "reason": "High CPU usage detected",
                "action": "Reduce thread count"
            })
        
        # High memory usage
        if performance_metrics.get("avg_memory_usage", 0) > 85:
            optimizations.append({
                "type": "enable_memory_optimization",
                "reason": "High memory usage detected",
                "action": "Enable aggressive memory optimization"
            })
        
        return optimizations
    
    def _apply_optimizations(self, config, optimizations):
        """Apply suggested optimizations to configuration"""
        optimized_config = config.copy()
        
        for opt in optimizations:
            if opt["type"] == "reduce_batch_size":
                current_batch = optimized_config.get("batch_size", 4)
                optimized_config["batch_size"] = max(1, int(current_batch * 0.5))
            
            elif opt["type"] == "reduce_threads":
                current_threads = optimized_config.get("num_threads", 8)
                optimized_config["num_threads"] = max(4, int(current_threads * 0.75))
            
            elif opt["type"] == "enable_memory_optimization":
                optimized_config["enable_memory_optimization"] = True
                optimized_config["offload_to_cpu"] = True
        
        return optimized_config
```

### Configuration Presets
```python
class PerformancePresets:
    @staticmethod
    def get_speed_optimized_config():
        """Configuration optimized for speed"""
        return {
            "quality_mode": "fast",
            "batch_size": 6,
            "enable_memory_optimization": True,
            "enable_attention_slicing": True,
            "torch_dtype": "float16",
            "num_inference_steps": 20,
            "guidance_scale": 7.0
        }
    
    @staticmethod
    def get_quality_optimized_config():
        """Configuration optimized for quality"""
        return {
            "quality_mode": "high",
            "batch_size": 2,
            "enable_memory_optimization": False,
            "enable_attention_slicing": False,
            "torch_dtype": "float32",
            "num_inference_steps": 50,
            "guidance_scale": 9.0
        }
    
    @staticmethod
    def get_balanced_config():
        """Balanced configuration"""
        return {
            "quality_mode": "balanced",
            "batch_size": 4,
            "enable_memory_optimization": True,
            "enable_attention_slicing": True,
            "torch_dtype": "float16",
            "num_inference_steps": 30,
            "guidance_scale": 8.0
        }
    
    @staticmethod
    def get_memory_conservative_config():
        """Configuration for limited VRAM"""
        return {
            "quality_mode": "fast",
            "batch_size": 1,
            "enable_memory_optimization": True,
            "enable_attention_slicing": True,
            "enable_vae_slicing": True,
            "enable_cpu_offload": True,
            "torch_dtype": "float16",
            "num_inference_steps": 25,
            "guidance_scale": 7.5
        }

# Usage example
presets = PerformancePresets()

# For fast processing
fast_config = presets.get_speed_optimized_config()

# For high quality
quality_config = presets.get_quality_optimized_config()

# For balanced performance
balanced_config = presets.get_balanced_config()

# For limited VRAM
conservative_config = presets.get_memory_conservative_config()
```

## Best Practices Summary

### Memory Management Best Practices
1. **Monitor VRAM usage** constantly and adjust batch sizes dynamically
2. **Use model offloading** when VRAM is limited
3. **Enable attention and VAE slicing** for memory efficiency
4. **Implement automatic cleanup** between processing batches
5. **Use half precision (float16)** when possible

### Performance Optimization Best Practices
1. **Batch process images** whenever possible
2. **Cache loaded models** to avoid reloading
3. **Use performance presets** based on your hardware
4. **Monitor and tune** performance continuously
5. **Profile bottlenecks** and optimize accordingly

### Hardware Utilization Best Practices
1. **Utilize all available CPU cores** for preprocessing
2. **Maximize GPU utilization** without exceeding limits
3. **Use fast storage** (SSD/NVMe) for model loading
4. **Ensure adequate cooling** for sustained performance
5. **Monitor temperature** and throttling

### Configuration Best Practices
1. **Start with presets** and tune based on results
2. **Test different configurations** for your specific use case
3. **Document optimal settings** for different scenarios
4. **Implement fallback configurations** for error recovery
5. **Validate configurations** before processing

By following these optimization guidelines and best practices, you can achieve optimal performance from the Enhanced SDXL pipeline while maintaining high quality results and efficient resource utilization.
