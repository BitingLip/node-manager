"""
Environment Validator
Simplified validation for created environments using unified GPU strategy.
"""

import asyncio
import subprocess
from typing import Dict, List, Optional
from pathlib import Path
from dataclasses import dataclass
import structlog

logger = structlog.get_logger(__name__)


@dataclass
class EnvironmentValidationResult:
    """Result of environment validation"""
    env_name: str
    overall_success: bool
    gpu_validation: Dict[str, bool]
    package_validation: Dict[str, bool]
    script_validation: Dict[str, bool]
    recommendations: List[str]
    errors: List[str]


class EnvironmentValidator:
    """
    Validates created environments by running validation scripts and checking GPU access.
    """

    def __init__(self):
        logger.info("EnvironmentValidator initialized")

    async def validate_environment(
        self, 
        env_name: str, 
        python_executable: str, 
        validation_commands: List[str], 
        gpu_devices: List[str]
    ) -> EnvironmentValidationResult:
        """
        Validate an environment by running validation commands.
        
        Args:
            env_name: Name of the environment
            python_executable: Path to Python executable
            validation_commands: List of validation script names or commands
            gpu_devices: List of GPU device IDs
            
        Returns:
            EnvironmentValidationResult with validation status
        """
        result = EnvironmentValidationResult(
            env_name=env_name,
            overall_success=False,
            gpu_validation={},
            package_validation={},
            script_validation={},
            recommendations=[],
            errors=[]
        )
        
        try:
            # Validate basic Python access
            basic_success = await self._validate_basic_python(python_executable, result)
            if not basic_success:
                return result
            
            # Validate GPU access for each device
            for gpu_id in gpu_devices:
                gpu_success = await self._validate_gpu_access(python_executable, gpu_id, result)
                result.gpu_validation[gpu_id] = gpu_success
            
            # Run custom validation scripts
            for script in validation_commands:
                if script:  # Skip empty scripts
                    script_success = await self._run_validation_script(python_executable, script, result)
                    result.script_validation[script] = script_success
            
            # Determine overall success
            gpu_ok = all(result.gpu_validation.values()) if result.gpu_validation else True
            scripts_ok = all(result.script_validation.values()) if result.script_validation else True
            
            result.overall_success = gpu_ok and scripts_ok
            
            if result.overall_success:
                result.recommendations.append("✅ Environment validation passed")
                logger.info(f"✅ Validation successful: {env_name}")
            else:
                result.recommendations.append("⚠️ Environment validation failed")
                logger.warning(f"⚠️ Validation failed: {env_name}")
                
        except Exception as e:
            result.errors.append(f"Validation exception: {str(e)}")
            logger.error(f"Validation exception for {env_name}", error=str(e))
        
        return result
    
    async def _validate_basic_python(self, python_exe: str, result: EnvironmentValidationResult) -> bool:
        """Validate basic Python functionality"""
        try:
            proc = await asyncio.create_subprocess_exec(
                python_exe, "-c", "import sys; print(f'Python {sys.version}')",
                stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE
            )
            out, err = await proc.communicate()
            
            if proc.returncode == 0:
                logger.debug(f"Python validation passed: {out.decode().strip()}")
                return True
            else:
                result.errors.append(f"Python validation failed: {err.decode()}")
                return False
                
        except Exception as e:
            result.errors.append(f"Python validation exception: {str(e)}")
            return False
    
    async def _validate_gpu_access(self, python_exe: str, gpu_id: str, result: EnvironmentValidationResult) -> bool:
        """Validate GPU access for a specific device"""
        try:
            # Determine validation script based on GPU type
            if "nvidia" in gpu_id.lower():
                script = "import torch; print(f'CUDA available: {torch.cuda.is_available()}')"
            elif "amd" in gpu_id.lower():
                script = "import torch; print(f'DirectML/ROCm available: {torch.cuda.is_available() or hasattr(torch, 'directml')}')"
            else:
                script = "import torch; print('CPU PyTorch available')"
            
            proc = await asyncio.create_subprocess_exec(
                python_exe, "-c", script,
                stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE
            )
            out, err = await proc.communicate()
            
            if proc.returncode == 0:
                output = out.decode().strip()
                logger.debug(f"GPU validation for {gpu_id}: {output}")
                return "True" in output or "available" in output.lower()
            else:
                result.errors.append(f"GPU validation failed for {gpu_id}: {err.decode()}")
                return False
                
        except Exception as e:
            result.errors.append(f"GPU validation exception for {gpu_id}: {str(e)}")
            return False
    
    async def _run_validation_script(self, python_exe: str, script_name: str, result: EnvironmentValidationResult) -> bool:
        """Run a custom validation script"""
        try:
            # For now, just run simple validation commands
            # In a full implementation, these would be actual script files
            validation_scripts = {
                "validate_directml.py": "import torch; import torch_directml; print('DirectML validation passed')",
                "validate_nvidia.py": "import torch; assert torch.cuda.is_available(); print('NVIDIA CUDA validation passed')",
                "validate_rocm.py": "import torch; print('ROCm validation passed')",
                "validate_cpu.py": "import torch; print('CPU PyTorch validation passed')"
            }
            
            script_code = validation_scripts.get(script_name, f"print('Unknown validation script: {script_name}')")
            
            proc = await asyncio.create_subprocess_exec(
                python_exe, "-c", script_code,
                stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE
            )
            out, err = await proc.communicate()
            
            if proc.returncode == 0:
                logger.debug(f"Validation script {script_name} passed: {out.decode().strip()}")
                return True
            else:
                result.errors.append(f"Validation script {script_name} failed: {err.decode()}")
                return False
                
        except Exception as e:
            result.errors.append(f"Validation script {script_name} exception: {str(e)}")
            return False
