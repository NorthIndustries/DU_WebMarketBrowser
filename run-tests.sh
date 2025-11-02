#!/bin/bash

# MarketBrowserMod Test Runner Script
# This script runs the comprehensive testing suite for the MarketBrowserMod

set -e  # Exit on any error

echo "=========================================="
echo "MarketBrowserMod Comprehensive Test Suite"
echo "=========================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    print_error ".NET SDK is not installed or not in PATH"
    exit 1
fi

print_status "Using .NET version: $(dotnet --version)"

# Navigate to the project directory
cd "$(dirname "$0")"
PROJECT_DIR=$(pwd)
print_status "Project directory: $PROJECT_DIR"

# Restore dependencies
print_status "Restoring NuGet packages..."
if dotnet restore; then
    print_success "NuGet packages restored successfully"
else
    print_error "Failed to restore NuGet packages"
    exit 1
fi

# Build the project
print_status "Building the project..."
if dotnet build --no-restore --configuration Release; then
    print_success "Project built successfully"
else
    print_error "Failed to build project"
    exit 1
fi

# Initialize test results
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0
SKIPPED_TESTS=0

# Function to run tests and capture results
run_test_category() {
    local category=$1
    local filter=$2
    local description=$3
    
    echo ""
    print_status "Running $description..."
    
    # Run tests with filter and capture output
    local test_output
    local test_exit_code
    
    if test_output=$(dotnet test --no-build --configuration Release --logger "console;verbosity=normal" --filter "$filter" 2>&1); then
        test_exit_code=0
    else
        test_exit_code=$?
    fi
    
    # Parse test results from output
    local category_passed=$(echo "$test_output" | grep -o "Passed: [0-9]*" | grep -o "[0-9]*" || echo "0")
    local category_failed=$(echo "$test_output" | grep -o "Failed: [0-9]*" | grep -o "[0-9]*" || echo "0")
    local category_skipped=$(echo "$test_output" | grep -o "Skipped: [0-9]*" | grep -o "[0-9]*" || echo "0")
    
    # Update totals
    TOTAL_TESTS=$((TOTAL_TESTS + category_passed + category_failed + category_skipped))
    PASSED_TESTS=$((PASSED_TESTS + category_passed))
    FAILED_TESTS=$((FAILED_TESTS + category_failed))
    SKIPPED_TESTS=$((SKIPPED_TESTS + category_skipped))
    
    # Print category results
    if [ $test_exit_code -eq 0 ] && [ $category_failed -eq 0 ]; then
        print_success "$description completed - Passed: $category_passed, Failed: $category_failed, Skipped: $category_skipped"
    else
        print_warning "$description completed with issues - Passed: $category_passed, Failed: $category_failed, Skipped: $category_skipped"
        if [ $category_failed -gt 0 ]; then
            echo "$test_output" | grep -A 5 -B 5 "Failed\|Error" || true
        fi
    fi
}

# Run different test categories
echo ""
print_status "Starting test execution..."

# Unit Tests
run_test_category "unit" "Category=Unit|FullyQualifiedName~Unit" "Unit Tests"

# Integration Tests  
run_test_category "integration" "Category=Integration|FullyQualifiedName~Integration" "Integration Tests"

# Performance Tests
run_test_category "performance" "Category=Performance|FullyQualifiedName~Performance" "Performance Tests"

# Container Tests (may require Docker)
if command -v docker &> /dev/null; then
    print_status "Docker detected, running container tests..."
    run_test_category "container" "FullyQualifiedName~Container" "Container Integration Tests"
else
    print_warning "Docker not found, skipping container tests"
fi

# Generate test coverage report if coverlet is available
print_status "Checking for test coverage tools..."
if dotnet tool list -g | grep -q "coverlet.console"; then
    print_status "Generating test coverage report..."
    dotnet test --no-build --configuration Release \
        --collect:"XPlat Code Coverage" \
        --results-directory:"./TestResults" \
        --logger "console;verbosity=normal" || print_warning "Coverage collection failed"
else
    print_warning "Coverlet not installed globally, skipping coverage report"
    print_status "To install: dotnet tool install -g coverlet.console"
fi

# Run static analysis if available
if command -v dotnet-sonarscanner &> /dev/null; then
    print_status "Running static analysis..."
    # This would require SonarQube setup
    print_warning "SonarQube analysis requires server configuration"
else
    print_status "SonarScanner not available, skipping static analysis"
fi

# Print final results
echo ""
echo "=========================================="
echo "Test Execution Summary"
echo "=========================================="

if [ $FAILED_TESTS -eq 0 ]; then
    print_success "All tests passed successfully!"
else
    print_error "Some tests failed!"
fi

echo "Total Tests:  $TOTAL_TESTS"
echo "Passed:       $PASSED_TESTS"
echo "Failed:       $FAILED_TESTS"
echo "Skipped:      $SKIPPED_TESTS"

if [ $TOTAL_TESTS -gt 0 ]; then
    SUCCESS_RATE=$(( (PASSED_TESTS * 100) / TOTAL_TESTS ))
    echo "Success Rate: ${SUCCESS_RATE}%"
fi

# Check for test results files
if [ -d "./TestResults" ]; then
    print_status "Test results saved to ./TestResults/"
    ls -la ./TestResults/ || true
fi

# Performance recommendations
echo ""
print_status "Performance Test Recommendations:"
echo "- Unit tests should complete in < 100ms each"
echo "- Integration tests should complete in < 5s each"
echo "- Cache operations should handle 1000+ concurrent requests"
echo "- Memory usage should remain stable under load"

# Exit with appropriate code
if [ $FAILED_TESTS -eq 0 ]; then
    print_success "Test suite completed successfully!"
    exit 0
else
    print_error "Test suite completed with failures!"
    exit 1
fi