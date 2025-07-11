# Test only our core working tests
Write-Host "Testing core working test files..." -ForegroundColor Green

# List current test files
Write-Host "Current test files:" -ForegroundColor Yellow
Get-ChildItem tests -Recurse -Name "*.cs" | ForEach-Object { Write-Host "  $_" }

Write-Host "`nRunning test discovery..." -ForegroundColor Green
dotnet test DeviceOperations.csproj --list-tests --verbosity normal
