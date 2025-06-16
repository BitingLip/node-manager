#!/usr/bin/env python3
"""
Hardware - Simplified hardware monitoring and metrics collection
"""
import time
import psutil
from typing import Dict, Any, Optional


class Hardware:
    """Simplified hardware monitoring and metrics collection"""
    
    def __init__(self, device_id: int, logger):
        self.device_id = device_id
        self.logger = logger
        
        # GPU libraries (try to import)
        self.torch = None
        self.torch_directml = None
        self._init_gpu_monitoring()
        
        self.logger.info(f"Hardware monitor initialized for GPU {device_id}")
    
    def _init_gpu_monitoring(self):
        """Initialize GPU monitoring libraries"""
        try:
            import torch
            self.torch = torch
            self.logger.info("PyTorch available for hardware monitoring")
            
            try:
                import torch_directml
                self.torch_directml = torch_directml
                self.logger.info("DirectML available for GPU monitoring")
            except ImportError:
                self.logger.warning("DirectML not available for GPU monitoring")
                
        except ImportError:
            self.logger.warning("PyTorch not available for hardware monitoring")
    
    def get_current_metrics(self) -> Dict[str, Any]:
        """Get current hardware metrics on demand"""
        timestamp = time.time()
        
        # Basic CPU metrics
        cpu_usage = psutil.cpu_percent(interval=0.1)
        memory = psutil.virtual_memory()
        cpu_ram_mb = memory.used // (1024 * 1024)  # MB
        cpu_ram_percent = memory.percent
        
        # GPU metrics
        gpu_metrics = self._get_gpu_metrics()
        
        metrics = {
            'timestamp': timestamp,
            'worker_id': f"worker_{self.device_id}",
            'device_id': self.device_id,
            
            # Essential CPU metrics
            'cpu_usage': cpu_usage,
            'cpu_ram_mb': cpu_ram_mb,
            'cpu_ram_percent': cpu_ram_percent,
            
            # Essential GPU metrics
            'gpu_available': gpu_metrics['available'],
            'gpu_vram_mb': gpu_metrics['vram_mb'],
            'gpu_vram_total_mb': gpu_metrics['vram_total_mb']
        }
        
        return metrics
    
    def _get_gpu_metrics(self) -> Dict[str, Any]:
        """Get essential GPU metrics only"""
        metrics = {
            'available': False,
            'vram_mb': 0,
            'vram_total_mb': 0
        }
        
        if not self.torch or not self.torch_directml:
            return metrics
        
        try:
            # Check if device is available
            device_count = self.torch_directml.device_count()
            if self.device_id >= device_count:
                return metrics
            
            metrics['available'] = True
            
            # Try to get VRAM usage
            device = f"privateuseone:{self.device_id}"
            
            # Create a small tensor to check device access
            try:
                test_tensor = self.torch.randn(10, device=device)
                del test_tensor
                
                # Estimate VRAM usage (DirectML doesn't have direct VRAM query)
                vram_estimate = self._estimate_vram_usage()
                metrics['vram_mb'] = vram_estimate
                metrics['vram_total_mb'] = 8192  # Assume 8GB cards by default
                
            except Exception as e:
                self.logger.debug(f"GPU memory check failed: {e}")
                
        except Exception as e:
            self.logger.debug(f"GPU metrics collection failed: {e}")
        
        return metrics
    
    def _estimate_vram_usage(self) -> int:
        """Estimate VRAM usage (DirectML doesn't provide direct query)"""
        try:
            # This is a rough estimation - DirectML doesn't expose VRAM directly
            # We'll use a heuristic based on process memory and known model sizes
            process = psutil.Process()
            process_memory_mb = process.memory_info().rss // (1024 * 1024)
            
            # Rough estimation: VRAM usage is typically 60-80% of process memory
            # for ML workloads (rest is CPU RAM, overhead, etc.)
            estimated_vram = int(process_memory_mb * 0.7)
            
            # Cap at reasonable values
            return min(estimated_vram, 8192)  # Max 8GB
            
        except Exception:
            return 0
    
    def get_gpu_status(self) -> Dict[str, Any]:
        """Get simplified GPU status"""
        metrics = self.get_current_metrics()
        
        return {
            'device_id': self.device_id,
            'available': metrics.get('gpu_available', False),
            'vram_usage_mb': metrics.get('gpu_vram_mb', 0),
            'vram_total_mb': metrics.get('gpu_vram_total_mb', 0),
            'vram_usage_percent': (
                (metrics.get('gpu_vram_mb', 0) / metrics.get('gpu_vram_total_mb', 1)) * 100
                if metrics.get('gpu_vram_total_mb', 0) > 0 else 0
            )
        }
    
    def check_resource_availability(self) -> Dict[str, bool]:
        """Check if resources are available for new tasks"""
        metrics = self.get_current_metrics()
        
        # Define thresholds
        GPU_VRAM_THRESHOLD = 7000  # MB
        CPU_RAM_THRESHOLD = 80     # Percent
        
        return {
            'gpu_vram_available': metrics.get('gpu_vram_mb', 0) < GPU_VRAM_THRESHOLD,
            'cpu_ram_available': metrics.get('cpu_ram_percent', 0) < CPU_RAM_THRESHOLD,
            'overall_available': (
                metrics.get('gpu_vram_mb', 0) < GPU_VRAM_THRESHOLD and
                metrics.get('cpu_ram_percent', 0) < CPU_RAM_THRESHOLD
            )
        }
    
    def get_system_info(self) -> Dict[str, Any]:
        """Get static system information"""
        try:
            return {
                'platform': 'windows' if psutil.WINDOWS else 'linux',
                'cpu_cores': psutil.cpu_count(logical=False),
                'cpu_threads': psutil.cpu_count(logical=True),
                'total_ram_gb': psutil.virtual_memory().total // (1024**3),
                'gpu_device_id': self.device_id,
                'gpu_available': self.torch_directml is not None,
                'torch_version': self.torch.__version__ if self.torch else None
            }
        except Exception as e:
            self.logger.error(f"Failed to get system info: {e}")
            return {}
