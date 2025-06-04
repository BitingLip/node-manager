"""
Environment Management Core Module
Provides environment planning, virtual environment management, and validation.
"""

from .environment_planner import EnvironmentPlanner, EnvironmentSpec, EnvironmentSetupResult
from .venv_manager import VenvManager, VenvInfo
from .environment_validator import EnvironmentValidator, EnvironmentValidationResult

__all__ = [
    "EnvironmentPlanner",
    "EnvironmentSpec",
    "EnvironmentSetupResult",
    "VenvManager", 
    "VenvInfo",
    "EnvironmentValidator",
    "EnvironmentValidationResult"
]
