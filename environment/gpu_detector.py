"""
GPU Hardware Detector with Enhanced RDNA4 and OS Support
Detects and analyzes GPU hardware capabilities for environment planning
Identifies vendor, architecture, driver versions, compatibility, and virtual environment requirements
Includes RDNA4 support and WSL detection for optimal environment strategy
"""

import platform
import subprocess
import os
import sys
import wmi
from typing import Dict, List, Optional, Any, NamedTuple
from enum import Enum
from dataclasses import dataclass
import structlog

logger = structlog.get_logger(__name__)


class OSEnvironmentType(Enum):
    """Operating system environment types for GPU optimization"""
    WINDOWS_NATIVE = "windows_native"
    WINDOWS_WSL = "windows_wsl"
    LINUX_NATIVE = "linux_native"
    MACOS = "macos"
    UNKNOWN = "unknown"


class VirtualEnvironmentStrategy(Enum):
    """Virtual environment strategies based on GPU architecture"""
    SYSTEM_PYTHON_REQUIRED = "system_python_required"  # RDNA1, RDNA2
    VENV_WSL_PREFERRED = "venv_wsl_preferred"  # RDNA3, RDNA4, NVIDIA
    VENV_CUDA = "venv_cuda"  # NVIDIA specific
    MIXED_STRATEGY = "mixed_strategy"  # Multiple GPU types


class GPUVendor(Enum):
    """GPU Vendor types"""
    NVIDIA = "nvidia"
    AMD = "amd" 
    INTEL = "intel"
    UNKNOWN = "unknown"


class AMDArchitecture(Enum):
    """AMD GPU Architecture types"""
    RDNA1 = "rdna1"  # RX 5000 series
    RDNA2 = "rdna2"  # RX 6000 series
    RDNA3 = "rdna3"  # RX 7000 series
    RDNA4 = "rdna4"  # Future RX 8000+ series
    VEGA = "vega"    # RX Vega series
    POLARIS = "polaris"  # RX 400/500 series
    UNKNOWN = "unknown"


class NVIDIAArchitecture(Enum):
    """NVIDIA GPU Architecture types"""
    PASCAL = "pascal"    # GTX 10 series
    TURING = "turing"    # RTX 20 series, GTX 16 series
    AMPERE = "ampere"    # RTX 30 series
    ADA = "ada"          # RTX 40 series
    HOPPER = "hopper"    # H100 series
    UNKNOWN = "unknown"


@dataclass
class GPUInfo:
    """GPU device information"""
    device_id: str
    vendor: GPUVendor
    name: str
    architecture: str
    memory_mb: int
    driver_version: Optional[str]
    compute_capability: Optional[str]
    pci_id: Optional[str]
    supported_apis: List[str]
    power_limit_w: Optional[int]
    temperature_c: Optional[int]


@dataclass
class EnvironmentRequirement:
    """Environment requirements for a GPU"""
    gpu_info: GPUInfo
    python_env_type: str  # "venv", "native", "conda"
    framework: str  # "pytorch", "directml", "rocm"
    min_driver_version: str
    required_packages: List[str]
    os_requirements: List[str]
    conflicts_with: List[str]
    validation_script: str


class GPUDetector:
    """
    Detects GPU hardware and determines environment requirements
    Supports NVIDIA, AMD, and mixed GPU configurations
    """
    
    def __init__(self):
        """Initialize GPU detector"""
        self.detected_gpus = []
        self.system_os = platform.system().lower()
        self.os_version = platform.release()
        
        # AMD architecture mapping
        self.amd_device_map = {
            # RDNA1 (RX 5000 series)
            "navi10": AMDArchitecture.RDNA1,
            "navi12": AMDArchitecture.RDNA1,
            "navi14": AMDArchitecture.RDNA1,
            
            # RDNA2 (RX 6000 series)  
            "navi21": AMDArchitecture.RDNA2,
            "navi22": AMDArchitecture.RDNA2,
            "navi23": AMDArchitecture.RDNA2,
            "navi24": AMDArchitecture.RDNA2,
            
            # RDNA3 (RX 7000 series)
            "navi31": AMDArchitecture.RDNA3,
            "navi32": AMDArchitecture.RDNA3,
            "navi33": AMDArchitecture.RDNA3,
            
            # Vega
            "vega10": AMDArchitecture.VEGA,
            "vega20": AMDArchitecture.VEGA,
        }
        
        logger.info("GPUDetector initialized", os=self.system_os, version=self.os_version)
    
    def detect_all_gpus(self) -> List[GPUInfo]:
        """Detect all GPUs in the system"""
        self.detected_gpus = []
        
        # Try different detection methods
        self._detect_nvidia_gpus()
        self._detect_amd_gpus()
        
        logger.info(f"Detected {len(self.detected_gpus)} GPUs")
        return self.detected_gpus
    
    def _detect_nvidia_gpus(self) -> List[GPUInfo]:
        """Detect NVIDIA GPUs using nvidia-ml-py"""
        nvidia_gpus = []
        
        try:
            import pynvml
            pynvml.nvmlInit()
            
            device_count = pynvml.nvmlDeviceGetCount()
            
            for i in range(device_count):
                handle = pynvml.nvmlDeviceGetHandleByIndex(i)
                
                # Get basic info
                name = pynvml.nvmlDeviceGetName(handle).decode('utf-8')
                memory_info = pynvml.nvmlDeviceGetMemoryInfo(handle)
                
                # Get driver version
                try:
                    driver_version = pynvml.nvmlSystemGetDriverVersion().decode('utf-8')
                except:
                    driver_version = None
                
                # Get compute capability
                try:
                    # pynvml does not provide compute capability, so use nvidia-smi as fallback
                    compute_capability = None
                    try:
                        smi_output = subprocess.check_output(
                            ["nvidia-smi", "--query-gpu=compute_cap", "--format=csv,noheader", f"--id={i}"],
                            encoding="utf-8"
                        )
                        compute_capability = smi_output.strip()
                        if not compute_capability or "N/A" in compute_capability:
                            compute_capability = None
                    except Exception:
                        compute_capability = None
                except Exception:
                    compute_capability = None
                
                # Determine architecture from compute capability
                architecture = self._determine_nvidia_architecture(compute_capability, name)
                
                gpu_info = GPUInfo(
                    device_id=f"nvidia:{i}",
                    vendor=GPUVendor.NVIDIA,
                    name=name,
                    architecture=architecture.value,
                    memory_mb=int(memory_info.total / (1024 * 1024)),
                    driver_version=driver_version,
                    compute_capability=compute_capability,
                    pci_id=None,  # TODO: Get PCI ID
                    supported_apis=["cuda", "opencl"],
                    power_limit_w=None,  # TODO: Get power limit
                    temperature_c=None   # TODO: Get temperature
                )
                
                nvidia_gpus.append(gpu_info)
                self.detected_gpus.append(gpu_info)
                
        except ImportError:
            logger.debug("pynvml not available for NVIDIA detection")
        except Exception as e:
            logger.warning("Failed to detect NVIDIA GPUs", error=str(e))
        
        return nvidia_gpus
    
    def _detect_amd_gpus(self) -> List[GPUInfo]:
        """Detect AMD GPUs using various methods"""
        amd_gpus = []
        
        if self.system_os == "windows":
            amd_gpus.extend(self._detect_amd_windows())
        elif self.system_os == "linux":
            amd_gpus.extend(self._detect_amd_linux())
        
        return amd_gpus
    
    def _detect_amd_windows(self) -> List[GPUInfo]:
        """Detect AMD GPUs on Windows using wmic"""
        amd_gpus = []
        
        try:
            # Use wmic to get GPU info
            result = subprocess.run([
                "wmic", "path", "win32_VideoController", 
                "get", "Name,AdapterRAM,DriverVersion,PNPDeviceID", "/format:csv"
            ], capture_output=True, text=True, check=True)
            
            lines = result.stdout.strip().split('\n')[1:]  # Skip header
            
            for i, line in enumerate(lines):
                if not line.strip():
                    continue
                    
                parts = line.split(',')
                if len(parts) < 5:
                    continue
                
                name = parts[2].strip()
                if not self._is_amd_gpu(name):
                    continue
                
                # Extract info
                memory_bytes = parts[1].strip() if parts[1].strip() else "0"
                driver_version = parts[3].strip()
                pci_id = parts[4].strip()
                
                # Determine architecture
                architecture = self._determine_amd_architecture(name, pci_id)
                
                gpu_info = GPUInfo(
                    device_id=f"amd:{i}",
                    vendor=GPUVendor.AMD,
                    name=name,
                    architecture=architecture.value,
                    memory_mb=int(int(memory_bytes) / (1024 * 1024)) if memory_bytes.isdigit() else 0,
                    driver_version=driver_version,
                    compute_capability=None,
                    pci_id=pci_id,
                    supported_apis=["directml", "opencl"],
                    power_limit_w=None,
                    temperature_c=None
                )
                
                amd_gpus.append(gpu_info)
                self.detected_gpus.append(gpu_info)
                
        except Exception as e:
            logger.warning("Failed to detect AMD GPUs on Windows", error=str(e))
        
        return amd_gpus
    
    def _detect_amd_linux(self) -> List[GPUInfo]:
        """Detect AMD GPUs on Linux using rocm-smi"""
        amd_gpus = []
        
        try:
            # Try rocm-smi
            result = subprocess.run(["rocm-smi", "--showallinfo"], 
                                   capture_output=True, text=True, check=True)
            
            # Parse rocm-smi output
            gpu_sections = result.stdout.split("GPU[")
            
            for i, section in enumerate(gpu_sections[1:]):  # Skip first empty section
                lines = section.split('\n')
                
                name = ""
                memory_mb = 0
                driver_version = ""
                
                for line in lines:
                    if "Card series:" in line:
                        name = line.split(":")[-1].strip()
                    elif "GPU Memory Total:" in line:
                        memory_str = line.split(":")[-1].strip()
                        # Parse memory (e.g., "8176 MB")
                        if "MB" in memory_str:
                            memory_mb = int(memory_str.split()[0])
                
                if name:
                    architecture = self._determine_amd_architecture(name, "")
                    
                    gpu_info = GPUInfo(
                        device_id=f"amd:{i}",
                        vendor=GPUVendor.AMD,
                        name=name,
                        architecture=architecture.value,
                        memory_mb=memory_mb,
                        driver_version=driver_version,
                        compute_capability=None,
                        pci_id=None,
                        supported_apis=["rocm", "opencl"],
                        power_limit_w=None,
                        temperature_c=None
                    )
                    
                    amd_gpus.append(gpu_info)
                    self.detected_gpus.append(gpu_info)
                    
        except FileNotFoundError:
            logger.debug("rocm-smi not available")
        except Exception as e:
            logger.warning("Failed to detect AMD GPUs on Linux", error=str(e))
        
        return amd_gpus
    
    def _is_amd_gpu(self, name: str) -> bool:
        """Check if GPU name indicates AMD GPU"""
        amd_indicators = ["radeon", "rx ", "vega", "navi", "rdna"]
        name_lower = name.lower()
        return any(indicator in name_lower for indicator in amd_indicators)
    
    def _determine_nvidia_architecture(self, compute_capability: Optional[str], name: str) -> NVIDIAArchitecture:
        """Determine NVIDIA architecture from compute capability and name"""
        if not compute_capability:
            return NVIDIAArchitecture.UNKNOWN
        
        # Map compute capability to architecture
        cc_major = int(compute_capability.split('.')[0])
        
        if cc_major >= 9:
            return NVIDIAArchitecture.HOPPER  # H100
        elif cc_major == 8:
            if "rtx 40" in name.lower() or "ada" in name.lower():
                return NVIDIAArchitecture.ADA  # RTX 40 series
            else:
                return NVIDIAArchitecture.AMPERE  # RTX 30 series
        elif cc_major == 7:
            return NVIDIAArchitecture.TURING  # RTX 20, GTX 16 series
        elif cc_major == 6:
            return NVIDIAArchitecture.PASCAL  # GTX 10 series
        else:
            return NVIDIAArchitecture.UNKNOWN
    
    def _determine_amd_architecture(self, name: str, pci_id: str) -> AMDArchitecture:
        """Determine AMD architecture from name and PCI ID"""
        name_lower = name.lower()
        
        # Check by series number
        if "rx 7" in name_lower:
            return AMDArchitecture.RDNA3
        elif "rx 6" in name_lower:
            return AMDArchitecture.RDNA2
        elif "rx 5" in name_lower:
            return AMDArchitecture.RDNA1
        elif "vega" in name_lower:
            return AMDArchitecture.VEGA
        elif any(x in name_lower for x in ["rx 4", "rx 5"]):
            return AMDArchitecture.POLARIS
        
        # TODO: Use PCI ID for more precise detection
        
        return AMDArchitecture.UNKNOWN
    
    def get_environment_requirements(self, gpu_info: GPUInfo) -> EnvironmentRequirement:
        """Get environment requirements for a specific GPU"""
        if gpu_info.vendor == GPUVendor.NVIDIA:
            return self._get_nvidia_requirements(gpu_info)
        elif gpu_info.vendor == GPUVendor.AMD:
            return self._get_amd_requirements(gpu_info)
        else:
            raise ValueError(f"Unsupported GPU vendor: {gpu_info.vendor}")
    
    def _get_nvidia_requirements(self, gpu_info: GPUInfo) -> EnvironmentRequirement:
        """Get environment requirements for NVIDIA GPU"""
        return EnvironmentRequirement(
            gpu_info=gpu_info,
            python_env_type="venv",
            framework="pytorch",
            min_driver_version="526.0",
            required_packages=[
                "torch>=2.1.0",
                "torchvision>=0.16.0",
                "torchaudio>=2.1.0",
                "transformers>=4.35.0",
                "diffusers>=0.24.0",
                "accelerate>=0.25.0"
            ],
            os_requirements=["windows>=10", "linux>=ubuntu-20.04"],
            conflicts_with=["torch-directml", "torch-rocm"],
            validation_script="validate_nvidia.py"
        )
    
    def _get_amd_requirements(self, gpu_info: GPUInfo) -> EnvironmentRequirement:
        """Get environment requirements for AMD GPU"""
        arch = AMDArchitecture(gpu_info.architecture)
        
        if arch in [AMDArchitecture.RDNA1, AMDArchitecture.RDNA2]:
            # RDNA1/2 requires DirectML on Windows (no venv)
            return EnvironmentRequirement(
                gpu_info=gpu_info,
                python_env_type="native",
                framework="directml",
                min_driver_version="23.40.27.06",
                required_packages=[
                    "torch-directml>=0.2.0",
                    "onnxruntime-directml>=1.16.0",
                    "transformers>=4.35.0"
                ],
                os_requirements=["windows>=10"],
                conflicts_with=["torch", "torch-rocm"],
                validation_script="validate_directml.py"
            )
        
        elif arch in [AMDArchitecture.RDNA3, AMDArchitecture.RDNA4]:
            # RDNA3/4 can use ROCm on Linux
            return EnvironmentRequirement(
                gpu_info=gpu_info,
                python_env_type="venv",
                framework="rocm",
                min_driver_version="6.4.1",
                required_packages=[
                    "torch>=2.1.0+rocm6.0",
                    "torchvision>=0.16.0+rocm6.0",
                    "transformers>=4.35.0"
                ],
                os_requirements=["linux>=ubuntu-22.04"],
                conflicts_with=["torch-directml", "torch+cu118"],
                validation_script="validate_rocm.py"
            )
        
        else:
            # Fallback to DirectML for older AMD cards
            return self._get_amd_requirements(
                GPUInfo(**{**gpu_info.__dict__, "architecture": AMDArchitecture.RDNA2.value})
            )
    
    def plan_environments(self, gpus: Optional[List[GPUInfo]] = None) -> Dict[str, List[EnvironmentRequirement]]:
        """Plan environment setup for detected GPUs"""
        if gpus is None:
            gpus = self.detected_gpus
        
        # Group GPUs by environment requirements
        env_groups = {}
        
        for gpu in gpus:
            req = self.get_environment_requirements(gpu)
            
            # Create environment key based on framework and env type
            env_key = f"{req.framework}_{req.python_env_type}"
            
            if env_key not in env_groups:
                env_groups[env_key] = []
            
            env_groups[env_key].append(req)
        
        logger.info("Environment planning completed", groups=list(env_groups.keys()))
        return env_groups
    
    def detect_os_environment(self) -> Dict[str, Any]:
        """Detect operating system environment including WSL status"""
        os_info = {
            "system": self.system_os,
            "release": self.os_version,
            "machine": platform.machine(),
            "is_wsl": False,
            "wsl_version": None,
            "wsl_available": False
        }
        
        # Check for WSL if running on Linux
        if self.system_os == "linux":
            try:
                with open('/proc/version', 'r') as f:
                    version_info = f.read().lower()
                    if 'microsoft' in version_info or 'wsl' in version_info:
                        os_info["is_wsl"] = True
                        if 'wsl2' in version_info:
                            os_info["wsl_version"] = "2"
                        else:
                            os_info["wsl_version"] = "1"
                        logger.info("WSL environment detected", version=os_info["wsl_version"])
            except Exception as e:
                logger.debug("Could not detect WSL status", error=str(e))
        
        # Check if WSL is available on Windows
        elif self.system_os == "windows":
            try:
                result = subprocess.run(["wsl", "--list", "--quiet"], 
                                      capture_output=True, text=True, timeout=5)
                if result.returncode == 0 and result.stdout.strip():
                    os_info["wsl_available"] = True
                    distributions = [
                        dist.strip() for dist in result.stdout.split('\n') 
                        if dist.strip()
                    ]
                    os_info["wsl_distributions"] = distributions
                    logger.info("WSL available on Windows", distributions=distributions)
                else:
                    os_info["wsl_available"] = False
            except Exception as e:
                logger.debug("WSL not available or accessible", error=str(e))
                os_info["wsl_available"] = False
        
        return os_info

    def determine_environment_strategy(self, gpus: Optional[List[GPUInfo]] = None) -> Dict[str, Any]:
        """Determine optimal environment strategy based on detected GPUs and OS"""
        if gpus is None:
            gpus = self.detected_gpus
        
        os_info = self.detect_os_environment()
        
        # Categorize GPUs by environment requirements
        rdna1_rdna2_count = 0
        modern_gpu_count = 0  # RDNA3+, NVIDIA
        
        for gpu in gpus:
            if gpu.vendor == GPUVendor.AMD:
                arch = AMDArchitecture(gpu.architecture)
                if arch in [AMDArchitecture.RDNA1, AMDArchitecture.RDNA2]:
                    rdna1_rdna2_count += 1
                elif arch in [AMDArchitecture.RDNA3, AMDArchitecture.RDNA4]:
                    modern_gpu_count += 1
            elif gpu.vendor == GPUVendor.NVIDIA:
                modern_gpu_count += 1
        
        # Determine strategy
        if rdna1_rdna2_count > 0 and modern_gpu_count == 0:
            # Only RDNA1/2 - must use native Windows
            strategy = {
                "type": "native_windows_required",
                "reason": "RDNA1/2 GPUs require DirectML on native Windows",
                "environment": "system_python",
                "use_wsl": False,
                "use_venv": False
            }
        elif rdna1_rdna2_count > 0 and modern_gpu_count > 0:
            # Mixed setup
            strategy = {
                "type": "mixed_environment",
                "reason": "Mixed GPU setup requires separate environments",
                "environment": "separate_per_gpu_type",
                "use_wsl": os_info["wsl_available"],
                "use_venv": True
            }
        elif modern_gpu_count > 0:
            # Only modern GPUs - prefer venv/WSL
            strategy = {
                "type": "venv_preferred",
                "reason": "Modern GPUs support venv and WSL environments",
                "environment": "venv_wsl" if os_info["wsl_available"] else "venv",
                "use_wsl": os_info["wsl_available"],
                "use_venv": True
            }
        else:
            # No GPUs or unknown - default venv
            strategy = {
                "type": "default_venv",
                "reason": "No specific GPU requirements",
                "environment": "venv",
                "use_wsl": False,
                "use_venv": True
            }
        
        strategy["os_info"] = os_info
        strategy["gpu_breakdown"] = {
            "rdna1_rdna2": rdna1_rdna2_count,
            "modern_gpus": modern_gpu_count,
            "total": len(gpus)
        }
        
        logger.info("Environment strategy determined", 
                   strategy_type=strategy["type"],
                   rdna1_rdna2=rdna1_rdna2_count,
                   modern_gpus=modern_gpu_count)
        
        return strategy
