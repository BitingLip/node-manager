"""
Metrics Collector
Collects performance metrics and system statistics for monitoring and optimization
Provides data for capacity planning and performance analysis
"""

import psutil
import time
import asyncio
import logging
from typing import Dict, List, Optional, Any, Callable
from datetime import datetime, timedelta
from dataclasses import dataclass
import structlog

logger = structlog.get_logger(__name__)


@dataclass
class SystemMetrics:
    """System performance metrics snapshot"""
    timestamp: datetime
    cpu_usage: float
    memory_total: int
    memory_used: int
    memory_available: int
    disk_total: int
    disk_used: int
    disk_available: int
    network_sent: int
    network_received: int
    gpu_metrics: Dict[str, Any]
    process_count: int
    load_average: float


class MetricsCollector:
    """
    Collects and manages system performance metrics
    Provides data for monitoring, alerting, and optimization
    """
    
    def __init__(self, collection_interval: int = 60, node_controller=None):
        """Initialize metrics collector"""
        self.collection_interval = collection_interval
        self.node_controller = node_controller
        self.metrics_history = []
        self.max_history_size = 1440  # 24 hours at 1-minute intervals
        
        # Callbacks for metrics
        self.metrics_callbacks = []
          # Collection task
        self.collection_task = None
        self.running = False
        
        logger.info(f"MetricsCollector initialized with {collection_interval}s interval")
    
    async def start_collection(self):
        """Start metrics collection"""
        if self.running:
            logger.warning("Metrics collection already running")
            return
            
        self.running = True
        self.collection_task = asyncio.create_task(self._collection_loop())
        logger.info("Metrics collection started")
    async def stop_collection(self):
        """Stop metrics collection"""
        if not self.running:
            return
            
        self.running = False        
        if self.collection_task:
            self.collection_task.cancel()
            from contextlib import suppress
            with suppress(asyncio.CancelledError):
                await self.collection_task
        
        logger.info("Metrics collection stopped")
    
    def collect_current_metrics(self) -> SystemMetrics:
        """Collect current system metrics"""
        try:
            # CPU usage
            cpu_usage = psutil.cpu_percent(interval=1)
            
            # Memory usage
            memory = psutil.virtual_memory()
            
            # Disk usage (for root filesystem)
            disk = psutil.disk_usage('/')
            
            # Network I/O
            network = psutil.net_io_counters()
            
            # Process count
            process_count = len(psutil.pids())
            
            # Load average (Unix-like systems)
            try:
                load_average = psutil.getloadavg()[0]
            except (AttributeError, OSError):
                load_average = 0.0
            
            # GPU metrics
            gpu_metrics = self.collect_gpu_metrics()
            
            return SystemMetrics(
                timestamp=datetime.now(),
                cpu_usage=cpu_usage,
                memory_total=memory.total,
                memory_used=memory.used,
                memory_available=memory.available,
                disk_total=disk.total,
                disk_used=disk.used,
                disk_available=disk.free,
                network_sent=network.bytes_sent if network else 0,
                network_received=network.bytes_recv if network else 0,
                gpu_metrics=gpu_metrics,
                process_count=process_count,
                load_average=load_average
            )
        except Exception as e:
            logger.error("Failed to collect system metrics", error=str(e))
            # Return empty metrics on error
            return SystemMetrics(
                timestamp=datetime.now(),
                cpu_usage=0.0,
                memory_total=0,
                memory_used=0,
                memory_available=0,
                disk_total=0,
                disk_used=0,
                disk_available=0,
                network_sent=0,
                network_received=0,
                gpu_metrics={},
                process_count=0,                load_average=0.0
            )
    def collect_gpu_metrics(self) -> Dict[str, Any]:
        """Collect GPU-specific metrics"""
        gpu_metrics = {}
        
        # Method 1: Try NVIDIA GPUs first
        try:
            import pynvml
            pynvml.nvmlInit()
            device_count = pynvml.nvmlDeviceGetCount()
            
            for i in range(device_count):
                handle = pynvml.nvmlDeviceGetHandleByIndex(i)
                name = pynvml.nvmlDeviceGetName(handle).decode('utf-8')
                
                # Memory info
                mem_info = pynvml.nvmlDeviceGetMemoryInfo(handle)
                
                # Utilization
                util = pynvml.nvmlDeviceGetUtilizationRates(handle)
                
                # Temperature
                temp = pynvml.nvmlDeviceGetTemperature(handle, pynvml.NVML_TEMPERATURE_GPU)
                
                gpu_metrics[f"nvidia_gpu_{i}"] = {
                    "name": name,
                    "vendor": "NVIDIA",
                    "memory_total": mem_info.total,
                    "memory_used": mem_info.used,
                    "memory_free": mem_info.free,
                    "memory_utilization": (mem_info.used / mem_info.total) * 100,
                    "gpu_utilization": util.gpu,
                    "memory_bandwidth_utilization": util.memory,
                    "temperature": temp
                }
                
            if device_count > 0:
                logger.info(f"Detected {device_count} NVIDIA GPU(s)")
                
        except Exception as e:
            logger.debug(f"NVIDIA GPU detection failed: {e}")
        
        # Method 2: Try AMD GPUs using Windows WMI
        try:
            import subprocess
            import json
            
            # Use PowerShell to query AMD GPU devices
            ps_cmd = '''
            Get-WmiObject -Class Win32_VideoController | 
            Where-Object {$_.Name -like "*AMD*" -or $_.Name -like "*Radeon*" -or $_.Name -like "*RX*"} | 
            Select-Object Name, AdapterRAM, Status, VideoModeDescription, PNPDeviceID |
            ConvertTo-Json
            '''
            
            result = subprocess.run(
                ["powershell", "-Command", ps_cmd],
                capture_output=True,
                text=True,
                timeout=15
            )
            
            if result.returncode == 0 and result.stdout.strip():
                devices_data = result.stdout.strip()
                try:
                    devices = json.loads(devices_data)
                    if not isinstance(devices, list):
                        devices = [devices]
                    
                    amd_count = 0
                    for device in devices:
                        gpu_name = device.get('Name', 'Unknown AMD GPU')
                        adapter_ram = device.get('AdapterRAM', 0)
                        status = device.get('Status', 'Unknown')
                        pnp_id = device.get('PNPDeviceID', '')
                        
                        # Convert RAM from bytes to MB/GB if available
                        if adapter_ram and adapter_ram > 0:
                            vram_gb = adapter_ram / (1024**3)
                        else:
                            vram_gb = 0
                        
                        gpu_metrics[f"amd_gpu_{amd_count}"] = {
                            "name": gpu_name,
                            "vendor": "AMD",
                            "vram_total_gb": round(vram_gb, 1),
                            "status": status,
                            "detection_method": "wmi",
                            "driver_available": status == "OK",
                            "pnp_device_id": pnp_id
                        }
                        
                        # Special handling for RX 6800 series based on your hardware
                        if "6800" in gpu_name:
                            if "XT" in gpu_name:
                                gpu_metrics[f"amd_gpu_{amd_count}"]["expected_vram_gb"] = 16
                                gpu_metrics[f"amd_gpu_{amd_count}"]["gpu_family"] = "RDNA2"
                                gpu_metrics[f"amd_gpu_{amd_count}"]["ai_capable"] = True
                                gpu_metrics[f"amd_gpu_{amd_count}"]["directml_support"] = True
                            else:
                                gpu_metrics[f"amd_gpu_{amd_count}"]["expected_vram_gb"] = 16
                                gpu_metrics[f"amd_gpu_{amd_count}"]["gpu_family"] = "RDNA2"
                                gpu_metrics[f"amd_gpu_{amd_count}"]["ai_capable"] = True
                                gpu_metrics[f"amd_gpu_{amd_count}"]["directml_support"] = True
                        
                        amd_count += 1
                    
                    if amd_count > 0:
                        logger.info(f"Detected {amd_count} AMD GPU(s) via WMI")
                        
                except json.JSONDecodeError as e:
                    logger.debug(f"Failed to parse WMI GPU data: {e}")
                    
        except Exception as e:
            logger.debug(f"WMI AMD GPU detection failed: {e}")
        
        # Method 3: Try DirectML detection for AMD GPUs
        try:
            import torch
            # Check if DirectML is available
            if hasattr(torch, 'directml'):
                try:
                    # Try to create a DirectML device
                    device = torch.device('dml')
                    test_tensor = torch.tensor([1.0], device=device)
                    
                    gpu_metrics["directml_backend"] = {
                        "name": "DirectML Backend",
                        "vendor": "AMD",
                        "status": "available",
                        "device_type": "directml",
                        "torch_version": torch.__version__,
                        "ai_capable": True,
                        "framework": "torch-directml"
                    }
                    logger.info("DirectML backend detected - AMD GPU AI support available")
                    
                except Exception as directml_error:
                    logger.debug(f"DirectML device test failed: {directml_error}")
            else:
                try:
                    import torch_directml
                    gpu_metrics["directml_backend"] = {
                        "name": "DirectML Backend",
                        "vendor": "AMD",
                        "status": "available (torch-directml)",
                        "device_type": "directml",
                        "torch_version": torch.__version__,
                        "ai_capable": True,
                        "framework": "torch-directml"
                    }
                    logger.info("DirectML backend detected via torch-directml - AMD GPU AI support available")
                except ImportError:
                    logger.debug("torch-directml not available for DirectML detection")
                    
        except ImportError:
            logger.debug("PyTorch not available for DirectML detection")
        except Exception as e:
            logger.debug(f"DirectML detection failed: {e}")
        
        # Method 4: Try ROCm detection (mostly for Linux/WSL)
        try:
            import subprocess
            result = subprocess.run(
                ["rocm-smi", "--showgpus"],
                capture_output=True,
                text=True,
                timeout=5
            )
            
            if result.returncode == 0 and "GPU" in result.stdout:
                gpu_metrics["rocm_backend"] = {
                    "status": "available",
                    "detection_method": "rocm-smi",
                    "vendor": "AMD",
                    "framework": "ROCm"
                }
                logger.info("ROCm detected for AMD GPUs")
                
        except Exception:
            # ROCm not available (expected on Windows)
            pass
        
        # Summary logging
        total_gpus = len([k for k in gpu_metrics.keys() if k.startswith(('nvidia_gpu_', 'amd_gpu_'))])
        if total_gpus > 0:
            logger.info(f"Total GPUs detected: {total_gpus}")
        else:
            logger.warning("No GPUs detected")
        
        return gpu_metrics
    def collect_worker_metrics(self) -> Dict[str, Any]:
        """Collect worker process metrics"""
        worker_metrics = {}
        
        try:
            if self.node_controller:
                worker_manager = getattr(self.node_controller, 'worker_manager', None)
                if worker_manager:
                    # Get worker pools and their metrics
                    pools = getattr(worker_manager, 'worker_pools', {})
                    
                    for pool_id, pool in pools.items():
                        pool_metrics = {
                            "worker_count": len(getattr(pool, 'workers', {})),
                            "active_workers": 0,
                            "total_tasks": 0,
                            "failed_tasks": 0,
                            "average_load": 0.0
                        }
                        
                        # Calculate pool statistics
                        workers = getattr(pool, 'workers', {})
                        loads = getattr(pool, 'worker_loads', {})
                        
                        if workers:
                            pool_metrics["active_workers"] = sum(1 for load in loads.values() if load > 0.1)
                            pool_metrics["average_load"] = sum(loads.values()) / len(loads) if loads else 0.0
                        
                        # Get pool metrics if available
                        if hasattr(pool, 'metrics'):
                            pool_stats = pool.metrics
                            pool_metrics["total_tasks"] = getattr(pool_stats, 'total_tasks_processed', 0)
                            
                        worker_metrics[pool_id] = pool_metrics
                        
            # Collect process-level metrics for worker processes
            for proc in psutil.process_iter(['pid', 'name', 'cpu_percent', 'memory_info']):
                try:
                    pinfo = proc.info
                    if 'worker' in pinfo['name'].lower():
                        worker_metrics[f"process_{pinfo['pid']}"] = {
                            "name": pinfo['name'],
                            "cpu_percent": pinfo['cpu_percent'],
                            "memory_mb": pinfo['memory_info'].rss / 1024 / 1024 if pinfo['memory_info'] else 0
                        }
                except (psutil.NoSuchProcess, psutil.AccessDenied):
                    continue
                    
        except Exception as e:
            logger.error("Failed to collect worker metrics", error=str(e))
            
        return worker_metrics
    
    def get_metrics_summary(self, hours: int = 1) -> Dict[str, Any]:
        """Get summarized metrics for specified time period"""
        cutoff_time = datetime.now() - timedelta(hours=hours)
        recent_metrics = [m for m in self.metrics_history if m.timestamp >= cutoff_time]
        
        if not recent_metrics:
            return {"error": "No metrics available for time period"}
        
        # Calculate averages and extremes
        cpu_values = [m.cpu_usage for m in recent_metrics]
        memory_values = [m.memory_used for m in recent_metrics]
        
        summary = {
            "time_period_hours": hours,
            "sample_count": len(recent_metrics),
            "cpu_usage": {
                "average": sum(cpu_values) / len(cpu_values),
                "min": min(cpu_values),
                "max": max(cpu_values)
            },
            "memory_usage": {
                "average": sum(memory_values) / len(memory_values),
                "min": min(memory_values),
                "max": max(memory_values),
                "average_percent": (sum(memory_values) / len(memory_values)) / recent_metrics[0].memory_total * 100
            },
            "latest_timestamp": recent_metrics[-1].timestamp.isoformat()
        }
        
        return summary

    def get_resource_trends(self) -> Dict[str, Any]:
        """Get resource usage trends"""
        if len(self.metrics_history) < 10:
            return {"error": "Insufficient data for trend analysis"}
        
        # Get recent metrics for trend analysis
        recent_metrics = self.metrics_history[-60:]  # Last hour if collecting every minute
        
        # Calculate trends (simple linear regression slope)
        def calculate_trend(values):
            n = len(values)
            if n < 2:
                return 0
            x_sum = sum(range(n))
            y_sum = sum(values)
            xy_sum = sum(i * values[i] for i in range(n))
            x2_sum = sum(i * i for i in range(n))
            return (n * xy_sum - x_sum * y_sum) / (n * x2_sum - x_sum * x_sum)
        
        cpu_values = [m.cpu_usage for m in recent_metrics]
        memory_values = [m.memory_used for m in recent_metrics]
        
        trends = {
            "cpu_usage_trend": calculate_trend(cpu_values),
            "memory_usage_trend": calculate_trend(memory_values),
            "analysis_period_minutes": len(recent_metrics),
            "prediction": {
                "cpu_direction": "increasing" if calculate_trend(cpu_values) > 0.1 else "stable",
                "memory_direction": "increasing" if calculate_trend(memory_values) > 0 else "stable"
            }
        }
        
        return trends
    
    def register_metrics_callback(self, callback: Callable[[SystemMetrics], None]):
        """Register callback for metrics updates"""
        # TODO: Add callback for metrics notifications
        self.metrics_callbacks.append(callback)
    
    def export_metrics(self, format: str = "json") -> str:
        """Export metrics in specified format"""
        if not self.metrics_history:
            return ""
        
        try:
            if format.lower() == "json":
                import json
                data = {
                    "collection_info": {
                        "total_samples": len(self.metrics_history),
                        "collection_interval": self.collection_interval,
                        "first_sample": self.metrics_history[0].timestamp.isoformat() if self.metrics_history else None,
                        "last_sample": self.metrics_history[-1].timestamp.isoformat() if self.metrics_history else None
                    },
                    "metrics": [
                        {
                            "timestamp": m.timestamp.isoformat(),
                            "cpu_usage": m.cpu_usage,
                            "memory_used": m.memory_used,
                            "memory_total": m.memory_total,
                            "disk_used": m.disk_used,
                            "disk_total": m.disk_total,
                            "gpu_metrics": m.gpu_metrics,
                            "process_count": m.process_count,
                            "load_average": m.load_average
                        }
                        for m in self.metrics_history
                    ]
                }
                return json.dumps(data, indent=2)
                
            elif format.lower() == "csv":
                import csv
                import io
                
                output = io.StringIO()
                writer = csv.writer(output)
                
                # Write header
                writer.writerow([
                    "timestamp", "cpu_usage", "memory_used", "memory_total", 
                    "disk_used", "disk_total", "process_count", "load_average"
                ])
                
                # Write data
                for m in self.metrics_history:
                    writer.writerow([
                        m.timestamp.isoformat(),
                        m.cpu_usage,
                        m.memory_used,
                        m.memory_total,
                        m.disk_used,
                        m.disk_total,
                        m.process_count,
                        m.load_average
                    ])
                
                return output.getvalue()
                
            elif format.lower() == "prometheus":
                # Basic Prometheus format
                lines = []
                if self.metrics_history:
                    latest = self.metrics_history[-1]
                    timestamp = int(latest.timestamp.timestamp() * 1000)
                    
                    lines.extend([
                        f"node_cpu_usage {latest.cpu_usage} {timestamp}",
                        f"node_memory_used_bytes {latest.memory_used} {timestamp}",
                        f"node_memory_total_bytes {latest.memory_total} {timestamp}",
                        f"node_disk_used_bytes {latest.disk_used} {timestamp}",
                        f"node_disk_total_bytes {latest.disk_total} {timestamp}",
                        f"node_process_count {latest.process_count} {timestamp}",
                        f"node_load_average {latest.load_average} {timestamp}"
                    ])
                
                return "\n".join(lines)
                
        except Exception as e:
            logger.error("Failed to export metrics", format=format, error=str(e))
            return f"Export error: {str(e)}"
        
        return f"Unsupported format: {format}"
    
    async def _collection_loop(self):
        """Main metrics collection loop"""
        while self.running:
            try:
                # Collect current metrics
                metrics = self.collect_current_metrics()
                
                # Add to history
                self.metrics_history.append(metrics)
                
                # Clean up old metrics
                self._cleanup_old_metrics()
                
                # Call registered callbacks
                for callback in self.metrics_callbacks:
                    try:
                        if asyncio.iscoroutinefunction(callback):
                            await callback(metrics)
                        else:
                            callback(metrics)
                    except Exception as e:
                        logger.error("Metrics callback failed", error=str(e))
                
                # Wait for next collection interval
                await asyncio.sleep(self.collection_interval)
                
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error("Error in metrics collection loop", error=str(e))
                await asyncio.sleep(5)  # Brief pause before retry
    
    def _cleanup_old_metrics(self):
        """Clean up old metrics to maintain memory limits"""
        # TODO: Remove old metrics beyond max_history_size
        if len(self.metrics_history) > self.max_history_size:
            self.metrics_history = self.metrics_history[-self.max_history_size:]
