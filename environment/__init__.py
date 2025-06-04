"""
Environment Management Package
Comprehensive GPU environment setup and management for the Biting Lip platform.

This package provides:
- GPU detection and strategy selection
- Environment planning and creation
- Virtual environment management
- Environment validation
- Orchestrated setup workflows
"""

from .gpu.gpu_detector import GPUDetector
from .gpu.gpu_strategy import GPUStrategyResult, GPUStrategyType, OSType, analyze_gpu_list
from .environment.environment_planner import EnvironmentPlanner
from .environment.venv_manager import VenvManager
from .environment.environment_validator import EnvironmentValidator
from .orchestrator.environment_setup import EnvironmentSetupOrchestrator, SetupSummary

__version__ = "2.0.0"
__author__ = "Biting Lip Platform Team"

__all__ = [
    "GPUDetector",
    "GPUStrategyResult",
    "GPUStrategyType", 
    "OSType",
    "analyze_gpu_list",
    "EnvironmentPlanner",
    "VenvManager",
    "EnvironmentValidator",
    "EnvironmentSetupOrchestrator",
    "SetupSummary"
]
