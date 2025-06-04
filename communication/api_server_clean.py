"""
API Server
FastAPI-based REST API server for external communication
"""

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from typing import Dict, List, Optional, Any
import structlog

logger = structlog.get_logger(__name__)


class APIServer:
    """
    FastAPI-based REST API server for node manager
    Provides endpoints for health checks, task management, and metrics
    """
    
    def __init__(self, node_controller=None, port: int = 8010):
        """Initialize API server"""
        self.node_controller = node_controller
        self.port = port
        
        # Create FastAPI app
        self.app = FastAPI(
            title="Node Manager API",
            description="REST API for BitingLip Node Manager",
            version="1.0.0"
        )
        
        # Add CORS middleware
        self.app.add_middleware(
            CORSMiddleware,
            allow_origins=["*"],  # Configure appropriately for production
            allow_credentials=True,
            allow_methods=["*"],
            allow_headers=["*"],
        )
        
        # Register routes
        self._register_routes()
        
        logger.info(f"APIServer initialized on port {port}")
    
    def _register_routes(self):
        """Register API routes"""
        @self.app.get("/health")
        async def health_check():
            """Health check endpoint"""
            if self.node_controller:
                status = await self.node_controller.get_health_status()
                return {"status": "healthy", "details": status}
            return {"status": "healthy", "message": "Node controller not initialized"}
        
        @self.app.get("/status")
        async def get_status():
            """Get node status"""
            if self.node_controller:
                status = await self.node_controller.get_status()
                return status
            return {"error": "Node controller not initialized"}
        
        @self.app.post("/tasks/execute")
        async def execute_task(task_data: Dict[str, Any]):
            """Execute a new task"""
            if not self.node_controller:
                raise HTTPException(status_code=503, detail="Node controller not available")
            
            try:
                task_id = await self.node_controller.submit_task(task_data)
                return {"task_id": task_id, "status": "queued"}
            except Exception as e:
                raise HTTPException(status_code=500, detail=str(e))
        
        @self.app.get("/tasks/{task_id}/status")
        async def get_task_status(task_id: str):
            """Get task status"""
            if not self.node_controller:
                raise HTTPException(status_code=503, detail="Node controller not available")
            
            try:
                status = await self.node_controller.get_task_status(task_id)
                return {"task_id": task_id, "status": status}
            except Exception as e:
                raise HTTPException(status_code=404, detail=str(e))
        
        @self.app.delete("/tasks/{task_id}")
        async def cancel_task(task_id: str):
            """Cancel a task"""
            if not self.node_controller:
                raise HTTPException(status_code=503, detail="Node controller not available")
            
            try:
                result = await self.node_controller.cancel_task(task_id)
                return {"task_id": task_id, "cancelled": result}
            except Exception as e:
                raise HTTPException(status_code=500, detail=str(e))
        
        @self.app.get("/workers")
        async def get_workers():
            """Get worker status"""
            if not self.node_controller:
                raise HTTPException(status_code=503, detail="Node controller not available")
            
            try:
                workers = await self.node_controller.get_workers()
                return {"workers": workers}
            except Exception as e:
                raise HTTPException(status_code=500, detail=str(e))
        
        @self.app.get("/metrics")
        async def get_metrics():
            """Get node metrics"""
            if not self.node_controller:
                raise HTTPException(status_code=503, detail="Node controller not available")
            
            try:
                metrics = await self.node_controller.get_metrics()
                return metrics
            except Exception as e:
                raise HTTPException(status_code=500, detail=str(e))
        
        @self.app.put("/config/update")
        async def update_config(config_data: Dict[str, Any]):
            """Update node configuration"""
            if not self.node_controller:
                raise HTTPException(status_code=503, detail="Node controller not available")
            
            try:
                await self.node_controller.update_config(config_data)
                return {"message": "Configuration updated"}
            except Exception as e:
                raise HTTPException(status_code=500, detail=str(e))
    
    async def start(self):
        """Start the API server"""
        import uvicorn
        
        config = uvicorn.Config(
            app=self.app,
            host="0.0.0.0",
            port=self.port,
            log_level="info"
        )
        
        server = uvicorn.Server(config)
        logger.info(f"Starting API server on port {self.port}")
        await server.serve()
        
    async def stop(self):
        """Stop the API server"""
        # Server will be stopped by cancelling the task
        logger.info("Stopping API server")
