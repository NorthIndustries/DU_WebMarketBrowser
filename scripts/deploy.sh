#!/bin/bash

# MarketBrowserMod Deployment Script
# Task 13: Deployment automation for MyDU mod deployment pattern

set -e  # Exit on any error

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
COMPOSE_SERVICE_NAME="marketbrowser"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if running from correct directory
check_environment() {
    log_info "Checking deployment environment..."
    
    if [[ ! -f "$PROJECT_DIR/MarketBrowserMod.csproj" ]]; then
        log_error "MarketBrowserMod.csproj not found. Please run from MarketBrowserMod directory."
        exit 1
    fi
    
    if [[ ! -f "$PROJECT_DIR/Dockerfile.mod" ]]; then
        log_error "Dockerfile.mod not found. Please ensure all files are present."
        exit 1
    fi
    
    # Check if docker-compose is available
    if ! command -v docker-compose &> /dev/null; then
        log_error "docker-compose not found. Please install docker-compose."
        exit 1
    fi
    
    # Check if we're in a MyDU server directory
    if [[ ! -f "../docker-compose.yml" ]] && [[ ! -f "../../docker-compose.yml" ]]; then
        log_warning "docker-compose.yml not found in parent directories. Make sure you're in the MyDU server directory."
    fi
    
    log_success "Environment check passed"
}

# Validate configuration
validate_configuration() {
    log_info "Validating configuration..."
    
    # Check for .env file
    if [[ ! -f "$PROJECT_DIR/.env" ]]; then
        if [[ -f "$PROJECT_DIR/.env.example" ]]; then
            log_warning ".env file not found. Creating from .env.example..."
            cp "$PROJECT_DIR/.env.example" "$PROJECT_DIR/.env"
            log_warning "Please edit .env file with your configuration before continuing."
            exit 1
        else
            log_error ".env file not found and no .env.example available."
            exit 1
        fi
    fi
    
    # Source .env file
    source "$PROJECT_DIR/.env"
    
    # Validate required variables
    if [[ -z "$MARKET_BOT_LOGIN" ]]; then
        log_error "MARKET_BOT_LOGIN not set in .env file"
        exit 1
    fi
    
    if [[ -z "$MARKET_BOT_PASSWORD" ]]; then
        log_error "MARKET_BOT_PASSWORD not set in .env file"
        exit 1
    fi
    
    # Validate bot credentials format
    if [[ ${#MARKET_BOT_LOGIN} -lt 3 ]]; then
        log_error "MARKET_BOT_LOGIN should be at least 3 characters long"
        exit 1
    fi
    
    if [[ ${#MARKET_BOT_PASSWORD} -lt 8 ]]; then
        log_error "MARKET_BOT_PASSWORD should be at least 8 characters long"
        exit 1
    fi
    
    log_success "Configuration validation passed"
    log_info "Bot Login: $MARKET_BOT_LOGIN"
    log_info "Web Port: ${WEB_PORT:-8080}"
    log_info "Refresh Interval: ${REFRESH_INTERVAL_MINUTES:-15} minutes"
}

# Build the Docker image
build_image() {
    log_info "Building MarketBrowserMod Docker image..."
    
    cd "$PROJECT_DIR"
    
    # Build the image
    if docker build -f Dockerfile.mod -t marketbrowsermod:latest .; then
        log_success "Docker image built successfully"
    else
        log_error "Failed to build Docker image"
        exit 1
    fi
}

# Deploy using docker-compose
deploy_service() {
    log_info "Deploying MarketBrowserMod service..."
    
    # Check if service is already running
    if docker-compose ps | grep -q "$COMPOSE_SERVICE_NAME"; then
        log_info "Service is already running. Stopping..."
        docker-compose stop "$COMPOSE_SERVICE_NAME"
    fi
    
    # Start the service
    if docker-compose up -d "$COMPOSE_SERVICE_NAME"; then
        log_success "Service deployed successfully"
    else
        log_error "Failed to deploy service"
        exit 1
    fi
    
    # Wait a moment for startup
    log_info "Waiting for service to start..."
    sleep 10
    
    # Check service status
    if docker-compose ps | grep -q "$COMPOSE_SERVICE_NAME.*Up"; then
        log_success "Service is running"
    else
        log_error "Service failed to start properly"
        docker-compose logs "$COMPOSE_SERVICE_NAME"
        exit 1
    fi
}

# Test the deployment
test_deployment() {
    log_info "Testing deployment..."
    
    local web_port="${WEB_PORT:-8080}"
    local max_attempts=30
    local attempt=1
    
    # Wait for web server to be ready
    while [[ $attempt -le $max_attempts ]]; do
        if curl -f -s "http://localhost:$web_port/health/live" > /dev/null 2>&1; then
            log_success "Health check passed"
            break
        fi
        
        log_info "Waiting for web server... (attempt $attempt/$max_attempts)"
        sleep 2
        ((attempt++))
    done
    
    if [[ $attempt -gt $max_attempts ]]; then
        log_error "Health check failed after $max_attempts attempts"
        log_info "Checking service logs..."
        docker-compose logs --tail=20 "$COMPOSE_SERVICE_NAME"
        exit 1
    fi
    
    # Test API endpoints
    log_info "Testing API endpoints..."
    
    if curl -f -s "http://localhost:$web_port/api/market" > /dev/null; then
        log_success "API endpoint accessible"
    else
        log_warning "API endpoint test failed (may be normal during startup)"
    fi
    
    # Show service information
    log_info "Service Information:"
    echo "  - Web Interface: http://localhost:$web_port"
    echo "  - API Endpoints: http://localhost:$web_port/api/market"
    echo "  - Health Check: http://localhost:$web_port/health"
    echo "  - Container Logs: docker-compose logs -f $COMPOSE_SERVICE_NAME"
}

# Show logs
show_logs() {
    log_info "Showing recent logs..."
    docker-compose logs --tail=50 "$COMPOSE_SERVICE_NAME"
}

# Main deployment function
deploy() {
    log_info "Starting MarketBrowserMod deployment..."
    
    check_environment
    validate_configuration
    build_image
    deploy_service
    test_deployment
    
    log_success "Deployment completed successfully!"
    echo
    log_info "Next steps:"
    echo "  1. Access web interface at http://localhost:${WEB_PORT:-8080}"
    echo "  2. Monitor logs with: docker-compose logs -f $COMPOSE_SERVICE_NAME"
    echo "  3. Check health status: curl http://localhost:${WEB_PORT:-8080}/health"
    echo
}

# Cleanup function
cleanup() {
    log_info "Cleaning up MarketBrowserMod deployment..."
    
    # Stop service
    if docker-compose ps | grep -q "$COMPOSE_SERVICE_NAME"; then
        docker-compose stop "$COMPOSE_SERVICE_NAME"
        log_success "Service stopped"
    fi
    
    # Remove container
    if docker-compose ps -a | grep -q "$COMPOSE_SERVICE_NAME"; then
        docker-compose rm -f "$COMPOSE_SERVICE_NAME"
        log_success "Container removed"
    fi
    
    # Remove image (optional)
    if docker images | grep -q "marketbrowsermod"; then
        read -p "Remove Docker image? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            docker rmi marketbrowsermod:latest
            log_success "Docker image removed"
        fi
    fi
    
    log_success "Cleanup completed"
}

# Update function
update() {
    log_info "Updating MarketBrowserMod..."
    
    # Pull latest changes (if in git repo)
    if [[ -d "$PROJECT_DIR/.git" ]]; then
        log_info "Pulling latest changes..."
        cd "$PROJECT_DIR"
        git pull
    fi
    
    # Rebuild and redeploy
    build_image
    deploy_service
    test_deployment
    
    log_success "Update completed successfully!"
}

# Status function
status() {
    log_info "MarketBrowserMod Status:"
    
    # Check if service is running
    if docker-compose ps | grep -q "$COMPOSE_SERVICE_NAME.*Up"; then
        log_success "Service is running"
        
        # Get container info
        local container_id=$(docker-compose ps -q "$COMPOSE_SERVICE_NAME")
        local web_port="${WEB_PORT:-8080}"
        
        echo "  Container ID: $container_id"
        echo "  Web Interface: http://localhost:$web_port"
        
        # Test health endpoint
        if curl -f -s "http://localhost:$web_port/health/live" > /dev/null 2>&1; then
            log_success "Health check: HEALTHY"
        else
            log_warning "Health check: UNHEALTHY"
        fi
        
        # Show resource usage
        echo "  Resource Usage:"
        docker stats --no-stream --format "table {{.CPUPerc}}\t{{.MemUsage}}" "$container_id" | tail -n +2 | while read line; do
            echo "    CPU: $(echo $line | cut -f1)  Memory: $(echo $line | cut -f2)"
        done
        
    else
        log_warning "Service is not running"
    fi
}

# Help function
show_help() {
    echo "MarketBrowserMod Deployment Script"
    echo
    echo "Usage: $0 [COMMAND]"
    echo
    echo "Commands:"
    echo "  deploy    Deploy the MarketBrowserMod service (default)"
    echo "  update    Update and redeploy the service"
    echo "  status    Show service status and health"
    echo "  logs      Show recent service logs"
    echo "  cleanup   Stop and remove the service"
    echo "  help      Show this help message"
    echo
    echo "Examples:"
    echo "  $0                # Deploy the service"
    echo "  $0 deploy         # Deploy the service"
    echo "  $0 status         # Check service status"
    echo "  $0 logs           # Show logs"
    echo "  $0 cleanup        # Remove service"
    echo
}

# Main script logic
case "${1:-deploy}" in
    deploy)
        deploy
        ;;
    update)
        update
        ;;
    status)
        status
        ;;
    logs)
        show_logs
        ;;
    cleanup)
        cleanup
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        log_error "Unknown command: $1"
        show_help
        exit 1
        ;;
esac