"""
Test Scenarios for Node Manager Emulation
Pre-defined test scenarios for comprehensive testing
"""

import asyncio
import random
from typing import Dict, List, Optional, Any, Callable
from datetime import datetime, timedelta
from dataclasses import dataclass
import structlog

from .emulator import WorkerEmulator, TaskEmulator
from .mock_worker import MockWorkerType
from core.task_dispatcher import TaskPriority

logger = structlog.get_logger(__name__)


@dataclass
class Scenario:
    """Test scenario definition"""
    name: str
    description: str
    duration_minutes: int
    fleet_config: Dict[MockWorkerType, int]
    task_config: Dict[str, Any]
    expected_outcomes: Dict[str, Any]
    setup_func: Optional[Callable] = None
    teardown_func: Optional[Callable] = None


class ScenarioRunner:
    """
    Runs predefined test scenarios against the node manager
    """
    
    def __init__(self, node_manager=None):
        self.node_manager = node_manager
        self.worker_emulator = WorkerEmulator(node_manager)
        self.task_emulator = TaskEmulator(self.worker_emulator)
        self.scenarios = self._create_builtin_scenarios()
        
    def _create_builtin_scenarios(self) -> Dict[str, Scenario]:
        """Create built-in test scenarios"""
        scenarios = {}
        
        # Basic smoke test
        scenarios['smoke_test'] = Scenario(
            name="Basic Smoke Test",
            description="Quick test to verify basic functionality",
            duration_minutes=2,
            fleet_config={
                MockWorkerType.LLM_SMALL: 1,
                MockWorkerType.GENERIC: 1
            },
            task_config={
                'task_types': ['text_generation', 'data_processing'],
                'task_rate': 5,  # tasks per minute
                'batch_size': 3
            },
            expected_outcomes={
                'min_success_rate': 0.9,
                'max_avg_execution_time': 10.0
            }
        )
        
        # Balanced load test
        scenarios['balanced_load'] = Scenario(
            name="Balanced Load Test",
            description="Test with balanced worker types and moderate load",
            duration_minutes=5,
            fleet_config={
                MockWorkerType.LLM_SMALL: 2,
                MockWorkerType.LLM_LARGE: 1,
                MockWorkerType.STABLE_DIFFUSION: 1,
                MockWorkerType.TTS_FAST: 1,
                MockWorkerType.GENERIC: 2
            },
            task_config={
                'task_types': ['text_generation', 'text_to_image', 'text_to_speech', 'data_processing'],
                'task_rate': 15,
                'batch_size': 5,
                'priority_distribution': {
                    TaskPriority.LOW: 0.3,
                    TaskPriority.NORMAL: 0.5,
                    TaskPriority.HIGH: 0.2
                }
            },
            expected_outcomes={
                'min_success_rate': 0.85,
                'max_avg_execution_time': 20.0,
                'min_worker_utilization': 0.6
            }
        )
        
        # High load stress test
        scenarios['stress_test'] = Scenario(
            name="High Load Stress Test",
            description="Heavy load to test system limits and error handling",
            duration_minutes=10,
            fleet_config={
                MockWorkerType.LLM_SMALL: 3,
                MockWorkerType.LLM_LARGE: 2,
                MockWorkerType.STABLE_DIFFUSION: 2,
                MockWorkerType.TTS_FAST: 2,
                MockWorkerType.IMAGE_TO_TEXT: 1,
                MockWorkerType.GENERIC: 4
            },
            task_config={
                'task_types': ['text_generation', 'text_to_image', 'text_to_speech', 'image_to_text', 'data_processing'],
                'task_rate': 50,
                'batch_size': 10,
                'priority_distribution': {
                    TaskPriority.LOW: 0.2,
                    TaskPriority.NORMAL: 0.4,
                    TaskPriority.HIGH: 0.3,
                    TaskPriority.URGENT: 0.1
                }
            },
            expected_outcomes={
                'min_success_rate': 0.75,
                'max_avg_execution_time': 30.0
            }
        )
        
        # Worker failure simulation
        scenarios['failure_recovery'] = Scenario(
            name="Worker Failure Recovery Test",
            description="Test system behavior when workers fail and recover",
            duration_minutes=8,
            fleet_config={
                MockWorkerType.LLM_SMALL: 3,
                MockWorkerType.STABLE_DIFFUSION: 2,
                MockWorkerType.GENERIC: 2
            },
            task_config={
                'task_types': ['text_generation', 'text_to_image', 'data_processing'],
                'task_rate': 20,
                'batch_size': 5,
                'simulate_failures': True,
                'failure_rate': 0.15  # Higher failure rate
            },
            expected_outcomes={
                'min_success_rate': 0.70,  # Lower due to failures
                'recovery_time_max': 30.0  # seconds
            }
        )
        
        # Resource constraint test
        scenarios['resource_constraint'] = Scenario(
            name="Resource Constraint Test",
            description="Test with limited workers and high demand",
            duration_minutes=6,
            fleet_config={
                MockWorkerType.LLM_LARGE: 1,  # Only one heavy worker
                MockWorkerType.GENERIC: 1
            },
            task_config={
                'task_types': ['text_generation'],  # Only heavy tasks
                'task_rate': 30,  # High demand
                'batch_size': 8
            },
            expected_outcomes={
                'min_success_rate': 0.80,
                'queue_buildup_expected': True
            }
        )
        
        # Mixed priority test
        scenarios['priority_handling'] = Scenario(
            name="Priority Handling Test",
            description="Test task prioritization and queue management",
            duration_minutes=7,
            fleet_config={
                MockWorkerType.LLM_SMALL: 2,
                MockWorkerType.TTS_FAST: 1,
                MockWorkerType.GENERIC: 2
            },
            task_config={
                'task_types': ['text_generation', 'text_to_speech', 'data_processing'],
                'task_rate': 25,
                'batch_size': 6,
                'priority_distribution': {
                    TaskPriority.LOW: 0.1,
                    TaskPriority.NORMAL: 0.3,
                    TaskPriority.HIGH: 0.4,
                    TaskPriority.URGENT: 0.2
                }
            },
            expected_outcomes={
                'min_success_rate': 0.85,
                'urgent_task_avg_wait_time_max': 5.0  # seconds
            }
        )
        
        return scenarios
    
    def add_custom_scenario(self, scenario: Scenario):
        """Add a custom test scenario"""
        self.scenarios[scenario.name.lower().replace(' ', '_')] = scenario
        logger.info("Added custom scenario", name=scenario.name)
    
    async def run_scenario(self, scenario_name: str) -> Dict[str, Any]:
        """Run a specific test scenario"""
        if scenario_name not in self.scenarios:
            raise ValueError(f"Unknown scenario: {scenario_name}")
        
        scenario = self.scenarios[scenario_name]
        logger.info("Starting test scenario", 
                   name=scenario.name,
                   duration=scenario.duration_minutes)
        
        start_time = datetime.now()
        
        try:
            # Setup
            if scenario.setup_func:
                await scenario.setup_func()
              # Create worker fleet
            fleet_config_converted = {worker_type.value: count for worker_type, count in scenario.fleet_config.items()}
            fleet = self.worker_emulator.create_worker_fleet(fleet_config_converted)
            
            # Start all workers
            start_results = await self.worker_emulator.start_all_workers()
            failed_starts = sum(1 for success in start_results.values() if not success)
            
            if failed_starts > 0:
                logger.warning("Some workers failed to start", 
                             failed_count=failed_starts,
                             total=len(start_results))
            
            # Configure task emulator
            task_config = scenario.task_config
            
            # Run the test load
            if 'simulate_failures' in task_config:
                # Set higher error rates for failure simulation
                for worker in self.worker_emulator.workers.values():
                    worker.error_rate = task_config.get('failure_rate', 0.15)
            
            # Execute test scenario
            test_stats = await self.task_emulator.run_continuous_load(
                tasks_per_minute=task_config['task_rate'],
                duration_minutes=scenario.duration_minutes
            )
            
            # Collect final metrics
            worker_metrics = self.worker_emulator.get_fleet_metrics()
            worker_status = self.worker_emulator.get_fleet_status()
            task_stats = self.task_emulator.get_task_stats()
            
            # Stop workers
            stop_results = await self.worker_emulator.stop_all_workers()
            
            # Cleanup
            if scenario.teardown_func:
                await scenario.teardown_func()
            
            # Calculate results
            end_time = datetime.now()
            duration = (end_time - start_time).total_seconds()
            
            results = {
                'scenario': scenario.name,
                'duration_seconds': duration,
                'worker_startup': {
                    'total_workers': len(start_results),
                    'successful_starts': sum(1 for s in start_results.values() if s),
                    'failed_starts': failed_starts
                },
                'task_execution': test_stats,
                'worker_metrics': worker_metrics,
                'worker_status': worker_status,
                'task_statistics': task_stats,
                'expected_outcomes': scenario.expected_outcomes,
                'success': self._evaluate_scenario(scenario, test_stats, worker_metrics, task_stats)
            }
            
            logger.info("Test scenario completed", 
                       name=scenario.name,
                       duration=duration,
                       success=results['success'])
            
            return results
            
        except Exception as e:
            logger.error("Test scenario failed", 
                        name=scenario.name,
                        error=str(e))
            
            # Try to cleanup
            try:
                await self.worker_emulator.stop_all_workers()
            except:
                pass
            
            return {
                'scenario': scenario.name,
                'duration_seconds': (datetime.now() - start_time).total_seconds(),
                'error': str(e),
                'success': False
            }
    
    def _evaluate_scenario(self, scenario: Scenario, test_stats: Dict, 
                          worker_metrics: Dict, task_stats: Dict) -> bool:
        """Evaluate if scenario meets expected outcomes"""
        expected = scenario.expected_outcomes
        success = True
        
        # Check success rate
        if 'min_success_rate' in expected:
            actual_rate = test_stats.get('success_rate', 0)
            if actual_rate < expected['min_success_rate']:
                logger.warning("Success rate below expected", 
                             actual=actual_rate,
                             expected=expected['min_success_rate'])
                success = False
        
        # Check execution time
        if 'max_avg_execution_time' in expected:
            actual_time = test_stats.get('average_execution_time', 0)
            if actual_time > expected['max_avg_execution_time']:
                logger.warning("Execution time above expected",
                             actual=actual_time,
                             expected=expected['max_avg_execution_time'])
                success = False
        
        # Check worker utilization
        if 'min_worker_utilization' in expected:
            # Calculate utilization based on worker metrics
            # This would need to be implemented based on actual metrics
            pass
        
        return success
    
    async def run_all_scenarios(self) -> Dict[str, Dict[str, Any]]:
        """Run all available scenarios"""
        logger.info("Running all test scenarios", count=len(self.scenarios))
        
        results = {}
        for scenario_name in self.scenarios.keys():
            logger.info("Running scenario", name=scenario_name)
            results[scenario_name] = await self.run_scenario(scenario_name)
            
            # Brief pause between scenarios
            await asyncio.sleep(2)
        
        # Summary
        successful = sum(1 for result in results.values() if result.get('success', False))
        total = len(results)
        
        logger.info("All scenarios completed",
                   successful=successful,
                   total=total,
                   success_rate=successful/total)
        
        return results
    
    def list_scenarios(self) -> List[Dict[str, Any]]:
        """List all available scenarios"""
        return [
            {
                'name': scenario.name,
                'description': scenario.description,
                'duration_minutes': scenario.duration_minutes,
                'worker_count': sum(scenario.fleet_config.values()),
                'task_rate': scenario.task_config.get('task_rate', 'N/A')
            }
            for scenario in self.scenarios.values()
        ]
