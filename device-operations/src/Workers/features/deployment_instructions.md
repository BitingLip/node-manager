# Enhanced SDXL Deployment Instructions

## Overview
Complete deployment instructions for the Enhanced SDXL pipeline with upscaling and post-processing features in production environments.

## Table of Contents
- [Prerequisites](#prerequisites)
- [Development Deployment](#development-deployment)
- [Production Deployment](#production-deployment)
- [Configuration Management](#configuration-management)
- [Monitoring and Logging](#monitoring-and-logging)
- [Security Considerations](#security-considerations)
- [Scaling and Load Balancing](#scaling-and-load-balancing)
- [Maintenance and Updates](#maintenance-and-updates)

## Prerequisites

### System Requirements

#### Minimum Production Requirements
- **Operating System**: Windows Server 2019+ or Ubuntu 20.04+
- **CPU**: 16-core processor (Intel Xeon or AMD EPYC)
- **RAM**: 64GB system memory
- **GPU**: 12GB+ VRAM (RTX 3080 Ti/4070 Ti or better)
- **Storage**: 500GB SSD for models and cache
- **Network**: 1Gbps network connection

#### Recommended Production Requirements
- **Operating System**: Windows Server 2022 or Ubuntu 22.04 LTS
- **CPU**: 32-core processor (Intel Xeon or AMD EPYC)
- **RAM**: 128GB system memory
- **GPU**: 24GB+ VRAM (RTX 4090/A6000 or better)
- **Storage**: 1TB NVMe SSD for models and cache
- **Network**: 10Gbps network connection

### Software Dependencies

#### .NET Requirements
```bash
# Install .NET 6.0 or later
# Windows
winget install Microsoft.DotNet.Runtime.6

# Ubuntu
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-runtime-6.0 dotnet-sdk-6.0
```

#### Python Requirements
```bash
# Install Python 3.10+
# Windows
winget install Python.Python.3.10

# Ubuntu
sudo apt update
sudo apt install python3.10 python3.10-venv python3.10-dev

# Install required packages
pip install torch torchvision torchaudio --extra-index-url https://download.pytorch.org/whl/cu118
pip install diffusers transformers accelerate safetensors
pip install torch-directml  # For DirectML support
pip install opencv-python pillow numpy fastapi uvicorn
pip install psutil requests aiofiles
```

#### GPU Drivers
```bash
# NVIDIA drivers (latest)
# Download from: https://www.nvidia.com/drivers/

# Verify installation
nvidia-smi

# DirectML (Windows)
# Included with Windows 10/11 version 1903+
# Verify: dxdiag
```

## Development Deployment

### Local Development Setup

#### 1. Clone and Setup Repository
```bash
# Clone repository
git clone <repository-url> device-manager
cd device-manager/device-operations

# Create Python virtual environment
python -m venv venv

# Activate virtual environment
# Windows
venv\Scripts\activate
# Linux/Mac
source venv/bin/activate

# Install Python dependencies
pip install -r requirements.txt
```

#### 2. Configure Environment Variables
```bash
# Windows (PowerShell)
$env:REALESRGAN_MODEL_PATH = "C:\models\realesrgan"
$env:ESRGAN_MODEL_PATH = "C:\models\esrgan"
$env:MAX_VRAM_USAGE = "8GB"
$env:ENABLE_MEMORY_OPTIMIZATION = "true"
$env:TORCH_DEVICE = "cuda"
$env:WORKER_PORT = "8888"
$env:LOG_LEVEL = "DEBUG"

# Linux/Mac
export REALESRGAN_MODEL_PATH="/models/realesrgan"
export ESRGAN_MODEL_PATH="/models/esrgan"
export MAX_VRAM_USAGE="8GB"
export ENABLE_MEMORY_OPTIMIZATION="true"
export TORCH_DEVICE="cuda"
export WORKER_PORT="8888"
export LOG_LEVEL="DEBUG"
```

#### 3. Download Required Models
```bash
# Create model directories
mkdir -p models/realesrgan
mkdir -p models/esrgan

# Download Real-ESRGAN models
cd models/realesrgan
wget https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.1/RealESRGAN_x2plus.pth
wget https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.1/RealESRGAN_x4plus.pth

# Download ESRGAN models (if needed)
cd ../esrgan
# Download from appropriate sources
```

#### 4. Build and Run Services
```bash
# Build C# service
dotnet build DeviceOperations.csproj

# Start Python worker
cd src/Workers/features
python upscaler_worker.py --port 8888 --debug

# Start C# service (in another terminal)
cd ../../..
dotnet run
```

#### 5. Verify Installation
```bash
# Test upscaling functionality
cd src/Workers/features
python comprehensive_testing.py

# Check service endpoints
curl http://localhost:5000/health
curl http://localhost:8888/health
```

## Production Deployment

### Docker Deployment

#### 1. Create Dockerfile for C# Service
```dockerfile
# Dockerfile.csharp
FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["DeviceOperations.csproj", "."]
RUN dotnet restore "DeviceOperations.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "DeviceOperations.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DeviceOperations.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install required system packages
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "DeviceOperations.dll"]
```

#### 2. Create Dockerfile for Python Worker
```dockerfile
# Dockerfile.python
FROM nvidia/cuda:11.8-devel-ubuntu22.04

# Set environment variables
ENV PYTHONUNBUFFERED=1
ENV DEBIAN_FRONTEND=noninteractive

# Install system dependencies
RUN apt-get update && apt-get install -y \
    python3.10 \
    python3.10-venv \
    python3.10-dev \
    python3-pip \
    wget \
    curl \
    git \
    libgl1-mesa-glx \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    libgomp1 \
    && rm -rf /var/lib/apt/lists/*

# Create working directory
WORKDIR /app

# Copy requirements and install Python packages
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Install PyTorch with CUDA support
RUN pip install torch torchvision torchaudio --extra-index-url https://download.pytorch.org/whl/cu118

# Copy application code
COPY src/ ./src/
COPY models/ ./models/

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

# Expose worker port
EXPOSE 8888

# Health check
HEALTHCHECK --interval=30s --timeout=30s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8888/health || exit 1

# Start worker
CMD ["python", "src/Workers/features/upscaler_worker.py", "--host", "0.0.0.0", "--port", "8888"]
```

#### 3. Create Docker Compose Configuration
```yaml
# docker-compose.yml
version: '3.8'

services:
  device-operations:
    build:
      context: .
      dockerfile: Dockerfile.csharp
    ports:
      - "5000:80"
      - "5001:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - WORKER_BASE_URL=http://upscaler-worker:8888
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION_STRING}
    depends_on:
      - upscaler-worker
    networks:
      - device-network
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  upscaler-worker:
    build:
      context: .
      dockerfile: Dockerfile.python
    ports:
      - "8888:8888"
    environment:
      - TORCH_DEVICE=cuda
      - MAX_VRAM_USAGE=12GB
      - ENABLE_MEMORY_OPTIMIZATION=true
      - LOG_LEVEL=INFO
      - REALESRGAN_MODEL_PATH=/app/models/realesrgan
      - ESRGAN_MODEL_PATH=/app/models/esrgan
    volumes:
      - ./models:/app/models:ro
      - ./logs:/app/logs
      - ./cache:/app/cache
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    networks:
      - device-network
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./ssl:/etc/nginx/ssl:ro
    depends_on:
      - device-operations
    networks:
      - device-network
    restart: unless-stopped

networks:
  device-network:
    driver: bridge

volumes:
  models:
  logs:
  cache:
```

#### 4. Create Nginx Configuration
```nginx
# nginx.conf
events {
    worker_connections 1024;
}

http {
    upstream device_operations {
        server device-operations:80;
    }
    
    upstream upscaler_worker {
        server upscaler-worker:8888;
    }
    
    # Rate limiting
    limit_req_zone $binary_remote_addr zone=api:10m rate=10r/s;
    limit_req_zone $binary_remote_addr zone=worker:10m rate=5r/s;
    
    server {
        listen 80;
        server_name localhost;
        
        # Redirect HTTP to HTTPS in production
        return 301 https://$server_name$request_uri;
    }
    
    server {
        listen 443 ssl http2;
        server_name localhost;
        
        # SSL configuration
        ssl_certificate /etc/nginx/ssl/cert.pem;
        ssl_certificate_key /etc/nginx/ssl/key.pem;
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers ECDHE-RSA-AES256-GCM-SHA512:DHE-RSA-AES256-GCM-SHA512;
        ssl_prefer_server_ciphers off;
        
        # Main API
        location /api/ {
            limit_req zone=api burst=20 nodelay;
            proxy_pass http://device_operations/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            
            # Timeout settings for long-running operations
            proxy_connect_timeout 60s;
            proxy_send_timeout 300s;
            proxy_read_timeout 300s;
        }
        
        # Worker API (internal only)
        location /worker/ {
            limit_req zone=worker burst=10 nodelay;
            proxy_pass http://upscaler_worker/;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            
            # Restrict access to internal network
            allow 10.0.0.0/8;
            allow 172.16.0.0/12;
            allow 192.168.0.0/16;
            deny all;
        }
        
        # Health checks
        location /health {
            proxy_pass http://device_operations/health;
            access_log off;
        }
        
        # Static files (if any)
        location /static/ {
            root /var/www;
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
    }
}
```

### Kubernetes Deployment

#### 1. Create Kubernetes Manifests
```yaml
# k8s-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: device-operations
  labels:
    app: device-operations
spec:
  replicas: 3
  selector:
    matchLabels:
      app: device-operations
  template:
    metadata:
      labels:
        app: device-operations
    spec:
      containers:
      - name: device-operations
        image: device-operations:latest
        ports:
        - containerPort: 80
        env:
        - name: WORKER_BASE_URL
          value: "http://upscaler-worker:8888"
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        resources:
          requests:
            memory: "2Gi"
            cpu: "1000m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5

---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: upscaler-worker
  labels:
    app: upscaler-worker
spec:
  replicas: 2
  selector:
    matchLabels:
      app: upscaler-worker
  template:
    metadata:
      labels:
        app: upscaler-worker
    spec:
      containers:
      - name: upscaler-worker
        image: upscaler-worker:latest
        ports:
        - containerPort: 8888
        env:
        - name: TORCH_DEVICE
          value: "cuda"
        - name: MAX_VRAM_USAGE
          value: "12GB"
        resources:
          requests:
            memory: "8Gi"
            cpu: "4000m"
            nvidia.com/gpu: 1
          limits:
            memory: "16Gi"
            cpu: "8000m"
            nvidia.com/gpu: 1
        volumeMounts:
        - name: models-volume
          mountPath: /app/models
          readOnly: true
        livenessProbe:
          httpGet:
            path: /health
            port: 8888
          initialDelaySeconds: 60
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 8888
          initialDelaySeconds: 30
          periodSeconds: 10
      volumes:
      - name: models-volume
        persistentVolumeClaim:
          claimName: models-pvc

---
apiVersion: v1
kind: Service
metadata:
  name: device-operations
spec:
  selector:
    app: device-operations
  ports:
  - port: 80
    targetPort: 80
  type: LoadBalancer

---
apiVersion: v1
kind: Service
metadata:
  name: upscaler-worker
spec:
  selector:
    app: upscaler-worker
  ports:
  - port: 8888
    targetPort: 8888
  type: ClusterIP

---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: models-pvc
spec:
  accessModes:
  - ReadOnlyMany
  resources:
    requests:
      storage: 100Gi
  storageClassName: fast-ssd
```

#### 2. Deploy to Kubernetes
```bash
# Apply manifests
kubectl apply -f k8s-deployment.yaml

# Check deployment status
kubectl get deployments
kubectl get pods
kubectl get services

# Check logs
kubectl logs -f deployment/device-operations
kubectl logs -f deployment/upscaler-worker

# Scale deployment if needed
kubectl scale deployment device-operations --replicas=5
kubectl scale deployment upscaler-worker --replicas=3
```

## Configuration Management

### Environment-Specific Configuration

#### Production Configuration
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-db;Database=DeviceOperations;Trusted_Connection=true;"
  },
  "WorkerConfiguration": {
    "BaseUrl": "http://upscaler-worker:8888",
    "TimeoutSeconds": 300,
    "RetryAttempts": 3,
    "MaxConcurrentRequests": 10
  },
  "UpscalingConfiguration": {
    "DefaultMethod": "realesrgan",
    "DefaultQualityMode": "balanced",
    "MaxBatchSize": 4,
    "EnableMemoryOptimization": true,
    "MaxVramUsageGB": 12
  },
  "SecurityConfiguration": {
    "RequireHttps": true,
    "EnableApiKey": true,
    "RateLimitPerMinute": 60
  }
}
```

#### Staging Configuration
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  },
  "WorkerConfiguration": {
    "BaseUrl": "http://staging-upscaler-worker:8888",
    "TimeoutSeconds": 180,
    "RetryAttempts": 5
  },
  "UpscalingConfiguration": {
    "DefaultQualityMode": "high",
    "MaxBatchSize": 2,
    "EnableDetailedLogging": true
  }
}
```

### Configuration Validation
```csharp
public class ConfigurationValidator
{
    public static void ValidateConfiguration(IConfiguration configuration)
    {
        var errors = new List<string>();
        
        // Validate worker configuration
        var workerUrl = configuration["WorkerConfiguration:BaseUrl"];
        if (string.IsNullOrEmpty(workerUrl))
        {
            errors.Add("WorkerConfiguration:BaseUrl is required");
        }
        
        // Validate upscaling configuration
        var maxVram = configuration.GetValue<int>("UpscalingConfiguration:MaxVramUsageGB");
        if (maxVram < 4)
        {
            errors.Add("UpscalingConfiguration:MaxVramUsageGB must be at least 4GB");
        }
        
        // Validate database connection
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            errors.Add("Database connection string is required");
        }
        
        if (errors.Any())
        {
            throw new InvalidOperationException($"Configuration validation failed: {string.Join(", ", errors)}");
        }
    }
}
```

## Monitoring and Logging

### Application Performance Monitoring

#### 1. Implement Health Checks
```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<WorkerHealthCheck>("worker-health")
    .AddCheck<DatabaseHealthCheck>("database-health")
    .AddCheck<ModelHealthCheck>("model-health");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

#### 2. Custom Health Checks
```csharp
public class WorkerHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    
    public WorkerHealthCheck(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workerUrl = _configuration["WorkerConfiguration:BaseUrl"];
            var response = await _httpClient.GetAsync($"{workerUrl}/health", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return HealthCheckResult.Healthy($"Worker responding: {content}");
            }
            
            return HealthCheckResult.Unhealthy($"Worker returned: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Worker health check failed: {ex.Message}");
        }
    }
}
```

### Logging Configuration

#### 1. Structured Logging with Serilog
```csharp
// Install: dotnet add package Serilog.AspNetCore

// Program.cs
using Serilog;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/device-operations-.txt", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elasticsearch:9200"))
    {
        IndexFormat = "device-operations-{0:yyyy.MM.dd}",
        AutoRegisterTemplate = true
    })
    .CreateLogger();

builder.Host.UseSerilog();
```

#### 2. Performance Logging
```csharp
public class PerformanceLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceLoggingMiddleware> _logger;
    
    public PerformanceLoggingMiddleware(RequestDelegate next, ILogger<PerformanceLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            _logger.LogInformation("Request {Method} {Path} completed in {ElapsedMilliseconds}ms with status {StatusCode}",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds,
                context.Response.StatusCode);
        }
    }
}
```

### Metrics Collection

#### 1. Prometheus Metrics
```csharp
// Install: dotnet add package prometheus-net.AspNetCore

// Program.cs
using Prometheus;

builder.Services.AddSingleton<IMetrics, MetricsService>();

app.UseMetricServer(); // Expose /metrics endpoint
app.UseHttpMetrics(); // Collect HTTP metrics

public class MetricsService : IMetrics
{
    private static readonly Counter ProcessedImages = Metrics
        .CreateCounter("upscaling_images_processed_total", "Total number of images processed");
    
    private static readonly Histogram ProcessingDuration = Metrics
        .CreateHistogram("upscaling_processing_duration_seconds", "Processing duration in seconds");
    
    private static readonly Gauge ActiveRequests = Metrics
        .CreateGauge("upscaling_active_requests", "Number of active upscaling requests");
    
    public void IncrementProcessedImages() => ProcessedImages.Inc();
    public void RecordProcessingDuration(double duration) => ProcessingDuration.Observe(duration);
    public void SetActiveRequests(int count) => ActiveRequests.Set(count);
}
```

#### 2. Grafana Dashboard Configuration
```json
{
  "dashboard": {
    "title": "Enhanced SDXL Pipeline",
    "panels": [
      {
        "title": "Request Rate",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(http_requests_total[5m])",
            "legendFormat": "{{method}} {{status_code}}"
          }
        ]
      },
      {
        "title": "Processing Duration",
        "type": "graph",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(upscaling_processing_duration_seconds_bucket[5m]))",
            "legendFormat": "95th percentile"
          }
        ]
      },
      {
        "title": "Active Requests",
        "type": "singlestat",
        "targets": [
          {
            "expr": "upscaling_active_requests",
            "legendFormat": "Active"
          }
        ]
      }
    ]
  }
}
```

## Security Considerations

### API Security

#### 1. API Key Authentication
```csharp
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    
    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API Key required");
            return;
        }
        
        var validApiKey = _configuration["SecurityConfiguration:ApiKey"];
        if (!string.Equals(extractedApiKey, validApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid API Key");
            return;
        }
        
        await _next(context);
    }
}
```

#### 2. Rate Limiting
```csharp
// Install: dotnet add package AspNetCoreRateLimit

// Program.cs
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

app.UseIpRateLimiting();
```

#### 3. Input Validation
```csharp
public class UpscalingRequestValidator : AbstractValidator<UpscalingRequest>
{
    public UpscalingRequestValidator()
    {
        RuleFor(x => x.ScaleFactor)
            .Must(x => x == 2.0 || x == 4.0)
            .WithMessage("Scale factor must be 2.0 or 4.0");
        
        RuleFor(x => x.Method)
            .Must(x => x == "realesrgan" || x == "esrgan")
            .WithMessage("Method must be 'realesrgan' or 'esrgan'");
        
        RuleFor(x => x.Images)
            .NotEmpty()
            .WithMessage("At least one image is required")
            .Must(x => x.Count <= 10)
            .WithMessage("Maximum 10 images per request");
        
        RuleForEach(x => x.Images)
            .Must(BeValidBase64Image)
            .WithMessage("Invalid image data");
    }
    
    private bool BeValidBase64Image(string imageData)
    {
        try
        {
            var bytes = Convert.FromBase64String(imageData);
            return bytes.Length > 0 && bytes.Length < 50 * 1024 * 1024; // Max 50MB
        }
        catch
        {
            return false;
        }
    }
}
```

### Data Security

#### 1. Image Data Encryption
```csharp
public class ImageEncryptionService
{
    private readonly byte[] _key;
    
    public ImageEncryptionService(IConfiguration configuration)
    {
        var keyString = configuration["SecurityConfiguration:EncryptionKey"];
        _key = Convert.FromBase64String(keyString);
    }
    
    public string EncryptImageData(byte[] imageData)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        
        // Write IV first
        msEncrypt.Write(aes.IV, 0, aes.IV.Length);
        
        // Write encrypted data
        csEncrypt.Write(imageData, 0, imageData.Length);
        csEncrypt.FlushFinalBlock();
        
        return Convert.ToBase64String(msEncrypt.ToArray());
    }
    
    public byte[] DecryptImageData(string encryptedData)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedData);
        
        using var aes = Aes.Create();
        aes.Key = _key;
        
        // Extract IV
        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);
        aes.IV = iv;
        
        using var decryptor = aes.CreateDecryptor();
        using var msDecrypt = new MemoryStream(encryptedBytes, iv.Length, encryptedBytes.Length - iv.Length);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var msResult = new MemoryStream();
        
        csDecrypt.CopyTo(msResult);
        return msResult.ToArray();
    }
}
```

## Scaling and Load Balancing

### Horizontal Scaling

#### 1. Load Balancer Configuration
```nginx
# nginx-lb.conf
upstream device_operations {
    least_conn;
    server device-operations-1:80 max_fails=3 fail_timeout=30s;
    server device-operations-2:80 max_fails=3 fail_timeout=30s;
    server device-operations-3:80 max_fails=3 fail_timeout=30s;
}

upstream upscaler_workers {
    least_conn;
    server upscaler-worker-1:8888 max_fails=3 fail_timeout=30s;
    server upscaler-worker-2:8888 max_fails=3 fail_timeout=30s;
    server upscaler-worker-3:8888 max_fails=3 fail_timeout=30s;
}

server {
    listen 80;
    
    location /api/ {
        proxy_pass http://device_operations;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        
        # Sticky sessions for file uploads
        ip_hash;
    }
    
    location /worker/ {
        proxy_pass http://upscaler_workers;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        
        # Load balancing for worker requests
        least_conn;
    }
}
```

#### 2. Auto-scaling with Kubernetes
```yaml
# horizontal-pod-autoscaler.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: device-operations-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: device-operations
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80

---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: upscaler-worker-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: upscaler-worker
  minReplicas: 2
  maxReplicas: 8
  metrics:
  - type: Resource
    resource:
      name: nvidia.com/gpu
      target:
        type: Utilization
        averageUtilization: 80
  - type: Pods
    pods:
      metric:
        name: active_requests_per_pod
      target:
        type: AverageValue
        averageValue: "5"
```

### Cache Strategy

#### 1. Redis Caching
```csharp
// Install: dotnet add package StackExchange.Redis

public class ImageCacheService
{
    private readonly IDatabase _database;
    private readonly ILogger<ImageCacheService> _logger;
    
    public ImageCacheService(IConnectionMultiplexer redis, ILogger<ImageCacheService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }
    
    public async Task<byte[]?> GetCachedImageAsync(string cacheKey)
    {
        try
        {
            var cachedData = await _database.StringGetAsync(cacheKey);
            return cachedData.HasValue ? cachedData : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve cached image: {CacheKey}", cacheKey);
            return null;
        }
    }
    
    public async Task CacheImageAsync(string cacheKey, byte[] imageData, TimeSpan expiry)
    {
        try
        {
            await _database.StringSetAsync(cacheKey, imageData, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache image: {CacheKey}", cacheKey);
        }
    }
    
    public string GenerateCacheKey(string originalImageHash, double scaleFactor, string method)
    {
        return $"upscaled:{originalImageHash}:{scaleFactor}:{method}";
    }
}
```

## Maintenance and Updates

### Rolling Updates

#### 1. Zero-Downtime Deployment Script
```bash
#!/bin/bash
# deploy.sh

set -e

echo "Starting zero-downtime deployment..."

# Build new images
docker build -t device-operations:$BUILD_NUMBER -f Dockerfile.csharp .
docker build -t upscaler-worker:$BUILD_NUMBER -f Dockerfile.python .

# Update deployment with new image
kubectl set image deployment/device-operations device-operations=device-operations:$BUILD_NUMBER
kubectl set image deployment/upscaler-worker upscaler-worker=upscaler-worker:$BUILD_NUMBER

# Wait for rollout to complete
kubectl rollout status deployment/device-operations --timeout=600s
kubectl rollout status deployment/upscaler-worker --timeout=600s

# Verify deployment
kubectl get pods
kubectl logs -l app=device-operations --tail=10
kubectl logs -l app=upscaler-worker --tail=10

echo "Deployment completed successfully!"
```

#### 2. Database Migration Strategy
```csharp
public class DatabaseMigrationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMigrationService> _logger;
    
    public async Task<bool> MigrateDatabaseAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DeviceOperationsContext>();
            
            // Check if migration is needed
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
                
                // Apply migrations
                await context.Database.MigrateAsync();
                
                _logger.LogInformation("Database migration completed successfully");
                return true;
            }
            
            _logger.LogInformation("No pending migrations found");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database migration failed");
            return false;
        }
    }
}
```

### Backup and Recovery

#### 1. Automated Backup Script
```bash
#!/bin/bash
# backup.sh

BACKUP_DIR="/backups/$(date +%Y%m%d)"
mkdir -p $BACKUP_DIR

echo "Starting backup process..."

# Backup database
kubectl exec -n production deployment/postgres -- pg_dump -U postgres device_operations > $BACKUP_DIR/database.sql

# Backup models
kubectl cp production/upscaler-worker-pod:/app/models $BACKUP_DIR/models

# Backup configuration
kubectl get configmaps -o yaml > $BACKUP_DIR/configmaps.yaml
kubectl get secrets -o yaml > $BACKUP_DIR/secrets.yaml

# Compress backup
tar -czf $BACKUP_DIR.tar.gz -C /backups $(basename $BACKUP_DIR)
rm -rf $BACKUP_DIR

# Upload to cloud storage (optional)
aws s3 cp $BACKUP_DIR.tar.gz s3://device-operations-backups/

echo "Backup completed: $BACKUP_DIR.tar.gz"
```

#### 2. Disaster Recovery Plan
```bash
#!/bin/bash
# restore.sh

BACKUP_FILE=$1

if [ -z "$BACKUP_FILE" ]; then
    echo "Usage: $0 <backup_file>"
    exit 1
fi

echo "Starting disaster recovery from: $BACKUP_FILE"

# Extract backup
tar -xzf $BACKUP_FILE -C /tmp/

# Restore database
kubectl exec -n production deployment/postgres -- psql -U postgres -c "DROP DATABASE IF EXISTS device_operations"
kubectl exec -n production deployment/postgres -- psql -U postgres -c "CREATE DATABASE device_operations"
kubectl exec -i -n production deployment/postgres -- psql -U postgres device_operations < /tmp/database.sql

# Restore configuration
kubectl apply -f /tmp/configmaps.yaml
kubectl apply -f /tmp/secrets.yaml

# Restart services
kubectl rollout restart deployment/device-operations
kubectl rollout restart deployment/upscaler-worker

echo "Disaster recovery completed"
```

For ongoing maintenance and support, refer to the monitoring dashboards and ensure regular backup procedures are followed.
