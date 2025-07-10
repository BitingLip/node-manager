"""
Conditioning Interface for SDXL Workers System
==============================================

Unified interface for conditioning operations.
"""

import logging
from typing import Dict, Any, Optional


class ConditioningInterface:
    """
    Unified interface for conditioning operations.
    
    This interface provides a consistent API for conditioning operations
    and delegates to appropriate managers and workers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(f"{__name__}.{self.__class__.__name__}")
        
        # Manager and worker instances
        self.conditioning_manager = None
        self.prompt_processor_worker = None
        self.controlnet_worker = None
        self.img2img_worker = None
        
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize conditioning interface and components."""
        try:
            self.logger.info("Initializing conditioning interface...")
            
            # Import components (lazy loading)
            from .managers.manager_conditioning import ConditioningManager
            from .workers.worker_prompt_processor import PromptProcessorWorker
            from .workers.worker_controlnet import ControlNetWorker
            from .workers.worker_img2img import Img2ImgWorker
            
            # Create components
            self.conditioning_manager = ConditioningManager(self.config)
            self.prompt_processor_worker = PromptProcessorWorker(self.config)
            self.controlnet_worker = ControlNetWorker(self.config)
            self.img2img_worker = Img2ImgWorker(self.config)
            
            # Initialize components
            components = [
                self.conditioning_manager,
                self.prompt_processor_worker,
                self.controlnet_worker,
                self.img2img_worker
            ]
            
            for component in components:
                if not await component.initialize():
                    self.logger.error("Failed to initialize %s", component.__class__.__name__)
                    return False
                    
            self.initialized = True
            self.logger.info("Conditioning interface initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error("Conditioning interface initialization failed: %s", e)
            return False
    
    async def process_prompt(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process text prompt conditioning."""
        if not self.initialized:
            return {"success": False, "error": "Conditioning interface not initialized"}
        
        try:
            prompt_data = request.get("data", {})
            result = await self.prompt_processor_worker.process_prompt(prompt_data)
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
    
    async def process_controlnet(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process ControlNet conditioning."""
        if not self.initialized:
            return {"success": False, "error": "Conditioning interface not initialized"}
        
        try:
            controlnet_data = request.get("data", {})
            result = await self.controlnet_worker.process_controlnet(controlnet_data)
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
    
    async def process_img2img(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Process image-to-image conditioning."""
        if not self.initialized:
            return {"success": False, "error": "Conditioning interface not initialized"}
        
        try:
            img2img_data = request.get("data", {})
            result = await self.img2img_worker.process_img2img(img2img_data)
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
    
    async def get_conditioning_info(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get conditioning information."""
        if not self.initialized:
            return {"success": False, "error": "Conditioning interface not initialized"}
        
        try:
            info = await self.conditioning_manager.get_conditioning_info()
            return {
                "success": True,
                "data": info,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get conditioning interface status."""
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
                ("conditioning_manager", self.conditioning_manager),
                ("prompt_processor", self.prompt_processor_worker),
                ("controlnet", self.controlnet_worker),
                ("img2img", self.img2img_worker)
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
        """Clean up conditioning interface resources."""
        try:
            self.logger.info("Cleaning up conditioning interface...")
            
            # Cleanup components
            components = [
                self.img2img_worker,
                self.controlnet_worker,
                self.prompt_processor_worker,
                self.conditioning_manager
            ]
            
            for component in components:
                if component:
                    try:
                        await component.cleanup()
                    except Exception as e:
                        self.logger.warning("Error during component cleanup: %s", e)
            
            self.initialized = False
            self.logger.info("Conditioning interface cleanup complete")
            
        except Exception as e:
            self.logger.error("Conditioning interface cleanup error: %s", e)


# Factory function for creating conditioning interface
def create_conditioning_interface(config: Optional[Dict[str, Any]] = None) -> ConditioningInterface:
    """
    Factory function to create a conditioning interface instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        ConditioningInterface instance
    """
    return ConditioningInterface(config or {})