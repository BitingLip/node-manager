#!/usr/bin/env python3
#! DO NOT USE EMOJIS IN TERMINAL OUTPUTS
"""
Node.py - Main orchestrator using core components
Simplified node manager that coordinates all specialized components
"""
import time
import signal
import threading
from typing import Optional
from pathlib import Path

# Import core components
from core.config import Config
from core.database import Database
from core.communication import Communication
from core.logger import Logger
from core.task_manager import TaskManager, TaskConfig
from core.worker_manager import WorkerManager
from core.system_monitor import SystemMonitor
from core.api_server import APIServer

# Try to import Rich for better terminal UI
try:
    from rich.console import Console
    from rich.panel import Panel
    RICH_AVAILABLE = True
except ImportError:
    RICH_AVAILABLE = False


class NodeManager:
    """Main node manager that coordinates all components"""
    
    def __init__(self, config_path: Optional[str] = None):
        # Load configuration
        self.config = Config(config_path)
        
        # Initialize logger first
        logging_config = self.config.get_logging_config()
        self.logger = Logger("NodeManager", 0, logging_config.get("level", "INFO"))
        
        # Initialize core components
        self.database = Database(self.config.get_database_config(), self.logger)
        self.communication = Communication(self.config.get_communication_config(), self.logger)
        
        # Initialize specialized managers
        self.task_manager = TaskManager(
            self.database, 
            self.logger, 
            self.config.get_processing_config()
        )
        
        self.worker_manager = WorkerManager(
            self.database, 
            self.logger, 
            self.communication, 
            self.config.get_node_manager_config()
        )
        
        self.system_monitor = SystemMonitor(
            self.logger, 
            self.database, 
            self.config.get_memory_config()
        )
        
        # Initialize API server
        self.api_server = APIServer(
            self.config.get_communication_config(), 
            self.logger, 
            self
        )
        
        # Runtime state
        self.running = False
        self.main_thread: Optional[threading.Thread] = None
        self.start_time = time.time()
        
        # Setup signal handlers
        self._setup_signal_handlers()
        
        # Rich console if available
        self.console = Console() if RICH_AVAILABLE else None
        
        self.logger.info("NodeManager initialized with modular architecture")
    
    def _setup_signal_handlers(self):
        """Setup signal handlers for graceful shutdown"""
        try:
            signal.signal(signal.SIGINT, self._signal_handler)
            signal.signal(signal.SIGTERM, self._signal_handler)
        except Exception as e:
            self.logger.warning(f"Could not setup signal handlers: {e}")
    
    def _signal_handler(self, signum, frame):
        """Handle shutdown signals"""
        self.logger.info(f"Received signal {signum}, initiating graceful shutdown...")
        self.stop()
    
    def start(self) -> bool:
        """Start the node manager and all components"""
        try:
            if self.running:
                self.logger.warning("NodeManager is already running")
                return True
            
            self.logger.info("Starting NodeManager...")
            self.running = True
            
            # Connect to database
            if not self.database.connect():
                self.logger.error("Failed to connect to database")
                return False
            
            # Start system monitoring
            self.system_monitor.start_monitoring()
            
            # Start worker manager and spawn workers
            if not self.worker_manager.start_all_workers():
                self.logger.warning("Some workers failed to start")
              # Start worker health monitoring
            self.worker_manager.start_health_monitoring()
            
            # Start database task monitoring
            if self.task_manager:
                self.task_manager.start_database_monitoring()
            
            # Start API server
            self.api_server.start_server()
            
            # Start main processing loop
            self.main_thread = threading.Thread(target=self._main_loop, daemon=True)
            self.main_thread.start()
            
            self._print_startup_banner()
            
            self.logger.info("NodeManager started successfully")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to start NodeManager: {e}")
            self.running = False
            return False
    
    def stop(self):
        """Stop the node manager and all components"""
        try:
            if not self.running:
                self.logger.info("NodeManager is not running")
                return
            
            self.logger.info("Stopping NodeManager...")
            self.running = False
            
            # Stop database task monitoring
            if self.task_manager:
                self.task_manager.stop_database_monitoring()
            
            # Stop API server
            self.api_server.stop_server()
            
            # Stop worker manager
            self.worker_manager.stop_all_workers()
            
            # Stop system monitoring
            self.system_monitor.stop_monitoring()
            
            # Disconnect from database
            self.database.disconnect()
              # Wait for main thread
            if self.main_thread and self.main_thread.is_alive():
                self.main_thread.join(timeout=5)
            
            self.logger.info("NodeManager stopped")
            
        except Exception as e:
            self.logger.error(f"Error during shutdown: {e}")
    
    def _main_loop(self):
        """Main processing loop"""
        self.logger.info("Starting main processing loop")
        
        while self.running:
            try:
                # Process worker messages from shared queues
                self.worker_manager.process_shared_queue_messages()
                
                # Process pending tasks
                self._process_pending_tasks()
                
                # Clean up old tasks periodically
                if int(time.time()) % 300 == 0:  # Every 5 minutes
                    self.task_manager.cleanup_old_tasks()
                    self.communication.cleanup_inactive_workers()
                
                # Small delay to prevent CPU spinning
                time.sleep(0.1)
                
            except Exception as e:
                self.logger.error(f"Main loop error: {e}")
                time.sleep(1)  # Longer delay on error
        
        self.logger.info("Main processing loop stopped")
    
    def _process_pending_tasks(self):
        """Process pending tasks by assigning them to available workers"""
        try:
            # Get next task from queue
            task = self.task_manager.get_next_task()
            if not task:
                return
            
            # Find available workers
            available_workers = self.worker_manager.get_available_workers()
            if not available_workers:
                # Put task back in queue
                self.task_manager.task_queue.put(task)
                return
            
            # Find optimal worker for this task
            optimal_worker = self.worker_manager.find_optimal_worker(task, available_workers)
            if not optimal_worker:
                # Put task back in queue
                self.task_manager.task_queue.put(task)
                return
            
            # Assign task to worker
            if self.task_manager.assign_task_to_worker(task, optimal_worker):
                # Send task to worker
                if self.worker_manager.send_task_to_worker(task, optimal_worker):
                    self.logger.info(f"Task {task.task_id} assigned to worker {optimal_worker}")
                else:
                    # Failed to send, revert assignment
                    self.worker_manager.update_worker_status(optimal_worker, "idle", None)
                    # Put task back in queue
                    self.task_manager.task_queue.put(task)
            else:
                # Put task back in queue
                self.task_manager.task_queue.put(task)
                
        except Exception as e:
            self.logger.error(f"Failed to process pending tasks: {e}")
    
    def submit_task(self, task_config: TaskConfig) -> str:
        """Public interface for task submission"""
        return self.task_manager.submit_task(task_config)
    
    def get_status(self) -> dict:
        """Get current system status"""
        try:
            task_stats = self.task_manager.get_statistics()
            worker_stats = self.worker_manager.get_worker_statistics()
            system_metrics = self.system_monitor.get_latest_metrics()
            
            return {
                "timestamp": time.time(),
                "uptime": time.time() - self.start_time,
                "running": self.running,
                "tasks": task_stats,
                "workers": worker_stats,
                "system": {
                    "metrics": system_metrics,
                    "health": self.system_monitor.get_system_health_status()
                },
                "communication": self.communication.get_communication_statistics(),
                "api_server": {
                    "running": self.api_server.is_running(),
                    "host": self.api_server.host,
                    "port": self.api_server.port
                }
            }
        except Exception as e:
            self.logger.error(f"Failed to get status: {e}")
            return {"error": str(e)}
    
    def _print_startup_banner(self):
        """Print startup banner with system information"""
        try:
            if self.console and RICH_AVAILABLE:
                from rich.panel import Panel
                
                status = self.get_status()
                
                banner_text = f"""
Node Manager Started Successfully!

API Server: http://{self.api_server.host}:{self.api_server.port}
Workers: {status['workers'].get('total_workers', 0)} initialized
Database: Connected
System Monitoring: Active

Ready to process tasks!
"""
                
                self.console.print(Panel(banner_text, title="🚀 BitingLip Node Manager", border_style="green"))
            else:
                print("\n" + "="*60)
                print("BITING LIP NODE MANAGER")
                print("="*60)
                print(f"API Server: http://{self.api_server.host}:{self.api_server.port}")
                print(f"Status: Running")
                print("Ready to process tasks!")
                print("="*60 + "\n")
                
        except Exception as e:
            self.logger.error(f"Failed to print banner: {e}")
    
    def print_status(self):
        """Print current status to console"""
        try:
            status = self.get_status()
            
            if self.console and RICH_AVAILABLE:
                from rich.table import Table
                from rich.panel import Panel
                
                # Create status table
                table = Table(title="Node Manager Status")
                table.add_column("Component", style="cyan")
                table.add_column("Status", style="green")
                table.add_column("Details", style="yellow")
                
                # Add rows
                table.add_row("Node Manager", "Running" if self.running else "Stopped", f"Uptime: {status.get('uptime', 0):.1f}s")
                table.add_row("API Server", "Running" if self.api_server.is_running() else "Stopped", f"{self.api_server.host}:{self.api_server.port}")
                table.add_row("Workers", str(status['workers'].get('total_workers', 0)), f"Active: {status['workers'].get('active_workers', 0)}")
                table.add_row("Tasks", f"Queue: {status['tasks'].get('queued_tasks', 0)}", f"Active: {status['tasks'].get('active_tasks', 0)}")
                
                self.console.print(table)
                
                # System health
                health = status['system'].get('health', {})
                health_panel = Panel(
                    f"Status: {health.get('status', 'unknown')}\nMessage: {health.get('message', 'N/A')}",
                    title="System Health",
                    border_style="green" if health.get('status') == 'healthy' else "yellow"
                )
                self.console.print(health_panel)
                
            else:
                print("\n" + "="*50)
                print("NODE MANAGER STATUS")
                print("="*50)
                print(f"Status: {'Running' if self.running else 'Stopped'}")
                print(f"Uptime: {status.get('uptime', 0):.1f} seconds")
                print(f"API Server: {self.api_server.host}:{self.api_server.port}")
                print(f"Workers: {status['workers'].get('total_workers', 0)} total")
                print(f"Tasks: {status['tasks'].get('queued_tasks', 0)} queued, {status['tasks'].get('active_tasks', 0)} active")
                print("="*50 + "\n")
                
        except Exception as e:
            self.logger.error(f"Failed to print status: {e}")
            print(f"Status error: {e}")
    
    def wait_for_shutdown(self):
        """Wait for shutdown signal"""
        try:
            while self.running:
                time.sleep(1)
        except KeyboardInterrupt:
            self.logger.info("Keyboard interrupt received")
            self.stop()


def main():
    """Main entry point"""
    import argparse
    
    parser = argparse.ArgumentParser(description="BitingLip Node Manager")
    parser.add_argument("--config", help="Path to configuration file")
    parser.add_argument("--status", action="store_true", help="Print status and exit")
    parser.add_argument("--no-workers", action="store_true", help="Don't start workers automatically")
    
    args = parser.parse_args()
    
    try:
        # Create and configure node manager
        node_manager = NodeManager(args.config)
        
        # Override worker auto-start if requested
        if args.no_workers:
            node_manager.config.set("node_manager", "auto_start_workers", False)
        
        if args.status:
            # Just print status and exit
            if node_manager.database.connect():
                node_manager.print_status()
                node_manager.database.disconnect()
            else:
                print("Cannot connect to database")
            return
        
        # Start the node manager
        if node_manager.start():
            # Wait for shutdown
            node_manager.wait_for_shutdown()
        else:
            print("Failed to start node manager")
            return 1
            
    except KeyboardInterrupt:
        print("\nShutdown requested by user")
    except Exception as e:
        print(f"Unexpected error: {e}")
        return 1
    finally:
        try:
            node_manager.stop()
        except:
            pass
    
    return 0


if __name__ == "__main__":
    exit(main())
