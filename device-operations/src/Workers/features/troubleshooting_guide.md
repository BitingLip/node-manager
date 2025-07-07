# Troubleshooting Guide

## Overview
Comprehensive troubleshooting guide for common issues in the Enhanced SDXL pipeline with upscaling and post-processing features.

## Table of Contents
- [Common Error Messages](#common-error-messages)
- [Performance Issues](#performance-issues)
- [Configuration Problems](#configuration-problems)
- [Hardware-Related Issues](#hardware-related-issues)
- [Model Loading Issues](#model-loading-issues)
- [Quality Issues](#quality-issues)
- [Memory and Resource Issues](#memory-and-resource-issues)
- [Integration Issues](#integration-issues)

## Common Error Messages

### "Insufficient VRAM for upscaling"

#### **Error Details**
```
ProcessingError: Insufficient VRAM for upscaling
Code: INSUFFICIENT_VRAM
Details: {"required": "2GB", "available": "1.5GB"}
```

#### **Causes**
- GPU memory is too limited for the requested upscaling operation
- Other processes are consuming VRAM
- Batch size is too large for available memory
- Models are not being properly unloaded

#### **Solutions**

##### Immediate Solutions
```python
# 1. Reduce batch size
upscale_config = {
    "batch_size": 1,  # Process one image at a time
    "scale_factor": 2.0
}

# 2. Enable memory optimization
upscale_config = {
    "enable_memory_optimization": True,
    "offload_to_cpu": True,
    "clear_cache_after_batch": True
}

# 3. Use lower precision
upscale_config = {
    "torch_dtype": "float16",  # Use half precision
    "enable_attention_slicing": True
}
```

##### Long-term Solutions
1. **Upgrade GPU** - Consider GPU with more VRAM (8GB+ recommended)
2. **Close other applications** - Free up VRAM from other processes
3. **Implement dynamic batch sizing** - Automatically adjust based on available memory

#### **Verification Steps**
```python
# Check available VRAM
import torch
if torch.cuda.is_available():
    total_memory = torch.cuda.get_device_properties(0).total_memory
    allocated_memory = torch.cuda.memory_allocated()
    available_memory = total_memory - allocated_memory
    print(f"Available VRAM: {available_memory / 1024**3:.2f} GB")
```

### "Model loading failed"

#### **Error Details**
```
ModelLoadError: Failed to load upscaling model
Code: MODEL_LOAD_FAILED
Details: {"model_path": "/models/realesrgan", "error": "File not found"}
```

#### **Causes**
- Model files are missing or corrupted
- Incorrect model file paths
- Insufficient storage space
- Permission issues

#### **Solutions**

##### File Path Issues
```python
# 1. Verify model paths
import os

model_paths = {
    "realesrgan_2x": "/models/realesrgan/RealESRGAN_x2plus.pth",
    "realesrgan_4x": "/models/realesrgan/RealESRGAN_x4plus.pth",
    "esrgan_4x": "/models/esrgan/ESRGAN_x4.pth"
}

for name, path in model_paths.items():
    if os.path.exists(path):
        print(f"✓ {name}: Found at {path}")
    else:
        print(f"✗ {name}: Missing at {path}")
```

##### Download Missing Models
```python
# 2. Download missing models
def download_model(model_name, url, destination):
    import requests
    import os
    
    os.makedirs(os.path.dirname(destination), exist_ok=True)
    
    print(f"Downloading {model_name}...")
    response = requests.get(url, stream=True)
    
    with open(destination, 'wb') as f:
        for chunk in response.iter_content(chunk_size=8192):
            f.write(chunk)
    
    print(f"✓ Downloaded {model_name} to {destination}")

# Example: Download Real-ESRGAN models
download_model(
    "RealESRGAN_x2plus",
    "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.1/RealESRGAN_x2plus.pth",
    "/models/realesrgan/RealESRGAN_x2plus.pth"
)
```

##### Permission Issues
```bash
# Fix permission issues (Windows)
icacls "C:\models" /grant Everyone:F /T

# Fix permission issues (Linux/Mac)
sudo chmod -R 755 /models/
sudo chown -R $USER:$USER /models/
```

### "Invalid upscale factor: must be 2.0 or 4.0"

#### **Error Details**
```
ConfigurationError: Invalid upscale factor: must be 2.0 or 4.0
Code: INVALID_SCALE_FACTOR
Details: {"provided_factor": 3.0, "valid_factors": [2.0, 4.0]}
```

#### **Solutions**
```python
# Correct configuration
upscale_config = {
    "scale_factor": 2.0,  # Use 2.0 or 4.0 only
    "method": "realesrgan"
}

# For custom scaling, use multiple passes
def custom_upscale(image, target_factor):
    if target_factor == 3.0:
        # First upscale by 2x, then downscale to 3x
        upscaled_2x = upscale_image(image, 2.0)
        return resize_image(upscaled_2x, target_factor / 2.0)
    else:
        return upscale_image(image, target_factor)
```

### "Python worker connection failed"

#### **Error Details**
```
ConnectionError: Python worker connection failed
Code: WORKER_CONNECTION_FAILED
Details: {"worker_type": "upscaler", "timeout": 30}
```

#### **Causes**
- Python worker process not running
- Network connectivity issues
- Worker process crashed
- Timeout configuration too low

#### **Solutions**

##### Check Worker Status
```python
# 1. Verify worker process
import psutil

def check_worker_processes():
    for proc in psutil.process_iter(['pid', 'name', 'cmdline']):
        try:
            if 'python' in proc.info['name'].lower():
                cmdline = ' '.join(proc.info['cmdline'])
                if 'upscaler_worker.py' in cmdline:
                    print(f"✓ Worker found: PID {proc.info['pid']}")
                    return True
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue
    print("✗ Worker process not found")
    return False
```

##### Restart Worker
```python
# 2. Restart worker process
import subprocess
import os

def restart_worker():
    # Kill existing worker
    os.system("taskkill /f /im python.exe")  # Windows
    # os.system("pkill -f upscaler_worker.py")  # Linux/Mac
    
    # Start new worker
    worker_script = "src/Workers/features/upscaler_worker.py"
    subprocess.Popen([
        "python", worker_script,
        "--port", "8888",
        "--workers", "2"
    ])
```

##### Increase Timeout
```csharp
// 3. Increase timeout in C# service
public class PyTorchWorkerService
{
    private readonly HttpClient _httpClient;
    
    public PyTorchWorkerService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)  // Increase timeout
        };
    }
}
```

## Performance Issues

### "Upscaling is very slow"

#### **Symptoms**
- Processing times > 60 seconds per image
- High CPU usage but low GPU usage
- System becomes unresponsive during processing

#### **Diagnostic Steps**
```python
# 1. Profile processing time
import time
import psutil

def profile_upscaling_performance():
    start_time = time.time()
    cpu_before = psutil.cpu_percent()
    
    # Your upscaling code here
    result = upscale_image(image)
    
    end_time = time.time()
    cpu_after = psutil.cpu_percent()
    
    print(f"Processing time: {end_time - start_time:.2f}s")
    print(f"CPU usage: {cpu_after}%")
    
    # Check GPU usage if available
    import torch
    if torch.cuda.is_available():
        print(f"GPU memory: {torch.cuda.memory_allocated() / 1024**3:.2f} GB")
```

#### **Optimization Solutions**

##### Hardware Optimization
```python
# 1. Enable GPU acceleration
optimization_config = {
    "device": "cuda",  # or "mps" for Mac, "directml" for Windows
    "enable_half_precision": True,
    "enable_attention_slicing": True,
    "enable_memory_efficient_attention": True
}

# 2. Optimize CPU usage
import torch
torch.set_num_threads(8)  # Set to number of CPU cores
```

##### Batch Processing
```python
# 3. Process multiple images together
def batch_upscale_optimized(images, batch_size=4):
    results = []
    
    for i in range(0, len(images), batch_size):
        batch = images[i:i + batch_size]
        
        # Process batch together
        batch_results = upscale_batch(batch)
        results.extend(batch_results)
        
        # Clear memory between batches
        torch.cuda.empty_cache()
    
    return results
```

##### Model Optimization
```python
# 4. Use optimized models
def load_optimized_model():
    model = load_upscaler_model()
    
    # Optimize for inference
    model.eval()
    model = torch.jit.optimize_for_inference(model)
    
    # Use TensorRT if available
    if hasattr(torch, 'tensorrt'):
        model = torch.jit.script(model)
    
    return model
```

### "High memory usage"

#### **Symptoms**
- System RAM usage > 90%
- VRAM usage > 95%
- Out of memory errors
- System swapping to disk

#### **Memory Monitoring**
```python
import psutil
import torch

def monitor_memory_usage():
    # System memory
    memory = psutil.virtual_memory()
    print(f"System RAM: {memory.percent}% used ({memory.used / 1024**3:.2f} GB)")
    
    # GPU memory
    if torch.cuda.is_available():
        gpu_memory = torch.cuda.memory_allocated()
        gpu_total = torch.cuda.get_device_properties(0).total_memory
        gpu_percent = (gpu_memory / gpu_total) * 100
        print(f"GPU VRAM: {gpu_percent:.1f}% used ({gpu_memory / 1024**3:.2f} GB)")
```

#### **Memory Optimization Solutions**

##### Aggressive Memory Management
```python
class AggressiveMemoryManager:
    def __init__(self):
        self.cleanup_threshold = 0.85  # 85% memory usage
    
    def cleanup_if_needed(self):
        memory = psutil.virtual_memory()
        if memory.percent > self.cleanup_threshold * 100:
            self.force_cleanup()
    
    def force_cleanup(self):
        import gc
        
        # Python garbage collection
        gc.collect()
        
        # PyTorch cache cleanup
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
            torch.cuda.synchronize()
        
        # Force immediate cleanup
        gc.set_threshold(1, 1, 1)
        gc.collect()
        gc.set_threshold(700, 10, 10)
```

##### Memory-Efficient Processing
```python
def memory_efficient_upscale(images):
    memory_manager = AggressiveMemoryManager()
    results = []
    
    for image in images:
        # Check memory before processing
        memory_manager.cleanup_if_needed()
        
        # Process single image
        result = upscale_single_image(image)
        results.append(result)
        
        # Cleanup after each image
        del image
        memory_manager.force_cleanup()
    
    return results
```

## Configuration Problems

### "Configuration validation failed"

#### **Error Details**
```
ValidationError: Configuration validation failed
Code: INVALID_CONFIGURATION
Details: {"field": "quality_mode", "value": "ultra", "valid_values": ["fast", "balanced", "high"]}
```

#### **Common Configuration Issues**

##### Invalid Quality Modes
```python
# ✗ Invalid configuration
invalid_config = {
    "quality_mode": "ultra",  # Not supported
    "scale_factor": 3.0,      # Not supported
    "method": "super_resolution"  # Not supported
}

# ✓ Valid configuration
valid_config = {
    "quality_mode": "high",    # "fast", "balanced", "high"
    "scale_factor": 2.0,       # 2.0 or 4.0
    "method": "realesrgan"     # "realesrgan" or "esrgan"
}
```

##### Configuration Validation Function
```python
def validate_upscale_config(config):
    """Validate upscaling configuration"""
    errors = []
    
    # Check scale factor
    if "scale_factor" in config:
        if config["scale_factor"] not in [2.0, 4.0]:
            errors.append(f"Invalid scale_factor: {config['scale_factor']}. Must be 2.0 or 4.0")
    
    # Check quality mode
    if "quality_mode" in config:
        valid_modes = ["fast", "balanced", "high"]
        if config["quality_mode"] not in valid_modes:
            errors.append(f"Invalid quality_mode: {config['quality_mode']}. Must be one of {valid_modes}")
    
    # Check method
    if "method" in config:
        valid_methods = ["realesrgan", "esrgan"]
        if config["method"] not in valid_methods:
            errors.append(f"Invalid method: {config['method']}. Must be one of {valid_methods}")
    
    return errors

# Usage
config = {"scale_factor": 3.0, "quality_mode": "ultra"}
errors = validate_upscale_config(config)
if errors:
    for error in errors:
        print(f"✗ {error}")
```

### "Environment variables not set"

#### **Required Environment Variables**
```bash
# Model paths
REALESRGAN_MODEL_PATH=/models/realesrgan
ESRGAN_MODEL_PATH=/models/esrgan

# Performance settings
MAX_VRAM_USAGE=6GB
ENABLE_MEMORY_OPTIMIZATION=true

# Device settings
TORCH_DEVICE=cuda
DIRECTML_DEVICE=0
```

#### **Environment Setup Script**
```bash
# setup_environment.bat (Windows)
@echo off
echo Setting up Enhanced SDXL environment...

set REALESRGAN_MODEL_PATH=C:\models\realesrgan
set ESRGAN_MODEL_PATH=C:\models\esrgan
set MAX_VRAM_USAGE=6GB
set ENABLE_MEMORY_OPTIMIZATION=true
set TORCH_DEVICE=cuda

echo Environment variables set successfully!
echo.
echo Model paths:
echo   Real-ESRGAN: %REALESRGAN_MODEL_PATH%
echo   ESRGAN: %ESRGAN_MODEL_PATH%
echo.
echo Performance settings:
echo   Max VRAM: %MAX_VRAM_USAGE%
echo   Memory optimization: %ENABLE_MEMORY_OPTIMIZATION%
```

```bash
# setup_environment.sh (Linux/Mac)
#!/bin/bash
echo "Setting up Enhanced SDXL environment..."

export REALESRGAN_MODEL_PATH=/models/realesrgan
export ESRGAN_MODEL_PATH=/models/esrgan
export MAX_VRAM_USAGE=6GB
export ENABLE_MEMORY_OPTIMIZATION=true
export TORCH_DEVICE=cuda

echo "Environment variables set successfully!"
echo ""
echo "Model paths:"
echo "  Real-ESRGAN: $REALESRGAN_MODEL_PATH"
echo "  ESRGAN: $ESRGAN_MODEL_PATH"
echo ""
echo "Performance settings:"
echo "  Max VRAM: $MAX_VRAM_USAGE"
echo "  Memory optimization: $ENABLE_MEMORY_OPTIMIZATION"
```

## Hardware-Related Issues

### "DirectML device not found"

#### **Error Details**
```
DeviceError: DirectML device not found
Code: DIRECTML_NOT_AVAILABLE
Details: {"available_devices": [], "required_device": "DirectML"}
```

#### **Diagnostic Steps**
```python
# 1. Check DirectML availability
def check_directml_availability():
    try:
        import torch_directml
        device_count = torch_directml.device_count()
        print(f"DirectML devices available: {device_count}")
        
        for i in range(device_count):
            device_name = torch_directml.get_device_name(i)
            print(f"  Device {i}: {device_name}")
        
        return device_count > 0
    except ImportError:
        print("torch-directml not installed")
        return False
    except Exception as e:
        print(f"DirectML error: {e}")
        return False

# 2. Check GPU drivers
def check_gpu_drivers():
    import subprocess
    
    try:
        # Windows: Check GPU info
        result = subprocess.run(["dxdiag", "/t", "gpu_info.txt"], 
                              capture_output=True, text=True)
        print("GPU information saved to gpu_info.txt")
    except Exception:
        print("Could not retrieve GPU information")
```

#### **Solutions**

##### Install DirectML
```bash
# Install torch-directml
pip install torch-directml

# Verify installation
python -c "import torch_directml; print(f'DirectML devices: {torch_directml.device_count()}')"
```

##### Update GPU Drivers
```bash
# Windows: Update GPU drivers
# 1. Go to Device Manager
# 2. Expand "Display adapters"
# 3. Right-click your GPU and select "Update driver"
# 4. Choose "Search automatically for drivers"

# Or download directly from:
# - NVIDIA: https://www.nvidia.com/drivers/
# - AMD: https://www.amd.com/support/
# - Intel: https://www.intel.com/content/www/us/en/support/
```

##### Fallback to CPU
```python
# Use CPU as fallback
def get_best_available_device():
    # Try DirectML first
    try:
        import torch_directml
        if torch_directml.device_count() > 0:
            return torch_directml.device()
    except:
        pass
    
    # Try CUDA
    if torch.cuda.is_available():
        return torch.device("cuda")
    
    # Try MPS (Mac)
    if hasattr(torch.backends, 'mps') and torch.backends.mps.is_available():
        return torch.device("mps")
    
    # Fallback to CPU
    print("Warning: Using CPU for processing (will be slower)")
    return torch.device("cpu")
```

### "GPU out of memory during processing"

#### **Error Details**
```
RuntimeError: CUDA out of memory. Tried to allocate 2.00 GiB
```

#### **Emergency Recovery**
```python
def emergency_memory_recovery():
    """Emergency memory recovery when GPU runs out of memory"""
    import torch
    import gc
    
    print("🚨 Emergency memory recovery activated!")
    
    # Clear all caches
    if torch.cuda.is_available():
        torch.cuda.empty_cache()
        torch.cuda.ipc_collect()
        torch.cuda.synchronize()
    
    # Force garbage collection
    gc.collect()
    
    # Try to free up system memory
    import psutil
    current_process = psutil.Process()
    children = current_process.children(recursive=True)
    
    for child in children:
        try:
            if 'python' in child.name().lower():
                child.terminate()
        except psutil.NoSuchProcess:
            pass
    
    print("✅ Emergency recovery completed")

# Automatic recovery wrapper
def safe_upscale_with_recovery(images, config):
    try:
        return upscale_images(images, config)
    except RuntimeError as e:
        if "out of memory" in str(e).lower():
            print("Out of memory detected, attempting recovery...")
            emergency_memory_recovery()
            
            # Retry with reduced batch size
            config_reduced = config.copy()
            config_reduced["batch_size"] = 1
            return upscale_images(images, config_reduced)
        else:
            raise e
```

## Model Loading Issues

### "Corrupted model file detected"

#### **Error Details**
```
ModelError: Corrupted model file detected
Code: CORRUPTED_MODEL
Details: {"model_path": "/models/realesrgan/model.pth", "checksum_expected": "abc123", "checksum_actual": "def456"}
```

#### **Model Verification**
```python
import hashlib
import os

def verify_model_integrity(model_path, expected_checksum=None):
    """Verify model file integrity"""
    if not os.path.exists(model_path):
        return False, "Model file not found"
    
    # Calculate file checksum
    sha256_hash = hashlib.sha256()
    with open(model_path, "rb") as f:
        for chunk in iter(lambda: f.read(4096), b""):
            sha256_hash.update(chunk)
    
    actual_checksum = sha256_hash.hexdigest()
    
    if expected_checksum and actual_checksum != expected_checksum:
        return False, f"Checksum mismatch: expected {expected_checksum}, got {actual_checksum}"
    
    # Check file size (basic sanity check)
    file_size = os.path.getsize(model_path)
    if file_size < 1024 * 1024:  # Less than 1MB
        return False, f"Model file too small: {file_size} bytes"
    
    return True, f"Model verified: {actual_checksum}"

# Verify all models
model_checksums = {
    "/models/realesrgan/RealESRGAN_x2plus.pth": "expected_checksum_here",
    "/models/realesrgan/RealESRGAN_x4plus.pth": "expected_checksum_here"
}

for model_path, expected_checksum in model_checksums.items():
    is_valid, message = verify_model_integrity(model_path, expected_checksum)
    status = "✓" if is_valid else "✗"
    print(f"{status} {os.path.basename(model_path)}: {message}")
```

#### **Model Recovery**
```python
def download_and_verify_model(model_name, download_url, destination, expected_checksum):
    """Download and verify model integrity"""
    import requests
    import tempfile
    import shutil
    
    print(f"Downloading {model_name}...")
    
    # Download to temporary location first
    with tempfile.NamedTemporaryFile(delete=False) as temp_file:
        response = requests.get(download_url, stream=True)
        response.raise_for_status()
        
        for chunk in response.iter_content(chunk_size=8192):
            temp_file.write(chunk)
        
        temp_path = temp_file.name
    
    # Verify downloaded file
    is_valid, message = verify_model_integrity(temp_path, expected_checksum)
    
    if is_valid:
        # Move to final location
        os.makedirs(os.path.dirname(destination), exist_ok=True)
        shutil.move(temp_path, destination)
        print(f"✓ {model_name} downloaded and verified successfully")
        return True
    else:
        # Remove corrupted download
        os.unlink(temp_path)
        print(f"✗ Downloaded {model_name} failed verification: {message}")
        return False

# Example: Re-download corrupted model
download_and_verify_model(
    "RealESRGAN_x2plus",
    "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.1/RealESRGAN_x2plus.pth",
    "/models/realesrgan/RealESRGAN_x2plus.pth",
    "expected_checksum_here"
)
```

## Quality Issues

### "Poor upscaling quality"

#### **Symptoms**
- Blurry or artifact-heavy results
- Loss of detail in upscaled images
- Inconsistent quality across different images

#### **Quality Assessment**
```python
import cv2
import numpy as np
from PIL import Image

def assess_image_quality(original_image, upscaled_image):
    """Assess upscaling quality using multiple metrics"""
    
    # Convert to numpy arrays
    if isinstance(original_image, Image.Image):
        original = np.array(original_image)
    if isinstance(upscaled_image, Image.Image):
        upscaled = np.array(upscaled_image)
    
    # Resize original to match upscaled for comparison
    scale_factor = upscaled.shape[0] / original.shape[0]
    original_resized = cv2.resize(original, 
                                 (upscaled.shape[1], upscaled.shape[0]), 
                                 interpolation=cv2.INTER_CUBIC)
    
    # Calculate PSNR (Peak Signal-to-Noise Ratio)
    mse = np.mean((original_resized - upscaled) ** 2)
    if mse == 0:
        psnr = float('inf')
    else:
        psnr = 20 * np.log10(255.0 / np.sqrt(mse))
    
    # Calculate SSIM (Structural Similarity Index)
    from skimage.metrics import structural_similarity as ssim
    ssim_score = ssim(original_resized, upscaled, multichannel=True, channel_axis=2)
    
    # Edge preservation metric
    def calculate_edge_preservation(img1, img2):
        edges1 = cv2.Canny(cv2.cvtColor(img1, cv2.COLOR_RGB2GRAY), 50, 150)
        edges2 = cv2.Canny(cv2.cvtColor(img2, cv2.COLOR_RGB2GRAY), 50, 150)
        return np.sum(edges1 & edges2) / np.sum(edges1 | edges2)
    
    edge_preservation = calculate_edge_preservation(original_resized, upscaled)
    
    return {
        "psnr": psnr,
        "ssim": ssim_score,
        "edge_preservation": edge_preservation,
        "scale_factor": scale_factor,
        "overall_quality": (psnr/40 + ssim_score + edge_preservation) / 3
    }

# Example usage
quality_metrics = assess_image_quality(original_image, upscaled_image)
print(f"Quality Assessment:")
print(f"  PSNR: {quality_metrics['psnr']:.2f} dB")
print(f"  SSIM: {quality_metrics['ssim']:.3f}")
print(f"  Edge Preservation: {quality_metrics['edge_preservation']:.3f}")
print(f"  Overall Quality: {quality_metrics['overall_quality']:.3f}")
```

#### **Quality Improvement Strategies**

##### Optimize Model Settings
```python
def get_quality_optimized_config():
    """Configuration for maximum quality"""
    return {
        "scale_factor": 2.0,  # Use 2x for better quality than 4x
        "method": "realesrgan",  # Generally better quality than ESRGAN
        "quality_mode": "high",
        "preserve_alpha": True,
        "enable_tile_processing": True,  # Better for large images
        "tile_size": 512,
        "tile_overlap": 32
    }
```

##### Pre-processing Enhancement
```python
def enhance_before_upscaling(image):
    """Pre-process image for better upscaling results"""
    import cv2
    import numpy as np
    
    # Convert to numpy array
    img_array = np.array(image)
    
    # Noise reduction
    img_denoised = cv2.bilateralFilter(img_array, 9, 75, 75)
    
    # Enhance contrast
    lab = cv2.cvtColor(img_denoised, cv2.COLOR_RGB2LAB)
    l, a, b = cv2.split(lab)
    clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8,8))
    l = clahe.apply(l)
    enhanced = cv2.merge([l, a, b])
    enhanced = cv2.cvtColor(enhanced, cv2.COLOR_LAB2RGB)
    
    return Image.fromarray(enhanced)
```

##### Post-processing Refinement
```python
def refine_after_upscaling(upscaled_image):
    """Post-process upscaled image for quality improvement"""
    import cv2
    import numpy as np
    
    img_array = np.array(upscaled_image)
    
    # Subtle sharpening
    kernel = np.array([[-1,-1,-1], [-1,9,-1], [-1,-1,-1]])
    sharpened = cv2.filter2D(img_array, -1, kernel)
    
    # Blend original and sharpened (subtle effect)
    refined = cv2.addWeighted(img_array, 0.8, sharpened, 0.2, 0)
    
    # Reduce any artifacts
    refined = cv2.bilateralFilter(refined, 5, 50, 50)
    
    return Image.fromarray(refined)
```

## Memory and Resource Issues

### "System becomes unresponsive during processing"

#### **Symptoms**
- GUI freezes during upscaling
- Mouse and keyboard input lag
- Other applications become slow
- System fan noise increases significantly

#### **Resource Monitoring**
```python
import psutil
import threading
import time

class SystemMonitor:
    def __init__(self):
        self.monitoring = False
        self.alerts = []
    
    def start_monitoring(self):
        self.monitoring = True
        monitor_thread = threading.Thread(target=self._monitor_loop, daemon=True)
        monitor_thread.start()
    
    def _monitor_loop(self):
        while self.monitoring:
            # Check CPU usage
            cpu_percent = psutil.cpu_percent(interval=1)
            if cpu_percent > 95:
                self.alerts.append(f"High CPU usage: {cpu_percent}%")
            
            # Check memory usage
            memory = psutil.virtual_memory()
            if memory.percent > 90:
                self.alerts.append(f"High memory usage: {memory.percent}%")
            
            # Check disk I/O
            disk_io = psutil.disk_io_counters()
            if hasattr(self, '_last_disk_io'):
                read_speed = (disk_io.read_bytes - self._last_disk_io.read_bytes) / 1024 / 1024  # MB/s
                if read_speed > 100:  # More than 100 MB/s
                    self.alerts.append(f"High disk I/O: {read_speed:.1f} MB/s")
            self._last_disk_io = disk_io
            
            time.sleep(1)
    
    def get_recent_alerts(self):
        alerts = self.alerts.copy()
        self.alerts.clear()
        return alerts

# Usage
monitor = SystemMonitor()
monitor.start_monitoring()

# During processing, check for alerts
alerts = monitor.get_recent_alerts()
for alert in alerts:
    print(f"⚠️ {alert}")
```

#### **Resource Management Solutions**

##### Process Priority Management
```python
import psutil
import os

def set_process_priority(priority="normal"):
    """Set process priority to prevent system lockup"""
    process = psutil.Process(os.getpid())
    
    if priority == "low":
        process.nice(psutil.IDLE_PRIORITY_CLASS)  # Windows
        # process.nice(19)  # Linux/Mac
    elif priority == "normal":
        process.nice(psutil.NORMAL_PRIORITY_CLASS)  # Windows
        # process.nice(0)  # Linux/Mac
    elif priority == "high":
        process.nice(psutil.HIGH_PRIORITY_CLASS)  # Windows
        # process.nice(-10)  # Linux/Mac
    
    print(f"Process priority set to: {priority}")

# Set low priority to prevent system lockup
set_process_priority("low")
```

##### CPU Thread Limiting
```python
import torch
import os

def limit_cpu_usage(max_threads=4):
    """Limit CPU thread usage to keep system responsive"""
    
    # Limit PyTorch threads
    torch.set_num_threads(max_threads)
    
    # Limit other threading libraries
    os.environ["OMP_NUM_THREADS"] = str(max_threads)
    os.environ["MKL_NUM_THREADS"] = str(max_threads)
    os.environ["NUMEXPR_NUM_THREADS"] = str(max_threads)
    
    print(f"Limited CPU threads to: {max_threads}")

# Example: Use only 4 threads to keep system responsive
limit_cpu_usage(4)
```

##### Processing with Breaks
```python
import time
import asyncio

async def process_with_system_breaks(images, process_function, break_interval=30):
    """Process images with regular breaks to keep system responsive"""
    
    results = []
    start_time = time.time()
    
    for i, image in enumerate(images):
        # Process single image
        result = await process_function(image)
        results.append(result)
        
        # Take a break every N seconds
        elapsed = time.time() - start_time
        if elapsed > break_interval:
            print("Taking a break to keep system responsive...")
            await asyncio.sleep(2)  # 2-second break
            start_time = time.time()
    
    return results
```

## Integration Issues

### "C# service cannot communicate with Python worker"

#### **Error Details**
```
IntegrationError: C# service cannot communicate with Python worker
Code: COMMUNICATION_FAILED
Details: {"service": "EnhancedSDXL", "worker": "upscaler", "last_successful": "2024-01-01T10:00:00Z"}
```

#### **Communication Diagnosis**
```python
# Python worker diagnostic
def diagnose_worker_communication():
    import socket
    import json
    
    # Check if worker is listening
    def check_port_listening(port):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            result = s.connect_ex(('localhost', port))
            return result == 0
    
    worker_ports = [8888, 8889, 8890]  # Common worker ports
    
    print("Worker Communication Diagnosis:")
    for port in worker_ports:
        if check_port_listening(port):
            print(f"✓ Worker listening on port {port}")
        else:
            print(f"✗ No worker on port {port}")
    
    # Test worker response
    def test_worker_response(port):
        try:
            import requests
            response = requests.get(f"http://localhost:{port}/health", timeout=5)
            if response.status_code == 200:
                print(f"✓ Worker on port {port} responding to health checks")
                return True
        except Exception as e:
            print(f"✗ Worker on port {port} not responding: {e}")
        return False
    
    for port in worker_ports:
        if check_port_listening(port):
            test_worker_response(port)

diagnose_worker_communication()
```

```csharp
// C# service diagnostic
public class CommunicationDiagnostic
{
    public async Task<bool> DiagnoseWorkerCommunication()
    {
        var workerUrls = new[] { 
            "http://localhost:8888", 
            "http://localhost:8889", 
            "http://localhost:8890" 
        };
        
        Console.WriteLine("C# Service Communication Diagnosis:");
        
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        
        foreach (var url in workerUrls)
        {
            try
            {
                var response = await httpClient.GetAsync($"{url}/health");
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✓ Can communicate with worker at {url}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"✗ Worker at {url} returned: {response.StatusCode}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"✗ Cannot reach worker at {url}: {e.Message}");
            }
        }
        
        return false;
    }
}
```

#### **Communication Recovery**

##### Restart Communication Pipeline
```python
# Python: Restart worker
def restart_worker_service():
    import subprocess
    import sys
    import os
    
    print("Restarting worker service...")
    
    # Kill existing workers
    os.system("taskkill /f /im python.exe /fi \"COMMANDLINE eq *upscaler_worker*\"")
    
    # Start new worker
    script_path = os.path.join(os.path.dirname(__file__), "upscaler_worker.py")
    subprocess.Popen([
        sys.executable, script_path,
        "--host", "localhost",
        "--port", "8888",
        "--workers", "2"
    ], creationflags=subprocess.CREATE_NEW_CONSOLE)
    
    print("Worker service restarted")
```

```csharp
// C# Service: Reconnection logic
public class ResilientWorkerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _reconnectSemaphore = new(1, 1);
    
    public async Task<T> SendRequestWithRetry<T>(object request, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await SendRequest<T>(request);
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                _logger.LogWarning($"Communication failed, attempt {attempt}/{maxRetries}");
                
                // Try to reconnect
                await TryReconnect();
                
                // Wait before retry
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }
        
        throw new InvalidOperationException("Failed to communicate with worker after all retries");
    }
    
    private async Task TryReconnect()
    {
        await _reconnectSemaphore.WaitAsync();
        try
        {
            // Restart Python worker process
            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "src/Workers/features/upscaler_worker.py --port 8888",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            Process.Start(startInfo);
            
            // Wait for worker to start
            await Task.Delay(5000);
        }
        finally
        {
            _reconnectSemaphore.Release();
        }
    }
}
```

For additional troubleshooting support, check the logs directory for detailed error information and consider enabling debug mode for more verbose logging.
