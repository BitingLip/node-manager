"""
Model Management Package for SDXL Workers System
===============================================

This package provides model management capabilities in the new hierarchical structure
with interface/manager/worker pattern.
"""

from .interface_model import ModelInterface, create_model_interface
from .managers import (
    VAEManager,
    EncoderManager,
    UNetManager,
    TokenizerManager,
    LoRAManager
)
from .workers import MemoryWorker

__all__ = [
    "ModelInterface",
    "create_model_interface",
    "VAEManager",
    "EncoderManager",
    "UNetManager", 
    "TokenizerManager",
    "LoRAManager",
    "MemoryWorker"
]