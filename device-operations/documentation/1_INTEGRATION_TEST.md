# Enhanced SDXL Workers Integration Test

## Test Overview
This document outlines testing procedures for the enhanced SDXL workers integration with the C# orchestrator.

## Prerequisites
- Python environment with required dependencies installed
- GPU with DirectML or CUDA support
- .NET 8.0 SDK
- Required Python packages (see requirements.txt in Workers directory)

## Test Endpoints

### 1. Health Check
```http
GET /health
```
Expected: 200 OK with status "healthy"

### 2. Enhanced SDXL Capabilities
```http
GET /api/sdxl/enhanced/capabilities
```
Expected: JSON response with supported features, schedulers, formats, and device info

### 3. Enhanced SDXL Request Validation
```http
POST /api/sdxl/enhanced/validate
Content-Type: application/json

{
  "prompt": "A beautiful landscape",
  "negativePrompt": "blurry, low quality",
  "width": 1024,
  "height": 1024,
  "steps": 20,
  "guidanceScale": 7.5,
  "seed": 42
}
```
Expected: JSON response with validation results

### 4. Enhanced SDXL Image Generation
```http
POST /api/sdxl/enhanced/generate
Content-Type: application/json

{
  "prompt": "A beautiful landscape with mountains and lakes",
  "negativePrompt": "blurry, low quality, distorted",
  "width": 1024,
  "height": 1024,
  "steps": 20,
  "guidanceScale": 7.5,
  "seed": 42,
  "numImages": 1,
  "scheduler": "euler_a",
  "qualityBoost": {
    "enabled": true,
    "strength": 0.7
  },
  "styleControls": {
    "enabled": true,
    "style": "photorealistic"
  }
}
```
Expected: JSON response with generated image paths and metadata

### 5. Supported Schedulers
```http
GET /api/sdxl/enhanced/schedulers
```
Expected: JSON array of supported scheduler names

### 6. ControlNet Types
```http
GET /api/sdxl/enhanced/controlnet-types
```
Expected: JSON array of supported ControlNet types

### 7. Performance Estimation
```http
POST /api/sdxl/enhanced/estimate
Content-Type: application/json

{
  "width": 1024,
  "height": 1024,
  "steps": 20,
  "numImages": 1,
  "complexityFactors": {
    "useControlNet": false,
    "useQualityBoost": true,
    "useStyleControls": true
  }
}
```
Expected: JSON response with estimated generation time and resource usage

## Test Steps

### Step 1: Build and Run Application
1. Open terminal in device-operations directory
2. Run: `dotnet build`
3. Verify no compilation errors
4. Run: `dotnet run`
5. Verify application starts and initializes services

### Step 2: Test Basic Health
1. Open browser or use curl: `curl http://localhost:5000/health`
2. Verify 200 response with healthy status

### Step 3: Test Worker Initialization
1. Check logs for worker initialization messages
2. Test capabilities endpoint
3. Verify GPU worker is responding

### Step 4: Test Enhanced SDXL Pipeline
1. Test validation endpoint with sample request
2. Test generation endpoint with simple prompt
3. Verify images are generated and saved
4. Check performance metrics

### Step 5: Test Advanced Features
1. Test with ControlNet controls
2. Test with style controls
3. Test with quality boost
4. Test with different schedulers

## Expected Outputs

### Successful Integration Signs
- ✅ No compilation errors
- ✅ Application starts without errors
- ✅ Worker processes initialize correctly
- ✅ All API endpoints respond correctly
- ✅ Images generate successfully
- ✅ Performance metrics are reported
- ✅ Python workers communicate properly with C# orchestrator

### Common Issues and Solutions

#### Issue: Python worker fails to start
**Solution**: Check Python environment and dependencies
```bash
cd src/Workers
python -m pip install -r requirements.txt
python main.py --worker pipeline_manager --log-level DEBUG
```

#### Issue: GPU not detected
**Solution**: Check DirectML/CUDA installation and GPU drivers

#### Issue: Model loading fails
**Solution**: Ensure model files are in correct directory and accessible

#### Issue: Generation timeout
**Solution**: Increase timeout values in configuration or reduce complexity

## Performance Benchmarks

### Basic Generation (1024x1024, 20 steps)
- Expected time: 10-30 seconds (depending on GPU)
- Memory usage: 4-8GB VRAM
- CPU usage: Moderate during generation

### Advanced Generation (with all features)
- Expected time: 20-60 seconds
- Memory usage: 6-12GB VRAM
- CPU usage: Higher during preprocessing

## Success Criteria
1. All endpoints return expected responses
2. Image generation completes successfully
3. Performance is within acceptable ranges
4. No memory leaks or crashes
5. Proper error handling and logging
6. Clean shutdown when application is stopped

## Monitoring and Debugging

### Log Files
- Application logs: `logs/device-operations-[date].txt`
- Worker logs: Check console output and stderr

### Performance Monitoring
- Check GPU memory usage during generation
- Monitor CPU and system memory
- Track generation times and success rates

### Debug Mode
Enable debug logging by setting log level to DEBUG in configuration or command line arguments.
