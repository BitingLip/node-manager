# DeviceOperations Test Runner Script
# PowerShell script to run comprehensive testing suite

param(
    [string]$TestCategory = "All",
    [switch]$Coverage = $false,
    [switch]$Verbose = $false,
    [bool]$ParallelExecution = $true,
    [string]$OutputDirectory = "TestResults"
)

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "    DeviceOperations Test Suite Runner" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$testProject = Join-Path $projectRoot "tests\DeviceOperations.Tests.csproj"
$outputDir = Join-Path $projectRoot $OutputDirectory

# Ensure output directory exists
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Build the test project first
Write-Host "Building test project..." -ForegroundColor Yellow
dotnet build $testProject --configuration Debug --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed. Exiting." -ForegroundColor Red
    exit 1
}

# Prepare test arguments
$testArgs = @(
    "test"
    $testProject
    "--configuration", "Debug"
    "--logger", "trx;LogFileName=TestResults.trx"
    "--logger", "console;verbosity=normal"
    "--results-directory", $outputDir
)

# Add verbosity if requested
if ($Verbose) {
    $testArgs += "--verbosity", "detailed"
}

# Add parallel execution settings
if ($ParallelExecution) {
    $testArgs += "--parallel"
} else {
    $testArgs += "--parallel", "false"
}

# Add coverage if requested
if ($Coverage) {
    $testArgs += "--collect", "XPlat Code Coverage"
    $testArgs += "--settings", "tests\coverlet.runsettings"
}

# Filter tests by category
switch ($TestCategory.ToLower()) {
    "unit" {
        Write-Host "Running Unit Tests..." -ForegroundColor Green
        $testArgs += "--filter", "Category=Unit"
    }
    "integration" {
        Write-Host "Running Integration Tests..." -ForegroundColor Green
        $testArgs += "--filter", "Category=Integration"
    }
    "services" {
        Write-Host "Running Service Layer Tests..." -ForegroundColor Green
        $testArgs += "--filter", "FullyQualifiedName~Services"
    }
    "controllers" {
        Write-Host "Running Controller Tests..." -ForegroundColor Green
        $testArgs += "--filter", "FullyQualifiedName~Controllers"
    }
    "python" {
        Write-Host "Running Python Worker Integration Tests..." -ForegroundColor Green
        $testArgs += "--filter", "FullyQualifiedName~PythonWorker"
    }
    "endtoend" {
        Write-Host "Running End-to-End Tests..." -ForegroundColor Green
        $testArgs += "--filter", "FullyQualifiedName~EndToEnd"
    }
    "all" {
        Write-Host "Running All Tests..." -ForegroundColor Green
    }
    default {
        Write-Host "Running tests matching filter: $TestCategory" -ForegroundColor Green
        $testArgs += "--filter", $TestCategory
    }
}

Write-Host ""
Write-Host "Test execution started at: $(Get-Date)" -ForegroundColor Gray
Write-Host "Command: dotnet $($testArgs -join ' ')" -ForegroundColor Gray
Write-Host ""

# Execute tests
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
& dotnet @testArgs

$exitCode = $LASTEXITCODE
$stopwatch.Stop()

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "           Test Execution Summary" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Execution Time: $($stopwatch.Elapsed.ToString('hh\:mm\:ss'))" -ForegroundColor Gray
Write-Host "Exit Code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } else { "Red" })

# Display results location
Write-Host ""
Write-Host "Test Results Location:" -ForegroundColor Yellow
Write-Host "  Directory: $outputDir" -ForegroundColor Gray
Write-Host "  TRX File: $outputDir\TestResults.trx" -ForegroundColor Gray

if ($Coverage) {
    $coverageFiles = Get-ChildItem -Path $outputDir -Filter "*.xml" -Recurse
    if ($coverageFiles) {
        Write-Host "  Coverage: $($coverageFiles[0].FullName)" -ForegroundColor Gray
    }
}

# Summary recommendations
Write-Host ""
Write-Host "Recommendations:" -ForegroundColor Yellow
if ($exitCode -eq 0) {
    Write-Host "  ✅ All tests passed successfully!" -ForegroundColor Green
    Write-Host "  📊 Review test results for performance metrics" -ForegroundColor Gray
    if (!$Coverage) {
        Write-Host "  📈 Consider running with -Coverage for code coverage analysis" -ForegroundColor Gray
    }
} else {
    Write-Host "  ❌ Some tests failed - review the output above" -ForegroundColor Red
    Write-Host "  🔍 Check the TRX file for detailed failure information" -ForegroundColor Gray
    Write-Host "  🐛 Run failed tests individually for debugging" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Example commands for specific test categories:" -ForegroundColor Yellow
Write-Host "  .\RunTests.ps1 -TestCategory Unit" -ForegroundColor Gray
Write-Host "  .\RunTests.ps1 -TestCategory Integration -Coverage" -ForegroundColor Gray
Write-Host "  .\RunTests.ps1 -TestCategory Services -Verbose" -ForegroundColor Gray
Write-Host "  .\RunTests.ps1 -TestCategory EndToEnd" -ForegroundColor Gray

exit $exitCode
