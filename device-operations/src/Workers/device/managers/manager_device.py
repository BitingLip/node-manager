"""
Device Manager for SDXL Workers System
=====================================

Migrated from core/device_manager.py
Handles device detection, initialization, and management for DirectML-based SDXL inference.
Provides optimized device selection and memory management capabilities.
"""

# CRITICAL: Import DirectML patch FIRST to intercept CUDA calls
try:
    from ..utilities import dml_patch
except ImportError:
    try:
        from utilities import dml_patch
    except ImportError:
        # DML patch will be available from utilities in production
        dml_patch = None

import logging
import torch
import platform
from typing import Dict, Any, List, Optional, Tuple
from dataclasses import dataclass
from enum import Enum
from datetime import datetime


class DeviceErrorCodes:
    """Standardized error codes for device operations"""
    DEVICE_NOT_FOUND = "DEVICE_NOT_FOUND"
    DEVICE_NOT_AVAILABLE = "DEVICE_NOT_AVAILABLE"
    DEVICE_OPTIMIZATION_FAILED = "DEVICE_OPTIMIZATION_FAILED"
    DEVICE_MEMORY_INFO_FAILED = "DEVICE_MEMORY_INFO_FAILED"
    DEVICE_CAPABILITIES_FAILED = "DEVICE_CAPABILITIES_FAILED"
    DEVICE_STATUS_FAILED = "DEVICE_STATUS_FAILED"
    DEVICE_HARDWARE_ERROR = "DEVICE_HARDWARE_ERROR"
    DEVICE_DRIVER_ERROR = "DEVICE_DRIVER_ERROR"
    DEVICE_TIMEOUT_ERROR = "DEVICE_TIMEOUT_ERROR"
    DEVICE_INITIALIZATION_FAILED = "DEVICE_INITIALIZATION_FAILED"
    DEVICE_DETECTION_FAILED = "DEVICE_DETECTION_FAILED"


def create_error_response(error_code: str, message: str, device_id: Optional[str] = None, details: Optional[dict] = None) -> Dict[str, Any]:
    """Create standardized error response"""
    error_response = {
        "success": False,
        "error_code": error_code,
        "error_message": message,
        "timestamp": datetime.utcnow().isoformat()
    }
    
    if device_id:
        error_response["device_id"] = device_id
        
    if details:
        error_response["error_details"] = details
        
    return error_response


def create_success_response(data: Any, source: str = "hardware") -> Dict[str, Any]:
    """Create standardized success response"""
    return {
        "success": True,
        "data": data,
        "source": source,
        "timestamp": datetime.utcnow().isoformat()
    }


class DeviceType(Enum):
    """Supported device types."""
    CPU = "cpu"
    DIRECTML = "privateuseone"  # DirectML device identifier
    CUDA = "cuda"
    MPS = "mps"  # Apple Metal Performance Shaders


@dataclass
class DeviceInfo:
    """Information about a compute device."""
    device_id: str
    device_type: DeviceType
    name: str
    memory_total: int  # In bytes
    memory_available: int  # In bytes
    compute_capability: Optional[str] = None
    is_available: bool = True
    performance_score: float = 0.0


class DeviceManager:
    """
    Manages compute devices for SDXL inference.
    
    Handles device detection, selection, and optimization for DirectML,
    CUDA, and CPU backends.
    """
    
    def __init__(self, config: Optional[Dict[str, Any]] = None):
        self.config = config or {}
        self.logger = logging.getLogger(__name__)
        self.devices: List[DeviceInfo] = []
        self.current_device: Optional[DeviceInfo] = None
        self._directml_available = False
        self._cuda_available = False
        self._mps_available = False
        self._initialized = False
        
    async def initialize(self) -> bool:
        """
        Initialize device manager and detect available devices.
        
        Returns:
            True if initialization successful
        """
        try:
            self.logger.info("Initializing device manager...")
            
            # Check PyTorch DirectML availability
            self._check_directml_availability()
            
            # Check CUDA availability
            self._check_cuda_availability()
            
            # Check MPS availability (Apple Silicon)
            self._check_mps_availability()
            
            # Detect and enumerate devices
            self._detect_devices()
            
            # Select optimal device
            self._select_optimal_device()
            
            self.logger.info(f"Device manager initialized with {len(self.devices)} devices")
            if self.current_device:
                self.logger.info(f"Selected device: {self.current_device.name} ({self.current_device.device_type.value})")
            
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to initialize device manager: {str(e)}")
            self._initialized = False
            return False
        
        self._initialized = True
    
    def _check_directml_availability(self) -> None:
        """Check if PyTorch DirectML is available."""
        try:
            import torch_directml
            self._directml_available = torch_directml.is_available()
            if self._directml_available:
                self.logger.info("PyTorch DirectML is available")
            else:
                self.logger.warning("PyTorch DirectML is installed but not available")
        except ImportError:
            self.logger.info("PyTorch DirectML is not installed")
            self._directml_available = False
    
    def _check_cuda_availability(self) -> None:
        """Check if CUDA is available."""
        self._cuda_available = torch.cuda.is_available()
        if self._cuda_available:
            self.logger.info(f"CUDA is available with {torch.cuda.device_count()} devices")
        else:
            self.logger.info("CUDA is not available")
    
    def _check_mps_availability(self) -> None:
        """Check if MPS (Metal Performance Shaders) is available."""
        self._mps_available = hasattr(torch.backends, 'mps') and torch.backends.mps.is_available()
        if self._mps_available:
            self.logger.info("MPS (Metal Performance Shaders) is available")
        else:
            self.logger.info("MPS is not available")
    
    def _detect_devices(self) -> None:
        """Detect and enumerate available devices."""
        self.devices = []
        
        # Add CPU device (always available)
        cpu_device = DeviceInfo(
            device_id="cpu",
            device_type=DeviceType.CPU,
            name="CPU",
            memory_total=self._get_system_memory(),
            memory_available=self._get_available_memory(),
            performance_score=1.0  # Base score
        )
        self.devices.append(cpu_device)
        
        # Add DirectML devices
        if self._directml_available:
            self._detect_directml_devices()
        
        # Add CUDA devices
        if self._cuda_available:
            self._detect_cuda_devices()
        
        # Add MPS device
        if self._mps_available:
            self._detect_mps_device()
    
    def _detect_directml_devices(self) -> None:
        """Detect DirectML devices."""
        try:
            import torch_directml
            
            # DirectML typically exposes a single device
            device_count = torch_directml.device_count()
            
            for i in range(device_count):
                device_name = torch_directml.device_name(i)
                
                directml_device = DeviceInfo(
                    device_id=f"privateuseone:{i}",
                    device_type=DeviceType.DIRECTML,
                    name=f"DirectML: {device_name}",
                    memory_total=self._estimate_directml_memory(),
                    memory_available=self._estimate_directml_memory(),
                    performance_score=3.0  # Higher than CPU
                )
                self.devices.append(directml_device)
                
        except Exception as e:
            self.logger.warning(f"Failed to detect DirectML devices: {str(e)}")
    
    def _detect_cuda_devices(self) -> None:
        """Detect CUDA devices."""
        try:
            for i in range(torch.cuda.device_count()):
                props = torch.cuda.get_device_properties(i)
                
                cuda_device = DeviceInfo(
                    device_id=f"cuda:{i}",
                    device_type=DeviceType.CUDA,
                    name=f"CUDA: {props.name}",
                    memory_total=props.total_memory,
                    memory_available=props.total_memory - torch.cuda.memory_reserved(i),
                    compute_capability=f"{props.major}.{props.minor}",
                    performance_score=4.0  # Highest score
                )
                self.devices.append(cuda_device)
                
        except Exception as e:
            self.logger.warning(f"Failed to detect CUDA devices: {str(e)}")
    
    def _detect_mps_device(self) -> None:
        """Detect MPS device (Apple Silicon)."""
        try:
            mps_device = DeviceInfo(
                device_id="mps",
                device_type=DeviceType.MPS,
                name="MPS: Apple Silicon GPU",
                memory_total=self._get_system_memory(),  # Unified memory
                memory_available=self._get_available_memory(),
                performance_score=3.5  # Between DirectML and CUDA
            )
            self.devices.append(mps_device)
            
        except Exception as e:
            self.logger.warning(f"Failed to detect MPS device: {str(e)}")
    
    def _select_optimal_device(self) -> None:
        """Select the optimal device based on performance score and availability."""
        if not self.devices:
            self.logger.error("No devices detected")
            return
        
        # Sort devices by performance score (descending)
        sorted_devices = sorted(self.devices, key=lambda d: d.performance_score, reverse=True)
        
        # Select the best available device
        for device in sorted_devices:
            if device.is_available:
                self.current_device = device
                break
        
        if not self.current_device:
            # Fallback to first device (should be CPU)
            self.current_device = self.devices[0]
    
    def get_device(self) -> torch.device:
        """
        Get the current PyTorch device.
        
        Returns:
            PyTorch device object
        """
        if not self.current_device:
            return torch.device("cpu")
        
        return torch.device(self.current_device.device_id)
    
    def get_device_info_sync(self) -> Optional[DeviceInfo]:
        """Get information about the current device (synchronous version)."""
        return self.current_device
    
    def list_devices_sync(self) -> List[DeviceInfo]:
        """List all detected devices (synchronous version)."""
        return self.devices.copy()
    
    async def get_device_info(self, device_id: Optional[str] = None) -> Dict[str, Any]:
        """Get information about a specific device or current device."""
        try:
            if not self._initialized:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_INITIALIZATION_FAILED,
                    "Device manager not initialized"
                )
            
            if device_id:
                # Get specific device info
                for device in self.devices:
                    if device.device_id == device_id:
                        device_data = {
                            "id": device.device_id,
                            "device_type": device.device_type.value,
                            "name": device.name,
                            "memory_total": device.memory_total,
                            "memory_available": device.memory_available,
                            "compute_capability": device.compute_capability,
                            "is_available": device.is_available,
                            "performance_score": device.performance_score,
                            "status": "available" if device.is_available else "offline"
                        }
                        return create_success_response(device_data)
                
                return create_error_response(
                    DeviceErrorCodes.DEVICE_NOT_FOUND,
                    f"Device not found: {device_id}",
                    device_id=device_id
                )
            
            # Get current device info
            if self.current_device:
                device_data = {
                    "id": self.current_device.device_id,
                    "device_type": self.current_device.device_type.value,
                    "name": self.current_device.name,
                    "memory_total": self.current_device.memory_total,
                    "memory_available": self.current_device.memory_available,
                    "compute_capability": self.current_device.compute_capability,
                    "is_available": self.current_device.is_available,
                    "performance_score": self.current_device.performance_score,
                    "status": "available" if self.current_device.is_available else "offline"
                }
                return create_success_response(device_data)
            
            return create_error_response(
                DeviceErrorCodes.DEVICE_NOT_FOUND,
                "No device selected"
            )
            
        except Exception as e:
            return create_error_response(
                DeviceErrorCodes.DEVICE_STATUS_FAILED,
                f"Failed to get device info: {str(e)}",
                device_id=device_id,
                details={"exception": str(e)}
            )
    
    async def list_devices(self) -> Dict[str, Any]:
        """List all detected devices."""
        try:
            if not self._initialized:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_INITIALIZATION_FAILED,
                    "Device manager not initialized"
                )
            
            devices_data = [
                {
                    "id": device.device_id,
                    "device_type": device.device_type.value,
                    "name": device.name,
                    "memory_total": device.memory_total,
                    "memory_available": device.memory_available,
                    "compute_capability": device.compute_capability,
                    "is_available": device.is_available,
                    "performance_score": device.performance_score,
                    "status": "available" if device.is_available else "offline"
                }
                for device in self.devices
            ]
            
            return create_success_response(devices_data)
            
        except Exception as e:
            return create_error_response(
                DeviceErrorCodes.DEVICE_DETECTION_FAILED,
                f"Failed to list devices: {str(e)}",
                details={"exception": str(e)}
            )
    
    async def set_device(self, device_id: str) -> Dict[str, Any]:
        """
        Set the current device by ID.
        
        Args:
            device_id: Device identifier
            
        Returns:
            Structured response with success/error information
        """
        try:
            if not self._initialized:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_INITIALIZATION_FAILED,
                    "Device manager not initialized",
                    device_id=device_id
                )
            
            for device in self.devices:
                if device.device_id == device_id:
                    if not device.is_available:
                        return create_error_response(
                            DeviceErrorCodes.DEVICE_NOT_AVAILABLE,
                            f"Device {device_id} is not available",
                            device_id=device_id
                        )
                    
                    self.current_device = device
                    self.logger.info(f"Device set to: {device.name}")
                    
                    return create_success_response({
                        "device_id": device_id,
                        "device_name": device.name,
                        "message": f"Successfully set device to {device.name}"
                    })
            
            return create_error_response(
                DeviceErrorCodes.DEVICE_NOT_FOUND,
                f"Device {device_id} not found",
                device_id=device_id
            )
            
        except Exception as e:
            self.logger.error(f"Error setting device {device_id}: {str(e)}")
            return create_error_response(
                DeviceErrorCodes.DEVICE_HARDWARE_ERROR,
                f"Failed to set device: {str(e)}",
                device_id=device_id,
                details={"exception": str(e)}
            )
    
    async def get_memory_info(self, device_id: Optional[str] = None) -> Dict[str, Any]:
        """
        Get memory information for the current device or specific device.
        
        Args:
            device_id: Optional device identifier
            
        Returns:
            Structured response with memory statistics
        """
        try:
            if not self._initialized:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_INITIALIZATION_FAILED,
                    "Device manager not initialized",
                    device_id=device_id
                )
            
            target_device = self.current_device
            
            # If device_id specified, find that device
            if device_id:
                target_device = None
                for device in self.devices:
                    if device.device_id == device_id:
                        target_device = device
                        break
                
                if not target_device:
                    return create_error_response(
                        DeviceErrorCodes.DEVICE_NOT_FOUND,
                        f"Device not found: {device_id}",
                        device_id=device_id
                    )
            
            if not target_device:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_NOT_FOUND,
                    "No device selected",
                    device_id=device_id
                )
            
            device_type = target_device.device_type
            
            if device_type == DeviceType.CUDA:
                try:
                    device_idx = int(target_device.device_id.split(":")[-1])
                    memory_data = {
                        "total_memory_bytes": torch.cuda.get_device_properties(device_idx).total_memory,
                        "allocated_memory_bytes": torch.cuda.memory_allocated(device_idx),
                        "reserved_memory_bytes": torch.cuda.memory_reserved(device_idx),
                        "available_memory_bytes": torch.cuda.get_device_properties(device_idx).total_memory - torch.cuda.memory_reserved(device_idx),
                        "supports_memory_pooling": True,
                        "allocation_alignment": 256
                    }
                    return create_success_response(memory_data)
                    
                except Exception as e:
                    return create_error_response(
                        DeviceErrorCodes.DEVICE_MEMORY_INFO_FAILED,
                        f"Failed to get CUDA memory info: {str(e)}",
                        device_id=device_id or target_device.device_id,
                        details={"exception": str(e)}
                    )
            
            elif device_type == DeviceType.DIRECTML:
                memory_data = {
                    "total_memory_bytes": target_device.memory_total,
                    "allocated_memory_bytes": target_device.memory_total - target_device.memory_available,
                    "available_memory_bytes": target_device.memory_available,
                    "supports_memory_pooling": False,
                    "allocation_alignment": 256,
                    "estimated": True
                }
                return create_success_response(memory_data)
            
            else:
                memory_data = {
                    "total_memory_bytes": target_device.memory_total,
                    "allocated_memory_bytes": target_device.memory_total - target_device.memory_available,
                    "available_memory_bytes": target_device.memory_available,
                    "supports_memory_pooling": False,
                    "allocation_alignment": 1
                }
                return create_success_response(memory_data)
                
        except Exception as e:
            return create_error_response(
                DeviceErrorCodes.DEVICE_MEMORY_INFO_FAILED,
                f"Failed to get memory info: {str(e)}",
                device_id=device_id,
                details={"exception": str(e)}
            )
    
    async def optimize_settings(self, device_id: Optional[str] = None, optimization_target: str = "balanced") -> Dict[str, Any]:
        """
        Get optimized settings for the specified device.
        
        Args:
            device_id: Optional device identifier
            optimization_target: Optimization target (performance, memory, balanced)
            
        Returns:
            Structured response with recommended settings
        """
        try:
            if not self._initialized:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_INITIALIZATION_FAILED,
                    "Device manager not initialized",
                    device_id=device_id
                )
            
            target_device = self.current_device
            
            # If device_id specified, find that device
            if device_id:
                target_device = None
                for device in self.devices:
                    if device.device_id == device_id:
                        target_device = device
                        break
                
                if not target_device:
                    return create_error_response(
                        DeviceErrorCodes.DEVICE_NOT_FOUND,
                        f"Device not found: {device_id}",
                        device_id=device_id
                    )
            
            if not target_device:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_NOT_FOUND,
                    "No device selected",
                    device_id=device_id
                )
            
            device_type = target_device.device_type
            memory_gb = target_device.memory_total / (1024**3)
            
            # Base settings
            settings = {
                "attention_slicing": True,
                "vae_slicing": True,
                "cpu_offload": False,
                "sequential_cpu_offload": False,
                "optimization_target": optimization_target
            }
            
            # Adjust settings based on device type, memory, and optimization target
            if device_type == DeviceType.CPU:
                settings.update({
                    "cpu_offload": False,
                    "sequential_cpu_offload": False,
                    "attention_slicing": True,
                    "vae_slicing": True
                })
            
            elif device_type == DeviceType.DIRECTML:
                if optimization_target == "memory" or memory_gb < 8:
                    settings.update({
                        "cpu_offload": True,
                        "sequential_cpu_offload": True
                    })
                elif optimization_target == "performance" and memory_gb >= 12:
                    settings.update({
                        "cpu_offload": False,
                        "sequential_cpu_offload": False,
                        "attention_slicing": False
                    })
                else:  # balanced
                    settings.update({
                        "cpu_offload": False,
                        "sequential_cpu_offload": False,
                        "attention_slicing": True
                    })
            
            elif device_type in [DeviceType.CUDA, DeviceType.MPS]:
                if optimization_target == "memory" or memory_gb < 8:
                    settings.update({
                        "cpu_offload": True,
                        "sequential_cpu_offload": True
                    })
                elif optimization_target == "performance" and memory_gb >= 16:
                    settings.update({
                        "attention_slicing": False,
                        "vae_slicing": False
                    })
            
            optimization_data = {
                "current_settings": settings,
                "recommended_settings": settings,
                "expected_improvement": 15.0,  # Percentage improvement estimate
                "confidence_score": 0.85,
                "analysis": {
                    "device_type": device_type.value,
                    "memory_gb": memory_gb,
                    "optimization_target": optimization_target
                }
            }
            
            return create_success_response(optimization_data)
            
        except Exception as e:
            return create_error_response(
                DeviceErrorCodes.DEVICE_OPTIMIZATION_FAILED,
                f"Failed to generate optimization settings: {str(e)}",
                device_id=device_id,
                details={"exception": str(e)}
            )
    
    async def get_device_status(self, device_id: str) -> Dict[str, Any]:
        """
        Get status information for a specific device.
        
        Args:
            device_id: Device identifier
            
        Returns:
            Structured response with device status information
        """
        try:
            if not self._initialized:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_INITIALIZATION_FAILED,
                    "Device manager not initialized",
                    device_id=device_id
                )
            
            target_device = None
            for device in self.devices:
                if device.device_id == device_id:
                    target_device = device
                    break
            
            if not target_device:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_NOT_FOUND,
                    f"Device not found: {device_id}",
                    device_id=device_id
                )
            
            # Get current utilization info
            utilization = {
                "cpu_utilization": 0.0,
                "memory_utilization": 0.0,
                "gpu_utilization": 0.0
            }
            
            if target_device.device_type == DeviceType.CUDA:
                try:
                    device_idx = int(target_device.device_id.split(":")[-1])
                    allocated = torch.cuda.memory_allocated(device_idx)
                    total = torch.cuda.get_device_properties(device_idx).total_memory
                    utilization["memory_utilization"] = (allocated / total) * 100
                    utilization["gpu_utilization"] = 50.0  # Placeholder
                except Exception:
                    pass
            
            status_data = {
                "status": "available" if target_device.is_available else "offline",
                "utilization": utilization,
                "performance": {
                    "operations_per_second": 0.0,
                    "average_operation_time": 0.0,
                    "throughput_mbps": 0.0,
                    "error_rate": 0.0,
                    "uptime_percentage": 100.0,
                    "performance_score": target_device.performance_score
                },
                "workload": {
                    "active_sessions": 0,
                    "queued_operations": 0,
                    "estimated_completion": None
                }
            }
            
            return create_success_response(status_data)
            
        except Exception as e:
            return create_error_response(
                DeviceErrorCodes.DEVICE_STATUS_FAILED,
                f"Failed to get device status: {str(e)}",
                device_id=device_id,
                details={"exception": str(e)}
            )
    
    async def get_device_status_extended(self, device_id: str, include_health_metrics: bool = True, 
                           include_workload_info: bool = True) -> Dict[str, Any]:
        """
        Get real-time device status with health and workload information
        Based on Phase 2 requirement: Real-time status communication protocol
        """
        try:
            if not self._initialized:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_INITIALIZATION_FAILED,
                    "Device manager not initialized",
                    device_id=device_id
                )
            
            target_device = None
            for device in self.devices:
                if device.device_id == device_id:
                    target_device = device
                    break
            
            if not target_device:
                return create_error_response(
                    DeviceErrorCodes.DEVICE_NOT_FOUND,
                    f"Device not found: {device_id}",
                    device_id=device_id
                )
            
            # Real-time status collection
            status_info = {
                "device_id": device_id,
                "status": await self._get_current_device_status(device_id),
                "status_description": await self._get_status_description(device_id),
                "utilization": await self._get_device_utilization(device_id),
                "last_updated": datetime.utcnow().isoformat()
            }
            
            # Health metrics (optional)
            if include_health_metrics:
                status_info["health_metrics"] = await self._get_health_metrics(device_id)
            
            # Workload information (optional)
            if include_workload_info:
                status_info["current_workload"] = await self._get_current_workload(device_id)
            
            # Performance metrics
            status_info["performance"] = await self._get_performance_metrics(device_id)
            
            return create_success_response(status_info)
            
        except Exception as e:
            return create_error_response(
                DeviceErrorCodes.DEVICE_STATUS_FAILED,
                f"Device status retrieval error: {str(e)}",
                device_id=device_id,
                details={"exception": str(e)}
            )

    async def _get_current_device_status(self, device_id: str) -> str:
        """Determine current device status"""
        try:
            # Check if device is accessible
            if not await self._is_device_accessible(device_id):
                return "offline"
            
            # Check if device has active workload
            if await self._has_active_workload(device_id):
                return "busy"
            
            # Check device health
            health_status = await self._check_device_health(device_id)
            if health_status == "error":
                return "error"
            elif health_status == "maintenance":
                return "maintenance"
            
            return "available"
            
        except Exception:
            return "unknown"

    async def _get_status_description(self, device_id: str) -> str:
        """Get detailed status description"""
        try:
            status = await self._get_current_device_status(device_id)
            
            status_descriptions = {
                "available": "Device is ready for inference operations",
                "busy": "Device is currently processing inference requests",
                "offline": "Device is not accessible or has been disconnected",
                "error": "Device has encountered an error and requires attention",
                "maintenance": "Device is undergoing maintenance operations",
                "unknown": "Device status could not be determined"
            }
            
            return status_descriptions.get(status, "Unknown device status")
            
        except Exception:
            return "Status description unavailable"

    async def _get_device_utilization(self, device_id: str) -> Dict[str, float]:
        """Get real-time device utilization metrics"""
        utilization = {
            "cpu_utilization": 0.0,
            "memory_utilization": 0.0,
            "gpu_utilization": 0.0
        }
        
        try:
            target_device = None
            for device in self.devices:
                if device.device_id == device_id:
                    target_device = device
                    break
            
            if not target_device:
                return utilization
            
            # CPU utilization
            if target_device.device_type == DeviceType.CPU:
                utilization["cpu_utilization"] = await self._get_cpu_utilization()
            
            # GPU utilization (DirectML/CUDA)
            elif target_device.device_type == DeviceType.CUDA:
                utilization["gpu_utilization"] = await self._get_gpu_utilization(device_id)
            elif target_device.device_type == DeviceType.DIRECTML:
                utilization["gpu_utilization"] = await self._get_directml_utilization(device_id)
            elif target_device.device_type == DeviceType.MPS:
                utilization["gpu_utilization"] = await self._get_mps_utilization(device_id)
            
            # Memory utilization
            memory_info = await self._get_memory_utilization(device_id)
            utilization["memory_utilization"] = memory_info.get("utilization_percentage", 0.0)
            
            return utilization
            
        except Exception as e:
            self.logger.warning(f"Utilization retrieval error for device {device_id}: {str(e)}")
            return utilization

    async def _get_health_metrics(self, device_id: str) -> Dict[str, Any]:
        """Get device health metrics"""
        try:
            return {
                "temperature_celsius": await self._get_device_temperature(device_id),
                "power_usage_watts": await self._get_power_usage(device_id),
                "error_count": await self._get_error_count(device_id),
                "uptime_seconds": await self._get_uptime(device_id),
                "driver_version": await self._get_driver_version(device_id),
                "firmware_version": await self._get_firmware_version(device_id)
            }
        except Exception as e:
            self.logger.warning(f"Health metrics retrieval error for device {device_id}: {str(e)}")
            return {}

    async def _get_current_workload(self, device_id: str) -> Dict[str, Any]:
        """Get current workload information"""
        try:
            return {
                "active_sessions": await self._get_active_sessions_count(device_id),
                "queued_operations": await self._get_queued_operations_count(device_id),
                "current_operation": await self._get_current_operation(device_id),
                "estimated_completion": await self._get_estimated_completion(device_id),
                "workload_priority": await self._get_workload_priority(device_id)
            }
        except Exception as e:
            self.logger.warning(f"Workload info retrieval error for device {device_id}: {str(e)}")
            return {}

    async def _get_performance_metrics(self, device_id: str) -> Dict[str, Any]:
        """Get device performance metrics"""
        try:
            return {
                "operations_per_second": await self._get_operations_per_second(device_id),
                "average_operation_time": await self._get_average_operation_time(device_id),
                "throughput_mbps": await self._get_throughput_metrics(device_id),
                "latency_ms": await self._get_latency_metrics(device_id),
                "error_rate": await self._get_error_rate(device_id),
                "efficiency_score": await self._get_efficiency_score(device_id)
            }
        except Exception as e:
            self.logger.warning(f"Performance metrics retrieval error for device {device_id}: {str(e)}")
            return {}

    # Helper methods for status monitoring (implement as placeholders for now)
    async def _is_device_accessible(self, device_id: str) -> bool:
        """Check if device is accessible"""
        try:
            for device in self.devices:
                if device.device_id == device_id:
                    return device.is_available
            return False
        except Exception:
            return False

    async def _has_active_workload(self, device_id: str) -> bool:
        """Check if device has active workload"""
        # Placeholder - would check actual device utilization
        return False

    async def _check_device_health(self, device_id: str) -> str:
        """Check device health status"""
        # Placeholder - would perform actual health checks
        return "healthy"

    async def _get_cpu_utilization(self) -> float:
        """Get CPU utilization percentage"""
        try:
            import psutil
            return psutil.cpu_percent(interval=0.1)
        except ImportError:
            return 0.0

    async def _get_gpu_utilization(self, device_id: str) -> float:
        """Get GPU utilization for CUDA devices"""
        try:
            if self._cuda_available:
                # Placeholder - would use nvidia-ml-py or similar
                return 0.0
        except Exception:
            pass
        return 0.0

    async def _get_directml_utilization(self, device_id: str) -> float:
        """Get DirectML device utilization"""
        # Placeholder - DirectML doesn't expose utilization directly
        return 0.0

    async def _get_mps_utilization(self, device_id: str) -> float:
        """Get MPS device utilization"""
        # Placeholder - would use Metal performance counters
        return 0.0

    async def _get_memory_utilization(self, device_id: str) -> Dict[str, float]:
        """Get memory utilization information"""
        try:
            target_device = None
            for device in self.devices:
                if device.device_id == device_id:
                    target_device = device
                    break
            
            if target_device and target_device.device_type == DeviceType.CUDA:
                device_idx = int(device_id.split(":")[-1])
                allocated = torch.cuda.memory_allocated(device_idx)
                total = torch.cuda.get_device_properties(device_idx).total_memory
                utilization_percentage = (allocated / total) * 100
                
                return {
                    "utilization_percentage": utilization_percentage,
                    "allocated_bytes": allocated,
                    "total_bytes": total
                }
            
            # Fallback for other device types
            return {"utilization_percentage": 0.0}
            
        except Exception:
            return {"utilization_percentage": 0.0}

    # Placeholder implementations for additional monitoring methods
    async def _get_device_temperature(self, device_id: str) -> float:
        """Get device temperature"""
        return 0.0  # Placeholder

    async def _get_power_usage(self, device_id: str) -> float:
        """Get device power usage"""
        return 0.0  # Placeholder

    async def _get_error_count(self, device_id: str) -> int:
        """Get device error count"""
        return 0  # Placeholder

    async def _get_uptime(self, device_id: str) -> float:
        """Get device uptime in seconds"""
        return 0.0  # Placeholder

    async def _get_driver_version(self, device_id: str) -> str:
        """Get device driver version"""
        return "unknown"  # Placeholder

    async def _get_firmware_version(self, device_id: str) -> str:
        """Get device firmware version"""
        return "unknown"  # Placeholder

    async def _get_active_sessions_count(self, device_id: str) -> int:
        """Get active sessions count"""
        return 0  # Placeholder

    async def _get_queued_operations_count(self, device_id: str) -> int:
        """Get queued operations count"""
        return 0  # Placeholder

    async def _get_current_operation(self, device_id: str) -> Optional[str]:
        """Get current operation"""
        return None  # Placeholder

    async def _get_estimated_completion(self, device_id: str) -> Optional[str]:
        """Get estimated completion time"""
        return None  # Placeholder

    async def _get_workload_priority(self, device_id: str) -> str:
        """Get workload priority"""
        return "normal"  # Placeholder

    async def _get_operations_per_second(self, device_id: str) -> float:
        """Get operations per second"""
        return 0.0  # Placeholder

    async def _get_average_operation_time(self, device_id: str) -> float:
        """Get average operation time"""
        return 0.0  # Placeholder

    async def _get_throughput_metrics(self, device_id: str) -> float:
        """Get throughput metrics"""
        return 0.0  # Placeholder

    async def _get_latency_metrics(self, device_id: str) -> float:
        """Get latency metrics"""
        return 0.0  # Placeholder

    async def _get_error_rate(self, device_id: str) -> float:
        """Get error rate"""
        return 0.0  # Placeholder

    async def _get_efficiency_score(self, device_id: str) -> float:
        """Get efficiency score"""
        return 0.0  # Placeholder

    def _get_system_memory(self) -> int:
        """Get total system memory in bytes."""
        try:
            import psutil
            return psutil.virtual_memory().total
        except ImportError:
            # Fallback estimation
            return 8 * 1024**3  # 8GB default
    
    def _get_available_memory(self) -> int:
        """Get available system memory in bytes."""
        try:
            import psutil
            return psutil.virtual_memory().available
        except ImportError:
            # Fallback estimation
            return 4 * 1024**3  # 4GB default
    
    def _estimate_directml_memory(self) -> int:
        """Estimate DirectML device memory."""
        # This is a rough estimation since DirectML doesn't expose memory info directly
        system_memory = self._get_system_memory()
        
        # Assume dedicated GPU has at least 4GB, integrated uses system memory
        if platform.machine().lower() in ['amd64', 'x86_64']:
            return min(system_memory // 2, 8 * 1024**3)  # Max 8GB estimation
        else:
            return system_memory // 4  # Conservative estimation for integrated
    
    async def cleanup(self) -> None:
        """Clean up device manager resources."""
        if self.current_device and self.current_device.device_type == DeviceType.CUDA:
            try:
                torch.cuda.empty_cache()
                self.logger.info("CUDA cache cleared")
            except Exception as e:
                self.logger.warning(f"Failed to clear CUDA cache: {str(e)}")
        
        self.devices.clear()
        self.current_device = None
        self.logger.info("Device manager cleaned up")


# Global device manager instance
_device_manager: Optional[DeviceManager] = None


def get_device_manager(config: Optional[Dict[str, Any]] = None) -> DeviceManager:
    """Get the global device manager instance."""
    global _device_manager
    if _device_manager is None:
        _device_manager = DeviceManager(config)
        # Note: Initialization should be done explicitly with await initialize_device_manager()
    return _device_manager


async def initialize_device_manager(config: Optional[Dict[str, Any]] = None) -> bool:
    """Initialize the global device manager."""
    global _device_manager
    _device_manager = DeviceManager(config)
    return await _device_manager.initialize()


async def cleanup_device_manager() -> None:
    """Clean up the global device manager."""
    global _device_manager
    if _device_manager:
        await _device_manager.cleanup()
        _device_manager = None
