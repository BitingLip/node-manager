#!/usr/bin/env python3
"""
Quick Demo of Node Manager Emulation
Shows the emulation system in action
"""

import asyncio
import sys
from pathlib import Path

# Add project paths
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from emulate import WorkerEmulator, TaskEmulator, MockWorkerType
from core.task_dispatcher import TaskPriority
import structlog

logger = structlog.get_logger(__name__)


async def demo_basic_emulation():
    """Demonstrate basic emulation functionality"""
    print("🚀 BitingLip Node Manager Emulation Demo")
    print("=" * 50)
    
    # Create emulators
    worker_emulator = WorkerEmulator()
    task_emulator = TaskEmulator(worker_emulator)
    
    print("\n1. Creating worker fleet...")
    fleet_config = {
        MockWorkerType.LLM_SMALL.value: 2,
        MockWorkerType.STABLE_DIFFUSION.value: 1,
        MockWorkerType.TTS_FAST.value: 1,
        MockWorkerType.GENERIC.value: 2
    }
    
    fleet = worker_emulator.create_worker_fleet(fleet_config)
    print(f"   Created {sum(len(workers) for workers in fleet.values())} workers")
    
    print("\n2. Starting all workers...")
    start_results = await worker_emulator.start_all_workers()
    successful_starts = sum(1 for success in start_results.values() if success)
    print(f"   {successful_starts}/{len(start_results)} workers started successfully")
    
    print("\n3. Creating test tasks...")
    tasks = task_emulator.create_task_batch(8, ['text_generation', 'text_to_image', 'text_to_speech'])
    print(f"   Created {len(tasks)} test tasks")
    
    print("\n4. Executing tasks...")
    results = await task_emulator.execute_task_batch(tasks)
    successful_tasks = sum(1 for result in results if result.get('success'))
    print(f"   {successful_tasks}/{len(results)} tasks completed successfully")
    print("\n5. Fleet status:")
    status = worker_emulator.get_fleet_status()
    fleet_summary = status['fleet_summary']
    print(f"   Ready: {fleet_summary['ready']}")
    print(f"   Busy: {fleet_summary['busy']}")
    print(f"   Error: {fleet_summary['error']}")
    
    print("\n6. Task statistics:")
    task_stats = task_emulator.get_task_stats()
    print(f"   Total tasks: {task_stats['total_tasks']}")
    print(f"   Success rate: {task_stats['success_rate']:.1%}")
    print(f"   Avg execution time: {task_stats['average_execution_time']:.2f}s")
    
    print("\n7. Running continuous load test (1 minute)...")
    load_stats = await task_emulator.run_continuous_load(
        tasks_per_minute=20,
        duration_minutes=1  # 1 minute
    )
    print(f"   Executed {load_stats['total_tasks']} tasks")
    print(f"   Success rate: {load_stats['success_rate']:.1%}")
    print(f"   Throughput: {load_stats['tasks_per_minute_actual']:.1f} tasks/min")
    
    print("\n8. Stopping workers...")
    stop_results = await worker_emulator.stop_all_workers()
    successful_stops = sum(1 for success in stop_results.values() if success)
    print(f"   {successful_stops}/{len(stop_results)} workers stopped successfully")
    
    print("\n✅ Demo completed!")
    print("\nNext steps:")
    print("- Run 'python emulate/run_emulation.py' for interactive mode")
    print("- Try different scenarios with '--scenario smoke_test'")
    print("- Test API connection with '--stress-test 10'")


async def demo_scenario_runner():
    """Demonstrate scenario runner"""
    print("\n" + "=" * 50)
    print("🎯 Scenario Runner Demo")
    print("=" * 50)
    
    from emulate import ScenarioRunner
    
    runner = ScenarioRunner()
    
    print("\nAvailable scenarios:")
    scenarios = runner.list_scenarios()
    for scenario in scenarios:
        print(f"  - {scenario['name']}: {scenario['description']}")
    
    print("\nRunning smoke test scenario...")
    result = await runner.run_scenario('smoke_test')
    
    print(f"\nResults:")
    print(f"Success: {result.get('success')}")
    print(f"Duration: {result.get('duration_seconds', 0):.1f}s")
    
    if 'task_execution' in result:
        stats = result['task_execution']
        print(f"Tasks executed: {stats.get('total_tasks', 0)}")
        print(f"Success rate: {stats.get('success_rate', 0):.1%}")


if __name__ == '__main__':
    # Setup basic logging
    structlog.configure(
        processors=[
            structlog.stdlib.add_log_level,
            structlog.processors.TimeStamper(fmt="iso"),
            structlog.processors.JSONRenderer()
        ],
        logger_factory=structlog.stdlib.LoggerFactory()
    )
    
    try:
        asyncio.run(demo_basic_emulation())
        asyncio.run(demo_scenario_runner())
    except KeyboardInterrupt:
        print("\nDemo interrupted!")
    except Exception as e:
        print(f"\nDemo error: {e}")
        sys.exit(1)
