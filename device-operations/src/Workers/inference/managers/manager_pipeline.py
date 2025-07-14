"""
Pipeline Manager for SDXL Workers System
=======================================

Migrated from inference/pipeline_manager.py
Handles pipeline lifecycle management and coordination between different inference modes.
"""

import logging
import asyncio
from typing import Dict, Any, Optional, List, Union
from dataclasses import dataclass
from datetime import datetime
import uuid


@dataclass
class WorkerRequest:
    """Represents a worker request."""
    request_id: str
    request_type: str
    data: Dict[str, Any]
    timestamp: Optional[datetime] = None


@dataclass 
class WorkerResponse:
    """Represents a worker response."""
    request_id: str
    status: str
    data: Dict[str, Any]
    error: Optional[str] = None
    timestamp: Optional[datetime] = None


@dataclass
class PipelineTask:
    """Represents a pipeline task."""
    task_id: str
    pipeline_type: str
    request_data: Dict[str, Any]
    priority: int = 0
    created_at: Optional[datetime] = None
    
    def __post_init__(self):
        if self.created_at is None:
            self.created_at = datetime.utcnow()


class PipelineManager:
    """
    Handles pipeline lifecycle management and coordination between different inference modes.
    
    This manager handles task queuing, pipeline switching, multi-stage generation,
    and resource management across different inference types.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.initialized = False
        
        # Pipeline state
        self.active_pipelines: Dict[str, Any] = {}
        self.task_queue: List[PipelineTask] = []
        self.pipeline_stats: Dict[str, Any] = {}
        
        # Task management  
        self.active_tasks: Dict[str, PipelineTask] = {}
        self.completed_tasks: Dict[str, Dict[str, Any]] = {}
        
        # Configuration
        self.max_concurrent_tasks = self.config.get("max_concurrent_tasks", 2)
        self.task_timeout = self.config.get("task_timeout", 600)  # 10 minutes
        
    async def initialize(self) -> bool:
        """Initialize pipeline manager."""
        try:
            self.logger.info("Initializing pipeline manager...")
            self.initialized = True
            self.logger.info("Pipeline manager initialized successfully")
            return True
        except Exception as e:
            self.logger.error(f"Pipeline manager initialization failed: {e}")
            return False
    
    async def get_pipeline_info(self) -> Dict[str, Any]:
        """Get pipeline information."""
        return {
            "active_pipelines": len(self.active_pipelines),
            "queued_tasks": len(self.task_queue),
            "pipeline_stats": self.pipeline_stats,
            "supported_types": ["text2img", "img2img", "inpainting", "controlnet", "lora"],
            "supported_models": ["stable-diffusion-xl", "stable-diffusion-v1-5", "flux"],
            "max_batch_size": 8,
            "max_concurrent": 3,
            "max_width": 2048,
            "max_height": 2048
        }

    async def get_session_status(self, session_id: str) -> Dict[str, Any]:
        """Get status of a specific session."""
        # Check if session is in active tasks
        if session_id in self.active_tasks:
            task = self.active_tasks[session_id]
            return {
                "session_id": session_id,
                "status": "running",
                "task_type": task.pipeline_type,
                "created_at": task.created_at.isoformat() if task.created_at else None,
                "progress": 0.5  # Mock progress
            }
        
        # Check if session is in completed tasks
        if session_id in self.completed_tasks:
            completed = self.completed_tasks[session_id]
            return {
                "session_id": session_id,
                "status": "completed" if completed["result"].success else "failed",
                "task_type": completed["task"].pipeline_type,
                "created_at": completed["task"].created_at.isoformat() if completed["task"].created_at else None,
                "completed_at": completed["completed_at"].isoformat(),
                "result": completed["result"].data if completed["result"].success else None,
                "error": completed["result"].error if not completed["result"].success else None
            }
        
        # Session not found
        return {
            "session_id": session_id,
            "status": "not_found",
            "error": f"Session {session_id} not found"
        }

    async def cancel_session(self, session_id: str, reason: str = "user_requested") -> Dict[str, Any]:
        """Cancel a session."""
        cancelled = False
        
        # Remove from queue if present
        original_queue_length = len(self.task_queue)
        self.task_queue = [task for task in self.task_queue if task.task_id != session_id]
        if len(self.task_queue) < original_queue_length:
            cancelled = True
        
        # Remove from active tasks if present
        if session_id in self.active_tasks:
            del self.active_tasks[session_id]
            cancelled = True
        
        return {
            "session_id": session_id,
            "cancelled": cancelled,
            "reason": reason,
            "timestamp": datetime.utcnow().isoformat()
        }

    async def get_active_sessions(self) -> List[Dict[str, Any]]:
        """Get list of all active sessions."""
        sessions = []
        
        # Add queued tasks
        for task in self.task_queue:
            sessions.append({
                "session_id": task.task_id,
                "status": "queued",
                "task_type": task.pipeline_type,
                "created_at": task.created_at.isoformat() if task.created_at else None,
                "priority": task.priority
            })
        
        # Add active tasks
        for session_id, task in self.active_tasks.items():
            sessions.append({
                "session_id": session_id,
                "status": "running",
                "task_type": task.pipeline_type,
                "created_at": task.created_at.isoformat() if task.created_at else None,
                "priority": task.priority
            })
        
        return sessions
        self.model_loader: Optional[ModelLoader] = None
        
        # Task management
        self.task_queue: List[PipelineTask] = []
        self.active_tasks: Dict[str, PipelineTask] = {}
        self.completed_tasks: Dict[str, Dict[str, Any]] = {}
        
        # Configuration
        self.max_concurrent_tasks = self.config.get("max_concurrent_tasks", 2)
        self.task_timeout = self.config.get("task_timeout", 600)  # 10 minutes
        
        # Pipeline configurations
        self.pipeline_configs = self.config.get("pipelines", {})
        
        # Resource management
        self.device_manager = None
        
    async def initialize(self) -> bool:
        """Initialize the pipeline manager."""
        try:
            self.logger.info("Initializing pipeline manager...")
            
            # Initialize device manager
            self.device_manager = get_device_manager()
            
            # Initialize model loader
            model_loader_config = self.config.get("model_loader", {})
            self.model_loader = ModelLoader("model_loader", model_loader_config)
            await self.model_loader.initialize()
            
            # Initialize SDXL worker
            sdxl_config = self.config.get("sdxl_worker", {})
            self.sdxl_worker = SDXLWorker("sdxl_worker", sdxl_config)
            await self.sdxl_worker.initialize()
            
            self.logger.info("Pipeline manager initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to initialize pipeline manager: {str(e)}")
            return False
    
    async def process_request(self, request: WorkerRequest) -> WorkerResponse:
        """Process a pipeline management request."""
        try:
            request_type = request.data.get("type", "inference")
            
            if request_type == "inference":
                return await self._handle_inference_request(request)
            elif request_type == "multi_stage":
                return await self._handle_multi_stage_request(request)
            elif request_type == "batch":
                return await self._handle_batch_request(request)
            elif request_type == "queue_task":
                return await self._handle_queue_task(request)
            elif request_type == "get_status":
                return await self._handle_status_request(request)
            elif request_type == "cancel_task":
                return await self._handle_cancel_task(request)
            else:
                raise WorkerError(f"Unknown request type: {request_type}")
                
        except Exception as e:
            error_msg = f"Pipeline manager error: {str(e)}"
            self.logger.error(error_msg)
            return WorkerResponse(
                request_id=request.request_id,
                success=False,
                error=error_msg
            )
    
    async def _handle_inference_request(self, request: WorkerRequest) -> WorkerResponse:
        """Handle a single inference request."""
        self.logger.info(f"Handling inference request: {request.request_id}")
        
        # Forward to SDXL worker
        return await self.sdxl_worker.process_request(request)
    
    async def _handle_multi_stage_request(self, request: WorkerRequest) -> WorkerResponse:
        """Handle a multi-stage inference request."""
        self.logger.info(f"Handling multi-stage request: {request.request_id}")
        
        stages = request.data.get("stages", [])
        if not stages:
            raise WorkerError("No stages specified for multi-stage request")
        
        results = []
        intermediate_images = []
        
        for i, stage_config in enumerate(stages):
            stage_id = f"{request.request_id}_stage_{i}"
            self.logger.info(f"Processing stage {i + 1}/{len(stages)}: {stage_id}")
            
            # Prepare stage request
            stage_data = stage_config.copy()
            
            # Use output from previous stage as input if specified
            if i > 0 and stage_config.get("use_previous_output", False):
                if intermediate_images:
                    stage_data["init_image"] = intermediate_images[-1]
            
            # Create stage request
            stage_request = WorkerRequest(
                request_id=stage_id,
                worker_type="sdxl_inference",
                data=stage_data,
                priority=request.priority,
                timeout=request.timeout
            )
            
            # Process stage
            stage_response = await self.sdxl_worker.process_request(stage_request)
            
            if not stage_response.success:
                return WorkerResponse(
                    request_id=request.request_id,
                    success=False,
                    error=f"Stage {i + 1} failed: {stage_response.error}"
                )
            
            results.append(stage_response.data)
            
            # Store intermediate images for next stage
            if stage_response.data.get("images"):
                intermediate_images.extend(stage_response.data["images"])
        
        # Combine results
        combined_results = {
            "stages": results,
            "final_images": results[-1].get("images", []) if results else [],
            "total_processing_time": sum(r.get("processing_time", 0) for r in results)
        }
        
        return WorkerResponse(
            request_id=request.request_id,
            success=True,
            data=combined_results
        )
    
    async def _handle_batch_request(self, request: WorkerRequest) -> WorkerResponse:
        """Handle a batch inference request."""
        self.logger.info(f"Handling batch request: {request.request_id}")
        
        batch_items = request.data.get("batch_items", [])
        if not batch_items:
            raise WorkerError("No batch items specified")
        
        max_concurrent = min(
            self.max_concurrent_tasks,
            request.data.get("max_concurrent", self.max_concurrent_tasks)
        )
        
        # Create tasks for batch items
        tasks = []
        for i, item_data in enumerate(batch_items):
            item_id = f"{request.request_id}_item_{i}"
            
            item_request = WorkerRequest(
                request_id=item_id,
                worker_type="sdxl_inference",
                data=item_data,
                priority=request.priority,
                timeout=request.timeout
            )
            
            tasks.append(self._process_batch_item(item_request))
        
        # Process batch with concurrency limit
        semaphore = asyncio.Semaphore(max_concurrent)
        
        async def limited_task(task):
            async with semaphore:
                return await task
        
        results = await asyncio.gather(
            *[limited_task(task) for task in tasks],
            return_exceptions=True
        )
        
        # Process results
        successful_results = []
        failed_results = []
        
        for i, result in enumerate(results):
            if isinstance(result, Exception):
                failed_results.append({
                    "item_index": i,
                    "error": str(result)
                })
            elif isinstance(result, WorkerResponse):
                if result.success:
                    successful_results.append({
                        "item_index": i,
                        "data": result.data
                    })
                else:
                    failed_results.append({
                        "item_index": i,
                        "error": result.error
                    })
        
        batch_results = {
            "total_items": len(batch_items),
            "successful_items": len(successful_results),
            "failed_items": len(failed_results),
            "results": successful_results,
            "failures": failed_results
        }
        
        return WorkerResponse(
            request_id=request.request_id,
            success=len(failed_results) == 0,
            data=batch_results,
            warnings=[f"Failed items: {len(failed_results)}"] if failed_results else None
        )
    
    async def _process_batch_item(self, request: WorkerRequest) -> WorkerResponse:
        """Process a single batch item."""
        try:
            return await self.sdxl_worker.process_request(request)
        except Exception as e:
            return WorkerResponse(
                request_id=request.request_id,
                success=False,
                error=str(e)
            )
    
    async def _handle_queue_task(self, request: WorkerRequest) -> WorkerResponse:
        """Handle task queuing request."""
        task_data = request.data.get("task_data", {})
        pipeline_type = request.data.get("pipeline_type", "text2img")
        priority = request.data.get("priority", 0)
        
        # Create task
        task = PipelineTask(
            task_id=request.request_id,
            pipeline_type=pipeline_type,
            request_data=task_data,
            priority=priority
        )
        
        # Add to queue (sorted by priority)
        self.task_queue.append(task)
        self.task_queue.sort(key=lambda t: t.priority, reverse=True)
        
        return WorkerResponse(
            request_id=request.request_id,
            success=True,
            data={
                "task_id": task.task_id,
                "queue_position": self.task_queue.index(task),
                "queue_length": len(self.task_queue)
            }
        )
    
    async def _handle_status_request(self, request: WorkerRequest) -> WorkerResponse:
        """Handle status request."""
        status_data = {
            "pipeline_manager": self.get_status(),
            "sdxl_worker": self.sdxl_worker.get_status() if self.sdxl_worker else None,
            "model_loader": self.model_loader.get_cache_stats() if self.model_loader else None,
            "device_info": self.device_manager.get_device_info().__dict__ if self.device_manager else None,
            "queue_info": {
                "queued_tasks": len(self.task_queue),
                "active_tasks": len(self.active_tasks),
                "completed_tasks": len(self.completed_tasks)
            }
        }
        
        return WorkerResponse(
            request_id=request.request_id,
            success=True,
            data=status_data
        )
    
    async def _handle_cancel_task(self, request: WorkerRequest) -> WorkerResponse:
        """Handle task cancellation request."""
        task_id = request.data.get("task_id")
        if not task_id:
            raise WorkerError("No task_id specified for cancellation")
        
        # Remove from queue
        removed_from_queue = False
        self.task_queue = [t for t in self.task_queue if t.task_id != task_id]
        if len(self.task_queue) != len([t for t in self.task_queue if t.task_id != task_id]):
            removed_from_queue = True
        
        # Remove from active tasks (cancellation of running tasks)
        removed_from_active = False
        if task_id in self.active_tasks:
            del self.active_tasks[task_id]
            removed_from_active = True
        
        success = removed_from_queue or removed_from_active
        
        return WorkerResponse(
            request_id=request.request_id,
            success=success,
            data={
                "task_id": task_id,
                "removed_from_queue": removed_from_queue,
                "removed_from_active": removed_from_active
            }
        )
    
    async def process_task_queue(self) -> None:
        """Process tasks from the queue."""
        while self.task_queue and len(self.active_tasks) < self.max_concurrent_tasks:
            task = self.task_queue.pop(0)
            
            # Move to active tasks
            self.active_tasks[task.task_id] = task
            
            # Process task asynchronously
            asyncio.create_task(self._process_queued_task(task))
    
    async def _process_queued_task(self, task: PipelineTask) -> None:
        """Process a queued task."""
        try:
            self.logger.info(f"Processing queued task: {task.task_id}")
            
            # Create request
            task_request = WorkerRequest(
                request_id=task.task_id,
                worker_type="sdxl_inference",
                data=task.request_data,
                priority=task.priority
            )
            
            # Process task
            result = await self.sdxl_worker.process_request(task_request)
            
            # Store result
            self.completed_tasks[task.task_id] = {
                "task": task,
                "result": result,
                "completed_at": datetime.utcnow()
            }
            
            self.logger.info(f"Completed task: {task.task_id}")
            
        except Exception as e:
            self.logger.error(f"Failed to process task {task.task_id}: {str(e)}")
            
            # Store error result
            error_result = WorkerResponse(
                request_id=task.task_id,
                success=False,
                error=str(e)
            )
            
            self.completed_tasks[task.task_id] = {
                "task": task,
                "result": error_result,
                "completed_at": datetime.utcnow()
            }
            
        finally:
            # Remove from active tasks
            if task.task_id in self.active_tasks:
                del self.active_tasks[task.task_id]
    
    def create_workflow(self, workflow_config: Dict[str, Any]) -> str:
        """Create a complex workflow with multiple stages."""
        workflow_id = str(uuid.uuid4())
        
        # Store workflow configuration
        # This would be expanded to handle complex workflows
        # with dependencies, branching, etc.
        
        return workflow_id
    
    def get_task_result(self, task_id: str) -> Optional[Dict[str, Any]]:
        """Get the result of a completed task."""
        return self.completed_tasks.get(task_id)
    
    def list_available_pipelines(self) -> List[str]:
        """List available pipeline types."""
        return ["text2img", "img2img", "inpainting", "upscaling"]
    
    async def cleanup(self) -> None:
        """Clean up pipeline manager resources."""
        # Cancel all active tasks
        for task_id in list(self.active_tasks.keys()):
            del self.active_tasks[task_id]
        
        # Clear queues
        self.task_queue.clear()
        self.completed_tasks.clear()
        
        # Clean up workers
    async def get_status(self) -> Dict[str, Any]:
        """Get pipeline manager status."""
        return {
            "initialized": self.initialized,
            "queue_length": len(self.task_queue),
            "active_pipelines": len(self.active_pipelines),
            "pipeline_stats": self.pipeline_stats
        }
    
    async def cleanup(self) -> None:
        """Clean up pipeline manager resources."""
        try:
            self.logger.info("Cleaning up pipeline manager...")
            
            # Clear pipeline state
            self.active_pipelines.clear()
            self.task_queue.clear()
            self.pipeline_stats.clear()
            
            self.initialized = False
            self.logger.info("Pipeline manager cleanup complete")
        except Exception as e:
            self.logger.error(f"Pipeline manager cleanup error: {e}")
