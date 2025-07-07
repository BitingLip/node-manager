# Enhanced Device Operations API

A high-performance API service for GPU memory management, AI inference, and advanced image processing operations using DirectML on Windows. Now featuring Enhanced SDXL pipeline with upscaling and post-processing capabilities.

## Features

### Core Operations
- **Memory Operations**: Allocate, deallocate, and manage GPU memory
- **AI Inference**: Load and run ONNX models using DirectML
- **Training Operations**: Execute training steps and manage checkpoints
- **Multi-GPU Support**: Handle multiple AMD RDNA2 GPUs
- **RESTful API**: Clean, documented API with Swagger/OpenAPI support
- **Real-time Monitoring**: Integration with device-monitor service

### Enhanced SDXL Pipeline (NEW)
- **High-Quality Image Generation**: SDXL base model with refiner support
- **Advanced Upscaling**: Real-ESRGAN and ESRGAN integration with 2x/4x scaling
- **LoRA Adapters**: Dynamic LoRA loading and weight adjustment
- **ControlNet Support**: Advanced conditioning with multiple ControlNet types
- **Custom VAE Integration**: Load and apply custom VAE models
- **Post-Processing Pipeline**: Comprehensive image enhancement workflow
- **Batch Processing**: Intelligent batch processing with memory optimization
- **Advanced Schedulers**: 10+ scheduler types including DPM++, Euler, DDIM
- **Quality Assessment**: Automatic quality scoring and validation

### Performance & Reliability
- **Memory Optimization**: Dynamic VRAM management and cleanup
- **Auto-Scaling**: Intelligent batch sizing based on available resources
- **Error Recovery**: Robust error handling with graceful degradation
- **Performance Monitoring**: Real-time metrics and benchmarking
- **Caching System**: Model and result caching for improved performance

## Prerequisites

### System Requirements
- **Operating System**: Windows 10/11 with DirectX 12 support
- **Development**: .NET 8.0 SDK
- **GPU**: AMD RDNA2 GPU (or other DirectML-compatible GPU) with 6GB+ VRAM
- **Memory**: 16GB+ RAM recommended (32GB+ for high-performance operations)
- **Storage**: 50GB+ available space for models and cache
- **Permissions**: Administrator privileges (for GPU access)

### Software Dependencies
- **Database**: PostgreSQL (optional, for device tracking)
- **Python**: Python 3.10+ (for Enhanced SDXL workers)
- **GPU Drivers**: Latest AMD or NVIDIA drivers
- **DirectML**: Windows DirectML runtime

### Enhanced SDXL Requirements
- **Models**: SDXL base models (4GB+), upscaling models (Real-ESRGAN/ESRGAN)
- **Python Packages**: PyTorch, Diffusers, Transformers, OpenCV, Pillow
- **VRAM**: 8GB+ recommended for upscaling operations
- **CPU**: 8+ cores recommended for preprocessing

## Quick Start

### 1. **Clone and Setup**
```bash
cd device-manager/device-operations

# Restore .NET dependencies
dotnet restore

# Setup Python environment for Enhanced SDXL
python -m venv venv
venv\Scripts\activate  # Windows
pip install -r requirements.txt
```

### 2. **Configure Environment**
```bash
# Set environment variables
set REALESRGAN_MODEL_PATH=C:\models\realesrgan
set ESRGAN_MODEL_PATH=C:\models\esrgan
set MAX_VRAM_USAGE=8GB
set ENABLE_MEMORY_OPTIMIZATION=true
```

### 3. **Download Models**
```bash
# Create model directories
mkdir models\realesrgan
mkdir models\esrgan

# Download upscaling models (automated script available)
# See deployment instructions for complete model setup
```

### 4. **Start Services**
```bash
# Start Enhanced SDXL Worker (Terminal 1)
cd src\Workers\features
python upscaler_worker.py --port 8888

# Start C# API Service (Terminal 2)
cd ..\..\..
dotnet run
```

### 5. **Access Services**
- **API Documentation**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/health
- **Worker Health**: http://localhost:8888/health

## API Endpoints

### Device Management
- `GET /api/device/list` - List all available GPU devices
- `GET /api/device/{deviceId}` - Get specific device information
- `GET /api/device/{deviceId}/memory` - Get device memory status
- `POST /api/device/{deviceId}/memory/allocate` - Allocate GPU memory
- `DELETE /api/device/{deviceId}/memory/{allocationId}` - Free GPU memory

### Enhanced SDXL Operations (NEW)
- `POST /api/enhanced-sdxl/generate` - Generate images with SDXL pipeline
- `POST /api/enhanced-sdxl/upscale` - Upscale images using Real-ESRGAN/ESRGAN
- `POST /api/enhanced-sdxl/batch-process` - Batch process multiple images
- `GET /api/enhanced-sdxl/models` - List available models and configurations
- `GET /api/enhanced-sdxl/status` - Get pipeline status and metrics

### Model Management
- `GET /api/models/list` - List all available models
- `POST /api/models/load` - Load a specific model
- `DELETE /api/models/{modelId}` - Unload a model
- `GET /api/models/{modelId}/status` - Get model status

### Performance & Monitoring
- `GET /api/performance/metrics` - Get real-time performance metrics
- `GET /api/performance/benchmark` - Run performance benchmarks
- `GET /api/memory/usage` - Get detailed memory usage statistics

### Health & Status
- `GET /health` - Service health status
- `GET /health/detailed` - Detailed health information
- `GET /worker/health` - Python worker health status

## Configuration

### Basic Configuration (`appsettings.json`)
```json
{
  "DeviceOperations": {
    "MaxMemoryAllocationGB": 24,
    "DefaultDevice": "gpu_0",
    "ModelCachePath": "./models",
    "EnableGPUScheduling": true
  },
  "EnhancedSDXL": {
    "WorkerUrl": "http://localhost:8888",
    "DefaultUpscaleMethod": "realesrgan",
    "DefaultQualityMode": "balanced",
    "MaxBatchSize": 4,
    "EnableMemoryOptimization": true,
    "MaxVramUsageGB": 8,
    "CacheUpscaledImages": true,
    "EnableQualityAssessment": true
  },
  "Performance": {
    "EnableMetrics": true,
    "EnableBenchmarking": true,
    "AutoOptimizeBatchSize": true,
    "MemoryCleanupThreshold": 0.85
  }
}
```

### Environment Variables
```bash
# Model Paths
REALESRGAN_MODEL_PATH=C:\models\realesrgan
ESRGAN_MODEL_PATH=C:\models\esrgan
SDXL_MODEL_PATH=C:\models\sdxl

# Performance Settings
MAX_VRAM_USAGE=8GB
ENABLE_MEMORY_OPTIMIZATION=true
TORCH_DEVICE=cuda
DEFAULT_QUALITY_MODE=balanced

# Worker Configuration
WORKER_PORT=8888
LOG_LEVEL=INFO
ENABLE_DEBUG_LOGGING=false
```

## Development

### Technology Stack
- **Backend**: ASP.NET Core 8.0 for the web framework
- **AI Processing**: Enhanced SDXL pipeline with Python workers
- **GPU Acceleration**: DirectML for Windows GPU operations
- **Image Processing**: Real-ESRGAN, ESRGAN for upscaling
- **Deep Learning**: PyTorch, Diffusers, Transformers
- **Logging**: Serilog for structured logging
- **Documentation**: Swagger/OpenAPI for API documentation
- **Monitoring**: Prometheus metrics and health checks

### Python Components
- **Upscaler Worker**: Real-ESRGAN and ESRGAN integration
- **Enhanced Response Handler**: Python → C# response conversion
- **Batch Manager**: Intelligent batch processing
- **Performance Monitor**: Real-time performance tracking
- **Model Manager**: Dynamic model loading and caching

## Architecture

```
device-operations/
├── src/
│   ├── Controllers/        # API endpoints
│   ├── Services/          # Business logic
│   │   ├── Enhanced/      # Enhanced SDXL services (NEW)
│   │   ├── Memory/        # Memory management
│   │   └── Device/        # Device operations
│   ├── Models/            # Data models and DTOs
│   ├── Middleware/        # Custom middleware
│   ├── Extensions/        # Extension methods
│   └── Workers/           # Python worker integration (NEW)
│       └── features/      # Enhanced SDXL features
│           ├── upscaler_worker.py
│           ├── comprehensive_testing.py
│           ├── batch_manager.py
│           ├── scheduler_manager.py
│           ├── lora_worker.py
│           ├── controlnet_worker.py
│           ├── sdxl_refiner_pipeline.py
│           ├── vae_manager.py
│           └── model_suite_coordinator.py
├── schemas/               # JSON schemas for validation
├── models/               # AI model storage
├── logs/                 # Application logs
├── docs/                 # Documentation (NEW)
│   ├── api_documentation.md
│   ├── performance_guide.md
│   ├── troubleshooting_guide.md
│   └── deployment_instructions.md
└── tests/                # Test suites
    ├── Integration/      # Integration tests
    └── Performance/      # Performance benchmarks
```

### Service Communication Flow
```
HTTP Request → C# API Controller → Enhanced Service Layer → Python Worker → GPU Processing → Response Transformation → HTTP Response
```

## Enhanced SDXL Examples

### Basic Image Upscaling
```bash
curl -X POST "http://localhost:5000/api/enhanced-sdxl/upscale" \
  -H "Content-Type: application/json" \
  -d '{
    "images": ["base64_encoded_image_data"],
    "scaleFactor": 2.0,
    "method": "realesrgan",
    "qualityMode": "high"
  }'
```

### Batch Image Processing
```bash
curl -X POST "http://localhost:5000/api/enhanced-sdxl/batch-process" \
  -H "Content-Type: application/json" \
  -d '{
    "images": ["image1_base64", "image2_base64", "image3_base64"],
    "scaleFactor": 4.0,
    "method": "esrgan",
    "batchSize": 2,
    "enableMemoryOptimization": true
  }'
```

### Performance Monitoring
```bash
# Get real-time metrics
curl -X GET "http://localhost:5000/api/performance/metrics"

# Run performance benchmark
curl -X POST "http://localhost:5000/api/performance/benchmark" \
  -H "Content-Type: application/json" \
  -d '{
    "testImages": 5,
    "configurations": ["fast", "balanced", "high"]
  }'
```

## Testing

### Run Comprehensive Tests
```bash
# Navigate to features directory
cd src\Workers\features

# Run comprehensive test suite
python comprehensive_testing.py

# Expected output:
# 📋 Execution Summary:
# • Total Tests: 4
# • Passed: 4
# • Failed: 0
# • Success Rate: 100.0%
```

### Performance Benchmarking
```bash
# Run performance benchmarks
python -c "
from upscaler_worker import UpscalerWorker
import asyncio

async def benchmark():
    worker = UpscalerWorker()
    # Benchmark code here
    
asyncio.run(benchmark())
"
```

## Documentation

### Complete Documentation Available
- **[API Documentation](src/Workers/features/api_documentation.md)** - Complete API reference
- **[Performance Guide](src/Workers/features/performance_guide.md)** - Optimization strategies
- **[Troubleshooting Guide](src/Workers/features/troubleshooting_guide.md)** - Problem resolution
- **[Deployment Instructions](src/Workers/features/deployment_instructions.md)** - Production deployment

### Performance Metrics
- **Upscaling Speed**: 15-30 seconds per image (2x), 25-45 seconds (4x)
- **Memory Usage**: 3-6GB VRAM depending on configuration
- **Batch Processing**: 3-5 images/minute with optimization
- **Quality Score**: 85-95% average quality assessment

## License

This project is part of the device-manager system.
