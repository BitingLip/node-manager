# Python Workers System

## Overview

This directory contains the modular Python workers system for SDXL image generation. The system is designed to work with the C# device operations host through JSON-based IPC communication.

## Architecture

### Core Components
- **BaseWorker**: Abstract base class for all workers
- **DeviceManager**: Handles DirectML/CUDA device detection and optimization
- **CommunicationManager**: Manages async JSON IPC with the C# host

### Model Management
- **ModelLoader**: Loads and caches SDXL models with LRU eviction
- **LoRAManager**: Handles LoRA weight loading and application

### Scheduler System
- **SchedulerFactory**: Creates and configures diffusion schedulers
- Supports 10+ scheduler types with quality presets

### Inference Workers
- **SDXLWorker**: Main SDXL inference worker (text2img, img2img, inpainting)
- **PipelineManager**: Orchestrates multi-worker tasks and batch processing

### Main Entry Point
- **WorkerOrchestrator**: Coordinates communication and worker routing

## Configuration

The system uses `config.json` for configuration:

```json
{
  "default_worker": "pipeline_manager",
  "workers": {
    "pipeline_manager": {
      "max_concurrent_tasks": 2,
      "task_timeout": 600
    }
  }
}
```

## Usage

### Starting the Workers System

```python
from main import WorkerOrchestrator
import asyncio

async def main():
    orchestrator = WorkerOrchestrator("config.json")
    await orchestrator.start()

if __name__ == "__main__":
    asyncio.run(main())
```

### C# Integration

The system communicates with C# via JSON messages over stdio:

```csharp
// C# side sends JSON request
var request = new {
    worker_type = "sdxl_worker",
    method = "text2img",
    parameters = new {
        prompt = "A beautiful landscape",
        model_id = "stabilityai/stable-diffusion-xl-base-1.0"
    }
};
```

### Worker Types

1. **pipeline_manager**: Orchestrates complex multi-step workflows
2. **sdxl_worker**: Direct SDXL inference for single tasks
3. **model_loader**: Model management and caching

## Request Format

All requests follow the JSON schema defined in `../schemas/prompt_submission_schema.json`:

```json
{
  "model_id": "stabilityai/stable-diffusion-xl-base-1.0",
  "prompt": "A beautiful landscape, masterpiece, high quality",
  "negative_prompt": "blurry, low quality",
  "width": 1024,
  "height": 1024,
  "num_inference_steps": 30,
  "guidance_scale": 7.5,
  "scheduler": "DPMSolverMultistepScheduler",
  "seed": 42,
  "lora": {
    "enabled": true,
    "adapters": [
      {
        "model_id": "lora_model_id",
        "weight": 0.8
      }
    ]
  }
}
```

## Model Support

### Base Models
- SDXL 1.0 and compatible checkpoints
- SafeTensors and PyTorch formats
- HuggingFace Hub integration

### LoRA Support
- Dynamic LoRA loading and weight adjustment
- Multiple LoRA combination
- Memory-efficient adapter management

### Schedulers
- DPM++ 2M Karras
- Euler Ancestral
- DDIM
- Heun
- UniPC
- LMS
- PNDM
- DPM++ 2M
- DPM++ SDE Karras
- Euler

## Performance Features

### Memory Optimization
- LRU model caching with configurable limits
- Automatic GPU memory management
- Model offloading for memory constrained systems

### Device Support
- DirectML for AMD GPUs
- CUDA for NVIDIA GPUs
- Automatic device detection and optimization

### Generation Features
- Streaming progress updates
- Batch processing support
- Multi-stage workflows (text2img → img2img)
- Safety checker integration

## Error Handling

The system provides comprehensive error handling:

- Model loading errors with fallback options
- Memory management with automatic cleanup
- Device compatibility checks
- JSON schema validation

## Logging

Structured logging throughout the system:

```python
import logging
logger = logging.getLogger(__name__)
logger.info("Model loaded successfully", extra={"model_id": model_id})
```

## Dependencies

Key Python packages:
- torch (with DirectML support)
- diffusers
- transformers
- safetensors
- Pillow
- numpy
- jsonschema

## File Structure

```
Workers/
├── config.json              # Configuration
├── README.md               # This file
├── main.py                 # Entry point
├── __init__.py             # Package initialization
├── core/                   # Core infrastructure
│   ├── __init__.py
│   ├── base_worker.py      # BaseWorker abstract class
│   ├── device_manager.py   # Device detection/management
│   └── communication.py    # IPC handling
├── models/                 # Model management
│   ├── __init__.py
│   ├── model_loader.py     # Model loading/caching
│   └── lora_manager.py     # LoRA management
├── schedulers/             # Scheduler factory
│   ├── __init__.py
│   └── scheduler_factory.py
└── inference/              # Inference workers
    ├── __init__.py
    ├── sdxl_worker.py      # Main SDXL worker
    └── pipeline_manager.py # Multi-worker orchestration
```

## Testing

Run tests with:

```bash
python -m pytest tests/
```

## Development

For development:

1. Install dependencies: `pip install -r requirements.txt`
2. Configure paths in `config.json`
3. Run with: `python main.py`
4. Monitor logs in `../logs/workers.log`

## Troubleshooting

### Common Issues

1. **Model Loading Errors**
   - Check model paths in config.json
   - Verify model format (SafeTensors/PyTorch)
   - Ensure sufficient disk space

2. **Memory Issues**
   - Reduce max_cache_memory_gb in config
   - Lower max_batch_size
   - Enable model offloading

3. **Device Issues**
   - Verify DirectML/CUDA installation
   - Check device compatibility
   - Monitor GPU memory usage

### Debug Mode

Enable debug logging:

```json
{
  "logging": {
    "level": "DEBUG"
  }
}
```
