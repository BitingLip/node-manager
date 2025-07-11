# PowerShell script to run only the working tests and see results
Write-Host "Running only the tests that were successfully fixed in Phase 5.1..." -ForegroundColor Green

# First, temporarily move the problematic test files
$problemFiles = @(
    "tests\Services\ServiceModelTests.cs",
    "tests\Services\ServiceMemoryTests.cs", 
    "tests\Services\ServiceDeviceTests.cs",
    "tests\Controllers\ControllerDeviceTests.cs",
    "tests\Integration\PythonWorkerIntegrationTests.cs",
    "tests\Helpers\TestHelpers.cs"
)

$tempDir = "tests\temp_excluded"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

foreach ($file in $problemFiles) {
    if (Test-Path $file) {
        Write-Host "Temporarily moving $file" -ForegroundColor Yellow
        Move-Item $file "$tempDir\$(Split-Path $file -Leaf)" -Force
    }
}

try {
    Write-Host "Running working tests with detailed output..." -ForegroundColor Green
    dotnet test DeviceOperations.csproj --verbosity detailed --logger "console;verbosity=detailed"
    
    Write-Host "Test run completed!" -ForegroundColor Green
}
finally {
    # Restore the problematic files
    Write-Host "Restoring temporarily moved files..." -ForegroundColor Yellow
    foreach ($file in $problemFiles) {
        $tempFile = "$tempDir\$(Split-Path $file -Leaf)"
        if (Test-Path $tempFile) {
            Move-Item $tempFile $file -Force
        }
    }
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
