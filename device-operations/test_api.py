#!/usr/bin/env python3
"""
Test SDXL API Request
====================

Test the C# API with a real SDXL inference request using cyberrealisticPony_v125.safetensors
"""

import requests
import json
import time
from pathlib import Path

def test_enhanced_sdxl_api():
    """Test the Enhanced SDXL API endpoint."""
    
    # API endpoint
    base_url = "http://localhost:5000"
    endpoint = f"{base_url}/api/EnhancedSDXL/generate"
    
    # Test request for cyberrealisticPony model
    request_data = {
        "model": {
            "base": "models/cyberrealisticPony_v125.safetensors",
            "refiner": "",
            "vae": "",
            "tokenizer": "",
            "textEncoder": "",
            "textEncoder2": ""
        },
        "scheduler": {
            "type": "DPMSolverMultistep",
            "steps": 15,
            "snrWeighting": False,
            "useKarrasSigmas": True,
            "timestepSpacing": "leading"
        },
        "hyperparameters": {
            "prompt": "a beautiful cyberpunk pony with neon lights, highly detailed, digital art",
            "negativePrompt": "blurry, low quality, distorted, ugly, bad anatomy",
            "width": 512,
            "height": 512,
            "guidanceScale": 7.5,
            "seed": 42,
            "batchSize": 1,
            "promptWeighting": False
        },
        "conditioning": {
            "prompt": "a beautiful cyberpunk pony with neon lights, highly detailed, digital art",
            "img2ImgStrength": None,
            "maskBlur": None,
            "inpaintMask": "",
            "initImage": "",
            "controlNets": [],
            "loras": [],
            "textualInversions": [],
            "referenceImages": []
        },
        "performance": {
            "dtype": "fp16",
            "xformers": False,
            "attentionSlicing": True,
            "cpuOffload": False,
            "sequentialCpuOffload": False,
            "device": "gpu_0",
            "deviceIds": [],
            "compilation": "none"
        },
        "postprocessing": {
            "safetyChecker": False,
            "upscaler": "none",
            "upscaleFactor": 1.0,
            "autoContrast": False,
            "outputFormat": "PNG",
            "quality": 95,
            "watermark": False
        }
    }
    
    print("🚀 Testing Enhanced SDXL API")
    print("=" * 50)
    print(f"🌐 Endpoint: {endpoint}")
    print("🎨 Model: cyberrealisticPony_v125.safetensors")
    print(f"📝 Prompt: {request_data['hyperparameters']['prompt']}")
    print(f"📐 Size: {request_data['hyperparameters']['width']}x{request_data['hyperparameters']['height']}")
    print(f"🔢 Steps: {request_data['scheduler']['steps']}")
    print(f"🎛️  Guidance: {request_data['hyperparameters']['guidanceScale']}")
    print(f"🖥️  Device: {request_data['performance']['device']}")
    
    try:
        print("\n⏳ Sending API request...")
        start_time = time.time()
        
        response = requests.post(
            endpoint,
            json=request_data,
            headers={"Content-Type": "application/json"},
            timeout=300  # 5 minute timeout
        )
        
        end_time = time.time()
        duration = end_time - start_time
        
        print(f"⏱️  Request completed in {duration:.2f} seconds")
        print(f"📊 Status Code: {response.status_code}")
        
        if response.status_code == 200:
            result = response.json()
            print("\n✅ SUCCESS! API Response:")
            print(json.dumps(result, indent=2))
            
            if result.get("success"):
                print(f"\n🎉 SDXL Generation Successful!")
                if "images" in result and result["images"]:
                    print(f"🖼️  Generated {len(result['images'])} image(s)")
                if "generationTimeSeconds" in result:
                    print(f"⏱️  Generation time: {result['generationTimeSeconds']:.2f} seconds")
                if "memoryUsedMb" in result:
                    print(f"🧠 Memory used: {result['memoryUsedMb']:.1f} MB")
                if "seedUsed" in result:
                    print(f"🌱 Seed used: {result['seedUsed']}")
                    
                # Check if output files exist
                if "images" in result:
                    for i, image_path in enumerate(result["images"]):
                        full_path = Path("c:/Users/admin/Desktop/node-manager/device-operations") / image_path
                        if full_path.exists():
                            print(f"✅ Output file {i+1} exists: {full_path}")
                        else:
                            print(f"❌ Output file {i+1} not found: {full_path}")
            else:
                print(f"❌ Generation failed: {result.get('error', 'Unknown error')}")
                
        else:
            print(f"\n❌ API Error: {response.status_code}")
            try:
                error_data = response.json()
                print(json.dumps(error_data, indent=2))
            except:
                print(response.text)
                
        return response.status_code == 200 and response.json().get("success", False)
        
    except requests.exceptions.Timeout:
        print("⏰ Request timed out (5 minutes)")
        return False
    except requests.exceptions.ConnectionError:
        print("🔌 Connection error - is the C# API running?")
        return False
    except Exception as e:
        print(f"💥 Unexpected error: {str(e)}")
        return False


def test_api_health():
    """Test if the API is responding."""
    try:
        response = requests.get("http://localhost:5000", timeout=5)
        return response.status_code in [200, 404]  # 404 is ok, means server is up
    except:
        return False


def main():
    print("🧪 SDXL API Test Suite")
    print("Testing C# ↔ Python Integration")
    print("=" * 60)
    
    # Check if API is running
    print("🔍 Checking if API is running...")
    if not test_api_health():
        print("❌ API is not responding. Please start the C# application first.")
        print("   Run: cd c:\\Users\\admin\\Desktop\\node-manager\\device-operations && dotnet run")
        return 1
    
    print("✅ API is responding")
    
    # Run the SDXL test
    print("\n🎨 Testing SDXL Generation...")
    success = test_enhanced_sdxl_api()
    
    print("\n" + "=" * 60)
    if success:
        print("🎉 ALL TESTS PASSED!")
        print("✅ C# API is working")
        print("✅ Python workers are responding")
        print("✅ SDXL inference pipeline is functional")
        return 0
    else:
        print("❌ TESTS FAILED!")
        print("Check the errors above and the C# application logs")
        return 1


if __name__ == "__main__":
    import sys
    sys.exit(main())
