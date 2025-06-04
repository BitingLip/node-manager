"""
Test script for environment setup orchestrator
"""

import asyncio
import sys
from pathlib import Path

# Add the project root directory to path
project_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(project_root))

# Import the orchestrator directly
from managers.node_manager.environment.orchestrator import EnvironmentSetupOrchestrator


async def main():
    print("\nTesting Environment Setup Orchestrator")
    print("=" * 50)
    
    orchestrator = EnvironmentSetupOrchestrator()
    
    # First, get current status
    status = orchestrator.get_status()
    print(f"Status: {status}")
    
    # Run setup
    summary = await orchestrator.setup_all()
    
    print(f"\nSetup Summary:")
    print(f"Total GPUs: {summary.total_gpus}")
    print(f"Environments created: {summary.environments_created}")
    print(f"Environments successful: {summary.environments_successful}")
    print(f"Setup time: {summary.setup_time_seconds:.2f}s")
    
    if summary.gpu_assignments:
        print(f"\nGPU Assignments:")
        for env, gpus in summary.gpu_assignments.items():
            print(f"{env}: {', '.join(gpus) if gpus else 'CPU-only'}")
    
    if summary.warnings:
        print(f"\nWarnings:")
        for warning in summary.warnings:
            print(f"- {warning}")
    
    if summary.errors:
        print(f"\nErrors:")
        for error in summary.errors:
            print(f"- {error}")


if __name__ == "__main__":
    asyncio.run(main())
