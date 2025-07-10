"""
Instructors Package for SDXL Workers System
==========================================

This package contains instructor classes that coordinate different domains
of the workers system. Each instructor manages a specific aspect of the
system and delegates to appropriate managers and workers.
"""

from .instructor_device import DeviceInstructor
from .instructor_communication import CommunicationInstructor
from .instructor_model import ModelInstructor
from .instructor_conditioning import ConditioningInstructor
from .instructor_inference import InferenceInstructor
from .instructor_scheduler import SchedulerInstructor
from .instructor_postprocessing import PostprocessingInstructor

__all__ = [
    "DeviceInstructor",
    "CommunicationInstructor",
    "ModelInstructor",
    "ConditioningInstructor",
    "InferenceInstructor",
    "SchedulerInstructor",
    "PostprocessingInstructor"
]