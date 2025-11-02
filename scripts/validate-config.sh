#!/bin/bash

# MarketBrowserMod Configuration Validation Script
# Task 13: Configuration validation and startup checks

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

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

# Validation results
VALIDATION_ERRORS=0
VALIDATION_WARNINGS=0

# Add error
add_error() {
    log_error "$1"
    ((VALIDATION_ERRORS++))
}

# Add warning
add_warning() {
    log_warning "$1"
    ((VALIDATION_WARNINGS++))
}

# Validate file exists
validate_file() {
    local file="$1"
    local description="$2"
    
    if [[ -f "$file" ]]; then
        log_success "$description found: $file"
        return 0
    else
        add_error "$description not found: $file"
        return 1
    fi
}

# Validate directory exists
validate_directory() {
    local dir="$1"
    local description="$2"
    
    if [[ -d "$dir" ]]; then
        log_success "$description found: $dir"
        return 0
    else
        add_error "$description not found: $dir"
        return 1
    fi
}

# Validate command exists
validate_command() {
    local cmd="$1"
    local description="$2"
    
    if command -v "$cmd" &> /dev/null; then
        local version=$(${cmd} --version 2>/dev/null | head -n1 || echo "unknown")
        log_success "$description available: $version"
        return 0
    else
        add_error "$description not found: $cmd"
        return 1
    fi
}

# Validate environment variable
validate_env_var() {
    local var_name="$1"
    local required="$2"
    local min_length="$3"
    
    local value="${!var_name}"
    
    if [[ -z "$value" ]]; then
        if [[ "$required" == "true" ]]; then
            add_error "Required environment variable not set: $var_name"
            return 1
        else
            add_warning "Optional environment variable not set: $var_name"
            return 0
        fi
    fi
    
    if [[ -n "$min_length" ]] && [[ ${#value} -lt $min_length ]]; then
        add_error "Environment variable $var_name is too short (minimum $min_length characters)"
        return 1
    fi
    
    log_success "Environment variable $var_name: ${value:0:10}${#value > 10 ? "..." : ""}"
    return 0
}

# Validate numeric environment variable
validate_numeric_env_var() {
    local var_name="$1"
    local min_value="$2"
    local max_value="$3"
    
    local value="${!var_name}"
    
    if [[ -z "$value" ]]; then
        return 0  # Optional, already handled by validate_env_var
    fi
    
    if ! [[ "$value" =~ ^[0-9]+$ ]]; then
        add_error "Environment variable $var_name must be numeric: $value"
        return 1
    fi
    
    if [[ -n "$min_value" ]] && [[ $value -lt $min_value ]]; then
        add_error "Environment variable $var_name must be at least $min_value: $value"
        return 1
    fi
    
    if [[ -n "$max_value" ]] && [[ $value -gt $max_value ]]; then
        add_error "Environment variable $var_name must be at most $max_value: $value"
        return 1
    fi
    
    log_success "Environment variable $var_name: $value"
    return 0
}

# Main validation function
main() {
    log_info "MarketBrowserMod Configuration Validation"
    log_info "========================================"
    
    # Change to project directory
    cd "$PROJECT_DIR"
    
    # 1. Validate project structure
    log_info "Validating project structure..."
    validate_file "MarketBrowserMod.csproj" "Project file"
    validate_file "Dockerfile.mod" "Docker file"
    validate_file "dual.yaml" "Orleans configuration"
    validate_file "Program.cs" "Main program"
    validate_directory "Controllers" "Controllers directory"
    validate_directory "Models" "Models directory"
    validate_directory "Services" "Services directory"
    validate_directory "wwwroot" "Web root directory"
    validate_file "wwwroot/index.html" "Web interface"
    
    # 2. Validate configuration files
    log_info "Validating configuration files..."
    validate_file "appsettings.json" "Application settings"
    validate_file ".env.example" "Environment template"
    
    if validate_file ".env" "Environment configuration"; then
        # Source .env file for validation
        source .env
    else
        add_warning "No .env file found - using .env.example as reference"
        if [[ -f ".env.example" ]]; then
            source .env.example
        fi
    fi
    
    # 3. Validate deployment files
    log_info "Validating deployment files..."
    validate_file "docker-compose.example.yml" "Docker compose example"
    validate_file "DEPLOYMENT.md" "Deployment guide"
    validate_file "scripts/deploy.sh" "Linux deployment script"
    validate_file "scripts/deploy.bat" "Windows deployment script"
    
    # Check script permissions
    if [[ -f "scripts/deploy.sh" ]]; then
        if [[ -x "scripts/deploy.sh" ]]; then
            log_success "Deploy script is executable"
        else
            add_warning "Deploy script is not executable (run: chmod +x scripts/deploy.sh)"
        fi
    fi
    
    # 4. Validate system dependencies
    log_info "Validating system dependencies..."
    validate_command "docker" "Docker"
    validate_command "docker-compose" "Docker Compose"
    validate_command "dotnet" ".NET SDK"
    validate_command "curl" "cURL (for health checks)"
    
    # 5. Validate environment variables
    log_info "Validating environment variables..."
    
    # Required variables
    validate_env_var "MARKET_BOT_LOGIN" "true" "3"
    validate_env_var "MARKET_BOT_PASSWORD" "true" "8"
    
    # Optional variables with validation
    validate_env_var "QUEUEING" "false"
    validate_numeric_env_var "WEB_PORT" "1" "65535"
    validate_numeric_env_var "REFRESH_INTERVAL_MINUTES" "1" "1440"
    validate_numeric_env_var "MAX_CACHE_AGE_MINUTES" "1" "10080"
    validate_numeric_env_var "MAX_RETRY_ATTEMPTS" "1" "10"
    validate_numeric_env_var "RATE_LIMIT_DELAY_MS" "100" "60000"
    validate_numeric_env_var "CONNECTION_TIMEOUT_SECONDS" "5" "300"
    validate_numeric_env_var "SESSION_RECONNECT_DELAY_MS" "1000" "60000"
    validate_numeric_env_var "MAX_CONSECUTIVE_FAILURES" "1" "20"
    
    # Log level validation
    if [[ -n "$LOG_LEVEL" ]]; then
        case "$LOG_LEVEL" in
            Trace|Debug|Information|Warning|Error|Critical)
                log_success "Log level is valid: $LOG_LEVEL"
                ;;
            *)
                add_error "Invalid log level: $LOG_LEVEL (must be: Trace, Debug, Information, Warning, Error, Critical)"
                ;;
        esac
    fi
    
    # 6. Validate network configuration
    log_info "Validating network configuration..."
    
    # Check if queueing URL is valid
    if [[ -n "$QUEUEING" ]]; then
        if [[ "$QUEUEING" =~ ^https?://[^/]+:[0-9]+$ ]]; then
            log_success "Queueing URL format is valid: $QUEUEING"
        else
            add_warning "Queueing URL format may be invalid: $QUEUEING"
        fi
    fi
    
    # Check web port availability (if not in container)
    local web_port="${WEB_PORT:-8080}"
    if ! netstat -ln 2>/dev/null | grep -q ":$web_port "; then
        log_success "Web port $web_port appears available"
    else
        add_warning "Web port $web_port may already be in use"
    fi
    
    # 7. Validate Docker environment
    log_info "Validating Docker environment..."
    
    # Check if Docker is running
    if docker info >/dev/null 2>&1; then
        log_success "Docker daemon is running"
    else
        add_error "Docker daemon is not running or not accessible"
    fi
    
    # Check if docker-compose file exists in parent directories
    if [[ -f "../docker-compose.yml" ]] || [[ -f "../../docker-compose.yml" ]]; then
        log_success "MyDU docker-compose.yml found in parent directory"
    else
        add_warning "MyDU docker-compose.yml not found - ensure you're in the correct directory"
    fi
    
    # 8. Validate build requirements
    log_info "Validating build requirements..."
    
    # Check .NET version
    if command -v dotnet &> /dev/null; then
        local dotnet_version=$(dotnet --version)
        if [[ "$dotnet_version" =~ ^[8-9]\. ]]; then
            log_success ".NET version is compatible: $dotnet_version"
        else
            add_warning ".NET version may be incompatible: $dotnet_version (recommended: 8.0+)"
        fi
    fi
    
    # Check disk space
    local available_space=$(df . | tail -1 | awk '{print $4}')
    if [[ $available_space -gt 1048576 ]]; then  # 1GB in KB
        log_success "Sufficient disk space available"
    else
        add_warning "Low disk space - may affect Docker builds"
    fi
    
    # 9. Summary
    log_info "Validation Summary"
    log_info "================="
    
    if [[ $VALIDATION_ERRORS -eq 0 ]] && [[ $VALIDATION_WARNINGS -eq 0 ]]; then
        log_success "All validations passed! Configuration is ready for deployment."
        echo
        log_info "Next steps:"
        echo "  1. Run: ./scripts/deploy.sh"
        echo "  2. Access web interface at: http://localhost:${WEB_PORT:-8080}"
        echo "  3. Monitor logs with: docker-compose logs -f marketbrowser"
        exit 0
    elif [[ $VALIDATION_ERRORS -eq 0 ]]; then
        log_success "Validation completed with $VALIDATION_WARNINGS warning(s)."
        echo
        log_info "You can proceed with deployment, but consider addressing the warnings."
        log_info "Run: ./scripts/deploy.sh"
        exit 0
    else
        log_error "Validation failed with $VALIDATION_ERRORS error(s) and $VALIDATION_WARNINGS warning(s)."
        echo
        log_info "Please fix the errors before proceeding with deployment."
        exit 1
    fi
}

# Run main function
main "$@"