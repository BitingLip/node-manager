"""
Virtual Environment Manager
Creates and manages Python virtual environments for different GPU configurations
Handles dependency installation, conflict resolution, and environment isolation
"""

import os
import sys
import shutil
import subprocess
import asyncio
import json
from typing import Dict, List, Optional, Any, Tuple
from pathlib import Path
from dataclasses import dataclass
import structlog

from gpu.gpu_detector import EnvironmentRequirement
from .environment_planner import EnvironmentSpec, EnvironmentSetupResult, EnvironmentType

logger = structlog.get_logger(__name__)


@dataclass
class VenvInfo:
    """Virtual environment information"""
    name: str
    path: Path
    python_executable: Path
    pip_executable: Path
    activated_env_vars: Dict[str, str]
    installed_packages: List[str]
    framework_type: str
    target_gpus: List[str]


class VenvManager:
    """
    Manages Python virtual environments for GPU-specific configurations
    Handles creation, activation, package installation, and cleanup
    """
    
    def __init__(self, base_path: Optional[Path] = None):
        """Initialize virtual environment manager"""
        self.base_path = base_path or Path.cwd() / "venvs"
        self.base_path.mkdir(exist_ok=True)
        
        self.system_os = sys.platform
        self.python_version = f"{sys.version_info.major}.{sys.version_info.minor}"
        
        # Track created environments
        self.environments: Dict[str, VenvInfo] = {}
        
        # Environment metadata file
        self.metadata_file = self.base_path / "environments.json"
        
        # Load existing environments
        self._load_environment_metadata()
        
        logger.info("VenvManager initialized", 
                   base_path=str(self.base_path),
                   existing_envs=len(self.environments))
    
    def _load_environment_metadata(self):
        """Load existing environment metadata"""
        if self.metadata_file.exists():
            try:
                with open(self.metadata_file, 'r') as f:
                    data = json.load(f)
                
                for env_name, env_data in data.items():
                    self.environments[env_name] = VenvInfo(
                        name=env_data["name"],
                        path=Path(env_data["path"]),
                        python_executable=Path(env_data["python_executable"]),
                        pip_executable=Path(env_data["pip_executable"]),
                        activated_env_vars=env_data["activated_env_vars"],
                        installed_packages=env_data["installed_packages"],
                        framework_type=env_data["framework_type"],
                        target_gpus=env_data["target_gpus"]
                    )
                    
            except Exception as e:
                logger.warning("Failed to load environment metadata", error=str(e))
    
    def _save_environment_metadata(self):
        """Save environment metadata"""
        data = {}
        for env_name, env_info in self.environments.items():
            data[env_name] = {
                "name": env_info.name,
                "path": str(env_info.path),
                "python_executable": str(env_info.python_executable),
                "pip_executable": str(env_info.pip_executable),
                "activated_env_vars": env_info.activated_env_vars,
                "installed_packages": env_info.installed_packages,
                "framework_type": env_info.framework_type,
                "target_gpus": env_info.target_gpus
            }
        
        with open(self.metadata_file, 'w') as f:
            json.dump(data, f, indent=2)
    async def create_environment(self, spec: EnvironmentSpec) -> EnvironmentSetupResult:
        """
        Creates (or reuses) the environment specified by spec.
        If env_type == VENV: make a venv and install spec.base_packages.
        If env_type == NATIVE: run pip install system-wide.
        """
        result = EnvironmentSetupResult(
            env_name=spec.name,
            success=False,
            path=None,
            python_executable=None,
            installed_packages=[],
            validation_results={},
            errors=[],
            warnings=[],
        )

        env_path = self.base_path / spec.name
        
        # If venv already exists and metadata says it's good, optionally reuse
        if spec.env_type == EnvironmentType.VENV and spec.name in self.environments:
            venv = self.environments[spec.name]
            result.path = str(venv.path)
            result.python_executable = str(venv.python_executable)
            result.installed_packages = venv.installed_packages
            result.success = True
            result.warnings.append("Using existing venv")
            logger.info(f"Reusing existing environment: {spec.name}")
            return result

        try:
            if spec.env_type == EnvironmentType.VENV:
                # Delete old if present
                if env_path.exists():
                    logger.info(f"Removing existing venv: {spec.name}")
                    shutil.rmtree(env_path)

                # Create venv
                logger.info(f"Creating new venv: {spec.name}")
                proc = await asyncio.create_subprocess_exec(
                    sys.executable, "-m", "venv", str(env_path),
                    stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE
                )
                out, err = await proc.communicate()
                if proc.returncode != 0:
                    result.errors.append(f"Venv creation failed: {err.decode()}")
                    return result

                # Locate executables
                if sys.platform == "win32":
                    python_exe = env_path / "Scripts" / "python.exe"
                    pip_exe = env_path / "Scripts" / "pip.exe"
                else:
                    python_exe = env_path / "bin" / "python"
                    pip_exe = env_path / "bin" / "pip"

                result.path = str(env_path)
                result.python_executable = str(python_exe)

                # Upgrade pip in venv first
                await self._run_pip(str(pip_exe), ["install", "--upgrade", "pip"], result)

                # Install base packages
                if spec.base_packages:
                    # Add extra index URLs if specified
                    pip_args = ["install"]
                    for url in spec.pip_extra_index_urls:
                        pip_args.extend(["--extra-index-url", url])
                    pip_args.extend(spec.base_packages)
                    
                    await self._run_pip(str(pip_exe), pip_args, result)

                # Record metadata
                installed = await self._list_packages(str(pip_exe))
                venv_info = VenvInfo(
                    name=spec.name,
                    path=env_path,
                    python_executable=python_exe,
                    pip_executable=pip_exe,
                    activated_env_vars=spec.environment_variables,
                    installed_packages=installed,
                    framework_type=spec.framework.value,
                    target_gpus=spec.target_gpus,
                )
                self.environments[spec.name] = venv_info
                self._save_environment_metadata()
                result.installed_packages = installed
                result.success = True
                logger.info(f"✅ Successfully created venv: {spec.name}")

            elif spec.env_type == EnvironmentType.NATIVE:
                # System Python: run pip install globally
                logger.info(f"Installing packages to system Python for: {spec.name}")
                result.python_executable = sys.executable
                
                pip_args = ["install"]
                for url in spec.pip_extra_index_urls:
                    pip_args.extend(["--extra-index-url", url])
                pip_args.extend(spec.base_packages)
                
                try:
                    proc = await asyncio.create_subprocess_exec(
                        "pip", *pip_args,
                        stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE
                    )
                    out, err = await proc.communicate()
                    if proc.returncode != 0:
                        result.errors.append(f"System pip install failed: {err.decode()}")
                        return result
                    
                    result.installed_packages = spec.base_packages
                    result.success = True
                    logger.info(f"✅ Successfully installed to system Python: {spec.name}")
                    
                except FileNotFoundError:
                    result.errors.append("System pip not found")
                    return result

            else:
                result.errors.append(f"Unsupported env type: {spec.env_type}")
                return result

        except Exception as e:
            result.errors.append(f"Environment creation exception: {str(e)}")
            logger.error(f"Environment creation failed: {spec.name}", error=str(e))

        return result

    async def _run_pip(self, pip_path: str, args: List[str], result: EnvironmentSetupResult):
        """Helper to run pip commands and capture errors"""
        try:
            logger.debug(f"Running pip: {pip_path} {' '.join(args)}")
            proc = await asyncio.create_subprocess_exec(
                pip_path, *args,
                stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE
            )
            out, err = await proc.communicate()
            if proc.returncode != 0:
                error_msg = f"Pip command failed: {err.decode()}"
                result.errors.append(error_msg)
                logger.warning(error_msg)
            else:
                logger.debug(f"Pip command succeeded: {out.decode()[:200]}...")
        except Exception as e:
            error_msg = f"Pip execution exception: {str(e)}"
            result.errors.append(error_msg)
            logger.error(error_msg)

    async def _list_packages(self, pip_path: str) -> List[str]:
        """
        Return the list of installed packages from pip list.
        """
        try:
            proc = await asyncio.create_subprocess_exec(
                pip_path, "list", "--format=json",
                stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE
            )
            out, err = await proc.communicate()
            if proc.returncode == 0:
                import json as _json
                data = _json.loads(out.decode())
                return [f"{pkg['name']}=={pkg['version']}" for pkg in data]
        except Exception as e:
            logger.warning(f"Failed to list packages: {str(e)}")
        return []
    
    async def _create_venv(self, spec: EnvironmentSpec, result: EnvironmentSetupResult) -> EnvironmentSetupResult:
        """Create Python virtual environment"""
        env_path = self.base_path / spec.name
        
        # Remove existing environment if it exists
        if env_path.exists():
            logger.info(f"Removing existing environment: {spec.name}")
            shutil.rmtree(env_path)
        
        try:
            # Create virtual environment
            logger.info(f"Creating venv at {env_path}")
            process = await asyncio.create_subprocess_exec(
                sys.executable, "-m", "venv", str(env_path),
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            stdout, stderr = await process.communicate()
            
            if process.returncode != 0:
                result.errors.append(f"Venv creation failed: {stderr.decode()}")
                return result
            
            # Get executables
            if sys.platform == "win32":
                python_exe = env_path / "Scripts" / "python.exe"
                pip_exe = env_path / "Scripts" / "pip.exe"
            else:
                python_exe = env_path / "bin" / "python"
                pip_exe = env_path / "bin" / "pip"
            
            result.path = str(env_path)
            result.python_executable = str(python_exe)
              # Upgrade pip first
            await self._run_pip_command(str(pip_exe), ["install", "--upgrade", "pip"], result)
            
            # Install packages
            await self._install_packages(spec, str(pip_exe), result)
            
            # Create VenvInfo
            venv_info = VenvInfo(
                name=spec.name,
                path=env_path,
                python_executable=python_exe,
                pip_executable=pip_exe,
                activated_env_vars=spec.environment_variables,
                installed_packages=result.installed_packages,
                framework_type=spec.framework.value,
                target_gpus=spec.target_gpus
            )
            
            self.environments[spec.name] = venv_info
            self._save_environment_metadata()
            
            result.success = True
            logger.info(f"Successfully created venv: {spec.name}")
            
        except Exception as e:
            result.errors.append(f"Venv creation error: {str(e)}")
            logger.error("Venv creation error", error=str(e))
        
        return result
    
    async def _setup_native_env(self, spec: EnvironmentSpec, result: EnvironmentSetupResult) -> EnvironmentSetupResult:
        """Setup native (system) environment"""
        logger.info(f"Setting up native environment: {spec.name}")
        
        # For native environments (DirectML), use system Python
        result.path = "native"
        result.python_executable = sys.executable
        
        # Check if system pip is available
        try:
            process = await asyncio.create_subprocess_exec(
                "pip", "--version",
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            stdout, stderr = await process.communicate()
            
            if process.returncode != 0:
                result.errors.append("System pip not available")
                return result
            
        except FileNotFoundError:
            result.errors.append("pip command not found")
            return result
        
        # Install packages to system Python
        await self._install_packages(spec, "pip", result)
        
        # Create VenvInfo for tracking
        venv_info = VenvInfo(
            name=spec.name,
            path=Path("native"),
            python_executable=Path(sys.executable),
            pip_executable=Path("pip"),
            activated_env_vars=spec.environment_variables,
            installed_packages=result.installed_packages,
            framework_type=spec.framework.value,
            target_gpus=spec.target_gpus
        )
        
        self.environments[spec.name] = venv_info
        self._save_environment_metadata()
        
        result.success = True
        logger.info(f"Successfully setup native environment: {spec.name}")
        
        return result
    
    async def _install_packages(self, spec: EnvironmentSpec, pip_exe: str, result: EnvironmentSetupResult):
        """Install packages to environment"""
        # Combine all packages
        all_packages = []
        all_packages.extend(spec.base_packages)
        all_packages.extend(spec.gpu_packages)
        all_packages.extend(spec.additional_packages)
        
        if not all_packages:
            return
        
        # Build pip install command
        pip_args = ["install", "--upgrade"]
        
        # Add extra index URLs
        for url in spec.pip_extra_index_urls:
            pip_args.extend(["--extra-index-url", url])
        
        # Add packages
        pip_args.extend(all_packages)
        
        # Install packages
        await self._run_pip_command(pip_exe, pip_args, result)
        
        if result.errors:
            logger.warning(f"Package installation had errors for {spec.name}")
        else:
            result.installed_packages = all_packages
            logger.info(f"Successfully installed {len(all_packages)} packages for {spec.name}")
    
    async def _run_pip_command(self, pip_exe: str, args: List[str], result: EnvironmentSetupResult):
        """Run pip command and capture output"""
        cmd = [str(pip_exe)] + args
        
        try:
            logger.debug(f"Running pip command: {' '.join(cmd)}")
            
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            
            stdout, stderr = await process.communicate()
            
            if process.returncode == 0:
                logger.debug("Pip command succeeded")
            else:
                error_msg = f"Pip command failed (exit {process.returncode}): {stderr.decode()}"
                result.errors.append(error_msg)
                logger.error("Pip command failed", error=error_msg)
                
        except Exception as e:
            error_msg = f"Pip command exception: {str(e)}"
            result.errors.append(error_msg)
            logger.error("Pip command exception", error=str(e))
    
    def get_environment_info(self, env_name: str) -> Optional[VenvInfo]:
        """Get information about an environment"""
        return self.environments.get(env_name)
    
    def list_environments(self) -> Dict[str, VenvInfo]:
        """List all managed environments"""
        return self.environments.copy()
    
    def remove_environment(self, env_name: str) -> bool:
        """Remove an environment"""
        if env_name not in self.environments:
            logger.warning(f"Environment not found: {env_name}")
            return False
        
        venv_info = self.environments[env_name]
        
        try:
            # Remove directory for venv environments
            if venv_info.path != Path("native") and venv_info.path.exists():
                shutil.rmtree(venv_info.path)
                logger.info(f"Removed environment directory: {venv_info.path}")
            
            # Remove from tracking
            del self.environments[env_name]
            self._save_environment_metadata()
            
            logger.info(f"Successfully removed environment: {env_name}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to remove environment {env_name}", error=str(e))
            return False
    
    def get_activation_command(self, env_name: str) -> Optional[str]:
        """Get command to activate environment"""
        if env_name not in self.environments:
            return None
        
        venv_info = self.environments[env_name]
        
        if venv_info.path == Path("native"):
            return "# Native environment - no activation needed"
        
        if sys.platform == "win32":
            activate_script = venv_info.path / "Scripts" / "activate.bat"
            return f"call {activate_script}"
        else:
            activate_script = venv_info.path / "bin" / "activate"
            return f"source {activate_script}"
    
    def get_environment_variables(self, env_name: str) -> Dict[str, str]:
        """Get environment variables for an environment"""
        if env_name not in self.environments:
            return {}
        
        return self.environments[env_name].activated_env_vars.copy()
    
    async def test_environment(self, env_name: str) -> Dict[str, Any]:
        """Test if environment is working correctly"""
        if env_name not in self.environments:
            return {"success": False, "error": "Environment not found"}
        
        venv_info = self.environments[env_name]
        
        try:
            # Test Python execution
            process = await asyncio.create_subprocess_exec(
                str(venv_info.python_executable), "--version",
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            stdout, stderr = await process.communicate()
            
            if process.returncode != 0:
                return {"success": False, "error": f"Python test failed: {stderr.decode()}"}
            
            python_version = stdout.decode().strip()
            
            # Test pip
            process = await asyncio.create_subprocess_exec(
                str(venv_info.pip_executable), "list", "--format=json",
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            stdout, stderr = await process.communicate()
            
            if process.returncode != 0:
                return {"success": False, "error": f"Pip test failed: {stderr.decode()}"}
            
            try:
                installed_packages = json.loads(stdout.decode())
                package_count = len(installed_packages)
            except:
                package_count = 0
            
            return {
                "success": True,
                "python_version": python_version,
                "package_count": package_count,
                "framework": venv_info.framework_type,
                "target_gpus": venv_info.target_gpus
            }
            
        except Exception as e:
            return {"success": False, "error": str(e)}
    
    def cleanup_invalid_environments(self):
        """Remove environments that no longer exist on disk"""
        to_remove = []
        
        for env_name, venv_info in self.environments.items():
            if venv_info.path == Path("native"):
                continue
                
            if not venv_info.path.exists():
                to_remove.append(env_name)
                logger.info(f"Marking invalid environment for removal: {env_name}")
        
        for env_name in to_remove:
            del self.environments[env_name]
        
        if to_remove:
            self._save_environment_metadata()
            logger.info(f"Cleaned up {len(to_remove)} invalid environments")
    
    def get_environment_summary(self) -> Dict[str, Any]:
        """Get summary of all environments"""
        total_envs = len(self.environments)
        frameworks = {}
        gpu_assignments = {}
        
        for env_name, venv_info in self.environments.items():
            # Count frameworks
            framework = venv_info.framework_type
            frameworks[framework] = frameworks.get(framework, 0) + 1
            
            # Count GPU assignments
            for gpu in venv_info.target_gpus:
                gpu_assignments[gpu] = gpu_assignments.get(gpu, [])
                gpu_assignments[gpu].append(env_name)
        
        return {
            "total_environments": total_envs,
            "frameworks": frameworks,
            "gpu_assignments": gpu_assignments,
            "base_path": str(self.base_path)
        }
