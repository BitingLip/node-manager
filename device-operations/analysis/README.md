# Architecture

## System Overview
The Device Operations system is a **dual-language orchestration platform** that combines a C# .NET REST API orchestrator with Python-based ML execution workers. The architecture follows an **atomic domain separation** pattern where each controller manages a single responsibility domain.

## Architecture Patterns

### Primary Pattern: Atomic Domain Controllers
```
External Request → Controller → Service → Python Worker → Response
```
- **Atomic Controllers**: Each controller handles one domain (Device, Memory, Model, Inference, Processing, Postprocessing)
- **Service Coordination**: Services can interact with each other but controllers only use their respective service
- **Cross-Cutting Concerns**: Middleware handles logging, error handling, and authentication across all domains

### ML Pipeline Flow
```
Device Discovery → Memory Allocation → Model Loading → Inference Execution → Postprocessing → Output
     ↓                    ↓                ↓                ↓                    ↓            ↓
ControllerDevice → ControllerMemory → ControllerModel → ControllerInference → ControllerPostprocessing → Result
```

## Communication Architecture

### External Communication (REST API)
- **Protocol**: HTTP/HTTPS REST API
- **Format**: JSON request/response bodies
- **Authentication**: Middleware-based authentication
- **Error Handling**: Standardized error responses with detailed error codes

### Internal Communication (C# ↔ Python)
- **Protocol**: STDIN/STDOUT pipe communication
- **Format**: JSON serialized commands and responses
- **Process Management**: Managed Python worker processes
- **Resource Isolation**: Separate processes for different worker types

### Inter-Service Communication (C# Internal)
- **Pattern**: Direct service injection and method calls
- **Coordination**: Services can call other services for complex operations
- **Transaction Management**: Service-level transaction coordination

## C# .NET Orchestrator Layers

### 1. **Presentation Layer** (Controllers)
- **Purpose**: HTTP request handling and response formatting
- **Pattern**: Atomic domain controllers with REST endpoints
- **Responsibilities**: Input validation, routing, response serialization
- **Controllers**: Device, Memory, Model, Inference, Processing, Postprocessing

### 2. **Cross-Cutting Concerns** (Middleware & Extensions)
- **Error Handling**: Global exception handling and error response formatting
- **Logging**: Request/response logging and operation tracking
- **Authentication**: Security validation and user context
- **Service Registration**: Dependency injection configuration

### 3. **Business Logic Layer** (Services)
- **Purpose**: Core business rules and orchestration logic
- **Pattern**: Service interfaces with implementation classes
- **Responsibilities**: Python worker coordination, business rule enforcement, state management
- **Coordination**: Services can interact with other services for complex workflows

### 4. **Data Transfer Layer** (Models)
- **Request Models**: Input validation and parameter binding
- **Response Models**: Output formatting and serialization
- **Common Models**: Shared data structures and domain entities
- **Error Models**: Standardized error response structures

### 5. **Integration Layer** (Python Communication)
- **PythonWorkerService**: STDIN/STDOUT communication management
- **Process Management**: Worker process lifecycle control
- **Message Serialization**: JSON command and response handling
- **Error Translation**: Python error to C# exception mapping

## Python Execution Environment Layers

### 1. **Interface Layer** (Unified Entry Points)
- **Purpose**: Single entry point coordination for all worker types
- **Pattern**: Interface classes that route to appropriate instructors
- **Responsibilities**: Request routing, response aggregation, error handling

### 2. **Instructor Layer** (Operation Coordination)
- **Purpose**: High-level operation orchestration and workflow management
- **Pattern**: Coordinator classes that manage complex multi-step operations
- **Responsibilities**: Resource coordination, operation sequencing, state management

### 3. **Manager Layer** (Resource Lifecycle)
- **Purpose**: Resource management and lifecycle control
- **Pattern**: Manager classes that handle specific resource types
- **Responsibilities**: Model loading/unloading, memory management, device allocation

### 4. **Worker Layer** (Task Execution)
- **Purpose**: Specialized task execution and ML operations
- **Pattern**: Worker classes that perform specific ML tasks
- **Responsibilities**: Model inference, image processing, scheduling operations

## Domain Architecture Mapping

### Atomic Domain Separation
Each domain represents a distinct area of responsibility with dedicated controller, service, and worker components:

| Domain | Responsibility | C# Controller | C# Service | Python Workers |
|--------|---------------|---------------|------------|----------------|
| **Device** | Hardware discovery, monitoring, control | ControllerDevice | ServiceDevice | device workers |
| **Memory** | Memory allocation, monitoring, transfer | ControllerMemory | ServiceMemory | memory workers |
| **Model** | Model loading, caching, VRAM management | ControllerModel | ServiceModel | model workers |
| **Inference** | ML inference execution and validation | ControllerInference | ServiceInference | inference workers |
| **Processing** | Workflow and batch operation coordination | ControllerProcessing | ServiceProcessing | processing coordination |
| **Postprocessing** | Image enhancement, upscaling, safety | ControllerPostprocessing | ServicePostprocessing | postprocessing workers |

### Service Interaction Patterns
- **Atomic Controllers**: Controllers only interact with their respective service
- **Service Coordination**: Services can call other services for complex operations
- **Cross-Domain Operations**: Managed through service-to-service communication
- **Python Worker Isolation**: Each worker type operates independently with dedicated processes

## Naming Conventions

### C# .NET Components
**Word Pattern**: `Type` + `Domain` + `Property`
- **Controllers**: PascalCase `ControllerDevice`, `ControllerMemory`
- **Services**: PascalCase `ServiceDevice`, `ServiceMemory`
- **Service Interfaces**: PascalCase `IServiceDevice`, `IServiceMemory`
- **Models**: PascalCase `DeviceInfo`, `MemoryAllocation`
- **Request Models**: PascalCase `RequestsDevice`, `RequestsMemory`
- **Response Models**: PascalCase `ResponsesDevice`, `ResponsesMemory`
- **Parameters**: camelCase `idDevice`, `allocationId`
- **Middleware**: PascalCase `MiddlewareErrorHandling`, `MiddlewareLogging`
- **Extensions**: PascalCase `ExtensionsServiceCollection`

### Python Worker Components  
**Word Pattern**: `type` + `domain` + `property`
- **Interfaces**: snake_case `interface_device`, `interface_memory`
- **Instructors**: snake_case `instructor_device`, `instructor_memory`
- **Managers**: snake_case `manager_device`, `manager_memory`
- **Workers**: snake_case `worker_device`, `worker_memory`
- **Files**: snake_case `device_monitor.py`, `memory_allocator.py`

## Request Lifecycle & Execution Flow

### Standard Request Flow
```
1. HTTP Request → Middleware (Auth, Logging) 
2. → Controller (Validation, Routing)
3. → Service (Business Logic, Coordination)
4. → Python Worker (STDIN/STDOUT Communication)
5. → Worker Execution (ML Operations)
6. → Response Serialization → HTTP Response
```

### ML Inference Pipeline Flow
```
Complete Inference Operation:
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│ Device Check    │ -> │ Memory Allocation│ -> │ Model Loading   │
│ ControllerDevice│    │ ControllerMemory │    │ ControllerModel │
└─────────────────┘    └──────────────────┘    └─────────────────┘
           ↓                       ↓                       ↓
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│ Inference Exec  │ -> │ Postprocessing   │ -> │ Output Storage  │
│ ControllerInfer │    │ ControllerPostpr │    │ File System     │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### Batch Processing Flow
```
Workflow Coordination (ControllerProcessing):
┌─────────────────┐
│ Batch Creation  │ → Session Management → Resource Allocation
└─────────────────┘              ↓                    ↓
                           ┌─────────────┐    ┌─────────────┐
                           │ Queue Items │ →  │ Execute     │
                           │ Management  │    │ Batch Items │
                           └─────────────┘    └─────────────┘
```

### Error Handling & Recovery
- **Controller Level**: Input validation and parameter checking
- **Service Level**: Business rule validation and retry logic  
- **Python Worker Level**: ML operation error handling and recovery
- **Middleware Level**: Global exception handling and error response formatting

## Project Structure

```
logs/                                           # Logs ✅ implemented
models/                                         # Model binaries and weights ✅ implemented
outputs/                                        # Generated outputs from inference ✅ implemented
device-operations/
├── Program.cs                                  # Main application entry point and API configuration ⚠️ Needs modification
├── DeviceOperations.csproj                     # .NET 8 project file with ML.NET and DirectML dependencies ⚠️ Needs modification
├── device-operations.sln                       # Visual Studio solution file ⚠️ Needs modification
│
├── bin/                                        # Compiled binaries and runtime files ✅ Completely implemented
│   ├── Debug/                                  # Debug build outputs
│   └── Release/                                # Release build outputs   
│         
├── config/                                     # Configuration files ✅ Completely implemented
│   ├── appsettings.json                        # Base application settings
│   ├── appsettings.Development.json            # Development environment settings
│   ├── appsettings.Production.json             # Production environment settings
│   └── workers_config.json                     # Python worker configuration
│         
├── src/                                        # Source code directory
│   ├── Controllers/                            # API Controllers ⚠️ Needs to be Created
│   │   ├── ControllerDevice.cs                 # Device management endpoints
│   │   ├── ControllerMemory.cs                 # Memory allocation and monitoring endpoints
│   │   ├── ControllerModel.cs                  # Model loading/unloading/caching endpoints
│   │   ├── ControllerProcessing.cs             # Processing pipeline coordination endpoints
│   │   ├── ControllerInference.cs              # Inference execution endpoints
│   │   └── ControllerPostprocessing.cs         # Post-processing operations endpoints
│   ├── Models/                                 # Data models and DTOs ⚠️ Needs to be Created
│   │   ├── Common/
│   │   │   ├── DeviceInfo.cs                   # Device information model
│   │   │   ├── MemoryInfo.cs                   # Memory status and allocation models
│   │   │   ├── ModelInfo.cs                    # Model metadata and status models
│   │   │   ├── InferenceSession.cs             # Inference session tracking model
│   │   │   ├── ApiResponse.cs                  # Standardized API response wrapper
│   │   │   └── ErrorDetails.cs                 # Error response model
│   │   ├── Requests/
│   │   │   ├── RequestsDevice.cs               # Device operation request models
│   │   │   ├── RequestsMemory.cs               # Memory allocation request models
│   │   │   ├── RequestsModel.cs                # Model loading/caching request models
│   │   │   ├── RequestsInference.cs            # Inference execution request models
│   │   │   └── RequestsPostprocessing.cs       # Post-processing request models
│   │   └── Responses/
│   │       ├── ResponsesDevice.cs              # Device operation response models
│   │       ├── ResponsesMemory.cs              # Memory status response models
│   │       ├── ResponsesModel.cs               # Model status/cache response models
│   │       ├── ResponsesInference.cs           # Inference result response models
│   │       └── ResponsesPostprocessing.cs      # Post-processing result response models
│   ├── Services/                               # Business logic services  ⚠️ Needs to be Created
│   │   ├── Device/
│   │   │   ├── IServiceDevice.cs               # Device service interface
│   │   │   └── ServiceDevice.cs                # Device service implementation
│   │   ├── Memory/
│   │   │   ├── IServiceMemory.cs               # Memory service interface
│   │   │   └── ServiceMemory.cs                # Memory service implementation
│   │   │   ├── IServiceModel.cs                # Model service interface
│   │   │   └── ServiceModel.cs                 # Model service implementation
│   │   ├── Processing/
│   │   │   ├── IServiceProcessing.cs           # Processing service interface
│   │   │   └── ServiceProcessing.cs            # Processing service implementation
│   │   ├── Inference/
│   │   │   ├── IServiceInference.cs            # Inference service interface
│   │   │   └── ServiceInference.cs             # Inference service implementation
│   │   ├── Postprocessing/
│   │   │   ├── IServicePostprocessing.cs       # Post-processing service interface
│   │   │   └── ServicePostprocessing.cs        # Post-processing service implementation
│   │   ├── Environment/                        # Python environment management
│   │   │   ├── IServiceEnvironment.cs          # Environment service interface
│   │   │   └── ServiceEnvironment.cs           # Python venv service - creates venv in src/workers/venv based on requirements.txt
│   │   └── Python/                             # Python worker communication
│   │       ├── IPythonWorkerService.cs         # Python worker communication interface
│   │       └── PythonWorkerService.cs          # STDIN/STDOUT communication with Python workers
│   │   
│   ├── Workers/                                # Python worker integration ✅ Completely implemented
│   │   ├── __init__.py                         # Package initialization
│   │   ├── main.py                             # Main entry point for worker processes
│   │   ├── interface_main.py                   # Unified interface for all worker types
│   │   ├── instructors/                        # Instruction coordination layer
│   │   │   ├── __init__.py                     
│   │   │   ├── instructor_device.py           # Device management coordinator
│   │   │   ├── instructor_communication.py    # Communication management coordinator
│   │   │   ├── instructor_model.py            # Model management coordinator
│   │   │   ├── instructor_conditioning.py     # Conditioning tasks coordinator
│   │   │   ├── instructor_inference.py        # Inference management coordinator
│   │   │   ├── instructor_scheduler.py        # Scheduler management coordinator
│   │   │   └── instructor_postprocessing.py   # Post-processing coordinator
│   │   ├── device/                            # Device management layer
│   │   │   ├── __init__.py                     
│   │   │   ├── interface_device.py            # Device interface
│   │   │   └── managers/
│   │   │       ├── __init__.py                 
│   │   │       └── manager_device.py          # Device management implementation
│   │   ├── communication/                     # Communication management layer
│   │   │   ├── __init__.py                     
│   │   │   ├── interface_communication.py     # Communication interface
│   │   │   └── managers/
│   │   │       ├── __init__.py                 
│   │   │       └── manager_communication.py   # Communication implementation
│   │   ├── models/                            # Model management layer
│   │   │   ├── __init__.py                     
│   │   │   ├── interface_model.py             # Model operations interface
│   │   │   ├── managers/                      # Model resource managers
│   │   │   │   ├── __init__.py                 
│   │   │   │   ├── manager_vae.py              # VAE model management
│   │   │   │   ├── manager_encoder.py          # Text encoder management
│   │   │   │   ├── manager_unet.py             # UNet model management
│   │   │   │   ├── manager_tokenizer.py        # Tokenizer management
│   │   │   │   └── manager_lora.py             # LoRA adapter management
│   │   │   └── workers/                       # Model execution workers
│   │   │       ├── __init__.py                 
│   │   │       └── worker_memory.py            # Memory management worker
│   │   ├── conditioning/                      # Conditioning processing layer
│   │   │   ├── __init__.py                     
│   │   │   ├── interface_conditioning.py      # Conditioning interface
│   │   │   ├── managers/                      # Conditioning resource managers
│   │   │   │   ├── __init__.py                 
│   │   │   │   └── manager_conditioning.py    # Conditioning lifecycle management
│   │   │   └── workers/                       # Conditioning execution workers
│   │   │       ├── __init__.py                 
│   │   │       ├── worker_prompt_processor.py  # Prompt processing worker
│   │   │       ├── worker_controlnet.py        # ControlNet conditioning worker
│   │   │       └── worker_img2img.py           # Image-to-image conditioning worker
│   │   ├── inference/                         # Inference processing layer
│   │   │   ├── __init__.py                     
│   │   │   ├── interface_inference.py         # Inference interface
│   │   │   ├── managers/                      # Inference resource managers
│   │   │   │   ├── __init__.py                 
│   │   │   │   ├── manager_batch.py            # Batch processing management
│   │   │   │   ├── manager_pipeline.py         # Pipeline lifecycle management
│   │   │   │   └── manager_memory.py           # Memory optimization management
│   │   │   └── workers/                       # Inference execution workers
│   │   │       ├── __init__.py                 
│   │   │       ├── worker_sdxl.py              # SDXL inference worker
│   │   │       ├── worker_controlnet.py        # ControlNet inference worker
│   │   │       └── worker_lora.py              # LoRA inference worker
│   │   ├── schedulers/                        # Scheduler management layer
│   │   │   ├── __init__.py                     
│   │   │   ├── interface_scheduler.py         # Scheduler interface
│   │   │   ├── managers/                      # Scheduler resource managers
│   │   │   │   ├── __init__.py                 
│   │   │   │   ├── manager_factory.py          # Scheduler factory management
│   │   │   │   └── manager_scheduler.py        # Scheduler lifecycle management
│   │   │   └── workers/                       # Scheduler execution workers
│   │   │       ├── __init__.py                 
│   │   │       ├── worker_ddim.py              # DDIM scheduler worker
│   │   │       ├── worker_dpm_plus_plus.py     # DPM++ scheduler worker
│   │   │       └── worker_euler.py             # Euler scheduler worker
│   │   ├── postprocessing/                    # Post-processing layer
│   │   │   ├── __init__.py                     
│   │   │   ├── interface_postprocessing.py    # Post-processing interface
│   │   │   ├── managers/                      # Post-processing resource managers
│   │   │   │   ├── __init__.py                 
│   │   │   │   └── manager_postprocessing.py  # Post-processing lifecycle management
│   │   │   └── workers/                       # Post-processing execution workers
│   │   │       ├── __init__.py                 
│   │   │       ├── worker_upscaler.py          # Image upscaling worker
│   │   │       ├── worker_image_enhancer.py    # Image enhancement worker
│   │   │       └── worker_safety_checker.py    # Safety checking worker
│   │   ├── utilities/                         # Support utilities layer
│   │   │   ├── __init__.py                     
│   │   │   └── dml_patch.py                    # DirectML patches and CUDA interception
│   │   ├── workers_config.json                # Hierarchical configuration template
│   │   ├── compatibility.py                   # Backward compatibility layer
│   │   ├── migration_backup/                  # Migration backup directory
│   │   └── __pycache__/                       # Python bytecode cache
│   ├── Extensions/                             # Extension methods and utilities ⚠️ Needs to be Created
│   │   ├── ExtensionsServiceCollection.cs     # Service registration extensions
│   │   ├── ExtensionsConfiguration.cs         # Configuration helper extensions
│   │   └── ExtensionsApplicationBuilder.cs    # Middleware configuration extensions
│   ├── Middleware/                             # ASP.NET Core middleware ⚠️ Needs to be Created
│   │   ├── MiddlewareErrorHandling.cs          # Global error handling middleware
│   │   ├── MiddlewareLogging.cs               # Request/response logging middleware
│   │   └── MiddlewareAuthentication.cs         # Authentication handling middleware
│   └── __init__.py                             # Python package initialization
│
├── schemas/                                 # API schemas and validation
│   ├── shema_prompt_submission.json         # Prompt submission validation schema
│   └── example_prompt.json                  # Example prompt format
│
├── tests/                                   # Unit and integration tests (currently empty)
│
├── obj/                                     # Build artifacts and temporary files
│
├── CLEANUP_COMPLETE.md                      # Cleanup operation completion log
├── CLEANUP_SUCCESS_SUMMARY.md               # Cleanup success summary
└── RENAMING_COMPLETE.md                     # File renaming operation log
```


# Domains
## Device
### ControllerDevice
#### Dependencies
```
ControllerDevice : ControllerBase
├── IServiceDevice _serviceDevice
└── ILogger<ControllerDevice> _logger
```
#### Route
`/api/device/`
#### Endpoints

| HTTP Method | Endpoint                          | Function                  | Parameters         | Targets                                           | Description                                    |
| ----------- | --------------------------------- | ------------------------- | ------------------ | ------------------------------------------------- | ---------------------------------------------- |
| **Device Discovery and Information** |
| GET         | list                              | GetDeviceList             | None               | _serviceDevice.GetDeviceListAsync()               | Get list of all available devices              |
| GET         | capabilities                      | GetDeviceCapabilities     | None               | _serviceDevice.GetDeviceCapabilitiesAsync()       | Get capabilities summary for all devices       |
| GET         | capabilities/{idDevice}           | GetDeviceCapabilities     | idDevice           | _serviceDevice.GetDeviceCapabilitiesAsync(idDevice) | Get detailed capabilities for specific device |
| **Device Status Monitoring** |
| GET         | status                            | GetDeviceStatus           | None               | _serviceDevice.GetDeviceStatusAsync()             | Get status of all devices                      |
| GET         | status/{idDevice}                 | GetDeviceStatus           | idDevice           | _serviceDevice.GetDeviceStatusAsync(idDevice)     | Get detailed status of specific device         |
| GET         | health                            | GetDeviceHealth           | None               | _serviceDevice.GetDeviceHealthAsync()             | Get health metrics for all devices             |
| GET         | health/{idDevice}                 | GetDeviceHealth           | idDevice           | _serviceDevice.GetDeviceHealthAsync(idDevice)     | Get health metrics for specific device         |
| **Device Control Operations** |
| POST        | control/{idDevice}/power          | PostDevicePower           | idDevice, Request  | _serviceDevice.PostDevicePowerAsync(idDevice, request) | Control device power state (enable/disable) |
| POST        | control/{idDevice}/reset          | PostDeviceReset           | idDevice, Request  | _serviceDevice.PostDeviceResetAsync(idDevice, request) | Reset device to default state             |
| POST        | control/{idDevice}/optimize       | PostDeviceOptimize        | idDevice, Request  | _serviceDevice.PostDeviceOptimizeAsync(idDevice, request) | Optimize device performance settings    |
| **Device Information Details** |
| GET         | details                           | GetDeviceDetails          | None               | _serviceDevice.GetDeviceDetailsAsync()            | Get detailed information for all devices       |
| GET         | details/{idDevice}                | GetDeviceDetails          | idDevice           | _serviceDevice.GetDeviceDetailsAsync(idDevice)    | Get detailed information for specific device   |
| GET         | drivers                           | GetDeviceDrivers          | None               | _serviceDevice.GetDeviceDriversAsync()            | Get driver information for all devices         |
| GET         | drivers/{idDevice}                | GetDeviceDrivers          | idDevice           | _serviceDevice.GetDeviceDriversAsync(idDevice)    | Get driver information for specific device     |

- **GetDeviceList** is responsible for:
    - Retrieving a list of all available devices in the system
    - Providing basic device identifiers and availability status
    - Enumerating both CPU and GPU devices with their IDs

- **GetDeviceCapabilities** is responsible for:
    - Retrieving device capabilities and feature support
    - Providing hardware specifications and limitations
    - Listing supported operations for each device (inference, postprocessing, etc.)

- **GetDeviceStatus** is responsible for:
    - Retrieving current operational status of devices
    - Providing real-time device state information (active, idle, busy, error)
    - Monitoring device availability and responsiveness

- **GetDeviceHealth** is responsible for:
    - Retrieving device health metrics and performance indicators
    - Providing temperature, utilization, and error statistics
    - Monitoring device stability and performance trends

- **PostDevicePower** is responsible for:
    - Controlling device power states (enable, disable, sleep)
    - Managing device availability for operations
    - Handling power management operations

- **PostDeviceReset** is responsible for:
    - Resetting devices to default operational state
    - Clearing device errors and recovering from fault states
    - Reinitializing device drivers and connections

- **PostDeviceOptimize** is responsible for:
    - Optimizing device performance settings
    - Applying device-specific performance configurations
    - Tuning device parameters for optimal operation

- **GetDeviceDetails** is responsible for:
    - Retrieving comprehensive device information and specifications
    - Providing hardware details, memory information, and driver versions
    - Offering detailed device metadata for system analysis

- **GetDeviceDrivers** is responsible for:
    - Retrieving device driver information and versions
    - Providing driver compatibility and update status
    - Monitoring driver health and installation status

### ServiceDevice
**Core Responsibilities**: Hardware discovery, monitoring, control, and optimization

#### Key Functionalities
**Device Discovery & Enumeration**
- Enumerate all available CPU and GPU devices in the system
- Detect device types (CPU, NVIDIA GPU, AMD GPU, Intel GPU)
- Retrieve device identifiers, names, and basic specifications
- Monitor device availability and connection status

**Device Information & Capabilities**
- Query device hardware specifications (memory, compute units, clock speeds)
- Retrieve device capabilities (supported operations, feature sets)
- Get driver information, versions, and compatibility status
- Provide device metadata for system analysis and optimization

**Device Status & Health Monitoring**
- Monitor real-time device operational status (active, idle, busy, error)
- Track device utilization metrics (GPU/CPU usage, memory consumption)
- Monitor device health indicators (temperature, power consumption)
- Detect and report device errors or failure states

**Device Control & Management**
- Control device power states (enable, disable, sleep, wake)
- Reset devices to default operational state
- Apply device-specific performance optimizations
- Manage device availability for ML operations

#### Python Worker Integration
- Communicate with `instructor_device.py` for device operations
- Coordinate with `manager_device.py` for device lifecycle management
- Handle device-specific error conditions and recovery

#### Service Dependencies
- Uses PythonWorkerService for STDIN/STDOUT communication
- Coordinates with ServiceMemory for device memory allocation
- Provides device information to ServiceModel and ServiceInference

## Memory
### ControllerMemory
#### Dependencies
```
ControllerMemory : ControllerBase
├── IServiceMemory _serviceMemory
└── ILogger<ControllerMemory> _logger
```
#### Route
`/api/memory/`
#### Endpoints

| HTTP Method | Endpoint                          | Function                 | Parameters         | Targets                                                            | Description                                          |
| ----------- | --------------------------------- | ------------------------ | ------------------ | ------------------------------------------------------------------ | ---------------------------------------------------- |
| **Memory Status Monitoring** |
| GET         | status                            | GetMemoryStatus          | None               | _serviceMemory.GetMemoryStatusAsync()                              | Get memory status for all devices                    |
| GET         | status/{idDevice}                 | GetMemoryStatus          | idDevice           | _serviceMemory.GetMemoryStatusAsync(idDevice)                      | Get memory status for specific device                |
| GET         | usage                             | GetMemoryUsage           | None               | _serviceMemory.GetMemoryUsageAsync()                               | Get detailed memory usage across all devices         |
| GET         | usage/{idDevice}                  | GetMemoryUsage           | idDevice           | _serviceMemory.GetMemoryUsageAsync(idDevice)                       | Get detailed memory usage for specific device        |
| **Memory Allocation Management** |
| GET         | allocations                       | GetMemoryAllocations     | None               | _serviceMemory.GetMemoryAllocationsAsync()                         | Get all active memory allocations                    |
| GET         | allocations/{idDevice}            | GetMemoryAllocations     | idDevice           | _serviceMemory.GetMemoryAllocationsAsync(idDevice)                 | Get memory allocations for specific device           |
| GET         | allocations/{allocationId}        | GetMemoryAllocation      | allocationId       | _serviceMemory.GetMemoryAllocationAsync(allocationId)              | Get details of specific memory allocation            |
| POST        | allocations/allocate              | PostMemoryAllocate       | Request            | _serviceMemory.PostMemoryAllocateAsync(request)                    | Allocate memory block on optimal device              |
| POST        | allocations/allocate/{idDevice}   | PostMemoryAllocate       | idDevice, Request  | _serviceMemory.PostMemoryAllocateAsync(request, idDevice)          | Allocate memory block on specific device             |
| DELETE      | allocations/{allocationId}        | DeleteMemoryAllocation   | allocationId       | _serviceMemory.DeleteMemoryAllocationAsync(allocationId)           | Deallocate specific memory allocation                |
| **Memory Operations** |
| POST        | operations/clear                  | PostMemoryClear          | Request            | _serviceMemory.PostMemoryClearAsync(request)                       | Clear memory on all devices                          |
| POST        | operations/clear/{idDevice}       | PostMemoryClear          | idDevice, Request  | _serviceMemory.PostMemoryClearAsync(request, idDevice)             | Clear memory on specific device                      |
| POST        | operations/defragment             | PostMemoryDefragment     | Request            | _serviceMemory.PostMemoryDefragmentAsync(request)                  | Defragment memory on all devices                     |
| POST        | operations/defragment/{idDevice}  | PostMemoryDefragment     | idDevice, Request  | _serviceMemory.PostMemoryDefragmentAsync(request, idDevice)        | Defragment memory on specific device                 |
| **Memory Transfer Operations** |
| POST        | transfer                          | PostMemoryTransfer       | Request            | _serviceMemory.PostMemoryTransferAsync(request)                    | Transfer memory between devices                       |
| GET         | transfer/{transferId}             | GetMemoryTransfer        | transferId         | _serviceMemory.GetMemoryTransferAsync(transferId)                  | Get status of memory transfer operation              |

- **GetMemoryStatus** is responsible for:
    - Retrieving memory status for all devices (RAM and VRAM)
    - Providing basic memory availability and usage information
    - Monitoring overall memory health across the system

- **GetMemoryUsage** is responsible for:
    - Retrieving detailed memory usage statistics and breakdowns
    - Providing memory consumption by process, allocation type, and time
    - Offering comprehensive memory analytics for optimization

- **GetMemoryAllocations** is responsible for:
    - Retrieving all active memory allocations across devices
    - Providing allocation metadata, sizes, and allocation times
    - Tracking memory allocation lifecycle and ownership

- **GetMemoryAllocation** is responsible for:
    - Retrieving details of specific memory allocations by ID
    - Providing allocation-specific metadata and usage statistics
    - Monitoring individual allocation status and health

- **PostMemoryAllocate** is responsible for:
    - Allocating memory blocks on optimal or specific devices
    - Managing memory allocation requests with size and type specifications
    - Returning allocation handles for future reference and deallocation

- **DeleteMemoryAllocation** is responsible for:
    - Deallocating specific memory allocations by ID
    - Freeing allocated memory and updating memory availability
    - Cleaning up allocation metadata and references

- **PostMemoryClear** is responsible for:
    - Clearing memory contents on all devices or specific device
    - Freeing unused memory and resetting memory state
    - Performing memory cleanup operations

- **PostMemoryDefragment** is responsible for:
    - Defragmenting memory to optimize allocation efficiency
    - Consolidating fragmented memory blocks
    - Improving memory layout for better performance

- **PostMemoryTransfer/GetMemoryTransfer** is responsible for:
    - Transferring memory blocks between devices (CPU to GPU, GPU to GPU)
    - Managing asynchronous memory transfer operations
    - Providing transfer status and progress monitoring

### ServiceMemory
**Core Responsibilities**: Memory allocation, monitoring, transfer, and optimization across devices

#### Key Functionalities
**Memory Status & Monitoring**
- Monitor RAM and VRAM usage across all devices
- Track memory availability and allocation patterns
- Provide detailed memory usage analytics and breakdowns
- Monitor memory health and performance metrics

**Memory Allocation Management**
- Allocate memory blocks on optimal or specific devices
- Track active memory allocations with metadata and ownership
- Manage allocation lifecycle from request to deallocation
- Provide allocation handles for reference and cleanup
- Support different allocation types (model weights, tensors, buffers)

**Memory Operations & Optimization**
- Clear memory contents and free unused allocations
- Defragment memory to optimize allocation efficiency
- Consolidate fragmented memory blocks for better performance
- Implement memory pooling strategies for frequent allocations

**Memory Transfer & Synchronization**
- Transfer memory blocks between devices (CPU↔GPU, GPU↔GPU)
- Manage asynchronous memory transfer operations
- Provide transfer status monitoring and progress tracking
- Handle memory synchronization between different device types

**Memory Allocation Strategies**
- Implement optimal device selection for memory allocation
- Support priority-based allocation for critical operations
- Manage memory pressure and automatic cleanup policies
- Coordinate memory allocation with model loading requirements

#### Python Worker Integration
- Coordinate with `worker_memory.py` for memory operations
- Communicate with device managers for device-specific allocations
- Handle memory-related error conditions and recovery

#### Service Dependencies
- Requires ServiceDevice for device availability and capabilities
- Coordinates with ServiceModel for model memory requirements
- Provides memory status to ServiceInference for operation planning
- Supports ServiceProcessing for batch operation memory management


## Model
### ControllerModel
#### Dependencies
```
ControllerModel : ControllerBase
├── IServiceModel _serviceModel
└── ILogger<ControllerModel> _logger
```
#### Route
`/api/model/`
#### Endpoints

| HTTP Method | Endpoint                          | Function                 | Parameters         | Targets                                                            | Description                                                 |
| ----------- | --------------------------------- | ------------------------ | ------------------ | ------------------------------------------------------------------ | ----------------------------------------------------------- |
| **Core Model Operations** |
| GET         | status                            | GetModelStatus           | None               | _serviceModel.GetModelStatusAsync()                                | Get model status for all devices                            |
| GET         | status/{idDevice}                 | GetModelStatus           | idDevice           | _serviceModel.GetModelStatusAsync(idDevice)                        | Get model status for specific device                        |
| POST        | load                              | PostModelLoad            | Request            | _serviceModel.PostModelLoadAsync(request)                          | Load model configuration onto all devices                   |
| POST        | load/{idDevice}                   | PostModelLoad            | idDevice, Request  | _serviceModel.PostModelLoadAsync(request, idDevice)                | Load model configuration onto specific device               |
| DELETE      | unload                            | DeleteModelUnload        | None               | _serviceModel.DeleteModelUnloadAsync()                             | Unload all models from all devices                          |
| DELETE      | unload/{idDevice}                 | DeleteModelUnload        | idDevice           | _serviceModel.DeleteModelUnloadAsync(idDevice)                     | Unload model from specific device                           |
| **Model Cache Management (RAM)** |
| GET         | cache                             | GetModelCache            | None               | _serviceModel.GetModelCacheAsync()                                 | Get cached model components status from RAM                 |
| GET         | cache/{componentId}               | GetModelCacheComponent   | componentId        | _serviceModel.GetModelCacheComponentAsync(componentId)             | Get specific component cache status                         |
| POST        | cache                             | PostModelCache           | Request            | _serviceModel.PostModelCacheAsync(request)                         | Cache model components into RAM                             |
| DELETE      | cache                             | DeleteModelCache         | None               | _serviceModel.DeleteModelCacheAsync()                              | Clear all model components from RAM                         |
| DELETE      | cache/{componentId}               | DeleteModelCacheComponent| componentId        | _serviceModel.DeleteModelCacheComponentAsync(componentId)          | Clear specific component from RAM                           |
| **VRAM Load/Unload Operations** |
| POST        | vram/load                         | PostModelVramLoad        | Request            | _serviceModel.PostModelVramLoadAsync(request)                      | Load cached components to VRAM on all devices              |
| POST        | vram/load/{idDevice}              | PostModelVramLoad        | idDevice, Request  | _serviceModel.PostModelVramLoadAsync(request, idDevice)            | Load cached components to VRAM on specific device          |
| DELETE      | vram/unload                       | DeleteModelVramUnload    | Request            | _serviceModel.DeleteModelVramUnloadAsync(request)                  | Unload components from VRAM on all devices                 |
| DELETE      | vram/unload/{idDevice}            | DeleteModelVramUnload    | idDevice, Request  | _serviceModel.DeleteModelVramUnloadAsync(request, idDevice)        | Unload components from VRAM on specific device             |
| **Model Component Discovery** |
| GET         | components                        | GetModelComponents       | None               | _serviceModel.GetModelComponentsAsync()                            | List all available model components                         |
| GET         | components/{componentType}        | GetModelComponentsByType | componentType      | _serviceModel.GetModelComponentsByTypeAsync(componentType)         | Get components by type (unet, vae, encoder, etc.)          |
| GET         | available                         | GetAvailableModels       | None               | _serviceModel.GetAvailableModelsAsync()                            | Get list of available models from model directory          |
| GET         | available/{modelType}             | GetAvailableModelsByType | modelType          | _serviceModel.GetAvailableModelsByTypeAsync(modelType)             | Get available models by type (sdxl, sd15, flux, etc.)      |

- **GetModelStatus** is responsible for:
    - Retrieving the current status of models loaded on all devices or a specific device
    - Providing information about model loading state, memory usage, and readiness

- **PostModelLoad/DeleteModelUnload** is responsible for:
    - Loading complete model configurations onto devices (full model setup)
    - Unloading complete model configurations from devices
    - Managing model lifecycle from configuration to active state

- **GetModelCache** is responsible for:
    - Retrieving the current cache status of model components in RAM
    - Providing information about cached components and their memory usage

- **GetModelCacheComponent** is responsible for:
    - Retrieving cache status of specific model components (UNet, VAE, encoders, etc.)
    - Providing detailed information about individual component caching

- **PostModelCache/DeleteModelCache** is responsible for:
    - Caching model components into RAM for faster access
    - Clearing model components from RAM to free memory
    - Managing component-level RAM caching strategies

- **PostModelVramLoad/DeleteModelVramUnload** is responsible for:
    - Loading cached model components from RAM to VRAM for inference
    - Unloading model components from VRAM while preserving RAM cache
    - Managing VRAM allocation for optimal inference performance

- **GetModelComponents** is responsible for:
    - Listing all available model components in the system
    - Providing component metadata and availability information

- **GetAvailableModels** is responsible for:
    - Discovering available models in the model directory
    - Filtering models by type (SDXL, SD1.5, Flux, etc.)
    - Providing model metadata and compatibility information

### ServiceModel
**Core Responsibilities**: Model loading, caching, VRAM management, and component discovery

#### Key Functionalities
**Model Status & Lifecycle Management**
- Track model loading status across all devices
- Manage complete model configurations and their lifecycle
- Monitor model readiness and operational state
- Handle model loading/unloading operations with proper cleanup

**Model Cache Management (RAM)**
- Cache model components in system RAM for faster access
- Manage component-level caching (UNet, VAE, encoders, tokenizers, LoRA)
- Track cached component status, memory usage, and access patterns
- Implement cache eviction policies and memory optimization
- Support selective component caching based on usage patterns

**VRAM Loading & Optimization**
- Load cached components from RAM to device VRAM for inference
- Manage VRAM allocation and deallocation for model components
- Optimize VRAM usage based on inference requirements
- Support partial model loading for memory-constrained devices
- Handle VRAM fragmentation and optimization

**Model Discovery & Metadata**
- Discover available models in the model directory structure
- Parse model metadata and configuration files
- Categorize models by type (SDXL, SD1.5, Flux, ControlNet, LoRA)
- Validate model compatibility and requirements
- Provide model component mapping and dependency tracking

**Component Management**
- Manage individual model components (UNet, VAE, text encoders)
- Handle component dependencies and loading order
- Support component sharing across different model configurations
- Manage LoRA adapters and their application to base models
- Track component versions and compatibility matrices

#### Python Worker Integration
- Coordinate with `instructor_model.py` for model operations
- Communicate with model managers (`manager_vae.py`, `manager_encoder.py`, etc.)
- Handle model-specific loading and caching operations

#### Service Dependencies
- Requires ServiceDevice for device capabilities and selection
- Uses ServiceMemory for memory allocation and management
- Provides loaded model information to ServiceInference
- Coordinates with ServiceProcessing for batch model management

## Processing
### ControllerProcessing
#### Dependencies
```
ControllerProcessing : ControllerBase
├── IServiceProcessing _serviceProcessing
└── ILogger<ControllerProcessing> _logger
```
#### Route
`/api/processing/`
#### Endpoints

| HTTP Method | Endpoint                          | Function                    | Parameters         | Targets                                                               | Description                                          |
| ----------- | --------------------------------- | --------------------------- | ------------------ | --------------------------------------------------------------------- | ---------------------------------------------------- |
| **Workflow Management** |
| GET         | workflows                         | GetProcessingWorkflows      | None               | _serviceProcessing.GetProcessingWorkflowsAsync()                      | Get all available processing workflows               |
| GET         | workflows/{workflowId}            | GetProcessingWorkflow       | workflowId         | _serviceProcessing.GetProcessingWorkflowAsync(workflowId)             | Get details of a specific workflow                  |
| POST        | workflows/execute                 | PostWorkflowExecute         | Request            | _serviceProcessing.PostWorkflowExecuteAsync(request)                  | Execute a processing workflow                        |
| **Session Management** |
| GET         | sessions                          | GetProcessingSessions       | None               | _serviceProcessing.GetProcessingSessionsAsync()                       | Get all active processing sessions                   |
| GET         | sessions/{sessionId}              | GetProcessingSession        | sessionId          | _serviceProcessing.GetProcessingSessionAsync(sessionId)               | Get details of a specific session                   |
| POST        | sessions/{sessionId}/control      | PostSessionControl          | sessionId, Request | _serviceProcessing.PostSessionControlAsync(sessionId, request)        | Control session (pause, resume, cancel)             |
| DELETE      | sessions/{sessionId}              | DeleteProcessingSession     | sessionId          | _serviceProcessing.DeleteProcessingSessionAsync(sessionId)            | Cancel and remove a processing session              |
| **Batch Operations** |
| GET         | batches                           | GetProcessingBatches        | None               | _serviceProcessing.GetProcessingBatchesAsync()                        | Get all batch operations                             |
| GET         | batches/{batchId}                 | GetProcessingBatch          | batchId            | _serviceProcessing.GetProcessingBatchAsync(batchId)                   | Get details of a specific batch                     |
| POST        | batches/create                    | PostBatchCreate             | Request            | _serviceProcessing.PostBatchCreateAsync(request)                      | Create a new batch processing operation              |
| POST        | batches/{batchId}/execute         | PostBatchExecute            | batchId, Request   | _serviceProcessing.PostBatchExecuteAsync(batchId, request)            | Execute a batch processing operation                 |
| DELETE      | batches/{batchId}                 | DeleteProcessingBatch       | batchId            | _serviceProcessing.DeleteProcessingBatchAsync(batchId)                | Cancel and remove a batch operation                 |

- **GetProcessingWorkflows** is responsible for:
    - Retrieving all available processing workflows (inference + postprocessing chains)
    - Providing workflow templates and their step definitions
    - Listing workflow capabilities and requirements

- **GetProcessingWorkflow** is responsible for:
    - Retrieving details of a specific workflow template
    - Providing step-by-step workflow configuration
    - Showing workflow input/output specifications

- **PostWorkflowExecute** is responsible for:
    - Executing complete processing workflows (multi-step operations)
    - Coordinating between inference, postprocessing, and other services
    - Managing workflow state and progress

- **GetProcessingSessions** is responsible for:
    - Retrieving all active processing sessions across the system
    - Providing session status, progress, and metadata
    - Tracking long-running operations

- **GetProcessingSession/PostSessionControl** is responsible for:
    - Managing individual processing sessions (get details, pause, resume, cancel)
    - Providing real-time session status and progress updates
    - Controlling session lifecycle and execution flow

- **GetProcessingBatches** is responsible for:
    - Retrieving all batch processing operations
    - Providing batch status, progress, and item counts
    - Managing multiple related processing tasks

- **PostBatchCreate/PostBatchExecute** is responsible for:
    - Creating and executing batch processing operations
    - Managing multiple inference/postprocessing tasks as a unit
    - Coordinating resource allocation for batch operations

### ServiceProcessing
**Core Responsibilities**: Workflow coordination, session management, and batch operation orchestration

#### Key Functionalities
**Workflow Management & Orchestration**
- Define and manage processing workflows (inference + postprocessing chains)
- Coordinate multi-step operations across different services
- Handle workflow templates and their execution parameters
- Manage workflow state and progress tracking
- Support conditional workflow execution based on results

**Session Management & Control**
- Create and manage long-running processing sessions
- Track session state, progress, and resource usage
- Provide session control operations (pause, resume, cancel)
- Handle session recovery and error handling
- Support real-time session monitoring and status updates

**Batch Processing Operations**
- Create and manage batch processing operations
- Coordinate multiple related inference/postprocessing tasks
- Implement batch optimization and resource allocation strategies
- Handle batch queue management and execution ordering
- Support batch progress tracking and partial result handling

**Cross-Service Coordination**
- Orchestrate operations across Device, Memory, Model, Inference, and Postprocessing services
- Manage resource allocation and scheduling for complex operations
- Handle service dependencies and execution ordering
- Implement workflow error handling and rollback strategies
- Coordinate resource cleanup and state management

**Processing Pipeline Management**
- Manage end-to-end processing pipelines from input to output
- Handle pipeline state transitions and checkpointing
- Support pipeline branching and conditional execution
- Implement pipeline optimization and caching strategies
- Manage pipeline resource requirements and allocation

#### Python Worker Integration
- Coordinate with all instructor layers for workflow execution
- Manage complex multi-step operations across Python workers
- Handle workflow-specific error conditions and recovery
- Support distributed processing coordination

#### Service Dependencies
- Orchestrates ServiceDevice for resource allocation
- Coordinates ServiceMemory for workflow memory management
- Uses ServiceModel for workflow model requirements
- Manages ServiceInference for inference operations
- Integrates ServicePostprocessing for enhancement workflows

## Inference
### ControllerInference
#### Dependencies
```
ControllerInference : ControllerBase
├── IServiceInference _serviceInference
└── ILogger<ControllerInference> _logger
```
#### Route
`/api/inference/`
#### Endpoints

| HTTP Method | Endpoint                          | Function                 | Parameters         | Targets                                                            | Description                                          |
| ----------- | --------------------------------- | ------------------------ | ------------------ | ------------------------------------------------------------------ | ---------------------------------------------------- |
| **Core Inference Operations** |
| GET         | capabilities                      | GetInferenceCapabilities | None               | _serviceInference.GetInferenceCapabilitiesAsync()                  | Get system inference capabilities and supported models |
| GET         | capabilities/{idDevice}           | GetInferenceCapabilities | idDevice           | _serviceInference.GetInferenceCapabilitiesAsync(idDevice)          | Get inference capabilities for specific device       |
| POST        | execute                           | PostInferenceExecute     | Request            | _serviceInference.PostInferenceExecuteAsync(request)               | Execute inference on optimal device                  |
| POST        | execute/{idDevice}                | PostInferenceExecute     | idDevice, Request  | _serviceInference.PostInferenceExecuteAsync(request, idDevice)     | Execute inference on specific device                |
| **Inference Validation** |
| POST        | validate                          | PostInferenceValidate    | Request            | _serviceInference.PostInferenceValidateAsync(request)              | Validate inference request without execution         |
| GET         | supported-types                   | GetSupportedTypes        | None               | _serviceInference.GetSupportedTypesAsync()                         | Get supported inference types (txt2img, img2img, etc.) |
| GET         | supported-types/{idDevice}        | GetSupportedTypes        | idDevice           | _serviceInference.GetSupportedTypesAsync(idDevice)                 | Get supported inference types for specific device    |

- **GetInferenceCapabilities** is responsible for:
    - Retrieving system's inference capabilities (supported models, inference types)
    - Providing device-specific inference capabilities and limitations
    - Determining which inference operations are available on each device

- **PostInferenceExecute** is responsible for:
    - Executing a single inference operation (txt2img, img2img, inpainting, etc.)
    - Running inference on optimal device or specific device
    - Returning inference results directly (synchronous operation)

- **PostInferenceValidate** is responsible for:
    - Validating inference request parameters without execution
    - Checking model compatibility and parameter validity
    - Providing validation feedback for request structure

- **GetSupportedTypes** is responsible for:
    - Listing supported inference types (txt2img, img2img, controlnet, etc.)
    - Providing type-specific parameter requirements
    - Device-specific inference type availability

### ServiceInference
**Core Responsibilities**: ML inference execution, validation, and capability management

#### Key Functionalities
**Inference Capabilities & Discovery**
- Query system inference capabilities and supported model types
- Determine device-specific inference capabilities and limitations
- Validate device compatibility with specific inference operations
- Provide capability matrices for different inference types

**Inference Execution Management**
- Execute single inference operations (txt2img, img2img, inpainting, ControlNet)
- Select optimal device for inference based on model requirements
- Manage inference session lifecycle and resource allocation
- Handle synchronous inference execution with result return
- Support device-specific inference optimizations

**Inference Type Support & Validation**
- Support multiple inference types (text-to-image, image-to-image, inpainting)
- Handle ControlNet inference with conditioning inputs
- Manage LoRA adapter application during inference
- Validate inference parameters and model compatibility
- Provide type-specific parameter validation and requirements

**Request Validation & Parameter Checking**
- Validate inference request structure and parameters
- Check model availability and compatibility
- Verify device capabilities for requested operations
- Provide detailed validation feedback and error messages
- Support parameter preprocessing and normalization

**Inference Optimization**
- Implement inference-specific optimizations (attention slicing, CPU offloading)
- Manage inference memory usage and optimization
- Support different precision modes (fp16, fp32) based on device capabilities
- Handle inference scheduling and queue management

#### Python Worker Integration
- Coordinate with `instructor_inference.py` for inference operations
- Communicate with inference workers (`worker_sdxl.py`, `worker_controlnet.py`, etc.)
- Handle conditioning through `instructor_conditioning.py`
- Manage schedulers via `instructor_scheduler.py`

#### Service Dependencies
- Requires ServiceDevice for device selection and capabilities
- Uses ServiceMemory for inference memory allocation
- Depends on ServiceModel for loaded model access
- Coordinates with ServiceProcessing for workflow integration

## Postprocessing
### ControllerPostprocessing
#### Dependencies
```
ControllerPostprocessing : ControllerBase
├── IServicePostprocessing _servicePostprocessing
└── ILogger<ControllerPostprocessing> _logger
```
#### Route
`/api/postprocessing/`
#### Endpoints

| HTTP Method | Endpoint                          | Function                       | Parameters         | Targets                                                                  | Description                                          |
| ----------- | --------------------------------- | ------------------------------ | ------------------ | ------------------------------------------------------------------------ | ---------------------------------------------------- |
| **Core Postprocessing Operations** |
| GET         | capabilities                      | GetPostprocessingCapabilities  | None               | _servicePostprocessing.GetPostprocessingCapabilitiesAsync()             | Get available postprocessing capabilities            |
| GET         | capabilities/{idDevice}           | GetPostprocessingCapabilities  | idDevice           | _servicePostprocessing.GetPostprocessingCapabilitiesAsync(idDevice)     | Get postprocessing capabilities for specific device |
| POST        | upscale                           | PostUpscale                    | Request            | _servicePostprocessing.PostUpscaleAsync(request)                        | Upscale single image using available upscalers      |
| POST        | upscale/{idDevice}                | PostUpscale                    | idDevice, Request  | _servicePostprocessing.PostUpscaleAsync(request, idDevice)              | Upscale single image on specific device             |
| POST        | enhance                           | PostEnhance                    | Request            | _servicePostprocessing.PostEnhanceAsync(request)                        | Enhance single image using enhancement models        |
| POST        | enhance/{idDevice}                | PostEnhance                    | idDevice, Request  | _servicePostprocessing.PostEnhanceAsync(request, idDevice)              | Enhance single image on specific device             |
| **Validation and Safety** |
| POST        | validate                          | PostPostprocessingValidate     | Request            | _servicePostprocessing.PostPostprocessingValidateAsync(request)         | Validate postprocessing request without execution   |
| POST        | safety-check                      | PostSafetyCheck                | Request            | _servicePostprocessing.PostSafetyCheckAsync(request)                    | Run safety checks on image content                  |
| **Model and Tool Discovery** |
| GET         | available-upscalers               | GetAvailableUpscalers          | None               | _servicePostprocessing.GetAvailableUpscalersAsync()                     | Get list of available upscaler models               |
| GET         | available-upscalers/{idDevice}    | GetAvailableUpscalers          | idDevice           | _servicePostprocessing.GetAvailableUpscalersAsync(idDevice)             | Get available upscalers for specific device         |
| GET         | available-enhancers               | GetAvailableEnhancers          | None               | _servicePostprocessing.GetAvailableEnhancersAsync()                     | Get list of available enhancement models             |
| GET         | available-enhancers/{idDevice}    | GetAvailableEnhancers          | idDevice           | _servicePostprocessing.GetAvailableEnhancersAsync(idDevice)             | Get available enhancers for specific device         |

- **GetPostprocessingCapabilities** is responsible for:
    - Retrieving available postprocessing capabilities and models
    - Providing device-specific postprocessing limitations and features
    - Listing supported postprocessing operations for each device

- **PostUpscale** is responsible for:
    - Upscaling a single image using available upscaler models
    - Supporting device-specific upscaling operations
    - Returning upscaled image results directly (synchronous operation)

- **PostEnhance** is responsible for:
    - Enhancing a single image using enhancement models
    - Supporting device-specific enhancement operations
    - Applying image quality improvements and corrections

- **PostPostprocessingValidate** is responsible for:
    - Validating postprocessing request parameters without execution
    - Checking model compatibility and parameter validity
    - Providing validation feedback for request structure

- **PostSafetyCheck** is responsible for:
    - Running safety checks on single image content
    - Detecting inappropriate or harmful content
    - Providing safety compliance feedback

- **GetAvailableUpscalers/GetAvailableEnhancers** is responsible for:
    - Discovering available postprocessing models in the system
    - Providing model metadata and compatibility information
    - Filtering models by device capabilities and availability

### ServicePostprocessing
**Core Responsibilities**: Image enhancement, upscaling, safety validation, and postprocessing model management

#### Key Functionalities
**Postprocessing Capabilities & Discovery**
- Query available postprocessing capabilities and models
- Determine device-specific postprocessing limitations and features
- Discover upscaler and enhancement models in the system
- Provide capability matrices for different postprocessing operations

**Image Upscaling Operations**
- Execute single image upscaling using available upscaler models
- Support multiple upscaling algorithms (ESRGAN, Real-ESRGAN, SwinIR)
- Handle device-specific upscaling optimizations
- Manage upscaling memory requirements and resource allocation
- Support different upscaling factors and quality settings

**Image Enhancement & Correction**
- Enhance image quality using specialized enhancement models
- Apply image corrections (denoising, sharpening, color correction)
- Support face enhancement and restoration operations
- Handle artistic style enhancement and post-effects
- Manage enhancement model loading and execution

**Safety & Content Validation**
- Run safety checks on image content using safety checker models
- Detect inappropriate or harmful content in generated images
- Provide content safety compliance feedback and scoring
- Support configurable safety thresholds and policies
- Handle safety model loading and inference

**Postprocessing Model Management**
- Manage postprocessing model loading and caching
- Handle model compatibility and device optimization
- Support model switching and dynamic loading
- Manage model memory usage and optimization
- Track model performance and usage statistics

**Request Validation & Parameter Management**
- Validate postprocessing request parameters and image inputs
- Check model availability and compatibility
- Verify device capabilities for requested operations
- Provide detailed validation feedback and error handling
- Support parameter preprocessing and optimization

#### Python Worker Integration
- Coordinate with `instructor_postprocessing.py` for postprocessing operations
- Communicate with postprocessing workers (`worker_upscaler.py`, `worker_image_enhancer.py`, etc.)
- Handle safety checking through `worker_safety_checker.py`
- Manage postprocessing model lifecycle

#### Service Dependencies
- Requires ServiceDevice for device selection and capabilities
- Uses ServiceMemory for postprocessing memory allocation
- May coordinate with ServiceModel for postprocessing model management
- Integrates with ServiceProcessing for workflow postprocessing steps

---