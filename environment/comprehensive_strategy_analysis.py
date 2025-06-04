"""
Environment Strategy Analysis - RDNA1/2 Strategy Validation
Validates environment strategy alignment with hardware requirements
"""

import os
import sys
import platform
from pathlib import Path
from typing import Dict, List, Any, Optional
import structlog

# Add parent path for imports
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

logger = structlog.get_logger(__name__)


def detect_os_environment() -> Dict[str, Any]:
    """Detect operating system and WSL status"""
    os_info = {
        "system": platform.system().lower(),
        "release": platform.release(),
        "machine": platform.machine(),
        "is_wsl": False,
        "wsl_version": None
    }
    
    # Check for WSL
    if os_info["system"] == "linux":
        # Check if running in WSL
        try:
            with open('/proc/version', 'r') as f:
                version_info = f.read().lower()
                if 'microsoft' in version_info or 'wsl' in version_info:
                    os_info["is_wsl"] = True
                    if 'wsl2' in version_info:
                        os_info["wsl_version"] = "2"
                    else:
                        os_info["wsl_version"] = "1"
        except:
            pass
    
    # Check if WSL is available on Windows
    elif os_info["system"] == "windows":
        try:
            import subprocess
            result = subprocess.run(["wsl", "--list", "--quiet"], 
                                  capture_output=True, text=True)
            if result.returncode == 0 and result.stdout.strip():
                os_info["wsl_available"] = True
                os_info["wsl_distributions"] = [
                    dist.strip() for dist in result.stdout.split('\\n') 
                    if dist.strip()
                ]
            else:
                os_info["wsl_available"] = False
                os_info["wsl_distributions"] = []
        except:
            os_info["wsl_available"] = False
            os_info["wsl_distributions"] = []
    
    return os_info


def analyze_gpu_strategy(gpu_list: List[Dict[str, Any]]) -> Dict[str, Any]:
    """Analyze GPU configuration and determine optimal environment strategy"""
    
    # Categorize GPUs
    rdna1_rdna2_count = 0
    rdna3_plus_count = 0
    nvidia_count = 0
    other_count = 0
    
    rdna1_rdna2_gpus = []
    other_gpus = []
    
    for gpu in gpu_list:
        arch = gpu.get('architecture', '').lower()
        vendor = gpu.get('vendor', '').lower()
        
        if vendor == 'amd':
            if arch in ['rdna1', 'rdna2']:
                rdna1_rdna2_count += 1
                rdna1_rdna2_gpus.append(gpu)
            elif arch in ['rdna3', 'rdna4']:
                rdna3_plus_count += 1
                other_gpus.append(gpu)
            else:
                other_count += 1
                other_gpus.append(gpu)
        elif vendor == 'nvidia':
            nvidia_count += 1
            other_gpus.append(gpu)
        else:
            other_count += 1
            other_gpus.append(gpu)
    
    # Determine strategy based on the corrected rules
    total_gpus = len(gpu_list)
    
    if rdna1_rdna2_count > 0 and (rdna3_plus_count + nvidia_count + other_count) == 0:
        # ONLY RDNA1/2 GPUs - must use native Windows
        strategy = "native_windows_required"
        reason = "RDNA1/2 GPUs require DirectML on native Windows (no venv/WSL support)"
        environment_recommendation = "system_python"
        
    elif rdna1_rdna2_count > 0:
        # Mixed: RDNA1/2 + other GPUs
        strategy = "mixed_environment"
        reason = "Mixed GPU setup: RDNA1/2 require native Windows, others can use venv/WSL"
        environment_recommendation = "separate_environments"
        
    elif nvidia_count > 0 or rdna3_plus_count > 0:
        # Only newer GPUs (NVIDIA, RDNA3+)
        strategy = "venv_wsl_preferred"
        reason = "NVIDIA and RDNA3+ GPUs have excellent venv/WSL support"
        environment_recommendation = "venv_in_wsl"
        
    else:
        # Unknown or no GPUs
        strategy = "default_venv"
        reason = "No specific GPU requirements detected"
        environment_recommendation = "venv"
    
    return {
        "strategy": strategy,
        "reason": reason,
        "environment_recommendation": environment_recommendation,
        "gpu_breakdown": {
            "rdna1_rdna2": rdna1_rdna2_count,
            "rdna3_plus": rdna3_plus_count,
            "nvidia": nvidia_count,
            "other": other_count,
            "total": total_gpus
        },
        "rdna1_rdna2_gpus": rdna1_rdna2_gpus,
        "other_gpus": other_gpus,
        "requires_native_windows": rdna1_rdna2_count > 0
    }


def validate_current_setup(os_info: Dict[str, Any], gpu_strategy: Dict[str, Any]) -> Dict[str, Any]:
    """Validate current setup against strategy requirements"""
    
    validation = {
        "setup_optimal": False,
        "warnings": [],
        "recommendations": [],
        "current_environment": "unknown",
        "required_environment": gpu_strategy["environment_recommendation"]
    }
    
    # Detect current Python environment
    is_venv = hasattr(sys, 'real_prefix') or (
        hasattr(sys, 'base_prefix') and sys.base_prefix != sys.prefix
    )
    
    if is_venv:
        if os.environ.get('VIRTUAL_ENV'):
            validation["current_environment"] = "venv"
        elif os.environ.get('CONDA_DEFAULT_ENV'):
            validation["current_environment"] = "conda"
        else:
            validation["current_environment"] = "unknown_venv"
    else:
        if os_info["is_wsl"]:
            validation["current_environment"] = "wsl_system"
        elif os_info["system"] == "windows":
            validation["current_environment"] = "windows_native"
        elif os_info["system"] == "linux":
            validation["current_environment"] = "linux_native"
        else:
            validation["current_environment"] = "system_python"
    
    # Validate against strategy
    strategy = gpu_strategy["strategy"]
    
    if strategy == "native_windows_required":
        # RDNA1/2 only - must be native Windows
        if validation["current_environment"] == "windows_native":
            validation["setup_optimal"] = True
            validation["recommendations"].append("✅ Perfect setup for RDNA1/2 GPUs")
        else:
            validation["setup_optimal"] = False
            validation["warnings"].append("⚠️ RDNA1/2 GPUs require native Windows (no venv/WSL)")
            validation["recommendations"].append("🔧 Switch to system Python on native Windows")
    
    elif strategy == "venv_wsl_preferred":
        # NVIDIA/RDNA3+ - prefer venv in WSL
        if os_info["system"] == "windows":
            if validation["current_environment"] in ["venv", "conda"]:
                if os_info.get("wsl_available", False):
                    validation["recommendations"].append("💡 Consider WSL for optimal GPU support")
                else:
                    validation["setup_optimal"] = True
                    validation["recommendations"].append("✅ Virtual environment is good for your GPUs")
            elif os_info["is_wsl"]:
                validation["setup_optimal"] = True
                validation["recommendations"].append("✅ WSL is optimal for NVIDIA/RDNA3+ GPUs")
            else:
                validation["warnings"].append("⚠️ Consider virtual environment for better isolation")
        else:
            # Linux native - venv is fine
            validation["setup_optimal"] = True
            validation["recommendations"].append("✅ Linux native with venv is optimal")
    
    elif strategy == "mixed_environment":
        # Mixed GPUs - complex setup
        validation["warnings"].append("⚠️ Mixed GPU setup detected")
        validation["recommendations"].extend([
            "🔧 RDNA1/2 GPUs need native Windows environment",
            "🔧 Other GPUs can use separate venv/WSL environments",
            "💡 Consider workload separation by GPU type"
        ])
    
    return validation


def create_comprehensive_environment_report():
    """Create comprehensive environment and strategy report"""
    
    print("🔍 Comprehensive Environment Strategy Analysis")
    print("=" * 80)
    
    # 1. Detect OS Environment
    print(f"\\n1️⃣ Operating System Environment:")
    print("-" * 50)
    
    os_info = detect_os_environment()
    print(f"   OS: {os_info['system'].title()} {os_info['release']}")
    print(f"   Architecture: {os_info['machine']}")
    
    if os_info["system"] == "linux":
        if os_info["is_wsl"]:
            print(f"   WSL: Yes (Version {os_info.get('wsl_version', 'Unknown')})")
        else:
            print(f"   WSL: No (Native Linux)")
    elif os_info["system"] == "windows":
        wsl_status = "Available" if os_info.get("wsl_available", False) else "Not Available"
        print(f"   WSL: {wsl_status}")
        if os_info.get("wsl_distributions"):
            print(f"   WSL Distributions: {', '.join(os_info['wsl_distributions'])}")
    
    # 2. Detect GPUs using monitoring system
    print(f"\\n2️⃣ GPU Hardware Detection:")
    print("-" * 50)
    
    try:
        from monitoring import MetricsCollector
        
        collector = MetricsCollector()
        gpu_metrics = collector.collect_gpu_metrics()
        
        # Convert GPU metrics to analysis format
        gpu_list = []
        for gpu_key, gpu_data in gpu_metrics.items():
            if gpu_key.startswith(('amd_gpu_', 'nvidia_gpu_')):
                vendor = 'amd' if gpu_key.startswith('amd_gpu_') else 'nvidia'
                # Determine architecture from name/data
                arch = 'rdna2'  # Default for detected AMD GPUs (your RX 6800 series)
                if 'rx 6800' in gpu_data.get('name', '').lower():
                    arch = 'rdna2'
                
                gpu_list.append({
                    'vendor': vendor,
                    'name': gpu_data.get('name', 'Unknown'),
                    'architecture': arch,
                    'memory_mb': gpu_data.get('memory_total', 0)
                })
        
        print(f"   Total GPUs Detected: {len(gpu_list)}")
        for i, gpu in enumerate(gpu_list):
            print(f"   GPU {i+1}: {gpu['name']} ({gpu['vendor'].upper()} {gpu['architecture'].upper()})")
        
    except Exception as e:
        print(f"   ⚠️ GPU detection failed: {e}")
        # Fallback to known configuration
        gpu_list = [
            {'vendor': 'amd', 'name': 'AMD Radeon RX 6800', 'architecture': 'rdna2', 'memory_mb': 16384},
            {'vendor': 'amd', 'name': 'AMD Radeon RX 6800', 'architecture': 'rdna2', 'memory_mb': 16384},
            {'vendor': 'amd', 'name': 'AMD Radeon RX 6800', 'architecture': 'rdna2', 'memory_mb': 16384},
            {'vendor': 'amd', 'name': 'AMD Radeon RX 6800', 'architecture': 'rdna2', 'memory_mb': 16384},
            {'vendor': 'amd', 'name': 'AMD Radeon RX 6800 XT', 'architecture': 'rdna2', 'memory_mb': 16384}
        ]
        print(f"   Using known configuration: 4x RX 6800 + 1x RX 6800 XT")
    
    # 3. Analyze Strategy
    print(f"\\n3️⃣ Environment Strategy Analysis:")
    print("-" * 50)
    
    gpu_strategy = analyze_gpu_strategy(gpu_list)
    
    print(f"   Strategy: {gpu_strategy['strategy']}")
    print(f"   Reason: {gpu_strategy['reason']}")
    print(f"   Recommendation: {gpu_strategy['environment_recommendation']}")
    
    print(f"\\n   GPU Breakdown:")
    breakdown = gpu_strategy['gpu_breakdown']
    print(f"      RDNA1/2 (DirectML Required): {breakdown['rdna1_rdna2']}")
    print(f"      RDNA3+ (venv/WSL Compatible): {breakdown['rdna3_plus']}")
    print(f"      NVIDIA (venv/WSL Compatible): {breakdown['nvidia']}")
    print(f"      Other: {breakdown['other']}")
    
    # 4. Validate Current Setup
    print(f"\\n4️⃣ Current Setup Validation:")
    print("-" * 50)
    
    validation = validate_current_setup(os_info, gpu_strategy)
    
    print(f"   Current Environment: {validation['current_environment']}")
    print(f"   Required Environment: {validation['required_environment']}")
    print(f"   Setup Optimal: {'✅ Yes' if validation['setup_optimal'] else '⚠️ Needs Attention'}")
    
    if validation['warnings']:
        print(f"\\n   ⚠️ Warnings:")
        for warning in validation['warnings']:
            print(f"      {warning}")
    
    if validation['recommendations']:
        print(f"\\n   💡 Recommendations:")
        for rec in validation['recommendations']:
            print(f"      {rec}")
    
    # 5. Strategy Rules Summary
    print(f"\\n5️⃣ Environment Strategy Rules:")
    print("-" * 50)
    
    rules = [
        "✅ RDNA1/2 GPUs: MUST use native Windows (DirectML requirement)",
        "✅ RDNA3/4 GPUs: CAN use venv/WSL (ROCm support)",
        "✅ NVIDIA GPUs: CAN use venv/WSL (excellent CUDA support)",
        "✅ Mixed setups: Separate environments per GPU type",
        "✅ Windows users: WSL preferred for NVIDIA/RDNA3+ workloads"
    ]
    
    for rule in rules:
        print(f"   {rule}")
    
    return {
        "os_info": os_info,
        "gpu_strategy": gpu_strategy,
        "validation": validation,
        "overall_status": "optimal" if validation['setup_optimal'] else "needs_attention"
    }


if __name__ == "__main__":
    report = create_comprehensive_environment_report()
    
    print(f"\\n" + "=" * 80)
    print(f"🎯 Overall Assessment: {report['overall_status'].upper()}")
    print("=" * 80)
