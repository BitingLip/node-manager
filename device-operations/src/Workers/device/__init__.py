"""
Device Management Package for SDXL Workers System
=================================================

This package provides device management capabilities including device detection,
initialization, and optimization for DirectML, CUDA, and CPU backends.
"""

from .interface_device import DeviceInterface
from .managers.manager_device import DeviceManager

__all__ = [
    "DeviceInterface",
    "DeviceManager"
]