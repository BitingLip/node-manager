"""
Environment Planner
Plans and manages Python environments for different GPU configurations
Handles venv creation, dependency resolution, and conflict avoidance
"""

import os
import sys
import platform
import subprocess
import json
import shutil
import asyncio
from typing import Dict, List, Optional, Any, Set, Tuple
from pathlib import Path
from dataclasses import dataclass, asdict
from enum import Enum
import structlog

from environment.gpu.gpu_detector import GPUInfo, GPUVendor, AMDArchitecture, NVIDIAArchitecture, EnvironmentRequirement

logger = structlog.get_logger(__name__)


class EnvironmentType(Enum):
    """Environment types"""
    VENV = "venv"
    NATIVE = "native"
    CONDA = "conda"
    SYSTEM = "system"


class FrameworkType(Enum):
    """AI Framework types"""
    PYTORCH = "pytorch"
    PYTORCH_CUDA = "pytorch_cuda"
    PYTORCH_ROCM = "pytorch_rocm"
    DIRECTML = "directml"
    TENSORFLOW = "tensorflow"
    ONNX = "onnx"


@dataclass
class EnvironmentSpec:
    """Complete environment specification"""
    name: str
    env_type: EnvironmentType
    framework: FrameworkType
    python_version: str
    base_packages: List[str]
    gpu_packages: List[str]
    additional_packages: List[str]
    pip_extra_index_urls: List[str]
    environment_variables: Dict[str, str]
    validation_commands: List[str]
    conflicting_envs: List[str]
    target_gpus: List[str]


@dataclass
class EnvironmentSetupResult:
    """Result of environment setup"""
    env_name: str
    success: bool
    path: Optional[str]
    python_executable: Optional[str]
    installed_packages: List[str]
    validation_results: Dict[str, bool]
    errors: List[str]
    warnings: List[str]


class EnvironmentPlanner:
    """
    Plans and creates optimal Python environments for GPU configurations
    Handles complex dependency management and conflict resolution
    """
    
    def __init__(self, base_path: Optional[Path] = None):
        """Initialize environment planner"""
        self.base_path = base_path or Path.cwd() / "environments"
        self.base_path.mkdir(exist_ok=True)
        
        self.system_os = platform.system().lower()
        self.python_version = f"{sys.version_info.major}.{sys.version_info.minor}"
        
        # Environment specifications
        self.env_specs = {}
        self.created_environments = {}
          # Driver version cache
        self._driver_versions = {}
        
        logger.info("EnvironmentPlanner initialized", 
                   base_path=str(self.base_path),
                   os=self.system_os,
                   python=self.python_version)
    
    def plan_environments(
        self, requirements: Dict[str, EnvironmentRequirement]
    ) -> Dict[str, EnvironmentSpec]:
        """
        Group GPUs whose EnvironmentRequirement attributes match exactly.
        Return a map { spec_name: EnvironmentSpec, … }.
        """
        grouped: Dict[tuple, List[EnvironmentRequirement]] = {}

        # Step 1: cluster by (python_env_type, framework, tuple(os_reqs), tuple(required_packages))
        for gpu_id, req in requirements.items():
            key = (
                req.python_env_type,
                req.framework,
                tuple(sorted(req.os_requirements)),
                tuple(sorted(req.required_packages)),
            )
            if key not in grouped:
                grouped[key] = []
            grouped[key].append(req)

        # Step 2: build an EnvironmentSpec for each group
        specs: Dict[str, EnvironmentSpec] = {}
        for idx, (key, req_list) in enumerate(grouped.items(), start=1):
            py_env_type, framework_str, os_reqs, pkgs = key
            target_ids = [r.gpu_info.device_id for r in req_list]

            spec_name = f"{framework_str}_{py_env_type}_{len(target_ids)}gpu"
            
            # Convert framework string to enum
            try:
                framework_enum = FrameworkType(framework_str.upper())
            except ValueError:
                # Handle custom framework names
                if framework_str == "pytorch_cuda":
                    framework_enum = FrameworkType.PYTORCH_CUDA
                elif framework_str == "pytorch_rocm":
                    framework_enum = FrameworkType.PYTORCH_ROCM
                elif framework_str == "directml":
                    framework_enum = FrameworkType.DIRECTML
                else:
                    framework_enum = FrameworkType.PYTORCH
            
            spec = EnvironmentSpec(
                name=spec_name,
                env_type=EnvironmentType(py_env_type.upper()),
                framework=framework_enum,
                python_version=self.python_version,
                base_packages=list(pkgs),
                gpu_packages=[],  # Already included in base_packages
                additional_packages=[],
                pip_extra_index_urls=self._determine_extra_index(framework_str),
                environment_variables=self._env_vars_for_framework(framework_str),
                validation_commands=[r.validation_script for r in req_list if r.validation_script],
                conflicting_envs=list({c for r in req_list for c in r.conflicts_with}),
                target_gpus=target_ids,
            )
            specs[spec_name] = spec

        self.env_specs = specs
        logger.info(f"Planned {len(specs)} environments")
        return specs

    def _determine_extra_index(self, framework_str: str) -> List[str]:
        """Determine extra pip index URLs based on framework"""
        if "cuda" in framework_str:
            return ["https://download.pytorch.org/whl/cu118"]
        if "rocm" in framework_str:
            return ["https://download.pytorch.org/whl/rocm6.4.1"]
        return []

    def _env_vars_for_framework(self, framework_str: str) -> Dict[str, str]:
        """Get environment variables for framework"""
        env = {}
        if framework_str == "directml":
            env["TORCH_DIRECTML"] = "1"
        elif framework_str == "pytorch_cuda":
            env["CUDA_VISIBLE_DEVICES"] = "all"
        elif framework_str == "pytorch_rocm":
            env["ROCM_PATH"] = "/opt/rocm"
        return env
    
    def _group_compatible_requirements(self, requirements: List[EnvironmentRequirement]) -> Dict[str, List[EnvironmentRequirement]]:
        """Group requirements that can share the same environment"""
        groups = {}
        
        for req in requirements:
            # Create group key based on compatibility factors
            group_key = self._get_compatibility_key(req)
            
            if group_key not in groups:
                groups[group_key] = []
            
            groups[group_key].append(req)
        
        return groups
    
    def _get_compatibility_key(self, req: EnvironmentRequirement) -> str:
        """Get compatibility key for grouping requirements"""
        # Key factors for compatibility:
        # 1. Framework type (CUDA vs DirectML vs ROCm)
        # 2. Environment type (venv vs native)
        # 3. OS requirements
        # 4. Major conflicts
        
        framework = req.framework.lower()
        env_type = req.python_env_type.lower()
        
        # Special handling for different frameworks
        if framework == "pytorch":
            if req.gpu_info.vendor == GPUVendor.NVIDIA:
                framework = "pytorch_cuda"
            elif req.gpu_info.vendor == GPUVendor.AMD:
                arch = req.gpu_info.architecture
                if arch in ["rdna3", "rdna4"]:
                    framework = "pytorch_rocm"
                else:
                    framework = "directml"
        
        return f"{framework}_{env_type}_{self.system_os}"
    
    def _create_environment_spec(self, group_name: str, requirements: List[EnvironmentRequirement]) -> EnvironmentSpec:
        """Create environment specification for a group of requirements"""
        # Determine primary framework and environment type
        primary_req = requirements[0]
        framework_name = primary_req.framework.lower()
        env_type = EnvironmentType(primary_req.python_env_type.lower())
        
        # Collect all target GPUs
        target_gpus = [req.gpu_info.device_id for req in requirements]
        
        # Generate environment name
        env_name = f"{group_name}_{len(target_gpus)}gpu"
        
        # Determine framework type and packages
        if framework_name == "pytorch" and any(req.gpu_info.vendor == GPUVendor.NVIDIA for req in requirements):
            framework_type = FrameworkType.PYTORCH_CUDA
            base_packages, gpu_packages, extra_urls = self._get_pytorch_cuda_packages()
        
        elif framework_name == "pytorch" and any(req.gpu_info.architecture in ["rdna3", "rdna4"] for req in requirements):
            framework_type = FrameworkType.PYTORCH_ROCM
            base_packages, gpu_packages, extra_urls = self._get_pytorch_rocm_packages()
        
        elif framework_name == "directml":
            framework_type = FrameworkType.DIRECTML
            base_packages, gpu_packages, extra_urls = self._get_directml_packages()
        
        else:
            framework_type = FrameworkType.PYTORCH
            base_packages, gpu_packages, extra_urls = self._get_default_packages()
        
        # Merge additional packages from requirements
        additional_packages = []
        for req in requirements:
            additional_packages.extend(req.required_packages)
        
        # Remove duplicates and conflicts
        additional_packages = list(set(additional_packages))
        additional_packages = self._remove_conflicting_packages(additional_packages, gpu_packages)
        
        # Environment variables
        env_vars = self._get_environment_variables(framework_type, target_gpus)
        
        # Validation commands
        validation_commands = [req.validation_script for req in requirements if req.validation_script]
        
        # Conflicting environments
        conflicting_envs = []
        for req in requirements:
            conflicting_envs.extend(req.conflicts_with)
        
        return EnvironmentSpec(
            name=env_name,
            env_type=env_type,
            framework=framework_type,
            python_version=self.python_version,
            base_packages=base_packages,
            gpu_packages=gpu_packages,
            additional_packages=additional_packages,
            pip_extra_index_urls=extra_urls,
            environment_variables=env_vars,
            validation_commands=validation_commands,
            conflicting_envs=list(set(conflicting_envs)),
            target_gpus=target_gpus
        )
    
    def _get_pytorch_cuda_packages(self) -> Tuple[List[str], List[str], List[str]]:
        """Get PyTorch CUDA packages"""
        base_packages = [
            "pip>=23.0",
            "setuptools>=65.0",
            "wheel>=0.38.0",
            "numpy>=1.24.0",
            "pillow>=9.0.0",
        ]
        
        gpu_packages = [
            "torch>=2.1.0+cu118",
            "torchvision>=0.16.0+cu118", 
            "torchaudio>=2.1.0+cu118",
            "transformers>=4.35.0",
            "diffusers>=0.24.0",
            "accelerate>=0.25.0",
            "bitsandbytes>=0.41.0",
        ]
        
        extra_urls = [
            "https://download.pytorch.org/whl/cu118"
        ]
        
        return base_packages, gpu_packages, extra_urls
    
    def _get_pytorch_rocm_packages(self) -> Tuple[List[str], List[str], List[str]]:
        """Get PyTorch ROCm packages"""
        base_packages = [
            "pip>=23.0",
            "setuptools>=65.0", 
            "wheel>=0.38.0",
            "numpy>=1.24.0",
            "pillow>=9.0.0",
        ]
        
        gpu_packages = [
            "torch>=2.1.0+rocm6.0",
            "torchvision>=0.16.0+rocm6.0",
            "torchaudio>=2.1.0+rocm6.0",
            "transformers>=4.35.0",
            "diffusers>=0.24.0",
            "accelerate>=0.25.0",
        ]
        
        extra_urls = [
            "https://download.pytorch.org/whl/rocm6.0"
        ]
        
        return base_packages, gpu_packages, extra_urls
    
    def _get_directml_packages(self) -> Tuple[List[str], List[str], List[str]]:
        """Get DirectML packages"""
        base_packages = [
            "pip>=23.0",
            "setuptools>=65.0",
            "wheel>=0.38.0",
            "numpy>=1.24.0",
            "pillow>=9.0.0",
        ]
        
        gpu_packages = [
            "torch-directml>=0.2.0",
            "onnxruntime-directml>=1.16.0",
            "transformers>=4.35.0",
            "diffusers>=0.24.0",
        ]
        
        extra_urls = []
        
        return base_packages, gpu_packages, extra_urls
    
    def _get_default_packages(self) -> Tuple[List[str], List[str], List[str]]:
        """Get default CPU-only packages"""
        base_packages = [
            "pip>=23.0",
            "setuptools>=65.0",
            "wheel>=0.38.0", 
            "numpy>=1.24.0",
            "pillow>=9.0.0",
        ]
        
        gpu_packages = [
            "torch>=2.1.0+cpu",
            "torchvision>=0.16.0+cpu",
            "torchaudio>=2.1.0+cpu",
            "transformers>=4.35.0",
        ]
        
        extra_urls = [
            "https://download.pytorch.org/whl/cpu"
        ]
        
        return base_packages, gpu_packages, extra_urls
    
    def _remove_conflicting_packages(self, additional: List[str], gpu_packages: List[str]) -> List[str]:
        """Remove packages that conflict with GPU packages"""
        # Extract package names without version constraints
        gpu_names = set()
        for pkg in gpu_packages:
            name = pkg.split('>=')[0].split('==')[0].split('+')[0]
            gpu_names.add(name)
        
        # Filter out conflicting packages
        filtered = []
        for pkg in additional:
            name = pkg.split('>=')[0].split('==')[0].split('+')[0]
            if name not in gpu_names:
                filtered.append(pkg)
        
        return filtered
    
    def _get_environment_variables(self, framework: FrameworkType, target_gpus: List[str]) -> Dict[str, str]:
        """Get environment variables for framework"""
        env_vars = {}
        
        if framework == FrameworkType.PYTORCH_CUDA:
            env_vars.update({
                "CUDA_VISIBLE_DEVICES": ",".join([gpu.split(':')[1] for gpu in target_gpus if gpu.startswith('nvidia:')]),
                "PYTORCH_CUDA_ALLOC_CONF": "max_split_size_mb:512",
                "TRANSFORMERS_CACHE": str(self.base_path / "cache" / "transformers"),
                "HF_HOME": str(self.base_path / "cache" / "huggingface"),
            })
        
        elif framework == FrameworkType.PYTORCH_ROCM:
            env_vars.update({
                "HIP_VISIBLE_DEVICES": ",".join([gpu.split(':')[1] for gpu in target_gpus if gpu.startswith('amd:')]),
                "ROCM_PATH": "/opt/rocm",
                "TRANSFORMERS_CACHE": str(self.base_path / "cache" / "transformers"),
                "HF_HOME": str(self.base_path / "cache" / "huggingface"),
            })
        
        elif framework == FrameworkType.DIRECTML:
            env_vars.update({
                "ONNX_BACKEND": "directml",
                "TRANSFORMERS_CACHE": str(self.base_path / "cache" / "transformers"),
                "HF_HOME": str(self.base_path / "cache" / "huggingface"),
            })
        
        return env_vars
    
    async def create_environment(self, spec: EnvironmentSpec) -> EnvironmentSetupResult:
        """Create environment from specification"""
        logger.info(f"Creating environment: {spec.name}")
        
        result = EnvironmentSetupResult(
            env_name=spec.name,
            success=False,
            path=None,
            python_executable=None,
            installed_packages=[],
            validation_results={},
            errors=[],
            warnings=[]
        )
        
        try:
            if spec.env_type == EnvironmentType.VENV:
                return await self._create_venv_environment(spec, result)
            elif spec.env_type == EnvironmentType.NATIVE:
                return await self._create_native_environment(spec, result)
            else:
                result.errors.append(f"Unsupported environment type: {spec.env_type}")
                return result
                
        except Exception as e:
            result.errors.append(f"Failed to create environment: {str(e)}")
            logger.error(f"Environment creation failed", env_name=spec.name, error=str(e))
            return result
    
    async def _create_venv_environment(self, spec: EnvironmentSpec, result: EnvironmentSetupResult) -> EnvironmentSetupResult:
        """Create virtual environment"""
        env_path = self.base_path / spec.name
        
        # Remove existing environment if it exists
        if env_path.exists():
            shutil.rmtree(env_path)
        
        # Create virtual environment
        subprocess.run([sys.executable, "-m", "venv", str(env_path)], check=True)
        
        # Get Python executable
        if self.system_os == "windows":
            python_exe = env_path / "Scripts" / "python.exe"
            pip_exe = env_path / "Scripts" / "pip.exe"
        else:
            python_exe = env_path / "bin" / "python"
            pip_exe = env_path / "bin" / "pip"
        
        result.path = str(env_path)
        result.python_executable = str(python_exe)
          # Install packages
        await self._install_packages(spec, str(pip_exe), result)
        
        return result
    
    async def _create_native_environment(self, spec: EnvironmentSpec, result: EnvironmentSetupResult) -> EnvironmentSetupResult:
        """Setup native (system) environment"""
        # For native environments (like DirectML), install to system Python
        result.path = "native"
        result.python_executable = sys.executable
        
        # Install packages to system Python
        await self._install_packages(spec, "pip", result)
        
        return result
    
    async def _install_packages(self, spec: EnvironmentSpec, pip_exe: str, result: EnvironmentSetupResult):
        """Install packages to environment"""
        all_packages = spec.base_packages + spec.gpu_packages + spec.additional_packages
        
        # Build pip install command
        cmd = [str(pip_exe), "install", "--upgrade"]
        
        # Add extra index URLs
        for url in spec.pip_extra_index_urls:
            cmd.extend(["--extra-index-url", url])
        
        # Add packages
        cmd.extend(all_packages)
        
        try:
            # Run pip install
            process = await asyncio.create_subprocess_exec(
                *cmd,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            
            stdout, stderr = await process.communicate()
            
            if process.returncode == 0:
                result.installed_packages = all_packages
                result.success = True
                logger.info(f"Successfully installed packages for {spec.name}")
            else:
                result.errors.append(f"Package installation failed: {stderr.decode()}")
                logger.error(f"Package installation failed for {spec.name}", stderr=stderr.decode())
                
        except Exception as e:
            result.errors.append(f"Package installation error: {str(e)}")
    
    def save_environment_specs(self, filename: str = "environment_specs.json"):
        """Save environment specifications to file"""
        specs_data = {}
        for name, spec in self.env_specs.items():
            specs_data[name] = asdict(spec)
        
        with open(self.base_path / filename, 'w') as f:
            json.dump(specs_data, f, indent=2, default=str)
        
        logger.info(f"Saved {len(specs_data)} environment specifications")
    
    def load_environment_specs(self, filename: str = "environment_specs.json") -> Dict[str, EnvironmentSpec]:
        """Load environment specifications from file"""
        try:
            with open(self.base_path / filename, 'r') as f:
                specs_data = json.load(f)
            
            specs = {}
            for name, data in specs_data.items():
                # Convert back to enums
                data['env_type'] = EnvironmentType(data['env_type'])
                data['framework'] = FrameworkType(data['framework'])
                specs[name] = EnvironmentSpec(**data)
            
            self.env_specs = specs
            logger.info(f"Loaded {len(specs)} environment specifications")
            return specs
            
        except FileNotFoundError:
            logger.warning(f"Environment specs file not found: {filename}")
            return {}
        except Exception as e:
            logger.error(f"Failed to load environment specs", error=str(e))
            return {}
