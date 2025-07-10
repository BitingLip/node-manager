"""
Communication Management Package for SDXL Workers System
========================================================

This package provides communication management capabilities including
message passing, protocol handling, and worker coordination.
"""

from .interface_communication import CommunicationInterface
from .managers.manager_communication import CommunicationManager

__all__ = [
    "CommunicationInterface",
    "CommunicationManager"
]