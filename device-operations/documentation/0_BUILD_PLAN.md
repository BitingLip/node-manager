I'll create a build plan for the `device-operations` service that handles memory, inference, and training operations through an API. Let me first check if there are any instruction files I should follow.

## Build Plan for `device-operations` Service

### 1. **Project Structure**
```
node-manager/
├── device-monitor/          (existing)
├── device-operations/       (new)
│   ├── DeviceOperations.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── README.md
│   ├── src/
│   │   ├── Controllers/
│   │   │   ├── MemoryController.cs
│   │   │   ├── InferenceController.cs
│   │   │   └── TrainingController.cs
│   │   ├── Services/
│   │   │   ├── Core/
│   │   │   │   ├── IDeviceService.cs
│   │   │   │   ├── DirectMLService.cs
│   │   │   │   └── DeviceManagerService.cs
│   │   │   ├── Memory/
│   │   │   │   ├── IMemoryOperationsService.cs
│   │   │   │   └── MemoryOperationsService.cs
│   │   │   ├── Inference/
│   │   │   │   ├── IInferenceService.cs
│   │   │   │   ├── InferenceService.cs
│   │   │   │   └── ModelLoaderService.cs
│   │   │   └── Training/
│   │   │       ├── ITrainingService.cs
│   │   │       └── TrainingService.cs
│   │   ├── Models/
│   │   │   ├── Requests/
│   │   │   │   ├── MemoryAllocateRequest.cs
│   │   │   │   ├── InferenceRequest.cs
│   │   │   │   └── TrainingRequest.cs
│   │   │   ├── Responses/
│   │   │   │   ├── MemoryStatusResponse.cs
│   │   │   │   ├── InferenceResponse.cs
│   │   │   │   └── TrainingStatusResponse.cs
│   │   │   └── Common/
│   │   │       ├── DeviceInfo.cs
│   │   │       ├── MemoryAllocation.cs
│   │   │       └── ModelMetadata.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   └── DeviceAvailabilityMiddleware.cs
│   │   └── Extensions/
│   │       ├── ServiceCollectionExtensions.cs
│   │       └── DirectMLExtensions.cs
│   └── tests/
│       └── DeviceOperations.Tests/
└── node-manager.sln (updated)
```

### 2. **Technology Stack**
- **Framework**: ASP.NET Core 8.0 (Web API)
- **GPU Compute**: 
  - Microsoft.ML.OnnxRuntime.DirectML (for inference)
  - Vortice.Windows.Direct3D12 (for low-level memory ops)
  - Vortice.Windows.DirectML (for custom operations)
- **API Documentation**: Swashbuckle (Swagger/OpenAPI)
- **Logging**: Serilog
- **Testing**: xUnit, Moq
- **Database**: PostgreSQL (shared with device-monitor for device tracking)

### 3. **Core Dependencies** (NuGet)
```xml
<PackageReference Include="Microsoft.ML.OnnxRuntime.DirectML" Version="1.16.3" />
<PackageReference Include="Vortice.Windows" Version="3.3.4" />
<PackageReference Include="Vortice.Direct3D12" Version="3.3.4" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="Npgsql" Version="8.0.3" />
```

### 4. **API Endpoints Design**

#### Memory Operations
- `GET /api/memory/status` - Get current memory status for all devices
- `GET /api/memory/device/{deviceId}` - Get memory status for specific device
- `POST /api/memory/allocate` - Allocate memory on device
- `DELETE /api/memory/deallocate/{allocationId}` - Deallocate memory
- `POST /api/memory/copy` - Copy data between allocations
- `POST /api/memory/clear/{allocationId}` - Clear memory allocation

#### Inference Operations
- `POST /api/inference/load-model` - Load ONNX model to device
- `GET /api/inference/models` - List loaded models
- `POST /api/inference/run` - Run inference on loaded model
- `DELETE /api/inference/unload-model/{modelId}` - Unload model
- `GET /api/inference/status/{sessionId}` - Get inference status

#### Training Operations
- `POST /api/training/initialize` - Initialize training session
- `POST /api/training/step` - Execute training step
- `GET /api/training/status/{sessionId}` - Get training status
- `POST /api/training/checkpoint` - Save checkpoint
- `DELETE /api/training/session/{sessionId}` - End training session

### 5. **Implementation Phases**

#### Phase 1: Project Setup & Core Infrastructure ✅ COMPLETED
1. ✅ Create project structure
2. ✅ Set up ASP.NET Core Web API with Swagger
3. ✅ Implement DirectML initialization and device enumeration (REAL HARDWARE)
4. ✅ Create base service interfaces and dependency injection
5. ✅ Set up logging and exception handling middleware
6. ✅ Basic health check endpoint

#### Phase 2: Memory Operations ✅ COMPLETED
1. ✅ Implement Direct3D12 device management
2. ✅ Create memory allocation service
3. ✅ Implement memory status tracking
4. ✅ Add memory copy/clear operations
5. ✅ Create memory controller endpoints
6. ✅ Unit tests for memory operations

#### Phase 3: Inference Operations ✅ COMPLETED
1. ✅ Implement ONNX Runtime integration
2. ✅ Create model loader service
3. ✅ Implement inference session management
4. ✅ Add support for different model types (SDXL, VAE, etc.)
5. ✅ Create inference controller endpoints
6. ✅ Performance benchmarking

#### Phase 4: Training Operations ✅ COMPLETED
1. ✅ Implement training infrastructure
2. ✅ Create gradient computation service
3. ✅ Add checkpoint management
4. ✅ Implement training controller endpoints
5. ✅ Integration with inference for validation

#### Phase 5: Integration & Testing 🚀 IN PROGRESS
1. 🔄 Integration with device-monitor service
2. 🔄 End-to-end testing
3. 🔄 Performance optimization
4. 🔄 Documentation

### 6. **Key Design Decisions**

1. **Stateful Service**: Keep models and allocations in memory between requests for performance
2. **Device Abstraction**: Abstract DirectML/D3D12 behind interfaces for testability
3. **Async Operations**: All GPU operations should be async with status endpoints
4. **Resource Management**: Implement proper disposal and cleanup for GPU resources
5. **Multi-GPU Support**: Design APIs to handle multiple devices from the start

### 7. **Configuration Structure**
```json
{
  "DeviceOperations": {
    "MaxMemoryAllocationGB": 24,
    "DefaultDevice": "gpu_0",
    "ModelCachePath": "./models",
    "EnableGPUScheduling": true,
    "Inference": {
      "MaxConcurrentSessions": 5,
      "SessionTimeoutMinutes": 30
    },
    "Training": {
      "CheckpointPath": "./checkpoints",
      "MaxBatchSize": 32
    }
  }
}
```