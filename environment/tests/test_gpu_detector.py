"""
Tests for the GPU hardware detector
"""

import os
import unittest
import sys
from unittest import mock
from pathlib import Path
import json

# Handle imports based on location
parent_dir = Path(__file__).resolve().parent.parent.parent
if str(parent_dir) not in sys.path:
    sys.path.append(str(parent_dir))

try:
    # Try relative import first
    from ..gpu.gpu_detector import (
        GPUDetector, 
        GPUVendor, 
        AMDArchitecture, 
        NVIDIAArchitecture,
        GPUInfo,
        EnvironmentRequirement,
        OSEnvironmentType
    )
except ImportError:
    # Fall back to absolute import
    from environment.gpu.gpu_detector import (
        GPUDetector, 
        GPUVendor, 
        AMDArchitecture, 
        NVIDIAArchitecture,
        GPUInfo,
        EnvironmentRequirement,
        OSEnvironmentType
    )


class GPUDetectionTests(unittest.TestCase):
    """Tests for the GPU hardware detection"""
    
    def setUp(self):
        """Set up test environment"""
        self.detector = GPUDetector()
    
    @mock.patch('environment.gpu.gpu_detector.pynvml')
    def test_detect_nvidia_gpus(self, mock_pynvml):
        """Test NVIDIA GPU detection"""
        # Mock pynvml methods
        mock_pynvml.nvmlDeviceGetCount.return_value = 2
        
        # Create mock handles
        mock_handle_1 = mock.MagicMock()
        mock_handle_2 = mock.MagicMock()
        
        # Configure handle behavior
        mock_pynvml.nvmlDeviceGetHandleByIndex.side_effect = [mock_handle_1, mock_handle_2]
        mock_pynvml.nvmlDeviceGetName.side_effect = [
            b"NVIDIA GeForce RTX 3080", 
            b"NVIDIA GeForce RTX 3070"
        ]
        
        # Mock memory info
        mem_info_1 = mock.MagicMock()
        mem_info_1.total = 10737418240  # 10 GB in bytes
        mem_info_2 = mock.MagicMock()
        mem_info_2.total = 8589934592   # 8 GB in bytes
        mock_pynvml.nvmlDeviceGetMemoryInfo.side_effect = [mem_info_1, mem_info_2]
        
        # Mock driver and compute capability
        mock_pynvml.nvmlSystemGetDriverVersion.return_value = b"535.104.05"
        mock_pynvml.nvmlDeviceGetCudaComputeCapability.side_effect = [(8, 6), (8, 6)]
        
        # Run detection
        self.detector._detect_nvidia_gpus()
        gpus = [gpu for gpu in self.detector.detected_gpus if gpu.vendor == GPUVendor.NVIDIA]
        
        # Verify results
        self.assertEqual(len(gpus), 2)
        self.assertEqual(gpus[0].vendor, GPUVendor.NVIDIA)
        self.assertEqual(gpus[0].name, "NVIDIA GeForce RTX 3080")
        self.assertEqual(gpus[0].architecture, str(NVIDIAArchitecture.AMPERE.value))
        self.assertEqual(gpus[0].memory_mb, 10240)
        self.assertEqual(gpus[1].name, "NVIDIA GeForce RTX 3070")

    @mock.patch('environment.gpu.gpu_detector.subprocess.run')
    def test_detect_amd_gpus(self, mock_run):
        """Test AMD GPU detection"""
        # Mock command outputs for AMD detection
        def mock_subprocess_output(*args, **kwargs):
            cmd = args[0][0] if isinstance(args[0], list) else args[0]
            
            if "lspci" in cmd:
                process = mock.Mock()
                process.returncode = 0
                process.stdout = (
                    "01:00.0 VGA compatible controller: Advanced Micro Devices, Inc. [AMD/ATI] "
                    "Navi 21 [Radeon RX 6800/6800 XT / 6900 XT] (rev c1)\n"
                    "02:00.0 VGA compatible controller: Advanced Micro Devices, Inc. [AMD/ATI] "
                    "Navi 23 [Radeon RX 6600/6600 XT] (rev c1)"
                ).encode()
                return process
            elif "rocm-smi" in cmd:
                process = mock.Mock()
                process.returncode = 0
                process.stdout = (
                    "GPU 0: Navi 21 [Radeon RX 6900 XT]\n"
                    "Memory: 16GB\n"
                    "Driver: amdgpu 5.18.13\n"
                    "GPU 1: Navi 23 [Radeon RX 6600 XT]\n"
                    "Memory: 8GB\n"
                    "Driver: amdgpu 5.18.13"
                ).encode()
                return process
            else:
                process = mock.Mock()
                process.returncode = 1
                return process
                
        mock_run.side_effect = mock_subprocess_output
        
        # Run detection
        self.detector._detect_amd_gpus()
        gpus = [gpu for gpu in self.detector.detected_gpus if gpu.vendor == GPUVendor.AMD]
        
        # Verify results
        self.assertEqual(len(gpus), 2)
        self.assertEqual(gpus[0].vendor, GPUVendor.AMD)
        self.assertEqual(gpus[0].architecture, str(AMDArchitecture.RDNA2.value))
        self.assertEqual(gpus[0].memory_mb, 16384)
        self.assertEqual(gpus[1].architecture, str(AMDArchitecture.RDNA2.value))
    
    @mock.patch('environment.gpu.gpu_detector.platform.system')
    @mock.patch('environment.gpu.gpu_detector.os.path.exists')
    def test_detect_os_environment(self, mock_exists, mock_system):
        """Test OS environment detection"""
        # Test Windows detection
        mock_system.return_value = "Windows"
        mock_exists.return_value = False
        
        env_type = self.detector.detect_os_environment()
        self.assertEqual(env_type, OSEnvironmentType.WINDOWS_NATIVE)
        
        # Test WSL detection
        mock_exists.return_value = True
        env_type = self.detector.detect_os_environment()
        self.assertEqual(env_type, OSEnvironmentType.WINDOWS_WSL)
        
        # Test Linux detection
        mock_system.return_value = "Linux"
        mock_exists.return_value = False
        env_type = self.detector.detect_os_environment()
        self.assertEqual(env_type, OSEnvironmentType.LINUX_NATIVE)
    
    @mock.patch('environment.gpu.gpu_detector.GPUDetector._detect_nvidia_gpus')
    @mock.patch('environment.gpu.gpu_detector.GPUDetector._detect_amd_gpus')
    @mock.patch('environment.gpu.gpu_detector.GPUDetector._detect_intel_gpus')
    def test_detect_all_gpus(self, mock_intel, mock_amd, mock_nvidia):
        """Test combined GPU detection"""
        # Mock detection methods to add GPUs to the detector
        def add_nvidia_gpus():
            self.detector.detected_gpus.append(
                GPUInfo(
                    device_id="0", 
                    vendor=GPUVendor.NVIDIA,
                    name="NVIDIA GeForce RTX 3080",
                    architecture=str(NVIDIAArchitecture.AMPERE.value),
                    memory_mb=10240,
                    driver_version="535.104.05",
                    compute_capability="8.6",
                    pci_id="0000:01:00.0",
                    supported_apis=["CUDA", "OpenGL"],
                    power_limit_w=320,
                    temperature_c=65
                )
            )
        
        def add_amd_gpus():
            self.detector.detected_gpus.append(
                GPUInfo(
                    device_id="1",
                    vendor=GPUVendor.AMD,
                    name="AMD Radeon RX 6800 XT",
                    architecture=str(AMDArchitecture.RDNA2.value),
                    memory_mb=16384,
                    driver_version="amdgpu 5.18.13",
                    compute_capability=None,
                    pci_id="0000:02:00.0",
                    supported_apis=["ROCm", "OpenGL"],
                    power_limit_w=300,
                    temperature_c=72
                )
            )
        
        # Set side effects to add GPUs
        mock_nvidia.side_effect = add_nvidia_gpus
        mock_amd.side_effect = add_amd_gpus
        
        # Run all detection
        gpus = self.detector.detect_all_gpus()
        
        # Verify results
        self.assertEqual(len(gpus), 2)
        self.assertEqual(gpus[0].vendor, GPUVendor.NVIDIA)
        self.assertEqual(gpus[1].vendor, GPUVendor.AMD)
        
    def test_get_environment_requirements(self):
        """Test getting environment requirements for a GPU"""
        # Create a test GPU
        gpu = GPUInfo(
            device_id="0", 
            vendor=GPUVendor.NVIDIA,
            name="NVIDIA GeForce RTX 3080",
            architecture=str(NVIDIAArchitecture.AMPERE.value),
            memory_mb=10240,
            driver_version="535.104.05",
            compute_capability="8.6",
            pci_id="0000:01:00.0",
            supported_apis=["CUDA", "OpenGL"],
            power_limit_w=320,
            temperature_c=65
        )
        
        # Get requirements
        req = self.detector.get_environment_requirements(gpu)
        
        # Verify results
        self.assertIsInstance(req, EnvironmentRequirement)
        self.assertEqual(req.gpu_info, gpu)
        self.assertEqual(req.framework, "pytorch")
        self.assertTrue("torch" in req.required_packages)
        self.assertTrue(any("cuda" in pkg for pkg in req.required_packages))


if __name__ == '__main__':
    unittest.main()
