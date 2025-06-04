"""
Environment Setup Orchestrator
Main implementation using unified GPU strategy approach.
This is the canonical orchestrator for the Biting Lip platform.
"""

import asyncio
import time
import sys
import json
from typing import Dict, List, Optional, Any
from pathlib import Path
import structlog

# Handle imports based on how the module is being used
try:
    # When imported as part of the package structure
    from ..gpu.gpu_detector import GPUDetector, EnvironmentRequirement, GPUInfo, GPUVendor
    from ..gpu.gpu_strategy import analyze_gpu_list, get_strategy_requirements
    from ..environment.environment_planner import EnvironmentPlanner, EnvironmentSpec, EnvironmentType, FrameworkType
    from ..environment.venv_manager import VenvManager, VenvInfo
    from ..environment.environment_validator import EnvironmentValidator, EnvironmentValidationResult
except ImportError:
    # When running as a script or fallback
    import sys
    from pathlib import Path
    
    # Add the parent directory to path to enable direct imports
    parent_dir = Path(__file__).resolve().parent.parent
    if str(parent_dir) not in sys.path:
        sys.path.append(str(parent_dir))
    
    from gpu.gpu_detector import GPUDetector, EnvironmentRequirement, GPUInfo, GPUVendor
    from gpu.gpu_strategy import analyze_gpu_list, get_strategy_requirements
    from environment.environment_planner import EnvironmentPlanner, EnvironmentSpec, EnvironmentType, FrameworkType
    from environment.venv_manager import VenvManager, VenvInfo
    from environment.environment_validator import EnvironmentValidator, EnvironmentValidationResult

logger = structlog.get_logger(__name__)


class SetupSummary:
    """
    Summary of the GPU environment setup process
    Provides information about created environments and validation results
    """
    def __init__(self, total_gpus, env_created, env_successful, time_s, gpu_assignments, val_results, warnings, errors):
        self.total_gpus = total_gpus
        self.environments_created = env_created
        self.environments_successful = env_successful
        self.setup_time_seconds = time_s
        self.gpu_assignments = gpu_assignments
        self.validation_results = val_results
        self.warnings = warnings
        self.errors = errors
        
    def to_dict(self) -> Dict[str, Any]:
        """Convert summary to dictionary for serialization"""
        return {
            "total_gpus": self.total_gpus,
            "environments_created": self.environments_created,
            "environments_successful": self.environments_successful,
            "setup_time_seconds": self.setup_time_seconds,
            "gpu_assignments": self.gpu_assignments,
            "warnings": self.warnings,
            "errors": self.errors
        }


class EnvironmentSetupOrchestrator:
    """
    Main orchestrator that follows the unified strategy pattern:
    detect ? analyze ? plan ? create ? validate
    
    This orchestrator handles the full lifecycle of environment setup:
    1. Detecting available GPUs
    2. Analyzing GPU strategy based on vendor mix
    3. Building environment requirements
    4. Planning environments by grouping compatible hardware
    5. Creating virtual environments with appropriate packages
    6. Validating environments with appropriate test scripts
    """

    def __init__(self, base_path: Optional[Path] = None):
        """Initialize the orchestrator with path for environments"""
        self.base_path = base_path or (Path.cwd() / "node_environments")
        self.base_path.mkdir(exist_ok=True)

        # Core components
        self.gpu_detector = GPUDetector()
        self.planner = EnvironmentPlanner(self.base_path / "specs")
        self.venv_mgr = VenvManager(self.base_path / "venvs")
        self.validator = EnvironmentValidator()
        
        logger.info("EnvironmentSetupOrchestrator initialized", base_path=str(self.base_path))

    async def setup_all(self, force_recreate: bool = False) -> SetupSummary:
        """
        Run complete environment setup pipeline
        
        Args:
            force_recreate: If True, recreate environments even if they exist
            
        Returns:
            SetupSummary: Summary of the setup process
        """
        start = time.time()
        errors: List[str] = []
        warnings: List[str] = []
        gpu_assignments: Dict[str, List[str]] = {}
        val_results: Dict[str, EnvironmentValidationResult] = {}
        specs_map = {}
        created_results = {}
        total_gpus = 0

        try:
            # Step 1: Detect GPUs
            logger.info("Step 1: Detecting GPUs")
            detected_gpus = self.gpu_detector.detect_all_gpus()
            total_gpus = len(detected_gpus)
            
            if total_gpus == 0:
                warnings.append("No GPUs detected - creating CPU-only fallback")
                logger.warning("No GPUs detected")

            # Step 2: Analyze GPU strategy and build requirements
            logger.info("Step 2: Analyzing GPU strategy")
            strategy_result = analyze_gpu_list(detected_gpus)
            warnings.extend(strategy_result.warnings)
            logger.info(f"GPU Strategy: {strategy_result.strategy}")
            
            # Step 3: Build environment requirements
            logger.info("Step 3: Building environment requirements")
            try:
                requirements_map = self.gpu_detector.get_environment_requirements_all()
                logger.info(f"Generated {len(requirements_map)} environment requirements")
            except Exception as e:
                errors.append(f"Failed to analyze GPU requirements: {str(e)}")
                logger.error("Failed to analyze GPU requirements", error=str(e))
                requirements_map = {}

            # Step 4: Plan environments (grouping by identical requirements)
            logger.info("Step 4: Planning environment specifications")
            try:
                specs_map = self.planner.plan_environments(requirements_map)
                logger.info(f"Planned {len(specs_map)} environment specifications")
            except Exception as e:
                errors.append(f"Failed to plan environments: {str(e)}")
                logger.error("Failed to plan environments", error=str(e))
                specs_map = {}

            # Step 5: Create environments
            logger.info("Step 5: Creating virtual environments")
            for name, spec in specs_map.items():
                logger.info(f"Creating environment: {name}")
                try:
                    res = await self.venv_mgr.create_environment(spec)
                    created_results[name] = res
                    
                    if not res.success:
                        errors.extend([f"Failed to create {name}: {error}" for error in res.errors])
                        logger.error(f"Failed to create environment {name}", errors=res.errors)
                    else:
                        logger.info(f"Successfully created: {name}")
                        # Track GPU assignments for reporting
                        gpu_assignments[name] = spec.target_gpus
                    
                except Exception as e:
                    errors.append(f"Exception creating {name}: {str(e)}")
                    logger.error(f"Exception creating {name}", error=str(e))

            # Step 6: Validate environments
            logger.info("Step 6: Validating environments")
            for name, res in created_results.items():
                if res.success:
                    logger.info(f"Validating environment: {name}")
                    try:
                        # Get the spec to access validation commands
                        spec = specs_map.get(name)
                        if spec and spec.validation_commands:
                            val_res = await self.validator.validate_environment(
                                env_name=name, 
                                python_executable=res.python_executable, 
                                validation_commands=spec.validation_commands,
                                gpu_devices=spec.target_gpus
                            )
                            val_results[name] = val_res
                            
                            if val_res.overall_success:
                                logger.info(f"Validation passed: {name}")
                            else:
                                warnings.append(f"Validation failed for {name}")
                                logger.warning(f"Validation failed: {name}")
                        else:
                            logger.info(f"No validation commands for {name}, skipping validation")
                            
                    except Exception as e:
                        warnings.append(f"Validation exception for {name}: {str(e)}")
                        logger.error(f"Validation exception for {name}", error=str(e))

        except Exception as e:
            errors.append(f"Setup pipeline exception: {str(e)}")
            logger.error("Setup pipeline exception", error=str(e))

        # Calculate results
        elapsed = time.time() - start
        success_count = len([r for r in created_results.values() if r.success])

        # Log completion
        logger.info("Environment setup completed", 
                  total_time=f"{elapsed:.2f}s",
                  environments_created=len(specs_map),
                  environments_successful=success_count)

        return SetupSummary(
            total_gpus=total_gpus,
            env_created=len(specs_map),
            env_successful=success_count,
            time_s=elapsed,
            gpu_assignments=gpu_assignments,
            val_results=val_results,
            warnings=warnings,
            errors=errors,
        )

    def get_status(self) -> Dict[str, Any]:
        """
        Get current setup status
        
        Returns:
            Dict with information about detected GPUs and created environments
        """
        # Safe access to detected GPUs
        gpu_count = 0
        try:
            detected = self.gpu_detector.detect_all_gpus()
            gpu_count = len(detected)
        except Exception:
            pass
            
        return {
            "detected_gpus": gpu_count,
            "environments_path": str(self.base_path),
            "venv_path": str(self.base_path / "venvs")
        }
    
    async def get_available_environments(self) -> Dict[str, Dict[str, Any]]:
        """
        Get information about all available environments
        
        Returns:
            Dict mapping environment names to their information
        """
        environments = {}
        
        try:
            venvs = self.venv_mgr.list_environments()
            for name, venv_info in venvs.items():
                python_exec = venv_info.python_executable
                is_valid = False
                
                if python_exec:
                    try:
                        # Simple check if environment exists and is accessible
                        is_valid = python_exec.exists()
                    except Exception:
                        pass
                
                environments[name] = {
                    "path": str(venv_info.path),
                    "python_executable": str(python_exec) if python_exec else None,
                    "is_valid": is_valid
                }
        except Exception as e:
            logger.error(f"Error listing environments: {str(e)}")
            
        return environments
    
    async def validate_single_environment(self, env_name: str) -> EnvironmentValidationResult:
        """
        Validate a single environment
        
        Args:
            env_name: Name of the environment to validate
            
        Returns:
            EnvironmentValidationResult: Results of validation
        """
        venv_info = self.venv_mgr.get_environment_info(env_name)
        if not venv_info or not venv_info.python_executable:
            logger.warning(f"Environment {env_name} not found or invalid")
            # Create a custom validation result
            return EnvironmentValidationResult(
                env_name=env_name,
                overall_success=False,
                gpu_validation={},
                package_validation={},
                script_validation={},
                recommendations=[],
                errors=[f"Environment {env_name} not found or has no Python executable"]
            )
        
        # Use basic validation commands if no spec is available
        basic_validation = ["import sys", "import torch", "print(torch.__version__)"]
        
        try:
            return await self.validator.validate_environment(
                env_name=env_name,
                python_executable=str(venv_info.python_executable),
                validation_commands=basic_validation,
                gpu_devices=[]  # No specific GPU target for basic validation
            )
        except Exception as e:
            logger.error(f"Error validating environment {env_name}: {str(e)}")
            return EnvironmentValidationResult(
                env_name=env_name,
                overall_success=False,
                gpu_validation={},
                package_validation={},
                script_validation={},
                recommendations=[],
                errors=[f"Validation error: {str(e)}"]
            )
            
    async def list_specs(self) -> Dict[str, Dict[str, Any]]:
        """
        List available environment specifications
        
        Returns:
            Dict mapping spec names to their basic information
        """
        specs = {}
        
        try:
            # Try to load specs from the specs directory
            specs_dir = getattr(self.planner, "specs_dir", self.base_path / "specs")
            
            # Just check if the directory exists and list files
            if specs_dir.exists():
                logger.info(f"Looking for specs in {specs_dir}")
                for spec_file in specs_dir.glob("*.json"):
                    try:
                        spec_name = spec_file.stem
                        with open(spec_file, "r") as f:
                            spec_data = json.load(f)
                        
                        specs[spec_name] = {
                            "framework": spec_data.get("framework", "unknown"),
                            "num_gpus": len(spec_data.get("target_gpus", [])),
                            "target_gpus": spec_data.get("target_gpus", []),
                            "has_validation": "validation_commands" in spec_data and bool(spec_data["validation_commands"])
                        }
                    except Exception as e:
                        logger.error(f"Error processing spec {spec_file}: {str(e)}")
        except Exception as e:
            logger.error(f"Error listing specs: {str(e)}")
            
        return specs


# Main entry point for testing
async def test_orchestrator():
    """Test the environment setup orchestrator"""
    print("\nTesting Environment Setup Orchestrator")
    print("=" * 50)
    
    orchestrator = EnvironmentSetupOrchestrator()
    summary = await orchestrator.setup_all()
    
    print("\nSetup Summary:")
    print(f"  - Total GPUs: {summary.total_gpus}")
    print(f"  - Environments created: {summary.environments_created}")
    print(f"  - Environments successful: {summary.environments_successful}")
    print(f"  - Setup time: {summary.setup_time_seconds:.2f}s")
    
    if summary.gpu_assignments:
        print("\nGPU Assignments:")
        for env, gpus in summary.gpu_assignments.items():
            print(f"  - {env}: {', '.join(gpus) if gpus else 'CPU-only'}")
    
    if summary.warnings:
        print("\nWarnings:")
        for warning in summary.warnings:
            print(f"  - {warning}")
    
    if summary.errors:
        print("\nErrors:")
        for error in summary.errors:
            print(f"  - {error}")


if __name__ == "__main__":
    asyncio.run(test_orchestrator())
