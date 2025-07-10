# dml_patch.py
# DirectML patch to intercept CUDA calls and redirect them to DirectML
# Enhanced version for multi-GPU support across all 5 AMD GPUs

import threading
import logging
from typing import Optional

# Try to import torch modules with error handling for AMD SMI issues
try:
    import torch
    import torch_directml as dml
    TORCH_AVAILABLE = True
except (ImportError, KeyError, OSError) as e:
    TORCH_AVAILABLE = False
    torch = None
    dml = None
    if "libamd_smi" in str(e):
        print(f"WARNING: PyTorch import failed due to AMD SMI library issue: {e}")
    else:
        print(f"WARNING: PyTorch import failed: {e}")

# Use the root logger which will inherit the worker's colored formatter
logger = logging.getLogger()

class DirectMLPatch:
    """Multi-GPU DirectML patch that distributes models across multiple DirectML devices"""
    
    def __init__(self):
        if not TORCH_AVAILABLE or not dml:
            self.device_count = 0
            self.devices = []
            self.current_device_index = 0
            self.device_lock = threading.Lock()
            logger.warning("DirectML not available - patch disabled")
            return
            
        self.device_count = dml.device_count()
        self.devices = [dml.device(i) for i in range(self.device_count)]
        self.current_device_index = 0
        self.device_lock = threading.Lock()
        
        # Store device info for external access, but don't log here
        # Logging will be handled by the memory module that imports this
        
    def get_next_device(self):
        """Get the next DirectML device in round-robin fashion"""
        with self.device_lock:
            device = self.devices[self.current_device_index]
            self.current_device_index = (self.current_device_index + 1) % self.device_count
            return device
    
    def get_device(self, device_id: Optional[int] = None):
        """Get a specific DirectML device or the next available one"""
        if device_id is not None and 0 <= device_id < self.device_count:
            return self.devices[device_id]
        return self.get_next_device()

# Create global patch instance
_dml_patch = DirectMLPatch()

# Only apply patches if torch is available
if TORCH_AVAILABLE and torch:
    # 1) Make torch.cuda.is_available() always return False
    torch.cuda.is_available = lambda: False

    # 2) Monkey-patch tensor.to(...) to reroute "cuda" → DirectML device
    _orig_tensor_to = torch.Tensor.to
    def _dml_tensor_to(self, *args, **kwargs):
        if len(args) > 0:
            target = args[0]
            if target == "cuda" or (isinstance(target, torch.device) and target.type == "cuda"):
                device = _dml_patch.get_next_device()
                return _orig_tensor_to(self, device, *args[1:], **kwargs)
            elif isinstance(target, str) and target.startswith("cuda:"):
                try:
                    cuda_id = int(target.split(":")[1])
                    device = _dml_patch.get_device(cuda_id % _dml_patch.device_count)
                    return _orig_tensor_to(self, device, *args[1:], **kwargs)
                except (ValueError, IndexError):
                    device = _dml_patch.get_next_device()
                    return _orig_tensor_to(self, device, *args[1:], **kwargs)
        return _orig_tensor_to(self, *args, **kwargs)

    torch.Tensor.to = _dml_tensor_to

    # 3) Monkey-patch module.to(...) to do the same
    _orig_module_to = torch.nn.Module.to
    def _dml_module_to(self, *args, **kwargs):
        if len(args) > 0:
            target = args[0]
            if target == "cuda" or (isinstance(target, torch.device) and target.type == "cuda"):
                device = _dml_patch.get_next_device()
                return _orig_module_to(self, device, *args[1:], **kwargs)
            elif isinstance(target, str) and target.startswith("cuda:"):
                try:
                    cuda_id = int(target.split(":")[1])
                    device = _dml_patch.get_device(cuda_id % _dml_patch.device_count)
                    return _orig_module_to(self, device, *args[1:], **kwargs)
                except (ValueError, IndexError):
                    device = _dml_patch.get_next_device()
                    return _orig_module_to(self, device, *args[1:], **kwargs)
        return _orig_module_to(self, *args, **kwargs)

    torch.nn.Module.to = _dml_module_to

    # 4) Override CUDA functions
    torch.cuda.device_count = lambda: _dml_patch.device_count
    torch.cuda.current_device = lambda: _dml_patch.current_device_index

    def _dml_set_device(device_id: int):
        if 0 <= device_id < _dml_patch.device_count:
            _dml_patch.current_device_index = device_id

    torch.cuda.set_device = _dml_set_device

    # 5) Override other CUDA flags
    torch.backends.cuda.is_built = False
    torch.backends.cudnn.enabled = False

    logger.info("DirectML CUDA patches applied for %s devices", _dml_patch.device_count)
else:
    logger.warning("DirectML patches not applied - torch not available")

# 6) Utility functions
def get_directml_device(device_id: Optional[int] = None):
    """Get a specific DirectML device"""
    return _dml_patch.get_device(device_id)

def get_directml_device_count() -> int:
    """Get the number of DirectML devices"""
    return _dml_patch.device_count

def distribute_models_across_gpus(*models):
    """Distribute multiple models across available DirectML devices"""
    results = []
    for i, model in enumerate(models):
        device_id = i % _dml_patch.device_count
        device = _dml_patch.get_device(device_id)
        model_on_device = model.to(device)
        results.append((model_on_device, device, device_id))
        # Logging will be handled by the component that uses this function
    return results

# DirectML patch is now loaded silently
# Device information will be logged by the memory module that imports this
