"""
Environment Validator
Tests and validates GPU environments after setup
Ensures frameworks work correctly with detected hardware
"""

import asyncio
import subprocess
import tempfile
import json
import time
from typing import Dict, List, Optional, Any, Tuple
from pathlib import Path
from dataclasses import dataclass
import structlog

from .gpu_detector import GPUInfo, GPUVendor

logger = structlog.get_logger(__name__)


@dataclass 
class ValidationResult:
    """Result of environment validation"""
    test_name: str
    success: bool
    duration_ms: float
    output: str
    error: Optional[str]
    details: Dict[str, Any]


@dataclass
class EnvironmentValidationResult:
    """Complete environment validation results"""
    environment_name: str
    overall_success: bool
    python_executable: str
    gpu_devices: List[str]
    test_results: List[ValidationResult]
    performance_metrics: Dict[str, float]
    recommendations: List[str]


class EnvironmentValidator:
    """
    Validates GPU environments and AI frameworks
    Runs comprehensive tests to ensure everything works
    """
    
    def __init__(self):
        """Initialize environment validator"""
        self.validation_scripts_path = Path(__file__).parent / "validation_scripts"
        self.validation_scripts_path.mkdir(exist_ok=True)
        
        # Create validation scripts
        self._create_validation_scripts()
        
        logger.info("EnvironmentValidator initialized")
    
    def _create_validation_scripts(self):
        """Create validation test scripts"""
        self._create_nvidia_validation_script()
        self._create_directml_validation_script() 
        self._create_rocm_validation_script()
    
    def _create_nvidia_validation_script(self):
        """Create NVIDIA CUDA validation script"""
        script_content = '''
import torch
import time
import json
import sys
import traceback

def test_cuda_availability():
    """Test CUDA availability"""
    try:
        available = torch.cuda.is_available()
        device_count = torch.cuda.device_count() if available else 0
        return {
            "success": available,
            "device_count": device_count,
            "cuda_version": torch.version.cuda if available else None,
            "devices": [torch.cuda.get_device_name(i) for i in range(device_count)] if available else []
        }
    except Exception as e:
        return {"success": False, "error": str(e)}

def test_tensor_operations():
    """Test basic tensor operations on GPU"""
    try:
        if not torch.cuda.is_available():
            return {"success": False, "error": "CUDA not available"}
        
        device = torch.device("cuda:0")
        
        # Create tensors
        a = torch.randn(1000, 1000, device=device)
        b = torch.randn(1000, 1000, device=device)
        
        # Time matrix multiplication
        start_time = time.time()
        c = torch.matmul(a, b)
        torch.cuda.synchronize()
        duration = time.time() - start_time
        
        return {
            "success": True,
            "duration_ms": duration * 1000,
            "result_shape": list(c.shape),
            "memory_allocated": torch.cuda.memory_allocated() / (1024**2)  # MB
        }
    except Exception as e:
        return {"success": False, "error": str(e)}

def test_model_loading():
    """Test loading a simple model"""
    try:
        if not torch.cuda.is_available():
            return {"success": False, "error": "CUDA not available"}
        
        device = torch.device("cuda:0")
        
        # Create simple model
        model = torch.nn.Sequential(
            torch.nn.Linear(784, 128),
            torch.nn.ReLU(),
            torch.nn.Linear(128, 10)
        ).to(device)
        
        # Test forward pass
        input_tensor = torch.randn(32, 784, device=device)
        output = model(input_tensor)
        
        return {
            "success": True,
            "model_parameters": sum(p.numel() for p in model.parameters()),
            "output_shape": list(output.shape),
            "memory_used": torch.cuda.memory_allocated() / (1024**2)
        }
    except Exception as e:
        return {"success": False, "error": str(e)}

def run_all_tests():
    """Run all validation tests"""
    results = {}
    
    try:
        results["cuda_availability"] = test_cuda_availability()
        results["tensor_operations"] = test_tensor_operations() 
        results["model_loading"] = test_model_loading()
        
        # Overall success
        results["overall_success"] = all(
            test_result.get("success", False) 
            for test_result in results.values()
        )
        
    except Exception as e:
        results["overall_success"] = False
        results["error"] = str(e)
        results["traceback"] = traceback.format_exc()
    
    return results

if __name__ == "__main__":
    results = run_all_tests()
    print(json.dumps(results, indent=2))
'''
        
        script_path = self.validation_scripts_path / "validate_nvidia.py"
        with open(script_path, 'w') as f:
            f.write(script_content)
    
    def _create_directml_validation_script(self):
        """Create DirectML validation script"""
        script_content = '''
import torch
import time
import json
import sys
import traceback

def test_directml_availability():
    """Test DirectML availability"""
    try:
        # Check if torch-directml is available
        import torch_directml
        
        device_count = torch_directml.device_count()
        devices = []
        
        for i in range(device_count):
            device_name = torch_directml.device_name(i)
            devices.append(device_name)
        
        return {
            "success": device_count > 0,
            "device_count": device_count,
            "devices": devices,
            "directml_version": getattr(torch_directml, '__version__', 'unknown')
        }
    except ImportError:
        return {"success": False, "error": "torch-directml not installed"}
    except Exception as e:
        return {"success": False, "error": str(e)}

def test_tensor_operations():
    """Test tensor operations with DirectML"""
    try:
        import torch_directml
        
        if torch_directml.device_count() == 0:
            return {"success": False, "error": "No DirectML devices"}
        
        device = torch_directml.device()
        
        # Create tensors
        a = torch.randn(1000, 1000, device=device)
        b = torch.randn(1000, 1000, device=device)
        
        # Time matrix multiplication
        start_time = time.time()
        c = torch.matmul(a, b)
        duration = time.time() - start_time
        
        return {
            "success": True,
            "duration_ms": duration * 1000,
            "result_shape": list(c.shape),
            "device_type": str(device)
        }
    except Exception as e:
        return {"success": False, "error": str(e)}

def test_model_loading():
    """Test loading model with DirectML"""
    try:
        import torch_directml
        
        if torch_directml.device_count() == 0:
            return {"success": False, "error": "No DirectML devices"}
        
        device = torch_directml.device()
        
        # Create simple model
        model = torch.nn.Sequential(
            torch.nn.Linear(784, 128),
            torch.nn.ReLU(),
            torch.nn.Linear(128, 10)
        ).to(device)
        
        # Test forward pass
        input_tensor = torch.randn(32, 784, device=device)
        output = model(input_tensor)
        
        return {
            "success": True,
            "model_parameters": sum(p.numel() for p in model.parameters()),
            "output_shape": list(output.shape)
        }
    except Exception as e:
        return {"success": False, "error": str(e)}

def run_all_tests():
    """Run all DirectML validation tests"""
    results = {}
    
    try:
        results["directml_availability"] = test_directml_availability()
        results["tensor_operations"] = test_tensor_operations()
        results["model_loading"] = test_model_loading()
        
        # Overall success
        results["overall_success"] = all(
            test_result.get("success", False)
            for test_result in results.values()
        )
        
    except Exception as e:
        results["overall_success"] = False
        results["error"] = str(e)
        results["traceback"] = traceback.format_exc()
    
    return results

if __name__ == "__main__":
    results = run_all_tests()
    print(json.dumps(results, indent=2))
'''
        
        script_path = self.validation_scripts_path / "validate_directml.py"
        with open(script_path, 'w') as f:
            f.write(script_content)
    
    def _create_rocm_validation_script(self):
        """Create ROCm validation script"""
        script_content = '''
import torch
import time
import json
import sys
import traceback

def test_rocm_availability():
    """Test ROCm availability"""
    try:
        available = torch.cuda.is_available()  # ROCm uses same API
        device_count = torch.cuda.device_count() if available else 0
        
        devices = []
        if available:
            for i in range(device_count):
                device_name = torch.cuda.get_device_name(i)
                devices.append(device_name)
        
        return {
            "success": available,
            "device_count": device_count,
            "devices": devices,
            "rocm_version": torch.version.hip if hasattr(torch.version, 'hip') else None
        }
    except Exception as e:
        return {"success": False, "error": str(e)}

def test_tensor_operations():
    """Test tensor operations with ROCm"""
    try:
        if not torch.cuda.is_available():
            return {"success": False, "error": "ROCm not available"}
        
        device = torch.device("cuda:0")  # ROCm uses cuda namespace
        
        # Create tensors
        a = torch.randn(1000, 1000, device=device)
        b = torch.randn(1000, 1000, device=device)
        
        # Time matrix multiplication
        start_time = time.time()
        c = torch.matmul(a, b)
        torch.cuda.synchronize()
        duration = time.time() - start_time
        
        return {
            "success": True,
            "duration_ms": duration * 1000,
            "result_shape": list(c.shape),
            "memory_allocated": torch.cuda.memory_allocated() / (1024**2)
        }
    except Exception as e:
        return {"success": False, "error": str(e)}

def test_model_loading():
    """Test loading model with ROCm"""
    try:
        if not torch.cuda.is_available():
            return {"success": False, "error": "ROCm not available"}
        
        device = torch.device("cuda:0")
        
        # Create simple model
        model = torch.nn.Sequential(
            torch.nn.Linear(784, 128),
            torch.nn.ReLU(),
            torch.nn.Linear(128, 10)
        ).to(device)
        
        # Test forward pass
        input_tensor = torch.randn(32, 784, device=device)
        output = model(input_tensor)
        
        return {
            "success": True,
            "model_parameters": sum(p.numel() for p in model.parameters()),
            "output_shape": list(output.shape),
            "memory_used": torch.cuda.memory_allocated() / (1024**2)
        }
    except Exception as e:
        return {"success": False, "error": str(e)}

def run_all_tests():
    """Run all ROCm validation tests"""
    results = {}
    
    try:
        results["rocm_availability"] = test_rocm_availability()
        results["tensor_operations"] = test_tensor_operations()
        results["model_loading"] = test_model_loading()
        
        # Overall success
        results["overall_success"] = all(
            test_result.get("success", False)
            for test_result in results.values()
        )
        
    except Exception as e:
        results["overall_success"] = False
        results["error"] = str(e)
        results["traceback"] = traceback.format_exc()
    
    return results

if __name__ == "__main__":
    results = run_all_tests()
    print(json.dumps(results, indent=2))
'''
        
        script_path = self.validation_scripts_path / "validate_rocm.py"
        with open(script_path, 'w') as f:
            f.write(script_content)
    
    async def validate_environment(self, env_name: str, python_executable: str, 
                                 framework: str, gpu_devices: List[str]) -> EnvironmentValidationResult:
        """Validate a specific environment"""
        logger.info(f"Validating environment: {env_name}")
        
        start_time = time.time()
        test_results = []
        
        # Determine validation script
        if framework in ["pytorch", "pytorch_cuda"]:
            script_name = "validate_nvidia.py"
        elif framework == "directml":
            script_name = "validate_directml.py"
        elif framework in ["rocm", "pytorch_rocm"]:
            script_name = "validate_rocm.py"
        else:
            raise ValueError(f"Unsupported framework for validation: {framework}")
        
        script_path = self.validation_scripts_path / script_name
        
        # Run validation script
        try:
            process = await asyncio.create_subprocess_exec(
                python_executable, str(script_path),
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            
            stdout, stderr = await process.communicate()
            
            if process.returncode == 0:
                # Parse results
                results_data = json.loads(stdout.decode())
                
                # Convert to ValidationResult objects
                for test_name, test_data in results_data.items():
                    if test_name in ["overall_success", "error", "traceback"]:
                        continue
                    
                    test_result = ValidationResult(
                        test_name=test_name,
                        success=test_data.get("success", False),
                        duration_ms=test_data.get("duration_ms", 0),
                        output=json.dumps(test_data, indent=2),
                        error=test_data.get("error"),
                        details=test_data
                    )
                    test_results.append(test_result)
                
                overall_success = results_data.get("overall_success", False)
                
            else:
                # Validation script failed
                error_output = stderr.decode()
                test_result = ValidationResult(
                    test_name="script_execution",
                    success=False,
                    duration_ms=0,
                    output=stdout.decode(),
                    error=error_output,
                    details={"returncode": process.returncode}
                )
                test_results.append(test_result)
                overall_success = False
                
        except Exception as e:
            # Exception running validation
            test_result = ValidationResult(
                test_name="execution_error",
                success=False,
                duration_ms=0,
                output="",
                error=str(e),
                details={}
            )
            test_results.append(test_result)
            overall_success = False
        
        # Performance metrics
        total_duration = time.time() - start_time
        performance_metrics = {
            "total_validation_time_ms": total_duration * 1000,
            "successful_tests": sum(1 for test in test_results if test.success),
            "total_tests": len(test_results)
        }
        
        # Generate recommendations
        recommendations = self._generate_recommendations(test_results, framework)
        
        result = EnvironmentValidationResult(
            environment_name=env_name,
            overall_success=overall_success,
            python_executable=python_executable,
            gpu_devices=gpu_devices,
            test_results=test_results,
            performance_metrics=performance_metrics,
            recommendations=recommendations
        )
        
        logger.info(f"Environment validation completed", 
                   env_name=env_name, 
                   success=overall_success,
                   tests_passed=performance_metrics["successful_tests"])
        
        return result
    
    def _generate_recommendations(self, test_results: List[ValidationResult], framework: str) -> List[str]:
        """Generate recommendations based on test results"""
        recommendations = []
        
        # Check for common issues
        failed_tests = [test for test in test_results if not test.success]
        
        if not failed_tests:
            recommendations.append("✅ All validation tests passed successfully")
            return recommendations
        
        for test in failed_tests:
            if test.test_name == "cuda_availability" or test.test_name == "rocm_availability":
                if "CUDA not available" in (test.error or ""):
                    recommendations.append("❌ GPU drivers may not be installed correctly")
                    recommendations.append("💡 Install latest NVIDIA drivers or ROCm")
                    
            elif test.test_name == "directml_availability":
                if "torch-directml not installed" in (test.error or ""):
                    recommendations.append("❌ DirectML packages not installed")
                    recommendations.append("💡 Install torch-directml and dependencies")
                    
            elif test.test_name == "tensor_operations":
                if "memory" in (test.error or "").lower():
                    recommendations.append("⚠️ GPU memory issues detected")
                    recommendations.append("💡 Reduce batch sizes or model size")
                    
            elif test.test_name == "model_loading":
                recommendations.append("⚠️ Model loading failed")
                recommendations.append("💡 Check framework compatibility")
        
        # Framework-specific recommendations
        if framework == "directml":
            recommendations.append("💡 Ensure Windows Adrenalin driver 23.40.27.06+")
            recommendations.append("💡 DirectML works best without virtual environments")
        elif framework in ["rocm", "pytorch_rocm"]:
            recommendations.append("💡 Ensure ROCm 6.4.1+ is installed on Linux")
            recommendations.append("💡 Check HIP_VISIBLE_DEVICES environment variable")
        
        return recommendations
    
    async def validate_all_environments(self, environments: Dict[str, Any]) -> Dict[str, EnvironmentValidationResult]:
        """Validate multiple environments"""
        results = {}
        
        for env_name, env_info in environments.items():
            try:
                result = await self.validate_environment(
                    env_name=env_name,
                    python_executable=env_info.get("python_executable", "python"),
                    framework=env_info.get("framework", "pytorch"),
                    gpu_devices=env_info.get("gpu_devices", [])
                )
                results[env_name] = result
            except Exception as e:
                logger.error(f"Failed to validate environment {env_name}", error=str(e))
                # Create failed result
                results[env_name] = EnvironmentValidationResult(
                    environment_name=env_name,
                    overall_success=False,
                    python_executable="",
                    gpu_devices=[],
                    test_results=[],
                    performance_metrics={},
                    recommendations=[f"Validation failed: {str(e)}"]
                )
        
        return results
    
    def save_validation_report(self, results: Dict[str, EnvironmentValidationResult], 
                              output_path: Optional[Path] = None):
        """Save validation results to report"""
        if output_path is None:
            output_path = Path("validation_report.json")
        
        # Convert results to serializable format
        report_data = {}
        for env_name, result in results.items():
            report_data[env_name] = {
                "environment_name": result.environment_name,
                "overall_success": result.overall_success,
                "python_executable": result.python_executable,
                "gpu_devices": result.gpu_devices,
                "performance_metrics": result.performance_metrics,
                "recommendations": result.recommendations,
                "test_results": [
                    {
                        "test_name": test.test_name,
                        "success": test.success,
                        "duration_ms": test.duration_ms,
                        "error": test.error,
                        "details": test.details
                    }
                    for test in result.test_results
                ]
            }
        
        with open(output_path, 'w') as f:
            json.dump(report_data, f, indent=2)
        
        logger.info(f"Validation report saved to {output_path}")
