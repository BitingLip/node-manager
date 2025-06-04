"""
Communication Coordinator
Orchestrates all communication components for the node manager
Demonstrates how ClusterClient, MessageQueue, and APIServer work together
"""

import asyncio
import logging
from typing import Dict, List, Optional, Any
import structlog

from .cluster_client import ClusterClient
from .message_queue import MessageQueue
from .api_server import APIServer

logger = structlog.get_logger(__name__)


class CommunicationCoordinator:
    """
    Coordinates all communication components for the node manager
    Acts as the central hub for external and internal communication
    """
    
    def __init__(self, config: Dict[str, Any], node_controller=None):
        """Initialize communication coordinator"""
        self.config = config
        self.node_controller = node_controller
        
        # Extract configuration
        cluster_config = config.get('cluster', {})
        node_config = config.get('node', {})
        
        # Initialize components
        self.cluster_client = ClusterClient(
            cluster_manager_url=f"http://{cluster_config.get('manager_host', 'localhost')}:{cluster_config.get('manager_port', 8005)}",
            node_id=config.get('node_id', 'unknown-node'),
            auth_token=cluster_config.get('auth_token')
        )
        
        self.message_queue = MessageQueue()
        
        self.api_server = APIServer(
            node_controller=node_controller,
            port=node_config.get('port', 8010)
        )
        
        # Coordination state
        self.running = False
        self.tasks = []
        
        logger.info("CommunicationCoordinator initialized")
    
    async def start(self):
        """Start all communication components"""
        logger.info("Starting communication coordinator...")
        
        try:
            # Start message queue (no async start needed)
            logger.info("Message queue ready")
            
            # Connect to cluster manager
            if await self.cluster_client.connect():
                logger.info("Connected to cluster manager")
                
                # Register this node
                capabilities = await self._get_node_capabilities()
                resources = await self._get_node_resources()
                
                if await self.cluster_client.register_node(capabilities, resources):
                    logger.info("Node registered with cluster manager")
                else:
                    logger.error("Failed to register node with cluster manager")
            else:
                logger.error("Failed to connect to cluster manager")
            
            # Setup message queue subscriptions
            self._setup_message_subscriptions()
            
            # Start API server (in background)
            api_task = asyncio.create_task(self.api_server.start())
            self.tasks.append(api_task)
            
            # Start coordination loops
            coordination_task = asyncio.create_task(self._coordination_loop())
            self.tasks.append(coordination_task)
            
            self.running = True
            logger.info("Communication coordinator started successfully")
            
        except Exception as e:
            logger.error(f"Failed to start communication coordinator: {e}")
            await self.stop()
            raise
    
    async def stop(self):
        """Stop all communication components"""
        logger.info("Stopping communication coordinator...")
        
        self.running = False
        
        try:
            # Stop API server
            await self.api_server.stop()
            
            # Disconnect from cluster manager
            await self.cluster_client.disconnect()
            
            # Shutdown message queue
            await self.message_queue.shutdown()
            
            # Cancel all tasks
            for task in self.tasks:
                if not task.done():
                    task.cancel()
                    try:
                        await task
                    except asyncio.CancelledError:
                        pass
            
            logger.info("Communication coordinator stopped")
            
        except Exception as e:
            logger.error(f"Error stopping communication coordinator: {e}")
    
    def _setup_message_subscriptions(self):
        """Setup message queue subscriptions for coordination"""
        # Subscribe to task completion events
        self.message_queue.subscribe("tasks.completed", self._on_task_completed)
        
        # Subscribe to status updates
        self.message_queue.subscribe("status.updates", self._on_status_update)
        
        # Subscribe to error events
        self.message_queue.subscribe("errors.occurred", self._on_error_occurred)
        
        logger.info("Message subscriptions configured")
    
    async def _coordination_loop(self):
        """Main coordination loop"""
        logger.info("Starting coordination loop")
        
        while self.running:
            try:
                # Check for new task assignments from cluster
                task_assignment = await self.cluster_client.get_task_assignment()
                if task_assignment:
                    await self._handle_task_assignment(task_assignment)
                
                # Process internal messages
                await self._process_internal_messages()
                
                # Update cluster with node status
                await self._update_cluster_status()
                
                # Wait before next iteration
                await asyncio.sleep(5)  # 5-second coordination cycle
                
            except asyncio.CancelledError:
                logger.info("Coordination loop cancelled")
                break
            except Exception as e:
                logger.error(f"Error in coordination loop: {e}")
                await asyncio.sleep(10)  # Longer wait on error
    
    async def _handle_task_assignment(self, task_assignment: Dict[str, Any]):
        """Handle new task assignment from cluster"""
        logger.info(f"Received task assignment: {task_assignment.get('task_id')}")
        
        try:
            # Queue the task for processing
            await self.message_queue.put_message("tasks", task_assignment, priority=1)
            
            # Report task received to cluster
            await self.cluster_client.report_task_status(
                task_assignment['task_id'], 
                "received"
            )
            
        except Exception as e:
            logger.error(f"Error handling task assignment: {e}")
            await self.cluster_client.report_task_status(
                task_assignment.get('task_id', 'unknown'), 
                "failed",
                {"error": str(e)}
            )
    
    async def _process_internal_messages(self):
        """Process messages from internal queues"""
        # Process status updates
        status_message = await self.message_queue.get_message("status", timeout=0.1)
        if status_message:
            await self._handle_status_message(status_message)
        
        # Process error messages
        error_message = await self.message_queue.get_message("errors", timeout=0.1)
        if error_message:
            await self._handle_error_message(error_message)
    
    async def _handle_status_message(self, message: Dict[str, Any]):
        """Handle status update messages"""
        data = message.get('data', {})
        task_id = data.get('task_id')
        status = data.get('status')
        result = data.get('result')
        
        if task_id and status:
            # Report to cluster manager
            await self.cluster_client.report_task_status(task_id, status, result)
            logger.debug(f"Reported task status to cluster: {task_id} -> {status}")
    
    async def _handle_error_message(self, message: Dict[str, Any]):
        """Handle error messages"""
        data = message.get('data', {})
        error_type = data.get('type', 'unknown')
        error_details = data.get('details', {})
        
        logger.error(f"Processing error event: {error_type} - {error_details}")
        
        # Could implement error escalation to cluster manager here
        await self.cluster_client.update_node_status("error", {
            "error_type": error_type,
            "error_details": error_details,
            "timestamp": message.get('timestamp')
        })
    
    async def _update_cluster_status(self):
        """Update cluster manager with current node status"""
        try:
            # Get current resources
            resources = await self._get_node_resources()
            await self.cluster_client.report_resources(resources)
            
            # Update general status
            queue_status = self.message_queue.get_all_queue_status()
            await self.cluster_client.update_node_status("active", {
                "queue_status": queue_status,
                "api_server_port": self.api_server.port
            })
            
        except Exception as e:
            logger.error(f"Error updating cluster status: {e}")
    
    async def _get_node_capabilities(self) -> Dict[str, Any]:
        """Get node capabilities for registration"""
        if self.node_controller:
            try:
                return await self.node_controller.get_capabilities()
            except Exception as e:
                logger.error(f"Error getting capabilities from node controller: {e}")
        
        # Default capabilities
        return {
            "supports_inference": True,
            "supports_training": False,
            "gpu_available": False,
            "max_concurrent_tasks": 4
        }
    
    async def _get_node_resources(self) -> Dict[str, Any]:
        """Get current node resource usage"""
        if self.node_controller:
            try:
                return await self.node_controller.get_resource_usage()
            except Exception as e:
                logger.error(f"Error getting resources from node controller: {e}")
        
        # Default resource info
        import psutil
        return {
            "cpu_percent": psutil.cpu_percent(interval=1),
            "memory_percent": psutil.virtual_memory().percent,
            "disk_percent": psutil.disk_usage('/').percent if hasattr(psutil.disk_usage('/'), 'percent') else 0,
            "active_tasks": 0
        }
    
    async def _on_task_completed(self, topic: str, message: Dict[str, Any]):
        """Callback for task completion events"""
        logger.info(f"Task completed event: {message}")
        
        # Queue status update
        await self.message_queue.put_message("status", {
            "task_id": message.get('task_id'),
            "status": "completed",
            "result": message.get('result'),
            "completion_time": message.get('timestamp')
        })
    
    async def _on_status_update(self, topic: str, message: Dict[str, Any]):
        """Callback for status update events"""
        logger.debug(f"Status update event: {message}")
        # Status updates are processed in the coordination loop
    
    async def _on_error_occurred(self, topic: str, message: Dict[str, Any]):
        """Callback for error events"""
        logger.error(f"Error event: {message}")
        
        # Queue error for processing
        await self.message_queue.put_message("errors", {
            "type": message.get('error_type', 'unknown'),
            "details": message.get('error_details', {}),
            "timestamp": message.get('timestamp')
        })
    
    # Public API for other components
    async def submit_task_result(self, task_id: str, status: str, result: Optional[Any] = None):
        """Submit task result (called by task processors)"""
        await self.message_queue.put_message("status", {
            "task_id": task_id,
            "status": status,
            "result": result
        })
    
    async def report_error(self, error_type: str, error_details: Dict[str, Any]):
        """Report an error (called by other components)"""
        await self.message_queue.put_message("errors", {
            "type": error_type,
            "details": error_details
        })
    
    async def get_communication_status(self) -> Dict[str, Any]:
        """Get status of all communication components"""
        return {
            "cluster_connected": self.cluster_client.connected,
            "api_server_running": self.running,
            "message_queue_status": self.message_queue.get_all_queue_status(),
            "active_tasks_count": len(self.tasks)
        }
