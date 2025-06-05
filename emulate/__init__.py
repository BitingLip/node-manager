"""
BitingLip Node Manager Emulation Suite
Flexible testing and development framework for worker management
"""

__version__ = "1.0.0"

from .emulator import WorkerEmulator, TaskEmulator
from .scenarios import ScenarioRunner, Scenario
from .mock_worker import MockWorker, MockWorkerType
from .test_client import NodeTestClient

__all__ = [
    'WorkerEmulator',
    'TaskEmulator', 
    'ScenarioRunner',
    'Scenario',
    'MockWorker',
    'MockWorkerType',
    'NodeTestClient'
]
