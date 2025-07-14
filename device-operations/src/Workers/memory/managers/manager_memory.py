"""
Memory Manager for SDXL Workers System
=======================================

Memory lifecycle management that coordinates memory operations between C# DirectML and Python PyTorch.
Based on Memory Domain Phase 4 Implementation Plan.
"""

import asyncio
import logging
from typing import Dict, Any, List, Optional
from dataclasses import dataclass
from datetime import datetime
import uuid


@dataclass
class AllocationTracker:
    """Memory allocation tracking for coordination with C# DirectML"""
    allocation_id: str
    device_id: str
    size_bytes: int
    allocation_type: str
    purpose: str
    timestamp: datetime
    persistent: bool
    status: str


@dataclass
class TransferTracker:
    """Memory transfer tracking for device-to-device operations"""
    transfer_id: str
    source_allocation_id: str
    destination_device_id: str
    transfer_type: str
    status: str
    timestamp: datetime
    progress_percentage: int


class MemoryManager:
    """
    Memory lifecycle management
    Coordinates memory operations between C# DirectML and Python PyTorch
    Based on Phase 3 finding: Need unified memory coordination
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.active_allocations: Dict[str, AllocationTracker] = {}
        self.active_transfers: Dict[str, TransferTracker] = {}
        self.memory_limits: Dict[str, int] = {}
        self.memory_pressure_thresholds: Dict[str, float] = {}
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize memory manager with device memory limits"""
        try:
            self.logger.info("Initializing memory manager...")
            
            # Initialize device memory limits and pressure thresholds
            await self._discover_device_memory_limits()
            await self._set_pressure_thresholds()
            
            self.initialized = True
            self.logger.info("Memory manager initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error("Memory manager initialization error: %s", e)
            return False

    async def track_allocation(self, allocation_info: Dict[str, Any]) -> None:
        """Track memory allocation for coordination with C# DirectML"""
        try:
            allocation_tracker = AllocationTracker(
                allocation_id=allocation_info["allocation_id"],
                device_id=allocation_info["device_id"],
                size_bytes=allocation_info["size_bytes"],
                allocation_type=allocation_info["allocation_type"],
                purpose=allocation_info["purpose"],
                timestamp=datetime.now(),
                persistent=allocation_info["persistent"],
                status="active"
            )
            
            self.active_allocations[allocation_tracker.allocation_id] = allocation_tracker
            
            # Check memory pressure after allocation
            await self._check_memory_pressure(allocation_tracker.device_id)
            
            self.logger.info("Tracked allocation: %s (%s bytes) on device %s", 
                           allocation_tracker.allocation_id, 
                           allocation_tracker.size_bytes, 
                           allocation_tracker.device_id)
            
        except Exception as e:
            self.logger.error("Allocation tracking error: %s", e)
            raise

    async def untrack_allocation(self, allocation_id: str) -> None:
        """Stop tracking memory allocation"""
        try:
            if allocation_id in self.active_allocations:
                tracker = self.active_allocations[allocation_id]
                tracker.status = "deallocated"
                del self.active_allocations[allocation_id]
                
                self.logger.info("Untracked allocation: %s", allocation_id)
            else:
                self.logger.warning("Attempted to untrack unknown allocation: %s", allocation_id)
                
        except Exception as e:
            self.logger.error("Allocation untracking error: %s", e)
            raise

    async def track_transfer(self, transfer_info: Dict[str, Any]) -> None:
        """Track memory transfer for device-to-device operations"""
        try:
            transfer_tracker = TransferTracker(
                transfer_id=transfer_info["transfer_id"],
                source_allocation_id=transfer_info["source_allocation_id"],
                destination_device_id=transfer_info["destination_device_id"],
                transfer_type=transfer_info["transfer_type"],
                status=transfer_info.get("status", "in_progress"),
                timestamp=datetime.now(),
                progress_percentage=transfer_info.get("progress_percentage", 0)
            )
            
            self.active_transfers[transfer_tracker.transfer_id] = transfer_tracker
            
            self.logger.info("Tracked transfer: %s from allocation %s to device %s", 
                           transfer_tracker.transfer_id, 
                           transfer_tracker.source_allocation_id, 
                           transfer_tracker.destination_device_id)
            
        except Exception as e:
            self.logger.error("Transfer tracking error: %s", e)
            raise

    async def update_transfer_progress(self, transfer_id: str, progress_percentage: int, 
                                     status: Optional[str] = None) -> None:
        """Update transfer progress"""
        try:
            if transfer_id in self.active_transfers:
                tracker = self.active_transfers[transfer_id]
                tracker.progress_percentage = progress_percentage
                if status:
                    tracker.status = status
                
                # Remove from active transfers if completed
                if status in ["completed", "failed", "cancelled"]:
                    del self.active_transfers[transfer_id]
                
                self.logger.debug("Updated transfer %s: %d%% (%s)", 
                               transfer_id, progress_percentage, status or tracker.status)
            else:
                self.logger.warning("Attempted to update unknown transfer: %s", transfer_id)
                
        except Exception as e:
            self.logger.error("Transfer progress update error: %s", e)
            raise

    async def clear_tracked_allocations(self, device_id: Optional[str], clear_type: str) -> Dict[str, Any]:
        """Clear tracked allocations based on device and type"""
        try:
            cleared_allocations = []
            allocations_to_remove = []
            
            for allocation_id, tracker in self.active_allocations.items():
                should_clear = False
                
                # Check device filter
                if device_id is None or tracker.device_id == device_id:
                    # Check clear type
                    if clear_type == "all":
                        should_clear = True
                    elif clear_type == "non_persistent" and not tracker.persistent:
                        should_clear = True
                    elif clear_type == "temporary" and tracker.purpose.startswith("temp"):
                        should_clear = True
                
                if should_clear:
                    cleared_allocations.append({
                        "allocation_id": allocation_id,
                        "device_id": tracker.device_id,
                        "size_bytes": tracker.size_bytes,
                        "purpose": tracker.purpose
                    })
                    allocations_to_remove.append(allocation_id)
            
            # Remove cleared allocations from tracking
            for allocation_id in allocations_to_remove:
                del self.active_allocations[allocation_id]
            
            total_freed_bytes = sum(alloc["size_bytes"] for alloc in cleared_allocations)
            
            self.logger.info("Cleared %d allocations, freed %d bytes", 
                           len(cleared_allocations), total_freed_bytes)
            
            return {
                "cleared_count": len(cleared_allocations),
                "freed_bytes": total_freed_bytes,
                "cleared_allocations": cleared_allocations
            }
            
        except Exception as e:
            self.logger.error("Clear tracked allocations error: %s", e)
            raise

    async def get_allocation_summary(self, device_id: Optional[str] = None) -> Dict[str, Any]:
        """Get summary of tracked allocations"""
        try:
            if device_id:
                relevant_allocations = {
                    aid: tracker for aid, tracker in self.active_allocations.items() 
                    if tracker.device_id == device_id
                }
            else:
                relevant_allocations = self.active_allocations.copy()
            
            total_bytes = sum(tracker.size_bytes for tracker in relevant_allocations.values())
            allocation_count = len(relevant_allocations)
            
            # Group by allocation type
            by_type = {}
            for tracker in relevant_allocations.values():
                alloc_type = tracker.allocation_type
                if alloc_type not in by_type:
                    by_type[alloc_type] = {"count": 0, "total_bytes": 0}
                by_type[alloc_type]["count"] += 1
                by_type[alloc_type]["total_bytes"] += tracker.size_bytes
            
            # Group by purpose
            by_purpose = {}
            for tracker in relevant_allocations.values():
                purpose = tracker.purpose
                if purpose not in by_purpose:
                    by_purpose[purpose] = {"count": 0, "total_bytes": 0}
                by_purpose[purpose]["count"] += 1
                by_purpose[purpose]["total_bytes"] += tracker.size_bytes
            
            return {
                "device_id": device_id or "all",
                "total_allocations": allocation_count,
                "total_bytes": total_bytes,
                "by_type": by_type,
                "by_purpose": by_purpose,
                "timestamp": datetime.now().isoformat()
            }
            
        except Exception as e:
            self.logger.error("Get allocation summary error: %s", e)
            raise

    async def get_memory_pressure_info(self, device_id: str) -> Dict[str, Any]:
        """Get memory pressure information for a device"""
        try:
            device_limit = self.memory_limits.get(device_id, 0)
            device_threshold = self.memory_pressure_thresholds.get(device_id, 0.85)
            
            # Calculate current usage from tracked allocations
            device_allocations = [
                tracker for tracker in self.active_allocations.values() 
                if tracker.device_id == device_id
            ]
            current_usage = sum(tracker.size_bytes for tracker in device_allocations)
            
            if device_limit > 0:
                usage_percentage = (current_usage / device_limit) * 100
                pressure_level = "high" if usage_percentage > (device_threshold * 100) else "normal"
            else:
                usage_percentage = 0
                pressure_level = "unknown"
            
            return {
                "device_id": device_id,
                "current_usage_bytes": current_usage,
                "memory_limit_bytes": device_limit,
                "usage_percentage": usage_percentage,
                "pressure_threshold_percentage": device_threshold * 100,
                "pressure_level": pressure_level,
                "active_allocations": len(device_allocations),
                "timestamp": datetime.now().isoformat()
            }
            
        except Exception as e:
            self.logger.error("Get memory pressure info error: %s", e)
            raise

    async def _discover_device_memory_limits(self) -> None:
        """Discover device memory limits for coordination with C# DirectML"""
        try:
            # Default memory limits (in bytes) - these would typically be discovered from actual devices
            default_limits = {
                "cpu": 8 * 1024 * 1024 * 1024,  # 8GB
                "gpu-0": 12 * 1024 * 1024 * 1024,  # 12GB
                "gpu-1": 12 * 1024 * 1024 * 1024,  # 12GB
            }
            
            # In a real implementation, this would query actual device capabilities
            # For now, use defaults from config or predefined values
            configured_limits = self.config.get("device_memory_limits", {})
            self.memory_limits = {**default_limits, **configured_limits}
            
            self.logger.info("Discovered device memory limits: %s", self.memory_limits)
            
        except Exception as e:
            self.logger.error("Device memory limit discovery error: %s", e)
            raise

    async def _set_pressure_thresholds(self) -> None:
        """Set memory pressure thresholds for each device"""
        try:
            # Default pressure thresholds (percentage of total memory)
            default_threshold = 0.85  # 85%
            
            # Set thresholds for all known devices
            for device_id in self.memory_limits.keys():
                configured_threshold = self.config.get("memory_pressure_thresholds", {}).get(device_id)
                self.memory_pressure_thresholds[device_id] = configured_threshold or default_threshold
            
            self.logger.info("Set memory pressure thresholds: %s", self.memory_pressure_thresholds)
            
        except Exception as e:
            self.logger.error("Memory pressure threshold setup error: %s", e)
            raise

    async def _check_memory_pressure(self, device_id: str) -> None:
        """Check memory pressure and coordinate with C# memory management"""
        try:
            pressure_info = await self.get_memory_pressure_info(device_id)
            
            if pressure_info["pressure_level"] == "high":
                self.logger.warning("High memory pressure detected on device %s: %.1f%% usage", 
                                  device_id, pressure_info["usage_percentage"])
                
                # In a real implementation, this would trigger memory optimization
                # or notify the C# layer about memory pressure
                await self._handle_memory_pressure(device_id, pressure_info)
            
        except Exception as e:
            self.logger.error("Memory pressure check error: %s", e)

    async def _handle_memory_pressure(self, device_id: str, pressure_info: Dict[str, Any]) -> None:
        """Handle memory pressure situation"""
        try:
            # Log the pressure situation
            self.logger.info("Handling memory pressure on device %s", device_id)
            
            # In a real implementation, this would:
            # 1. Identify candidates for cleanup (non-persistent allocations)
            # 2. Coordinate with allocation worker to free memory
            # 3. Notify C# layer about memory pressure
            # 4. Trigger automatic memory optimization if configured
            
            # For now, just log the situation
            self.logger.info("Memory pressure handling would be implemented here")
            
        except Exception as e:
            self.logger.error("Memory pressure handling error: %s", e)

    async def get_status(self) -> Dict[str, Any]:
        """Get memory manager status"""
        try:
            return {
                "initialized": self.initialized,
                "active_allocations": len(self.active_allocations),
                "active_transfers": len(self.active_transfers),
                "tracked_devices": list(self.memory_limits.keys()),
                "memory_limits": self.memory_limits,
                "pressure_thresholds": self.memory_pressure_thresholds
            }
        except Exception as e:
            return {"status": "error", "error": str(e)}

    async def cleanup(self) -> None:
        """Clean up memory manager resources"""
        try:
            self.logger.info("Cleaning up memory manager...")
            
            # Clear all tracked allocations and transfers
            self.active_allocations.clear()
            self.active_transfers.clear()
            self.memory_limits.clear()
            self.memory_pressure_thresholds.clear()
            
            self.initialized = False
            self.logger.info("Memory manager cleanup complete")
            
        except Exception as e:
            self.logger.error("Memory manager cleanup error: %s", e)