"""
Simple API Server
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
                try:
                    status = self.node_controller.get_status()
                    return {"status": "healthy", "details": status}
                except Exception as e:
                    return {"status": "error", "error": str(e)}
            return {"status": "healthy", "message": "Node controller not initialized"}
        
        @self.app.get("/status")
        async def get_status():
            """Get node status"""
            if self.node_controller:
                try:
                    status = self.node_controller.get_status()
                    return status
                except Exception as e:
                    return {"error": str(e)}
            return {"error": "Node controller not initialized"}
        
        @self.app.get("/resources")
        async def get_resources():
            """Get available resources"""
            if self.node_controller and self.node_controller.resource_manager:
                try:
                    resources = self.node_controller.resource_manager.get_available_resources()
                    return {"resources": resources}
                except Exception as e:
                    return {"error": str(e)}
            return {"error": "Resource manager not available"}
        
        @self.app.post("/tasks/execute")
        async def execute_task(task_data: Dict[str, Any]):
            """Execute a new task (stub for now)"""
            if not self.node_controller:
                raise HTTPException(status_code=503, detail="Node controller not available")
            
            # For now, just return a mock response
            import uuid
            task_id = str(uuid.uuid4())
            return {"task_id": task_id, "status": "queued", "message": "Task execution not yet implemented"}
        
        @self.app.get("/info")
        async def get_info():
            """Get general node information"""
            return {
                "node_id": self.node_controller.node_id if self.node_controller else "unknown",
                "version": "1.0.0",
                "api_version": "1.0.0"
            }

    async def start(self):
        """Start the API server"""
        import uvicorn
        logger.info(f"Starting API server on port {self.port}")
        
        config = uvicorn.Config(
            self.app,
            host="0.0.0.0",
            port=self.port,
            log_level="info"
        )
        server = uvicorn.Server(config)
        await server.serve()
