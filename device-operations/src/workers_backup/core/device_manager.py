"""
Device Management Module for DirectML Workers
=============================================

Handles device detection, initialization, and management for DirectML-based SDXL inference.
Provides optimized device selection and memory management capabilities.
"""

import logging
import torch
import platform
from typing import Dict, Any, List, Optional, Tuple
from dataclasses import dataclass
from enum import Enum


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
    
    def __init__(self):
        self.logger = logging.getLogger(__name__)
        self.devices: List[DeviceInfo] = []
        self.current_device: Optional[DeviceInfo] = None
        self._directml_available = False
        self._cuda_available = False
        self._mps_available = False
        
    def initialize(self) -> bool:
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
            return False
    
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
    
    def get_device_info(self) -> Optional[DeviceInfo]:
        """Get information about the current device."""
        return self.current_device
    
    def list_devices(self) -> List[DeviceInfo]:
        """List all detected devices."""
        return self.devices.copy()
    
    def set_device(self, device_id: str) -> bool:
        """
        Set the current device by ID.
        
        Args:
            device_id: Device identifier
            
        Returns:
            True if device was set successfully
        """
        for device in self.devices:
            if device.device_id == device_id and device.is_available:
                self.current_device = device
                self.logger.info(f"Device set to: {device.name}")
                return True
        
        self.logger.error(f"Device {device_id} not found or not available")
        return False
    
    def get_memory_info(self) -> Dict[str, Any]:
        """
        Get memory information for the current device.
        
        Returns:
            Dictionary with memory statistics
        """
        if not self.current_device:
            return {"error": "No device selected"}
        
        device_type = self.current_device.device_type
        
        if device_type == DeviceType.CUDA:
            try:
                device_idx = int(self.current_device.device_id.split(":")[-1])
                return {
                    "total": torch.cuda.get_device_properties(device_idx).total_memory,
                    "allocated": torch.cuda.memory_allocated(device_idx),
                    "reserved": torch.cuda.memory_reserved(device_idx),
                    "free": torch.cuda.get_device_properties(device_idx).total_memory - torch.cuda.memory_reserved(device_idx)
                }
            except Exception as e:
                return {"error": f"Failed to get CUDA memory info: {str(e)}"}
        
        elif device_type == DeviceType.DIRECTML:
            return {
                "total": self.current_device.memory_total,
                "available": self.current_device.memory_available,
                "estimated": True
            }
        
        else:
            return {
                "total": self.current_device.memory_total,
                "available": self.current_device.memory_available
            }
    
    def optimize_memory_settings(self) -> Dict[str, Any]:
        """
        Get optimized memory settings for the current device.
        
        Returns:
            Dictionary with recommended memory settings
        """
        if not self.current_device:
            return {}
        
        device_type = self.current_device.device_type
        memory_gb = self.current_device.memory_total / (1024**3)
        
        settings = {
            "attention_slicing": True,
            "vae_slicing": True,
            "cpu_offload": False,
            "sequential_cpu_offload": False
        }
        
        # Adjust settings based on device type and memory
        if device_type == DeviceType.CPU:
            settings.update({
                "cpu_offload": False,
                "sequential_cpu_offload": False,
                "attention_slicing": True,
                "vae_slicing": True
            })
        
        elif device_type == DeviceType.DIRECTML:
            if memory_gb < 8:
                settings.update({
                    "cpu_offload": True,
                    "sequential_cpu_offload": True
                })
            elif memory_gb < 12:
                settings.update({
                    "cpu_offload": False,
                    "sequential_cpu_offload": False,
                    "attention_slicing": True
                })
        
        elif device_type in [DeviceType.CUDA, DeviceType.MPS]:
            if memory_gb < 8:
                settings.update({
                    "cpu_offload": True,
                    "sequential_cpu_offload": True
                })
            elif memory_gb >= 16:
                settings.update({
                    "attention_slicing": False,
                    "vae_slicing": False
                })
        
        return settings
    
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
    
    def cleanup(self) -> None:
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
_device_manager = None


def get_device_manager() -> DeviceManager:
    """Get the global device manager instance."""
    global _device_manager
    if _device_manager is None:
        _device_manager = DeviceManager()
        _device_manager.initialize()
    return _device_manager


def initialize_device_manager() -> bool:
    """Initialize the global device manager."""
    global _device_manager
    _device_manager = DeviceManager()
    return _device_manager.initialize()


def cleanup_device_manager() -> None:
    """Clean up the global device manager."""
    global _device_manager
    if _device_manager:
        _device_manager.cleanup()
        _device_manager = None
