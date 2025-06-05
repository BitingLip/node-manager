"""
Worker and Task Emulator
Main emulation engine for testing BitingLip Node Manager
"""

import asyncio
import random
import json
import time
from typing import Dict, List, Optional, Any, Callable
from datetime import datetime, timedelta
from pathlib import Path
import structlog
import uuid

# Import core types
import sys
sys.path.append(str(Path(__file__).parent.parent))

from core.task_dispatcher import TaskInfo, TaskStatus, TaskPriority
from core.worker_manager import WorkerStatus, WorkerType
from .mock_worker import MockWorker, MockWorkerType

logger = structlog.get_logger(__name__)


class WorkerEmulator:
    """
    Emulates a fleet of workers for testing
    """
    
    def __init__(self, node_manager=None):
        self.node_manager = node_manager
        self.workers: Dict[str, MockWorker] = {}
        self.worker_configs = {}
        self.is_running = False
        self.monitor_task: Optional[asyncio.Task] = None
        
    def add_worker_type(self, worker_type: MockWorkerType, count: int = 1, 
                       config: Optional[Dict[str, Any]] = None) -> List[str]:
        """Add workers of a specific type"""
        added_workers = []
        
        for i in range(count):
            worker_id = f"{worker_type.value}_{uuid.uuid4().hex[:8]}"
            worker = MockWorker(worker_id, worker_type, config)
            self.workers[worker_id] = worker
            self.worker_configs[worker_id] = config or {}
            added_workers.append(worker_id)
            
            logger.info("Added mock worker", 
                       worker_id=worker_id, 
                       type=worker_type.value,
                       total_workers=len(self.workers))
        
        return added_workers
    
    def create_worker_fleet(self, fleet_config: Optional[Dict[str, int]] = None) -> Dict[str, List[str]]:
        """Create a balanced fleet of workers"""
        if fleet_config is None:
            fleet_config = {
                MockWorkerType.LLM_SMALL.value: 2,
                MockWorkerType.LLM_LARGE.value: 1,
                MockWorkerType.STABLE_DIFFUSION.value: 1,
                MockWorkerType.TTS_FAST.value: 2,
                MockWorkerType.IMAGE_TO_TEXT.value: 1,
                MockWorkerType.GENERIC.value: 3
            }
        
        fleet = {}
        for worker_type_str, count in fleet_config.items():
            # Convert string back to enum
            worker_type = MockWorkerType(worker_type_str)
            fleet[worker_type.value] = self.add_worker_type(worker_type, count)
        
        logger.info("Created worker fleet", 
                   total_workers=len(self.workers),
                   fleet_composition=fleet)
        return fleet
    
    async def start_all_workers(self) -> Dict[str, bool]:
        """Start all configured workers"""
        logger.info("Starting all workers", count=len(self.workers))
        
        start_results = {}
        tasks = []
        
        # Start workers concurrently
        for worker_id, worker in self.workers.items():
            task = asyncio.create_task(worker.start())
            tasks.append((worker_id, task))
        
        # Wait for all to complete
        for worker_id, task in tasks:
            try:
                success = await task
                start_results[worker_id] = success
                if success:
                    logger.info("Worker started successfully", worker_id=worker_id)
                else:
                    logger.error("Worker failed to start", worker_id=worker_id)
            except Exception as e:
                start_results[worker_id] = False
                logger.error("Worker startup error", worker_id=worker_id, error=str(e))
        
        successful = sum(1 for success in start_results.values() if success)
        logger.info("Worker startup complete", 
                   successful=successful, 
                   total=len(self.workers))
        
        self.is_running = True
        return start_results
    
    async def stop_all_workers(self) -> Dict[str, bool]:
        """Stop all workers"""
        logger.info("Stopping all workers", count=len(self.workers))
        
        self.is_running = False
        if self.monitor_task:
            self.monitor_task.cancel()
        
        stop_results = {}
        tasks = []
        
        for worker_id, worker in self.workers.items():
            task = asyncio.create_task(worker.stop())
            tasks.append((worker_id, task))
        
        for worker_id, task in tasks:
            try:
                success = await task
                stop_results[worker_id] = success
            except Exception as e:
                stop_results[worker_id] = False
                logger.error("Worker stop error", worker_id=worker_id, error=str(e))
        
        logger.info("All workers stopped")
        return stop_results
    
    def get_worker_by_capability(self, task_type: str) -> Optional[MockWorker]:
        """Find a suitable worker for a task type"""
        available_workers = [
            worker for worker in self.workers.values()
            if worker.status == WorkerStatus.READY and 
            task_type in worker.capabilities.get('tasks', [])
        ]
        
        if not available_workers:
            # Fallback to generic workers
            available_workers = [
                worker for worker in self.workers.values()
                if worker.status == WorkerStatus.READY and 
                worker.worker_type == MockWorkerType.GENERIC
            ]
        
        return random.choice(available_workers) if available_workers else None
    
    async def execute_task(self, task: TaskInfo) -> Dict[str, Any]:
        """Execute a task using an appropriate worker"""
        worker = self.get_worker_by_capability(task.task_type)
        if not worker:
            return {
                'success': False,
                'error': f'No available worker for task type: {task.task_type}',
                'task_id': task.task_id
            }
        
        try:
            result = await worker.execute_task(task)
            result['task_id'] = task.task_id
            return result
        except Exception as e:
            logger.error("Task execution failed", 
                        task_id=task.task_id, 
                        worker_id=worker.worker_id,
                        error=str(e))
            return {
                'success': False,
                'error': str(e),
                'task_id': task.task_id,
                'worker_id': worker.worker_id
            }
    
    def get_fleet_status(self) -> Dict[str, Any]:
        """Get status of all workers"""
        status_by_type = {}
        total_status = {
            'total_workers': len(self.workers),
            'ready': 0,
            'busy': 0,
            'error': 0,
            'stopped': 0
        }
        
        for worker in self.workers.values():
            worker_type = worker.worker_type.value
            if worker_type not in status_by_type:
                status_by_type[worker_type] = {
                    'count': 0,
                    'ready': 0,
                    'busy': 0,
                    'error': 0,
                    'workers': []
                }
            
            status_by_type[worker_type]['count'] += 1
            status_by_type[worker_type]['workers'].append(worker.get_status())
            
            # Update counts
            status = worker.status
            if status == WorkerStatus.READY:
                status_by_type[worker_type]['ready'] += 1
                total_status['ready'] += 1
            elif status == WorkerStatus.BUSY:
                status_by_type[worker_type]['busy'] += 1
                total_status['busy'] += 1
            elif status == WorkerStatus.ERROR:
                status_by_type[worker_type]['error'] += 1
                total_status['error'] += 1
            else:
                total_status['stopped'] += 1
        
        return {
            'fleet_summary': total_status,
            'by_type': status_by_type,
            'is_running': self.is_running
        }
    
    def get_fleet_metrics(self) -> Dict[str, Any]:
        """Get performance metrics for the fleet"""
        metrics = {
            'total_tasks': 0,
            'failed_tasks': 0,
            'success_rate': 0.0,
            'by_worker_type': {}
        }
        
        for worker in self.workers.values():
            worker_metrics = worker.get_metrics()
            worker_type = worker.worker_type.value
            
            metrics['total_tasks'] += worker_metrics['total_tasks_completed']
            metrics['failed_tasks'] += worker_metrics['failed_tasks']
            
            if worker_type not in metrics['by_worker_type']:
                metrics['by_worker_type'][worker_type] = {
                    'total_tasks': 0,
                    'failed_tasks': 0,
                    'avg_success_rate': 0.0,
                    'worker_count': 0
                }
            
            type_metrics = metrics['by_worker_type'][worker_type]
            type_metrics['total_tasks'] += worker_metrics['total_tasks_completed']
            type_metrics['failed_tasks'] += worker_metrics['failed_tasks']
            type_metrics['avg_success_rate'] += worker_metrics['success_rate']
            type_metrics['worker_count'] += 1
        
        # Calculate averages
        if metrics['total_tasks'] > 0:
            metrics['success_rate'] = (metrics['total_tasks'] - metrics['failed_tasks']) / metrics['total_tasks']
        
        for type_metrics in metrics['by_worker_type'].values():
            if type_metrics['worker_count'] > 0:
                type_metrics['avg_success_rate'] /= type_metrics['worker_count']
        
        return metrics


class TaskEmulator:
    """
    Generates and manages test tasks
    """
    
    def __init__(self, worker_emulator: WorkerEmulator):
        self.worker_emulator = worker_emulator
        self.task_queue: List[TaskInfo] = []
        self.completed_tasks: List[TaskInfo] = []
        self.running_tasks: Dict[str, TaskInfo] = {}
        self.task_templates = self._create_task_templates()
        
    def _create_task_templates(self) -> Dict[str, Dict[str, Any]]:
        """Create templates for different task types"""
        return {
            'text_generation': {
                'prompt': 'Write a short story about {topic}',
                'max_tokens': 500,
                'temperature': 0.7
            },
            'text_to_image': {
                'prompt': 'A beautiful {subject} in {style} style',
                'resolution': '512x512',
                'steps': 20
            },
            'text_to_speech': {
                'text': 'Hello, this is a test of the text to speech system.',
                'voice': 'en-US-neural',
                'speed': 1.0
            },
            'image_to_text': {
                'image_url': 'mock://test-image.jpg',
                'task': 'caption'
            },
            'data_processing': {
                'data': [1, 2, 3, 4, 5],
                'operation': 'sum'
            }
        }
    
    def create_random_task(self, task_type: Optional[str] = None, 
                          priority: Optional[TaskPriority] = None) -> TaskInfo:
        """Create a randomized task"""
        if task_type is None:
            task_type = random.choice(list(self.task_templates.keys()))
        
        if priority is None:
            priority = random.choice(list(TaskPriority))
        
        task_id = f"task_{uuid.uuid4().hex[:8]}"
        
        # Get template and randomize
        template = self.task_templates[task_type].copy()
        
        if task_type == 'text_generation':
            topics = ['space exploration', 'ancient civilizations', 'future cities', 'deep ocean']
            template['prompt'] = template['prompt'].format(topic=random.choice(topics))
        elif task_type == 'text_to_image':
            subjects = ['sunset', 'mountain', 'cat', 'robot', 'castle']
            styles = ['photorealistic', 'anime', 'oil painting', 'cyberpunk']
            template['prompt'] = template['prompt'].format(
                subject=random.choice(subjects),
                style=random.choice(styles)
            )
        
        task = TaskInfo(task_id, task_type, template, priority)
        return task
    
    def create_task_batch(self, count: int, task_types: Optional[List[str]] = None) -> List[TaskInfo]:
        """Create a batch of random tasks"""
        tasks = []
        for _ in range(count):
            task_type = random.choice(task_types) if task_types else None
            task = self.create_random_task(task_type)
            tasks.append(task)
        return tasks
    
    async def execute_task_batch(self, tasks: List[TaskInfo]) -> List[Dict[str, Any]]:
        """Execute a batch of tasks"""
        logger.info("Executing task batch", count=len(tasks))
        
        # Execute tasks concurrently
        task_coroutines = []
        for task in tasks:
            self.running_tasks[task.task_id] = task
            task.status = TaskStatus.RUNNING
            task.started_at = datetime.now()
            
            coro = self.worker_emulator.execute_task(task)
            task_coroutines.append(coro)
        
        # Wait for all tasks to complete
        results = await asyncio.gather(*task_coroutines, return_exceptions=True)
        
        # Process results
        processed_results = []
        for i, result in enumerate(results):
            task = tasks[i]
            
            if isinstance(result, Exception):
                task.status = TaskStatus.FAILED
                task.error = str(result)
                result = {
                    'success': False,
                    'error': str(result),
                    'task_id': task.task_id
                }
            else:
                task.status = TaskStatus.COMPLETED if result.get('success') else TaskStatus.FAILED
                task.result = result
                if not result.get('success'):
                    task.error = result.get('error', 'Unknown error')
            
            task.completed_at = datetime.now()
            self.completed_tasks.append(task)
            del self.running_tasks[task.task_id]
            
            processed_results.append(result)
        
        logger.info("Task batch completed", 
                   completed=len(processed_results),
                   successful=sum(1 for r in processed_results if r.get('success')))
        
        return processed_results
    
    async def run_continuous_load(self, tasks_per_minute: int = 10, 
                                 duration_minutes: int = 5) -> Dict[str, Any]:
        """Run a continuous load test"""
        logger.info("Starting continuous load test", 
                   tasks_per_minute=tasks_per_minute,
                   duration_minutes=duration_minutes)
        
        start_time = datetime.now()
        end_time = start_time + timedelta(minutes=duration_minutes)
        interval = 60.0 / tasks_per_minute  # seconds between tasks
        
        all_results = []
        task_count = 0
        
        while datetime.now() < end_time:
            # Create and execute a task
            task = self.create_random_task()
            result = await self.worker_emulator.execute_task(task)
            all_results.append(result)
            task_count += 1
            
            # Wait for next interval
            await asyncio.sleep(interval)
        
        # Calculate statistics
        successful = sum(1 for r in all_results if r.get('success'))
        failed = task_count - successful
        
        stats = {
            'duration_minutes': duration_minutes,
            'total_tasks': task_count,
            'successful_tasks': successful,
            'failed_tasks': failed,
            'success_rate': successful / max(1, task_count),
            'tasks_per_minute_actual': task_count / duration_minutes,
            'average_execution_time': sum(r.get('execution_time', 0) for r in all_results) / max(1, task_count)
        }
        
        logger.info("Continuous load test completed", **stats)
        return stats
    
    def get_task_stats(self) -> Dict[str, Any]:
        """Get task execution statistics"""
        total_tasks = len(self.completed_tasks)
        if total_tasks == 0:
            return {'total_tasks': 0}
        
        successful = sum(1 for task in self.completed_tasks if task.status == TaskStatus.COMPLETED)
        failed = sum(1 for task in self.completed_tasks if task.status == TaskStatus.FAILED)
        
        # Calculate average execution time
        execution_times = []
        for task in self.completed_tasks:
            if task.started_at and task.completed_at:
                duration = (task.completed_at - task.started_at).total_seconds()
                execution_times.append(duration)
        
        avg_execution_time = sum(execution_times) / len(execution_times) if execution_times else 0
        
        # Stats by task type
        by_type = {}
        for task in self.completed_tasks:
            task_type = task.task_type
            if task_type not in by_type:
                by_type[task_type] = {'total': 0, 'successful': 0, 'failed': 0}
            
            by_type[task_type]['total'] += 1
            if task.status == TaskStatus.COMPLETED:
                by_type[task_type]['successful'] += 1
            else:
                by_type[task_type]['failed'] += 1
        
        return {
            'total_tasks': total_tasks,
            'successful_tasks': successful,
            'failed_tasks': failed,
            'success_rate': successful / total_tasks,
            'average_execution_time': avg_execution_time,
            'running_tasks': len(self.running_tasks),
            'by_task_type': by_type
        }
