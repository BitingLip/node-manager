"""
Tests for the environment planner module
"""

import os
import unittest
import sys
import tempfile
from unittest import mock
from pathlib import Path
import json

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from environment.environment.environment_planner import (
    EnvironmentPlanner,
    EnvironmentSpec,
    EnvironmentType,
    FrameworkType,
    EnvironmentSetupResult
)
from environment.gpu.gpu_detector import GPUInfo, GPUVendor, EnvironmentRequirement


class EnvironmentPlannerTests(unittest.TestCase):
    """Tests for the environment planner"""
    
    def setUp(self):
        """Set up test environment"""
        self.temp_dir = tempfile.TemporaryDirectory()
        self.planner = EnvironmentPlanner(base_path=Path(self.temp_dir.name))
        
        # Create some test GPU info objects
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
    
    def test_create_environment_spec(self):
        """Test creating environment spec for NVIDIA GPU"""
        # Prepare requirements dict with GPU ID as key
        requirements = {"0": self.nvidia_req}
        
        # Plan environments (this will create specs internally)
        specs = self.planner.plan_environments(requirements)
        
        # Verify we got a spec back
        self.assertEqual(len(specs), 1)
        
        # Get the first spec
        spec_name = list(specs.keys())[0]
        spec = specs[spec_name]
        
        # Verify spec properties
        self.assertIn("pytorch", spec_name)
        self.assertEqual(spec.env_type, EnvironmentType.VENV)
        self.assertEqual(spec.framework, FrameworkType.PYTORCH_CUDA)
        self.assertTrue(any("torch" in pkg for pkg in spec.base_packages))
    
    def test_plan_environments_single_gpu(self):
        """Test planning environments with a single GPU"""
        # Prepare requirements dict with GPU ID as key
        requirements = {"0": self.nvidia_req}
        
        # Plan environments
        specs = self.planner.plan_environments(requirements)
          # Verify results
        self.assertEqual(len(specs), 1)
        spec_name = list(specs.keys())[0]
        spec = specs[spec_name]
        
        self.assertEqual(spec.framework, FrameworkType.PYTORCH_CUDA)
        self.assertEqual(len(spec.target_gpus), 1)
        self.assertEqual(spec.target_gpus[0], "0")
    
    def test_plan_environments_multiple_gpus(self):
        """Test planning environments with multiple GPUs"""
        # Prepare requirements dict with GPU IDs as keys
        requirements = {"0": self.nvidia_req, "1": self.amd_req}
        
        # Plan environments
        specs = self.planner.plan_environments(requirements)
        
        # Verify results - should create separate environments for different GPU types
        self.assertEqual(len(specs), 2)
          # Check that we have two different environment types
        has_cuda_env = False
        has_rocm_env = False
        
        for name, spec in specs.items():
            if spec.framework == FrameworkType.PYTORCH_CUDA:
                has_cuda_env = True
                # Check that NVIDIA GPU is in this environment
                self.assertEqual(spec.target_gpus[0], "0")
            elif spec.framework == FrameworkType.PYTORCH_ROCM:
                has_rocm_env = True
                # Check that AMD GPU is in this environment
                self.assertEqual(spec.target_gpus[0], "1")
        
        # Verify both environment types were created
        self.assertTrue(has_cuda_env)
        self.assertTrue(has_rocm_env)
    
    @mock.patch('environment.environment.environment_planner.Path.exists')
    @mock.patch('environment.environment.environment_planner.Path.mkdir')
    @mock.patch('environment.environment.environment_planner.Path.open')
    def test_save_environment_specs(self, mock_open, mock_mkdir, mock_exists):
        """Test saving environment specs to filesystem"""
        # Mock filesystem operations
        mock_exists.return_value = False
        
        # Create a mock file object
        mock_file = mock.MagicMock()
        mock_open.return_value.__enter__.return_value = mock_file
        
        # Plan environments (creates specs)
        specs = self.planner.plan_environments({"0": self.nvidia_req})
        
        # Save specs
        self.planner.save_environment_specs()
        
        # Verify that directory creation was called
        mock_mkdir.assert_called()
        
        # Verify file write was called
        mock_file.write.assert_called()
        
        # Verify serialized data contains expected content
        write_arg = mock_file.write.call_args[0][0]
        self.assertIn("pytorch", write_arg)
        self.assertIn("venv", write_arg)
    
    @mock.patch('environment.environment.environment_planner.asyncio.sleep', return_value=None)
    @mock.patch('environment.environment.environment_planner.subprocess.run')
    async def test_create_environment(self, mock_run, mock_sleep):
        """Test creating an environment asynchronously"""
        # Mock subprocess run
        mock_run.return_value = mock.Mock(returncode=0)
        
        # Plan an environment
        specs = self.planner.plan_environments({"0": self.nvidia_req})
        spec = list(specs.values())[0]
        
        # Create environment
        result = await self.planner.create_environment(spec)
          # Verify successful setup
        self.assertTrue(result.success)
        self.assertEqual(result.env_name, spec.name)
        self.assertIsNotNone(result.path)


if __name__ == '__main__':
    unittest.main()
