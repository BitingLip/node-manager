# Device Operations API - Usage Guide

## Overview

The Device Operations API provides comprehensive ML device management, model loading, inference execution, and postprocessing capabilities. This guide provides practical examples for common use cases.

## Base URL

- Development: `http://localhost:5000/api`
- Production: `https://bitinglip.com/api`

## Authentication

The API supports multiple authentication methods:

### API Key Authentication
```bash
curl -H "X-API-Key: your-api-key" \
     -H "Content-Type: application/json" \
     http://localhost:5000/api/device/status
```

### Bearer Token Authentication
```bash
curl -H "Authorization: Bearer your-jwt-token" \
     -H "Content-Type: application/json" \
     http://localhost:5000/api/device/status
```

### Basic Authentication
```bash
curl -u username:password \
     -H "Content-Type: application/json" \
     http://localhost:5000/api/device/status
```

## Quick Start Workflow

### 1. Discover Available Devices

```bash
# Get all available devices
curl http://localhost:5000/api/device/list

# Example Response:
{
  "success": true,
  "data": [
    {
      "id": "cpu-0",
      "name": "Intel Core i7-12700K",
      "type": "cpu",
      "isAvailable": true,
      "memoryTotal": 32768,
      "memoryAvailable": 28672
    },
    {
      "id": "gpu-0",
      "name": "NVIDIA RTX 4090",
      "type": "gpu",
      "isAvailable": true,
      "memoryTotal": 24576,
      "memoryAvailable": 22528
    }
  ]
}
```

### 2. Check Device Capabilities

```bash
# Get capabilities for specific device
curl http://localhost:5000/api/device/capabilities/gpu-0

# Example Response:
{
  "success": true,
  "data": {
    "deviceId": "gpu-0",
    "supportedOperations": ["inference", "postprocessing", "model_loading"],
    "supportedModelTypes": ["sdxl", "sd15", "flux", "controlnet"],
    "maxConcurrentSessions": 2,
    "capabilities": {
      "fp16": true,
      "fp32": true,
      "int8": true,
      "directml": true,
      "cuda": true
    }
  }
}
```

### 3. Load a Model

```bash
# Load SDXL model onto GPU
curl -X POST http://localhost:5000/api/model/load/gpu-0 \
     -H "Content-Type: application/json" \
     -d '{
       "modelPath": "models/stable-diffusion-xl-base-1.0",
       "modelType": "sdxl",
       "deviceId": "gpu-0",
       "loadingStrategy": "optimal"
     }'

# Example Response:
{
  "success": true,
  "data": {
    "sessionId": "550e8400-e29b-41d4-a716-446655440000",
    "modelPath": "models/stable-diffusion-xl-base-1.0",
    "modelType": "sdxl",
    "deviceId": "gpu-0",
    "status": "loaded",
    "memoryUsage": 6144,
    "loadingTime": 15.2
  }
}
```

### 4. Execute Inference

```bash
# Generate an image using the loaded model
curl -X POST http://localhost:5000/api/inference/execute/gpu-0 \
     -H "Content-Type: application/json" \
     -d '{
       "prompt": "A beautiful landscape painting of mountains at sunset",
       "negativePrompt": "blurry, low quality, distorted",
       "width": 1024,
       "height": 1024,
       "steps": 20,
       "guidanceScale": 7.5,
       "seed": 12345,
       "sessionId": "550e8400-e29b-41d4-a716-446655440000"
     }'

# Example Response:
{
  "success": true,
  "data": {
    "inferenceId": "inf-550e8400-e29b-41d4-a716-446655440001",
    "sessionId": "550e8400-e29b-41d4-a716-446655440000",
    "status": "completed",
    "results": {
      "images": [
        {
          "imageData": "base64-encoded-image-data...",
          "format": "png",
          "width": 1024,
          "height": 1024,
          "seed": 12345
        }
      ],
      "metadata": {
        "inferenceTime": 8.7,
        "steps": 20,
        "guidanceScale": 7.5,
        "scheduler": "DPMSolverMultistep"
      }
    }
  }
}
```

### 5. Apply Postprocessing

```bash
# Upscale the generated image
curl -X POST http://localhost:5000/api/postprocessing/upscale \
     -H "Content-Type: application/json" \
     -d '{
       "imageData": "base64-encoded-image-data...",
       "upscaleModel": "RealESRGAN_x4plus",
       "upscaleFactor": 2,
       "deviceId": "gpu-0"
     }'

# Example Response:
{
  "success": true,
  "data": {
    "upscaledImage": {
      "imageData": "base64-encoded-upscaled-image...",
      "format": "png",
      "width": 2048,
      "height": 2048
    },
    "processingTime": 3.2,
    "upscaleModel": "RealESRGAN_x4plus",
    "upscaleFactor": 2
  }
}
```

## Advanced Use Cases

### Batch Processing

```bash
# Create a batch processing job
curl -X POST http://localhost:5000/api/processing/batches/create \
     -H "Content-Type: application/json" \
     -d '{
       "batchName": "landscape_generation_batch",
       "operations": [
         {
           "type": "inference",
           "parameters": {
             "prompt": "Mountain landscape at dawn",
             "steps": 20,
             "width": 1024,
             "height": 1024
           }
         },
         {
           "type": "postprocessing",
           "parameters": {
             "operation": "upscale",
             "upscaleFactor": 2,
             "upscaleModel": "RealESRGAN_x4plus"
           }
         }
       ],
       "itemCount": 10,
       "deviceIds": ["gpu-0"]
     }'

# Execute the batch
curl -X POST http://localhost:5000/api/processing/batches/{batchId}/execute \
     -H "Content-Type: application/json" \
     -d '{
       "concurrent": true,
       "maxConcurrency": 2
     }'
```

### Memory Management

```bash
# Check memory status
curl http://localhost:5000/api/memory/status

# Allocate memory for custom operations
curl -X POST http://localhost:5000/api/memory/allocations/allocate \
     -H "Content-Type: application/json" \
     -d '{
       "size": 2048,
       "type": "tensor_buffer",
       "deviceId": "gpu-0",
       "persistent": true
     }'

# Transfer memory between devices
curl -X POST http://localhost:5000/api/memory/transfer \
     -H "Content-Type: application/json" \
     -d '{
       "sourceDevice": "cpu-0",
       "targetDevice": "gpu-0",
       "allocationId": "alloc-550e8400-e29b-41d4-a716-446655440002",
       "async": true
     }'
```

### Session Management

```bash
# Get all active sessions
curl http://localhost:5000/api/processing/sessions

# Control a specific session
curl -X POST http://localhost:5000/api/processing/sessions/{sessionId}/control \
     -H "Content-Type: application/json" \
     -d '{
       "action": "pause"
     }'

# Resume a session
curl -X POST http://localhost:5000/api/processing/sessions/{sessionId}/control \
     -H "Content-Type: application/json" \
     -d '{
       "action": "resume"
     }'

# Cancel and cleanup session
curl -X DELETE http://localhost:5000/api/processing/sessions/{sessionId}
```

## Error Handling

The API uses standard HTTP status codes and provides detailed error information:

### Common Error Responses

```json
// 400 Bad Request - Invalid parameters
{
  "success": false,
  "error": {
    "code": "INVALID_PARAMETERS",
    "message": "Invalid prompt: prompt cannot be empty",
    "details": {
      "field": "prompt",
      "value": "",
      "constraint": "required"
    }
  }
}

// 404 Not Found - Resource not found
{
  "success": false,
  "error": {
    "code": "DEVICE_NOT_FOUND",
    "message": "Device with ID 'gpu-5' not found",
    "details": {
      "deviceId": "gpu-5",
      "availableDevices": ["cpu-0", "gpu-0", "gpu-1"]
    }
  }
}

// 409 Conflict - Resource conflict
{
  "success": false,
  "error": {
    "code": "DEVICE_BUSY",
    "message": "Device gpu-0 is currently busy with another operation",
    "details": {
      "deviceId": "gpu-0",
      "currentOperation": "inference",
      "estimatedAvailableAt": "2024-01-15T10:30:00Z"
    }
  }
}

// 500 Internal Server Error - Server error
{
  "success": false,
  "error": {
    "code": "INFERENCE_FAILED",
    "message": "Inference execution failed due to CUDA out of memory",
    "details": {
      "pythonError": "RuntimeError: CUDA out of memory...",
      "correlationId": "req-abc123-456"
    }
  }
}
```

## Rate Limiting

The API implements rate limiting to ensure fair usage:

- **Default Limits**: 100 requests per minute per API key
- **Inference Endpoints**: 10 requests per minute per device
- **Batch Operations**: 5 concurrent batches per API key

Rate limit headers are included in responses:
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 87
X-RateLimit-Reset: 1642248000
```

## Performance Optimization

### Model Caching
```bash
# Pre-cache frequently used models
curl -X POST http://localhost:5000/api/model/cache \
     -H "Content-Type: application/json" \
     -d '{
       "modelPaths": [
         "models/stable-diffusion-xl-base-1.0",
         "models/stable-diffusion-xl-refiner-1.0"
       ],
       "components": ["unet", "vae", "text_encoder"]
     }'
```

### Memory Optimization
```bash
# Enable automatic memory optimization
curl -X POST http://localhost:5000/api/memory/operations/defragment \
     -H "Content-Type: application/json" \
     -d '{
       "deviceIds": ["gpu-0"],
       "aggressive": false,
       "async": true
     }'
```

### Device Selection
```bash
# Get optimal device for specific operation
curl -X POST http://localhost:5000/api/device/optimal \
     -H "Content-Type: application/json" \
     -d '{
       "operation": "inference",
       "modelType": "sdxl",
       "requirements": {
         "minMemory": 8192,
         "preferredType": "gpu"
       }
     }'
```

## SDK Examples

### Python SDK Example
```python
import requests
import base64
from io import BytesIO
from PIL import Image

class DeviceOperationsClient:
    def __init__(self, base_url, api_key):
        self.base_url = base_url
        self.headers = {
            'X-API-Key': api_key,
            'Content-Type': 'application/json'
        }
    
    def get_devices(self):
        response = requests.get(f"{self.base_url}/device/list", headers=self.headers)
        return response.json()
    
    def load_model(self, device_id, model_path, model_type):
        data = {
            'modelPath': model_path,
            'modelType': model_type,
            'deviceId': device_id,
            'loadingStrategy': 'optimal'
        }
        response = requests.post(f"{self.base_url}/model/load/{device_id}", 
                               json=data, headers=self.headers)
        return response.json()
    
    def generate_image(self, device_id, prompt, **kwargs):
        data = {
            'prompt': prompt,
            'deviceId': device_id,
            **kwargs
        }
        response = requests.post(f"{self.base_url}/inference/execute/{device_id}",
                               json=data, headers=self.headers)
        result = response.json()
        
        if result['success']:
            # Decode base64 image
            image_data = base64.b64decode(result['data']['results']['images'][0]['imageData'])
            image = Image.open(BytesIO(image_data))
            return image
        else:
            raise Exception(f"Generation failed: {result['error']['message']}")

# Usage
client = DeviceOperationsClient('http://localhost:5000/api', 'your-api-key')

# Get available devices
devices = client.get_devices()
print(f"Available devices: {[d['id'] for d in devices['data']]}")

# Load model
session = client.load_model('gpu-0', 'models/stable-diffusion-xl-base-1.0', 'sdxl')
print(f"Model loaded with session ID: {session['data']['sessionId']}")

# Generate image
image = client.generate_image('gpu-0', 'A beautiful sunset over mountains', 
                             width=1024, height=1024, steps=20)
image.save('generated_image.png')
```

### JavaScript/Node.js SDK Example
```javascript
const axios = require('axios');
const fs = require('fs');

class DeviceOperationsClient {
    constructor(baseUrl, apiKey) {
        this.baseUrl = baseUrl;
        this.headers = {
            'X-API-Key': apiKey,
            'Content-Type': 'application/json'
        };
    }

    async getDevices() {
        const response = await axios.get(`${this.baseUrl}/device/list`, 
                                       { headers: this.headers });
        return response.data;
    }

    async loadModel(deviceId, modelPath, modelType) {
        const data = {
            modelPath,
            modelType,
            deviceId,
            loadingStrategy: 'optimal'
        };
        const response = await axios.post(`${this.baseUrl}/model/load/${deviceId}`,
                                        data, { headers: this.headers });
        return response.data;
    }

    async generateImage(deviceId, prompt, options = {}) {
        const data = {
            prompt,
            deviceId,
            ...options
        };
        const response = await axios.post(`${this.baseUrl}/inference/execute/${deviceId}`,
                                        data, { headers: this.headers });
        return response.data;
    }

    async saveImageFromResponse(response, filename) {
        if (response.success) {
            const imageData = response.data.results.images[0].imageData;
            const buffer = Buffer.from(imageData, 'base64');
            fs.writeFileSync(filename, buffer);
            return filename;
        } else {
            throw new Error(`Generation failed: ${response.error.message}`);
        }
    }
}

// Usage
async function main() {
    const client = new DeviceOperationsClient('http://localhost:5000/api', 'your-api-key');
    
    try {
        // Get devices
        const devices = await client.getDevices();
        console.log('Available devices:', devices.data.map(d => d.id));
        
        // Load model
        const session = await client.loadModel('gpu-0', 'models/stable-diffusion-xl-base-1.0', 'sdxl');
        console.log('Model loaded with session ID:', session.data.sessionId);
        
        // Generate image
        const result = await client.generateImage('gpu-0', 'A beautiful sunset over mountains', {
            width: 1024,
            height: 1024,
            steps: 20
        });
        
        await client.saveImageFromResponse(result, 'generated_image.png');
        console.log('Image saved as generated_image.png');
        
    } catch (error) {
        console.error('Error:', error.message);
    }
}

main();
```

## Support and Resources

- **API Documentation**: Visit `/api-docs` for interactive Swagger UI
- **GitHub Repository**: https://github.com/BitingLip/node-manager
- **Issue Tracker**: Report bugs and feature requests on GitHub
- **Community**: Join our Discord for support and discussions

## Changelog

### v1.0.0 (Current)
- Initial release with full device, model, inference, and postprocessing capabilities
- Comprehensive API documentation and examples
- Multi-authentication support
- Performance monitoring and optimization features
