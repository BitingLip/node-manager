"""
Environment Strategy Analysis & Enhancement
Analyzes current environment setup against new GPU-specific venv strategy
Adds OS detection, WSL support, and AMD RDNA architecture-specific handling
"""

import platform
import subprocess
import os
import sys
from pathlib import Path
from typing import Dict, List, Optional, Any, Tuple
from enum import Enum
from dataclasses import dataclass
import structlog

logger = structlog.get_logger(__name__)


class OSType(Enum):
    """Operating System types"""
    WINDOWS_NATIVE = "windows_native"
    WINDOWS_WSL = "windows_wsl"
    LINUX_NATIVE = "linux_native"
    MACOS = "macos"
    UNKNOWN = "unknown"


class AMDDirectMLCompatibility(Enum):
    """AMD DirectML compatibility levels"""
    SYSTEM_PYTHON_REQUIRED = "system_python_required"  # RDNA1, RDNA2
    VENV_COMPATIBLE = "venv_compatible"  # RDNA3, RDNA4
    ROCM_LINUX_ONLY = "rocm_linux_only"  # All RDNA on Linux
    NOT_SUPPORTED = "not_supported"


@dataclass
class SystemEnvironmentInfo:
    """Complete system environment information"""
    os_type: OSType
    os_version: str
    python_version: str
    wsl_available: bool
    wsl_version: Optional[str]
    wsl_distributions: List[str]
    docker_available: bool
    conda_available: bool
    virtualization_support: bool
    recommendations: List[str]


class EnvironmentStrategyAnalyzer:
    """
    Analyzes system capabilities and GPU-specific environment requirements
    Implements the new strategy: venv in WSL for most cases, system Python for RDNA1/2
    """
    
    def __init__(self):
        """Initialize environment strategy analyzer"""
        self.system_info = self._detect_system_environment()
        logger.info("Environment strategy analyzer initialized", 
                   os_type=self.system_info.os_type.value)
    
    def _detect_system_environment(self) -> SystemEnvironmentInfo:
        """Detect comprehensive system environment information"""
        logger.info("Detecting system environment...")
        
        # Basic OS detection
        system = platform.system().lower()
        version = platform.release()
        python_version = f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}"
        
        # Determine OS type
        os_type = self._determine_os_type(system)
        
        # WSL detection
        wsl_available, wsl_version, wsl_distributions = self._detect_wsl()
        
        # Other capabilities
        docker_available = self._check_docker()
        conda_available = self._check_conda()
        virtualization_support = self._check_virtualization_support()
        
        # Generate recommendations
        recommendations = self._generate_system_recommendations(os_type, wsl_available)
        
        return SystemEnvironmentInfo(
            os_type=os_type,
            os_version=version,
            python_version=python_version,
            wsl_available=wsl_available,
            wsl_version=wsl_version,
            wsl_distributions=wsl_distributions,
            docker_available=docker_available,
            conda_available=conda_available,
            virtualization_support=virtualization_support,
            recommendations=recommendations
        )
    
    def _determine_os_type(self, system: str) -> OSType:
        """Determine detailed OS type"""
        if system == "windows":
            # Check if we're running in WSL
            if os.path.exists('/proc/version'):
                try:
                    with open('/proc/version', 'r') as f:
                        version_info = f.read().lower()
                        if 'microsoft' in version_info or 'wsl' in version_info:
                            return OSType.WINDOWS_WSL
                except:
                    pass
            return OSType.WINDOWS_NATIVE
        elif system == "linux":
            return OSType.LINUX_NATIVE        elif system == "darwin":
            return OSType.MACOS
        else:
            return OSType.UNKNOWN
    
    def _detect_wsl(self) -> Tuple[bool, Optional[str], List[str]]:
        """Detect WSL availability and versions"""
        # Check if we're already in WSL
        if os.path.exists('/proc/version'):
            try:
                with open('/proc/version', 'r') as f:
                    version_info = f.read().lower()
                    if 'microsoft' in version_info or 'wsl' in version_info:
                        return True, "2.0", ["current"]
            except:
                pass
        
        if platform.system().lower() != "windows":
            return False, None, []
        
        try:
            # Check WSL availability
            result = subprocess.run(
                ["wsl", "--status"],
                capture_output=True,
                text=True,
                timeout=10
            )
            
            if result.returncode == 0:
                # WSL is available, get version
                version_result = subprocess.run(
                    ["wsl", "--version"],
                    capture_output=True,
                    text=True,
                    timeout=5
                )
                
                wsl_version = "1.0"  # Default
                if version_result.returncode == 0 and "WSL version" in version_result.stdout:
                    # WSL 2 is available
                    wsl_version = "2.0"
                
                # Get distributions
                list_result = subprocess.run(
                    ["wsl", "-l", "-v"],
                    capture_output=True,
                    text=True,
                    timeout=5
                )
                
                distributions = []
                if list_result.returncode == 0:
                    lines = list_result.stdout.split('\n')
                    for line in lines[1:]:  # Skip header
                        if line.strip():
                            parts = line.split()
                            if len(parts) >= 1:
                                dist_name = parts[0].strip('*').strip()
                                if dist_name and dist_name != "NAME":
                                    distributions.append(dist_name)
                
                return True, wsl_version, distributions
            
        except (subprocess.TimeoutExpired, FileNotFoundError, Exception) as e:
            logger.debug(f"WSL detection failed: {e}")
        
        return False, None, []
    
    def _check_docker(self) -> bool:
        """Check Docker availability"""
        try:
            result = subprocess.run(
                ["docker", "--version"],
                capture_output=True,
                text=True,
                timeout=5
            )
            return result.returncode == 0
        except:
            return False
    
    def _check_conda(self) -> bool:
        """Check Conda availability"""
        try:
            result = subprocess.run(
                ["conda", "--version"],
                capture_output=True,
                text=True,
                timeout=5
            )
            return result.returncode == 0
        except:
            return False
    
    def _check_virtualization_support(self) -> bool:
        """Check virtualization support"""
        if platform.system().lower() == "windows":
            try:
                # Check Hyper-V support
                result = subprocess.run(
                    ["powershell", "-Command", "Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All"],
                    capture_output=True,
                    text=True,
                    timeout=10
                )
                return "Enabled" in result.stdout
            except:
                return False
        return True  # Assume Linux/macOS support virtualization
    
    def _generate_system_recommendations(self, os_type: OSType, wsl_available: bool) -> List[str]:
        """Generate system-specific recommendations"""
        recommendations = []
        
        if os_type == OSType.WINDOWS_NATIVE:
            if wsl_available:
                recommendations.append("WSL detected - optimal for NVIDIA GPU environments")
                recommendations.append("Consider WSL for mixed GPU setups with RDNA3+ AMD GPUs")
            else:
                recommendations.append("Consider installing WSL for better GPU environment management")
                recommendations.append("NVIDIA GPU support will require native Windows CUDA")
        
        elif os_type == OSType.WINDOWS_WSL:
            recommendations.append("Running in WSL - excellent for NVIDIA and modern AMD GPUs")
            recommendations.append("ROCm support available for AMD RDNA3+ architectures")
        
        elif os_type == OSType.LINUX_NATIVE:
            recommendations.append("Native Linux - optimal for all GPU types")
            recommendations.append("Full ROCm support for AMD GPUs")
            recommendations.append("Native CUDA support for NVIDIA GPUs")
        
        return recommendations
    
    def analyze_gpu_environment_strategy(self, gpu_infos: List[Dict[str, Any]]) -> Dict[str, Any]:
        """
        Analyze GPU configuration and determine optimal environment strategy
        Based on new rules:
        - venv in WSL for most cases
        - System Python only for RDNA1/RDNA2 on Windows
        """
        logger.info(f"Analyzing environment strategy for {len(gpu_infos)} GPUs")
        
        analysis = {
            "system_info": self.system_info,
            "gpu_analysis": {},
            "environment_strategy": {},
            "recommendations": [],
            "warnings": []
        }
        
        # Analyze each GPU
        amd_architectures = []
        nvidia_count = 0
        
        for gpu in gpu_infos:
            gpu_analysis = self._analyze_individual_gpu(gpu)
            analysis["gpu_analysis"][gpu.get("name", "unknown")] = gpu_analysis
            
            if gpu_analysis["vendor"] == "amd":
                amd_architectures.append(gpu_analysis["architecture"])
            elif gpu_analysis["vendor"] == "nvidia":
                nvidia_count += 1
        
        # Determine overall strategy
        strategy = self._determine_environment_strategy(amd_architectures, nvidia_count)
        analysis["environment_strategy"] = strategy
        
        # Generate recommendations
        analysis["recommendations"] = self._generate_strategy_recommendations(strategy, amd_architectures, nvidia_count)
        
        # Generate warnings
        analysis["warnings"] = self._generate_strategy_warnings(strategy, amd_architectures)
        
        return analysis
    
    def _analyze_individual_gpu(self, gpu_info: Dict[str, Any]) -> Dict[str, Any]:
        """Analyze individual GPU for environment requirements"""
        name = gpu_info.get("name", "").lower()
        vendor = gpu_info.get("vendor", "unknown").lower()
        
        analysis = {
            "vendor": vendor,
            "name": gpu_info.get("name", "unknown"),
            "architecture": "unknown",
            "directml_compatibility": AMDDirectMLCompatibility.NOT_SUPPORTED,
            "environment_recommendation": "venv",
            "special_requirements": []
        }
        
        if vendor == "amd":
            # Determine AMD architecture
            if "6800" in name or "6900" in name or "6700" in name or "6600" in name:
                analysis["architecture"] = "rdna2"
                analysis["directml_compatibility"] = AMDDirectMLCompatibility.SYSTEM_PYTHON_REQUIRED
                analysis["special_requirements"].append("Requires Adrenalin Edition 23.40.27.06")
                analysis["special_requirements"].append("System Python required for DirectML")
                
            elif "5700" in name or "5600" in name or "5500" in name:
                analysis["architecture"] = "rdna1"
                analysis["directml_compatibility"] = AMDDirectMLCompatibility.SYSTEM_PYTHON_REQUIRED
                analysis["special_requirements"].append("Requires Adrenalin Edition 23.40.27.06")
                analysis["special_requirements"].append("System Python required for DirectML")
                
            elif "7900" in name or "7800" in name or "7700" in name or "7600" in name:
                analysis["architecture"] = "rdna3"
                analysis["directml_compatibility"] = AMDDirectMLCompatibility.VENV_COMPATIBLE
                analysis["environment_recommendation"] = "venv_wsl_preferred"
                analysis["special_requirements"].append("ROCm support on Linux")
                
            elif "8000" in name:  # Future RDNA4
                analysis["architecture"] = "rdna4"
                analysis["directml_compatibility"] = AMDDirectMLCompatibility.VENV_COMPATIBLE
                analysis["environment_recommendation"] = "venv_wsl_preferred"
                analysis["special_requirements"].append("ROCm support on Linux")
            
        elif vendor == "nvidia":
            analysis["environment_recommendation"] = "venv_cuda"
            analysis["special_requirements"].append("CUDA support excellent in venv")
            analysis["special_requirements"].append("WSL provides optimal Linux CUDA environment")
        
        return analysis
    
    def _determine_environment_strategy(self, amd_architectures: List[str], nvidia_count: int) -> Dict[str, Any]:
        """Determine overall environment strategy"""
        has_rdna1_rdna2 = any(arch in ["rdna1", "rdna2"] for arch in amd_architectures)
        has_rdna3_rdna4 = any(arch in ["rdna3", "rdna4"] for arch in amd_architectures)
        has_nvidia = nvidia_count > 0
        
        strategy = {
            "primary_approach": "mixed",
            "amd_rdna1_rdna2_handling": "system_python",
            "amd_rdna3_rdna4_handling": "venv_wsl",
            "nvidia_handling": "venv_cuda",
            "mixed_gpu_complexity": "medium"
        }
        
        # Determine primary approach
        if not has_rdna1_rdna2 and not has_rdna3_rdna4 and has_nvidia:
            # NVIDIA only
            strategy["primary_approach"] = "nvidia_only_venv"
            strategy["mixed_gpu_complexity"] = "low"
            
        elif has_rdna1_rdna2 and not has_rdna3_rdna4 and not has_nvidia:
            # RDNA1/2 only
            strategy["primary_approach"] = "amd_legacy_system_python"
            strategy["mixed_gpu_complexity"] = "low"
            
        elif not has_rdna1_rdna2 and has_rdna3_rdna4 and not has_nvidia:
            # RDNA3/4 only
            strategy["primary_approach"] = "amd_modern_venv"
            strategy["mixed_gpu_complexity"] = "low"
            
        elif has_rdna1_rdna2 and (has_rdna3_rdna4 or has_nvidia):
            # Mixed with legacy AMD
            strategy["primary_approach"] = "complex_mixed_legacy_amd"
            strategy["mixed_gpu_complexity"] = "high"
            
        else:
            # Other mixed scenarios
            strategy["primary_approach"] = "mixed"
            strategy["mixed_gpu_complexity"] = "medium"
          # WSL recommendations
        if self.system_info.os_type == OSType.WINDOWS_NATIVE:
            if self.system_info.wsl_available and (has_nvidia or has_rdna3_rdna4):
                strategy["wsl_recommended"] = "yes"
                strategy["wsl_reason"] = "Better GPU environment isolation and ROCm support"
            else:
                strategy["wsl_recommended"] = "no"
                strategy["wsl_reason"] = "Not required for current GPU configuration"
        
        return strategy
    
    def _generate_strategy_recommendations(self, strategy: Dict[str, Any], 
                                         amd_architectures: List[str], 
                                         nvidia_count: int) -> List[str]:
        """Generate strategic recommendations"""
        recommendations = []
        
        primary_approach = strategy["primary_approach"]
        
        if primary_approach == "nvidia_only_venv":
            recommendations.append("🎯 NVIDIA-only setup: Use venv with CUDA")
            if self.system_info.wsl_available:
                recommendations.append("💡 Consider WSL for optimal Linux CUDA environment")
        
        elif primary_approach == "amd_legacy_system_python":
            recommendations.append("🎯 AMD RDNA1/2 setup: Use system Python with DirectML")
            recommendations.append("⚠️ Virtual environments will break DirectML functionality")
            recommendations.append("💡 Ensure Adrenalin Edition 23.40.27.06 is installed")
        
        elif primary_approach == "amd_modern_venv":
            recommendations.append("🎯 AMD RDNA3+ setup: Use venv with ROCm")
            if self.system_info.wsl_available:
                recommendations.append("💡 WSL provides excellent ROCm support")
        
        elif primary_approach == "complex_mixed_legacy_amd":
            recommendations.append("🎯 Complex mixed setup with legacy AMD GPUs")
            recommendations.append("⚠️ RDNA1/2 GPUs require system Python")
            recommendations.append("💡 Modern GPUs (NVIDIA/RDNA3+) can use venv in WSL")
            recommendations.append("🔧 Consider separate environments per GPU type")
        
        else:
            recommendations.append("🎯 Mixed GPU setup: Hybrid environment strategy")
            recommendations.append("💡 Each GPU type has specific environment requirements")
        
        return recommendations
    
    def _generate_strategy_warnings(self, strategy: Dict[str, Any], 
                                  amd_architectures: List[str]) -> List[str]:
        """Generate strategy warnings"""
        warnings = []
        
        has_rdna1_rdna2 = any(arch in ["rdna1", "rdna2"] for arch in amd_architectures)
        
        if has_rdna1_rdna2:
            warnings.append("⚠️ RDNA1/RDNA2 GPUs detected: DirectML requires system Python")
            warnings.append("⚠️ Virtual environments will break AMD GPU AI functionality")
            
            if self.system_info.os_type == OSType.WINDOWS_NATIVE:
                warnings.append("⚠️ Windows native: Ensure Adrenalin drivers are properly installed")
        
        if strategy["mixed_gpu_complexity"] == "high":
            warnings.append("⚠️ Complex mixed GPU setup: Environment management will be challenging")
            warnings.append("⚠️ Some GPU types may require different Python environments")
        
        return warnings
    
    def generate_implementation_plan(self, gpu_infos: List[Dict[str, Any]]) -> Dict[str, Any]:
        """Generate detailed implementation plan for the environment strategy"""
        analysis = self.analyze_gpu_environment_strategy(gpu_infos)
        
        plan = {
            "analysis_summary": analysis,
            "implementation_steps": [],
            "environment_configurations": {},
            "validation_procedures": [],
            "troubleshooting_guide": {}
        }
        
        strategy = analysis["environment_strategy"]
        primary_approach = strategy["primary_approach"]
        
        # Generate implementation steps
        if primary_approach == "amd_legacy_system_python":
            plan["implementation_steps"] = self._generate_amd_legacy_steps()
        elif primary_approach == "nvidia_only_venv":
            plan["implementation_steps"] = self._generate_nvidia_venv_steps()
        elif primary_approach == "amd_modern_venv":
            plan["implementation_steps"] = self._generate_amd_modern_steps()
        elif primary_approach == "complex_mixed_legacy_amd":
            plan["implementation_steps"] = self._generate_complex_mixed_steps()
        else:
            plan["implementation_steps"] = self._generate_general_mixed_steps()
        
        return plan
    
    def _generate_amd_legacy_steps(self) -> List[Dict[str, Any]]:
        """Implementation steps for AMD RDNA1/2 system Python setup"""
        return [
            {
                "step": 1,
                "title": "Verify AMD Adrenalin Drivers",
                "description": "Ensure Adrenalin Edition 23.40.27.06+ is installed",
                "commands": ["Control Panel -> Programs -> AMD Software"],
                "validation": "DirectML device detection"
            },
            {
                "step": 2,
                "title": "Use System Python",
                "description": "Install packages in system Python (not venv)",
                "commands": ["pip install torch-directml", "pip install transformers"],
                "validation": "torch.device('dml') test"
            },
            {
                "step": 3,
                "title": "Configure Environment Variables",
                "description": "Set up DirectML environment",
                "commands": ["set PYTORCH_ENABLE_MPS_FALLBACK=1"],
                "validation": "Environment variable check"
            }
        ]
    
    def _generate_nvidia_venv_steps(self) -> List[Dict[str, Any]]:
        """Implementation steps for NVIDIA-only venv setup"""
        return [
            {
                "step": 1,
                "title": "Create Virtual Environment",
                "description": "Set up isolated venv for CUDA",
                "commands": ["python -m venv nvidia_env", "nvidia_env\\Scripts\\activate"],
                "validation": "Virtual environment activation"
            },
            {
                "step": 2,
                "title": "Install CUDA PyTorch",
                "description": "Install PyTorch with CUDA support",
                "commands": ["pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121"],
                "validation": "torch.cuda.is_available() test"
            }
        ]
    
    def _generate_amd_modern_steps(self) -> List[Dict[str, Any]]:
        """Implementation steps for AMD RDNA3+ venv setup"""
        return [
            {
                "step": 1,
                "title": "Set up WSL (if Windows)",
                "description": "Configure WSL for ROCm support",
                "commands": ["wsl --install Ubuntu", "wsl --set-version Ubuntu 2"],
                "validation": "WSL Ubuntu access"
            },
            {
                "step": 2,
                "title": "Create ROCm Environment",
                "description": "Set up venv with ROCm",
                "commands": ["python -m venv rocm_env", "source rocm_env/bin/activate"],
                "validation": "Virtual environment activation"
            }
        ]
    
    def _generate_complex_mixed_steps(self) -> List[Dict[str, Any]]:
        """Implementation steps for complex mixed GPU setup"""
        return [
            {
                "step": 1,
                "title": "System Python for RDNA1/2",
                "description": "Configure system Python for legacy AMD GPUs",
                "commands": ["pip install torch-directml (system)"],
                "validation": "DirectML device detection"
            },
            {
                "step": 2,
                "title": "WSL venv for Modern GPUs", 
                "description": "Set up WSL environments for NVIDIA and RDNA3+",
                "commands": ["wsl --install", "Create separate venvs"],
                "validation": "Multiple environment access"
            },
            {
                "step": 3,
                "title": "Environment Selection Logic",
                "description": "Implement GPU-specific environment routing",
                "commands": ["Create environment selector script"],
                "validation": "Automatic GPU-environment matching"
            }
        ]
    
    def _generate_general_mixed_steps(self) -> List[Dict[str, Any]]:
        """Implementation steps for general mixed GPU setup"""
        return [
            {
                "step": 1,
                "title": "Analyze GPU Requirements",
                "description": "Determine per-GPU environment needs",
                "commands": ["Run GPU detection and analysis"],
                "validation": "Complete GPU inventory"
            },
            {
                "step": 2,
                "title": "Create Appropriate Environments",
                "description": "Set up environments based on GPU types",
                "commands": ["Various based on GPU analysis"],
                "validation": "All environments functional"
            }
        ]


async def main():
    """Test environment strategy analysis"""
    print("🔍 Environment Strategy Analysis")
    print("=" * 60)
    
    analyzer = EnvironmentStrategyAnalyzer()
    
    # Display system info
    system_info = analyzer.system_info
    print(f"\\n🖥️ System Information:")
    print(f"   OS Type: {system_info.os_type.value}")
    print(f"   OS Version: {system_info.os_version}")
    print(f"   Python: {system_info.python_version}")
    print(f"   WSL Available: {system_info.wsl_available}")
    if system_info.wsl_available:
        print(f"   WSL Version: {system_info.wsl_version}")
        print(f"   WSL Distributions: {', '.join(system_info.wsl_distributions)}")
    print(f"   Docker: {system_info.docker_available}")
    print(f"   Conda: {system_info.conda_available}")
    
    # Simulate your GPU configuration (4x RX 6800 + 1x RX 6800 XT)
    test_gpus = [
        {"name": "AMD Radeon RX 6800", "vendor": "amd"},
        {"name": "AMD Radeon RX 6800", "vendor": "amd"},
        {"name": "AMD Radeon RX 6800", "vendor": "amd"},
        {"name": "AMD Radeon RX 6800", "vendor": "amd"},
        {"name": "AMD Radeon RX 6800 XT", "vendor": "amd"}
    ]
    
    # Analyze strategy
    analysis = analyzer.analyze_gpu_environment_strategy(test_gpus)
    
    print(f"\\n🎯 Environment Strategy Analysis:")
    strategy = analysis["environment_strategy"]
    print(f"   Primary Approach: {strategy['primary_approach']}")
    print(f"   Complexity: {strategy['mixed_gpu_complexity']}")
    print(f"   AMD RDNA1/2 Handling: {strategy['amd_rdna1_rdna2_handling']}")
    
    print(f"\\n💡 Recommendations:")
    for rec in analysis["recommendations"]:
        print(f"   {rec}")
    
    if analysis["warnings"]:
        print(f"\\n⚠️ Warnings:")
        for warning in analysis["warnings"]:
            print(f"   {warning}")
    
    # Generate implementation plan
    plan = analyzer.generate_implementation_plan(test_gpus)
    print(f"\\n🔧 Implementation Plan:")
    for step in plan["implementation_steps"]:
        print(f"   Step {step['step']}: {step['title']}")
        print(f"      {step['description']}")

if __name__ == "__main__":
    import asyncio
    asyncio.run(main())
