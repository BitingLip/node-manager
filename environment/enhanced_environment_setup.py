"""
Enhanced Environment Setup - GPU Strategy Implementation
Implements RDNA4 support, OS detection, WSL handling, and GPU-specific environment strategy
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
    """Operating System types with WSL detection"""
    WINDOWS_NATIVE = "windows_native"
    WINDOWS_WSL = "windows_wsl"
    LINUX_NATIVE = "linux_native"
    MACOS = "macos"
    UNKNOWN = "unknown"


class GPUEnvironmentStrategy(Enum):
    """GPU-specific environment strategies"""
    SYSTEM_PYTHON_REQUIRED = "system_python_required"  # RDNA1, RDNA2
    VENV_WSL_PREFERRED = "venv_wsl_preferred"  # RDNA3, RDNA4, NVIDIA
    VENV_CUDA = "venv_cuda"  # NVIDIA specific
    MIXED_COMPLEX = "mixed_complex"  # Multiple GPU types


@dataclass
class EnhancedSystemInfo:
    """Enhanced system information with GPU strategy awareness"""
    os_type: OSType
    os_version: str
    python_version: str
    wsl_available: bool
    wsl_version: Optional[str]
    wsl_distributions: List[str]
    is_virtual_env: bool
    gpu_strategy_compatibility: Dict[str, bool]
    recommendations: List[str]


class EnhancedEnvironmentSetup:
    """
    Enhanced environment setup with GPU-specific strategy implementation
    Handles RDNA4, OS detection, WSL, and the new venv strategy
    """
    
    def __init__(self):
        """Initialize enhanced environment setup"""
        self.system_info = self._detect_enhanced_system_info()
        self.gpu_strategies = self._initialize_gpu_strategies()
        
        logger.info(
            "Enhanced environment setup initialized",
            os_type=self.system_info.os_type.value,
            wsl_available=self.system_info.wsl_available,
            is_venv=self.system_info.is_virtual_env
        )
    
    def _detect_enhanced_system_info(self) -> EnhancedSystemInfo:
        """Detect comprehensive system information"""
        # Basic OS detection
        system = platform.system().lower()
        version = platform.release()
        python_version = f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}"
        
        # Determine OS type with WSL detection
        os_type = self._determine_os_type(system)
        
        # WSL detection
        wsl_available, wsl_version, wsl_distributions = self._detect_wsl(os_type)
        
        # Virtual environment detection
        is_virtual_env = self._detect_virtual_environment()
        
        # GPU strategy compatibility
        gpu_strategy_compatibility = self._assess_gpu_strategy_compatibility(os_type, is_virtual_env)
        
        # Generate recommendations
        recommendations = self._generate_system_recommendations(os_type, wsl_available, is_virtual_env)
        
        return EnhancedSystemInfo(
            os_type=os_type,
            os_version=version,
            python_version=python_version,
            wsl_available=wsl_available,
            wsl_version=wsl_version,
            wsl_distributions=wsl_distributions,
            is_virtual_env=is_virtual_env,
            gpu_strategy_compatibility=gpu_strategy_compatibility,
            recommendations=recommendations
        )
    
    def _determine_os_type(self, system: str) -> OSType:
        """Determine OS type with WSL detection"""
        if system == "windows":
            # Check if we're in WSL
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
            return OSType.LINUX_NATIVE
        elif system == "darwin":
            return OSType.MACOS
        else:
            return OSType.UNKNOWN
    
    def _detect_wsl(self, os_type: OSType) -> Tuple[bool, Optional[str], List[str]]:
        """Enhanced WSL detection"""
        if os_type == OSType.WINDOWS_WSL:
            # Already in WSL
            return True, "2.0", ["current"]
        
        if os_type != OSType.WINDOWS_NATIVE:
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
                # Get WSL version
                wsl_version = "2.0"  # Default assumption for modern WSL
                
                # Get distributions
                list_result = subprocess.run(
                    ["wsl", "-l", "-v"],
                    capture_output=True,
                    text=True,
                    timeout=5
                )
                
                distributions = []
                if list_result.returncode == 0:
                    lines = list_result.stdout.split('\\n')
                    for line in lines[1:]:  # Skip header
                        if line.strip():
                            parts = line.split()
                            if len(parts) >= 1:
                                dist_name = parts[0].strip('*').strip()
                                if dist_name and dist_name not in ["NAME", "STATE", "VERSION"]:
                                    distributions.append(dist_name)
                
                return True, wsl_version, distributions
            
        except Exception as e:
            logger.debug(f"WSL detection failed: {e}")
        
        return False, None, []
    
    def _detect_virtual_environment(self) -> bool:
        """Detect if running in virtual environment"""
        return (
            hasattr(sys, 'real_prefix') or 
            (hasattr(sys, 'base_prefix') and sys.base_prefix != sys.prefix) or
            os.environ.get('VIRTUAL_ENV') is not None
        )
    
    def _assess_gpu_strategy_compatibility(self, os_type: OSType, is_venv: bool) -> Dict[str, bool]:
        """Assess compatibility with different GPU strategies"""
        return {
            "amd_rdna1_rdna2_directml": not is_venv and os_type == OSType.WINDOWS_NATIVE,
            "amd_rdna3_rdna4_rocm": os_type in [OSType.LINUX_NATIVE, OSType.WINDOWS_WSL],
            "nvidia_cuda_venv": True,  # CUDA generally works in venv
            "mixed_gpu_support": os_type in [OSType.WINDOWS_NATIVE, OSType.LINUX_NATIVE, OSType.WINDOWS_WSL]
        }
    
    def _generate_system_recommendations(self, os_type: OSType, wsl_available: bool, is_venv: bool) -> List[str]:
        """Generate system-specific recommendations"""
        recommendations = []
        
        if os_type == OSType.WINDOWS_NATIVE:
            if is_venv:
                recommendations.append("⚠️ Virtual environment detected - may break AMD DirectML")
                recommendations.append("💡 Consider system Python for AMD RDNA1/2 GPUs")
            
            if wsl_available:
                recommendations.append("✅ WSL available - excellent for NVIDIA and RDNA3+ GPUs")
                recommendations.append("💡 Use WSL for modern GPU environments")
            else:
                recommendations.append("💡 Consider installing WSL for better GPU environment isolation")
        
        elif os_type == OSType.WINDOWS_WSL:
            recommendations.append("✅ Running in WSL - optimal for NVIDIA and modern AMD GPUs")
            recommendations.append("💡 ROCm support available for RDNA3+ AMD GPUs")
        
        elif os_type == OSType.LINUX_NATIVE:
            recommendations.append("✅ Native Linux - optimal for all GPU types")
            recommendations.append("💡 Full ROCm support for AMD GPUs")
        
        return recommendations
    
    def _initialize_gpu_strategies(self) -> Dict[str, Any]:
        """Initialize GPU-specific environment strategies"""
        return {
            "amd_rdna1": {
                "strategy": GPUEnvironmentStrategy.SYSTEM_PYTHON_REQUIRED,
                "directml_support": True,
                "rocm_support": False,
                "requirements": ["Adrenalin Edition 23.40.27.06+", "System Python"],
                "incompatibilities": ["Virtual environments", "ROCm on Linux"]
            },
            "amd_rdna2": {
                "strategy": GPUEnvironmentStrategy.SYSTEM_PYTHON_REQUIRED,
                "directml_support": True,
                "rocm_support": False,
                "requirements": ["Adrenalin Edition 23.40.27.06+", "System Python"],
                "incompatibilities": ["Virtual environments", "ROCm on Linux"]
            },
            "amd_rdna3": {
                "strategy": GPUEnvironmentStrategy.VENV_WSL_PREFERRED,
                "directml_support": True,
                "rocm_support": True,
                "requirements": ["ROCm 6.4.1+ on Linux", "Virtual environments supported"],
                "incompatibilities": []
            },
            "amd_rdna4": {
                "strategy": GPUEnvironmentStrategy.VENV_WSL_PREFERRED,
                "directml_support": True,
                "rocm_support": True,
                "requirements": ["Future ROCm support", "Virtual environments supported"],
                "incompatibilities": []
            },
            "nvidia": {
                "strategy": GPUEnvironmentStrategy.VENV_CUDA,
                "cuda_support": True,
                "requirements": ["CUDA toolkit", "Virtual environments supported"],
                "incompatibilities": []
            }
        }
    
    def analyze_gpu_environment_requirements(self, gpu_infos: List[Dict[str, Any]]) -> Dict[str, Any]:
        """Analyze GPU configuration and determine environment requirements"""
        analysis = {
            "system_info": self.system_info,
            "gpu_analysis": {},
            "overall_strategy": {},
            "environment_plan": {},
            "warnings": [],
            "recommendations": []
        }
        
        # Analyze each GPU
        gpu_strategies = []
        amd_rdna1_rdna2_count = 0
        amd_rdna3_rdna4_count = 0
        nvidia_count = 0
        
        for gpu in gpu_infos:
            gpu_analysis = self._analyze_individual_gpu(gpu)
            analysis["gpu_analysis"][gpu.get("name", "unknown")] = gpu_analysis
            
            if gpu_analysis["vendor"] == "amd":
                if gpu_analysis["architecture"] in ["rdna1", "rdna2"]:
                    amd_rdna1_rdna2_count += 1
                elif gpu_analysis["architecture"] in ["rdna3", "rdna4"]:
                    amd_rdna3_rdna4_count += 1
            elif gpu_analysis["vendor"] == "nvidia":
                nvidia_count += 1
            
            gpu_strategies.append(gpu_analysis["recommended_strategy"])
        
        # Determine overall strategy
        overall_strategy = self._determine_overall_strategy(
            amd_rdna1_rdna2_count, amd_rdna3_rdna4_count, nvidia_count
        )
        analysis["overall_strategy"] = overall_strategy
        
        # Create environment plan
        environment_plan = self._create_environment_plan(overall_strategy, gpu_strategies)
        analysis["environment_plan"] = environment_plan
        
        # Generate warnings and recommendations
        analysis["warnings"] = self._generate_strategy_warnings(overall_strategy, amd_rdna1_rdna2_count)
        analysis["recommendations"] = self._generate_strategy_recommendations(overall_strategy)
        
        return analysis
    
    def _analyze_individual_gpu(self, gpu_info: Dict[str, Any]) -> Dict[str, Any]:
        """Analyze individual GPU for environment strategy"""
        name = gpu_info.get("name", "").lower()
        vendor = gpu_info.get("vendor", "unknown").lower()
        
        analysis = {
            "vendor": vendor,
            "name": gpu_info.get("name", "unknown"),
            "architecture": "unknown",
            "recommended_strategy": GPUEnvironmentStrategy.SYSTEM_PYTHON_REQUIRED,
            "environment_type": "system_python",
            "special_requirements": [],
            "compatibility_notes": []
        }
        
        if vendor == "amd":
            # Enhanced AMD GPU detection with RDNA4 support
            if any(series in name for series in ["6800", "6900", "6700", "6600", "6500", "6400"]):
                analysis.update({
                    "architecture": "rdna2",
                    "recommended_strategy": GPUEnvironmentStrategy.SYSTEM_PYTHON_REQUIRED,
                    "environment_type": "system_python",
                    "special_requirements": [
                        "Adrenalin Edition 23.40.27.06+",
                        "System Python (not venv)",
                        "DirectML support"
                    ],
                    "compatibility_notes": [
                        "Virtual environments BREAK DirectML",
                        "ROCm 6.4.1 does NOT support RX 6000 series"
                    ]
                })
            
            elif any(series in name for series in ["5700", "5600", "5500"]):
                analysis.update({
                    "architecture": "rdna1",
                    "recommended_strategy": GPUEnvironmentStrategy.SYSTEM_PYTHON_REQUIRED,
                    "environment_type": "system_python",
                    "special_requirements": [
                        "Adrenalin Edition 23.40.27.06+",
                        "System Python (not venv)"
                    ],
                    "compatibility_notes": [
                        "Virtual environments BREAK DirectML"
                    ]
                })
            
            elif any(series in name for series in ["7900", "7800", "7700", "7600"]):
                analysis.update({
                    "architecture": "rdna3",
                    "recommended_strategy": GPUEnvironmentStrategy.VENV_WSL_PREFERRED,
                    "environment_type": "venv_wsl",
                    "special_requirements": [
                        "ROCm 6.4.1+ on Linux",
                        "Virtual environments supported"
                    ],
                    "compatibility_notes": [
                        "WSL provides optimal ROCm environment"
                    ]
                })
            
            elif any(series in name for series in ["8000", "8100", "8200", "8300", "8400", "8500", "8600", "8700", "8800", "8900"]):
                analysis.update({
                    "architecture": "rdna4",
                    "recommended_strategy": GPUEnvironmentStrategy.VENV_WSL_PREFERRED,
                    "environment_type": "venv_wsl",
                    "special_requirements": [
                        "Future ROCm support expected",
                        "Virtual environments supported"
                    ],
                    "compatibility_notes": [
                        "Next-generation AMD architecture",
                        "Enhanced Linux support expected"
                    ]
                })
        
        elif vendor == "nvidia":
            analysis.update({
                "recommended_strategy": GPUEnvironmentStrategy.VENV_CUDA,
                "environment_type": "venv_cuda",
                "special_requirements": [
                    "CUDA toolkit",
                    "Virtual environments supported"
                ],
                "compatibility_notes": [
                    "Excellent venv compatibility",
                    "WSL provides optimal Linux CUDA"
                ]
            })
        
        return analysis
    
    def _determine_overall_strategy(self, amd_rdna1_rdna2: int, amd_rdna3_rdna4: int, nvidia: int) -> Dict[str, Any]:
        """Determine overall environment strategy based on GPU mix"""
        total_gpus = amd_rdna1_rdna2 + amd_rdna3_rdna4 + nvidia
        
        strategy = {
            "gpu_counts": {
                "amd_rdna1_rdna2": amd_rdna1_rdna2,
                "amd_rdna3_rdna4": amd_rdna3_rdna4,
                "nvidia": nvidia,
                "total": total_gpus
            },
            "complexity": "unknown",
            "primary_approach": "unknown",
            "environment_types_needed": [],
            "implementation_notes": []
        }
        
        # Determine complexity and approach
        if amd_rdna1_rdna2 > 0 and (amd_rdna3_rdna4 > 0 or nvidia > 0):
            # Mixed setup with legacy AMD - most complex
            strategy.update({
                "complexity": "high",
                "primary_approach": "mixed_legacy_amd",
                "environment_types_needed": ["system_python", "venv_wsl"],
                "implementation_notes": [
                    "Legacy AMD GPUs require system Python",
                    "Modern GPUs can use venv in WSL",
                    "Environment selection logic needed"
                ]
            })
        
        elif amd_rdna1_rdna2 > 0 and amd_rdna3_rdna4 == 0 and nvidia == 0:
            # Only legacy AMD - simple but restricted
            strategy.update({
                "complexity": "low",
                "primary_approach": "legacy_amd_only",
                "environment_types_needed": ["system_python"],
                "implementation_notes": [
                    "System Python required for all GPUs",
                    "Virtual environments not supported"
                ]
            })
        
        elif amd_rdna1_rdna2 == 0 and (amd_rdna3_rdna4 > 0 or nvidia > 0):
            # Only modern GPUs - can use venv strategy
            strategy.update({
                "complexity": "medium",
                "primary_approach": "modern_gpus_venv",
                "environment_types_needed": ["venv_wsl"],
                "implementation_notes": [
                    "Virtual environments supported",
                    "WSL recommended for optimal support"
                ]
            })
        
        else:
            # No GPUs or unknown configuration
            strategy.update({
                "complexity": "minimal",
                "primary_approach": "cpu_only",
                "environment_types_needed": ["venv"],
                "implementation_notes": [
                    "CPU-only configuration",
                    "Standard venv approach"
                ]
            })
        
        return strategy
    
    def _create_environment_plan(self, overall_strategy: Dict[str, Any], gpu_strategies: List[Any]) -> Dict[str, Any]:
        """Create detailed environment implementation plan"""
        plan = {
            "primary_strategy": overall_strategy["primary_approach"],
            "environments_needed": {},
            "implementation_steps": [],
            "validation_procedures": []
        }
        
        # Define environments based on strategy
        if overall_strategy["primary_approach"] == "legacy_amd_only":
            plan["environments_needed"] = {
                "system_python_directml": {
                    "type": "system_python",
                    "purpose": "AMD RDNA1/2 DirectML support",
                    "packages": ["torch-directml", "transformers"],
                    "requirements": ["Adrenalin Edition 23.40.27.06+"]
                }
            }
        
        elif overall_strategy["primary_approach"] == "modern_gpus_venv":
            plan["environments_needed"] = {
                "venv_rocm": {
                    "type": "venv_wsl",
                    "purpose": "AMD RDNA3+ ROCm support",
                    "packages": ["torch", "rocm"],
                    "requirements": ["WSL", "ROCm 6.4.1+"]
                },
                "venv_cuda": {
                    "type": "venv",
                    "purpose": "NVIDIA CUDA support",
                    "packages": ["torch[cuda]"],
                    "requirements": ["CUDA toolkit"]
                }
            }
        
        elif overall_strategy["primary_approach"] == "mixed_legacy_amd":
            plan["environments_needed"] = {
                "system_python_directml": {
                    "type": "system_python",
                    "purpose": "AMD RDNA1/2 DirectML support",
                    "packages": ["torch-directml"],
                    "requirements": ["Adrenalin Edition 23.40.27.06+"]
                },
                "venv_modern_gpus": {
                    "type": "venv_wsl",
                    "purpose": "Modern GPU support",
                    "packages": ["torch", "rocm/cuda"],
                    "requirements": ["WSL", "ROCm/CUDA"]
                }
            }
        
        return plan
    
    def _generate_strategy_warnings(self, overall_strategy: Dict[str, Any], amd_rdna1_rdna2_count: int) -> List[str]:
        """Generate strategy-specific warnings"""
        warnings = []
        
        if amd_rdna1_rdna2_count > 0:
            warnings.append("⚠️ AMD RDNA1/RDNA2 GPUs detected - virtual environments will BREAK DirectML")
            warnings.append("⚠️ System Python required for AMD RX 5000/6000 series AI functionality")
        
        if overall_strategy["complexity"] == "high":
            warnings.append("⚠️ Complex mixed GPU setup - multiple environment types needed")
            warnings.append("⚠️ Environment selection logic required for optimal GPU utilization")
        
        if not self.system_info.wsl_available and overall_strategy["primary_approach"] in ["modern_gpus_venv", "mixed_legacy_amd"]:
            warnings.append("⚠️ WSL not available - consider installing for better GPU environment support")
        
        return warnings
    
    def _generate_strategy_recommendations(self, overall_strategy: Dict[str, Any]) -> List[str]:
        """Generate strategy-specific recommendations"""
        recommendations = []
        
        primary_approach = overall_strategy["primary_approach"]
        
        if primary_approach == "legacy_amd_only":
            recommendations.append("✅ Continue using system Python - optimal for your RX 6000 series")
            recommendations.append("💡 Ensure Adrenalin drivers are up to date")
            recommendations.append("⚠️ Avoid virtual environments for GPU workloads")
        
        elif primary_approach == "modern_gpus_venv":
            recommendations.append("💡 Use virtual environments with WSL for optimal performance")
            recommendations.append("✅ Full ROCm/CUDA support available")
        
        elif primary_approach == "mixed_legacy_amd":
            recommendations.append("🔧 Implement hybrid environment strategy")
            recommendations.append("💡 Use system Python for RDNA1/2, venv for modern GPUs")
            recommendations.append("⚠️ Complex setup - consider environment automation")
        
        return recommendations


def analyze_current_alignment():
    """Analyze current environment setup against new GPU strategy"""
    print("🔍 Enhanced Environment Strategy Alignment Analysis")
    print("=" * 70)
    
    enhanced_setup = EnhancedEnvironmentSetup()
    
    # Display system information
    system_info = enhanced_setup.system_info
    print(f"\\n🖥️ Enhanced System Information:")
    print(f"   OS Type: {system_info.os_type.value}")
    print(f"   Python Version: {system_info.python_version}")
    print(f"   Virtual Environment: {system_info.is_virtual_env}")
    print(f"   WSL Available: {system_info.wsl_available}")
    if system_info.wsl_available:
        print(f"   WSL Distributions: {', '.join(system_info.wsl_distributions)}")
    
    # GPU Strategy Compatibility
    print(f"\\n🎮 GPU Strategy Compatibility:")
    for strategy, compatible in system_info.gpu_strategy_compatibility.items():
        status = "✅" if compatible else "❌"
        print(f"   {status} {strategy.replace('_', ' ').title()}")
    
    # System Recommendations
    if system_info.recommendations:
        print(f"\\n💡 System Recommendations:")
        for rec in system_info.recommendations:
            print(f"   {rec}")
    
    # Test with sample GPU configuration (your 5x RX 6800 setup)
    sample_gpus = [
        {"name": "AMD Radeon RX 6800", "vendor": "amd"},
        {"name": "AMD Radeon RX 6800", "vendor": "amd"},
        {"name": "AMD Radeon RX 6800", "vendor": "amd"},
        {"name": "AMD Radeon RX 6800", "vendor": "amd"},
        {"name": "AMD Radeon RX 6800 XT", "vendor": "amd"}
    ]
    
    analysis = enhanced_setup.analyze_gpu_environment_requirements(sample_gpus)
    
    print(f"\\n🎯 GPU Environment Analysis:")
    overall_strategy = analysis["overall_strategy"]
    print(f"   Primary Approach: {overall_strategy['primary_approach']}")
    print(f"   Complexity: {overall_strategy['complexity']}")
    print(f"   Total GPUs: {overall_strategy['gpu_counts']['total']}")
    print(f"   RDNA1/2 GPUs: {overall_strategy['gpu_counts']['amd_rdna1_rdna2']}")
    
    print(f"\\n📋 Environment Plan:")
    env_plan = analysis["environment_plan"]
    for env_name, env_config in env_plan["environments_needed"].items():
        print(f"   {env_name}:")
        print(f"      Type: {env_config['type']}")
        print(f"      Purpose: {env_config['purpose']}")
    
    if analysis["warnings"]:
        print(f"\\n⚠️ Strategy Warnings:")
        for warning in analysis["warnings"]:
            print(f"   {warning}")
    
    if analysis["recommendations"]:
        print(f"\\n💡 Strategy Recommendations:")
        for rec in analysis["recommendations"]:
            print(f"   {rec}")
    
    return analysis


if __name__ == "__main__":
    analysis = analyze_current_alignment()
    
    print(f"\\n" + "=" * 70)
    print(f"🎉 Enhanced Environment Analysis Complete!")
    print(f"   RDNA4 support: ✅ Implemented")
    print(f"   OS detection: ✅ Enhanced with WSL")
    print(f"   GPU strategy: ✅ Aligned with venv rules")
    print("=" * 70)
