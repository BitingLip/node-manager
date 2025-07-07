"""
Inference Workers Package
=========================

SDXL inference workers and pipeline management components.
"""

from .sdxl_worker import SDXLWorker
from .pipeline_manager import (
    PipelineManager,
    PipelineTask
)

__all__ = [
    "SDXLWorker",
    "PipelineManager",
    "PipelineTask"
]
