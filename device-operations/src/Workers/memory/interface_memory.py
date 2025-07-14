"""
Memory Interface for SDXL Workers System
=========================================

Memory operation integration layer that coordinates between instructor and specialized memory workers.
Bridges C# DirectML allocation with Python PyTorch memory management.
Based on Memory Domain Phase 4 Implementation Plan.
"""

import asyncio
import logging
from typing import Dict, Any, List, Optional
from datetime import datetime


class MemoryInterface:
    """
    Memory operation integration layer
    Coordinates between instructor and specialized memory workers
    Bridges C# DirectML allocation with Python PyTorch memory management
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.memory_manager = None
        self.allocation_worker = None
        self.transfer_worker = None
        self.analytics_worker = None
        self.model_memory_worker = None
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize all memory workers and coordination systems"""
        try:
            self.logger.info("Initializing memory interface...")
            
            # Import and initialize memory manager
            from .managers.manager_memory import MemoryManager
            self.memory_manager = MemoryManager(self.config)
            if not await self.memory_manager.initialize():
                self.logger.error("Failed to initialize memory manager")
                return False
                
            # Import and initialize allocation worker
            from .workers.worker_allocation import AllocationWorker
            self.allocation_worker = AllocationWorker(self.config)
            if not await self.allocation_worker.initialize():
                self.logger.error("Failed to initialize allocation worker")
                return False
                
            # Import and initialize transfer worker  
            from .workers.worker_transfer import TransferWorker
            self.transfer_worker = TransferWorker(self.config)
            if not await self.transfer_worker.initialize():
                self.logger.error("Failed to initialize transfer worker")
                return False
                
            # Import and initialize analytics worker
            from .workers.worker_analytics import AnalyticsWorker
            self.analytics_worker = AnalyticsWorker(self.config)
            if not await self.analytics_worker.initialize():
                self.logger.error("Failed to initialize analytics worker")
                return False
                
            # Initialize existing model memory worker
            from ..model.workers.worker_memory import MemoryWorker
            self.model_memory_worker = MemoryWorker(self.config)
            if not await self.model_memory_worker.initialize():
                self.logger.error("Failed to initialize model memory worker")
                return False
                
            self.initialized = True
            self.logger.info("Memory interface initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error("Memory interface initialization error: %s", e)
            return False

    async def get_memory_status(self, device_id: Optional[str], include_allocations: bool, 
                               include_usage_stats: bool, include_fragmentation: bool) -> Dict[str, Any]:
        """
        Get comprehensive memory status
        Coordinates C# DirectML status with Python PyTorch memory information
        """
        try:
            # Get allocation information from allocation worker
            allocations = []
            if include_allocations:
                allocations = await self.allocation_worker.get_allocations(device_id)
            
            # Get usage statistics from analytics worker
            usage_stats = {}
            if include_usage_stats:
                usage_stats = await self.analytics_worker.get_usage_statistics(device_id)
            
            # Get fragmentation information
            fragmentation = {}
            if include_fragmentation:
                fragmentation = await self.analytics_worker.get_fragmentation_info(device_id)
            
            # Get model memory status from existing worker
            model_memory_status = await self.model_memory_worker.get_status()
            
            # Combine all memory information
            memory_status = {
                "device_id": device_id or "all",
                "allocations": allocations,
                "usage_stats": usage_stats,
                "fragmentation": fragmentation,
                "model_memory": model_memory_status,
                "timestamp": datetime.now().isoformat()
            }
            
            return memory_status
            
        except Exception as e:
            raise Exception(f"Memory status retrieval error: {str(e)}")

    async def get_memory_usage(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Get memory usage information"""
        try:
            return await self.analytics_worker.get_memory_usage(device_id)
        except Exception as e:
            raise Exception(f"Memory usage retrieval error: {str(e)}")

    async def get_memory_allocations(self, device_id: Optional[str]) -> List[Dict[str, Any]]:
        """Get memory allocations information"""
        try:
            return await self.allocation_worker.get_allocations(device_id)
        except Exception as e:
            raise Exception(f"Memory allocations retrieval error: {str(e)}")

    async def allocate_memory(self, device_id: str, size_bytes: int, allocation_type: str,
                             alignment: int, purpose: str, persistent: bool) -> Dict[str, Any]:
        """
        Allocate memory with coordination between C# and Python layers
        Based on Phase 1 finding: Coordination gap between C# DirectML and Python PyTorch
        """
        try:
            # Coordinate with allocation worker for memory allocation
            allocation_result = await self.allocation_worker.allocate(
                device_id, size_bytes, allocation_type, alignment, purpose, persistent
            )
            
            # Update memory manager with allocation tracking
            await self.memory_manager.track_allocation(allocation_result)
            
            # Update analytics with allocation event
            await self.analytics_worker.record_allocation_event(allocation_result)
            
            return allocation_result
            
        except Exception as e:
            raise Exception(f"Memory allocation error: {str(e)}")

    async def deallocate_memory(self, allocation_id: str, device_id: Optional[str], force: bool) -> Dict[str, Any]:
        """Deallocate memory"""
        try:
            # Deallocate through allocation worker
            deallocation_result = await self.allocation_worker.deallocate(allocation_id, device_id, force)
            
            # Update memory manager with deallocation tracking
            await self.memory_manager.untrack_allocation(allocation_id)
            
            # Update analytics with deallocation event
            await self.analytics_worker.record_deallocation_event(allocation_id, deallocation_result)
            
            return deallocation_result
            
        except Exception as e:
            raise Exception(f"Memory deallocation error: {str(e)}")

    async def clear_memory(self, device_id: Optional[str], clear_type: str) -> Dict[str, Any]:
        """Clear memory"""
        try:
            # Clear memory through allocation worker
            clear_result = await self.allocation_worker.clear_memory(device_id, clear_type)
            
            # Update memory manager with clear event
            await self.memory_manager.clear_tracked_allocations(device_id, clear_type)
            
            # Update analytics with clear event
            await self.analytics_worker.record_clear_event(device_id, clear_type, clear_result)
            
            return clear_result
            
        except Exception as e:
            raise Exception(f"Memory clear error: {str(e)}")

    async def defragment_memory(self, device_id: Optional[str], aggressive: bool) -> Dict[str, Any]:
        """Defragment memory"""
        try:
            return await self.allocation_worker.defragment_memory(device_id, aggressive)
        except Exception as e:
            raise Exception(f"Memory defragmentation error: {str(e)}")

    async def transfer_memory(self, source_allocation_id: str, destination_device_id: str, 
                             transfer_type: str) -> Dict[str, Any]:
        """Transfer memory between devices"""
        try:
            # Execute transfer through transfer worker
            transfer_result = await self.transfer_worker.transfer_memory(
                source_allocation_id, destination_device_id, transfer_type
            )
            
            # Update memory manager with transfer tracking
            await self.memory_manager.track_transfer(transfer_result)
            
            # Update analytics with transfer event
            await self.analytics_worker.record_transfer_event(transfer_result)
            
            return transfer_result
            
        except Exception as e:
            raise Exception(f"Memory transfer error: {str(e)}")

    async def get_transfer_status(self, transfer_id: str) -> Dict[str, Any]:
        """Get memory transfer status"""
        try:
            return await self.transfer_worker.get_transfer_status(transfer_id)
        except Exception as e:
            raise Exception(f"Memory transfer status error: {str(e)}")

    async def optimize_memory(self, device_id: Optional[str], optimization_level: str) -> Dict[str, Any]:
        """Optimize memory usage"""
        try:
            # Get optimization recommendations from analytics worker
            recommendations = await self.analytics_worker.get_optimization_recommendations(device_id)
            
            # Execute optimizations through allocation worker
            optimization_result = await self.allocation_worker.optimize_memory(
                device_id, optimization_level, recommendations
            )
            
            # Update analytics with optimization event
            await self.analytics_worker.record_optimization_event(device_id, optimization_result)
            
            return optimization_result
            
        except Exception as e:
            raise Exception(f"Memory optimization error: {str(e)}")

    async def get_model_memory_status(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Get model memory status"""
        try:
            # Get status from model memory worker
            model_status = await self.model_memory_worker.get_status()
            
            # Enhance with device-specific information if requested
            if device_id:
                device_allocations = await self.allocation_worker.get_allocations(device_id)
                model_allocations = [alloc for alloc in device_allocations if alloc.get("purpose", "").startswith("model")]
                model_status["device_allocations"] = model_allocations
            
            return model_status
            
        except Exception as e:
            raise Exception(f"Model memory status error: {str(e)}")

    async def optimize_model_memory(self, device_id: Optional[str], optimization_type: str, 
                                   target_usage_percentage: int, force: bool) -> Dict[str, Any]:
        """Optimize model memory usage"""
        try:
            # Execute model memory optimization
            optimization_result = await self.model_memory_worker.optimize_memory()
            
            # If optimization was successful and we have specific targets, coordinate with allocation worker
            if optimization_result.get("optimized", False) and device_id:
                # Check if we need additional optimization beyond model level
                current_usage = await self.analytics_worker.get_memory_usage(device_id)
                usage_percentage = current_usage.get("usage_percentage", 0)
                
                if usage_percentage > target_usage_percentage and force:
                    # Execute additional optimization through allocation worker
                    additional_optimization = await self.allocation_worker.optimize_memory(
                        device_id, "aggressive", {}
                    )
                    optimization_result["additional_optimization"] = additional_optimization
            
            # Enhance result with current memory status
            optimization_result.update({
                "device_id": device_id,
                "optimization_type": optimization_type,
                "target_usage_percentage": target_usage_percentage,
                "timestamp": datetime.now().isoformat()
            })
            
            return optimization_result
            
        except Exception as e:
            raise Exception(f"Model memory optimization error: {str(e)}")

    async def get_memory_pressure(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Get memory pressure information"""
        try:
            return await self.analytics_worker.get_memory_pressure(device_id)
        except Exception as e:
            raise Exception(f"Memory pressure error: {str(e)}")

    async def get_memory_analytics(self, device_id: Optional[str], time_range: str) -> Dict[str, Any]:
        """Get memory analytics information"""
        try:
            return await self.analytics_worker.get_memory_analytics(device_id, time_range)
        except Exception as e:
            raise Exception(f"Memory analytics error: {str(e)}")

    async def get_optimization_recommendations(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Get memory optimization recommendations"""
        try:
            return await self.analytics_worker.get_optimization_recommendations(device_id)
        except Exception as e:
            raise Exception(f"Memory optimization recommendations error: {str(e)}")

    async def get_status(self) -> Dict[str, Any]:
        """Get memory interface status"""
        if not self.initialized:
            return {"status": "not_initialized"}
        
        try:
            status = {
                "status": "healthy",
                "initialized": self.initialized,
                "components": {}
            }
            
            # Get status from all components
            if self.memory_manager:
                status["components"]["memory_manager"] = await self.memory_manager.get_status()
            
            if self.allocation_worker:
                status["components"]["allocation_worker"] = await self.allocation_worker.get_status()
            
            if self.transfer_worker:
                status["components"]["transfer_worker"] = await self.transfer_worker.get_status()
            
            if self.analytics_worker:
                status["components"]["analytics_worker"] = await self.analytics_worker.get_status()
            
            if self.model_memory_worker:
                status["components"]["model_memory_worker"] = await self.model_memory_worker.get_status()
            
            return status
            
        except Exception as e:
            return {"status": "error", "error": str(e)}

    async def cleanup(self) -> None:
        """Clean up memory interface resources"""
        try:
            self.logger.info("Cleaning up memory interface...")
            
            # Cleanup all components
            if self.model_memory_worker:
                await self.model_memory_worker.cleanup()
                self.model_memory_worker = None
            
            if self.analytics_worker:
                await self.analytics_worker.cleanup()
                self.analytics_worker = None
            
            if self.transfer_worker:
                await self.transfer_worker.cleanup()
                self.transfer_worker = None
            
            if self.allocation_worker:
                await self.allocation_worker.cleanup()
                self.allocation_worker = None
            
            if self.memory_manager:
                await self.memory_manager.cleanup()
                self.memory_manager = None
            
            self.initialized = False
            self.logger.info("Memory interface cleanup complete")
            
        except Exception as e:
            self.logger.error("Memory interface cleanup error: %s", e)