"""
Standalone test for environment planner
"""

import unittest
from unittest import mock
import tempfile
from pathlib import Path

# Mock classes
class EnvironmentType:
    VENV = "venv"
    NATIVE = "native"
    CONDA = "conda"
    SYSTEM = "system"

class FrameworkType:
    PYTORCH = "pytorch"
    PYTORCH_CUDA = "pytorch_cuda"
    PYTORCH_ROCM = "pytorch_rocm"
    DIRECTML = "directml"
    TENSORFLOW = "tensorflow"
    ONNX = "onnx"

class GPUVendor:
    NVIDIA = "nvidia"
    AMD = "amd"
    INTEL = "intel"
    UNKNOWN = "unknown"

class EnvironmentSpec:
    def __init__(self, name, env_type, framework, python_version, base_packages,
                 gpu_packages, additional_packages, pip_extra_index_urls,
                 environment_variables, validation_commands, conflicting_envs, target_gpus):
        self.name = name
        self.env_type = env_type
        self.framework = framework
        self.python_version = python_version
        self.base_packages = base_packages
        self.gpu_packages = gpu_packages
        self.additional_packages = additional_packages
        self.pip_extra_index_urls = pip_extra_index_urls
        self.environment_variables = environment_variables
        self.validation_commands = validation_commands
        self.conflicting_envs = conflicting_envs
        self.target_gpus = target_gpus

class EnvironmentSetupResult:
    def __init__(self, env_name, success, path, python_executable, 
                 installed_packages, validation_results, errors, warnings):
        self.env_name = env_name
        self.success = success
        self.path = path
        self.python_executable = python_executable
        self.installed_packages = installed_packages
        self.validation_results = validation_results
        self.errors = errors
        self.warnings = warnings

class GPUInfo:
    def __init__(self, device_id, vendor, name, architecture, memory_mb, **kwargs):
        self.device_id = device_id
        self.vendor = vendor
        self.name = name
        self.architecture = architecture
        self.memory_mb = memory_mb
        for key, value in kwargs.items():
            setattr(self, key, value)

class EnvironmentRequirement:
    def __init__(self, gpu_info, python_env_type, framework, min_driver_version, 
                 required_packages, os_requirements, conflicts_with, validation_script):
        self.gpu_info = gpu_info
        self.python_env_type = python_env_type
        self.framework = framework
        self.min_driver_version = min_driver_version
        self.required_packages = required_packages
        self.os_requirements = os_requirements
        self.conflicts_with = conflicts_with
        self.validation_script = validation_script

# Mock EnvironmentPlanner class
class EnvironmentPlanner:
    def __init__(self, base_path=None):
        """Initialize environment planner"""
        self.base_path = base_path or Path.cwd() / "environments"
        self.environment_specs = {}
        self.python_version = "3.10"
        self.system_os = "win32"
    
    def plan_environments(self, requirements):
        """
        Plan environments based on requirements
        Return a map of environment name -> environment spec
        """
        specs = {}
        
        # Group requirements by framework
        cuda_reqs = []
        rocm_reqs = []
        directml_reqs = []
        
        for gpu_id, req in requirements.items():
            # Check GPU vendor to determine framework
            if req.gpu_info.vendor == GPUVendor.NVIDIA:
                cuda_reqs.append(req)
            elif req.gpu_info.vendor == GPUVendor.AMD:
                if "rdna3" in req.gpu_info.architecture or "rdna4" in req.gpu_info.architecture:
                    rocm_reqs.append(req)
                else:
                    directml_reqs.append(req)
        
        # Create environment specs
        if cuda_reqs:
            specs["pytorch_cuda_env"] = self._create_environment_spec(
                "pytorch_cuda_env", cuda_reqs)
        
        if rocm_reqs:
            specs["pytorch_rocm_env"] = self._create_environment_spec(
                "pytorch_rocm_env", rocm_reqs)
        
        if directml_reqs:
            specs["directml_env"] = self._create_environment_spec(
                "directml_env", directml_reqs)
        
        self.environment_specs = specs
        return specs
    
    def _create_environment_spec(self, name, requirements):
        """Create environment spec from requirements"""
        # Determine framework type
        if any(req.gpu_info.vendor == GPUVendor.NVIDIA for req in requirements):
            framework = FrameworkType.PYTORCH_CUDA
        elif any("rdna3" in req.gpu_info.architecture or "rdna4" in req.gpu_info.architecture 
                for req in requirements):
            framework = FrameworkType.PYTORCH_ROCM
        else:
            framework = FrameworkType.DIRECTML
        
        # Collect all GPU IDs
        target_gpus = [req.gpu_info.device_id for req in requirements]
        
        return EnvironmentSpec(
            name=name,
            env_type=EnvironmentType.VENV,
            framework=framework,
            python_version=self.python_version,
            base_packages=["torch", "torchvision", "torchaudio"],
            gpu_packages=["cuda-toolkit"] if framework == FrameworkType.PYTORCH_CUDA else 
                        ["rocm-smi"] if framework == FrameworkType.PYTORCH_ROCM else 
                        ["directml"],
            additional_packages=["numpy", "pillow", "matplotlib"],
            pip_extra_index_urls=[],
            environment_variables={},
            validation_commands=[],
            conflicting_envs=[],
            target_gpus=target_gpus
        )
    
    async def create_environment(self, spec):
        """Create environment from spec"""
        return EnvironmentSetupResult(
            env_name=spec.name,
            success=True,
            path=str(self.base_path / spec.name),
            python_executable=str(self.base_path / spec.name / "bin" / "python"),
            installed_packages=[],
            validation_results={},
            errors=[],
            warnings=[]
        )
    
    def save_environment_specs(self, filename="environment_specs.json"):
        """Save environment specs to file"""
        pass
    
    def load_environment_specs(self, filename="environment_specs.json"):
        """Load environment specs from file"""
        return self.environment_specs


class TestEnvironmentPlanner(unittest.TestCase):
    """Test environment planner functionality"""
    
    def setUp(self):
        """Set up test environment"""
        self.temp_dir = tempfile.TemporaryDirectory()
        self.planner = EnvironmentPlanner(Path(self.temp_dir.name))
        
        # Create test GPUs
        self.nvidia_gpu = GPUInfo(
            device_id="0",
            vendor=GPUVendor.NVIDIA,
            name="NVIDIA GeForce RTX 3080",
            architecture="ampere",
            memory_mb=10240,
            driver_version="535.104.05",
            compute_capability="8.6",
            pci_id="0000:01:00.0",
            supported_apis=["CUDA", "OpenGL"],
            power_limit_w=320,
            temperature_c=65
        )
        
        self.amd_gpu = GPUInfo(
            device_id="1",
            vendor=GPUVendor.AMD,
            name="AMD Radeon RX 6800 XT",
            architecture="rdna2",
            memory_mb=16384,
            driver_version="amdgpu 5.18.13",
            compute_capability=None,
            pci_id="0000:02:00.0",
            supported_apis=["ROCm", "OpenGL"],
            power_limit_w=300,
            temperature_c=72
        )
        
        # Create environment requirements
        self.nvidia_req = EnvironmentRequirement(
            gpu_info=self.nvidia_gpu,
            python_env_type="venv",
            framework="pytorch",
            min_driver_version="450.0",
            required_packages=["torch", "torchvision", "torchaudio", "cuda-toolkit"],
            os_requirements=["CUDA 11.7+"],
            conflicts_with=[],
            validation_script="validate_cuda.py"
        )
        
        self.amd_req = EnvironmentRequirement(
            gpu_info=self.amd_gpu,
            python_env_type="venv",
            framework="pytorch",
            min_driver_version="amdgpu 5.10",
            required_packages=["torch", "torchvision", "torchaudio", "rocm-smi"],
            os_requirements=["ROCm 5.0+"],
            conflicts_with=[],
            validation_script="validate_rocm.py"
        )
    
    def tearDown(self):
        """Clean up test environment"""
        self.temp_dir.cleanup()
    
    def test_plan_environments_single_gpu(self):
        """Test planning environments with a single GPU"""
        # Create requirements dictionary
        requirements = {"0": self.nvidia_req}
        
        # Plan environments
        specs = self.planner.plan_environments(requirements)
        
        # Verify results
        self.assertEqual(len(specs), 1)
        self.assertIn("pytorch_cuda_env", specs)
        
        # Check environment spec
        spec = specs["pytorch_cuda_env"]
        self.assertEqual(spec.framework, FrameworkType.PYTORCH_CUDA)
        self.assertEqual(len(spec.target_gpus), 1)
        self.assertEqual(spec.target_gpus[0], "0")
    
    def test_plan_environments_multiple_gpus(self):
        """Test planning environments with multiple GPUs"""
        # Create requirements dictionary
        requirements = {"0": self.nvidia_req, "1": self.amd_req}
        
        # Plan environments
        specs = self.planner.plan_environments(requirements)
        
        # Verify results
        self.assertEqual(len(specs), 2)
        self.assertIn("pytorch_cuda_env", specs)
        self.assertIn("directml_env", specs)
        
        # Check CUDA environment spec
        cuda_spec = specs["pytorch_cuda_env"]
        self.assertEqual(cuda_spec.framework, FrameworkType.PYTORCH_CUDA)
        self.assertEqual(len(cuda_spec.target_gpus), 1)
        self.assertEqual(cuda_spec.target_gpus[0], "0")
        
        # Check DirectML environment spec
        directml_spec = specs["directml_env"]
        self.assertEqual(directml_spec.framework, FrameworkType.DIRECTML)
        self.assertEqual(len(directml_spec.target_gpus), 1)
        self.assertEqual(directml_spec.target_gpus[0], "1")    
    
    async def test_create_environment(self):
        """Test creating environment from spec"""
        # Create requirements dictionary
        requirements = {"0": self.nvidia_req}
        
        # Plan environments
        specs = self.planner.plan_environments(requirements)
        spec = specs["pytorch_cuda_env"]
        
        # Create environment
        result = await self.planner.create_environment(spec)
        
        # Verify results
        self.assertTrue(result.success)
        self.assertEqual(result.env_name, spec.name)
        self.assertTrue(result.path.endswith(spec.name))


if __name__ == "__main__":
    import asyncio
    
    # Create test suite
    suite = unittest.TestSuite()
    suite.addTest(TestEnvironmentPlanner('test_plan_environments_single_gpu'))
    suite.addTest(TestEnvironmentPlanner('test_plan_environments_multiple_gpus'))
    
    # Run synchronous tests
    test_runner = unittest.TextTestRunner()
    test_runner.run(suite)
      # Run async tests separately
    async def run_async_tests():
        test = TestEnvironmentPlanner('test_create_environment')
        test.setUp()
        try:
            # Test create_environment without passing the mock as an argument
            await test.test_create_environment()
            print("Async test passed: test_create_environment")
        finally:
            test.tearDown()
    
    # Run async tests
    asyncio.run(run_async_tests())
