"""
Resource Manager
Monitors and manages local system resources (CPU, Memory, GPU)
Provides resource allocation and capacity planning for workers
"""

import psutil
import logging
from typing import Dict, List, Optional, Any
from datetime import datetime
import structlog

logger = structlog.get_logger(__name__)


class ResourceManager:
    """
    Manages local system resources and provides allocation capabilities
    Monitors CPU, memory, GPU resources and enforces limits
    """
    
    def __init__(self, config: Optional[Dict[str, Any]] = None):
        """Initialize resource manager"""
        self.config = config or {}
        self.cpu_count = psutil.cpu_count()
        self.memory_total = psutil.virtual_memory().total
        self.gpu_devices = []
        self.system_resources = {}
        
        # Resource tracking
        self.allocated_cpu = 0.0
        self.allocated_memory = 0
        self.allocated_gpu_memory = {}
        self.worker_allocations = {}
        
        logger.info("ResourceManager initialized")
    
    def detect_resources(self):
        """Detect available system resources - public method for external calling"""
        return self.detect_system_resources()
    
    def detect_system_resources(self) -> Dict[str, Any]:
        """Detect and catalog all available system resources"""
        resources = {
            'cpu': {
                'cores': self.cpu_count,
                'logical_cores': psutil.cpu_count(logical=True),
                'frequency': psutil.cpu_freq()._asdict() if psutil.cpu_freq() else None,
                'architecture': 'x86_64'  # TODO: Detect actual architecture
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
        
        # Detect disk resources
        try:
            for partition in psutil.disk_partitions():
                if partition.device:
                    usage = psutil.disk_usage(partition.mountpoint)
                    resources['disk'][partition.device] = {
                        'mountpoint': partition.mountpoint,
                        'fstype': partition.fstype,
                        'total': usage.total,
                        'used': usage.used,
                        'free': usage.free
                    }
        except Exception as e:
            logger.warning(f"Failed to detect disk resources: {e}")
        
        # Detect network interfaces
        try:
            for interface, addresses in psutil.net_if_addrs().items():
                resources['network'][interface] = [
                    {
                        'family': addr.family.name,
                        'address': addr.address,
                        'netmask': addr.netmask,
                        'broadcast': addr.broadcast
                    }
                    for addr in addresses
                ]
        except Exception as e:
            logger.warning(f"Failed to detect network resources: {e}")
        
        # Detect GPU resources
        try:
            resources['gpu'] = self.get_gpu_info()
        except Exception as e:
            logger.warning(f"Failed to detect GPU resources: {e}")
        
        # Cache the detected resources
        self.system_resources = resources
        logger.info(f"Detected system resources: {len(resources['gpu'])} GPUs, {self.cpu_count} CPU cores, {self.memory_total // (1024**3)}GB RAM")
        
        return resources
    
    def get_current_usage(self) -> Dict[str, Any]:
        """Get current resource utilization"""
        try:
            # Get current CPU usage
            cpu_percent = psutil.cpu_percent(interval=1)
            
            # Get current memory usage
            memory = psutil.virtual_memory()
            
            # Get current disk usage for main disk
            disk_usage = {}
            try:
                main_disk = psutil.disk_usage('/')
                disk_usage = {
                    'total': main_disk.total,
                    'used': main_disk.used,
                    'free': main_disk.free,
                    'percent': (main_disk.used / main_disk.total) * 100
                }
            except:
                # Windows fallback
                try:
                    main_disk = psutil.disk_usage('C:\\')
                    disk_usage = {
                        'total': main_disk.total,
                        'used': main_disk.used,
                        'free': main_disk.free,
                        'percent': (main_disk.used / main_disk.total) * 100
                    }
                except Exception as e:
                    logger.warning(f"Failed to get disk usage: {e}")
                    disk_usage = {'total': 0, 'used': 0, 'free': 0, 'percent': 0}
            
            # Get network I/O
            network_io = psutil.net_io_counters()
            
            usage_data = {
                'timestamp': datetime.now().isoformat(),
                'cpu_usage': cpu_percent,
                'memory_usage': memory.used,
                'memory_total': memory.total,
                'memory_percent': memory.percent,
                'disk_usage': disk_usage['used'],
                'disk_total': disk_usage['total'],
                'disk_percent': disk_usage['percent'],
                'network_rx': network_io.bytes_recv if network_io else 0,
                'network_tx': network_io.bytes_sent if network_io else 0,
                'gpu_memory_usage': {},
                'gpu_memory_total': {}
            }
            
            # Add GPU usage if available
            try:
                gpu_usage = self._get_gpu_usage()
                usage_data['gpu_memory_usage'] = gpu_usage.get('memory_usage', {})
                usage_data['gpu_memory_total'] = gpu_usage.get('memory_total', {})
            except Exception as e:
                logger.debug(f"GPU usage not available: {e}")
            
            return usage_data
            
        except Exception as e:
            logger.error(f"Failed to get current resource usage: {e}")
            return {
                'timestamp': datetime.now().isoformat(),
                'cpu_usage': 0.0,
                'memory_usage': 0,
                'memory_total': self.memory_total,
                'disk_usage': 0,
                'disk_total': 0,
                'network_rx': 0,
                'network_tx': 0,
                'gpu_memory_usage': {},
                'gpu_memory_total': {}
            }
    
    def allocate_resources(self, worker_id: str, requirements: Dict[str, Any]) -> bool:
        """Allocate resources for a worker"""
        try:
            # Check if resources are available
            if not self.can_allocate(requirements):
                logger.warning(f"Cannot allocate resources for worker {worker_id}: insufficient resources")
                return False
            
            # Allocate CPU
            cpu_cores = requirements.get('cpu_cores', 1)
            self.allocated_cpu += cpu_cores
            
            # Allocate memory
            memory_mb = requirements.get('memory_mb', 512)
            self.allocated_memory += memory_mb * 1024 * 1024  # Convert to bytes
            
            # Allocate GPU memory if requested
            gpu_memory = requirements.get('gpu_memory', {})
            for gpu_id, memory_mb in gpu_memory.items():
                if gpu_id not in self.allocated_gpu_memory:
                    self.allocated_gpu_memory[gpu_id] = 0
                self.allocated_gpu_memory[gpu_id] += memory_mb * 1024 * 1024  # Convert to bytes
            
            # Track allocation for this worker
            if not hasattr(self, 'worker_allocations'):
                self.worker_allocations = {}
            
            self.worker_allocations[worker_id] = requirements
            
            logger.info(f"Allocated resources for worker {worker_id}: {requirements}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to allocate resources for worker {worker_id}: {e}")
            return False
    
    def release_resources(self, worker_id: str):
        """Release resources from a worker"""
        try:
            if not hasattr(self, 'worker_allocations'):
                return
            
            if worker_id not in self.worker_allocations:
                logger.warning(f"No resource allocation found for worker {worker_id}")
                return
            
            requirements = self.worker_allocations[worker_id]
            
            # Release CPU
            cpu_cores = requirements.get('cpu_ores', 1)
            self.allocated_cpu = max(0, self.allocated_cpu - cpu_cores)
            
            # Release memory
            memory_mb = requirements.get('memory_mb', 512)
            self.allocated_memory = max(0, self.allocated_memory - (memory_mb * 1024 * 1024))
            
            # Release GPU memory
            gpu_memory = requirements.get('gpu_memory', {})
            for gpu_id, memory_mb in gpu_memory.items():
                if gpu_id in self.allocated_gpu_memory:
                    self.allocated_gpu_memory[gpu_id] = max(
                        0, 
                        self.allocated_gpu_memory[gpu_id] - (memory_mb * 1024 * 1024)
                    )
            
            # Remove allocation tracking
            del self.worker_allocations[worker_id]
            
            logger.info(f"Released resources for worker {worker_id}")
            
        except Exception as e:
            logger.error(f"Failed to release resources for worker {worker_id}: {e}")
    
    def can_allocate(self, requirements: Dict[str, Any]) -> bool:
        """Check if resources can be allocated"""
        try:
            # Check CPU availability
            cpu_cores = requirements.get('cpu_cores', 1)
            if self.allocated_cpu + cpu_cores > self.cpu_count:
                return False
            
            # Check memory availability
            memory_mb = requirements.get('memory_mb', 512)
            memory_bytes = memory_mb * 1024 * 1024
            available_memory = psutil.virtual_memory().available
            if self.allocated_memory + memory_bytes > available_memory:
                return False
            
            # Check GPU memory if requested
            gpu_memory = requirements.get('gpu_memory', {})
            for gpu_id, memory_mb in gpu_memory.items():
                memory_bytes = memory_mb * 1024 * 1024
                current_allocated = self.allocated_gpu_memory.get(gpu_id, 0)
                
                # TODO: Get actual GPU memory total from detection
                gpu_total = 8 * 1024 * 1024 * 1024  # Default 8GB, should be detected
                
                if current_allocated + memory_bytes > gpu_total:
                    return False
            
            return True
            
        except Exception as e:
            logger.error(f"Failed to check resource availability: {e}")
            return False
    
    def get_gpu_info(self) -> List[Dict[str, Any]]:
        """Get detailed GPU information"""
        gpu_devices = []
        
        # Try NVIDIA GPU detection
        try:
            import pynvml
            pynvml.nvmlInit()
            device_count = pynvml.nvmlDeviceGetCount()
            
            for i in range(device_count):
                handle = pynvml.nvmlDeviceGetHandleByIndex(i)
                name = pynvml.nvmlDeviceGetName(handle).decode('utf-8')
                memory_info = pynvml.nvmlDeviceGetMemoryInfo(handle)
                
                gpu_devices.append({
                    'id': i,
                    'name': name,
                    'vendor': 'NVIDIA',
                    'memory_total': memory_info.total,
                    'memory_used': memory_info.used,
                    'memory_free': memory_info.free,
                    'driver_version': pynvml.nvmlSystemGetDriverVersion().decode('utf-8')
                })
                
            logger.info(f"Detected {len(gpu_devices)} NVIDIA GPU(s)")
            
        except Exception as e:
            logger.debug(f"NVIDIA GPU detection failed: {e}")
          # Try AMD GPU detection using WMI (Windows) and other methods
        try:
            amd_gpus = self._detect_amd_gpus()
            gpu_devices.extend(amd_gpus)
            if amd_gpus:
                logger.info(f"Detected {len(amd_gpus)} AMD GPU(s)")
        except Exception as e:
            logger.debug(f"AMD GPU detection failed: {e}")
        
        # If no GPUs detected, return empty list
        if not gpu_devices:
            logger.info("No GPUs detected")
        
        return gpu_devices
    
    def _get_gpu_usage(self) -> Dict[str, Dict[str, int]]:
        """Get current GPU memory usage"""
        usage = {
            'memory_usage': {},
            'memory_total': {}
        }
        
        try:
            import pynvml
            pynvml.nvmlInit()
            device_count = pynvml.nvmlDeviceGetCount()
            
            for i in range(device_count):
                handle = pynvml.nvmlDeviceGetHandleByIndex(i)
                memory_info = pynvml.nvmlDeviceGetMemoryInfo(handle)
                
                usage['memory_usage'][str(i)] = memory_info.used
                usage['memory_total'][str(i)] = memory_info.total
                
        except Exception as e:
            logger.debug(f"Failed to get GPU usage: {e}")
        
        return usage
    
    def _detect_amd_gpus(self) -> List[Dict[str, Any]]:
        """Detect AMD GPUs using WMI and other Windows methods"""
        amd_gpus = []
        
        # Method 1: Try WMI on Windows
        try:
            # Check if we're on Windows first
            import platform
            if platform.system() != 'Windows':
                logger.debug("Non-Windows system, skipping WMI AMD GPU detection")
                return amd_gpus
                
            import wmi
            c = wmi.WMI()
            gpu_id = 0
            
            for gpu in c.Win32_VideoController():
                if gpu.Name and ('AMD' in gpu.Name or 'Radeon' in gpu.Name):
                    # Convert adapter RAM to bytes (WMI returns it in bytes)
                    memory_total = int(gpu.AdapterRAM or 0)
                    
                    # WMI often reports incorrect memory for AMD cards, use model-based defaults
                    if memory_total < 1024 * 1024 * 1024:  # Less than 1GB, probably wrong
                        if 'RX 6800' in gpu.Name:
                            memory_total = 16 * 1024 * 1024 * 1024  # 16GB for RX 6800/6800 XT
                        else:
                            memory_total = 8 * 1024 * 1024 * 1024   # Default 8GB for other AMD cards
                    
                    amd_gpus.append({
                        'id': gpu_id,
                        'name': gpu.Name.strip(),
                        'vendor': 'AMD',
                        'memory_total': memory_total,
                        'memory_used': 0,  # Would need ROCm tools for actual usage
                        'memory_free': memory_total,
                        'driver_version': gpu.DriverVersion or 'Unknown',
                        'device_id': gpu.DeviceID or f"amd_gpu_{gpu_id}",
                        'detection_method': 'WMI',
                        'status': 'available'
                    })
                    gpu_id += 1
                    logger.debug(f"Detected AMD GPU: {gpu.Name} with {memory_total // (1024**3)}GB VRAM")
                    
        except ImportError:
            logger.debug("WMI module not available for AMD GPU detection")
        except Exception as e:
            logger.debug(f"WMI AMD GPU detection failed: {e}")
        
        # Method 2: Try PowerShell fallback if WMI didn't work or failed
        if not amd_gpus:
            try:
                import subprocess
                import json
                
                ps_command = """
                Get-WmiObject -Class Win32_VideoController | 
                Where-Object {$_.Name -like '*AMD*' -or $_.Name -like '*Radeon*'} | 
                Select-Object Name, AdapterRAM, DriverVersion, DeviceID | 
                ConvertTo-Json
                """
                
                result = subprocess.run([
                    'powershell', '-Command', ps_command
                ], capture_output=True, text=True, timeout=15)
                
                if result.returncode == 0 and result.stdout.strip():
                    gpu_data = json.loads(result.stdout)
                    
                    # Handle both single GPU (dict) and multiple GPUs (list)
                    if isinstance(gpu_data, dict):
                        gpu_data = [gpu_data]
                    
                    for i, gpu in enumerate(gpu_data):
                        memory_total = int(gpu.get('AdapterRAM') or 0)
                        
                        # Fix incorrect memory reporting
                        if memory_total < 1024 * 1024 * 1024:  # Less than 1GB
                            if 'RX 6800' in gpu.get('Name', ''):
                                memory_total = 16 * 1024 * 1024 * 1024  # 16GB for RX 6800/6800 XT
                            else:
                                memory_total = 8 * 1024 * 1024 * 1024   # Default 8GB
                        
                        amd_gpus.append({
                            'id': i,
                            'name': gpu.get('Name', 'Unknown AMD GPU').strip(),
                            'vendor': 'AMD',
                            'memory_total': memory_total,
                            'memory_used': 0,
                            'memory_free': memory_total,
                            'driver_version': gpu.get('DriverVersion', 'Unknown'),
                            'device_id': gpu.get('DeviceID', f"amd_gpu_{i}"),
                            'detection_method': 'PowerShell_WMI',
                            'status': 'available'
                        })
                        logger.debug(f"Detected AMD GPU via PowerShell: {gpu.get('Name')} with {memory_total // (1024**3)}GB VRAM")
                        
            except Exception as e:
                logger.debug(f"PowerShell AMD GPU detection failed: {e}")
        
        logger.info(f"AMD GPU detection completed: found {len(amd_gpus)} devices")
        return amd_gpus
    
    def monitor_resources(self) -> Dict[str, Any]:
        """Continuous resource monitoring for metrics collection"""
        # This method is called by the monitoring loop
        # Return current usage for storage/reporting
        return self.get_current_usage()
