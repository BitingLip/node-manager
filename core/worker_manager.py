"""
Worker Manager
Manages worker processes, their lifecycle, health monitoring, and capabilities
Handles spawning, monitoring, and coordination of specialized worker processes
"""

import subprocess
import threading
import logging
from typing import Dict, List, Optional, Any, Set
from datetime import datetime
from enum import Enum
import structlog

logger = structlog.get_logger(__name__)


class WorkerStatus(Enum):
    """Worker process status"""
    STARTING = "starting"
    READY = "ready"
    BUSY = "busy"
    ERROR = "error"
    STOPPING = "stopping"
    STOPPED = "stopped"


class WorkerType(Enum):
    """Types of worker processes"""
    LLM = "llm"
    STABLE_DIFFUSION = "stable_diffusion"
    TTS = "text_to_speech"
    IMAGE_TO_TEXT = "image_to_text"
    GENERIC = "generic"


class WorkerInfo:
    """Information about a worker process"""
    
    def __init__(self, worker_id: str, worker_type: WorkerType, process_id: Optional[int] = None):
        self.worker_id = worker_id
        self.worker_type = worker_type
        self.process_id = process_id
        self.status = WorkerStatus.STARTING
        self.capabilities = set()
        self.resource_allocation = {}
        self.current_task = None
        self.last_heartbeat = datetime.now()
        self.error_count = 0


class WorkerManager:
    """
    Manages all worker processes on this node
    Handles spawning, monitoring, health checks, and process coordination
    """
    
    def __init__(self, node_id: str, config: Dict[str, Any], resource_manager=None, database=None):
        """Initialize worker manager"""
        self.node_id = node_id
        self.config = config
        self.resource_manager = resource_manager
        self.database = database
        self.workers = {}  # worker_id -> WorkerInfo
        self.worker_processes = {}  # worker_id -> subprocess.Popen
        
        # Configuration
        self.max_workers_per_type = {
            WorkerType.LLM: 2,
            WorkerType.STABLE_DIFFUSION: 1,
            WorkerType.TTS: 1,
            WorkerType.IMAGE_TO_TEXT: 1        }        
        logger.info("WorkerManager initialized")
    
    def spawn_worker(self, worker_type: WorkerType, config: Dict[str, Any]) -> Optional[str]:
        """Spawn a new worker process"""
        import uuid
        import os
        
        try:
            # 1. Generate worker ID
            worker_id = f"worker-{worker_type.value}-{uuid.uuid4().hex[:8]}"
            
            # 2. Check resource availability if resource manager is available
            if self.resource_manager:
                required_resources = config.get('resources', {
                    'cpu_cores': 1,
                    'memory_mb': 512
                })
                
                if not self.resource_manager.can_allocate(required_resources):
                    logger.warning(f"Cannot spawn worker {worker_id}: insufficient resources")
                    return None
                
                # 3. Allocate resources
                if not self.resource_manager.allocate_resources(worker_id, required_resources):
                    logger.error(f"Failed to allocate resources for worker {worker_id}")
                    return None
            
            # 4. Create worker info
            worker_info = WorkerInfo(worker_id, worker_type)
            worker_info.resource_allocation = config.get('resources', {})
            
            # 5. Start worker process (placeholder command)
            # In a real implementation, this would start actual worker scripts
            worker_script = config.get('script', 'python -c "import time; time.sleep(1000)"')
            env = os.environ.copy()
            env['WORKER_ID'] = worker_id
            env['WORKER_TYPE'] = worker_type.value
            
            process = subprocess.Popen(
                worker_script,
                shell=True,
                env=env,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE
            )
            
            # 6. Register worker
            worker_info.process_id = process.pid
            worker_info.status = WorkerStatus.READY  # Set to ready after successful spawn
            self.workers[worker_id] = worker_info
            self.worker_processes[worker_id] = process
            
            # Register in database if available
            if self.database:
                self.database.register_worker({
                    'worker_id': worker_id,
                    'node_id': self.node_id,
                    'worker_type': worker_type.value,
                    'status': worker_info.status.value,
                    'capabilities': {},
                    'resource_allocation': worker_info.resource_allocation
                })
            
            logger.info(f"Spawned worker {worker_id} (PID: {process.pid})")
            return worker_id
            
        except Exception as e:
            logger.error(f"Failed to spawn worker: {e}")
            return None
    
    def stop_worker(self, worker_id: str, graceful: bool = True) -> bool:
        """Stop a worker process"""
        try:
            if worker_id not in self.workers:
                logger.warning(f"Worker {worker_id} not found")
                return False
            
            worker_info = self.workers[worker_id]
            process = self.worker_processes.get(worker_id)
            
            if process is None:
                logger.warning(f"No process found for worker {worker_id}")
                return False
            
            # 1. Send shutdown signal
            worker_info.status = WorkerStatus.STOPPING
            
            if graceful:
                # 2. Wait for graceful shutdown
                try:
                    process.terminate()  # SIGTERM
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    # 3. Force kill if needed
                    logger.warning(f"Worker {worker_id} did not shutdown gracefully, forcing termination")
                    process.kill()
                    process.wait()
            else:
                process.kill()
                process.wait()
            
            # 4. Release resources
            if self.resource_manager:
                self.resource_manager.release_resources(worker_id)
            
            # 5. Update status and cleanup
            worker_info.status = WorkerStatus.STOPPED
            del self.worker_processes[worker_id]
            
            # Update database
            if self.database:
                self.database.update_worker_status(worker_id, 'stopped')
            
            logger.info(f"Stopped worker {worker_id}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to stop worker {worker_id}: {e}")
            return False
    
    def restart_worker(self, worker_id: str) -> bool:
        """Restart a worker process"""
        try:
            if worker_id not in self.workers:
                logger.warning(f"Worker {worker_id} not found")
                return False
            
            worker_info = self.workers[worker_id]
            old_config = {
                'resources': worker_info.resource_allocation,
                'script': 'python -c "import time; time.sleep(1000)"'  # Placeholder
            }
            
            # 1. Stop existing worker
            if not self.stop_worker(worker_id, graceful=True):
                logger.error(f"Failed to stop worker {worker_id} for restart")
                return False
            
            # 2. Spawn new worker with same config
            if new_worker_id := self.spawn_worker(worker_info.worker_type, old_config):
                logger.info(f"Restarted worker {worker_id} as {new_worker_id}")
                return True
            else:
                logger.error(f"Failed to restart worker {worker_id}")
                return False
                
        except Exception as e:
            logger.error(f"Failed to restart worker {worker_id}: {e}")
            return False
    
    def get_available_workers(self, worker_type: Optional[WorkerType] = None) -> List[WorkerInfo]:
        """Get list of available workers"""
        available_workers = []
        
        for worker_info in self.workers.values():
            # Check if worker is ready and not busy
            if worker_info.status == WorkerStatus.READY and worker_info.current_task is None:
                # Filter by type if specified
                if worker_type is None or worker_info.worker_type == worker_type:
                    available_workers.append(worker_info)
        
        return available_workers
    
    def assign_task_to_worker(self, worker_id: str, task_data: Dict[str, Any]) -> bool:
        """Assign a task to a specific worker"""
        try:
            if worker_id not in self.workers:
                logger.warning(f"Worker {worker_id} not found")
                return False
            
            worker_info = self.workers[worker_id]
            
            # 1. Validate worker availability
            if worker_info.status != WorkerStatus.READY:
                logger.warning(f"Worker {worker_id} is not ready (status: {worker_info.status})")
                return False
            
            # 2. Update worker status and assign task
            worker_info.status = WorkerStatus.BUSY
            worker_info.current_task = task_data.get('task_id')
            
            # Update database
            if self.database:
                self.database.update_worker_status(worker_id, 'busy', {
                    'current_task_id': task_data.get('task_id')
                })
            
            # 3. Send task to worker (placeholder - would use IPC in real implementation)
            logger.info(f"Assigned task {task_data.get('task_id')} to worker {worker_id}")
            
            return True
            
        except Exception as e:
            logger.error(f"Failed to assign task to worker {worker_id}: {e}")
            return False
    
    def complete_task(self, worker_id: str, task_id: str, result: Any = None):
        """Mark task as completed and free worker"""
        if worker_id in self.workers:
            worker_info = self.workers[worker_id]
            if worker_info.current_task == task_id:
                worker_info.status = WorkerStatus.READY
                worker_info.current_task = None
                
                # Update database
                if self.database:
                    self.database.update_worker_status(worker_id, 'ready', {
                        'current_task_id': None
                    })
                
                logger.info(f"Worker {worker_id} completed task {task_id}")
    
    def monitor_worker_health(self):
        """Monitor health of all worker processes"""
        try:
            current_time = datetime.now()
            
            for worker_id, worker_info in list(self.workers.items()):
                process = self.worker_processes.get(worker_id)
                
                if process is None:
                    continue
                
                # 1. Check process status
                poll_result = process.poll()
                if poll_result is not None:
                    # Process has terminated
                    logger.warning(f"Worker {worker_id} process terminated unexpectedly (exit code: {poll_result})")
                    worker_info.status = WorkerStatus.ERROR
                    worker_info.error_count += 1
                    
                    # Update database
                    if self.database:
                        self.database.update_worker_status(worker_id, 'error')
                    
                    # 4. Restart failed workers (with limit)
                    if worker_info.error_count < 3:
                        logger.info(f"Attempting to restart worker {worker_id}")
                        self.restart_worker(worker_id)
                    else:
                        logger.error(f"Worker {worker_id} has failed too many times, not restarting")
                
                # 2. Verify heartbeats (simplified - would use actual heartbeat mechanism)
                # For now, just update last_heartbeat if process is alive
                if poll_result is None:
                    worker_info.last_heartbeat = current_time
            
        except Exception as e:
            logger.error(f"Error during worker health monitoring: {e}")
    
    def get_worker_capabilities(self) -> Dict[str, Set[str]]:
        """Get capabilities of all workers"""
        capabilities = {}
        
        for worker_id, worker_info in self.workers.items():
            # Map worker types to capabilities
            type_capabilities = {
                WorkerType.LLM: {'text-generation', 'chat', 'completion'},
                WorkerType.STABLE_DIFFUSION: {'image-generation', 'txt2img', 'img2img'},
                WorkerType.TTS: {'text-to-speech', 'voice-generation'},
                WorkerType.IMAGE_TO_TEXT: {'image-captioning', 'ocr', 'visual-qa'},
                WorkerType.GENERIC: {'general-purpose'}
            }
            
            worker_capabilities = type_capabilities.get(worker_info.worker_type, set())
            worker_capabilities.update(worker_info.capabilities)
            
            capabilities[worker_id] = worker_capabilities
        
        return capabilities
    
    def scale_workers(self, target_counts: Dict[WorkerType, int]):
        """Scale worker processes to target counts"""
        try:
            current_counts = {}
            
            # 1. Count current workers by type
            for worker_info in self.workers.values():
                if worker_info.status not in [WorkerStatus.STOPPED, WorkerStatus.ERROR]:
                    current_counts[worker_info.worker_type] = current_counts.get(worker_info.worker_type, 0) + 1
            
            for worker_type, target_count in target_counts.items():
                current_count = current_counts.get(worker_type, 0)
                
                if current_count < target_count:
                    # 2. Spawn additional workers
                    workers_to_spawn = target_count - current_count
                    max_allowed = self.max_workers_per_type.get(worker_type, 1)
                    workers_to_spawn = min(workers_to_spawn, max_allowed - current_count)
                    
                    for _ in range(workers_to_spawn):
                        config = {'resources': {'cpu_cores': 1, 'memory_mb': 512}}
                        if worker_id := self.spawn_worker(worker_type, config):
                            logger.info(f"Scaled up: spawned {worker_type.value} worker {worker_id}")
                
                elif current_count > target_count:
                    # 3. Stop excess workers
                    workers_to_stop = current_count - target_count
                    workers_of_type = [
                        (wid, winfo) for wid, winfo in self.workers.items()
                        if winfo.worker_type == worker_type and winfo.status not in [WorkerStatus.STOPPED, WorkerStatus.ERROR]
                    ]
                    
                    # Stop oldest workers first
                    workers_of_type.sort(key=lambda x: x[1].last_heartbeat)
                    
                    for i in range(min(workers_to_stop, len(workers_of_type))):
                        worker_id = workers_of_type[i][0]
                        if self.stop_worker(worker_id):
                            logger.info(f"Scaled down: stopped {worker_type.value} worker {worker_id}")
            
        except Exception as e:
            logger.error(f"Failed to scale workers: {e}")
    
    def get_all_workers(self) -> List[WorkerInfo]:
        """Get all workers"""
        return list(self.workers.values())
    
    def get_worker_stats(self) -> Dict[str, Any]:
        """Get worker statistics"""
        stats = {
            'total_workers': len(self.workers),
            'by_status': {},
            'by_type': {},
            'active_tasks': 0
        }
        
        for worker_info in self.workers.values():
            # Count by status
            status = worker_info.status.value
            stats['by_status'][status] = stats['by_status'].get(status, 0) + 1
            
            # Count by type
            worker_type = worker_info.worker_type.value
            stats['by_type'][worker_type] = stats['by_type'].get(worker_type, 0) + 1
            
            # Count active tasks
            if worker_info.current_task:
                stats['active_tasks'] += 1
        
        return stats
    
    def shutdown_workers(self, timeout: int = 30):
        """Shutdown all workers gracefully"""
        logger.info("Shutting down all workers")
        
        for worker_id in list(self.workers.keys()):
            try:
                self.stop_worker(worker_id, graceful=True)
            except Exception as e:
                logger.error(f"Error stopping worker {worker_id}: {e}")
        
        logger.info("All workers shutdown complete")
    
    def health_check(self) -> Dict[str, Any]:
        """Perform health check on all workers"""
        self.monitor_worker_health()

        healthy_workers = sum(
            1 for worker_info in self.workers.values()
            if worker_info.status in [WorkerStatus.READY, WorkerStatus.BUSY]
        )
        total_workers = len(self.workers)

        return {
            'healthy_workers': healthy_workers,
            'total_workers': total_workers,
            'health_percentage': (healthy_workers / total_workers * 100) if total_workers > 0 else 100
        }
