"""
Health Monitor
Monitors system health, worker status, and node operations
Provides health checks and alerting capabilities
"""

import asyncio
import logging
import psutil
import contextlib
from typing import Dict, List, Optional, Any, Callable
from datetime import datetime, timedelta
import structlog
import psutil

logger = structlog.get_logger(__name__)


class HealthStatus:
    """Health status information"""
    
    def __init__(self, status: str, details: Dict[str, Any]):
        self.status = status  # healthy, warning, critical, unknown
        self.details = details
        self.timestamp = datetime.now()


class HealthMonitor:
    """
    Monitors health of node components and system resources
    Provides health checks, alerting, and diagnostics
    """
    
    def __init__(self, node_controller=None):
        """Initialize health monitor"""
        self.node_controller = node_controller
        self.health_checks = {}  # check_name -> check_function
        self.health_status = {}  # component -> HealthStatus
        self.alert_callbacks = []  # List of alert callback functions
        
        # Monitoring configuration
        self.check_interval = 60  # seconds
        self.alert_thresholds = {
            "cpu_usage": 80.0,
            "memory_usage": 85.0,
            "disk_usage": 90.0,
            "gpu_memory_usage": 90.0,
            "worker_error_rate": 10.0
        }
        
        # Monitoring task
        self.monitoring_task = None
        
        # Register default health checks
        self._register_default_checks()
        
        logger.info("HealthMonitor initialized")
    
    def _register_default_checks(self):
        """Register default health checks"""
        self.health_checks["cpu_usage"] = self._check_cpu_usage
        self.health_checks["memory_usage"] = self._check_memory_usage
        self.health_checks["disk_usage"] = self._check_disk_usage
        self.health_checks["worker_health"] = self._check_worker_health
        
        # Initialize health status
        for check_name in self.health_checks:
            self.health_status[check_name] = HealthStatus("unknown", {"message": "Not yet checked"})

    def register_health_check(self, name: str, check_function: Callable) -> bool:
        """Register a custom health check"""
        try:
            if not callable(check_function):
                logger.error("Health check function must be callable", name=name)
                return False
            
            self.health_checks[name] = check_function
            self.health_status[name] = HealthStatus("unknown", {"message": "Not yet checked"})
            logger.info("Health check registered", name=name)
            return True
            
        except Exception as e:
            logger.error("Failed to register health check", name=name, error=str(e))
            return False
    
    async def run_health_check(self, check_name: str) -> HealthStatus:
        """Run a specific health check"""
        if check_name not in self.health_checks:
            return HealthStatus("unknown", {"error": f"Health check '{check_name}' not found"})
        
        try:
            check_function = self.health_checks[check_name]
            
            if asyncio.iscoroutinefunction(check_function):
                status = await check_function()
            else:
                status = check_function()
            
            self.health_status[check_name] = status
            
            # Trigger alerts if needed
            if status.status in ["warning", "critical"]:
                self._trigger_alert(check_name, status)
            
            return status
            
        except Exception as e:
            error_status = HealthStatus("critical", {"error": str(e), "check": check_name})
            self.health_status[check_name] = error_status
            logger.error("Health check failed", check=check_name, error=str(e))
            return error_status

    async def run_all_health_checks(self) -> Dict[str, HealthStatus]:
        """Run all registered health checks"""
        results = {}
        
        for check_name in self.health_checks:
            results[check_name] = await self.run_health_check(check_name)
        
        return results

    def get_overall_health(self) -> HealthStatus:
        """Get overall system health status"""
        if not self.health_status:
            return HealthStatus("unknown", {"message": "No health checks have been run"})
        
        # Determine overall status based on component statuses
        statuses = [status.status for status in self.health_status.values()]
        
        if "critical" in statuses:
            overall_status = "critical"
        elif "warning" in statuses:
            overall_status = "warning"
        elif "unknown" in statuses:
            overall_status = "unknown"
        else:
            overall_status = "healthy"
        
        # Count status types
        status_counts = {}
        for status in statuses:
            status_counts[status] = status_counts.get(status, 0) + 1
        
        details = {
            "total_checks": len(self.health_status),
            "status_breakdown": status_counts,
            "last_check": max((s.timestamp for s in self.health_status.values()), default=datetime.now()),
            "failing_checks": [name for name, status in self.health_status.items() if status.status in ["warning", "critical"]]
        }
        
        return HealthStatus(overall_status, details)
    
    def get_component_health(self, component: str) -> Optional[HealthStatus]:
        """Get health status for specific component"""
        return self.health_status.get(component)

    def register_alert_callback(self, callback: Callable):
        """Register callback for health alerts"""
        self.alert_callbacks.append(callback)
        logger.info("Alert callback registered")
    
    async def start_monitoring(self):
        """Start continuous health monitoring"""
        if self.monitoring_task:
            logger.warning("Health monitoring already running")
            return
        
        self.monitoring_task = asyncio.create_task(self._monitoring_loop())
        logger.info("Health monitoring started")    
    
    async def stop_monitoring(self):
        """Stop health monitoring"""
        if self.monitoring_task:
            self.monitoring_task.cancel()
            with contextlib.suppress(asyncio.CancelledError):
                await self.monitoring_task
            self.monitoring_task = None
        logger.info("Health monitoring stopped")

    def _trigger_alert(self, component: str, status: HealthStatus):
        """Trigger alert for health issue"""
        alert_data = {
            "component": component,
            "status": status.status,
            "details": status.details,
            "timestamp": status.timestamp,
            "message": status.details.get("message", f"Component {component} status: {status.status}")
        }
        
        logger.warning("Health alert triggered", **alert_data)
        
        # Call registered alert callbacks
        for callback in self.alert_callbacks:
            try:
                if asyncio.iscoroutinefunction(callback):
                    asyncio.create_task(callback(alert_data))
                else:
                    callback(alert_data)
            except Exception as e:
                logger.error("Alert callback failed", error=str(e))

    async def _monitoring_loop(self):
        """Main health monitoring loop"""
        while True:
            try:
                # Run all health checks
                await self.run_all_health_checks()
                
                # Wait for next check interval
                await asyncio.sleep(self.check_interval)
                
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error("Error in health monitoring loop", error=str(e))
                await asyncio.sleep(10)  # Brief pause before retry

    def _check_cpu_usage(self) -> HealthStatus:
        """Check CPU usage health"""
        try:
            cpu_usage = psutil.cpu_percent(interval=1)
            threshold = self.alert_thresholds["cpu_usage"]
            
            if cpu_usage > threshold:
                status = "critical" if cpu_usage > threshold * 1.2 else "warning"
                details = {
                    "cpu_usage": cpu_usage,
                    "threshold": threshold,
                    "message": f"CPU usage {cpu_usage:.1f}% exceeds threshold {threshold}%"
                }
            else:
                status = "healthy"
                details = {
                    "cpu_usage": cpu_usage,
                    "threshold": threshold,
                    "message": "CPU usage within normal range"
                }
                
            return HealthStatus(status, details)
            
        except Exception as e:
            return HealthStatus("unknown", {"error": str(e), "check": "cpu_usage"})
    
    def _check_memory_usage(self) -> HealthStatus:
        """Check memory usage health"""
        try:
            memory = psutil.virtual_memory()
            memory_usage = memory.percent
            threshold = self.alert_thresholds["memory_usage"]
            
            if memory_usage > threshold:
                status = "critical" if memory_usage > threshold * 1.1 else "warning"
                details = {
                    "memory_usage": memory_usage,
                    "memory_total": memory.total,
                    "memory_used": memory.used,
                    "memory_available": memory.available,
                    "threshold": threshold,
                    "message": f"Memory usage {memory_usage:.1f}% exceeds threshold {threshold}%"
                }
            else:
                status = "healthy"
                details = {
                    "memory_usage": memory_usage,
                    "memory_total": memory.total,
                    "memory_available": memory.available,
                    "threshold": threshold,
                    "message": "Memory usage within normal range"
                }
                
            return HealthStatus(status, details)
            
        except Exception as e:
            return HealthStatus("unknown", {"error": str(e), "check": "memory_usage"})
    
    def _check_disk_usage(self) -> HealthStatus:
        """Check disk usage health"""
        try:
            # Check root filesystem
            disk = psutil.disk_usage('/')
            disk_usage = (disk.used / disk.total) * 100
            threshold = self.alert_thresholds["disk_usage"]
            
            if disk_usage > threshold:
                status = "critical" if disk_usage > threshold * 1.05 else "warning"
                details = {
                    "disk_usage": disk_usage,
                    "disk_total": disk.total,
                    "disk_used": disk.used,
                    "disk_free": disk.free,
                    "threshold": threshold,
                    "message": f"Disk usage {disk_usage:.1f}% exceeds threshold {threshold}%"
                }
            else:
                status = "healthy"
                details = {
                    "disk_usage": disk_usage,
                    "disk_total": disk.total,
                    "disk_free": disk.free,
                    "threshold": threshold,
                    "message": "Disk usage within normal range"
                }
                
            return HealthStatus(status, details)
            
        except Exception as e:
            return HealthStatus("unknown", {"error": str(e), "check": "disk_usage"})
    
    def _check_worker_health(self) -> HealthStatus:
        """Check worker process health"""
        try:
            if not self.node_controller:
                return HealthStatus("unknown", {"message": "No node controller available"})
            
            # Get worker manager if available
            if hasattr(self.node_controller, 'worker_manager'):
                worker_manager = self.node_controller.worker_manager
                
                # Check if workers are responding
                if hasattr(worker_manager, 'get_worker_status'):
                    worker_statuses = worker_manager.get_worker_status()
                    
                    total_workers = len(worker_statuses)
                    healthy_workers = sum(1 for status in worker_statuses.values() 
                                        if status.get('state') == 'ready')
                    error_workers = sum(1 for status in worker_statuses.values() 
                                      if status.get('state') == 'error')
                    
                    if total_workers == 0:
                        status = "warning"
                        message = "No workers currently active"
                    elif error_workers > total_workers * 0.1:  # More than 10% errors
                        status = "critical"
                        message = f"{error_workers}/{total_workers} workers in error state"
                    elif healthy_workers < total_workers * 0.8:  # Less than 80% healthy
                        status = "warning"
                        message = f"Only {healthy_workers}/{total_workers} workers healthy"
                    else:
                        status = "healthy"
                        message = f"{healthy_workers}/{total_workers} workers healthy"
                    
                    details = {
                        "total_workers": total_workers,
                        "healthy_workers": healthy_workers,
                        "error_workers": error_workers,
                        "message": message
                    }
                    
                    return HealthStatus(status, details)
            
            return HealthStatus("unknown", {"message": "Worker manager not available"})
            
        except Exception as e:
            return HealthStatus("unknown", {"error": str(e), "check": "worker_health"})
