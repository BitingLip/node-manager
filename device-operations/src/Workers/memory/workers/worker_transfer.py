"""
Transfer Worker for SDXL Workers System
========================================

Handles memory transfer operations between devices for coordination between C# DirectML and Python PyTorch.
Based on Memory Domain Phase 4 Implementation Plan.
"""

import asyncio
import logging
from typing import Dict, Any, List, Optional
from datetime import datetime
import uuid


class TransferWorker:
    """
    Memory transfer operations worker
    Handles device-to-device memory transfers with progress tracking
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.active_transfers: Dict[str, Dict[str, Any]] = {}
        self.transfer_queue: List[Dict[str, Any]] = []
        self.max_concurrent_transfers = config.get("max_concurrent_transfers", 2)
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize transfer worker"""
        try:
            self.logger.info("Initializing transfer worker...")
            
            # Start transfer processing loop
            asyncio.create_task(self._transfer_processing_loop())
            
            self.initialized = True
            self.logger.info("Transfer worker initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error("Transfer worker initialization error: %s", e)
            return False

    async def transfer_memory(self, source_allocation_id: str, destination_device_id: str, 
                             transfer_type: str) -> Dict[str, Any]:
        """
        Transfer memory between devices
        Based on Phase 3 optimization: Async transfer operations with progress tracking
        """
        try:
            # Generate transfer ID
            transfer_id = str(uuid.uuid4())
            
            # Create transfer record
            transfer_request = {
                "transfer_id": transfer_id,
                "source_allocation_id": source_allocation_id,
                "destination_device_id": destination_device_id,
                "transfer_type": transfer_type,
                "status": "queued",
                "progress_percentage": 0,
                "created_timestamp": datetime.now().isoformat(),
                "started_timestamp": None,
                "completed_timestamp": None,
                "error_message": None
            }
            
            # Add to transfer queue
            self.transfer_queue.append(transfer_request)
            self.active_transfers[transfer_id] = transfer_request
            
            self.logger.info("Queued transfer %s: allocation %s to device %s (%s)", 
                           transfer_id, source_allocation_id, destination_device_id, transfer_type)
            
            # Return initial transfer info
            return {
                "transfer_id": transfer_id,
                "source_allocation_id": source_allocation_id,
                "destination_device_id": destination_device_id,
                "transfer_type": transfer_type,
                "status": "queued",
                "progress_percentage": 0,
                "estimated_duration_seconds": await self._estimate_transfer_duration(transfer_request),
                "timestamp": transfer_request["created_timestamp"]
            }
            
        except Exception as e:
            self.logger.error("Memory transfer initiation error: %s", e)
            raise

    async def get_transfer_status(self, transfer_id: str) -> Dict[str, Any]:
        """Get memory transfer status"""
        try:
            if transfer_id not in self.active_transfers:
                raise Exception(f"Transfer {transfer_id} not found")
            
            transfer_info = self.active_transfers[transfer_id]
            
            return {
                "transfer_id": transfer_id,
                "source_allocation_id": transfer_info["source_allocation_id"],
                "destination_device_id": transfer_info["destination_device_id"],
                "transfer_type": transfer_info["transfer_type"],
                "status": transfer_info["status"],
                "progress_percentage": transfer_info["progress_percentage"],
                "created_timestamp": transfer_info["created_timestamp"],
                "started_timestamp": transfer_info["started_timestamp"],
                "completed_timestamp": transfer_info["completed_timestamp"],
                "error_message": transfer_info["error_message"],
                "duration_seconds": await self._calculate_transfer_duration(transfer_info)
            }
            
        except Exception as e:
            self.logger.error("Get transfer status error: %s", e)
            raise

    async def cancel_transfer(self, transfer_id: str) -> Dict[str, Any]:
        """Cancel an active or queued transfer"""
        try:
            if transfer_id not in self.active_transfers:
                raise Exception(f"Transfer {transfer_id} not found")
            
            transfer_info = self.active_transfers[transfer_id]
            
            if transfer_info["status"] in ["completed", "failed", "cancelled"]:
                raise Exception(f"Transfer {transfer_id} cannot be cancelled (status: {transfer_info['status']})")
            
            # Update transfer status
            transfer_info["status"] = "cancelled"
            transfer_info["completed_timestamp"] = datetime.now().isoformat()
            
            # Remove from queue if still queued
            if transfer_info in self.transfer_queue:
                self.transfer_queue.remove(transfer_info)
            
            self.logger.info("Cancelled transfer %s", transfer_id)
            
            return {
                "transfer_id": transfer_id,
                "cancelled": True,
                "timestamp": transfer_info["completed_timestamp"]
            }
            
        except Exception as e:
            self.logger.error("Cancel transfer error: %s", e)
            raise

    async def list_transfers(self, device_id: Optional[str] = None, 
                            status_filter: Optional[str] = None) -> List[Dict[str, Any]]:
        """List transfers with optional filtering"""
        try:
            transfers = []
            
            for transfer_info in self.active_transfers.values():
                # Apply device filter
                if device_id and transfer_info["destination_device_id"] != device_id:
                    continue
                
                # Apply status filter
                if status_filter and transfer_info["status"] != status_filter:
                    continue
                
                transfers.append({
                    "transfer_id": transfer_info["transfer_id"],
                    "source_allocation_id": transfer_info["source_allocation_id"],
                    "destination_device_id": transfer_info["destination_device_id"],
                    "transfer_type": transfer_info["transfer_type"],
                    "status": transfer_info["status"],
                    "progress_percentage": transfer_info["progress_percentage"],
                    "created_timestamp": transfer_info["created_timestamp"]
                })
            
            return transfers
            
        except Exception as e:
            self.logger.error("List transfers error: %s", e)
            raise

    async def _transfer_processing_loop(self) -> None:
        """Background loop to process transfer queue"""
        while True:
            try:
                # Check if we can start new transfers
                active_count = len([t for t in self.active_transfers.values() 
                                  if t["status"] == "in_progress"])
                
                if active_count < self.max_concurrent_transfers and self.transfer_queue:
                    # Start next transfer
                    transfer_request = self.transfer_queue.pop(0)
                    asyncio.create_task(self._execute_transfer(transfer_request))
                
                # Clean up completed transfers older than 1 hour
                await self._cleanup_old_transfers()
                
                # Wait before next iteration
                await asyncio.sleep(1.0)
                
            except Exception as e:
                self.logger.error("Transfer processing loop error: %s", e)
                await asyncio.sleep(5.0)

    async def _execute_transfer(self, transfer_request: Dict[str, Any]) -> None:
        """Execute a memory transfer"""
        transfer_id = transfer_request["transfer_id"]
        
        try:
            self.logger.info("Starting transfer %s", transfer_id)
            
            # Update status to in_progress
            transfer_request["status"] = "in_progress"
            transfer_request["started_timestamp"] = datetime.now().isoformat()
            
            # Simulate transfer process with progress updates
            await self._simulate_transfer_process(transfer_request)
            
            # Update status to completed
            transfer_request["status"] = "completed"
            transfer_request["progress_percentage"] = 100
            transfer_request["completed_timestamp"] = datetime.now().isoformat()
            
            self.logger.info("Completed transfer %s", transfer_id)
            
        except Exception as e:
            self.logger.error("Transfer execution error for %s: %s", transfer_id, e)
            
            # Update status to failed
            transfer_request["status"] = "failed"
            transfer_request["error_message"] = str(e)
            transfer_request["completed_timestamp"] = datetime.now().isoformat()

    async def _simulate_transfer_process(self, transfer_request: Dict[str, Any]) -> None:
        """Simulate the transfer process with progress updates"""
        transfer_id = transfer_request["transfer_id"]
        transfer_type = transfer_request["transfer_type"]
        
        # Simulate different transfer speeds based on type
        if transfer_type == "copy":
            total_steps = 10
            step_duration = 0.5
        elif transfer_type == "move":
            total_steps = 8
            step_duration = 0.3
        else:  # "reference" or other types
            total_steps = 5
            step_duration = 0.2
        
        for step in range(1, total_steps + 1):
            # Update progress
            progress = int((step / total_steps) * 100)
            transfer_request["progress_percentage"] = progress
            
            self.logger.debug("Transfer %s progress: %d%%", transfer_id, progress)
            
            # Simulate processing time
            await asyncio.sleep(step_duration)
            
            # Simulate occasional transfer issues (5% chance)
            if step == total_steps // 2 and await self._should_simulate_error():
                raise Exception("Simulated transfer error")

    async def _should_simulate_error(self) -> bool:
        """Simulate occasional transfer errors for testing"""
        # 5% chance of error in simulation
        import random
        return random.random() < 0.05

    async def _estimate_transfer_duration(self, transfer_request: Dict[str, Any]) -> float:
        """Estimate transfer duration based on transfer type and data size"""
        transfer_type = transfer_request["transfer_type"]
        
        # Simulate duration estimation based on transfer type
        if transfer_type == "copy":
            return 5.0  # 5 seconds
        elif transfer_type == "move":
            return 3.0  # 3 seconds
        else:  # "reference" or other types
            return 1.0  # 1 second

    async def _calculate_transfer_duration(self, transfer_info: Dict[str, Any]) -> Optional[float]:
        """Calculate actual transfer duration"""
        try:
            if not transfer_info["started_timestamp"]:
                return None
            
            start_time = datetime.fromisoformat(transfer_info["started_timestamp"])
            
            if transfer_info["completed_timestamp"]:
                end_time = datetime.fromisoformat(transfer_info["completed_timestamp"])
                return (end_time - start_time).total_seconds()
            else:
                # Transfer still in progress
                current_time = datetime.now()
                return (current_time - start_time).total_seconds()
                
        except Exception as e:
            self.logger.error("Calculate transfer duration error: %s", e)
            return None

    async def _cleanup_old_transfers(self) -> None:
        """Clean up completed transfers older than 1 hour"""
        try:
            current_time = datetime.now()
            transfers_to_remove = []
            
            for transfer_id, transfer_info in self.active_transfers.items():
                if transfer_info["status"] in ["completed", "failed", "cancelled"]:
                    if transfer_info["completed_timestamp"]:
                        completed_time = datetime.fromisoformat(transfer_info["completed_timestamp"])
                        age_hours = (current_time - completed_time).total_seconds() / 3600
                        
                        if age_hours > 1.0:  # 1 hour
                            transfers_to_remove.append(transfer_id)
            
            # Remove old transfers
            for transfer_id in transfers_to_remove:
                del self.active_transfers[transfer_id]
                
            if transfers_to_remove:
                self.logger.info("Cleaned up %d old transfers", len(transfers_to_remove))
                
        except Exception as e:
            self.logger.error("Transfer cleanup error: %s", e)

    async def get_status(self) -> Dict[str, Any]:
        """Get transfer worker status"""
        try:
            status_counts = {}
            for transfer_info in self.active_transfers.values():
                status = transfer_info["status"]
                status_counts[status] = status_counts.get(status, 0) + 1
            
            return {
                "initialized": self.initialized,
                "active_transfers": len(self.active_transfers),
                "queued_transfers": len(self.transfer_queue),
                "max_concurrent_transfers": self.max_concurrent_transfers,
                "status_counts": status_counts
            }
        except Exception as e:
            return {"status": "error", "error": str(e)}

    async def cleanup(self) -> None:
        """Clean up transfer worker resources"""
        try:
            self.logger.info("Cleaning up transfer worker...")
            
            # Cancel all active transfers
            for transfer_info in self.active_transfers.values():
                if transfer_info["status"] in ["queued", "in_progress"]:
                    transfer_info["status"] = "cancelled"
                    transfer_info["completed_timestamp"] = datetime.now().isoformat()
            
            self.active_transfers.clear()
            self.transfer_queue.clear()
            self.initialized = False
            
            self.logger.info("Transfer worker cleanup complete")
            
        except Exception as e:
            self.logger.error("Transfer worker cleanup error: %s", e)