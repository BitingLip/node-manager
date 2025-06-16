#!/usr/bin/env python3
"""
Status Reporter - Generates and manages status reports for the node manager
"""
import time
import threading
from typing import Dict, Any, Optional
from datetime import datetime, timedelta

try:
    from rich.console import Console
    from rich.table import Table
    from rich.panel import Panel
    from rich.live import Live
    RICH_AVAILABLE = True
except ImportError:
    RICH_AVAILABLE = False


class StatusReporter:
    """Generates comprehensive status reports for the node manager"""
    
    def __init__(self, logger, config: Dict):
        self.logger = logger
        self.config = config
        
        # Configuration
        self.update_interval = config.get("status_update_interval", 5)  # seconds
        self.enable_live_display = config.get("enable_live_display", False)
        self.enable_rich_output = config.get("enable_rich_output", RICH_AVAILABLE)
          # Rich console if available
        if RICH_AVAILABLE:
            from rich.console import Console
            self.console = Console()
        else:
            self.console = None
        
        # Runtime state
        self.live_display_active = False
        self.live_display_thread: Optional[threading.Thread] = None
        self.last_status_report: Optional[Dict[str, Any]] = None
        
        # References to managers (set by NodeManager)
        self.task_manager = None
        self.worker_manager = None
        self.system_monitor = None
        
        self.logger.info("StatusReporter initialized")
        
    def set_managers(self, task_manager, worker_manager, system_monitor):
        """Set references to other managers"""
        self.task_manager = task_manager
        self.worker_manager = worker_manager
        self.system_monitor = system_monitor
        
    def generate_status_report(self) -> Dict[str, Any]:
        """Generate comprehensive status report"""
        try:
            current_time = time.time()
            
            report = {
                'timestamp': current_time,
                'datetime': datetime.now().isoformat(),
                'uptime': self._calculate_uptime(),
                'overview': self._generate_overview(),
                'tasks': self._generate_task_status(),
                'workers': self._generate_worker_status(),
                'system': self._generate_system_status()
            }
            
            self.last_status_report = report
            return report
            
        except Exception as e:
            self.logger.error(f"Failed to generate status report: {e}")
            return {
                'timestamp': time.time(),
                'error': str(e)
            }
            
    def _calculate_uptime(self) -> Dict[str, Any]:
        """Calculate system uptime"""
        # This is a placeholder - would need to track start time
        return {
            'seconds': 0,
            'formatted': '0:00:00'
        }
        
    def _generate_overview(self) -> Dict[str, Any]:
        """Generate high-level overview"""
        overview = {
            'status': 'running',
            'version': '1.0.0',
            'node_type': 'processing_node'
        }
        
        if self.task_manager:
            task_stats = self.task_manager.get_statistics()
            overview.update({
                'total_tasks_processed': task_stats.get('total_processed', 0),
                'active_tasks': task_stats.get('active_tasks', 0),
                'queued_tasks': task_stats.get('queued_tasks', 0)
            })
            
        if self.worker_manager:
            worker_stats = self.worker_manager.get_worker_statistics()
            overview['total_workers'] = worker_stats.get('total_workers', 0)
            status_breakdown = worker_stats.get('status_breakdown', {})
            overview['active_workers'] = str(
                sum(status_breakdown.get(status, 0) for status in ['idle', 'busy'])
            )
            
        return overview
        
    def _generate_task_status(self) -> Dict[str, Any]:
        """Generate task status information"""
        if not self.task_manager:
            return {'error': 'TaskManager not available'}
            
        try:
            stats = self.task_manager.get_statistics()
            
            return {
                'queue_size': stats.get('queued_tasks', 0),
                'active_count': stats.get('active_tasks', 0),
                'completed_count': stats.get('completed_tasks', 0),
                'total_processed': stats.get('total_processed', 0),
                'active_tasks': self._get_active_task_details(),
                'recent_completions': self._get_recent_completions()
            }
            
        except Exception as e:
            self.logger.error(f"Failed to generate task status: {e}")
            return {'error': str(e)}
            
    def _generate_worker_status(self) -> Dict[str, Any]:
        """Generate worker status information"""
        if not self.worker_manager:
            return {'error': 'WorkerManager not available'}
            
        try:
            stats = self.worker_manager.get_worker_statistics()
            
            # Get detailed worker info
            workers = []
            for worker_id, worker in self.worker_manager.worker_status.items():
                workers.append({
                    'worker_id': worker.worker_id,
                    'device_id': worker.device_id,
                    'status': worker.status,
                    'current_task': worker.current_task,
                    'vram_usage_mb': worker.vram_usage_mb,
                    'last_activity': worker.last_activity,
                    'idle_time': time.time() - worker.last_activity if worker.status == 'idle' else 0
                })
            
            return {
                'total_workers': stats.get('total_workers', 0),
                'status_breakdown': stats.get('status_breakdown', {}),
                'device_usage': stats.get('device_usage', {}),
                'workers': workers,
                'process_health': self._check_process_health()
            }
            
        except Exception as e:
            self.logger.error(f"Failed to generate worker status: {e}")
            return {'error': str(e)}
            
    def _generate_system_status(self) -> Dict[str, Any]:
        """Generate system status information"""
        if not self.system_monitor:
            return {'error': 'SystemMonitor not available'}
            
        try:
            latest_metrics = self.system_monitor.get_latest_metrics()
            health_status = self.system_monitor.get_system_health_status()
            
            return {
                'health': health_status,
                'metrics': latest_metrics,
                'monitoring_active': self.system_monitor.monitoring_active
            }
            
        except Exception as e:
            self.logger.error(f"Failed to generate system status: {e}")
            return {'error': str(e)}
            
    def _get_active_task_details(self) -> list:
        """Get details of currently active tasks"""
        if not self.task_manager:
            return []
            
        try:
            active_tasks = []
            for task_id, task_info in self.task_manager.active_tasks.items():
                active_tasks.append({
                    'task_id': task_id,
                    'status': task_info.get('status'),
                    'worker_id': task_info.get('worker_id'),
                    'submitted_at': task_info.get('submitted_at'),
                    'started_at': task_info.get('started_at'),
                    'elapsed_time': time.time() - task_info.get('started_at', time.time()) 
                                  if task_info.get('started_at') else 0
                })
            return active_tasks[:10]  # Limit to recent 10
            
        except Exception as e:
            self.logger.error(f"Failed to get active task details: {e}")
            return []
            
    def _get_recent_completions(self) -> list:
        """Get recently completed tasks"""
        if not self.task_manager:
            return []
            
        try:
            completed = []
            for task_id, task_info in self.task_manager.completed_tasks.items():
                completed.append({
                    'task_id': task_id,
                    'status': task_info.get('status'),
                    'completed_at': task_info.get('completed_at'),
                    'processing_time': task_info.get('processing_time', 0),
                    'worker_id': task_info.get('worker_id')
                })
            
            # Sort by completion time and return recent 5
            completed.sort(key=lambda x: x.get('completed_at', 0), reverse=True)
            return completed[:5]
            
        except Exception as e:
            self.logger.error(f"Failed to get recent completions: {e}")
            return []
            
    def _check_process_health(self) -> Dict[str, Any]:
        """Check health of worker processes"""
        if not self.worker_manager:
            return {}
            
        try:
            alive_processes = 0
            total_processes = len(self.worker_manager.worker_processes)
            
            for device_id, process in self.worker_manager.worker_processes.items():
                if process.is_alive():
                    alive_processes += 1
                    
            return {
                'total_processes': total_processes,
                'alive_processes': alive_processes,
                'health_percentage': (alive_processes / total_processes * 100) if total_processes > 0 else 0
            }
        except Exception as e:
            self.logger.error(f"Failed to check process health: {e}")
            return {}
            
    def print_status_report(self, report: Optional[Dict[str, Any]] = None):
        """Print status report to console"""
        if report is None:
            report = self.generate_status_report()
            
        if self.enable_rich_output and self.console:
            self._print_rich_status(report)
        else:
            self._print_plain_status(report)
            
    def _print_rich_status(self, report: Dict[str, Any]):
        """Print status using Rich formatting"""
        if not self.console:
            return
            
        try:
            from rich.table import Table
            from rich.panel import Panel
            
            # Overview panel
            overview = report.get('overview', {})
            overview_text = f"""
Status: {overview.get('status', 'unknown')}
Workers: {overview.get('total_workers', 0)} total, {overview.get('active_workers', 0)} active
Tasks: {overview.get('queued_tasks', 0)} queued, {overview.get('active_tasks', 0)} active
Total Processed: {overview.get('total_tasks_processed', 0)}
"""
            
            self.console.print(Panel(overview_text, title="Node Manager Status", border_style="blue"))
            
            # Workers table
            worker_data = report.get('workers', {})
            if 'workers' in worker_data:
                worker_table = Table(title="Workers")
                worker_table.add_column("ID", style="cyan")
                worker_table.add_column("Device", style="magenta")
                worker_table.add_column("Status", style="green")
                worker_table.add_column("Current Task", style="yellow")
                worker_table.add_column("VRAM MB", style="red")
                
                for worker in worker_data['workers'][:10]:  # Show top 10
                    worker_table.add_row(
                        worker.get('worker_id', 'N/A'),
                        str(worker.get('device_id', 'N/A')),
                        worker.get('status', 'unknown'),
                        worker.get('current_task', 'none')[:20] + '...' if worker.get('current_task') and len(worker.get('current_task', '')) > 20 else worker.get('current_task', 'none'),
                        str(worker.get('vram_usage_mb', 0))
                    )
                
                self.console.print(worker_table)
            
            # System health
            system_data = report.get('system', {})
            if 'health' in system_data:
                health = system_data['health']
                health_text = f"Status: {health.get('status', 'unknown')}\nMessage: {health.get('message', 'N/A')}"
                
                style = "green" if health.get('status') == 'healthy' else "yellow" if health.get('status') == 'warning' else "red"
                self.console.print(Panel(health_text, title="System Health", border_style=style))
                
        except Exception as e:
            self.logger.error(f"Failed to print rich status: {e}")
            self._print_plain_status(report)
            
    def _print_plain_status(self, report: Dict[str, Any]):
        """Print status using plain text"""
        try:
            print("\n" + "="*50)
            print("NODE MANAGER STATUS")
            print("="*50)
            
            # Overview
            overview = report.get('overview', {})
            print(f"Status: {overview.get('status', 'unknown')}")
            print(f"Workers: {overview.get('total_workers', 0)} total, {overview.get('active_workers', 0)} active")
            print(f"Tasks: {overview.get('queued_tasks', 0)} queued, {overview.get('active_tasks', 0)} active")
            print(f"Total Processed: {overview.get('total_tasks_processed', 0)}")
            
            # System health
            system_data = report.get('system', {})
            if 'health' in system_data:
                health = system_data['health']
                print(f"\nSystem Health: {health.get('status', 'unknown')}")
                print(f"Message: {health.get('message', 'N/A')}")
            
            print("="*50 + "\n")
            
        except Exception as e:
            self.logger.error(f"Failed to print plain status: {e}")
            print(f"Status report error: {e}")
            
    def start_live_display(self):
        """Start live status display"""
        if self.live_display_active or not self.enable_live_display:
            return
            
        self.live_display_active = True
        self.live_display_thread = threading.Thread(target=self._live_display_loop, daemon=True)
        self.live_display_thread.start()
        
        self.logger.info("Live status display started")
        
    def stop_live_display(self):
        """Stop live status display"""
        self.live_display_active = False
        if self.live_display_thread:
            self.live_display_thread.join(timeout=5)
            
        self.logger.info("Live status display stopped")
        
    def _live_display_loop(self):
        """Live display loop"""
        while self.live_display_active:
            try:
                report = self.generate_status_report()
                
                # Clear screen and print status
                import os
                os.system('cls' if os.name == 'nt' else 'clear')
                self.print_status_report(report)
                
                time.sleep(self.update_interval)
                
            except Exception as e:
                self.logger.error(f"Live display loop error: {e}")
                time.sleep(5)
                
    def get_status_summary(self) -> str:
        """Get a brief status summary string"""
        try:
            report = self.generate_status_report()
            overview = report.get('overview', {})
            
            return (f"Status: {overview.get('status', 'unknown')} | "
                   f"Workers: {overview.get('active_workers', 0)}/{overview.get('total_workers', 0)} | "
                   f"Tasks: Q:{overview.get('queued_tasks', 0)} A:{overview.get('active_tasks', 0)} "
                   f"C:{overview.get('total_tasks_processed', 0)}")
                   
        except Exception as e:
            return f"Status error: {e}"
            
    def export_status_report(self, format: str = 'json') -> str:
        """Export status report in specified format"""
        try:
            report = self.generate_status_report()
            
            if format.lower() == 'json':
                import json
                return json.dumps(report, indent=2, default=str)
            elif format.lower() == 'text':
                # Convert to readable text format
                lines = []
                lines.append(f"Node Manager Status Report - {report.get('datetime', 'Unknown time')}")
                lines.append("="*60)
                
                overview = report.get('overview', {})
                lines.append("OVERVIEW:")
                for key, value in overview.items():
                    lines.append(f"  {key}: {value}")
                
                return "\n".join(lines)
            else:
                return f"Unsupported format: {format}"
                
        except Exception as e:
            self.logger.error(f"Failed to export status report: {e}")
            return f"Export error: {e}"
