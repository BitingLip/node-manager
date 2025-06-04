"""
Simplified Environment Setup Orchestrator
Implements the refactored approach using unified GPU strategy.
"""

import asyncio
import time
from typing import Dict, List, Optional, Any,     def get_setup_status(self) -> Dict[str, Any]:ny
from pathlib import Path
import structlog

from .gpu_detector import GPUDetector, EnvironmentRequirement
from .environment_planner import EnvironmentPlanner, EnvironmentSpec
from .venv_manager import VenvManager, EnvironmentSetupResult
from .environment_validator import EnvironmentValidator, EnvironmentValidationResult

logger = structlog.get_logger(__name__)


class SetupSummary:
    """Summary of environment setup results"""
    
    def __init__(self, total_gpus: int, env_created: int, env_successful: int, 
                 time_s: float, gpu_assignments: Dict[str, List[str]], 
                 val_results: Dict[str, EnvironmentValidationResult], 
                 warnings: List[str], errors: List[str]):
        self.total_gpus = total_gpus
        self.environments_created = env_created
        self.environments_successful = env_successful
        self.setup_time_seconds = time_s
        self.gpu_assignments = gpu_assignments
        self.validation_results = val_results
        self.warnings = warnings
        self.errors = errors


class EnvironmentSetupOrchestrator:
    """
    Simplified orchestrator using unified GPU strategy approach.
    Coordinates detection → planning → creation → validation pipeline.
    """

    def __init__(self, base_path: Optional[Path] = None):
        self.base_path = base_path or (Path.cwd() / "node_environments")
        self.base_path.mkdir(exist_ok=True)

        self.gpu_detector = GPUDetector()
        self.planner = EnvironmentPlanner(self.base_path / "specs")
        self.venv_mgr = VenvManager(self.base_path / "venvs")
        self.validator = EnvironmentValidator()
        
        logger.info("EnvironmentSetupOrchestrator initialized", base_path=str(self.base_path))

    async def setup_all(self, force_recreate: bool = False) -> SetupSummary:
        """
        Complete environment setup pipeline using unified strategy.
        
        Steps:
        1. Detect GPUs
        2. Get unified strategy requirements  
        3. Plan environments (group identical requirements)
        4. Create environments
        5. Validate environments
        """
        start = time.time()
        errors: List[str] = []
        warnings: List[str] = []
        gpu_assignments: Dict[str, List[str]] = {}
        val_results: Dict[str, EnvironmentValidationResult] = {}

        try:
            # Step 1: Detect GPUs
            logger.info("🔍 Step 1: Detecting GPUs")
            detected = self.gpu_detector.detect_all_gpus()
            total = len(detected)
            
            if total == 0:
                errors.append("No GPUs detected; creating CPU-only fallback.")
                warnings.append("No GPU acceleration available")
                # Could implement CPU fallback here

            # Step 2: Build EnvironmentRequirement using unified strategy
            logger.info("📋 Step 2: Analyzing GPU strategy and requirements")
            reqs_map = self.gpu_detector.get_environment_requirements_all()
            
            if not reqs_map:
                errors.append("No environment requirements generated")
                return self._create_error_summary(total, start, errors, warnings)

            # Step 3: Plan environments (grouping by identical requirements)
            logger.info("🏗️ Step 3: Planning environments")
            specs_map = self.planner.plan_environments(reqs_map)
            
            if not specs_map:
                errors.append("No environment specifications created")
                return self._create_error_summary(total, start, errors, warnings)

            # Step 4: Create environments
            logger.info("⚙️ Step 4: Creating environments")
            created_results = {}
            for name, spec in specs_map.items():
                logger.info(f"Creating environment: {name}")
                
                try:
                    res = await self.venv_mgr.create_environment(spec)
                    created_results[name] = res
                    
                    if not res.success:
                        errors.extend(res.errors)
                        logger.error(f"Failed to create {name}", errors=res.errors)
                    else:
                        logger.info(f"✅ {name} created successfully")
                        
                    # Track GPU assignments
                    gpu_assignments[name] = spec.target_gpus
                    
                except Exception as e:
                    error_msg = f"Exception creating {name}: {str(e)}"
                    errors.append(error_msg)
                    logger.error(error_msg)

            # Step 5: Validate environments
            logger.info("✅ Step 5: Validating environments")
            for name, res in created_results.items():
                if res.success and res.python_executable:
                    try:
                        logger.info(f"Validating environment: {name}")
                        
                        # Get validation commands from spec
                        spec = specs_map[name]
                        validation_commands = getattr(spec, 'validation_commands', [])
                        
                        val_res = await self.validator.validate_environment(
                            env_name=name,
                            python_executable=res.python_executable,
                            framework=spec.framework.value,
                            gpu_devices=spec.target_gpus
                        )
                        val_results[name] = val_res
                        
                        if not val_res.overall_success:
                            warnings.append(f"Validation failed for {name}")
                            logger.warning(f"⚠️ Validation failed for {name}")
                        else:
                            logger.info(f"✅ Validation passed for {name}")
                            
                    except Exception as e:
                        warning_msg = f"Validation exception for {name}: {str(e)}"
                        warnings.append(warning_msg)
                        logger.warning(warning_msg)

            elapsed = time.time() - start
            success_count = sum(1 for r in created_results.values() if r.success)

            logger.info("Environment setup completed", 
                       total_time=f"{elapsed:.2f}s",
                       environments_created=len(specs_map),
                       environments_successful=success_count)

            return SetupSummary(
                total_gpus=total,
                env_created=len(specs_map),
                env_successful=success_count,
                time_s=elapsed,
                gpu_assignments=gpu_assignments,
                val_results=val_results,
                warnings=warnings,
                errors=errors,
            )
            
        except Exception as e:
            error_msg = f"Setup pipeline failed: {str(e)}"
            errors.append(error_msg)
            logger.error(error_msg)
              return self._create_error_summary(
                total if 'total' in locals() else 0,
                start, errors, warnings
            )

    def _create_error_summary(self, total_gpus: int, start_time: float, 
                            errors: List[str], warnings: List[str]) -> SetupSummary:
        """Create summary for failed setup"""
        return SetupSummary(
            total_gpus=total_gpus,
            env_created=0,
            env_successful=0,
            time_s=time.time() - start_time,
            gpu_assignments={},
            val_results={},
            warnings=warnings,
            errors=errors,
        )

    def get_setup_status(self) -> Dict[str, Any]:
        """Get current setup status summary"""
        detected = self.gpu_detector.detected_gpus
        
        return {
            "gpus_detected": len(detected),
            "gpu_summary": [
                {
                    "id": gpu.device_id,
                    "name": gpu.name,
                    "vendor": gpu.vendor.value,
                    "architecture": gpu.architecture,
                    "memory_mb": gpu.memory_mb
                }
                for gpu in detected
            ],
            "environments_managed": len(self.venv_mgr.environments),
            "base_path": str(self.base_path)
        }

    async def cleanup_all_environments(self):
        """Clean up all managed environments"""
        logger.info("Cleaning up all environments")
        
        for env_name in list(self.venv_mgr.environments.keys()):
            try:
                success = self.venv_mgr.remove_environment(env_name)
                if success:
                    logger.info(f"Removed environment: {env_name}")
                else:
                    logger.warning(f"Failed to remove environment: {env_name}")
            except Exception as e:
                logger.error(f"Exception removing {env_name}: {str(e)}")
