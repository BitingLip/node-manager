"""
SDXL Workers System
===================

Modular SDXL inference workers with advanced controls for model components,
schedulers, hyperparameters, conditioning modules, precision settings,
and output processing.

This package provides a comprehensive system for SDXL inference with:
- Modular architecture with specialized workers
- Advanced model management with caching
- LoRA and textual inversion support
- Multiple scheduler options with custom configurations
- DirectML, CUDA, and CPU backend support
- Memory optimization and device management
- JSON-based communication protocol
- Streaming responses and progress tracking
- Batch processing and multi-stage workflows
"""

from .core import *
from .models import *
from .schedulers import *
from .inference import *

__version__ = "1.0.0"
__author__ = "SDXL Workers Development Team"

__all__ = [
    # Core components
    "BaseWorker",
    "WorkerRequest",
    "WorkerResponse", 
    "DeviceManager",
    "CommunicationManager",
    
    # Model management
    "ModelLoader",
    "LoRAManager",
    
    # Schedulers
    "SchedulerFactory",
    
    # Inference workers
    "SDXLWorker",
    "PipelineManager"
]
