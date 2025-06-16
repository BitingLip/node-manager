#!/usr/bin/env python3
"""
System Monitor - Monitors system resources and performance metrics
"""
import time
import threading
import psutil
from typing import Dict, Any, Optional
from datetime import datetime, timedelta

try:
    import GPUtil
    GPU_AVAILABLE = True
except ImportError:
    GPU_AVAILABLE = False


class SystemMonitor:
    """Monitors system resources and performance metrics"""
    
    def __init__(self, logger, database, config: Dict):
        self.logger = logger
        self.database = database
        self.config = config
        
        # Configuration
        self.monitoring_interval = config.get("monitoring_interval", 30)  # seconds
        self.vram_monitoring = config.get("vram_monitoring", True)
        self.cleanup_interval = config.get("cleanup_interval", 60)  # seconds
        self.memory_threshold = config.get("memory_threshold", 0.9)  # 90%
        self.store_metrics = config.get("store_metrics", True)
        
        # Runtime state
        self.monitoring_active = False
        self.monitoring_thread: Optional[threading.Thread] = None
        self.last_metrics: Optional[Dict[str, Any]] = None
        self.metrics_history: list = []
        self.max_history_items = 100
        
        self.logger.info("SystemMonitor initialized")
        
    def start_monitoring(self):
        """Start system monitoring"""
        if self.monitoring_active:
            self.logger.warning("System monitoring already active")
            return
            
        self.monitoring_active = True
        self.monitoring_thread = threading.Thread(target=self._monitoring_loop, daemon=True)
        self.monitoring_thread.start()
        
        self.logger.info("System monitoring started")
        
    def stop_monitoring(self):
        """Stop system monitoring"""
        if not self.monitoring_active:
            return
            
        self.monitoring_active = False
        
        if self.monitoring_thread:
            self.monitoring_thread.join(timeout=5)
            
        self.logger.info("System monitoring stopped")
        
    def _monitoring_loop(self):
        """Main monitoring loop"""
        while self.monitoring_active:
            try:
                # Collect metrics
                metrics = self.collect_metrics()
                self.last_metrics = metrics
                
                # Add to history
                self._add_to_history(metrics)
                
                # Store in database if enabled
                if self.store_metrics:
                    self.store_metrics_data(metrics)
                
                # Check for alerts
                self._check_alerts(metrics)
                
                # Sleep until next collection
                time.sleep(self.monitoring_interval)
                
            except Exception as e:
                self.logger.error(f"Monitoring loop error: {e}")
                time.sleep(5)  # Short delay before retrying
                
    def collect_metrics(self) -> Dict[str, Any]:
        """Collect current system metrics"""
        try:
            metrics = {
                'timestamp': time.time(),
                'datetime': datetime.now().isoformat(),
                'cpu': self._collect_cpu_metrics(),
                'memory': self._collect_memory_metrics(),
                'disk': self._collect_disk_metrics(),
                'network': self._collect_network_metrics(),
            }
            
            # Add GPU metrics if available
            if GPU_AVAILABLE and self.vram_monitoring:
                metrics['gpu'] = self._collect_gpu_metrics()
            
            return metrics
            
        except Exception as e:
            self.logger.error(f"Failed to collect metrics: {e}")
            return {'timestamp': time.time(), 'error': str(e)}
            
    def _collect_cpu_metrics(self) -> Dict[str, Any]:
        """Collect CPU metrics"""
        try:
            return {
                'usage_percent': psutil.cpu_percent(interval=1),
                'count': psutil.cpu_count(),
                'count_logical': psutil.cpu_count(logical=True),
                'load_avg': psutil.getloadavg() if hasattr(psutil, 'getloadavg') else None,
                'freq': psutil.cpu_freq()._asdict() if psutil.cpu_freq() else None
            }
        except Exception as e:
            self.logger.error(f"Failed to collect CPU metrics: {e}")
            return {'error': str(e)}
            
    def _collect_memory_metrics(self) -> Dict[str, Any]:
        """Collect memory metrics"""
        try:
            virtual_mem = psutil.virtual_memory()
            swap_mem = psutil.swap_memory()
            
            return {
                'virtual': {
                    'total_gb': virtual_mem.total / (1024**3),
                    'available_gb': virtual_mem.available / (1024**3),
                    'used_gb': virtual_mem.used / (1024**3),
                    'percent': virtual_mem.percent
                },
                'swap': {
                    'total_gb': swap_mem.total / (1024**3),
                    'used_gb': swap_mem.used / (1024**3),
                    'percent': swap_mem.percent
                }
            }
        except Exception as e:
            self.logger.error(f"Failed to collect memory metrics: {e}")
            return {'error': str(e)}
            
    def _collect_disk_metrics(self) -> Dict[str, Any]:
        """Collect disk metrics"""
        try:
            disk_usage = psutil.disk_usage('/')
            disk_io = psutil.disk_io_counters()
            
            metrics = {
                'usage': {
                    'total_gb': disk_usage.total / (1024**3),
                    'used_gb': disk_usage.used / (1024**3),
                    'free_gb': disk_usage.free / (1024**3),
                    'percent': (disk_usage.used / disk_usage.total) * 100
                }
            }
            
            if disk_io:
                metrics['io'] = {
                    'read_bytes': disk_io.read_bytes,
                    'write_bytes': disk_io.write_bytes,
                    'read_count': disk_io.read_count,
                    'write_count': disk_io.write_count
                }
            
            return metrics
            
        except Exception as e:
            self.logger.error(f"Failed to collect disk metrics: {e}")
            return {'error': str(e)}
            
    def _collect_network_metrics(self) -> Dict[str, Any]:
        """Collect network metrics"""
        try:
            net_io = psutil.net_io_counters()
            
            if net_io:
                return {
                    'bytes_sent': net_io.bytes_sent,
                    'bytes_recv': net_io.bytes_recv,
                    'packets_sent': net_io.packets_sent,
                    'packets_recv': net_io.packets_recv,
                    'errin': net_io.errin,
                    'errout': net_io.errout                }
            else:
                return {}
                
        except Exception as e:
            self.logger.error(f"Failed to collect network metrics: {e}")
            return {'error': str(e)}
            
    def _collect_gpu_metrics(self) -> Dict[str, Any]:
        """Collect GPU metrics"""
        try:
            if not GPU_AVAILABLE:
                return {'error': 'GPUtil not available'}
                
            import GPUtil
            gpus = GPUtil.getGPUs()
            gpu_metrics = []
            
            for gpu in gpus:
                gpu_metrics.append({
                    'id': gpu.id,
                    'name': gpu.name,
                    'load': gpu.load * 100,  # Convert to percentage
                    'memory_used_mb': gpu.memoryUsed,
                    'memory_total_mb': gpu.memoryTotal,
                    'memory_percent': (gpu.memoryUsed / gpu.memoryTotal) * 100,
                    'temperature': gpu.temperature
                })
            
            return {
                'count': len(gpus),
                'devices': gpu_metrics
            }
            
        except Exception as e:
            self.logger.error(f"Failed to collect GPU metrics: {e}")
            return {'error': str(e)}
            
    def _add_to_history(self, metrics: Dict[str, Any]):
        """Add metrics to history with size limit"""
        self.metrics_history.append(metrics)
        
        # Keep only recent history
        if len(self.metrics_history) > self.max_history_items:
            self.metrics_history = self.metrics_history[-self.max_history_items:]
            
    def _check_alerts(self, metrics: Dict[str, Any]):
        """Check for system alerts based on thresholds"""
        try:
            # Check memory usage
            memory_percent = metrics.get('memory', {}).get('virtual', {}).get('percent', 0)
            if memory_percent > self.memory_threshold * 100:
                self.logger.warning(f"High memory usage: {memory_percent:.1f}%")
            
            # Check disk usage
            disk_percent = metrics.get('disk', {}).get('usage', {}).get('percent', 0)
            if disk_percent > 85:  # Alert at 85% disk usage
                self.logger.warning(f"High disk usage: {disk_percent:.1f}%")
            
            # Check GPU memory if available
            if 'gpu' in metrics and 'devices' in metrics['gpu']:
                for gpu in metrics['gpu']['devices']:
                    if gpu.get('memory_percent', 0) > 90:
                        self.logger.warning(f"High GPU memory usage on {gpu['name']}: {gpu['memory_percent']:.1f}%")
                        
        except Exception as e:
            self.logger.error(f"Alert checking failed: {e}")
            
    def store_metrics_data(self, metrics: Dict[str, Any]):
        """Store metrics in database"""
        try:
            if self.database and self.database.connected:
                self.database.store_system_metrics(metrics)
        except Exception as e:
            self.logger.error(f"Failed to store metrics: {e}")
                
    def get_latest_metrics(self) -> Optional[Dict[str, Any]]:
        """Get the latest collected metrics"""
        return self.last_metrics
        
    def get_metrics_summary(self) -> Dict[str, Any]:
        """Get a summary of recent metrics"""
        try:
            if not self.metrics_history:
                return {'error': 'No metrics available'}
            
            recent_metrics = self.metrics_history[-10:]  # Last 10 samples
            
            # Calculate averages
            cpu_usage = [m.get('cpu', {}).get('usage_percent', 0) for m in recent_metrics if 'cpu' in m]
            memory_usage = [m.get('memory', {}).get('virtual', {}).get('percent', 0) for m in recent_metrics if 'memory' in m]
            
            summary = {
                'sample_count': len(recent_metrics),
                'time_range': {
                    'start': recent_metrics[0].get('datetime'),
                    'end': recent_metrics[-1].get('datetime')
                },
                'averages': {
                    'cpu_percent': sum(cpu_usage) / len(cpu_usage) if cpu_usage else 0,
                    'memory_percent': sum(memory_usage) / len(memory_usage) if memory_usage else 0
                },
                'latest': self.last_metrics
            }
            
            return summary
            
        except Exception as e:
            self.logger.error(f"Failed to generate metrics summary: {e}")
            return {'error': str(e)}
            
    def get_metrics_history(self, limit: int = 50) -> list:
        """Get recent metrics history"""
        return self.metrics_history[-limit:] if self.metrics_history else []
        
    def clear_history(self):
        """Clear metrics history"""
        self.metrics_history.clear()
        self.logger.info("Metrics history cleared")
        
    def get_system_health_status(self) -> Dict[str, Any]:
        """Get overall system health status"""
        if not self.last_metrics:
            return {'status': 'unknown', 'message': 'No metrics available'}
            
        try:
            issues = []
            
            # Check various metrics for issues
            memory_percent = self.last_metrics.get('memory', {}).get('virtual', {}).get('percent', 0)
            if memory_percent > 90:
                issues.append(f"High memory usage: {memory_percent:.1f}%")
                
            cpu_percent = self.last_metrics.get('cpu', {}).get('usage_percent', 0)
            if cpu_percent > 90:
                issues.append(f"High CPU usage: {cpu_percent:.1f}%")
                
            disk_percent = self.last_metrics.get('disk', {}).get('usage', {}).get('percent', 0)
            if disk_percent > 85:
                issues.append(f"High disk usage: {disk_percent:.1f}%")
            
            # Check GPU if available
            if 'gpu' in self.last_metrics and 'devices' in self.last_metrics['gpu']:
                for gpu in self.last_metrics['gpu']['devices']:
                    if gpu.get('memory_percent', 0) > 90:
                        issues.append(f"High GPU memory on {gpu['name']}: {gpu['memory_percent']:.1f}%")
            
            # Determine overall status
            if not issues:
                return {'status': 'healthy', 'message': 'All systems normal'}
            elif len(issues) <= 2:
                return {'status': 'warning', 'message': 'Some issues detected', 'issues': issues}
            else:
                return {'status': 'critical', 'message': 'Multiple issues detected', 'issues': issues}
                
        except Exception as e:
            self.logger.error(f"Failed to determine system health: {e}")
            return {'status': 'error', 'message': f'Health check failed: {e}'}
