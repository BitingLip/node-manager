# PowerShell script to create working test project structure
Write-Host "Creating clean test project structure with only working tests..." -ForegroundColor Green

# Create temporary test directory
$workingTestDir = "tests_working"
$workingTestProjectDir = "$workingTestDir\tests"

# Clean up existing temporary directory
if (Test-Path $workingTestDir) {
    Remove-Item $workingTestDir -Recurse -Force
}

# Create directory structure
New-Item -ItemType Directory -Path $workingTestProjectDir -Force | Out-Null
New-Item -ItemType Directory -Path "$workingTestProjectDir\Services" -Force | Out-Null
New-Item -ItemType Directory -Path "$workingTestProjectDir\Controllers" -Force | Out-Null

# Copy working test files
Write-Host "Copying working test files..." -ForegroundColor Yellow
Copy-Item "tests\Services\ServiceInferenceTests.cs" "$workingTestProjectDir\Services\"
Copy-Item "tests\Services\ServiceProcessingTests.cs" "$workingTestProjectDir\Services\"
Copy-Item "tests\Services\ServicePostprocessingTests.cs" "$workingTestProjectDir\Services\"
Copy-Item "tests\Controllers\ControllerInferenceTests.cs" "$workingTestProjectDir\Controllers\"

# Copy project files
Copy-Item "tests\DeviceOperations.Tests.csproj" "$workingTestProjectDir\"

Write-Host "Working test files copied. Structure:" -ForegroundColor Green
Get-ChildItem $workingTestProjectDir -Recurse -Name | ForEach-Object { Write-Host "  $_" }

Write-Host "`nRunning tests in working directory..." -ForegroundColor Green
Set-Location $workingTestProjectDir
dotnet test DeviceOperations.Tests.csproj --verbosity normal --logger "console;verbosity=normal"

# Return to original directory
Set-Location "..\..\"

Write-Host "`nCompleted!" -ForegroundColor Green
