#!/usr/bin/env python3
"""
BitingLip Node Manager Emulation Runner
Interactive testing and development tool
"""

import asyncio
import argparse
import json
import sys
from pathlib import Path
from typing import Dict, Any, Optional
import structlog

# Add project paths
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

from emulate import (
    WorkerEmulator,
    TaskEmulator, 
    ScenarioRunner,
    NodeTestClient,
    MockWorkerType
)

logger = structlog.get_logger(__name__)


class EmulationInterface:
    """
    Interactive interface for the emulation system
    """
    
    def __init__(self, node_url: str = "http://localhost:8013"):
        self.node_url = node_url
        self.worker_emulator = WorkerEmulator()
        self.task_emulator = TaskEmulator(self.worker_emulator)
        self.scenario_runner = ScenarioRunner()
        
    async def run_interactive_mode(self):
        """Run interactive emulation mode"""
        print("🚀 BitingLip Node Manager Emulation Interface")
        print("=" * 50)
        
        while True:
            print("\nAvailable Commands:")
            print("1. List scenarios")
            print("2. Run scenario")
            print("3. Test API connection")
            print("4. Submit test task")
            print("5. Create custom worker fleet")
            print("6. Run stress test")
            print("7. Show fleet status")
            print("0. Exit")
            
            try:
                choice = input("\nEnter choice (0-7): ").strip()
                
                if choice == '0':
                    print("Goodbye! 👋")
                    break
                elif choice == '1':
                    await self._list_scenarios()
                elif choice == '2':
                    await self._run_scenario()
                elif choice == '3':
                    await self._test_api_connection()
                elif choice == '4':
                    await self._submit_test_task()
                elif choice == '5':
                    await self._create_fleet()
                elif choice == '6':
                    await self._run_stress_test()
                elif choice == '7':
                    await self._show_fleet_status()
                else:
                    print("Invalid choice. Please try again.")
                    
            except KeyboardInterrupt:
                print("\n\nOperation cancelled. Use '0' to exit cleanly.")
            except Exception as e:
                print(f"Error: {e}")
    
    async def _list_scenarios(self):
        """List available test scenarios"""
        scenarios = self.scenario_runner.list_scenarios()
        
        print("\n📋 Available Test Scenarios:")
        print("-" * 40)
        for i, scenario in enumerate(scenarios, 1):
            print(f"{i}. {scenario['name']}")
            print(f"   Description: {scenario['description']}")
            print(f"   Duration: {scenario['duration_minutes']} minutes")
            print(f"   Workers: {scenario['worker_count']}")
            print(f"   Task Rate: {scenario['task_rate']}/min")
            print()
    
    async def _run_scenario(self):
        """Run a test scenario"""
        scenarios = list(self.scenario_runner.scenarios.keys())
        
        print("\n🏃 Run Test Scenario")
        for i, name in enumerate(scenarios, 1):
            print(f"{i}. {name}")
        
        try:
            choice = int(input("Select scenario number: ")) - 1
            if 0 <= choice < len(scenarios):
                scenario_name = scenarios[choice]
                print(f"\nRunning scenario: {scenario_name}")
                
                result = await self.scenario_runner.run_scenario(scenario_name)
                
                print(f"\n✅ Scenario Results:")
                print(f"Success: {result.get('success', False)}")
                print(f"Duration: {result.get('duration_seconds', 0):.1f}s")
                
                if 'task_execution' in result:
                    stats = result['task_execution']
                    print(f"Tasks: {stats.get('total_tasks', 0)}")
                    print(f"Success Rate: {stats.get('success_rate', 0):.1%}")
                    print(f"Avg Time: {stats.get('average_execution_time', 0):.2f}s")
                
                if result.get('success'):
                    print("🎉 Scenario PASSED!")
                else:
                    print("❌ Scenario FAILED!")
                    
            else:
                print("Invalid choice.")
        except ValueError:
            print("Please enter a valid number.")
    
    async def _test_api_connection(self):
        """Test API connection to node manager"""
        print(f"\n🔌 Testing API Connection to {self.node_url}")
        
        async with NodeTestClient(self.node_url) as client:
            try:
                health_check = await client.run_health_check()
                
                if health_check.get('overall_health') == 'healthy':
                    print("✅ Node Manager is healthy!")
                    print(f"Workers: {len(health_check.get('workers', {}).get('workers', []))}")
                    
                    if 'resources' in health_check:
                        resources = health_check['resources']
                        print(f"CPUs: {resources.get('cpu_cores', 'N/A')}")
                        print(f"Memory: {resources.get('memory_total', 0) // (1024**3)}GB")
                        print(f"GPUs: {len(resources.get('gpus', []))}")
                else:
                    print("❌ Node Manager is unhealthy")
                    print(f"Error: {health_check.get('error', 'Unknown')}")
                    
            except Exception as e:
                print(f"❌ Connection failed: {e}")
    
    async def _submit_test_task(self):
        """Submit a test task to the node manager"""
        print("\n📝 Submit Test Task")
        
        task_types = ['text_generation', 'data_processing', 'text_to_speech']
        
        print("Available task types:")
        for i, task_type in enumerate(task_types, 1):
            print(f"{i}. {task_type}")
        
        try:
            choice = int(input("Select task type: ")) - 1
            if 0 <= choice < len(task_types):
                task_type = task_types[choice]
                
                async with NodeTestClient(self.node_url) as client:
                    result = await client.submit_test_task(task_type)
                    
                    if result.get('final_status') == 'completed':
                        print("✅ Task completed successfully!")
                        print(f"Task ID: {result.get('task_id')}")
                        print(f"Wait Time: {result.get('wait_time')}s")
                    else:
                        print("❌ Task failed or timed out")
                        print(f"Error: {result.get('error', 'Unknown')}")
            else:
                print("Invalid choice.")
        except ValueError:
            print("Please enter a valid number.")
        except Exception as e:
            print(f"Error: {e}")
    
    async def _create_fleet(self):
        """Create a custom worker fleet"""
        print("\n🏭 Create Custom Worker Fleet")
        
        worker_types = list(MockWorkerType)
        fleet_config = {}
        
        for worker_type in worker_types:
            try:
                count = input(f"Number of {worker_type.value} workers (0-10, default 0): ").strip()
                count = int(count) if count else 0
                if 0 <= count <= 10:
                    if count > 0:
                        fleet_config[worker_type] = count
                else:
                    print(f"Invalid count for {worker_type.value}")
            except ValueError:
                print(f"Invalid input for {worker_type.value}")
        
        if fleet_config:
            print(f"\nCreating fleet: {fleet_config}")
            
            fleet = self.worker_emulator.create_worker_fleet(fleet_config)
            start_results = await self.worker_emulator.start_all_workers()
            
            successful = sum(1 for success in start_results.values() if success)
            total = len(start_results)
            
            print(f"✅ Fleet created: {successful}/{total} workers started successfully")
        else:
            print("No workers specified.")
    
    async def _run_stress_test(self):
        """Run a stress test against the API"""
        print("\n⚡ Run Stress Test")
        
        try:
            num_tasks = int(input("Number of concurrent tasks (1-100, default 10): ") or "10")
            if not 1 <= num_tasks <= 100:
                print("Invalid number of tasks.")
                return
            
            async with NodeTestClient(self.node_url) as client:
                print(f"Running stress test with {num_tasks} tasks...")
                
                result = await client.stress_test(num_tasks)
                
                print("\n📊 Stress Test Results:")
                print(f"Total Tasks: {result['total_tasks']}")
                print(f"Successful: {result['successful']}")
                print(f"Failed: {result['failed']}")
                print(f"Success Rate: {result['success_rate']:.1%}")
                print(f"Duration: {result['duration_seconds']:.1f}s")
                print(f"Throughput: {result['tasks_per_second']:.1f} tasks/sec")
                
                if result['errors']:
                    print("\nFirst few errors:")
                    for error in result['errors']:
                        print(f"  - {error}")
                        
        except ValueError:
            print("Please enter a valid number.")
        except Exception as e:
            print(f"Error: {e}")
    
    async def _show_fleet_status(self):
        """Show current fleet status"""
        print("\n📊 Fleet Status")
        
        if not self.worker_emulator.workers:
            print("No workers in fleet. Create a fleet first.")
            return
        
        status = self.worker_emulator.get_fleet_status()
        metrics = self.worker_emulator.get_fleet_metrics()
        
        print(f"Fleet Running: {status['is_running']}")
        print(f"Total Workers: {status['fleet_summary']['total_workers']}")
        print(f"Ready: {status['fleet_summary']['ready']}")
        print(f"Busy: {status['fleet_summary']['busy']}")
        print(f"Error: {status['fleet_summary']['error']}")
        print(f"Stopped: {status['fleet_summary']['stopped']}")
        
        print(f"\nTotal Tasks Executed: {metrics['total_tasks']}")
        print(f"Failed Tasks: {metrics['failed_tasks']}")
        print(f"Success Rate: {metrics['success_rate']:.1%}")


async def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(description="BitingLip Node Manager Emulation Tool")
    parser.add_argument('--node-url', default='http://localhost:8013',
                       help='Node Manager API URL (default: http://localhost:8013)')
    parser.add_argument('--scenario', help='Run specific scenario and exit')
    parser.add_argument('--list-scenarios', action='store_true',
                       help='List available scenarios and exit')
    parser.add_argument('--stress-test', type=int, metavar='N',
                       help='Run stress test with N tasks and exit')
    
    args = parser.parse_args()
      # Setup logging
    structlog.configure(
        processors=[
            structlog.stdlib.add_log_level,
            structlog.processors.TimeStamper(fmt="iso"),
            structlog.processors.JSONRenderer()
        ],
        logger_factory=structlog.stdlib.LoggerFactory()
    )
    
    interface = EmulationInterface(args.node_url)
    
    try:
        if args.list_scenarios:
            scenarios = interface.scenario_runner.list_scenarios()
            print("Available Scenarios:")
            for scenario in scenarios:
                print(f"  {scenario['name']}: {scenario['description']}")
            return
        
        if args.scenario:
            print(f"Running scenario: {args.scenario}")
            result = await interface.scenario_runner.run_scenario(args.scenario)
            
            print(f"Result: {'PASS' if result.get('success') else 'FAIL'}")
            if not result.get('success'):
                sys.exit(1)
            return
        
        if args.stress_test:
            async with NodeTestClient(args.node_url) as client:
                print(f"Running stress test with {args.stress_test} tasks...")
                result = await client.stress_test(args.stress_test)
                
                print(f"Success Rate: {result['success_rate']:.1%}")
                print(f"Throughput: {result['tasks_per_second']:.1f} tasks/sec")
                
                if result['success_rate'] < 0.8:  # 80% threshold
                    sys.exit(1)
            return
        
        # Interactive mode
        await interface.run_interactive_mode()
        
    except KeyboardInterrupt:
        print("\nShutting down...")
    except Exception as e:
        logger.error("Emulation error", error=str(e))
        sys.exit(1)


if __name__ == '__main__':
    asyncio.run(main())
