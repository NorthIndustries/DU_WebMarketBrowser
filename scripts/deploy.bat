@echo off
REM MarketBrowserMod Deployment Script for Windows
REM Task 13: Deployment automation for MyDU mod deployment pattern

setlocal enabledelayedexpansion

REM Configuration
set SCRIPT_DIR=%~dp0
set PROJECT_DIR=%SCRIPT_DIR%..
set COMPOSE_SERVICE_NAME=marketbrowser

REM Check command line arguments
set COMMAND=%1
if "%COMMAND%"=="" set COMMAND=deploy

REM Main script logic
if "%COMMAND%"=="deploy" goto :deploy
if "%COMMAND%"=="update" goto :update
if "%COMMAND%"=="status" goto :status
if "%COMMAND%"=="logs" goto :logs
if "%COMMAND%"=="cleanup" goto :cleanup
if "%COMMAND%"=="help" goto :help
if "%COMMAND%"=="--help" goto :help
if "%COMMAND%"=="-h" goto :help

echo [ERROR] Unknown command: %COMMAND%
goto :help

:deploy
echo [INFO] Starting MarketBrowserMod deployment...
call :check_environment
if errorlevel 1 exit /b 1

call :validate_configuration
if errorlevel 1 exit /b 1

call :build_image
if errorlevel 1 exit /b 1

call :deploy_service
if errorlevel 1 exit /b 1

call :test_deployment
if errorlevel 1 exit /b 1

echo [SUCCESS] Deployment completed successfully!
echo.
echo Next steps:
echo   1. Access web interface at http://localhost:%WEB_PORT%
echo   2. Monitor logs with: docker-compose logs -f %COMPOSE_SERVICE_NAME%
echo   3. Check health status: curl http://localhost:%WEB_PORT%/health
echo.
goto :eof

:update
echo [INFO] Updating MarketBrowserMod...

REM Check if in git repo
if exist "%PROJECT_DIR%\.git" (
    echo [INFO] Pulling latest changes...
    cd /d "%PROJECT_DIR%"
    git pull
)

call :build_image
if errorlevel 1 exit /b 1

call :deploy_service
if errorlevel 1 exit /b 1

call :test_deployment
if errorlevel 1 exit /b 1

echo [SUCCESS] Update completed successfully!
goto :eof

:status
echo [INFO] MarketBrowserMod Status:

REM Check if service is running
docker-compose ps | findstr "%COMPOSE_SERVICE_NAME%" | findstr "Up" >nul
if errorlevel 1 (
    echo [WARNING] Service is not running
    goto :eof
)

echo [SUCCESS] Service is running

REM Get container info
for /f %%i in ('docker-compose ps -q %COMPOSE_SERVICE_NAME%') do set CONTAINER_ID=%%i
if "%WEB_PORT%"=="" set WEB_PORT=8080

echo   Container ID: %CONTAINER_ID%
echo   Web Interface: http://localhost:%WEB_PORT%

REM Test health endpoint
curl -f -s "http://localhost:%WEB_PORT%/health/live" >nul 2>&1
if errorlevel 1 (
    echo [WARNING] Health check: UNHEALTHY
) else (
    echo [SUCCESS] Health check: HEALTHY
)

goto :eof

:logs
echo [INFO] Showing recent logs...
docker-compose logs --tail=50 %COMPOSE_SERVICE_NAME%
goto :eof

:cleanup
echo [INFO] Cleaning up MarketBrowserMod deployment...

REM Stop service
docker-compose ps | findstr "%COMPOSE_SERVICE_NAME%" >nul
if not errorlevel 1 (
    docker-compose stop %COMPOSE_SERVICE_NAME%
    echo [SUCCESS] Service stopped
)

REM Remove container
docker-compose ps -a | findstr "%COMPOSE_SERVICE_NAME%" >nul
if not errorlevel 1 (
    docker-compose rm -f %COMPOSE_SERVICE_NAME%
    echo [SUCCESS] Container removed
)

REM Remove image (optional)
docker images | findstr "marketbrowsermod" >nul
if not errorlevel 1 (
    set /p REPLY="Remove Docker image? (y/N): "
    if /i "!REPLY!"=="y" (
        docker rmi marketbrowsermod:latest
        echo [SUCCESS] Docker image removed
    )
)

echo [SUCCESS] Cleanup completed
goto :eof

:help
echo MarketBrowserMod Deployment Script for Windows
echo.
echo Usage: %0 [COMMAND]
echo.
echo Commands:
echo   deploy    Deploy the MarketBrowserMod service (default)
echo   update    Update and redeploy the service
echo   status    Show service status and health
echo   logs      Show recent service logs
echo   cleanup   Stop and remove the service
echo   help      Show this help message
echo.
echo Examples:
echo   %0                # Deploy the service
echo   %0 deploy         # Deploy the service
echo   %0 status         # Check service status
echo   %0 logs           # Show logs
echo   %0 cleanup        # Remove service
echo.
goto :eof

:check_environment
echo [INFO] Checking deployment environment...

if not exist "%PROJECT_DIR%\MarketBrowserMod.csproj" (
    echo [ERROR] MarketBrowserMod.csproj not found. Please run from MarketBrowserMod directory.
    exit /b 1
)

if not exist "%PROJECT_DIR%\Dockerfile.mod" (
    echo [ERROR] Dockerfile.mod not found. Please ensure all files are present.
    exit /b 1
)

REM Check if docker-compose is available
docker-compose --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] docker-compose not found. Please install docker-compose.
    exit /b 1
)

REM Check if we're in a MyDU server directory
if not exist "..\docker-compose.yml" (
    if not exist "..\..\docker-compose.yml" (
        echo [WARNING] docker-compose.yml not found in parent directories. Make sure you're in the MyDU server directory.
    )
)

echo [SUCCESS] Environment check passed
goto :eof

:validate_configuration
echo [INFO] Validating configuration...

REM Check for .env file
if not exist "%PROJECT_DIR%\.env" (
    if exist "%PROJECT_DIR%\.env.example" (
        echo [WARNING] .env file not found. Creating from .env.example...
        copy "%PROJECT_DIR%\.env.example" "%PROJECT_DIR%\.env"
        echo [WARNING] Please edit .env file with your configuration before continuing.
        exit /b 1
    ) else (
        echo [ERROR] .env file not found and no .env.example available.
        exit /b 1
    )
)

REM Source .env file (simplified for batch)
for /f "usebackq tokens=1,2 delims==" %%a in ("%PROJECT_DIR%\.env") do (
    if not "%%a"=="" if not "%%b"=="" (
        set %%a=%%b
    )
)

REM Validate required variables
if "%MARKET_BOT_LOGIN%"=="" (
    echo [ERROR] MARKET_BOT_LOGIN not set in .env file
    exit /b 1
)

if "%MARKET_BOT_PASSWORD%"=="" (
    echo [ERROR] MARKET_BOT_PASSWORD not set in .env file
    exit /b 1
)

REM Set defaults for optional variables
if "%WEB_PORT%"=="" set WEB_PORT=8080
if "%REFRESH_INTERVAL_MINUTES%"=="" set REFRESH_INTERVAL_MINUTES=15

echo [SUCCESS] Configuration validation passed
echo [INFO] Bot Login: %MARKET_BOT_LOGIN%
echo [INFO] Web Port: %WEB_PORT%
echo [INFO] Refresh Interval: %REFRESH_INTERVAL_MINUTES% minutes
goto :eof

:build_image
echo [INFO] Building MarketBrowserMod Docker image...

cd /d "%PROJECT_DIR%"

REM Build the image
docker build -f Dockerfile.mod -t marketbrowsermod:latest .
if errorlevel 1 (
    echo [ERROR] Failed to build Docker image
    exit /b 1
)

echo [SUCCESS] Docker image built successfully
goto :eof

:deploy_service
echo [INFO] Deploying MarketBrowserMod service...

REM Check if service is already running
docker-compose ps | findstr "%COMPOSE_SERVICE_NAME%" >nul
if not errorlevel 1 (
    echo [INFO] Service is already running. Stopping...
    docker-compose stop %COMPOSE_SERVICE_NAME%
)

REM Start the service
docker-compose up -d %COMPOSE_SERVICE_NAME%
if errorlevel 1 (
    echo [ERROR] Failed to deploy service
    exit /b 1
)

echo [SUCCESS] Service deployed successfully

REM Wait a moment for startup
echo [INFO] Waiting for service to start...
timeout /t 10 /nobreak >nul

REM Check service status
docker-compose ps | findstr "%COMPOSE_SERVICE_NAME%" | findstr "Up" >nul
if errorlevel 1 (
    echo [ERROR] Service failed to start properly
    docker-compose logs %COMPOSE_SERVICE_NAME%
    exit /b 1
)

echo [SUCCESS] Service is running
goto :eof

:test_deployment
echo [INFO] Testing deployment...

if "%WEB_PORT%"=="" set WEB_PORT=8080
set MAX_ATTEMPTS=30
set ATTEMPT=1

:health_check_loop
curl -f -s "http://localhost:%WEB_PORT%/health/live" >nul 2>&1
if not errorlevel 1 (
    echo [SUCCESS] Health check passed
    goto :health_check_done
)

echo [INFO] Waiting for web server... (attempt %ATTEMPT%/%MAX_ATTEMPTS%)
timeout /t 2 /nobreak >nul
set /a ATTEMPT+=1

if %ATTEMPT% leq %MAX_ATTEMPTS% goto :health_check_loop

echo [ERROR] Health check failed after %MAX_ATTEMPTS% attempts
echo [INFO] Checking service logs...
docker-compose logs --tail=20 %COMPOSE_SERVICE_NAME%
exit /b 1

:health_check_done
REM Test API endpoints
echo [INFO] Testing API endpoints...

curl -f -s "http://localhost:%WEB_PORT%/api/market" >nul 2>&1
if not errorlevel 1 (
    echo [SUCCESS] API endpoint accessible
) else (
    echo [WARNING] API endpoint test failed (may be normal during startup)
)

REM Show service information
echo [INFO] Service Information:
echo   - Web Interface: http://localhost:%WEB_PORT%
echo   - API Endpoints: http://localhost:%WEB_PORT%/api/market
echo   - Health Check: http://localhost:%WEB_PORT%/health
echo   - Container Logs: docker-compose logs -f %COMPOSE_SERVICE_NAME%
goto :eof