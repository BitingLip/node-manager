"""
Cluster Client
Handles communication between node manager and cluster manager
Manages registration, heartbeat, task coordination, and status reporting
"""

import aiohttp
import asyncio
import logging
import socket
from typing import Dict, List, Optional, Any, Callable
from datetime import datetime
import structlog

logger = structlog.get_logger(__name__)


class ClusterClient:
    """
    Client for communicating with cluster manager
    Handles node registration, heartbeat, and task coordination
    """
    
    def __init__(self, cluster_manager_url: str, node_id: str, auth_token: Optional[str] = None):
        """Initialize cluster client"""
        self.cluster_manager_url = cluster_manager_url.rstrip('/')
        self.node_id = node_id
        self.auth_token = auth_token
        self.session = None
        self.connected = False
        
        # Heartbeat configuration
        self.heartbeat_interval = 30  # seconds
        self.heartbeat_task = None
        
        logger.info(f"ClusterClient initialized for node {node_id}")
    
    async def connect(self) -> bool:
        """Connect to cluster manager"""
        try:
            # Create aiohttp session with proper configuration
            timeout = aiohttp.ClientTimeout(total=30, connect=10)
            connector = aiohttp.TCPConnector(
                keepalive_timeout=30,
                enable_cleanup_closed=True
            )
            
            self.session = aiohttp.ClientSession(
                timeout=timeout,
                connector=connector,
                headers=self._prepare_headers()
            )
            
            # Test connection with health check
            async with self.session.get(f"{self.cluster_manager_url}/health") as response:
                if response.status == 200:
                    self.connected = True
                    logger.info(f"Successfully connected to cluster manager at {self.cluster_manager_url}")
                    return True
                else:
                    logger.error(f"Cluster manager health check failed: {response.status}")
                    return False
                    
        except Exception as e:
            logger.error(f"Failed to connect to cluster manager: {e}")
            if self.session:
                await self.session.close()
                self.session = None
            return False

    async def disconnect(self):
        """Disconnect from cluster manager"""
        try:
            # Stop heartbeat loop
            if self.heartbeat_task and not self.heartbeat_task.done():
                self.heartbeat_task.cancel()
                try:
                    await self.heartbeat_task
                except asyncio.CancelledError:
                    pass
            
            # Close HTTP session
            if self.session:
                await self.session.close()
                self.session = None
            
            self.connected = False
            logger.info("Disconnected from cluster manager")
            
        except Exception as e:
            logger.error(f"Error during disconnect: {e}")

    async def register_node(self, capabilities: Dict[str, Any], resources: Dict[str, Any]) -> bool:
        """Register this node with cluster manager"""
        if not self.connected or not self.session:
            logger.error("Not connected to cluster manager")
            return False
            
        try:
            registration_data = {
                "node_id": self.node_id,
                "hostname": socket.gethostname(),
                "capabilities": capabilities,
                "resources": resources,
                "timestamp": datetime.utcnow().isoformat(),
                "status": "registering"
            }
            
            async with self.session.post(
                f"{self.cluster_manager_url}/nodes/register",
                json=registration_data
            ) as response:
                if response.status == 201:
                    logger.info(f"Successfully registered node {self.node_id}")
                    # Start heartbeat after successful registration
                    await self.start_heartbeat_loop()
                    return True
                else:
                    error_text = await response.text()
                    logger.error(f"Node registration failed: {response.status} - {error_text}")
                    return False
                    
        except Exception as e:
            logger.error(f"Failed to register node: {e}")
            return False

    async def send_heartbeat(self) -> bool:
        """Send heartbeat to cluster manager"""
        if not self.connected or not self.session:
            return False
            
        try:
            heartbeat_data = {
                "node_id": self.node_id,
                "timestamp": datetime.utcnow().isoformat(),
                "status": "active"
            }
            
            async with self.session.put(
                f"{self.cluster_manager_url}/nodes/{self.node_id}/heartbeat",
                json=heartbeat_data
            ) as response:
                if response.status == 200:
                    return True
                else:
                    logger.warning(f"Heartbeat failed: {response.status}")
                    return False
                    
        except Exception as e:
            logger.error(f"Heartbeat error: {e}")
            return False

    async def update_node_status(self, status: str, details: Optional[Dict[str, Any]] = None) -> bool:
        """Update node status with cluster manager"""
        if not self.connected or not self.session:
            return False
            
        try:
            status_data = {
                "node_id": self.node_id,
                "status": status,
                "details": details or {},
                "timestamp": datetime.utcnow().isoformat()
            }
            
            async with self.session.put(
                f"{self.cluster_manager_url}/nodes/{self.node_id}/status",
                json=status_data
            ) as response:
                return response.status == 200
                
        except Exception as e:
            logger.error(f"Failed to update node status: {e}")
            return False

    async def report_capabilities(self, capabilities: Dict[str, Any]) -> bool:
        """Report updated capabilities to cluster"""
        if not self.connected or not self.session:
            return False
            
        try:
            async with self.session.put(
                f"{self.cluster_manager_url}/nodes/{self.node_id}/capabilities",
                json={"capabilities": capabilities, "timestamp": datetime.utcnow().isoformat()}
            ) as response:
                return response.status == 200
                
        except Exception as e:
            logger.error(f"Failed to report capabilities: {e}")
            return False

    async def report_resources(self, resources: Dict[str, Any]) -> bool:
        """Report current resource usage to cluster"""
        if not self.connected or not self.session:
            return False
            
        try:
            async with self.session.put(
                f"{self.cluster_manager_url}/nodes/{self.node_id}/resources",
                json={"resources": resources, "timestamp": datetime.utcnow().isoformat()}
            ) as response:
                return response.status == 200
                
        except Exception as e:
            logger.error(f"Failed to report resources: {e}")
            return False

    async def get_task_assignment(self) -> Optional[Dict[str, Any]]:
        """Check for new task assignments from cluster"""
        if not self.connected or not self.session:
            return None
            
        try:
            async with self.session.get(
                f"{self.cluster_manager_url}/nodes/{self.node_id}/tasks/next"
            ) as response:
                if response.status == 200:
                    return await response.json()
                elif response.status == 204:
                    # No tasks available
                    return None
                else:
                    logger.warning(f"Failed to get task assignment: {response.status}")
                    return None
                    
        except Exception as e:
            logger.error(f"Error getting task assignment: {e}")
            return None

    async def report_task_status(self, task_id: str, status: str, result: Optional[Any] = None) -> bool:
        """Report task execution status to cluster"""
        if not self.connected or not self.session:
            return False
            
        try:
            status_data = {
                "task_id": task_id,
                "node_id": self.node_id,
                "status": status,
                "result": result,
                "timestamp": datetime.utcnow().isoformat()
            }
            
            async with self.session.put(
                f"{self.cluster_manager_url}/tasks/{task_id}/status",
                json=status_data
            ) as response:
                return response.status == 200
                
        except Exception as e:
            logger.error(f"Failed to report task status: {e}")
            return False

    async def start_heartbeat_loop(self):
        """Start automatic heartbeat loop"""
        if self.heartbeat_task and not self.heartbeat_task.done():
            # Already running
            return
            
        async def heartbeat_loop():
            """Heartbeat loop coroutine"""
            while self.connected:
                try:
                    success = await self.send_heartbeat()
                    if not success:
                        logger.warning("Heartbeat failed, connection may be lost")
                        # Could implement reconnection logic here
                    
                    await asyncio.sleep(self.heartbeat_interval)
                    
                except asyncio.CancelledError:
                    logger.info("Heartbeat loop cancelled")
                    break
                except Exception as e:
                    logger.error(f"Heartbeat loop error: {e}")
                    await asyncio.sleep(self.heartbeat_interval)
        
        self.heartbeat_task = asyncio.create_task(heartbeat_loop())
        logger.info(f"Started heartbeat loop with {self.heartbeat_interval}s interval")

    async def stop_heartbeat_loop(self):
        """Stop heartbeat loop"""
        if self.heartbeat_task and not self.heartbeat_task.done():
            self.heartbeat_task.cancel()
            try:
                await self.heartbeat_task
            except asyncio.CancelledError:
                pass
            
        self.heartbeat_task = None
        logger.info("Stopped heartbeat loop")

    def _prepare_headers(self) -> Dict[str, str]:
        """Prepare HTTP headers for requests"""
        headers = {"Content-Type": "application/json"}
        if self.auth_token:
            headers["Authorization"] = f"Bearer {self.auth_token}"
        return headers
