# resource_manager.py

"""
Resource Manager
Monitors and manages local system resources (CPU, Memory, GPU)
Provides resource allocation and capacity planning for workers
"""

import platform
import psutil
import logging
import structlog
import subprocess
import json
import ctypes
from ctypes import wintypes
from datetime import datetime
from typing import Dict, List, Optional, Any

logger = structlog.get_logger(__name__)

# ── BEGIN: Fallback GUID definition ──
try:
    from ctypes import GUID  # type: ignore
except (ImportError, AttributeError):
    class GUID(ctypes.Structure):  # type: ignore
        _fields_ = [
            ('Data1', wintypes.DWORD),
            ('Data2', wintypes.WORD),
            ('Data3', wintypes.WORD),
            ('Data4', wintypes.BYTE * 8),
        ]
# ── END: Fallback GUID definition ──


# DXGI GUIDs for factory and adapter enumeration
# GUID for IDXGIFactory1: {770aae78-f26f-4dba-a829-253c83d1b387}
try:
    # Try to use string parsing if available
    IID_IDXGIFactory1 = GUID('{770aae78-f26f-4dba-a829-253c83d1b387}')
except (TypeError, ValueError):
    # Fallback: construct manually from components
    IID_IDXGIFactory1 = GUID()
    IID_IDXGIFactory1.Data1 = 0x770aae78
    IID_IDXGIFactory1.Data2 = 0xf26f
    IID_IDXGIFactory1.Data3 = 0x4dba
    IID_IDXGIFactory1.Data4 = (wintypes.BYTE * 8)(0xa8, 0x29, 0x25, 0x3c, 0x83, 0xd1, 0xb3, 0x87)

# Vendor IDs
VENDOR_ID_NVIDIA = 0x10DE
VENDOR_ID_AMD    = 0x1002
VENDOR_ID_INTEL  = 0x8086


class ResourceManager:
    """
    Manages local system resources and provides allocation capabilities.
    Monitors CPU, memory, GPU resources and enforces limits.
    """

    def __init__(self, config: Optional[Dict[str, Any]] = None):
        """Initialize resource manager"""
        self.config = config or {}
        self.cpu_physical_cores = psutil.cpu_count(logical=False) or 1
        self.cpu_logical_cores  = psutil.cpu_count(logical=True) or 1
        self.cpu_architecture   = platform.machine()
        self.memory_total       = psutil.virtual_memory().total
        self.system_resources: Dict[str, Any] = {}

        # Resource tracking
        self.allocated_cpu: float = 0.0
        self.allocated_memory: int = 0
        self.allocated_gpu_memory: Dict[int, int] = {}  # key = gpu id, value = bytes
        self.worker_allocations: Dict[str, Dict[str, Any]] = {}

        logger.info("ResourceManager initialized",
                    cpu_cores=self.cpu_physical_cores,
                    logical_cores=self.cpu_logical_cores,
                    architecture=self.cpu_architecture,
                    total_memory=self.memory_total)

    def detect_resources(self) -> Dict[str, Any]:
        """Public method to detect all system resources"""
        return self.detect_system_resources()

    def detect_system_resources(self) -> Dict[str, Any]:
        """Detect and catalog all available system resources"""
        resources: Dict[str, Any] = {
            'cpu': {
                'physical_cores': self.cpu_physical_cores,
                'logical_cores': self.cpu_logical_cores,
                'architecture': self.cpu_architecture,
                'frequency': None
            },
            'memory': {
                'total': self.memory_total,
                'available': psutil.virtual_memory().available,
                'swap_total': psutil.swap_memory().total
            },
            'disk': {},
            'network': {},
            'gpu': []
        }

        # CPU frequency (may be None on some platforms)
        try:
            freq = psutil.cpu_freq()
            resources['cpu']['frequency'] = freq._asdict() if freq else None
        except Exception as e:
            logger.warning("Unable to get CPU frequency", error=e)

        # Detect disk partitions
        try:
            for partition in psutil.disk_partitions():
                try:
                    usage = psutil.disk_usage(partition.mountpoint)
                    resources['disk'][partition.device] = {
                        'mountpoint': partition.mountpoint,
                        'fstype': partition.fstype,
                        'total': usage.total,
                        'used': usage.used,
                        'free': usage.free,
                        'percent': usage.percent
                    }
                except Exception:
                    # Skip inaccessible mountpoints
                    continue
        except Exception as e:
            logger.warning("Failed to detect disk resources", error=e)

        # Detect network interfaces
        try:
            for iface, addrs in psutil.net_if_addrs().items():
                resources['network'][iface] = []
                for addr in addrs:
                    fam = getattr(addr.family, 'name', str(addr.family))
                    resources['network'][iface].append({
                        'family': fam,
                        'address': addr.address,
                        'netmask': addr.netmask,
                        'broadcast': addr.broadcast
                    })
        except Exception as e:
            logger.warning("Failed to detect network resources", error=e)

        # Detect GPU resources
        try:
            gpu_list = self.get_gpu_info()
            resources['gpu'] = gpu_list
        except Exception as e:
            logger.warning("Failed to detect GPU resources", error=e)

        # Cache
        self.system_resources = resources
        logger.info(
            "Detected system resources",
            cpu=f"{self.cpu_physical_cores}C/{self.cpu_logical_cores}L",
            memory_gb=f"{self.memory_total // (1024**3)}GB",
            gpus=len(resources['gpu'])
        )
        return resources

    def get_current_usage(self) -> Dict[str, Any]:
        """Get current resource utilization (CPU, memory, disk, network, GPU)"""
        try:
            # CPU usage
            cpu_percent = psutil.cpu_percent(interval=1)

            # Memory usage
            mem = psutil.virtual_memory()

            # Disk usage for root '/'
            disk_usage = {}
            try:
                root = '/'
                du = psutil.disk_usage(root)
                disk_usage = {
                    'total': du.total,
                    'used': du.used,
                    'free': du.free,
                    'percent': du.percent
                }
            except Exception:
                # Windows fallback: 'C:\\'
                try:
                    du = psutil.disk_usage('C:\\')
                    disk_usage = {
                        'total': du.total,
                        'used': du.used,
                        'free': du.free,
                        'percent': du.percent
                    }
                except Exception as e:
                    logger.warning("Failed to get disk usage", error=e)
                    disk_usage = {
                        'total': 0, 'used': 0, 'free': 0, 'percent': 0.0
                    }

            # Network I/O
            net_io = psutil.net_io_counters()

            usage_data: Dict[str, Any] = {
                'timestamp': datetime.now().isoformat(),
                'cpu_percent': cpu_percent,
                'memory_used': mem.used,
                'memory_total': mem.total,
                'memory_percent': mem.percent,
                'disk_used': disk_usage.get('used', 0),
                'disk_total': disk_usage.get('total', 0),
                'disk_percent': disk_usage.get('percent', 0.0),
                'network_rx': getattr(net_io, 'bytes_recv', 0),
                'network_tx': getattr(net_io, 'bytes_sent', 0),
                'gpu_memory_usage': {},
                'gpu_memory_total': {}
            }

            # GPU memory usage
            try:
                gpu_usage = self._get_gpu_usage()
                usage_data['gpu_memory_usage'] = gpu_usage.get('memory_usage', {})
                usage_data['gpu_memory_total'] = gpu_usage.get('memory_total', {})
            except Exception as e:
                logger.debug("GPU usage not available", error=e)

            return usage_data

        except Exception as e:
            logger.error("Failed to get current resource usage", error=e)
            return {
                'timestamp': datetime.now().isoformat(),
                'cpu_percent': 0.0,
                'memory_used': 0,
                'memory_total': self.memory_total,
                'memory_percent': 0.0,
                'disk_used': 0,
                'disk_total': 0,
                'disk_percent': 0.0,
                'network_rx': 0,
                'network_tx': 0,
                'gpu_memory_usage': {},
                'gpu_memory_total': {}
            }

    def allocate_resources(self, worker_id: str, requirements: Dict[str, Any]) -> bool:
        """
        Allocate resources for a worker.
        requirements keys:
         - cpu_cores: int
         - memory_mb: int
         - gpu_memory: Dict[int, int]  # {gpu_id: mb}
        """
        try:
            if not self.can_allocate(requirements):
                logger.warning(
                    "Cannot allocate resources: insufficient resources",
                    worker_id=worker_id,
                    requirements=requirements
                )
                return False

            # Allocate CPU
            cpu_cores = requirements.get('cpu_cores', 1)
            self.allocated_cpu += cpu_cores

            # Allocate memory (convert MB → bytes)
            mem_mb = requirements.get('memory_mb', 512)
            mem_bytes = mem_mb * 1024 * 1024
            self.allocated_memory += mem_bytes

            # Allocate GPU memory
            gpu_reqs: Dict[int, int] = requirements.get('gpu_memory', {})
            for gpu_id, mb in gpu_reqs.items():
                bytes_req = mb * 1024 * 1024
                self.allocated_gpu_memory[gpu_id] = (
                    self.allocated_gpu_memory.get(gpu_id, 0) + bytes_req
                )

            # Track per-worker requirements
            self.worker_allocations[worker_id] = {
                'cpu_cores': cpu_cores,
                'memory_mb': mem_mb,
                'gpu_memory': gpu_reqs.copy()
            }

            logger.info(
                "Allocated resources for worker",
                worker_id=worker_id,
                requirements=requirements
            )
            return True

        except Exception as e:
            logger.error("Failed to allocate resources", error=e, worker_id=worker_id)
            return False

    def release_resources(self, worker_id: str):
        """
        Release resources previously allocated to a worker.
        """
        try:
            if worker_id not in self.worker_allocations:
                logger.warning("No resources to release for worker", worker_id=worker_id)
                return

            reqs = self.worker_allocations[worker_id]

            # Release CPU
            cpu_cores = reqs.get('cpu_cores', 1)
            self.allocated_cpu = max(0.0, self.allocated_cpu - cpu_cores)

            # Release memory
            mem_mb = reqs.get('memory_mb', 512)
            mem_bytes = mem_mb * 1024 * 1024
            self.allocated_memory = max(0, self.allocated_memory - mem_bytes)

            # Release GPU memory
            gpu_reqs: Dict[int, int] = reqs.get('gpu_memory', {})
            for gpu_id, mb in gpu_reqs.items():
                bytes_req = mb * 1024 * 1024
                if gpu_id in self.allocated_gpu_memory:
                    self.allocated_gpu_memory[gpu_id] = max(
                        0,
                        self.allocated_gpu_memory[gpu_id] - bytes_req
                    )

            # Remove the worker from tracking
            del self.worker_allocations[worker_id]
            logger.info("Released resources for worker", worker_id=worker_id)

        except Exception as e:
            logger.error("Failed to release resources", error=e, worker_id=worker_id)

    def can_allocate(self, requirements: Dict[str, Any]) -> bool:
        """
        Check if the requested resources can be allocated.
        """
        try:
            # 1) CPU
            req_cpu = requirements.get('cpu_cores', 1)
            if (self.allocated_cpu + req_cpu) > self.cpu_physical_cores:
                return False

            # 2) Memory
            req_mem_mb = requirements.get('memory_mb', 512)
            req_mem_bytes = req_mem_mb * 1024 * 1024
            available_mem = psutil.virtual_memory().available
            if (self.allocated_memory + req_mem_bytes) > available_mem:
                return False

            # 3) GPU memory
            gpu_reqs: Dict[int, int] = requirements.get('gpu_memory', {})
            # Build a local map of detected GPU totals
            gpu_totals: Dict[int, int] = {
                gpu['id']: gpu['memory_total']
                for gpu in self.system_resources.get('gpu', [])
            }

            for gpu_id, mb in gpu_reqs.items():
                req_bytes = mb * 1024 * 1024
                already = self.allocated_gpu_memory.get(gpu_id, 0)
                total = gpu_totals.get(gpu_id, 0)
                if total == 0:
                    # If GPU not detected or no total known, deny allocation
                    return False
                if (already + req_bytes) > total:
                    return False

            return True

        except Exception as e:
            logger.error("Failed to check resource availability", error=e)
            return False

    def get_gpu_info(self) -> List[Dict[str, Any]]:
        """
        Get detailed GPU information for NVIDIA, AMD, and Intel.
        Attempts:
         1) DXGI enumeration (all vendors)
         2) NVIDIA-specific via NVML for driver versions & usage
         3) Fallback WMI for AMD/Intel if DXGI not available
        """
        gpu_list: List[Dict[str, Any]] = []

        # Try DXGI enumeration first
        try:
            dxgi_gpus = self._detect_gpus_dxgi()
            if dxgi_gpus:
                gpu_list.extend(dxgi_gpus)
        except Exception as e:
            logger.debug("DXGI GPU detection failed", error=e)        # If any NVIDIA GPU was found, enrich via NVML
        try:
            try:
                import pynvml  # type: ignore
            except ImportError:                
                pynvml = None
                
            if pynvml:
                pynvml.nvmlInit()
                driver_ver = pynvml.nvmlSystemGetDriverVersion().decode('utf-8')
                count = pynvml.nvmlDeviceGetCount()

                for idx in range(count):
                    handle = pynvml.nvmlDeviceGetHandleByIndex(idx)
                    name = pynvml.nvmlDeviceGetName(handle).decode('utf-8')
                    mem_info = pynvml.nvmlDeviceGetMemoryInfo(handle)

                    # Check if already in gpu_list by vendor + name
                    matched = False
                    for gpu in gpu_list:
                        if gpu['vendor'] == 'NVIDIA' and gpu['name'] == name:
                            gpu['memory_total'] = mem_info.total
                            gpu['memory_free']  = mem_info.free
                            gpu['memory_used']  = mem_info.used
                            gpu['driver_version'] = driver_ver
                            matched = True
                            break

                    if not matched:
                        # Add new NVIDIA entry if DXGI missed it
                        gpu_list.append({
                            'id': idx,
                            'name': name,
                            'vendor': 'NVIDIA',
                            'memory_total': mem_info.total,
                            'memory_used': mem_info.used,
                            'memory_free': mem_info.free,
                            'driver_version': driver_ver
                        })

                pynvml.nvmlShutdown()
        except Exception as e:
            logger.debug("NVIDIA NVML enrichment failed or NVML not installed", error=e)

        # If no GPUs detected so far, try fallback WMI for AMD/Intel
        if not gpu_list:
            try:
                gpu_list = self._detect_gpus_wmi()
            except Exception as e:
                logger.debug("WMI GPU detection failed", error=e)

        if not gpu_list:
            logger.info("No GPUs detected on this system")

        # Reassign consistent IDs (0...N-1)
        for new_id, gpu in enumerate(gpu_list):
            gpu['id'] = new_id

        return gpu_list

    def _detect_gpus_dxgi(self) -> List[Dict[str, Any]]:
        """
        Enumerate all DXGI adapters, return vendor, name, VRAM.
        """
        gpus: List[Dict[str, Any]] = []
        try:
            dxgi = ctypes.WinDLL('dxgi.dll')
            CreateFactory = dxgi.CreateDXGIFactory1
            CreateFactory.argtypes = [
                ctypes.POINTER(GUID),
                ctypes.POINTER(ctypes.c_void_p)
            ]
            CreateFactory.restype = ctypes.HRESULT

            pFactory = ctypes.c_void_p()
            hr = CreateFactory(ctypes.byref(IID_IDXGIFactory1), ctypes.byref(pFactory))
            if hr != 0 or not pFactory.value:
                return gpus

            # IDXGIFactory1 vtable index 10 → EnumAdapters1
            factory_vtable = ctypes.POINTER(ctypes.c_void_p).from_address(int(pFactory.value))
            EnumAdapters1_ptr = ctypes.cast(
                factory_vtable[10],
                ctypes.CFUNCTYPE(
                    ctypes.HRESULT,
                    ctypes.c_void_p,
                    wintypes.UINT,
                    ctypes.POINTER(ctypes.c_void_p)
                )
            )

            adapter_index = 0
            while True:
                pAdapter = ctypes.c_void_p()
                hr_enum = EnumAdapters1_ptr(pFactory, adapter_index, ctypes.byref(pAdapter))
                if hr_enum != 0:  # DXGI_ERROR_NOT_FOUND or other failure
                    break

                if not pAdapter.value:
                    adapter_index += 1
                    continue

                # IDXGIAdapter1 vtable index 5 → GetDesc1
                adapter_vtable = ctypes.POINTER(ctypes.c_void_p).from_address(int(pAdapter.value))
                GetDesc1_ptr = ctypes.cast(
                    adapter_vtable[5],
                    ctypes.CFUNCTYPE(
                        ctypes.HRESULT,
                        ctypes.c_void_p,
                        ctypes.POINTER(self._DXGI_ADAPTER_DESC)
                    )
                )

                desc = self._DXGI_ADAPTER_DESC()
                hr_desc = GetDesc1_ptr(pAdapter, ctypes.byref(desc))
                if hr_desc == 0:
                    name = desc.Description.strip()
                    vendor = desc.VendorId
                    vram = desc.DedicatedVideoMemory

                    if vendor == VENDOR_ID_NVIDIA:
                        vend_str = 'NVIDIA'
                    elif vendor == VENDOR_ID_AMD:
                        vend_str = 'AMD'
                    elif vendor == VENDOR_ID_INTEL:
                        vend_str = 'Intel'
                    else:
                        vend_str = f"Unknown(0x{vendor:04x})"

                    gpus.append({
                        'id': adapter_index,
                        'name': name,
                        'vendor': vend_str,
                        'memory_total': vram,
                        'memory_used': 0,
                        'memory_free': vram,
                        'driver_version': 'Unknown'
                    })

                # Release adapter
                Release = ctypes.CFUNCTYPE(ctypes.HRESULT, ctypes.c_void_p)
                release_adapter = ctypes.cast(adapter_vtable[2], Release)
                release_adapter(pAdapter)

                adapter_index += 1

            # Release factory
            Release = ctypes.CFUNCTYPE(ctypes.HRESULT, ctypes.c_void_p)
            release_factory = ctypes.cast(factory_vtable[2], Release)
            release_factory(pFactory)

        except Exception as e:
            logger.debug("DXGI enumeration error", error=e)

        return gpus

    class _DXGI_ADAPTER_DESC(ctypes.Structure):
        _fields_ = [
            ('Description', wintypes.WCHAR * 128),
            ('VendorId',   wintypes.UINT),
            ('DeviceId',   wintypes.UINT),
            ('SubSysId',   wintypes.UINT),
            ('Revision',   wintypes.UINT),
            ('DedicatedVideoMemory', ctypes.c_size_t),
            ('DedicatedSystemMemory', ctypes.c_size_t),
            ('SharedSystemMemory', ctypes.c_size_t),
            ('AdapterLuid', ctypes.c_ulonglong * 2),
        ]

    def _detect_gpus_wmi(self) -> List[Dict[str, Any]]:
        """
        Fallback GPU detection using WMI (for AMD/Intel if DXGI fails).
        """
        gpus: List[Dict[str, Any]] = []
        try:
            try:
                import wmi  # type: ignore
            except ImportError:
                wmi = None

            if platform.system() != 'Windows' or wmi is None:
                return gpus

            c = wmi.WMI()
            idx = 0
            for gpu in c.Win32_VideoController():
                name = gpu.Name or "Unknown GPU"
                vendor_lower = name.lower()
                if 'nvidia' in vendor_lower:
                    vend_str = 'NVIDIA'
                elif 'amd' in vendor_lower or 'radeon' in vendor_lower:
                    vend_str = 'AMD'
                elif 'intel' in vendor_lower:
                    vend_str = 'Intel'
                else:
                    continue  # skip non-GPU or unknown vendors

                # WMI reports AdapterRAM in bytes
                mem = int(gpu.AdapterRAM or 0)
                # If WMI says <1GB, treat as unknown
                if mem < (1 * 1024**3):
                    mem = 0

                gpus.append({
                    'id': idx,
                    'name': name.strip(),
                    'vendor': vend_str,
                    'memory_total': mem,
                    'memory_used': 0,
                    'memory_free': mem,
                    'driver_version': gpu.DriverVersion or 'Unknown'
                })
                idx += 1

        except Exception as e:
            logger.debug("WMI GPU detection error", error=e)

        return gpus

    def _get_gpu_usage(self) -> Dict[str, Dict[int, int]]:
        """
        Returns per-GPU memory usage and total (only NVIDIA via NVML is populated).
        All other vendors default to 0 or total = what was detected.
        """
        usage: Dict[str, Dict[int, int]] = {
            'memory_usage': {},
            'memory_total': {}
        }

        # Use system_resources cache for total values
        for gpu in self.system_resources.get('gpu', []):
            idx = gpu['id']
            usage['memory_total'][idx] = gpu.get('memory_total', 0)
            usage['memory_usage'][idx] = 0        # Enrich NVIDIA usage
        try:
            try:
                import pynvml  # type: ignore
            except ImportError:
                pynvml = None

            if pynvml:
                pynvml.nvmlInit()
                count = pynvml.nvmlDeviceGetCount()
                for i in range(count):
                    handle = pynvml.nvmlDeviceGetHandleByIndex(i)
                    mem_info = pynvml.nvmlDeviceGetMemoryInfo(handle)
                    usage['memory_total'][i] = int(mem_info.total)
                    usage['memory_usage'][i] = int(mem_info.used)
                pynvml.nvmlShutdown()
        except Exception:
            # If NVML missing or no NVIDIA, leave as-is
            pass

        return usage

    def monitor_resources(self) -> Dict[str, Any]:
        """
        Continuous resource monitoring for metrics collection
        (simply returns current usage; can be called in a loop)
        """
        return self.get_current_usage()
