"""
Memory Instructor for SDXL Workers System
==========================================

Coordinates memory management and optimization for DirectML, CUDA, and CPU backends.
Handles C# ServiceMemory requests and coordinates with memory interface.
Based on Memory Domain Phase 4 Implementation Plan.
"""

import asyncio
import json
import logging
from typing import Dict, Any, Optional, TYPE_CHECKING
from .instructor_device import BaseInstructor

if TYPE_CHECKING:
    from ..memory.interface_memory import MemoryInterface


class MemoryInstructor(BaseInstructor):
    """
    Memory operation coordination instructor
    Handles C# ServiceMemory requests and coordinates with memory interface
    """
    
    def __init__(self, config: Dict[str, Any]):
        super().__init__(config)
        self.memory_interface: Optional["MemoryInterface"] = None
        self.worker_type = "memory"
        
    async def initialize(self) -> bool:
        """Initialize memory instructor and interface"""
        try:
            self.logger.info("Initializing memory instructor...")
            
            # Import memory interface (lazy loading)
            from ..memory.interface_memory import MemoryInterface
            
            # Create memory interface
            self.memory_interface = MemoryInterface(self.config)
            
            # Initialize interface
            if await self.memory_interface.initialize():
                self.initialized = True
                self.logger.info("Memory instructor initialized successfully")
                return True
            else:
                self.logger.error("Failed to initialize memory interface")
                self.memory_interface = None
                return False
                
        except Exception as e:
            self.logger.error("Memory instructor initialization error: %s", e)
            self.memory_interface = None
            return False

    def _check_interface(self, request_id: str) -> Optional[Dict[str, Any]]:
        """Check if memory interface is available, return error response if not."""
        if self.memory_interface is None:
            return self.create_error_response(
                request_id, 
                "INTERFACE_NOT_AVAILABLE", 
                "Memory interface not available"
            )
        return None

    async def handle_request(self, request: Dict[str, Any]) -> Dict[str, Any]:
        """
        Handle memory operation requests from C# ServiceMemory
        Based on Phase 2 Command Mapping Analysis: 15 memory operations
        """
        if not self.initialized:
            return self.create_error_response("", "INSTRUCTOR_NOT_INITIALIZED", "Memory instructor not initialized")
            
        try:
            action = request.get("action", "")
            request_id = request.get("request_id", "")
            data = request.get("data", {})
            
            self.logger.info("Handling memory request: %s", action)
            
            # Memory Status Operations (Phase 1: 87% aligned)
            if action == "memory.get_status":
                return await self.memory_status(request_id, data)
            elif action == "memory.get_usage":
                return await self.memory_usage(request_id, data)
            elif action == "memory.get_allocations":
                return await self.memory_allocations(request_id, data)
                
            # Memory Allocation Operations (Phase 1: Coordination gaps identified)
            elif action == "memory.allocate":
                return await self.memory_allocate(request_id, data)
            elif action == "memory.deallocate":
                return await self.memory_deallocate(request_id, data)
                
            # Memory Management Operations (Phase 1: Missing Python implementations)
            elif action == "memory.clear":
                return await self.memory_clear(request_id, data)
            elif action == "memory.defragment":
                return await self.memory_defragment(request_id, data)
            elif action == "memory.transfer":
                return await self.memory_transfer(request_id, data)
            elif action == "memory.get_transfer":
                return await self.memory_get_transfer(request_id, data)
                
            # Memory Optimization Operations (Phase 1: Partial coordination)
            elif action == "memory.optimize":
                return await self.memory_optimize(request_id, data)
            elif action == "memory.model_status":
                return await self.memory_model_status(request_id, data)
            elif action == "memory.model_optimize":
                return await self.memory_model_optimize(request_id, data)
                
            # Memory Analytics Operations (Phase 1: Missing implementations)
            elif action == "memory.get_pressure":
                return await self.memory_pressure(request_id, data)
            elif action == "memory.analytics":
                return await self.memory_analytics(request_id, data)
            elif action == "memory.optimization_recs":
                return await self.memory_optimization_recommendations(request_id, data)
                
            else:
                return self.create_error_response(
                    request_id, "UNKNOWN_ACTION", f"Unknown memory action: {action}"
                )
                
        except Exception as e:
            self.logger.error("Memory instructor error: %s", e)
            return self.create_error_response(
                request_id, "INSTRUCTOR_ERROR", f"Memory instructor error: {str(e)}"
            )

    # Memory Status Operations Implementation
    async def memory_status(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Get memory status - coordinates C# DirectML status with Python PyTorch status"""
        # Check if interface is available
        error_response = self._check_interface(request_id)
        if error_response:
            return error_response
            
        assert self.memory_interface is not None  # For type checker
        
        try:
            device_id = data.get("device_id")
            include_allocations = data.get("include_allocations", True)
            include_usage_stats = data.get("include_usage_stats", True)
            include_fragmentation = data.get("include_fragmentation", False)
            
            result = await self.memory_interface.get_memory_status(
                device_id, include_allocations, include_usage_stats, include_fragmentation
            )
            
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_STATUS_ERROR", f"Memory status error: {str(e)}"
            )

    async def memory_usage(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Get memory usage information"""
        # Check if interface is available
        error_response = self._check_interface(request_id)
        if error_response:
            return error_response
            
        assert self.memory_interface is not None  # For type checker
        
        try:
            device_id = data.get("device_id")
            result = await self.memory_interface.get_memory_usage(device_id)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_USAGE_ERROR", f"Memory usage error: {str(e)}"
            )

    async def memory_allocations(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Get memory allocations information"""
        try:
            device_id = data.get("device_id")
            result = await self.memory_interface.get_memory_allocations(device_id)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_ALLOCATIONS_ERROR", f"Memory allocations error: {str(e)}"
            )

    async def memory_allocate(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Allocate memory - coordinates with C# DirectML allocation tracking"""
        try:
            device_id = data.get("device_id")
            size_bytes = data.get("size_bytes")
            allocation_type = data.get("allocation_type")
            alignment = data.get("alignment", 0)
            purpose = data.get("purpose", "")
            persistent = data.get("persistent", False)
            
            result = await self.memory_interface.allocate_memory(
                device_id, size_bytes, allocation_type, alignment, purpose, persistent
            )
            
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_ALLOCATE_ERROR", f"Memory allocation error: {str(e)}"
            )

    async def memory_deallocate(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Deallocate memory"""
        try:
            allocation_id = data.get("allocation_id")
            device_id = data.get("device_id")
            force = data.get("force", False)
            
            result = await self.memory_interface.deallocate_memory(allocation_id, device_id, force)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_DEALLOCATE_ERROR", f"Memory deallocation error: {str(e)}"
            )

    async def memory_clear(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Clear memory"""
        try:
            device_id = data.get("device_id")
            clear_type = data.get("clear_type", "all")
            
            result = await self.memory_interface.clear_memory(device_id, clear_type)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_CLEAR_ERROR", f"Memory clear error: {str(e)}"
            )

    async def memory_defragment(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Defragment memory"""
        try:
            device_id = data.get("device_id")
            aggressive = data.get("aggressive", False)
            
            result = await self.memory_interface.defragment_memory(device_id, aggressive)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_DEFRAGMENT_ERROR", f"Memory defragmentation error: {str(e)}"
            )

    async def memory_transfer(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Transfer memory between devices"""
        try:
            source_allocation_id = data.get("source_allocation_id")
            destination_device_id = data.get("destination_device_id")
            transfer_type = data.get("transfer_type", "copy")
            
            result = await self.memory_interface.transfer_memory(
                source_allocation_id, destination_device_id, transfer_type
            )
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_TRANSFER_ERROR", f"Memory transfer error: {str(e)}"
            )

    async def memory_get_transfer(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Get memory transfer status"""
        try:
            transfer_id = data.get("transfer_id")
            result = await self.memory_interface.get_transfer_status(transfer_id)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_GET_TRANSFER_ERROR", f"Memory transfer status error: {str(e)}"
            )

    async def memory_optimize(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Optimize memory usage"""
        try:
            device_id = data.get("device_id")
            optimization_level = data.get("optimization_level", "normal")
            
            result = await self.memory_interface.optimize_memory(device_id, optimization_level)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_OPTIMIZE_ERROR", f"Memory optimization error: {str(e)}"
            )

    async def memory_model_status(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Get model memory status"""
        try:
            device_id = data.get("device_id")
            result = await self.memory_interface.get_model_memory_status(device_id)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_MODEL_STATUS_ERROR", f"Model memory status error: {str(e)}"
            )

    async def memory_model_optimize(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Optimize model memory usage"""
        try:
            device_id = data.get("device_id")
            optimization_type = data.get("optimization_type", "standard")
            target_usage_percentage = data.get("target_usage_percentage", 70)
            force = data.get("force", False)
            
            result = await self.memory_interface.optimize_model_memory(
                device_id, optimization_type, target_usage_percentage, force
            )
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_MODEL_OPTIMIZE_ERROR", f"Model memory optimization error: {str(e)}"
            )

    async def memory_pressure(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Get memory pressure information"""
        try:
            device_id = data.get("device_id")
            result = await self.memory_interface.get_memory_pressure(device_id)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_PRESSURE_ERROR", f"Memory pressure error: {str(e)}"
            )

    async def memory_analytics(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Get memory analytics information"""
        try:
            device_id = data.get("device_id")
            time_range = data.get("time_range", "1h")
            
            result = await self.memory_interface.get_memory_analytics(device_id, time_range)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_ANALYTICS_ERROR", f"Memory analytics error: {str(e)}"
            )

    async def memory_optimization_recommendations(self, request_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
        """Get memory optimization recommendations"""
        try:
            device_id = data.get("device_id")
            result = await self.memory_interface.get_optimization_recommendations(device_id)
            return self.create_success_response(request_id, result)
        except Exception as e:
            return self.create_error_response(
                request_id, "MEMORY_OPTIMIZATION_RECS_ERROR", f"Memory optimization recommendations error: {str(e)}"
            )

    def create_success_response(self, request_id: str, data: Any) -> Dict[str, Any]:
        """Create standardized success response"""
        return {
            "success": True,
            "request_id": request_id,
            "data": data,
            "timestamp": asyncio.get_event_loop().time()
        }

    def create_error_response(self, request_id: str, error_code: str, error_message: str) -> Dict[str, Any]:
        """Create standardized error response"""
        return {
            "success": False,
            "request_id": request_id,
            "error_code": error_code,
            "error_message": error_message,
            "timestamp": asyncio.get_event_loop().time()
        }

    async def get_status(self) -> Dict[str, Any]:
        """Get memory instructor status."""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            # Get status from memory interface
            if self.memory_interface:
                interface_status = await self.memory_interface.get_status()
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
        """Clean up memory instructor resources."""
        try:
            self.logger.info("Cleaning up memory instructor...")
            
            if self.memory_interface:
                await self.memory_interface.cleanup()
                self.memory_interface = None
            
            self.initialized = False
            self.logger.info("Memory instructor cleanup complete")
            
        except Exception as e:
            self.logger.error("Memory instructor cleanup error: %s", e)