"""
Utility Worker Base Class
Base class for utility and system maintenance workers
Handles data processing, file operations, and system tasks
"""

import asyncio
import time
import json
import os
import shutil
import psutil
from typing import Dict, List, Optional, Any, Callable, AsyncGenerator
from pathlib import Path
from dataclasses import dataclass, asdict
from abc import ABC, abstractmethod
import structlog

logger = structlog.get_logger(__name__)


@dataclass
class UtilityTask:
    """Utility task definition"""
    task_id: str
    task_type: str
    input_data: Any
    output_path: Optional[str]
    priority: int
    submitted_at: float
    started_at: Optional[float]
    completed_at: Optional[float]
    result: Optional[Any]
    error: Optional[str]
    progress_percent: float
    metadata: Dict[str, Any]


@dataclass
class SystemMetrics:
    """System resource metrics"""
    cpu_percent: float
    memory_percent: float
    disk_usage_percent: float
    disk_free_gb: float
    network_io_mb: float
    gpu_memory_mb: Optional[float]
    gpu_utilization: Optional[float]
    temperature_c: Optional[float]
    timestamp: float


class BaseUtilityWorker(ABC):
    """
    Base class for utility workers
    Provides common functionality for system and data processing tasks
    """
    
    def __init__(self, worker_id: str, config: Dict[str, Any]):
        """Initialize utility worker"""
        self.worker_id = worker_id
        self.config = config
        
        # Task management
        self.active_tasks: Dict[str, UtilityTask] = {}
        self.completed_tasks: List[UtilityTask] = []
        self.task_queue: asyncio.Queue = asyncio.Queue()
        
        # Worker state
        self.is_running = False
        self.worker_task: Optional[asyncio.Task] = None
        
        # Resource limits
        self.max_concurrent_tasks = config.get("max_concurrent_tasks", 5)
        self.memory_limit_mb = config.get("memory_limit_mb", 2048)
        self.disk_space_limit_gb = config.get("disk_space_limit_gb", 50)
        
        # Monitoring
        self.system_metrics: List[SystemMetrics] = []
        self.callbacks: List[Callable] = []
        
        logger.info("BaseUtilityWorker initialized", 
                   worker_id=worker_id,
                   max_tasks=self.max_concurrent_tasks)
    
    @abstractmethod
    async def process_task(self, task: UtilityTask) -> Any:
        """Process a single utility task"""
        pass
    
    async def start(self):
        """Start the utility worker"""
        if self.is_running:
            logger.warning("Utility worker already running", worker_id=self.worker_id)
            return
        
        self.is_running = True
        self.worker_task = asyncio.create_task(self._worker_loop())
        
        # Start system monitoring
        asyncio.create_task(self._system_monitor_loop())
        
        logger.info("Utility worker started", worker_id=self.worker_id)
    
    async def stop(self):
        """Stop the utility worker"""
        if not self.is_running:
            return
        
        self.is_running = False
        
        if self.worker_task:
            self.worker_task.cancel()
            try:
                await self.worker_task
            except asyncio.CancelledError:
                pass
        
        logger.info("Utility worker stopped", worker_id=self.worker_id)
    
    async def submit_task(self, task_type: str, input_data: Any, 
                         output_path: Optional[str] = None,
                         priority: int = 0,
                         metadata: Optional[Dict[str, Any]] = None) -> str:
        """Submit a utility task"""
        task_id = f"{self.worker_id}_{task_type}_{int(time.time() * 1000)}"
        
        task = UtilityTask(
            task_id=task_id,
            task_type=task_type,
            input_data=input_data,
            output_path=output_path,
            priority=priority,
            submitted_at=time.time(),
            started_at=None,
            completed_at=None,
            result=None,
            error=None,
            progress_percent=0.0,
            metadata=metadata or {}
        )
        
        await self.task_queue.put(task)
        logger.debug("Utility task submitted", task_id=task_id, task_type=task_type)
        
        return task_id
    
    async def get_task_status(self, task_id: str) -> Optional[UtilityTask]:
        """Get status of a specific task"""
        # Check active tasks
        if task_id in self.active_tasks:
            return self.active_tasks[task_id]
        
        # Check completed tasks
        for task in self.completed_tasks:
            if task.task_id == task_id:
                return task
        
        return None
    
    async def wait_for_task(self, task_id: str, timeout: Optional[float] = None) -> Optional[UtilityTask]:
        """Wait for a task to complete"""
        start_time = time.time()
        
        while True:
            task = await self.get_task_status(task_id)
            if task and task.completed_at is not None:
                return task
            
            if timeout and (time.time() - start_time) > timeout:
                return None
            
            await asyncio.sleep(0.1)
    
    async def _worker_loop(self):
        """Main worker processing loop"""
        logger.debug("Utility worker loop started")
        
        while self.is_running:
            try:
                # Get task from queue
                task = await asyncio.wait_for(self.task_queue.get(), timeout=1.0)
                
                # Check resource limits
                if len(self.active_tasks) >= self.max_concurrent_tasks:
                    await self.task_queue.put(task)  # Put back in queue
                    await asyncio.sleep(0.1)
                    continue
                
                # Process task
                asyncio.create_task(self._process_task_wrapper(task))
                
            except asyncio.TimeoutError:
                continue
            except asyncio.CancelledError:
                logger.debug("Utility worker loop cancelled")
                break
            except Exception as e:
                logger.error("Worker loop error", error=str(e))
                await asyncio.sleep(1)
        
        logger.debug("Utility worker loop stopped")
    
    async def _process_task_wrapper(self, task: UtilityTask):
        """Wrapper for task processing with error handling"""
        task.started_at = time.time()
        self.active_tasks[task.task_id] = task
        
        try:
            logger.debug("Processing utility task", task_id=task.task_id, task_type=task.task_type)
            
            # Process the task
            result = await self.process_task(task)
            
            # Mark as completed
            task.completed_at = time.time()
            task.result = result
            task.progress_percent = 100.0
            
            logger.debug("Utility task completed", task_id=task.task_id)
            
        except Exception as e:
            task.completed_at = time.time()
            task.error = str(e)
            task.progress_percent = 0.0
            
            logger.error("Utility task failed", task_id=task.task_id, error=str(e))
        
        finally:
            # Move to completed and cleanup
            self.completed_tasks.append(task)
            if task.task_id in self.active_tasks:
                del self.active_tasks[task.task_id]
            
            # Notify callbacks
            for callback in self.callbacks:
                try:
                    await callback(task)
                except Exception as e:
                    logger.warning("Task callback failed", error=str(e))
    
    async def _system_monitor_loop(self):
        """Monitor system resources"""
        while self.is_running:
            try:
                metrics = await self._collect_system_metrics()
                self.system_metrics.append(metrics)
                
                # Keep only recent metrics (last 100)
                if len(self.system_metrics) > 100:
                    self.system_metrics = self.system_metrics[-100:]
                
                # Check resource limits
                await self._check_resource_limits(metrics)
                
            except Exception as e:
                logger.warning("System monitoring error", error=str(e))
            
            await asyncio.sleep(30)  # Monitor every 30 seconds
    
    async def _collect_system_metrics(self) -> SystemMetrics:
        """Collect current system metrics"""
        cpu_percent = psutil.cpu_percent(interval=1)
        memory = psutil.virtual_memory()
        disk = psutil.disk_usage('/')
        
        # Network I/O (simplified)
        network = psutil.net_io_counters()
        network_io_mb = (network.bytes_sent + network.bytes_recv) / (1024 * 1024)
        
        # GPU metrics (if available)
        gpu_memory_mb = None
        gpu_utilization = None
        temperature_c = None
        
        try:
            import pynvml
            pynvml.nvmlInit()
            handle = pynvml.nvmlDeviceGetHandleByIndex(0)
            
            mem_info = pynvml.nvmlDeviceGetMemoryInfo(handle)
            gpu_memory_mb = mem_info.used / (1024 * 1024)
            
            gpu_utilization = pynvml.nvmlDeviceGetUtilizationRates(handle).gpu
            
            temp = pynvml.nvmlDeviceGetTemperature(handle, pynvml.NVML_TEMPERATURE_GPU)
            temperature_c = temp
            
        except:
            pass  # GPU monitoring not available
        
        return SystemMetrics(
            cpu_percent=cpu_percent,
            memory_percent=memory.percent,
            disk_usage_percent=(disk.used / disk.total) * 100,
            disk_free_gb=disk.free / (1024**3),
            network_io_mb=network_io_mb,
            gpu_memory_mb=gpu_memory_mb,
            gpu_utilization=gpu_utilization,
            temperature_c=temperature_c,
            timestamp=time.time()
        )
    
    async def _check_resource_limits(self, metrics: SystemMetrics):
        """Check if resource limits are exceeded"""
        warnings = []
        
        if metrics.memory_percent > 90:
            warnings.append("High memory usage")
        
        if metrics.disk_free_gb < 5:
            warnings.append("Low disk space")
        
        if metrics.cpu_percent > 95:
            warnings.append("High CPU usage")
        
        if warnings:
            logger.warning("Resource limit warnings", warnings=warnings, metrics=asdict(metrics))
    
    async def get_system_status(self) -> Dict[str, Any]:
        """Get current system status"""
        recent_metrics = self.system_metrics[-1] if self.system_metrics else None
        
        return {
            "worker_id": self.worker_id,
            "is_running": self.is_running,
            "active_tasks": len(self.active_tasks),
            "completed_tasks": len(self.completed_tasks),
            "queue_size": self.task_queue.qsize(),
            "recent_metrics": asdict(recent_metrics) if recent_metrics else None,
            "resource_limits": {
                "max_concurrent_tasks": self.max_concurrent_tasks,
                "memory_limit_mb": self.memory_limit_mb,
                "disk_space_limit_gb": self.disk_space_limit_gb
            }
        }
    
    def add_callback(self, callback: Callable):
        """Add task completion callback"""
        self.callbacks.append(callback)
    
    async def cleanup_old_tasks(self, max_age_hours: int = 24):
        """Cleanup old completed tasks"""
        cutoff_time = time.time() - (max_age_hours * 3600)
        
        old_tasks = [
            task for task in self.completed_tasks
            if task.completed_at and task.completed_at < cutoff_time
        ]
        
        for task in old_tasks:
            self.completed_tasks.remove(task)
        
        logger.info(f"Cleaned up {len(old_tasks)} old tasks")
    
    async def update_task_progress(self, task_id: str, progress_percent: float):
        """Update task progress"""
        if task_id in self.active_tasks:
            self.active_tasks[task_id].progress_percent = min(100.0, max(0.0, progress_percent))


class FileProcessingWorker(BaseUtilityWorker):
    """
    Utility worker for file processing tasks
    Handles file conversion, compression, organization, etc.
    """
    
    async def process_task(self, task: UtilityTask) -> Any:
        """Process file-related tasks"""
        task_type = task.task_type
        input_data = task.input_data
        
        if task_type == "compress_files":
            return await self._compress_files(task, input_data)
        elif task_type == "extract_archive":
            return await self._extract_archive(task, input_data)
        elif task_type == "organize_files":
            return await self._organize_files(task, input_data)
        elif task_type == "convert_format":
            return await self._convert_format(task, input_data)
        elif task_type == "cleanup_directory":
            return await self._cleanup_directory(task, input_data)
        else:
            raise ValueError(f"Unknown task type: {task_type}")
    
    async def _compress_files(self, task: UtilityTask, file_paths: List[str]) -> str:
        """Compress files into archive"""
        import zipfile
        
        output_path = task.output_path or f"archive_{int(time.time())}.zip"
        total_files = len(file_paths)
        
        with zipfile.ZipFile(output_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for i, file_path in enumerate(file_paths):
                if os.path.exists(file_path):
                    zipf.write(file_path, os.path.basename(file_path))
                
                # Update progress
                progress = ((i + 1) / total_files) * 100
                await self.update_task_progress(task.task_id, progress)
        
        return output_path
    
    async def _extract_archive(self, task: UtilityTask, archive_path: str) -> str:
        """Extract archive to directory"""
        import zipfile
        
        output_dir = task.output_path or f"extracted_{int(time.time())}"
        os.makedirs(output_dir, exist_ok=True)
        
        with zipfile.ZipFile(archive_path, 'r') as zipf:
            file_list = zipf.namelist()
            total_files = len(file_list)
            
            for i, file_name in enumerate(file_list):
                zipf.extract(file_name, output_dir)
                
                # Update progress
                progress = ((i + 1) / total_files) * 100
                await self.update_task_progress(task.task_id, progress)
        
        return output_dir
    
    async def _organize_files(self, task: UtilityTask, directory: str) -> Dict[str, int]:
        """Organize files by type"""
        file_types = {}
        total_files = 0
        processed_files = 0
        
        # Count total files first
        for root, dirs, files in os.walk(directory):
            total_files += len(files)
        
        # Organize files
        for root, dirs, files in os.walk(directory):
            for file_name in files:
                file_path = os.path.join(root, file_name)
                file_ext = os.path.splitext(file_name)[1].lower()
                
                if file_ext:
                    ext_dir = os.path.join(directory, f"{file_ext[1:]}_files")
                    os.makedirs(ext_dir, exist_ok=True)
                    
                    new_path = os.path.join(ext_dir, file_name)
                    shutil.move(file_path, new_path)
                    
                    file_types[file_ext] = file_types.get(file_ext, 0) + 1
                
                processed_files += 1
                progress = (processed_files / total_files) * 100
                await self.update_task_progress(task.task_id, progress)
        
        return file_types
    
    async def _convert_format(self, task: UtilityTask, conversion_data: Dict[str, Any]) -> str:
        """Convert file format"""
        input_path = conversion_data["input_path"]
        output_path = conversion_data["output_path"]
        format_from = conversion_data["from_format"]
        format_to = conversion_data["to_format"]
        
        # This is a placeholder - actual implementation would depend on specific formats
        # For now, just copy the file
        shutil.copy2(input_path, output_path)
        
        logger.info(f"Format conversion: {format_from} -> {format_to}")
        return output_path
    
    async def _cleanup_directory(self, task: UtilityTask, directory: str) -> Dict[str, int]:
        """Cleanup directory by removing temporary files"""
        removed_files = 0
        freed_bytes = 0
        
        temp_extensions = ['.tmp', '.temp', '.cache', '.log']
        
        for root, dirs, files in os.walk(directory):
            for file_name in files:
                file_path = os.path.join(root, file_name)
                file_ext = os.path.splitext(file_name)[1].lower()
                
                if file_ext in temp_extensions:
                    try:
                        file_size = os.path.getsize(file_path)
                        os.remove(file_path)
                        removed_files += 1
                        freed_bytes += file_size
                        
                        logger.debug(f"Removed temp file: {file_path}")
                    except Exception as e:
                        logger.warning(f"Could not remove {file_path}: {e}")
        
        return {
            "removed_files": removed_files,
            "freed_mb": freed_bytes / (1024 * 1024)
        }


class DataProcessingWorker(BaseUtilityWorker):
    """
    Utility worker for data processing tasks
    Handles CSV processing, data cleaning, transformations, etc.
    """
    
    async def process_task(self, task: UtilityTask) -> Any:
        """Process data-related tasks"""
        task_type = task.task_type
        input_data = task.input_data
        
        if task_type == "process_csv":
            return await self._process_csv(task, input_data)
        elif task_type == "clean_data":
            return await self._clean_data(task, input_data)
        elif task_type == "transform_data":
            return await self._transform_data(task, input_data)
        elif task_type == "validate_data":
            return await self._validate_data(task, input_data)
        else:
            raise ValueError(f"Unknown task type: {task_type}")
    
    async def _process_csv(self, task: UtilityTask, csv_config: Dict[str, Any]) -> str:
        """Process CSV file"""
        import pandas as pd
        
        input_path = csv_config["input_path"]
        output_path = task.output_path or f"processed_{int(time.time())}.csv"
        operations = csv_config.get("operations", [])
        
        # Load CSV
        df = pd.read_csv(input_path)
        total_operations = len(operations)
        
        # Apply operations
        for i, operation in enumerate(operations):
            op_type = operation["type"]
            
            if op_type == "filter":
                condition = operation["condition"]
                df = df.query(condition)
            elif op_type == "sort":
                columns = operation["columns"]
                df = df.sort_values(columns)
            elif op_type == "group":
                group_by = operation["group_by"]
                agg_func = operation["aggregate"]
                df = df.groupby(group_by).agg(agg_func).reset_index()
            
            # Update progress
            progress = ((i + 1) / total_operations) * 100
            await self.update_task_progress(task.task_id, progress)
        
        # Save processed data
        df.to_csv(output_path, index=False)
        
        return output_path
    
    async def _clean_data(self, task: UtilityTask, data_config: Dict[str, Any]) -> Dict[str, Any]:
        """Clean data by removing duplicates, handling missing values"""
        import pandas as pd
        
        input_path = data_config["input_path"]
        df = pd.read_csv(input_path)
        
        original_rows = len(df)
        
        # Remove duplicates
        df = df.drop_duplicates()
        duplicates_removed = original_rows - len(df)
        
        # Handle missing values
        if data_config.get("fill_na"):
            fill_value = data_config["fill_na"]
            df = df.fillna(fill_value)
        elif data_config.get("drop_na"):
            df = df.dropna()
        
        # Save cleaned data
        output_path = task.output_path or f"cleaned_{int(time.time())}.csv"
        df.to_csv(output_path, index=False)
        
        return {
            "original_rows": original_rows,
            "final_rows": len(df),
            "duplicates_removed": duplicates_removed,
            "output_path": output_path
        }
    
    async def _transform_data(self, task: UtilityTask, transform_config: Dict[str, Any]) -> str:
        """Transform data structure"""
        # Placeholder for data transformation logic
        return task.output_path or "transformed_data.json"
    
    async def _validate_data(self, task: UtilityTask, validation_config: Dict[str, Any]) -> Dict[str, Any]:
        """Validate data against schema"""
        # Placeholder for data validation logic
        return {
            "valid": True,
            "errors": [],
            "warnings": []
        }
