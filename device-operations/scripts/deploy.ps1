# Device Operations API - PowerShell Deployment Script
param(
    [string]$Environment = "Production",
    [switch]$SkipBackup = $false,
    [switch]$SkipHealthCheck = $false,
    [string]$BackupLocation = "",
    [int]$HealthCheckRetries = 30,
    [int]$HealthCheckInterval = 10
)

# Configuration
$ProjectName = "device-operations"
$HealthCheckUrl = "http://localhost:5000/health"

if ([string]::IsNullOrEmpty($BackupLocation)) {
    $BackupLocation = "C:\backup\device-operations\$(Get-Date -Format 'yyyyMMdd_HHmmss')"
}

# Logging functions
function Write-LogInfo {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Green
}

function Write-LogWarn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-LogError {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

# Check prerequisites
function Test-Prerequisites {
    Write-LogInfo "Checking prerequisites..."
    
    # Check if Docker is installed and running
    try {
        $dockerVersion = docker --version
        Write-LogInfo "Docker found: $dockerVersion"
    } catch {
        Write-LogError "Docker is not installed or not in PATH"
        exit 1
    }
    
    try {
        docker info | Out-Null
        Write-LogInfo "Docker daemon is running"
    } catch {
        Write-LogError "Docker daemon is not running"
        exit 1
    }
    
    # Check if Docker Compose is available
    try {
        $composeVersion = docker-compose --version
        Write-LogInfo "Docker Compose found: $composeVersion"
    } catch {
        try {
            $composeVersion = docker compose version
            Write-LogInfo "Docker Compose found: $composeVersion"
        } catch {
            Write-LogError "Docker Compose is not available"
            exit 1
        }
    }
    
    # Check if .env file exists
    if (-not (Test-Path ".env")) {
        Write-LogWarn ".env file not found"
        if (Test-Path ".env.example") {
            Copy-Item ".env.example" ".env"
            Write-LogWarn "Copied .env.example to .env - please update with your configuration"
        } else {
            Write-LogError ".env.example file not found"
            exit 1
        }
    }
    
    Write-LogInfo "Prerequisites check completed"
}

# Create backup
function New-Backup {
    if ($SkipBackup) {
        Write-LogInfo "Skipping backup as requested"
        return
    }
    
    Write-LogInfo "Creating backup..."
    
    New-Item -Path $BackupLocation -ItemType Directory -Force | Out-Null
    
    # Backup volumes if they exist
    $volumePrefix = "${ProjectName}_"
    
    try {
        docker volume inspect "${volumePrefix}postgres_data" | Out-Null
        Write-LogInfo "Backing up database..."
        docker run --rm -v "${volumePrefix}postgres_data:/source:ro" -v "$BackupLocation`:/backup" alpine tar czf /backup/postgres_data.tar.gz -C /source .
    } catch {
        Write-LogWarn "Database volume not found, skipping backup"
    }
    
    try {
        docker volume inspect "${volumePrefix}device_outputs" | Out-Null
        Write-LogInfo "Backing up outputs..."
        docker run --rm -v "${volumePrefix}device_outputs:/source:ro" -v "$BackupLocation`:/backup" alpine tar czf /backup/device_outputs.tar.gz -C /source .
    } catch {
        Write-LogWarn "Outputs volume not found, skipping backup"
    }
    
    try {
        docker volume inspect "${volumePrefix}device_cache" | Out-Null
        Write-LogInfo "Backing up cache..."
        docker run --rm -v "${volumePrefix}device_cache:/source:ro" -v "$BackupLocation`:/backup" alpine tar czf /backup/device_cache.tar.gz -C /source .
    } catch {
        Write-LogWarn "Cache volume not found, skipping backup"
    }
    
    Write-LogInfo "Backup created at $BackupLocation"
}

# Build and deploy
function Start-Deployment {
    Write-LogInfo "Starting deployment..."
    
    # Set environment
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    
    # Pull latest images
    Write-LogInfo "Pulling latest base images..."
    docker-compose pull postgres redis seq nginx prometheus grafana
    
    # Build application
    Write-LogInfo "Building application..."
    docker-compose build --no-cache device-operations
    
    # Stop existing services gracefully
    Write-LogInfo "Stopping existing services..."
    docker-compose down --timeout 30
    
    # Start services
    Write-LogInfo "Starting services..."
    docker-compose up -d
    
    Write-LogInfo "Services started, waiting for health checks..."
}

# Health check
function Wait-ForHealth {
    if ($SkipHealthCheck) {
        Write-LogInfo "Skipping health check as requested"
        return $true
    }
    
    Write-LogInfo "Waiting for application to be healthy..."
    
    for ($i = 1; $i -le $HealthCheckRetries; $i++) {
        try {
            $response = Invoke-WebRequest -Uri $HealthCheckUrl -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                Write-LogInfo "Application is healthy!"
                return $true
            }
        } catch {
            # Continue to retry
        }
        
        Write-LogInfo "Health check attempt $i/$HealthCheckRetries failed, retrying in ${HealthCheckInterval}s..."
        Start-Sleep -Seconds $HealthCheckInterval
    }
    
    Write-LogError "Health check failed after $HealthCheckRetries attempts"
    return $false
}

# Post-deployment verification
function Test-Deployment {
    Write-LogInfo "Verifying deployment..."
    
    # Check all services are running
    $services = @("device-operations", "postgres", "redis", "seq", "nginx")
    foreach ($service in $services) {
        $serviceStatus = docker-compose ps $service
        if ($serviceStatus -match "Up") {
            Write-LogInfo "✓ $service is running"
        } else {
            Write-LogError "✗ $service is not running"
            docker-compose logs $service | Select-Object -Last 20
            return $false
        }
    }
    
    # Test API endpoints
    Write-LogInfo "Testing API endpoints..."
    
    # Test health endpoint
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -TimeoutSec 10
        if ($response.StatusCode -eq 200) {
            Write-LogInfo "✓ Health endpoint responding"
        } else {
            Write-LogError "✗ Health endpoint returned status $($response.StatusCode)"
            return $false
        }
    } catch {
        Write-LogError "✗ Health endpoint not responding: $($_.Exception.Message)"
        return $false
    }
    
    # Test device list endpoint (if authentication allows)
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/api/device/list" -UseBasicParsing -TimeoutSec 10
        Write-LogInfo "✓ Device list endpoint responding"
    } catch {
        Write-LogWarn "⚠ Device list endpoint requires authentication"
    }
    
    Write-LogInfo "Deployment verification completed successfully"
    return $true
}

# Rollback function
function Start-Rollback {
    Write-LogError "Deployment failed, initiating rollback..."
    
    # Stop current deployment
    docker-compose down --timeout 30
    
    # Restore backup if available
    if (Test-Path $BackupLocation) {
        Write-LogInfo "Restoring from backup..."
        
        $volumePrefix = "${ProjectName}_"
        
        # Restore database if backup exists
        if (Test-Path "$BackupLocation\postgres_data.tar.gz") {
            docker volume create "${volumePrefix}postgres_data"
            docker run --rm -v "${volumePrefix}postgres_data:/target" -v "$BackupLocation`:/backup" alpine tar xzf /backup/postgres_data.tar.gz -C /target
        }
        
        # Restore outputs if backup exists
        if (Test-Path "$BackupLocation\device_outputs.tar.gz") {
            docker volume create "${volumePrefix}device_outputs"
            docker run --rm -v "${volumePrefix}device_outputs:/target" -v "$BackupLocation`:/backup" alpine tar xzf /backup/device_outputs.tar.gz -C /target
        }
        
        # Restore cache if backup exists
        if (Test-Path "$BackupLocation\device_cache.tar.gz") {
            docker volume create "${volumePrefix}device_cache"
            docker run --rm -v "${volumePrefix}device_cache:/target" -v "$BackupLocation`:/backup" alpine tar xzf /backup/device_cache.tar.gz -C /target
        }
        
        Write-LogInfo "Backup restored"
    }
    
    Write-LogError "Rollback completed"
    exit 1
}

# Main deployment process
function Main {
    Write-Host "Device Operations API - PowerShell Deployment" -ForegroundColor Cyan
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host "Environment: $Environment" -ForegroundColor Cyan
    Write-Host ""
    
    try {
        Test-Prerequisites
        New-Backup
        Start-Deployment
        
        if (Wait-ForHealth) {
            if (Test-Deployment) {
                Write-LogInfo "Deployment completed successfully!"
                
                Write-Host ""
                Write-Host "Deployment Summary:" -ForegroundColor Cyan
                Write-Host "==================" -ForegroundColor Cyan
                Write-Host "✓ Application: http://localhost:5000" -ForegroundColor Green
                Write-Host "✓ API Documentation: http://localhost:5000/api-docs" -ForegroundColor Green
                Write-Host "✓ Health Check: http://localhost:5000/health" -ForegroundColor Green
                Write-Host "✓ Grafana Dashboard: http://localhost:3000" -ForegroundColor Green
                Write-Host "✓ Prometheus Metrics: http://localhost:9090" -ForegroundColor Green
                Write-Host "✓ Seq Logs: http://localhost:5341" -ForegroundColor Green
                Write-Host ""
                Write-Host "Backup Location: $BackupLocation" -ForegroundColor Yellow
                Write-Host ""
                Write-LogInfo "Deployment completed successfully!"
            } else {
                Start-Rollback
            }
        } else {
            Start-Rollback
        }
    } catch {
        Write-LogError "Deployment failed with error: $($_.Exception.Message)"
        Start-Rollback
    }
}

# Script entry point
Main
