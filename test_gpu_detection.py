#!/usr/bin/env python3
"""Test AMD GPU detection"""

import sys
from pathlib import Path

# Add current directory to path
current_dir = Path(__file__).parent
sys.path.insert(0, str(current_dir))

from core.resource_manager import ResourceManager

def test_gpu_detection():
    print("Testing AMD GPU detection...")
    
    rm = ResourceManager()
    gpus = rm.get_gpu_info()
    
    print(f"Found {len(gpus)} GPUs:")
    for gpu in gpus:
        memory_gb = gpu["memory_total"] // (1024**3)
        print(f"  - {gpu['name']} ({gpu['vendor']}) - {memory_gb}GB VRAM")
        print(f"    Driver: {gpu.get('driver_version', 'Unknown')}")
        print(f"    Detection: {gpu.get('detection_method', 'Unknown')}")
        print()
    
    return gpus

if __name__ == "__main__":
    test_gpu_detection()
