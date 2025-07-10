"""
Inference Managers Package for SDXL Workers System
==================================================

This package contains inference managers that handle batch processing,
pipeline lifecycle management, and memory optimization.
"""

from .manager_batch import BatchManager
from .manager_pipeline import PipelineManager
from .manager_memory import MemoryManager

__all__ = [
    "BatchManager",
    "PipelineManager",
    "MemoryManager"
]