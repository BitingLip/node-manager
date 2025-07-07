"""
Inference Workers Package
=========================

SDXL inference workers and pipeline management components.
"""

from inference.sdxl_worker import SDXLWorker
from inference.pipeline_manager import (
    PipelineManager,
    PipelineTask
)

__all__ = [
    "SDXLWorker",
    "PipelineManager",
    "PipelineTask"
]
