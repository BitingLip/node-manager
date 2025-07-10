"""
Post-processing Interface for SDXL Workers System
=================================================

Unified interface for post-processing operations including image enhancement,
upscaling, and safety checking.
"""

import logging
from typing import Dict, Any, Optional


class PostprocessingInterface:
    """
    Unified interface for post-processing operations.
    
    This interface provides a consistent API for post-processing operations
    and delegates to appropriate managers and workers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(f"{__name__}.{self.__class__.__name__}")
        
        # Manager and worker instances
        self.postprocessing_manager = None
        self.upscaler_worker = None
        self.image_enhancer_worker = None
        self.safety_checker_worker = None
        
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize post-processing interface and components."""
        try:
            self.logger.info("Initializing post-processing interface...")
            
            # Import components (lazy loading)
            from .managers.manager_postprocessing import PostprocessingManager
            from .workers.worker_upscaler import UpscalerWorker
            from .workers.worker_image_enhancer import ImageEnhancerWorker
            from .workers.worker_safety_checker import SafetyCheckerWorker
            
            # Create components
            self.postprocessing_manager = PostprocessingManager(self.config)
            self.upscaler_worker = UpscalerWorker(self.config)
            self.image_enhancer_worker = ImageEnhancerWorker(self.config)
            self.safety_checker_worker = SafetyCheckerWorker(self.config)
            
            # Initialize components
            components = [
                self.postprocessing_manager,
                self.upscaler_worker,
                self.image_enhancer_worker,
                self.safety_checker_worker
            ]
            
            for component in components:
                if not await component.initialize():
                    self.logger.error("Failed to initialize %s", component.__class__.__name__)
                    return False
                    
            self.initialized = True
            self.logger.info("Post-processing interface initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error("Post-processing interface initialization failed: %s", e)
            return False
    
    async def upscale_image(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process image upscaling request."""
        if not self.initialized:
            return {"success": False, "error": "Post-processing interface not initialized"}
        
        try:
            upscale_data = request.get("data", {})
            result = await self.upscaler_worker.process_upscaling(upscale_data)
            return {
                "success": True,
                "data": result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def enhance_image(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process image enhancement request."""
        if not self.initialized:
            return {"success": False, "error": "Post-processing interface not initialized"}
        
        try:
            enhance_data = request.get("data", {})
            result = await self.image_enhancer_worker.process_enhancement(enhance_data)
            return {
                "success": True,
                "data": result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def check_safety(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process safety checking request."""
        if not self.initialized:
            return {"success": False, "error": "Post-processing interface not initialized"}
        
        try:
            safety_data = request.get("data", {})
            result = await self.safety_checker_worker.process_safety_check(safety_data)
            return {
                "success": True,
                "data": result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def process_pipeline(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process a complete post-processing pipeline."""
        if not self.initialized:
            return {"success": False, "error": "Post-processing interface not initialized"}
        
        try:
            pipeline_data = request.get("data", {})
            result = await self.postprocessing_manager.process_pipeline(pipeline_data)
            return {
                "success": True,
                "data": result,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_available_upscalers(self) -> Dict[str, Any]:
        """Get list of available upscalers."""
        if not self.initialized:
            return {"success": False, "error": "Post-processing interface not initialized"}
        
        try:
            result = await self.upscaler_worker.get_available_upscalers()
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def get_enhancement_options(self) -> Dict[str, Any]:
        """Get available enhancement options."""
        if not self.initialized:
            return {"success": False, "error": "Post-processing interface not initialized"}
        
        try:
            result = await self.image_enhancer_worker.get_supported_enhancements()
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def get_safety_settings(self) -> Dict[str, Any]:
        """Get safety checker settings."""
        if not self.initialized:
            return {"success": False, "error": "Post-processing interface not initialized"}
        
        try:
            result = await self.safety_checker_worker.get_safety_statistics()
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def get_postprocessing_info(self) -> Dict[str, Any]:
        """Get post-processing capabilities information."""
        if not self.initialized:
            return {"success": False, "error": "Post-processing interface not initialized"}
        
        try:
            result = await self.postprocessing_manager.get_capabilities()
            return {
                "success": True,
                "data": result
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e)
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get post-processing interface status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            status = {
                "status": "healthy",
                "initialized": self.initialized,
                "components": {}
            }
            
            # Collect status from all components
            components = [
                ("postprocessing_manager", self.postprocessing_manager),
                ("upscaler_worker", self.upscaler_worker),
                ("image_enhancer_worker", self.image_enhancer_worker),
                ("safety_checker_worker", self.safety_checker_worker)
            ]
            
            for name, component in components:
                if component:
                    try:
                        status["components"][name] = await component.get_status()
                    except Exception as e:
                        status["components"][name] = {"error": str(e)}
                        
            return status
            
        except Exception as e:
            return {"status": "error", "error": str(e)}
    
    async def cleanup(self) -> None:
        """Clean up post-processing interface resources."""
        try:
            self.logger.info("Cleaning up post-processing interface...")
            
            # Cleanup components
            components = [
                self.safety_checker_worker,
                self.image_enhancer_worker,
                self.upscaler_worker,
                self.postprocessing_manager
            ]
            
            for component in components:
                if component:
                    try:
                        await component.cleanup()
                    except Exception as e:
                        self.logger.warning("Error during component cleanup: %s", e)
            
            self.initialized = False
            self.logger.info("Post-processing interface cleanup complete")
            
        except Exception as e:
            self.logger.error("Post-processing interface cleanup error: %s", e)


# Factory function for creating post-processing interface
def create_postprocessing_interface(config: Optional[Dict[str, Any]] = None) -> PostprocessingInterface:
    """
    Factory function to create a post-processing interface instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        PostprocessingInterface instance
    """
    return PostprocessingInterface(config or {})