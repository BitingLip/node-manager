"""
Analytics Worker for SDXL Workers System
=========================================

Handles memory monitoring, analytics, and optimization recommendations.
Based on Memory Domain Phase 4 Implementation Plan and Phase 3 optimization requirements.
"""

import asyncio
import logging
from typing import Dict, Any, List, Optional
from datetime import datetime, timedelta
from dataclasses import dataclass
from collections import deque
import statistics


@dataclass
class MemoryEvent:
    """Memory event for analytics tracking"""
    event_type: str  # allocation, deallocation, clear, transfer, optimization
    device_id: str
    timestamp: datetime
    size_bytes: int
    allocation_id: Optional[str] = None
    details: Optional[Dict[str, Any]] = None


class AnalyticsWorker:
    """
    Memory monitoring and analytics worker
    Provides memory usage statistics, pressure monitoring, and optimization recommendations
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.memory_events: deque = deque(maxlen=10000)  # Store last 10k events
        self.device_stats: Dict[str, Dict[str, Any]] = {}
        self.monitoring_active = False
        self.monitoring_interval = config.get("monitoring_interval_seconds", 5.0)
        self.pressure_threshold = config.get("memory_pressure_threshold", 0.85)
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize analytics worker"""
        try:
            self.logger.info("Initializing analytics worker...")
            
            # Initialize device statistics tracking
            await self._initialize_device_stats()
            
            # Start background monitoring
            await self.start_monitoring()
            
            self.initialized = True
            self.logger.info("Analytics worker initialized successfully")
            return True
            
        except Exception as e:
            self.logger.error("Analytics worker initialization error: %s", e)
            return False

    async def start_monitoring(self) -> None:
        """Start continuous memory monitoring"""
        try:
            if not self.monitoring_active:
                self.monitoring_active = True
                asyncio.create_task(self._monitoring_loop())
                self.logger.info("Memory monitoring started (interval: %s seconds)", self.monitoring_interval)
        except Exception as e:
            self.logger.error("Start monitoring error: %s", e)

    async def stop_monitoring(self) -> None:
        """Stop continuous memory monitoring"""
        try:
            self.monitoring_active = False
            self.logger.info("Memory monitoring stopped")
        except Exception as e:
            self.logger.error("Stop monitoring error: %s", e)

    async def record_allocation_event(self, allocation_info: Dict[str, Any]) -> None:
        """Record memory allocation event"""
        try:
            event = MemoryEvent(
                event_type="allocation",
                device_id=allocation_info["device_id"],
                timestamp=datetime.now(),
                size_bytes=allocation_info["size_bytes"],
                allocation_id=allocation_info["allocation_id"],
                details=allocation_info
            )
            
            self.memory_events.append(event)
            await self._update_device_stats(event)
            
            self.logger.debug("Recorded allocation event: %s (%d bytes) on device %s", 
                            event.allocation_id, event.size_bytes, event.device_id)
            
        except Exception as e:
            self.logger.error("Record allocation event error: %s", e)

    async def record_deallocation_event(self, allocation_id: str, deallocation_info: Dict[str, Any]) -> None:
        """Record memory deallocation event"""
        try:
            event = MemoryEvent(
                event_type="deallocation",
                device_id=deallocation_info.get("device_id", "unknown"),
                timestamp=datetime.now(),
                size_bytes=0,  # Size would be tracked from original allocation
                allocation_id=allocation_id,
                details=deallocation_info
            )
            
            self.memory_events.append(event)
            await self._update_device_stats(event)
            
            self.logger.debug("Recorded deallocation event: %s on device %s", 
                            allocation_id, event.device_id)
            
        except Exception as e:
            self.logger.error("Record deallocation event error: %s", e)

    async def record_clear_event(self, device_id: Optional[str], clear_type: str, 
                                clear_result: Dict[str, Any]) -> None:
        """Record memory clear event"""
        try:
            event = MemoryEvent(
                event_type="clear",
                device_id=device_id or "all",
                timestamp=datetime.now(),
                size_bytes=clear_result.get("freed_bytes", 0),
                details={"clear_type": clear_type, "result": clear_result}
            )
            
            self.memory_events.append(event)
            await self._update_device_stats(event)
            
            self.logger.debug("Recorded clear event: %s (%d bytes) on device %s", 
                            clear_type, event.size_bytes, event.device_id)
            
        except Exception as e:
            self.logger.error("Record clear event error: %s", e)

    async def record_transfer_event(self, transfer_info: Dict[str, Any]) -> None:
        """Record memory transfer event"""
        try:
            event = MemoryEvent(
                event_type="transfer",
                device_id=transfer_info["destination_device_id"],
                timestamp=datetime.now(),
                size_bytes=0,  # Size would be determined from source allocation
                details=transfer_info
            )
            
            self.memory_events.append(event)
            await self._update_device_stats(event)
            
            self.logger.debug("Recorded transfer event: %s to device %s", 
                            transfer_info["transfer_id"], event.device_id)
            
        except Exception as e:
            self.logger.error("Record transfer event error: %s", e)

    async def record_optimization_event(self, device_id: Optional[str], 
                                       optimization_result: Dict[str, Any]) -> None:
        """Record memory optimization event"""
        try:
            event = MemoryEvent(
                event_type="optimization",
                device_id=device_id or "all",
                timestamp=datetime.now(),
                size_bytes=optimization_result.get("total_freed_bytes", 0),
                details=optimization_result
            )
            
            self.memory_events.append(event)
            await self._update_device_stats(event)
            
            self.logger.debug("Recorded optimization event: %d bytes freed on device %s", 
                            event.size_bytes, event.device_id)
            
        except Exception as e:
            self.logger.error("Record optimization event error: %s", e)

    async def get_memory_usage(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Get current memory usage information"""
        try:
            if device_id:
                device_stats = self.device_stats.get(device_id, {})
                return {
                    "device_id": device_id,
                    "current_usage_bytes": device_stats.get("current_usage_bytes", 0),
                    "peak_usage_bytes": device_stats.get("peak_usage_bytes", 0),
                    "allocation_count": device_stats.get("allocation_count", 0),
                    "usage_percentage": device_stats.get("usage_percentage", 0),
                    "last_updated": device_stats.get("last_updated"),
                    "timestamp": datetime.now().isoformat()
                }
            else:
                # Aggregate usage across all devices
                total_usage = 0
                total_peak = 0
                total_allocations = 0
                device_count = len(self.device_stats)
                
                for stats in self.device_stats.values():
                    total_usage += stats.get("current_usage_bytes", 0)
                    total_peak += stats.get("peak_usage_bytes", 0)
                    total_allocations += stats.get("allocation_count", 0)
                
                return {
                    "device_id": "all",
                    "total_usage_bytes": total_usage,
                    "total_peak_usage_bytes": total_peak,
                    "total_allocation_count": total_allocations,
                    "device_count": device_count,
                    "timestamp": datetime.now().isoformat()
                }
                
        except Exception as e:
            self.logger.error("Get memory usage error: %s", e)
            raise

    async def get_usage_statistics(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Get detailed memory usage statistics"""
        try:
            # Filter events for the specified device or all devices
            if device_id:
                relevant_events = [e for e in self.memory_events if e.device_id == device_id]
            else:
                relevant_events = list(self.memory_events)
            
            if not relevant_events:
                return {
                    "device_id": device_id or "all",
                    "event_count": 0,
                    "statistics": {},
                    "timestamp": datetime.now().isoformat()
                }
            
            # Calculate statistics
            allocation_events = [e for e in relevant_events if e.event_type == "allocation"]
            deallocation_events = [e for e in relevant_events if e.event_type == "deallocation"]
            
            allocation_sizes = [e.size_bytes for e in allocation_events if e.size_bytes > 0]
            
            statistics_data = {
                "total_events": len(relevant_events),
                "allocation_events": len(allocation_events),
                "deallocation_events": len(deallocation_events),
                "event_types": self._count_event_types(relevant_events),
                "allocation_statistics": {
                    "count": len(allocation_sizes),
                    "total_bytes": sum(allocation_sizes),
                    "average_bytes": statistics.mean(allocation_sizes) if allocation_sizes else 0,
                    "median_bytes": statistics.median(allocation_sizes) if allocation_sizes else 0,
                    "min_bytes": min(allocation_sizes) if allocation_sizes else 0,
                    "max_bytes": max(allocation_sizes) if allocation_sizes else 0
                },
                "time_range": {
                    "start": relevant_events[0].timestamp.isoformat() if relevant_events else None,
                    "end": relevant_events[-1].timestamp.isoformat() if relevant_events else None,
                    "duration_hours": self._calculate_time_range_hours(relevant_events)
                }
            }
            
            return {
                "device_id": device_id or "all",
                "statistics": statistics_data,
                "timestamp": datetime.now().isoformat()
            }
            
        except Exception as e:
            self.logger.error("Get usage statistics error: %s", e)
            raise

    async def get_fragmentation_info(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Get memory fragmentation information"""
        try:
            # Simulate fragmentation calculation
            # In a real implementation, this would analyze actual memory layout
            
            if device_id:
                device_stats = self.device_stats.get(device_id, {})
                allocation_count = device_stats.get("allocation_count", 0)
                
                # Simulate fragmentation percentage based on allocation patterns
                fragmentation_percentage = min(allocation_count * 2.5, 45.0)  # Max 45%
                
                return {
                    "device_id": device_id,
                    "fragmentation_percentage": fragmentation_percentage,
                    "fragmentation_level": self._classify_fragmentation_level(fragmentation_percentage),
                    "largest_free_block_bytes": device_stats.get("largest_free_block", 0),
                    "free_block_count": device_stats.get("free_block_count", 0),
                    "timestamp": datetime.now().isoformat()
                }
            else:
                # Aggregate fragmentation across all devices
                total_fragmentation = 0
                device_count = 0
                
                for device_id, stats in self.device_stats.items():
                    allocation_count = stats.get("allocation_count", 0)
                    fragmentation = min(allocation_count * 2.5, 45.0)
                    total_fragmentation += fragmentation
                    device_count += 1
                
                avg_fragmentation = total_fragmentation / device_count if device_count > 0 else 0
                
                return {
                    "device_id": "all",
                    "average_fragmentation_percentage": avg_fragmentation,
                    "fragmentation_level": self._classify_fragmentation_level(avg_fragmentation),
                    "device_count": device_count,
                    "timestamp": datetime.now().isoformat()
                }
                
        except Exception as e:
            self.logger.error("Get fragmentation info error: %s", e)
            raise

    async def get_memory_pressure(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Get memory pressure information"""
        try:
            if device_id:
                device_stats = self.device_stats.get(device_id, {})
                usage_percentage = device_stats.get("usage_percentage", 0)
                
                pressure_level = self._calculate_pressure_level(usage_percentage)
                
                return {
                    "device_id": device_id,
                    "usage_percentage": usage_percentage,
                    "pressure_level": pressure_level,
                    "pressure_threshold": self.pressure_threshold * 100,
                    "recommendations": await self._get_pressure_recommendations(device_id, pressure_level),
                    "timestamp": datetime.now().isoformat()
                }
            else:
                # Check pressure across all devices
                device_pressures = []
                for device_id, stats in self.device_stats.items():
                    usage_percentage = stats.get("usage_percentage", 0)
                    pressure_level = self._calculate_pressure_level(usage_percentage)
                    device_pressures.append({
                        "device_id": device_id,
                        "usage_percentage": usage_percentage,
                        "pressure_level": pressure_level
                    })
                
                # Determine overall pressure level
                high_pressure_devices = [d for d in device_pressures if d["pressure_level"] == "high"]
                overall_pressure = "high" if high_pressure_devices else "normal"
                
                return {
                    "device_id": "all",
                    "overall_pressure_level": overall_pressure,
                    "device_pressures": device_pressures,
                    "high_pressure_device_count": len(high_pressure_devices),
                    "timestamp": datetime.now().isoformat()
                }
                
        except Exception as e:
            self.logger.error("Get memory pressure error: %s", e)
            raise

    async def get_memory_analytics(self, device_id: Optional[str], time_range: str) -> Dict[str, Any]:
        """Get memory analytics for specified time range"""
        try:
            # Parse time range
            hours = self._parse_time_range(time_range)
            cutoff_time = datetime.now() - timedelta(hours=hours)
            
            # Filter events within time range
            if device_id:
                relevant_events = [
                    e for e in self.memory_events 
                    if e.device_id == device_id and e.timestamp >= cutoff_time
                ]
            else:
                relevant_events = [
                    e for e in self.memory_events 
                    if e.timestamp >= cutoff_time
                ]
            
            # Generate analytics
            analytics = {
                "device_id": device_id or "all",
                "time_range": time_range,
                "event_count": len(relevant_events),
                "activity_summary": self._generate_activity_summary(relevant_events),
                "memory_trends": await self._calculate_memory_trends(relevant_events, hours),
                "performance_metrics": await self._calculate_performance_metrics(relevant_events),
                "timestamp": datetime.now().isoformat()
            }
            
            return analytics
            
        except Exception as e:
            self.logger.error("Get memory analytics error: %s", e)
            raise

    async def get_optimization_recommendations(self, device_id: Optional[str]) -> Dict[str, Any]:
        """Get memory optimization recommendations"""
        try:
            recommendations = []
            
            if device_id:
                device_stats = self.device_stats.get(device_id, {})
                recommendations = await self._generate_device_recommendations(device_id, device_stats)
            else:
                # Generate recommendations for all devices
                for device_id, stats in self.device_stats.items():
                    device_recs = await self._generate_device_recommendations(device_id, stats)
                    recommendations.extend(device_recs)
            
            return {
                "device_id": device_id or "all",
                "recommendation_count": len(recommendations),
                "recommendations": recommendations,
                "priority_recommendations": [r for r in recommendations if r["priority"] == "high"],
                "timestamp": datetime.now().isoformat()
            }
            
        except Exception as e:
            self.logger.error("Get optimization recommendations error: %s", e)
            raise

    # Background monitoring methods
    async def _monitoring_loop(self) -> None:
        """Continuous memory monitoring loop"""
        while self.monitoring_active:
            try:
                # Update device statistics
                await self._update_all_device_stats()
                
                # Check for memory pressure changes
                await self._check_pressure_changes()
                
                # Clean up old events
                await self._cleanup_old_events()
                
                # Wait for next monitoring cycle
                await asyncio.sleep(self.monitoring_interval)
                
            except Exception as e:
                self.logger.error("Memory monitoring loop error: %s", e)
                await asyncio.sleep(self.monitoring_interval)

    async def _update_all_device_stats(self) -> None:
        """Update statistics for all devices"""
        try:
            # In a real implementation, this would query actual device memory status
            # For now, simulate periodic updates
            for device_id in self.device_stats:
                await self._simulate_device_stats_update(device_id)
        except Exception as e:
            self.logger.error("Update all device stats error: %s", e)

    async def _check_pressure_changes(self) -> None:
        """Check for memory pressure changes and log warnings"""
        try:
            for device_id, stats in self.device_stats.items():
                usage_percentage = stats.get("usage_percentage", 0)
                previous_pressure = stats.get("previous_pressure_level", "normal")
                current_pressure = self._calculate_pressure_level(usage_percentage)
                
                if current_pressure != previous_pressure:
                    if current_pressure == "high":
                        self.logger.warning("Memory pressure increased to HIGH on device %s (%.1f%% usage)", 
                                          device_id, usage_percentage)
                    elif previous_pressure == "high":
                        self.logger.info("Memory pressure decreased from HIGH on device %s (%.1f%% usage)", 
                                       device_id, usage_percentage)
                    
                    stats["previous_pressure_level"] = current_pressure
        except Exception as e:
            self.logger.error("Check pressure changes error: %s", e)

    async def _cleanup_old_events(self) -> None:
        """Clean up events older than retention period"""
        try:
            retention_hours = self.config.get("event_retention_hours", 24)
            cutoff_time = datetime.now() - timedelta(hours=retention_hours)
            
            # The deque automatically handles size limits, but we could implement time-based cleanup here
            # For now, rely on maxlen parameter of deque
            pass
        except Exception as e:
            self.logger.error("Cleanup old events error: %s", e)

    # Helper methods
    async def _initialize_device_stats(self) -> None:
        """Initialize device statistics tracking"""
        try:
            # Initialize stats for common devices
            default_devices = ["cpu", "gpu-0", "gpu-1"]
            for device_id in default_devices:
                self.device_stats[device_id] = {
                    "current_usage_bytes": 0,
                    "peak_usage_bytes": 0,
                    "allocation_count": 0,
                    "usage_percentage": 0,
                    "last_updated": datetime.now().isoformat(),
                    "previous_pressure_level": "normal"
                }
        except Exception as e:
            self.logger.error("Initialize device stats error: %s", e)

    async def _update_device_stats(self, event: MemoryEvent) -> None:
        """Update device statistics based on memory event"""
        try:
            device_id = event.device_id
            if device_id not in self.device_stats:
                self.device_stats[device_id] = {
                    "current_usage_bytes": 0,
                    "peak_usage_bytes": 0,
                    "allocation_count": 0,
                    "usage_percentage": 0,
                    "last_updated": datetime.now().isoformat(),
                    "previous_pressure_level": "normal"
                }
            
            stats = self.device_stats[device_id]
            
            # Update stats based on event type
            if event.event_type == "allocation":
                stats["current_usage_bytes"] += event.size_bytes
                stats["allocation_count"] += 1
            elif event.event_type == "deallocation":
                stats["allocation_count"] = max(0, stats["allocation_count"] - 1)
            elif event.event_type == "clear":
                stats["current_usage_bytes"] = max(0, stats["current_usage_bytes"] - event.size_bytes)
            
            # Update peak usage
            stats["peak_usage_bytes"] = max(stats["peak_usage_bytes"], stats["current_usage_bytes"])
            
            # Update usage percentage (simulate device memory limit)
            device_limit = 8 * 1024 * 1024 * 1024  # 8GB default
            stats["usage_percentage"] = (stats["current_usage_bytes"] / device_limit) * 100
            
            stats["last_updated"] = datetime.now().isoformat()
            
        except Exception as e:
            self.logger.error("Update device stats error: %s", e)

    async def _simulate_device_stats_update(self, device_id: str) -> None:
        """Simulate periodic device statistics update"""
        try:
            # Simulate minor fluctuations in memory usage
            stats = self.device_stats[device_id]
            # Add small random variation to simulate real memory usage changes
            import random
            variation = random.randint(-10485760, 10485760)  # ±10MB variation
            stats["current_usage_bytes"] = max(0, stats["current_usage_bytes"] + variation)
            
            # Recalculate usage percentage
            device_limit = 8 * 1024 * 1024 * 1024  # 8GB default
            stats["usage_percentage"] = (stats["current_usage_bytes"] / device_limit) * 100
            stats["last_updated"] = datetime.now().isoformat()
            
        except Exception as e:
            self.logger.error("Simulate device stats update error: %s", e)

    def _count_event_types(self, events: List[MemoryEvent]) -> Dict[str, int]:
        """Count events by type"""
        counts = {}
        for event in events:
            counts[event.event_type] = counts.get(event.event_type, 0) + 1
        return counts

    def _calculate_time_range_hours(self, events: List[MemoryEvent]) -> float:
        """Calculate time range of events in hours"""
        if len(events) < 2:
            return 0.0
        
        start_time = min(e.timestamp for e in events)
        end_time = max(e.timestamp for e in events)
        return (end_time - start_time).total_seconds() / 3600

    def _classify_fragmentation_level(self, fragmentation_percentage: float) -> str:
        """Classify fragmentation level"""
        if fragmentation_percentage < 10:
            return "low"
        elif fragmentation_percentage < 25:
            return "moderate"
        else:
            return "high"

    def _calculate_pressure_level(self, usage_percentage: float) -> str:
        """Calculate memory pressure level"""
        if usage_percentage >= self.pressure_threshold * 100:
            return "high"
        elif usage_percentage >= (self.pressure_threshold - 0.1) * 100:
            return "medium"
        else:
            return "normal"

    async def _get_pressure_recommendations(self, device_id: str, pressure_level: str) -> List[str]:
        """Get recommendations for memory pressure situation"""
        if pressure_level == "high":
            return [
                "Consider clearing non-persistent allocations",
                "Run memory defragmentation",
                "Optimize model memory usage",
                "Move some allocations to other devices"
            ]
        elif pressure_level == "medium":
            return [
                "Monitor memory usage closely",
                "Consider preemptive optimization"
            ]
        else:
            return []

    def _parse_time_range(self, time_range: str) -> float:
        """Parse time range string to hours"""
        try:
            if time_range.endswith('h'):
                return float(time_range[:-1])
            elif time_range.endswith('m'):
                return float(time_range[:-1]) / 60
            elif time_range.endswith('d'):
                return float(time_range[:-1]) * 24
            else:
                return 1.0  # Default to 1 hour
        except:
            return 1.0

    def _generate_activity_summary(self, events: List[MemoryEvent]) -> Dict[str, Any]:
        """Generate activity summary from events"""
        return {
            "total_events": len(events),
            "event_types": self._count_event_types(events),
            "busiest_hour": "12:00-13:00",  # Simulated
            "average_events_per_hour": len(events) / max(1, self._calculate_time_range_hours(events))
        }

    async def _calculate_memory_trends(self, events: List[MemoryEvent], hours: float) -> Dict[str, Any]:
        """Calculate memory usage trends"""
        allocation_events = [e for e in events if e.event_type == "allocation"]
        
        return {
            "allocation_trend": "increasing" if len(allocation_events) > 0 else "stable",
            "average_allocation_size": statistics.mean([e.size_bytes for e in allocation_events]) if allocation_events else 0,
            "peak_allocation_hour": "14:00-15:00"  # Simulated
        }

    async def _calculate_performance_metrics(self, events: List[MemoryEvent]) -> Dict[str, Any]:
        """Calculate performance metrics"""
        return {
            "allocation_success_rate": 0.98,  # Simulated
            "average_allocation_time_ms": 2.5,  # Simulated
            "defragmentation_efficiency": 0.85  # Simulated
        }

    async def _generate_device_recommendations(self, device_id: str, stats: Dict[str, Any]) -> List[Dict[str, Any]]:
        """Generate optimization recommendations for a device"""
        recommendations = []
        usage_percentage = stats.get("usage_percentage", 0)
        allocation_count = stats.get("allocation_count", 0)
        
        # High usage recommendation
        if usage_percentage > 80:
            recommendations.append({
                "type": "memory_cleanup",
                "priority": "high",
                "description": f"Device {device_id} has {usage_percentage:.1f}% memory usage. Consider clearing unused allocations.",
                "estimated_benefit": "20-30% memory reduction"
            })
        
        # High fragmentation recommendation
        if allocation_count > 20:
            recommendations.append({
                "type": "defragmentation",
                "priority": "medium",
                "description": f"Device {device_id} has {allocation_count} allocations. Defragmentation may improve performance.",
                "estimated_benefit": "10-15% fragmentation reduction"
            })
        
        return recommendations

    async def get_status(self) -> Dict[str, Any]:
        """Get analytics worker status"""
        return {
            "initialized": self.initialized,
            "monitoring_active": self.monitoring_active,
            "event_count": len(self.memory_events),
            "tracked_devices": len(self.device_stats),
            "monitoring_interval": self.monitoring_interval
        }

    async def cleanup(self) -> None:
        """Clean up analytics worker resources"""
        try:
            self.logger.info("Cleaning up analytics worker...")
            await self.stop_monitoring()
            self.memory_events.clear()
            self.device_stats.clear()
            self.initialized = False
            self.logger.info("Analytics worker cleanup complete")
        except Exception as e:
            self.logger.error("Analytics worker cleanup error: %s", e)