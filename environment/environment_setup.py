"""
Environment Setup Orchestrator
Coordinates GPU detection, environment planning, creation, and validation
Main entry point for setting up optimal GPU environments
"""

import asyncio
import time
from typing import Dict, List, Optional, Any, Tuple
from pathlib import Path
from dataclasses import dataclass
import structlog

from .gpu_detector import GPUDetector, GPUInfo, EnvironmentRequirement
from .environment_planner import EnvironmentPlanner, EnvironmentSpec, EnvironmentSetupResult
from .venv_manager import VenvManager, VenvInfo
from .environment_validator import EnvironmentValidator, EnvironmentValidationResult

logger = structlog.get_logger(__name__)


@dataclass
class SetupSummary:
    """Summary of complete environment setup"""
    total_gpus: int
    environments_created: int
    environments_successful: int
    setup_time_seconds: float
    gpu_assignments: Dict[str, List[str]]
    validation_results: Dict[str, bool]
    recommendations: List[str]
    errors: List[str]


class EnvironmentSetupOrchestrator:
    """
    Orchestrates complete environment setup for detected GPUs
    Handles the full pipeline: detection -> planning -> creation -> validation
    """
    
    def __init__(self, base_path: Optional[Path] = None):
        """Initialize environment setup orchestrator"""
        self.base_path = base_path or Path.cwd() / "node_environments"
        self.base_path.mkdir(exist_ok=True)
        
        # Initialize components
        self.gpu_detector = GPUDetector()
        self.env_planner = EnvironmentPlanner(self.base_path / "specs")
        self.venv_manager = VenvManager(self.base_path / "venvs")
        self.validator = EnvironmentValidator()
        
        # Track setup state
        self.detected_gpus: List[GPUInfo] = []
        self.environment_specs: Dict[str, EnvironmentSpec] = {}
        self.created_environments: Dict[str, EnvironmentSetupResult] = {}
        self.validation_results: Dict[str, EnvironmentValidationResult] = {}
        
        logger.info("EnvironmentSetupOrchestrator initialized", base_path=str(self.base_path))
    
    async def setup_all_environments(self, force_recreate: bool = False) -> SetupSummary:
        """
        Complete environment setup pipeline
        
        Args:
            force_recreate: Whether to recreate existing environments
            
        Returns:
            SetupSummary with results
        """
        logger.info("Starting complete environment setup")
        start_time = time.time()
        
        setup_errors = []
        
        try:
            # Step 1: Detect GPUs
            logger.info("🔍 Step 1: Detecting GPUs")
            self.detected_gpus = self.gpu_detector.detect_all_gpus()
            
            if not self.detected_gpus:
                setup_errors.append("No GPUs detected")
                logger.warning("No GPUs detected - will create CPU-only environment")
                # Create CPU-only environment
                await self._create_cpu_fallback_environment()
            
            # Step 2: Plan environments  
            logger.info("📋 Step 2: Planning environments")
            requirements = []
            for gpu in self.detected_gpus:
                req = self.gpu_detector.get_environment_requirements(gpu)
                requirements.append(req)
            
            self.environment_specs = self.env_planner.plan_environments(requirements)
            logger.info(f"Planned {len(self.environment_specs)} environments")
            
            # Step 3: Create environments
            logger.info("🏗️ Step 3: Creating environments")
            self.created_environments = await self._create_environments(force_recreate)
            
            # Step 4: Validate environments
            logger.info("✅ Step 4: Validating environments")
            self.validation_results = await self._validate_environments()
            
            # Step 5: Generate summary
            setup_time = time.time() - start_time
            summary = self._generate_setup_summary(setup_time, setup_errors)
            
            # Save setup state
            await self._save_setup_state()
            
            logger.info("Environment setup completed", 
                       total_time=f"{setup_time:.2f}s",
                       environments=len(self.created_environments),
                       successful=summary.environments_successful)
            
            return summary
            
        except Exception as e:
            setup_errors.append(f"Setup failed: {str(e)}")
            logger.error("Environment setup failed", error=str(e))
            
            # Return failed summary
            return SetupSummary(
                total_gpus=len(self.detected_gpus),
                environments_created=0,
                environments_successful=0,
                setup_time_seconds=time.time() - start_time,
                gpu_assignments={},
                validation_results={},
                recommendations=[],
                errors=setup_errors
            )
    
    async def _create_cpu_fallback_environment(self):
        """Create CPU-only fallback environment"""
        from .environment_planner import EnvironmentSpec, EnvironmentType, FrameworkType
        
        cpu_spec = EnvironmentSpec(
            name="cpu_fallback",
            env_type=EnvironmentType.VENV,
            framework=FrameworkType.PYTORCH,
            python_version=self.env_planner.python_version,
            base_packages=[
                "pip>=23.0",
                "setuptools>=65.0",
                "wheel>=0.38.0",
                "numpy>=1.24.0",
                "pillow>=9.0.0"
            ],
            gpu_packages=[
                "torch>=2.1.0+cpu",
                "torchvision>=0.16.0+cpu", 
                "torchaudio>=2.1.0+cpu",
                "transformers>=4.35.0"
            ],
            additional_packages=[],
            pip_extra_index_urls=["https://download.pytorch.org/whl/cpu"],
            environment_variables={"TRANSFORMERS_CACHE": str(self.base_path / "cache")},
            validation_commands=[],
            conflicting_envs=[],
            target_gpus=[]
        )
        
        self.environment_specs["cpu_fallback"] = cpu_spec
    
    async def _create_environments(self, force_recreate: bool) -> Dict[str, EnvironmentSetupResult]:
        """Create all planned environments"""
        results = {}
        
        for spec_name, spec in self.environment_specs.items():
            logger.info(f"Creating environment: {spec_name}")
            
            # Check if environment already exists
            existing_env = self.venv_manager.get_environment_info(spec_name)
            if existing_env and not force_recreate:
                logger.info(f"Environment {spec_name} already exists, skipping")                # Test existing environment
                test_result = await self.venv_manager.test_environment(spec_name)
                if test_result.get("success", False):
                    results[spec_name] = EnvironmentSetupResult(
                        env_name=spec_name,
                        success=True,
                        path=str(existing_env.path),
                        python_executable=str(existing_env.python_executable),
                        installed_packages=existing_env.installed_packages,
                        validation_results={},
                        errors=[],
                        warnings=["Using existing environment"]
                    )
                    continue
                else:
                    logger.warning(f"Existing environment {spec_name} failed tests, recreating")
            
            # Create environment
            try:
                result = await self.venv_manager.create_environment(spec)
                results[spec_name] = result
                
                if result.success:
                    logger.info(f"✅ Successfully created environment: {spec_name}")
                else:
                    logger.error(f"❌ Failed to create environment: {spec_name}", 
                               errors=result.errors)
                    
            except Exception as e:
                logger.error(f"Exception creating environment {spec_name}", error=str(e))
                results[spec_name] = EnvironmentSetupResult(
                    env_name=spec_name,
                    success=False,
                    path=None,
                    python_executable=None,
                    installed_packages=[],
                    validation_results={},
                    errors=[str(e)],
                    warnings=[]
                )
        
        return results
    
    async def _validate_environments(self) -> Dict[str, EnvironmentValidationResult]:
        """Validate all created environments"""
        validation_results = {}
        
        for env_name, setup_result in self.created_environments.items():
            if not setup_result.success:
                logger.info(f"Skipping validation for failed environment: {env_name}")
                continue
            
            logger.info(f"Validating environment: {env_name}")
            
            # Get environment spec for framework info
            spec = self.environment_specs.get(env_name)
            if not spec:
                logger.warning(f"No spec found for environment: {env_name}")
                continue
              try:
                python_exec = setup_result.python_executable
                if python_exec is None:
                    logger.warning(f"No Python executable for environment: {env_name}")
                    continue
                    
                validation_result = await self.validator.validate_environment(
                    env_name=env_name,
                    python_executable=python_exec,
                    framework=spec.framework.value,
                    gpu_devices=spec.target_gpus
                )
                
                validation_results[env_name] = validation_result
                
                if validation_result.overall_success:
                    logger.info(f"✅ Environment validation passed: {env_name}")
                else:
                    logger.warning(f"⚠️ Environment validation failed: {env_name}")
                    
            except Exception as e:
                logger.error(f"Validation exception for {env_name}", error=str(e))
        
        return validation_results
    
    def _generate_setup_summary(self, setup_time: float, errors: List[str]) -> SetupSummary:
        """Generate comprehensive setup summary"""
        # Count successful environments
        successful_envs = sum(1 for result in self.created_environments.values() if result.success)
        
        # Count successful validations
        successful_validations = sum(1 for result in self.validation_results.values() if result.overall_success)
        
        # Create GPU assignments map
        gpu_assignments = {}
        for spec_name, spec in self.environment_specs.items():
            for gpu_id in spec.target_gpus:
                if gpu_id not in gpu_assignments:
                    gpu_assignments[gpu_id] = []
                gpu_assignments[gpu_id].append(spec_name)
        
        # Validation results map
        validation_success_map = {
            env_name: result.overall_success 
            for env_name, result in self.validation_results.items()
        }
        
        # Generate recommendations
        recommendations = self._generate_recommendations()
        
        return SetupSummary(
            total_gpus=len(self.detected_gpus),
            environments_created=len(self.created_environments),
            environments_successful=successful_envs,
            setup_time_seconds=setup_time,
            gpu_assignments=gpu_assignments,
            validation_results=validation_success_map,
            recommendations=recommendations,
            errors=errors
        )
    
    def _generate_recommendations(self) -> List[str]:
        """Generate setup recommendations"""
        recommendations = []
        
        # Check for failed environments
        failed_environments = [
            name for name, result in self.created_environments.items() 
            if not result.success
        ]
        
        if failed_environments:
            recommendations.append(f"❌ {len(failed_environments)} environment(s) failed to create")
            recommendations.append("💡 Check dependency conflicts and driver versions")
        
        # Check for failed validations
        failed_validations = [
            name for name, result in self.validation_results.items()
            if not result.overall_success
        ]
        
        if failed_validations:
            recommendations.append(f"⚠️ {len(failed_validations)} environment(s) failed validation")
            recommendations.append("💡 Run individual validation tests for details")
        
        # GPU-specific recommendations
        nvidia_gpus = [gpu for gpu in self.detected_gpus if gpu.vendor.value == "nvidia"]
        amd_gpus = [gpu for gpu in self.detected_gpus if gpu.vendor.value == "amd"]
        
        if nvidia_gpus and amd_gpus:
            recommendations.append("🔄 Mixed NVIDIA/AMD setup detected")
            recommendations.append("💡 Environments are isolated to prevent conflicts")
        
        # Driver recommendations
        for gpu in self.detected_gpus:
            if gpu.vendor.value == "amd" and gpu.architecture in ["rdna1", "rdna2"]:
                recommendations.append("📝 AMD RDNA1/2 requires Adrenalin 23.40.27.06+ for DirectML")
        
        # Success case
        if not failed_environments and not failed_validations:
            recommendations.append("✅ All environments created and validated successfully")
            recommendations.append("🚀 Node is ready for AI workloads")
        
        return recommendations
    
    async def _save_setup_state(self):
        """Save current setup state to disk"""
        state_file = self.base_path / "setup_state.json"
        
        state_data = {
            "detected_gpus": [
                {
                    "device_id": gpu.device_id,
                    "vendor": gpu.vendor.value,
                    "name": gpu.name,
                    "architecture": gpu.architecture,
                    "memory_mb": gpu.memory_mb
                }
                for gpu in self.detected_gpus
            ],
            "environments": {
                name: {
                    "success": result.success,
                    "path": result.path,
                    "python_executable": result.python_executable,
                    "errors": result.errors,
                    "warnings": result.warnings
                }
                for name, result in self.created_environments.items()
            },
            "validations": {
                name: {
                    "overall_success": result.overall_success,
                    "recommendations": result.recommendations
                }
                for name, result in self.validation_results.items()
            }
        }
        
        import json
        with open(state_file, 'w') as f:
            json.dump(state_data, f, indent=2)
        
        logger.info(f"Setup state saved to {state_file}")
    
    def get_setup_status(self) -> Dict[str, Any]:
        """Get current setup status"""
        return {
            "gpus_detected": len(self.detected_gpus),
            "environments_planned": len(self.environment_specs),
            "environments_created": len(self.created_environments),
            "environments_validated": len(self.validation_results),
            "gpu_list": [
                {
                    "id": gpu.device_id,
                    "name": gpu.name,
                    "vendor": gpu.vendor.value,
                    "memory_mb": gpu.memory_mb
                }
                for gpu in self.detected_gpus
            ],
            "environment_status": {
                name: {
                    "created": name in self.created_environments,
                    "success": self.created_environments.get(name, {}).success if name in self.created_environments else False,
                    "validated": name in self.validation_results,
                    "validation_success": self.validation_results.get(name, {}).overall_success if name in self.validation_results else False
                }
                for name in self.environment_specs.keys()
            }
        }
    
    async def repair_failed_environments(self) -> Dict[str, bool]:
        """Attempt to repair failed environments"""
        logger.info("Attempting to repair failed environments")
        repair_results = {}
        
        failed_envs = [
            name for name, result in self.created_environments.items()
            if not result.success
        ]
        
        for env_name in failed_envs:
            logger.info(f"Repairing environment: {env_name}")
            
            spec = self.environment_specs.get(env_name)
            if not spec:
                repair_results[env_name] = False
                continue
            
            try:
                # Force recreate the environment
                result = await self.venv_manager.create_environment(spec)
                self.created_environments[env_name] = result
                repair_results[env_name] = result.success
                
                if result.success:
                    logger.info(f"✅ Repaired environment: {env_name}")
                else:
                    logger.error(f"❌ Failed to repair environment: {env_name}")
                    
            except Exception as e:
                logger.error(f"Exception repairing {env_name}", error=str(e))
                repair_results[env_name] = False
        
        return repair_results
    
    def cleanup_all_environments(self):
        """Clean up all created environments"""
        logger.info("Cleaning up all environments")
        
        for env_name in list(self.venv_manager.environments.keys()):
            success = self.venv_manager.remove_environment(env_name)
            if success:
                logger.info(f"Removed environment: {env_name}")
            else:
                logger.warning(f"Failed to remove environment: {env_name}")
        
        # Clear state
        self.environment_specs.clear()
        self.created_environments.clear()
        self.validation_results.clear()
        
        logger.info("Environment cleanup completed")
