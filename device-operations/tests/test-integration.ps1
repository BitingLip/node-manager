# Enhanced SDXL Integration Test Script
# Tests the C# orchestrator with enhanced Python workers

Write-Host "Starting Enhanced SDXL Integration Tests..." -ForegroundColor Green

# Configuration
$baseUrl = "http://localhost:5000"
$timeout = 30

# Test 1: Health Check
Write-Host "`n=== Test 1: Health Check ===" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/health" -Method GET -TimeoutSec $timeout
    Write-Host "✅ Health check passed: $($response.status)" -ForegroundColor Green
} catch {
    Write-Host "❌ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Enhanced SDXL Capabilities
Write-Host "`n=== Test 2: Enhanced SDXL Capabilities ===" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/sdxl/enhanced/capabilities" -Method GET -TimeoutSec $timeout
    Write-Host "✅ Capabilities retrieved successfully" -ForegroundColor Green
    Write-Host "   - Features: $($response.features.Count)" -ForegroundColor Cyan
    Write-Host "   - Schedulers: $($response.supportedSchedulers.Count)" -ForegroundColor Cyan
    Write-Host "   - Max Resolution: $($response.maxResolution)" -ForegroundColor Cyan
} catch {
    Write-Host "❌ Capabilities test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Request Validation
Write-Host "`n=== Test 3: Request Validation ===" -ForegroundColor Yellow
$validationRequest = @{
    prompt = "A beautiful landscape with mountains and lakes"
    negativePrompt = "blurry, low quality"
    width = 1024
    height = 1024
    steps = 20
    guidanceScale = 7.5
    seed = 42
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/sdxl/enhanced/validate" -Method POST -Body $validationRequest -ContentType "application/json" -TimeoutSec $timeout
    if ($response.valid) {
        Write-Host "✅ Request validation passed" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Request validation failed: $($response.error)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Validation test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Supported Schedulers
Write-Host "`n=== Test 4: Supported Schedulers ===" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/sdxl/enhanced/schedulers" -Method GET -TimeoutSec $timeout
    Write-Host "✅ Schedulers retrieved: $($response.Count) schedulers available" -ForegroundColor Green
    $response | ForEach-Object { Write-Host "   - $_" -ForegroundColor Cyan }
} catch {
    Write-Host "❌ Schedulers test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 5: ControlNet Types
Write-Host "`n=== Test 5: ControlNet Types ===" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/sdxl/enhanced/controlnet-types" -Method GET -TimeoutSec $timeout
    Write-Host "✅ ControlNet types retrieved: $($response.Count) types available" -ForegroundColor Green
    $response | ForEach-Object { Write-Host "   - $_" -ForegroundColor Cyan }
} catch {
    Write-Host "❌ ControlNet types test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 6: Performance Estimation
Write-Host "`n=== Test 6: Performance Estimation ===" -ForegroundColor Yellow
$estimateRequest = @{
    width = 1024
    height = 1024
    steps = 20
    numImages = 1
    complexityFactors = @{
        useControlNet = $false
        useQualityBoost = $true
        useStyleControls = $true
    }
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/sdxl/enhanced/estimate" -Method POST -Body $estimateRequest -ContentType "application/json" -TimeoutSec $timeout
    Write-Host "✅ Performance estimation completed" -ForegroundColor Green
    Write-Host "   - Estimated time: $($response.estimatedTimeSeconds) seconds" -ForegroundColor Cyan
    Write-Host "   - Estimated memory: $($response.estimatedMemoryMB) MB" -ForegroundColor Cyan
} catch {
    Write-Host "❌ Performance estimation failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 7: Enhanced SDXL Generation (Optional - requires longer timeout)
Write-Host "`n=== Test 7: Enhanced SDXL Generation (Optional) ===" -ForegroundColor Yellow
$generateRequest = @{
    prompt = "A beautiful landscape with mountains and lakes, photorealistic"
    negativePrompt = "blurry, low quality, distorted"
    width = 512  # Smaller size for faster testing
    height = 512
    steps = 10   # Fewer steps for faster testing
    guidanceScale = 7.5
    seed = 42
    numImages = 1
    scheduler = "euler_a"
    qualityBoost = @{
        enabled = $true
        strength = 0.5
    }
    styleControls = @{
        enabled = $true
        style = "photorealistic"
    }
} | ConvertTo-Json

$generateTest = Read-Host "Run generation test? This may take 30+ seconds (y/n)"
if ($generateTest -eq "y" -or $generateTest -eq "Y") {
    try {
        Write-Host "Starting image generation (this may take a while)..." -ForegroundColor Cyan
        $response = Invoke-RestMethod -Uri "$baseUrl/api/sdxl/enhanced/generate" -Method POST -Body $generateRequest -ContentType "application/json" -TimeoutSec 120
        
        if ($response.success) {
            Write-Host "✅ Image generation completed successfully!" -ForegroundColor Green
            Write-Host "   - Generated images: $($response.images.Count)" -ForegroundColor Cyan
            Write-Host "   - Generation time: $($response.metrics.generationTimeSeconds) seconds" -ForegroundColor Cyan
            Write-Host "   - GPU memory used: $($response.metrics.memoryUsage.gpuMemoryMB) MB" -ForegroundColor Cyan
            
            # List generated images
            $response.images | ForEach-Object {
                Write-Host "   - Image: $($_.path)" -ForegroundColor Cyan
            }
        } else {
            Write-Host "❌ Image generation failed: $($response.error)" -ForegroundColor Red
        }
    } catch {
        Write-Host "❌ Generation test failed: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "⏭️ Skipping generation test" -ForegroundColor Yellow
}

Write-Host "`n=== Integration Test Summary ===" -ForegroundColor Green
Write-Host "Tests completed! Check above for any failures." -ForegroundColor White
Write-Host "If all tests passed, the enhanced SDXL workers are properly integrated with the C# orchestrator." -ForegroundColor Green

# Keep window open
Write-Host "`nPress any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
