"""
Model Interface for SDXL Workers System
======================================

Unified interface for model operations.
Consolidates model_loader.py, unified_model_manager.py, and gpu_model_manager.py.
"""

import logging
from typing import Dict, Any, Optional


class ModelInterface:
    """
    Unified interface for model operations and memory management.
    
    This interface provides a consistent API for model operations
    and delegates to appropriate managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(f"{__name__}.{self.__class__.__name__}")
        
        # Manager instances
        self.vae_manager = None
        self.encoder_manager = None
        self.unet_manager = None
        self.tokenizer_manager = None
        self.lora_manager = None
        self.memory_worker = None
        
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize model interface and managers."""
        try:
            self.logger.info("Initializing model interface...")
            
            # Import managers (lazy loading)
            from .managers.manager_vae import VAEManager
            from .managers.manager_encoder import EncoderManager
            from .managers.manager_unet import UNetManager
            from .managers.manager_tokenizer import TokenizerManager
            from .managers.manager_lora import LoRAManager
            from .workers.worker_memory import MemoryWorker
            
            # Create managers
            self.vae_manager = VAEManager(self.config)
            self.encoder_manager = EncoderManager(self.config)
            self.unet_manager = UNetManager(self.config)
            self.tokenizer_manager = TokenizerManager(self.config)
            self.lora_manager = LoRAManager(self.config)
            self.memory_worker = MemoryWorker(self.config)
            
            # Initialize managers
            managers = [
                self.vae_manager,
                self.encoder_manager,
                self.unet_manager,
                self.tokenizer_manager,
                self.lora_manager,
                self.memory_worker
            ]
            
            for manager in managers:
                if not await manager.initialize():
                    self.logger.error("Failed to initialize %s", manager.__class__.__name__)
                    return False
                    
            self.initialized = True
            self.logger.info("Model interface initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error("Model interface initialization failed: %s", e)
            return False
    
    async def load_model(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Load a model."""
        if not self.initialized:
            return {"success": False, "error": "Model interface not initialized"}
        
        try:
            model_data = request.get("data", {})
            result = await self.memory_worker.load_model(model_data)
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
    
    async def unload_model(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Unload a model."""
        if not self.initialized:
            return {"success": False, "error": "Model interface not initialized"}
        
        try:
            model_data = request.get("data", {})
            result = await self.memory_worker.unload_model(model_data)
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
    
    async def get_model_info(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Get model information."""
        if not self.initialized:
            return {"success": False, "error": "Model interface not initialized"}
        
        try:
            model_info = await self.memory_worker.get_model_info()
            return {
                "success": True,
                "data": model_info,
                "request_id": request.get("request_id", "")
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def optimize_memory(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Optimize memory usage."""
        if not self.initialized:
            return {"success": False, "error": "Model interface not initialized"}
        
        try:
            result = await self.memory_worker.optimize_memory()
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
    
    async def load_vae(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Load VAE model."""
        if not self.initialized:
            return {"success": False, "error": "Model interface not initialized"}
        
        try:
            vae_data = request.get("data", {})
            result = await self.vae_manager.load_vae(vae_data)
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
    
    async def load_lora(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Load LoRA adapter."""
        if not self.initialized:
            return {"success": False, "error": "Model interface not initialized"}
        
        try:
            lora_data = request.get("data", {})
            result = await self.lora_manager.load_lora(lora_data)
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
    
    async def load_encoder(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Load text encoder."""
        if not self.initialized:
            return {"success": False, "error": "Model interface not initialized"}
        
        try:
            encoder_data = request.get("data", {})
            result = await self.encoder_manager.load_encoder(encoder_data)
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
    
    async def load_unet(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Load UNet model."""
        if not self.initialized:
            return {"success": False, "error": "Model interface not initialized"}
        
        try:
            unet_data = request.get("data", {})
            result = await self.unet_manager.load_unet(unet_data)
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
    
    async def get_status(self) -> Dict[str, Any]:
        """Get model interface status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            status = {
                "status": "healthy",
                "initialized": self.initialized,
                "managers": {}
            }
            
            # Collect status from all managers
            managers = [
                ("vae", self.vae_manager),
                ("encoder", self.encoder_manager),
                ("unet", self.unet_manager),
                ("tokenizer", self.tokenizer_manager),
                ("lora", self.lora_manager),
                ("memory", self.memory_worker)
            ]
            
            for name, manager in managers:
                if manager:
                    try:
                        status["managers"][name] = await manager.get_status()
                    except Exception as e:
                        status["managers"][name] = {"error": str(e)}
                        
            return status
            
        except Exception as e:
            return {"status": "error", "error": str(e)}
    
    async def cleanup(self) -> None:
        """Clean up model interface resources."""
        try:
            self.logger.info("Cleaning up model interface...")
            
            # Cleanup managers
            managers = [
                self.memory_worker,
                self.lora_manager,
                self.tokenizer_manager,
                self.unet_manager,
                self.encoder_manager,
                self.vae_manager
            ]
            
            for manager in managers:
                if manager:
                    try:
                        await manager.cleanup()
                    except Exception as e:
                        self.logger.warning("Error during manager cleanup: %s", e)
            
            self.initialized = False
            self.logger.info("Model interface cleanup complete")
            
        except Exception as e:
            self.logger.error("Model interface cleanup error: %s", e)


# Factory function for creating model interface
def create_model_interface(config: Optional[Dict[str, Any]] = None) -> ModelInterface:
    """
    Factory function to create a model interface instance.
    
    Args:
        config: Optional configuration dictionary
        
    Returns:
        ModelInterface instance
    """
    return ModelInterface(config or {})