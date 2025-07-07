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

# Import core components with graceful handling of missing dependencies
try:
    # Always available components
    from .core.base_worker import BaseWorker, WorkerRequest, WorkerResponse, ProcessingError
    from .core.communication import CommunicationManager, MessageProtocol
    
    # Conditionally available components (require torch/ML libraries)
    try:
        from .core.device_manager import get_device_manager, initialize_device_manager
        from .models.model_loader import ModelLoader
        from .inference.sdxl_worker import SDXLWorker
        from .inference.pipeline_manager import PipelineManager
        _ML_COMPONENTS_AVAILABLE = True
    except ImportError as e:
        _ML_COMPONENTS_AVAILABLE = False
        # Create placeholder classes for missing ML components
        def get_device_manager():
            raise ImportError("ML components require torch, diffusers, and other dependencies")
        def initialize_device_manager():
            return False
        class ModelLoader:
            def __init__(self, *args, **kwargs):
                raise ImportError("ModelLoader requires torch and diffusers")
        class SDXLWorker:
            def __init__(self, *args, **kwargs):
                raise ImportError("SDXLWorker requires torch and diffusers")
        class PipelineManager:
            def __init__(self, *args, **kwargs):
                raise ImportError("PipelineManager requires torch and diffusers")

except ImportError:
    # Fallback for direct execution without proper package structure
    import sys
    from pathlib import Path
    workers_dir = Path(__file__).parent
    sys.path.insert(0, str(workers_dir))
    
    # Try absolute imports
    try:
        from core.base_worker import BaseWorker, WorkerRequest, WorkerResponse, ProcessingError
        from core.communication import CommunicationManager, MessageProtocol
        
        # ML components with conditional import
        try:
            from core.device_manager import get_device_manager, initialize_device_manager
            from models.model_loader import ModelLoader
            from inference.sdxl_worker import SDXLWorker
            from inference.pipeline_manager import PipelineManager
            _ML_COMPONENTS_AVAILABLE = True
        except ImportError:
            _ML_COMPONENTS_AVAILABLE = False
            def get_device_manager():
                raise ImportError("ML components require torch, diffusers, and other dependencies")
            def initialize_device_manager():
                return False
            class ModelLoader:
                def __init__(self, *args, **kwargs):
                    raise ImportError("ModelLoader requires torch and diffusers")
            class SDXLWorker:
                def __init__(self, *args, **kwargs):
                    raise ImportError("SDXLWorker requires torch and diffusers")
            class PipelineManager:
                def __init__(self, *args, **kwargs):
                    raise ImportError("PipelineManager requires torch and diffusers")
    except ImportError as e:
        # If even basic imports fail, provide minimal interface
        class BaseWorker:
            def __init__(self, *args, **kwargs):
                raise ImportError(f"Workers package not properly configured: {e}")
        class WorkerRequest:
            pass
        class WorkerResponse:
            pass
        class ProcessingError(Exception):
            pass
        class CommunicationManager:
            def __init__(self, *args, **kwargs):
                raise ImportError(f"Workers package not properly configured: {e}")
        class MessageProtocol:
            pass
        _ML_COMPONENTS_AVAILABLE = False

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
