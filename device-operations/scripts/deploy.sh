#!/bin/bash

# Device Operations API - Production Deployment Script
set -e

echo "Starting Device Operations API deployment..."

# Configuration
PROJECT_NAME="device-operations"
BACKUP_DIR="/backup/device-operations/$(date +%Y%m%d_%H%M%S)"
HEALTH_CHECK_URL="http://localhost:5000/health"
MAX_HEALTH_CHECK_RETRIES=30
HEALTH_CHECK_INTERVAL=10

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check if Docker is installed and running
    if ! command -v docker &> /dev/null; then
        log_error "Docker is not installed"
        exit 1
    fi
    
    if ! docker info &> /dev/null; then
        log_error "Docker is not running"
        exit 1
    fi
    
    # Check if Docker Compose is available
    if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
        log_error "Docker Compose is not available"
        exit 1
    fi
    
    # Check if .env file exists
    if [ ! -f ".env" ]; then
        log_warn ".env file not found, copying from .env.example"
        if [ -f ".env.example" ]; then
            cp .env.example .env
            log_warn "Please update .env file with your configuration"
        else
            log_error ".env.example file not found"
            exit 1
        fi
    fi
    
    log_info "Prerequisites check completed"
}

# Create backup
create_backup() {
    log_info "Creating backup..."
    
    mkdir -p "$BACKUP_DIR"
    
    # Backup volumes if they exist
    if docker volume inspect ${PROJECT_NAME}_postgres_data &> /dev/null; then
        log_info "Backing up database..."
        docker run --rm -v ${PROJECT_NAME}_postgres_data:/source:ro -v "$BACKUP_DIR":/backup alpine tar czf /backup/postgres_data.tar.gz -C /source .
    fi
    
    if docker volume inspect ${PROJECT_NAME}_device_outputs &> /dev/null; then
        log_info "Backing up outputs..."
        docker run --rm -v ${PROJECT_NAME}_device_outputs:/source:ro -v "$BACKUP_DIR":/backup alpine tar czf /backup/device_outputs.tar.gz -C /source .
    fi
    
    if docker volume inspect ${PROJECT_NAME}_device_cache &> /dev/null; then
        log_info "Backing up cache..."
        docker run --rm -v ${PROJECT_NAME}_device_cache:/source:ro -v "$BACKUP_DIR":/backup alpine tar czf /backup/device_cache.tar.gz -C /source .
    fi
    
    log_info "Backup created at $BACKUP_DIR"
}

# Build and deploy
deploy() {
    log_info "Starting deployment..."
    
    # Pull latest images
    log_info "Pulling latest base images..."
    docker-compose pull postgres redis seq nginx prometheus grafana
    
    # Build application
    log_info "Building application..."
    docker-compose build --no-cache device-operations
    
    # Stop existing services gracefully
    log_info "Stopping existing services..."
    docker-compose down --timeout 30
    
    # Start services
    log_info "Starting services..."
    docker-compose up -d
    
    log_info "Services started, waiting for health checks..."
}

# Health check
wait_for_health() {
    log_info "Waiting for application to be healthy..."
    
    local retries=0
    while [ $retries -lt $MAX_HEALTH_CHECK_RETRIES ]; do
        if curl -f -s "$HEALTH_CHECK_URL" > /dev/null 2>&1; then
            log_info "Application is healthy!"
            return 0
        fi
        
        retries=$((retries + 1))
        log_info "Health check attempt $retries/$MAX_HEALTH_CHECK_RETRIES failed, retrying in ${HEALTH_CHECK_INTERVAL}s..."
        sleep $HEALTH_CHECK_INTERVAL
    done
    
    log_error "Health check failed after $MAX_HEALTH_CHECK_RETRIES attempts"
    return 1
}

# Post-deployment verification
verify_deployment() {
    log_info "Verifying deployment..."
    
    # Check all services are running
    local services=("device-operations" "postgres" "redis" "seq" "nginx")
    for service in "${services[@]}"; do
        if docker-compose ps "$service" | grep -q "Up"; then
            log_info "✓ $service is running"
        else
            log_error "✗ $service is not running"
            docker-compose logs "$service" | tail -20
            return 1
        fi
    done
    
    # Test API endpoints
    log_info "Testing API endpoints..."
    
    # Test health endpoint
    if curl -f -s "http://localhost:5000/health" > /dev/null; then
        log_info "✓ Health endpoint responding"
    else
        log_error "✗ Health endpoint not responding"
        return 1
    fi
    
    # Test device list endpoint (if authentication allows)
    if curl -f -s "http://localhost:5000/api/device/list" > /dev/null; then
        log_info "✓ Device list endpoint responding"
    else
        log_warn "⚠ Device list endpoint requires authentication"
    fi
    
    log_info "Deployment verification completed successfully"
}

# Rollback function
rollback() {
    log_error "Deployment failed, initiating rollback..."
    
    # Stop current deployment
    docker-compose down --timeout 30
    
    # Restore backup if available
    if [ -d "$BACKUP_DIR" ]; then
        log_info "Restoring from backup..."
        
        # Restore database if backup exists
        if [ -f "$BACKUP_DIR/postgres_data.tar.gz" ]; then
            docker volume create ${PROJECT_NAME}_postgres_data
            docker run --rm -v ${PROJECT_NAME}_postgres_data:/target -v "$BACKUP_DIR":/backup alpine tar xzf /backup/postgres_data.tar.gz -C /target
        fi
        
        # Restore outputs if backup exists
        if [ -f "$BACKUP_DIR/device_outputs.tar.gz" ]; then
            docker volume create ${PROJECT_NAME}_device_outputs
            docker run --rm -v ${PROJECT_NAME}_device_outputs:/target -v "$BACKUP_DIR":/backup alpine tar xzf /backup/device_outputs.tar.gz -C /target
        fi
        
        # Restore cache if backup exists
        if [ -f "$BACKUP_DIR/device_cache.tar.gz" ]; then
            docker volume create ${PROJECT_NAME}_device_cache
            docker run --rm -v ${PROJECT_NAME}_device_cache:/target -v "$BACKUP_DIR":/backup alpine tar xzf /backup/device_cache.tar.gz -C /target
        fi
        
        log_info "Backup restored"
    fi
    
    log_error "Rollback completed"
    exit 1
}

# Main deployment process
main() {
    echo "Device Operations API - Production Deployment"
    echo "=============================================="
    
    # Set trap for rollback on failure
    trap rollback ERR
    
    check_prerequisites
    create_backup
    deploy
    
    if wait_for_health; then
        verify_deployment
        log_info "Deployment completed successfully!"
        
        echo
        echo "Deployment Summary:"
        echo "=================="
        echo "✓ Application: http://localhost:5000"
        echo "✓ API Documentation: http://localhost:5000/api-docs"
        echo "✓ Health Check: http://localhost:5000/health"
        echo "✓ Grafana Dashboard: http://localhost:3000"
        echo "✓ Prometheus Metrics: http://localhost:9090"
        echo "✓ Seq Logs: http://localhost:5341"
        echo
        echo "Backup Location: $BACKUP_DIR"
        echo
        log_info "Deployment completed successfully!"
    else
        rollback
    fi
}

# Script entry point
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi
