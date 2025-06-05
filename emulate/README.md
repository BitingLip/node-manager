# Node Manager Emulation Suite

A comprehensive testing and development framework for the BitingLip Node Manager.

## 🎯 Overview

The emulation suite provides:

- **Mock Workers**: Simulated AI workers for different model types
- **Task Emulation**: Realistic task generation and execution simulation  
- **Test Scenarios**: Pre-defined test cases for different load patterns
- **API Testing**: Client for testing Node Manager HTTP API
- **Interactive Interface**: Command-line tool for flexible testing

## 🚀 Quick Start

### 1. Basic Demo
```bash
# Run the basic demonstration
python emulate/demo.py
```

### 2. Interactive Mode
```bash
# Start interactive emulation interface
python emulate/run_emulation.py
```

### 3. Scenario Testing
```bash
# List available scenarios
python emulate/run_emulation.py --list-scenarios

# Run a specific scenario
python emulate/run_emulation.py --scenario smoke_test

# Run stress test
python emulate/run_emulation.py --stress-test 20
```

## 📁 Components

### Mock Workers (`mock_worker.py`)
Simulates different types of AI workers:
- **LLM Workers**: Small/Large language models
- **Stable Diffusion**: Image generation
- **Text-to-Speech**: Fast/Quality TTS
- **Image-to-Text**: Vision models
- **Generic**: General-purpose workers
- **Heavy Compute**: Training/Fine-tuning

### Worker & Task Emulators (`emulator.py`)
- **WorkerEmulator**: Manages fleet of mock workers
- **TaskEmulator**: Creates and executes test tasks

### Test Scenarios (`scenarios.py`)
Pre-defined test scenarios:
- **Smoke Test**: Basic functionality verification
- **Balanced Load**: Moderate load across worker types
- **Stress Test**: High load and system limits
- **Failure Recovery**: Worker failure simulation
- **Resource Constraint**: Limited workers, high demand
- **Priority Handling**: Task prioritization testing

### API Test Client (`test_client.py`)
HTTP client for Node Manager API:
- Health checks and status monitoring
- Worker management operations
- Task submission and tracking
- Resource information queries
- Stress testing capabilities

## 🎮 Interactive Commands

When running in interactive mode:

1. **List scenarios** - Show all available test scenarios
2. **Run scenario** - Execute a specific test scenario
3. **Test API connection** - Verify Node Manager connectivity
4. **Submit test task** - Send a single test task
5. **Create custom worker fleet** - Build a custom worker configuration
6. **Run stress test** - Execute concurrent tasks
7. **Show fleet status** - Display current worker status

## 📊 Example Usage

### Create Custom Worker Fleet
```python
from emulate import WorkerEmulator, MockWorkerType

emulator = WorkerEmulator()

# Create balanced fleet
fleet_config = {
    MockWorkerType.LLM_SMALL: 2,
    MockWorkerType.STABLE_DIFFUSION: 1,
    MockWorkerType.TTS_FAST: 1,
    MockWorkerType.GENERIC: 2
}

fleet = emulator.create_worker_fleet(fleet_config)
await emulator.start_all_workers()
```

### Run Task Batch
```python
from emulate import TaskEmulator

task_emulator = TaskEmulator(worker_emulator)

# Create test tasks
tasks = task_emulator.create_task_batch(10, ['text_generation', 'text_to_image'])

# Execute concurrently
results = await task_emulator.execute_task_batch(tasks)
```

### API Testing
```python
from emulate import NodeTestClient

async with NodeTestClient("http://localhost:8013") as client:
    # Health check
    health = await client.run_health_check()
    
    # Submit task
    result = await client.submit_test_task('text_generation')
    
    # Stress test
    stress_results = await client.stress_test(20)
```

## 🔧 Configuration

### Worker Performance Profiles
Each worker type has realistic performance characteristics:
- Execution time distributions
- Error rates
- Resource requirements
- Capabilities and supported tasks

### Scenario Customization
Create custom scenarios by defining:
- Worker fleet composition
- Task generation patterns
- Load patterns and timing
- Expected success criteria

## 📈 Metrics & Monitoring

The emulation system tracks:
- **Task Metrics**: Success rates, execution times, throughput
- **Worker Metrics**: Utilization, error rates, status distribution
- **System Metrics**: Resource usage, queue depths, response times

## 🎯 Use Cases

### Development Testing
- Verify new features work under load
- Test error handling and recovery
- Validate performance optimizations

### Load Testing
- Determine system capacity limits
- Test with different worker configurations
- Simulate production traffic patterns

### Integration Testing
- End-to-end API testing
- Multi-component interaction validation
- Failure scenario testing

### Performance Benchmarking
- Compare different configurations
- Measure throughput and latency
- Identify bottlenecks

## 🛠️ Extending the System

### Adding New Worker Types
```python
class CustomWorkerType(Enum):
    MY_WORKER = "my_custom_worker"

# Add capabilities and performance profile
def _get_capabilities(self):
    capabilities[CustomWorkerType.MY_WORKER] = {
        'tasks': ['my_task_type'],
        'models': ['my_model']
    }
```

### Creating Custom Scenarios
```python
custom_scenario = Scenario(
    name="My Custom Test",
    description="Tests specific functionality",
    duration_minutes=5,
    fleet_config={MockWorkerType.LLM_SMALL: 3},
    task_config={'task_rate': 10},
    expected_outcomes={'min_success_rate': 0.9}
)

scenario_runner.add_custom_scenario(custom_scenario)
```

## 🚦 Best Practices

1. **Start Small**: Begin with smoke tests before heavy loads
2. **Monitor Resources**: Watch CPU/memory during stress tests  
3. **Incremental Testing**: Gradually increase load to find limits
4. **Scenario Documentation**: Document custom scenarios and results
5. **Clean Shutdown**: Always stop workers cleanly after tests

## 🔍 Troubleshooting

### Common Issues

**Connection Errors**: Ensure Node Manager is running on correct port
```bash
# Check if Node Manager is running
curl http://localhost:8013/health
```

**Import Errors**: Verify Python path includes project root
```python
import sys
sys.path.insert(0, '/path/to/node-manager')
```

**High Error Rates**: Check worker configuration and error simulation settings

### Debug Mode
Enable debug logging for detailed output:
```python
import structlog
structlog.configure(level="DEBUG")
```

---

**Happy Testing!** 🎉

The emulation suite provides a powerful foundation for testing, development, and validation of the BitingLip Node Manager system.
