"""
Unified GPU Strategy Analysis Module
Consolidates all GPU-based environment strategy logic into a single module.
Replaces simple_strategy_analysis.py, comprehensive_strategy_analysis.py, 
strategy_analyzer.py, and parts of enhanced_environment_setup.py.
"""

import platform
import subprocess
import os
import sys
from enum import Enum, auto
from typing import List, Dict, Any, Optional
from dataclasses import dataclass
import structlog

logger = structlog.get_logger(__name__)


class OSType(Enum):
    """Operating system environment types"""
    WINDOWS_NATIVE = auto()
    WINDOWS_WSL = auto()
    LINUX_NATIVE = auto()
    MACOS = auto()
    UNKNOWN = auto()


class GPUStrategyType(Enum):
    """Environment strategies based on GPU analysis"""
    SYSTEM_PYTHON = auto()        # RDNA1/RDNA2 on Windows
    VENV_WSL = auto()             # RDNA3+/NVIDIA on WSL or Linux
    VENV_CUDA = auto()            # NVIDIA-specific CUDA venv
    MIXED = auto()                # Mixed GPU types, need separate envs
    CPU_FALLBACK = auto()         # No compatible GPUs detected


@dataclass
class GPUStrategyResult:
    """Result of GPU strategy analysis"""
    os_type: OSType
    wsl_available: bool
    strategy: GPUStrategyType
    details: Dict[str, Any]
    warnings: List[str]
    recommendations: List[str]


def detect_os_type() -> OSType:
    """
    Return one of OSType.* based on platform + WSL detection.
    Single source of truth for OS detection across the codebase.
    """
    system = platform.system().lower()
    
    if system == "windows":
        # Check if we're actually running inside WSL
        if os.path.exists("/proc/version"):
            try:
                with open("/proc/version", "r") as f:
                    ver = f.read().lower()
                    if "microsoft" in ver or "wsl" in ver:
                        return OSType.WINDOWS_WSL
            except Exception:
                pass
        return OSType.WINDOWS_NATIVE
    
    elif system == "linux":
        # Additional check to see if we're in WSL from Linux side
        try:
            with open("/proc/version", "r") as f:
                ver = f.read().lower()
                if "microsoft" in ver or "wsl" in ver:
                    return OSType.WINDOWS_WSL
        except Exception:
            pass
        return OSType.LINUX_NATIVE
    
    elif system == "darwin":
        return OSType.MACOS
    
    return OSType.UNKNOWN


def detect_wsl_available() -> bool:
    """
    Return True if WSL is installed and available (but not necessarily running inside).
    Single source of truth for WSL availability detection.
    """
    if platform.system().lower() != "windows":
        return False

    try:
        # Check WSL status
        result = subprocess.run(
            ["wsl", "--status"], 
            capture_output=True, 
            text=True, 
            timeout=5
        )
        return result.returncode == 0
    except Exception:
        pass

    try:
        # Fallback: check if any distributions are installed
        result = subprocess.run(
            ["wsl", "--list", "--quiet"],
            capture_output=True,
            text=True,
            timeout=5
        )
        if result.returncode == 0 and result.stdout.strip():
            return True
    except Exception:
        pass

    return False


def analyze_gpu_list(gpu_list: List[Any]) -> GPUStrategyResult:
    """
    Given a list of GPUInfo objects, decide on an overall strategy.
    This is the main entry point that replaces all the scattered strategy logic.
    
    Args:
        gpu_list: List of GPUInfo objects from gpu_detector.py
        
    Returns:
        GPUStrategyResult with strategy decision and details
    """
    os_type = detect_os_type()
    wsl_ok = detect_wsl_available()
    
    # Initialize counters
    rdna1_2 = 0
    rdna3_4 = 0
    nvidia = 0
    other = 0
    
    # Categorize GPUs
    gpu_details = []
    for gpu in gpu_list:
        gpu_info = {
            "device_id": getattr(gpu, 'device_id', 'unknown'),
            "name": getattr(gpu, 'name', 'unknown'),
            "vendor": getattr(gpu, 'vendor', 'unknown'),
            "architecture": getattr(gpu, 'architecture', 'unknown').lower()
        }
        gpu_details.append(gpu_info)
          # Use string-based detection to avoid circular imports
        vendor = str(getattr(gpu, 'vendor', '')).lower()
        arch = str(getattr(gpu, 'architecture', '')).lower()
        
        # Handle enum values properly
        if hasattr(gpu, 'vendor') and hasattr(gpu.vendor, 'value'):
            vendor = gpu.vendor.value.lower()
        
        if 'amd' in vendor:
            if any(x in arch for x in ['rdna1', 'rdna2']):
                rdna1_2 += 1
            elif any(x in arch for x in ['rdna3', 'rdna4']):
                rdna3_4 += 1
            else:
                other += 1
        elif 'nvidia' in vendor:
            nvidia += 1
        else:
            other += 1

    total = len(gpu_list)
    details = {
        "rdna1_2_count": rdna1_2,
        "rdna3_4_count": rdna3_4,
        "nvidia_count": nvidia,
        "other_count": other,
        "total_gpus": total,
        "gpu_details": gpu_details,
        "os_detected": os_type.name,
        "wsl_detected": wsl_ok
    }

    warnings: List[str] = []
    recommendations: List[str] = []

    # Determine primary strategy based on GPU composition
    if total == 0:
        strategy = GPUStrategyType.CPU_FALLBACK
        warnings.append("⚠️ No GPUs detected - falling back to CPU-only environment")
        recommendations.append("Consider installing GPU drivers or checking hardware")
        
    elif rdna1_2 > 0 and (rdna3_4 + nvidia + other) == 0:
        # Only RDNA1/2 → system Python on Windows
        strategy = GPUStrategyType.SYSTEM_PYTHON
        recommendations.append("Using system Python for RDNA1/RDNA2 compatibility")
        
        if os_type != OSType.WINDOWS_NATIVE:
            warnings.append(
                "⚠️ RDNA1/RDNA2 GPUs require system Python on native Windows. "
                "DirectML does not work reliably in virtual environments or WSL."
            )
            recommendations.append("Switch to Windows native environment for optimal performance")
            
    elif rdna1_2 > 0 and (rdna3_4 + nvidia + other) > 0:
        # Mixed RDNA1/2 + others → requires separate environments
        strategy = GPUStrategyType.MIXED
        warnings.append(
            "⚠️ Mixed GPU configuration detected. RDNA1/RDNA2 requires system Python, "
            "while newer GPUs work better in virtual environments."
        )
        recommendations.extend([
            "Consider using system Python for RDNA1/RDNA2 workloads",
            "Use virtual environments or WSL for RDNA3+/NVIDIA workloads",
            "May require separate model serving configurations"
        ])
        
    elif nvidia > 0 or rdna3_4 > 0:
        # Only newer GPUs → virtual environment preferred
        strategy = GPUStrategyType.VENV_WSL
        
        if nvidia > 0:
            recommendations.append("NVIDIA GPUs detected - CUDA virtual environment recommended")
        if rdna3_4 > 0:
            recommendations.append("RDNA3/RDNA4 GPUs detected - ROCm virtual environment recommended")
            
        if os_type == OSType.WINDOWS_NATIVE and not wsl_ok:
            warnings.append(
                "ℹ️ No WSL detected. Virtual environments on native Windows are possible "
                "for NVIDIA/RDNA3+, but Linux compatibility may be limited."
            )
            recommendations.append("Consider installing WSL2 for better Linux ecosystem support")
            
    else:
        # Unknown/other GPUs → default to virtual environment
        strategy = GPUStrategyType.VENV_WSL
        warnings.append("Unknown GPU types detected - using default virtual environment strategy")

    # Additional OS-specific recommendations
    if os_type == OSType.WINDOWS_WSL:
        recommendations.append("Running in WSL - good for ROCm and CUDA development")
    elif os_type == OSType.LINUX_NATIVE:
        recommendations.append("Native Linux detected - optimal for ROCm and CUDA")
    elif os_type == OSType.MACOS:
        warnings.append("⚠️ macOS detected - limited GPU compute options available")

    return GPUStrategyResult(
        os_type=os_type,
        wsl_available=wsl_ok,
        strategy=strategy,
        details=details,
        warnings=warnings,
        recommendations=recommendations
    )


def get_strategy_requirements(strategy: GPUStrategyType, gpu_list: List[Any]) -> Dict[str, Any]:
    """
    Convert a strategy decision into specific environment requirements.
    This helps bridge the gap between strategy and implementation.
    """
    requirements = {
        "python_env_type": "venv",  # default
        "frameworks": [],
        "packages": [],
        "os_requirements": [],
        "conflicts": [],
        "validation_scripts": []
    }
    
    if strategy == GPUStrategyType.SYSTEM_PYTHON:
        requirements.update({
            "python_env_type": "native",
            "frameworks": ["directml"],
            "packages": [
                "torch-directml>=0.2.0",
                "onnxruntime-directml>=1.16.0",
                "numpy>=1.21.0"
            ],
            "os_requirements": ["windows_native"],
            "conflicts": ["venv", "wsl", "conda"],
            "validation_scripts": ["validate_directml.py"]
        })
        
    elif strategy == GPUStrategyType.VENV_WSL:
        # Determine specific framework based on GPU types
        frameworks = []
        packages = []
        validation_scripts = []
        
        has_nvidia = any(
            'nvidia' in str(getattr(gpu, 'vendor', '')).lower() 
            for gpu in gpu_list
        )
        has_amd_modern = any(
            'amd' in str(getattr(gpu, 'vendor', '')).lower() and
            any(arch in str(getattr(gpu, 'architecture', '')).lower() 
                for arch in ['rdna3', 'rdna4'])
            for gpu in gpu_list
        )
        
        if has_nvidia:
            frameworks.append("pytorch_cuda")
            packages.extend([
                "torch>=2.1.0+cu118",
                "torchvision>=0.16.0+cu118",
                "torchaudio>=2.1.0+cu118"
            ])
            validation_scripts.append("validate_nvidia.py")
            
        if has_amd_modern:
            frameworks.append("pytorch_rocm")
            packages.extend([
                "torch>=2.1.0+rocm6.4.1",
                "torchvision>=0.16.0+rocm6.4.1"
            ])
            validation_scripts.append("validate_rocm.py")
            
        if not frameworks:  # Fallback
            frameworks.append("pytorch")
            packages.append("torch>=2.1.0")
            validation_scripts.append("validate_cpu.py")
        
        requirements.update({
            "python_env_type": "venv",
            "frameworks": frameworks,
            "packages": packages,
            "os_requirements": ["linux_native", "windows_wsl"],
            "conflicts": [],
            "validation_scripts": validation_scripts
        })
        
    elif strategy == GPUStrategyType.MIXED:
        # Mixed strategy - return requirements for creating multiple environments
        requirements.update({
            "python_env_type": "mixed",
            "frameworks": ["directml", "pytorch_cuda", "pytorch_rocm"],
            "packages": [],  # Will be determined per-environment
            "os_requirements": ["windows_native", "linux_native", "windows_wsl"],
            "conflicts": [],
            "validation_scripts": ["validate_directml.py", "validate_nvidia.py", "validate_rocm.py"],
            "note": "Mixed strategy requires separate environments per GPU type"
        })
        
    elif strategy == GPUStrategyType.CPU_FALLBACK:
        requirements.update({
            "python_env_type": "venv",
            "frameworks": ["pytorch"],
            "packages": ["torch>=2.1.0", "torchvision>=0.16.0"],
            "os_requirements": [],
            "conflicts": [],
            "validation_scripts": ["validate_cpu.py"]
        })
    
    return requirements


# Convenience functions for backward compatibility
def detect_wsl() -> Dict[str, Any]:
    """Legacy function for backward compatibility"""
    wsl_available = detect_wsl_available()
    os_type = detect_os_type()
    
    return {
        "in_wsl": os_type == OSType.WINDOWS_WSL,
        "available": wsl_available,
        "version": "2.0" if wsl_available else None
    }


def analyze_amd_gpu_strategy(gpu_name: str) -> Dict[str, Any]:
    """Legacy function for backward compatibility - deprecated"""
    logger.warning(
        "analyze_amd_gpu_strategy is deprecated. Use analyze_gpu_list instead.",
        gpu_name=gpu_name
    )
    
    # Basic analysis for single GPU
    name_lower = gpu_name.lower()
    
    if any(series in name_lower for series in ["6800", "6900", "6700", "6600", "5700", "5600", "5500"]):
        return {
            "gpu_name": gpu_name,
            "architecture": "RDNA2" if "6" in name_lower else "RDNA1",
            "environment_recommendation": "system_python_windows",
            "special_requirements": [
                "DirectML breaks in virtual environments",
                "System Python required for AI functionality"
            ]
        }
    elif any(series in name_lower for series in ["7900", "7800", "7700", "7600"]):
        return {
            "gpu_name": gpu_name,
            "architecture": "RDNA3",
            "environment_recommendation": "venv_wsl_preferred",
            "special_requirements": [
                "ROCm 6.4.1 support on Linux",
                "Can use virtual environments"
            ]
        }
    
    return {
        "gpu_name": gpu_name,
        "architecture": "unknown",
        "environment_recommendation": "venv_wsl_preferred",
        "special_requirements": []
    }
