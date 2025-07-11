# Device Operations API - Production Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000 5001

# Install Python and dependencies
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    python3-venv \
    curl \
    wget \
    git \
    && rm -rf /var/lib/apt/lists/*

# Create Python virtual environment
RUN python3 -m venv /opt/python
ENV PATH="/opt/python/bin:$PATH"

# Copy Python requirements and install packages
COPY requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir -r /tmp/requirements.txt

# Install additional ML packages for GPU support
RUN pip install --no-cache-dir torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118
RUN pip install --no-cache-dir diffusers[torch] transformers accelerate

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["DeviceOperations.csproj", "."]
RUN dotnet restore "DeviceOperations.csproj"

# Copy source code
COPY . .
WORKDIR "/src"
RUN dotnet build "DeviceOperations.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DeviceOperations.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Copy Python workers
COPY src/ ./src/

# Create necessary directories
RUN mkdir -p /data/models /data/outputs /cache/models /logs /tmp/device-operations

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000;https://+:5001
ENV PYTHONPATH=/app/src
ENV PATH="/opt/python/bin:$PATH"

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser && \
    chown -R appuser:appuser /app /data /cache /logs /tmp/device-operations
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "DeviceOperations.dll"]
