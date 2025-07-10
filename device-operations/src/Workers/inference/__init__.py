"""
Inference Package for SDXL Workers System
==========================================

This package contains inference components including workers, managers,
and the unified inference interface.
"""

from .interface_inference import InferenceInterface, create_inference_interface

# Managers
from .managers.manager_batch import BatchManager
from .managers.manager_pipeline import PipelineManager  
from .managers.manager_memory import MemoryManager

# Workers
from .workers.worker_sdxl import SDXLWorker
from .workers.worker_controlnet import ControlNetWorker
from .workers.worker_lora import LoRAWorker

__all__ = [
    "InferenceInterface",
    "create_inference_interface",
    "BatchManager", 
    "PipelineManager",
    "MemoryManager",
    "SDXLWorker",
    "ControlNetWorker",
    "LoRAWorker"
]
