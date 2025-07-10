#!/usr/bin/env python3
"""
API Test Script for Device Operations
=====================================

Test the C# Device Operations API endpoints to:
1. Load cyberrealisticPony_v125.safetensors to all GPU VRAM
2. Start inference on one GPU
"""

import json
import requests
import time
from typing import Dict, Any

class DeviceOperationsAPITest:
    def __init__(self, base_url: str = "http://localhost:5000"):
        self.base_url = base_url
        self.session = requests.Session()
        self.session.headers.update({
            "Content-Type": "application/json",
            "Accept": "application/json"
        })
    
    def test_api_health(self) -> bool:
        """Test if the API is responding."""
        try:
            response = self.session.get(f"{self.base_url}/health", timeout=5)
            print(f"Health check: {response.status_code}")
            if response.status_code == 200:
                print("✅ API is healthy")
                return True
            else:
                print(f"⚠️  API responded with status {response.status_code}")
                return False
        except requests.exceptions.RequestException as e:
            print(f"❌ API health check failed: {e}")
            return False
    
    def get_device_status(self) -> Dict[str, Any]:
        """Get status of all GPU devices."""
        try:
            response = self.session.get(f"{self.base_url}/api/device/list")
            response.raise_for_status()
            
            data = response.json()
            print(f"✅ Device Status Retrieved:")
            print(f"   - Total devices: {data.get('count', 0)}")
            
            for device in data.get('devices', []):
                device_name = device.get('deviceId', 'Unknown')
                memory_info = device.get('memoryInfo', {})
                total_memory = memory_info.get('totalMemory', 0) / (1024**3)  # Convert to GB
                available_memory = memory_info.get('availableMemory', 0) / (1024**3)  # Convert to GB
                print(f"   - {device_name}: {total_memory-available_memory:.1f}GB / {total_memory:.1f}GB used")
            
            return data
            
        except requests.exceptions.RequestException as e:
            print(f"❌ Failed to get device status: {e}")
            return {}
    
    def load_model_to_all_gpus(self, model_name: str = "cyberrealisticPony_v125") -> bool:
        """Load model to all GPU VRAM."""
        try:
            # First, try to load on the first GPU
            payload = {
                "modelPath": f"C:\\Users\\admin\\Desktop\\node-manager\\models\\{model_name}.safetensors",
                "deviceId": "gpu_0",
                "modelName": model_name,
                "modelType": 1,  # SDXL enum value
                "preloadToMemory": True,
                "enableMultiGpu": True
            }
            
            print(f"🔄 Loading model '{model_name}' to GPU...")
            print(f"   Model path: {payload['modelPath']}")
            response = self.session.post(
                f"{self.base_url}/api/inference/load-model", 
                json=payload,
                timeout=60
            )
            response.raise_for_status()
            
            result = response.json()
            print(f"✅ Model loaded successfully: {result}")
            return True
            
        except requests.exceptions.RequestException as e:
            print(f"❌ Failed to load model: {e}")
            if hasattr(e, 'response') and e.response is not None:
                try:
                    error_detail = e.response.json()
                    print(f"   Error details: {error_detail}")
                except:
                    print(f"   Response text: {e.response.text}")
            return False
    
    def start_inference_on_gpu(self, gpu_id: str = "gpu_0", prompt: str = "a beautiful landscape") -> bool:
        """Start inference on a specific GPU using the Enhanced SDXL API."""
        try:
            payload = {
                "model": {
                    "base": "cyberrealisticPony_v125"
                },
                "scheduler": {
                    "type": "DPMSolverMultistepScheduler",
                    "steps": 20,
                    "useKarrasSigmas": True,
                    "timestepSpacing": "leading"
                },
                "hyperparameters": {
                    "guidanceScale": 7.5,
                    "seed": 42,
                    "batchSize": 1,
                    "width": 1024,
                    "height": 1024
                },
                "conditioning": {
                    "prompt": prompt,
                    "negativePrompt": "blurry, low quality, deformed"
                },
                "performance": {
                    "deviceId": gpu_id,
                    "enableXformers": False,
                    "memoryOptimizations": ["attention_slicing"]
                }
            }
            
            print(f"🎨 Starting Enhanced SDXL inference on {gpu_id}...")
            print(f"   Prompt: '{prompt}'")
            print(f"   Resolution: 1024x1024, Steps: 20")
            
            response = self.session.post(
                f"{self.base_url}/api/enhancedsdxl/generate",
                json=payload,
                timeout=300  # 5 minutes for inference
            )
            response.raise_for_status()
            
            result = response.json()
            print(f"✅ Enhanced SDXL inference completed successfully!")
            print(f"   Success: {result.get('success', False)}")
            if 'data' in result and 'images' in result['data']:
                print(f"   Generated {len(result['data']['images'])} images")
            return True
            
        except requests.exceptions.RequestException as e:
            print(f"❌ Failed to start inference: {e}")
            if hasattr(e, 'response') and e.response is not None:
                try:
                    error_detail = e.response.json()
                    print(f"   Error details: {error_detail}")
                except:
                    print(f"   Response text: {e.response.text}")
            return False
    
    def run_full_test(self):
        """Run the complete test suite."""
        print("=" * 60)
        print("🚀 Device Operations API Test")
        print("=" * 60)
        
        # 1. Health check
        if not self.test_api_health():
            print("❌ API is not available. Exiting.")
            return
        
        time.sleep(1)
        
        # 2. Get device status
        print("\n📊 Checking device status...")
        device_status = self.get_device_status()
        
        time.sleep(1)
        
        # 3. Load model to all GPUs
        print(f"\n📦 Loading cyberrealisticPony_v125.safetensors to all GPU VRAM...")
        model_loaded = self.load_model_to_all_gpus()
        
        if model_loaded:
            time.sleep(2)
            
            # 4. Check updated device status after model loading
            print("\n📊 Checking device status after model loading...")
            self.get_device_status()
            
            time.sleep(1)
            
            # 5. Start inference on first GPU
            print(f"\n🎨 Starting inference on GPU 0...")
            inference_success = self.start_inference_on_gpu(
                gpu_id="gpu_0",
                prompt="a beautiful cyberpunk cityscape with neon lights, highly detailed, 8k resolution"
            )
            
            if inference_success:
                print("\n🎉 Full test completed successfully!")
            else:
                print("\n⚠️  Test completed with inference issues")
        else:
            print("\n❌ Test failed at model loading stage")
        
        print("=" * 60)


if __name__ == "__main__":
    # Create test instance
    test = DeviceOperationsAPITest()
    
    # Run the full test
    test.run_full_test()
