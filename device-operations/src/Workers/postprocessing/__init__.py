"""
Post-processing Package for SDXL Workers System
===============================================

This package contains the complete post-processing pipeline with interface,
managers, and workers for image upscaling, enhancement, and safety checking.
"""

from .interface_postprocessing import PostprocessingInterface, create_postprocessing_interface
from .managers.manager_postprocessing import PostprocessingManager, create_postprocessing_manager
from .workers import (
    UpscalerWorker,
    ImageEnhancerWorker,
    SafetyCheckerWorker,
    create_upscaler_worker,
    create_image_enhancer_worker,
    create_safety_checker_worker
)

__all__ = [
    # Interface
    "PostprocessingInterface",
    "create_postprocessing_interface",
    
    # Managers
    "PostprocessingManager",
    "create_postprocessing_manager",
    
    # Workers
    "UpscalerWorker",
    "ImageEnhancerWorker",
    "SafetyCheckerWorker",
    "create_upscaler_worker",
    "create_image_enhancer_worker",
    "create_safety_checker_worker"
]