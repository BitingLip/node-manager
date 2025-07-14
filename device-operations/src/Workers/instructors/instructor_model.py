"""
Model Instructor for SDXL Workers System
========================================

Coordinates model management and optimization across different model types and devices.
Controls model managers through the model interface.
"""

import logging
from typing import Dict, Any, Optional
from .instructor_device import BaseInstructor


class ModelInstructor(BaseInstructor):
    """
    Model management and optimization coordinator.
    
    This instructor manages model loading, unloading, and optimization
    across different model types and devices by coordinating with model managers.
    """
    
    def __init__(self, config: Dict[str, Any]):
        super().__init__(config)
        self.model_interface = None
        
    async def initialize(self) -> bool:
        """Initialize model instructor and interface."""
        try:
            self.logger.info("Initializing model instructor...")
            
            # Import model interface (lazy loading)
            from ..model.interface_model import ModelInterface
            
            # Create model interface
            self.model_interface = ModelInterface(self.config)
            
            # Initialize interface
            if await self.model_interface.initialize():
                self.initialized = True
                self.logger.info("Model instructor initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize model interface")
                return False
                
        except Exception as e:
            self.logger.error(f"Model instructor initialization failed: {e}")
            return False
    
    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """Handle model-related requests with perfect C# endpoint alignment."""
        if not self.initialized:
            return {"success": False, "error": "Model instructor not initialized"}
        
        try:
            request_type = request.get("type", "")
            request_id = request.get("request_id", "")
            
            self.logger.info(f"Handling model request: {request_type}")
            
            # ALIGNED COMMANDS: Perfect 1:1 mapping with C# endpoints
            # Core model operations
            if request_type == "model.get_model":                    # Maps to GetModel
                return await self.model_interface.get_model(request)
            elif request_type == "model.post_model_load":            # Maps to PostModelLoad
                return await self.model_interface.post_model_load(request)
            elif request_type == "model.post_model_unload":          # Maps to PostModelUnload
                return await self.model_interface.post_model_unload(request)
            elif request_type == "model.delete_model":              # Maps to DeleteModel
                return await self.model_interface.delete_model(request)
            elif request_type == "model.get_model_status":          # Maps to GetModelStatus
                return await self.model_interface.get_model_status(request)
            
            # Model optimization and validation
            elif request_type == "model.post_model_optimize":       # Maps to PostModelOptimize
                return await self.model_interface.post_model_optimize(request)
            elif request_type == "model.post_model_validate":       # Maps to PostModelValidate
                return await self.model_interface.post_model_validate(request)
            elif request_type == "model.post_model_benchmark":      # Maps to PostModelBenchmark
                return await self.model_interface.post_model_benchmark(request)
            elif request_type == "model.get_model_benchmark_results": # Maps to GetModelBenchmarkResults
                return await self.model_interface.get_model_benchmark_results(request)
            
            # Model metadata operations
            elif request_type == "model.get_model_metadata":        # Maps to GetModelMetadata
                return await self.model_interface.get_model_metadata(request)
            elif request_type == "model.put_model_metadata":        # Maps to PutModelMetadata
                return await self.model_interface.put_model_metadata(request)
            elif request_type == "model.get_model_config":          # Maps to GetModelConfig
                return await self.model_interface.get_model_config(request)
            elif request_type == "model.post_model_config_update":  # Maps to PostModelConfigUpdate
                return await self.model_interface.post_model_config_update(request)
            
            # Model conversion and processing
            elif request_type == "model.post_model_convert":        # Maps to PostModelConvert
                return await self.model_interface.post_model_convert(request)
            elif request_type == "model.post_model_preload":        # Maps to PostModelPreload
                return await self.model_interface.post_model_preload(request)
            elif request_type == "model.post_model_share":          # Maps to PostModelShare
                return await self.model_interface.post_model_share(request)
            
            # Cache and VRAM operations
            elif request_type == "model.get_model_cache":           # Maps to GetModelCache
                return await self.model_interface.get_model_cache(request)
            elif request_type == "model.post_model_cache":          # Maps to PostModelCache
                return await self.model_interface.post_model_cache(request)
            elif request_type == "model.delete_model_cache":        # Maps to DeleteModelCache
                return await self.model_interface.delete_model_cache(request)
            elif request_type == "model.post_model_vram_load":      # Maps to PostModelVramLoad
                return await self.model_interface.post_model_vram_load(request)
            elif request_type == "model.delete_model_vram_unload":  # Maps to DeleteModelVramUnload
                return await self.model_interface.delete_model_vram_unload(request)
            
            # Discovery and availability operations
            elif request_type == "model.get_available_models":      # Maps to GetAvailableModels
                return await self.model_interface.get_available_models(request)
            elif request_type == "model.get_model_components":      # Maps to GetModelComponents
                return await self.model_interface.get_model_components(request)
            
            else:
                return {
                    "success": False,
                    "error": f"Unknown model request type: {request_type}",
                    "request_id": request_id
                }
                
        except Exception as e:
            self.logger.error(f"Model request handling failed: {e}")
            return {
                "success": False,
                "error": str(e),
                "request_id": request.get("request_id", "")
            }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get model instructor status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            # Get status from model interface
            if self.model_interface:
                interface_status = await self.model_interface.get_status()
                return {
                    "status": "healthy",
                    "initialized": self.initialized,
                    "interface": interface_status
                }
            else:
                return {"status": "interface_not_available"}
                
        except Exception as e:
            return {"status": "error", "error": str(e)}
    
    async def cleanup(self) -> None:
        """Clean up model instructor resources."""
        try:
            self.logger.info("Cleaning up model instructor...")
            
            if self.model_interface:
                await self.model_interface.cleanup()
                self.model_interface = None
            
            self.initialized = False
            self.logger.info("Model instructor cleanup complete")
            
        except Exception as e:
            self.logger.error(f"Model instructor cleanup error: {e}")