"""
Allocation Worker for SDXL Workers System
==========================================

Handles memory allocation operations for coordination between C# DirectML and Python PyTorch.
Based on Memory Domain Phase 4 Implementation Plan.
"""

import asyncio
import logging
from typing import Dict, Any, List, Optional
from datetime import datetime
import uuid


class AllocationWorker:
    """
    Memory allocation operations worker
    Handles allocation, deallocation, and memory management operations
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.allocation_pool = {}  # For allocation pooling optimization
        self.common_sizes = [1024*1024, 4*1024*1024, 16*1024*1024, 64*1024*1024]  # 1MB, 4MB, 16MB, 64MB
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize allocation worker"""
        try:
            self.logger.info("Initializing allocation worker...")
            
            # Initialize allocation pools for common sizes
            await self._initialize_allocation_pools()
            
            self.initialized = True
            self.logger.info("Allocation worker initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error("Allocation worker initialization error: %s", e)
            return False

    async def allocate(self, device_id: str, size_bytes: int, allocation_type: str,
                      alignment: int, purpose: str, persistent: bool) -> Dict[str, Any]:
        """
        Allocate memory with optimization strategies
        Based on Phase 3 optimization: Allocation pooling for common sizes
        """
        try:
            # Generate allocation ID
            allocation_id = str(uuid.uuid4())
            
            # Check if we can use pooled allocation for efficiency
            pooled_allocation = await self._get_pooled_allocation(device_id, size_bytes)
            if pooled_allocation:
                self.logger.info("Using pooled allocation for %d bytes on device %s", size_bytes, device_id)
                allocation_id = pooled_allocation["allocation_id"]
                actual_size = pooled_allocation["size_bytes"]
            else:
                # Create new allocation
                actual_size = size_bytes
                
                # Apply alignment if specified
                if alignment > 0:
                    aligned_size = ((size_bytes + alignment - 1) // alignment) * alignment
                    actual_size = aligned_size
                
                self.logger.info("Creating new allocation: %d bytes on device %s", actual_size, device_id)
            
            # Simulate allocation process
            # In a real implementation, this would interface with actual memory allocation APIs
            allocation_success = await self._simulate_allocation(device_id, actual_size, allocation_type)
            
            if allocation_success:
                allocation_result = {
                    "allocation_id": allocation_id,
                    "device_id": device_id,
                    "size_bytes": actual_size,
                    "requested_size_bytes": size_bytes,
                    "allocation_type": allocation_type,
                    "alignment": alignment,
                    "purpose": purpose,
                    "persistent": persistent,
                    "status": "active",
                    "timestamp": datetime.now().isoformat(),
                    "pooled": pooled_allocation is not None
                }
                
                self.logger.info("Successfully allocated %d bytes (ID: %s) on device %s", 
                               actual_size, allocation_id, device_id)
                return allocation_result
            else:
                raise Exception(f"Failed to allocate {actual_size} bytes on device {device_id}")
            
        except Exception as e:
            self.logger.error("Memory allocation error: %s", e)
            raise

    async def deallocate(self, allocation_id: str, device_id: Optional[str], force: bool) -> Dict[str, Any]:
        """Deallocate memory allocation"""
        try:
            # Check if allocation can be returned to pool for reuse
            pooled = await self._return_allocation_to_pool(allocation_id, device_id)
            
            if not pooled:
                # Perform actual deallocation
                deallocation_success = await self._simulate_deallocation(allocation_id, device_id, force)
                
                if not deallocation_success and not force:
                    raise Exception(f"Failed to deallocate allocation {allocation_id}")
            
            deallocation_result = {
                "allocation_id": allocation_id,
                "device_id": device_id,
                "deallocated": True,
                "pooled": pooled,
                "force": force,
                "timestamp": datetime.now().isoformat()
            }
            
            self.logger.info("Successfully deallocated allocation %s%s", 
                           allocation_id, " (returned to pool)" if pooled else "")
            return deallocation_result
            
        except Exception as e:
            self.logger.error("Memory deallocation error: %s", e)
            raise

    async def get_allocations(self, device_id: Optional[str]) -> List[Dict[str, Any]]:
        """Get current memory allocations"""
        try:
            # In a real implementation, this would query actual allocation tracking
            # For now, return simulated allocation data
            allocations = await self._get_simulated_allocations(device_id)
            
            self.logger.debug("Retrieved %d allocations for device %s", 
                            len(allocations), device_id or "all")
            return allocations
            
        except Exception as e:
            self.logger.error("Get allocations error: %s", e)
            raise

    async def clear_memory(self, device_id: Optional[str], clear_type: str) -> Dict[str, Any]:
        """Clear memory based on device and type"""
        try:
            cleared_count = 0
            freed_bytes = 0
            
            # Simulate memory clearing based on clear_type
            if clear_type == "all":
                result = await self._simulate_clear_all_memory(device_id)
            elif clear_type == "non_persistent":
                result = await self._simulate_clear_non_persistent_memory(device_id)
            elif clear_type == "temporary":
                result = await self._simulate_clear_temporary_memory(device_id)
            else:
                raise Exception(f"Unknown clear type: {clear_type}")
            
            clear_result = {
                "device_id": device_id,
                "clear_type": clear_type,
                "cleared_count": result["cleared_count"],
                "freed_bytes": result["freed_bytes"],
                "timestamp": datetime.now().isoformat()
            }
            
            self.logger.info("Cleared %d allocations (%d bytes) on device %s", 
                           result["cleared_count"], result["freed_bytes"], device_id or "all")
            return clear_result
            
        except Exception as e:
            self.logger.error("Clear memory error: %s", e)
            raise

    async def defragment_memory(self, device_id: Optional[str], aggressive: bool) -> Dict[str, Any]:
        """Defragment memory to reduce fragmentation"""
        try:
            # Simulate memory defragmentation
            defrag_result = await self._simulate_defragmentation(device_id, aggressive)
            
            result = {
                "device_id": device_id,
                "aggressive": aggressive,
                "defragmented": defrag_result["success"],
                "freed_bytes": defrag_result["freed_bytes"],
                "compacted_allocations": defrag_result["compacted_allocations"],
                "fragmentation_reduction_percentage": defrag_result["fragmentation_reduction"],
                "duration_seconds": defrag_result["duration_seconds"],
                "timestamp": datetime.now().isoformat()
            }
            
            self.logger.info("Defragmentation on device %s: %s (%d bytes freed, %.1f%% fragmentation reduction)", 
                           device_id or "all", 
                           "successful" if result["defragmented"] else "failed",
                           result["freed_bytes"], 
                           result["fragmentation_reduction_percentage"])
            return result
            
        except Exception as e:
            self.logger.error("Memory defragmentation error: %s", e)
            raise

    async def optimize_memory(self, device_id: Optional[str], optimization_level: str, 
                             recommendations: Dict[str, Any]) -> Dict[str, Any]:
        """Optimize memory usage based on level and recommendations"""
        try:
            optimization_actions = []
            total_freed_bytes = 0
            
            # Apply optimization based on level and recommendations
            if optimization_level in ["normal", "aggressive"]:
                # Implement pooling optimization
                pooling_result = await self._optimize_allocation_pooling(device_id)
                optimization_actions.append(pooling_result)
                total_freed_bytes += pooling_result["freed_bytes"]
                
                # Implement fragmentation reduction
                if optimization_level == "aggressive":
                    defrag_result = await self.defragment_memory(device_id, True)
                    optimization_actions.append({
                        "action": "defragmentation",
                        "freed_bytes": defrag_result["freed_bytes"],
                        "success": defrag_result["defragmented"]
                    })
                    total_freed_bytes += defrag_result["freed_bytes"]
                
                # Apply specific recommendations
                if recommendations:
                    rec_result = await self._apply_optimization_recommendations(device_id, recommendations)
                    optimization_actions.extend(rec_result["actions"])
                    total_freed_bytes += rec_result["freed_bytes"]
            
            optimization_result = {
                "device_id": device_id,
                "optimization_level": optimization_level,
                "optimized": len(optimization_actions) > 0,
                "actions_performed": optimization_actions,
                "total_freed_bytes": total_freed_bytes,
                "timestamp": datetime.now().isoformat()
            }
            
            self.logger.info("Memory optimization on device %s: %d actions, %d bytes freed", 
                           device_id or "all", len(optimization_actions), total_freed_bytes)
            return optimization_result
            
        except Exception as e:
            self.logger.error("Memory optimization error: %s", e)
            raise

    # Helper methods for allocation pooling optimization
    async def _get_pooled_allocation(self, device_id: str, size_bytes: int) -> Optional[Dict[str, Any]]:
        """Get allocation from pool if available"""
        try:
            # Find closest pooled size
            pooled_size = self._find_closest_pooled_size(size_bytes)
            if pooled_size and device_id in self.allocation_pool:
                if pooled_size in self.allocation_pool[device_id]:
                    pool = self.allocation_pool[device_id][pooled_size]
                    if pool:
                        allocation = pool.pop()
                        self.logger.debug("Retrieved allocation from pool: %s", allocation["allocation_id"])
                        return allocation
            return None
        except Exception as e:
            self.logger.error("Get pooled allocation error: %s", e)
            return None

    async def _return_allocation_to_pool(self, allocation_id: str, device_id: Optional[str]) -> bool:
        """Return allocation to pool for reuse"""
        try:
            # In a real implementation, this would check allocation details and return to appropriate pool
            # For now, simulate pool return
            if device_id and allocation_id:
                self.logger.debug("Returned allocation to pool: %s", allocation_id)
                return True
            return False
        except Exception as e:
            self.logger.error("Return allocation to pool error: %s", e)
            return False

    def _find_closest_pooled_size(self, size_bytes: int) -> Optional[int]:
        """Find the closest pooled size for the requested allocation"""
        for pooled_size in sorted(self.common_sizes):
            if size_bytes <= pooled_size:
                return pooled_size
        return None

    async def _initialize_allocation_pools(self) -> None:
        """Initialize allocation pools for common sizes"""
        try:
            # Initialize pools for each device and common size
            # In a real implementation, this would pre-allocate memory blocks
            self.allocation_pool = {}
            self.logger.info("Allocation pools initialized for sizes: %s", self.common_sizes)
        except Exception as e:
            self.logger.error("Allocation pool initialization error: %s", e)
            raise

    # Simulation methods (would be replaced with real implementations)
    async def _simulate_allocation(self, device_id: str, size_bytes: int, allocation_type: str) -> bool:
        """Simulate memory allocation"""
        # Simulate allocation success/failure based on available memory
        return True  # Always succeed in simulation

    async def _simulate_deallocation(self, allocation_id: str, device_id: Optional[str], force: bool) -> bool:
        """Simulate memory deallocation"""
        return True  # Always succeed in simulation

    async def _get_simulated_allocations(self, device_id: Optional[str]) -> List[Dict[str, Any]]:
        """Get simulated allocation data"""
        # Return sample allocation data
        sample_allocations = [
            {
                "allocation_id": "alloc-001",
                "device_id": device_id or "gpu-0",
                "size_bytes": 1073741824,  # 1GB
                "allocation_type": "VRAM",
                "purpose": "model_loading",
                "persistent": False,
                "status": "active",
                "timestamp": datetime.now().isoformat()
            }
        ]
        return sample_allocations if not device_id or device_id == "gpu-0" else []

    async def _simulate_clear_all_memory(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Simulate clearing all memory"""
        return {"cleared_count": 5, "freed_bytes": 2147483648}  # 2GB

    async def _simulate_clear_non_persistent_memory(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Simulate clearing non-persistent memory"""
        return {"cleared_count": 3, "freed_bytes": 1073741824}  # 1GB

    async def _simulate_clear_temporary_memory(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Simulate clearing temporary memory"""
        return {"cleared_count": 2, "freed_bytes": 536870912}  # 512MB

    async def _simulate_defragmentation(self, device_id: Optional[str], aggressive: bool) -> Dict[str, Any]:
        """Simulate memory defragmentation"""
        return {
            "success": True,
            "freed_bytes": 268435456,  # 256MB
            "compacted_allocations": 8,
            "fragmentation_reduction": 15.5,
            "duration_seconds": 2.3
        }

    async def _optimize_allocation_pooling(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Optimize allocation pooling"""
        return {
            "action": "allocation_pooling",
            "freed_bytes": 134217728,  # 128MB
            "success": True
        }

    async def _apply_optimization_recommendations(self, device_id: Optional[str], 
                                                 recommendations: Dict[str, Any]) -> Dict[str, Any]:
        """Apply optimization recommendations"""
        return {
            "actions": [
                {"action": "recommendation_applied", "freed_bytes": 67108864, "success": True}  # 64MB
            ],
            "freed_bytes": 67108864
        }

    async def get_status(self) -> Dict[str, Any]:
        """Get allocation worker status"""
        return {
            "initialized": self.initialized,
            "pool_sizes": self.common_sizes,
            "active_pools": len(self.allocation_pool)
        }

    async def cleanup(self) -> None:
        """Clean up allocation worker resources"""
        try:
            self.logger.info("Cleaning up allocation worker...")
            self.allocation_pool.clear()
            self.initialized = False
            self.logger.info("Allocation worker cleanup complete")
        except Exception as e:
            self.logger.error("Allocation worker cleanup error: %s", e)