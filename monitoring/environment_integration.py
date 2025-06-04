"""
Environment-Aware Monitoring Integration
Aligns monitoring with environment management and virtual environment considerations
"""

import os
import sys
import subprocess
from typing import Dict, Any, Optional, List
from dataclasses import dataclass
from pathlib import Path
import structlog

logger = structlog.get_logger(__name__)


@dataclass
class EnvironmentStatus:
    """Environment status information"""
    is_virtual_env: bool
    python_executable: str
    environment_type: str  # "system", "venv", "conda", "pipenv"
    environment_path: Optional[str]
    directml_compatible: bool
    cuda_compatible: bool
    monitoring_warnings: List[str]


class EnvironmentMonitoringIntegrator:
    """
    Integrates monitoring with environment management
    Handles virtual environment compatibility for GPU monitoring
    """
    
    def __init__(self):
        """Initialize environment monitoring integrator"""
        self.environment_status = self._detect_environment()
        
    def _detect_environment(self) -> EnvironmentStatus:
        """Detect current Python environment status"""
        warnings = []
        
        # Check if in virtual environment
        is_venv = hasattr(sys, 'real_prefix') or (
            hasattr(sys, 'base_prefix') and sys.base_prefix != sys.prefix
        )
        
        # Determine environment type
        env_type = "system"
        env_path = None
        
        if is_venv:
            if os.environ.get('VIRTUAL_ENV'):
                env_type = "venv"
                env_path = os.environ['VIRTUAL_ENV']
            elif os.environ.get('CONDA_DEFAULT_ENV'):
                env_type = "conda"
                env_path = os.environ.get('CONDA_PREFIX')
            elif os.environ.get('PIPENV_ACTIVE'):
                env_type = "pipenv"
                env_path = os.environ.get('VIRTUAL_ENV')
            else:
                env_type = "unknown_venv"
        
        # Check DirectML compatibility
        directml_compatible = True
        if is_venv:
            directml_compatible = False
            warnings.append(
                "DirectML may not work in virtual environments - AMD GPU AI features may be limited"
            )
        
        # CUDA is generally compatible with virtual environments
        cuda_compatible = True
        
        python_exe = sys.executable
        
        logger.info(
            "Environment detected",
            is_virtual=is_venv,
            type=env_type,
            path=env_path,
            directml_compatible=directml_compatible
        )
        
        return EnvironmentStatus(
            is_virtual_env=is_venv,
            python_executable=python_exe,
            environment_type=env_type,
            environment_path=env_path,
            directml_compatible=directml_compatible,
            cuda_compatible=cuda_compatible,
            monitoring_warnings=warnings
        )
    
    def get_gpu_monitoring_recommendations(self) -> Dict[str, Any]:
        """Get recommendations for GPU monitoring based on environment"""
        recommendations = {
            "environment_status": self.environment_status,
            "monitoring_adjustments": [],
            "setup_recommendations": [],
            "limitations": []
        }
        
        if self.environment_status.is_virtual_env:
            # Virtual environment detected
            if not self.environment_status.directml_compatible:
                recommendations["limitations"].append({
                    "type": "DirectML incompatibility",
                    "description": "AMD GPU AI features may not work in virtual environments",
                    "affected_hardware": "AMD RX 6800 series",
                    "workaround": "Use system Python for AMD GPU workloads"
                })
                
                recommendations["setup_recommendations"].append({
                    "priority": "high",
                    "action": "Consider system Python installation for AMD GPU support",
                    "details": "DirectML requires system-level driver integration"
                })
            
            recommendations["monitoring_adjustments"].append({
                "component": "GPU metrics",
                "adjustment": "Basic detection only - AI capabilities may be limited",
                "reason": "Virtual environment limitations"
            })
            
        else:
            # System Python - full compatibility
            recommendations["setup_recommendations"].append({
                "priority": "info", 
                "action": "Full GPU monitoring available",
                "details": "System Python supports all GPU backends"
            })
        
        return recommendations
    
    def validate_monitoring_environment(self) -> Dict[str, Any]:
        """Validate that monitoring will work correctly in current environment"""
        validation = {
            "environment_compatible": True,
            "gpu_monitoring_status": "full",
            "warnings": [],
            "errors": [],
            "recommendations": []
        }
        
        # Check basic monitoring requirements
        try:
            import psutil
            validation["psutil_available"] = True
        except ImportError:
            validation["psutil_available"] = False
            validation["errors"].append("psutil not installed - system monitoring will fail")
            validation["environment_compatible"] = False
        
        # Check GPU monitoring capabilities
        if self.environment_status.is_virtual_env:
            if not self.environment_status.directml_compatible:
                validation["gpu_monitoring_status"] = "limited"
                validation["warnings"].append(
                    "AMD GPU AI monitoring may be limited in virtual environment"
                )
        
        # Check for required packages
        missing_packages = []
        optional_packages = ["torch", "pynvml"]
        
        for package in optional_packages:
            try:
                __import__(package)
            except ImportError:
                missing_packages.append(package)
        
        if missing_packages:
            validation["warnings"].append(
                f"Optional GPU packages not available: {', '.join(missing_packages)}"
            )
        
        return validation
    
    def get_environment_specific_gpu_metrics(self) -> Dict[str, Any]:
        """Get GPU metrics appropriate for current environment"""
        from ..monitoring.metrics_collector import MetricsCollector
        
        collector = MetricsCollector()
        gpu_metrics = collector.collect_gpu_metrics()
        
        # Add environment context to GPU metrics
        enhanced_metrics = {
            "environment_info": {
                "type": self.environment_status.environment_type,
                "is_virtual": self.environment_status.is_virtual_env,
                "directml_compatible": self.environment_status.directml_compatible,
                "monitoring_mode": "full" if not self.environment_status.is_virtual_env else "basic"
            },
            "gpu_devices": gpu_metrics,
            "monitoring_warnings": self.environment_status.monitoring_warnings
        }
        
        # Add specific warnings for detected AMD GPUs in virtual environments
        if self.environment_status.is_virtual_env:
            amd_gpu_count = len([k for k in gpu_metrics.keys() if k.startswith('amd_gpu_')])
            if amd_gpu_count > 0:
                enhanced_metrics["amd_venv_warning"] = {
                    "detected_amd_gpus": amd_gpu_count,
                    "virtual_env_impact": "DirectML AI features may not work",
                    "recommendation": "Use system Python for full AMD GPU support"
                }
        
        return enhanced_metrics


def create_monitoring_environment_report() -> str:
    """Create a comprehensive report on monitoring environment compatibility"""
    integrator = EnvironmentMonitoringIntegrator()
    recommendations = integrator.get_gpu_monitoring_recommendations()
    validation = integrator.validate_monitoring_environment()
    
    report = []
    report.append("🔍 Node Manager Monitoring Environment Report")
    report.append("=" * 60)
    
    # Environment Status
    env_status = recommendations["environment_status"]
    report.append(f"\n📊 Environment Status:")
    report.append(f"   Type: {env_status.environment_type}")
    report.append(f"   Virtual Environment: {'Yes' if env_status.is_virtual_env else 'No'}")
    report.append(f"   Python: {env_status.python_executable}")
    if env_status.environment_path:
        report.append(f"   Path: {env_status.environment_path}")
    
    # Compatibility
    report.append(f"\n🎮 GPU Compatibility:")
    report.append(f"   DirectML (AMD): {'✅ Compatible' if env_status.directml_compatible else '⚠️  Limited'}")
    report.append(f"   CUDA (NVIDIA): {'✅ Compatible' if env_status.cuda_compatible else '❌ Not Compatible'}")
    
    # Validation Results
    report.append(f"\n✅ Monitoring Validation:")
    report.append(f"   Environment Compatible: {'Yes' if validation['environment_compatible'] else 'No'}")
    report.append(f"   GPU Monitoring Status: {validation['gpu_monitoring_status']}")
    
    # Warnings
    if validation["warnings"]:
        report.append(f"\n⚠️  Warnings:")
        for warning in validation["warnings"]:
            report.append(f"   • {warning}")
    
    # Errors
    if validation["errors"]:
        report.append(f"\n❌ Errors:")
        for error in validation["errors"]:
            report.append(f"   • {error}")
    
    # Recommendations
    if recommendations["setup_recommendations"]:
        report.append(f"\n💡 Recommendations:")
        for rec in recommendations["setup_recommendations"]:
            priority_icon = "🔴" if rec["priority"] == "high" else "🟡" if rec["priority"] == "medium" else "ℹ️"
            report.append(f"   {priority_icon} {rec['action']}")
            report.append(f"      {rec['details']}")
    
    report.append(f"\n" + "=" * 60)
    
    return "\\n".join(report)


# Integration function for existing monitoring
def enhance_monitoring_with_environment_awareness():
    """Enhance existing monitoring with environment awareness"""
    integrator = EnvironmentMonitoringIntegrator()
    
    # Log environment status
    logger.info("Monitoring environment integration initialized")
    
    if integrator.environment_status.monitoring_warnings:
        for warning in integrator.environment_status.monitoring_warnings:
            logger.warning("Environment monitoring warning", message=warning)
    
    return integrator
