# MarketBrowserMod Deployment Guide

This guide covers the complete deployment setup for MarketBrowserMod following the MyDU mod deployment patterns.

## Prerequisites

- MyDU server installation with docker-compose
- Docker and docker-compose installed
- Bot account created in MyDU backoffice
- Available IP address in the 10.5.0.x range for the container

## Quick Start

### 1. Environment Configuration

Copy the environment template and configure your values:

```bash
cp .env.example .env
```

Edit `.env` and set the required values:

```bash
# Required - Bot credentials
MARKET_BOT_LOGIN=marketbrowser_bot
MARKET_BOT_PASSWORD=your_secure_password_here

# Optional - Adjust as needed
WEB_PORT=8080
REFRESH_INTERVAL_MINUTES=15
```

### 2. Docker Compose Integration

Add the MarketBrowserMod service to your main MyDU `docker-compose.yml`:

```yaml
marketbrowser:
  build:
    context: ./MarketBrowserMod
    dockerfile: Dockerfile.mod
  container_name: mod_MarketBrowser
  pull_policy: never
  command: ["/Mod/MarketBrowserMod", "/config/dual.yaml"]
  volumes:
    - ${CONFPATH}:/config:ro
    - ${LOGPATH}:/logs
  environment:
    BOT_LOGIN: ${MARKET_BOT_LOGIN}
    BOT_PASSWORD: ${MARKET_BOT_PASSWORD}
    QUEUEING: http://queueing:9630
    WEB_PORT: 8080
    REFRESH_INTERVAL_MINUTES: 15
    MAX_CACHE_AGE_MINUTES: 60
    LOG_LEVEL: Information
    ASPNETCORE_ENVIRONMENT: Production
  ports:
    - "8080:8080"
  restart: unless-stopped
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:8080/health/live"]
    interval: 30s
    timeout: 10s
    retries: 3
    start_period: 60s
  networks:
    vpcbr:
      ipv4_address: 10.5.0.52
```

### 3. Build and Deploy

```bash
# Build the mod
docker-compose build marketbrowser

# Start the service
docker-compose up -d marketbrowser

# Check logs
docker-compose logs -f marketbrowser
```

## Configuration Reference

### Required Environment Variables

| Variable       | Description          | Example               |
| -------------- | -------------------- | --------------------- |
| `BOT_LOGIN`    | Bot account username | `marketbrowser_bot`   |
| `BOT_PASSWORD` | Bot account password | `secure_password_123` |

### Optional Environment Variables

| Variable                     | Default                | Description                        |
| ---------------------------- | ---------------------- | ---------------------------------- |
| `QUEUEING`                   | `http://queueing:9630` | Orleans queueing service URL       |
| `WEB_PORT`                   | `8080`                 | Web server port                    |
| `REFRESH_INTERVAL_MINUTES`   | `15`                   | Market data refresh interval       |
| `MAX_CACHE_AGE_MINUTES`      | `60`                   | Maximum cache age before stale     |
| `MAX_RETRY_ATTEMPTS`         | `3`                    | Orleans operation retry attempts   |
| `RATE_LIMIT_DELAY_MS`        | `1000`                 | Delay between API calls            |
| `CONNECTION_TIMEOUT_SECONDS` | `30`                   | Orleans connection timeout         |
| `SESSION_RECONNECT_DELAY_MS` | `5000`                 | Session reconnection delay         |
| `MAX_CONSECUTIVE_FAILURES`   | `5`                    | Max failures before extended delay |
| `LOG_LEVEL`                  | `Information`          | Application log level              |
| `MAX_DISTANCE_KM`            | `1000000`              | Maximum route calculation distance |
| `PROFIT_MARGIN_THRESHOLD`    | `0.1`                  | Minimum profit margin (10%)        |

### Network Configuration

The mod uses the standard MyDU VPC bridge network:

- **Network**: `vpcbr` (10.5.0.0/16)
- **Default IP**: `10.5.0.52` (configurable)
- **Internal Communication**: Uses Docker internal DNS
- **External Access**: Web interface on configured port

## Bot Account Setup

### 1. Create Bot Account

In MyDU backoffice:

1. Navigate to Player Management
2. Create new player account
3. Set username to match `BOT_LOGIN` environment variable
4. Set password to match `BOT_PASSWORD` environment variable
5. Ensure account has basic market access permissions

### 2. Bot Permissions

The bot requires minimal permissions:

- ✅ **Market Access**: Can view and query market data
- ✅ **Basic Player Info**: Can retrieve player information
- ❌ **Admin Permissions**: Not required
- ❌ **Special Privileges**: Not required

### 3. Bot Funding

The bot doesn't need funds for basic operation, but may need credits if:

- Server requires payment for market access
- Bot needs to perform test transactions (not implemented)

## Health Monitoring

### Health Check Endpoints

| Endpoint        | Purpose               | Use Case                           |
| --------------- | --------------------- | ---------------------------------- |
| `/health`       | Overall health status | General monitoring                 |
| `/health/ready` | Readiness probe       | Kubernetes/container orchestration |
| `/health/live`  | Liveness probe        | Container restart decisions        |

### Monitoring Integration

For Kubernetes or advanced container orchestration:

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 60
  periodSeconds: 30

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
```

## Performance Tuning

### Resource Allocation

Recommended container resources:

```yaml
deploy:
  resources:
    limits:
      memory: 512M
      cpus: "0.5"
    reservations:
      memory: 256M
      cpus: "0.25"
```

### Configuration Tuning

For high-traffic servers:

```bash
# Reduce refresh frequency to lower server load
REFRESH_INTERVAL_MINUTES=30

# Increase cache age to serve stale data longer
MAX_CACHE_AGE_MINUTES=120

# Increase rate limiting to be more conservative
RATE_LIMIT_DELAY_MS=2000
```

For low-latency requirements:

```bash
# Increase refresh frequency for more current data
REFRESH_INTERVAL_MINUTES=5

# Reduce cache age for fresher data
MAX_CACHE_AGE_MINUTES=30

# Reduce rate limiting for faster updates
RATE_LIMIT_DELAY_MS=500
```

## Security Considerations

### Bot Credentials

- Use strong, unique passwords for bot accounts
- Consider using Docker secrets for production:

```yaml
secrets:
  bot_password:
    external: true

services:
  marketbrowser:
    secrets:
      - bot_password
    environment:
      BOT_PASSWORD_FILE: /run/secrets/bot_password
```

### Network Security

- The web interface has no built-in authentication
- Consider using a reverse proxy with authentication:

```yaml
# nginx reverse proxy example
location /market/ {
auth_basic "Market Browser";
auth_basic_user_file /etc/nginx/.htpasswd;
proxy_pass http://10.5.0.52:8080/;
}
```

### Firewall Configuration

- Only expose necessary ports
- Use Docker's built-in network isolation
- Consider restricting access to internal networks only

## Troubleshooting

### Common Issues

#### 1. Bot Authentication Failed

**Symptoms**: "BOT_LOGIN environment variable is required" or authentication errors

**Solutions**:

- Verify `BOT_LOGIN` and `BOT_PASSWORD` environment variables are set
- Check bot account exists in MyDU backoffice
- Ensure bot credentials match exactly (case-sensitive)
- Verify bot account is not locked or disabled

#### 2. Orleans Connection Failed

**Symptoms**: "Cannot reach queueing service" or Orleans connection errors

**Solutions**:

- Verify `QUEUEING` URL is correct (default: `http://queueing:9630`)
- Check queueing service is running: `docker-compose ps queueing`
- Verify network connectivity: `docker-compose exec marketbrowser ping queueing`
- Check Orleans service logs: `docker-compose logs orleans`

#### 3. Web Interface Not Accessible

**Symptoms**: Cannot access web interface on configured port

**Solutions**:

- Verify port mapping in docker-compose.yml
- Check if port is already in use: `netstat -ln | grep 8080`
- Verify container is running: `docker-compose ps marketbrowser`
- Check container logs: `docker-compose logs marketbrowser`

#### 4. No Market Data

**Symptoms**: Empty market data or "No markets found"

**Solutions**:

- Verify markets exist on your server
- Check bot has market access permissions
- Review logs for specific error messages
- Try manual refresh: `curl -X POST http://localhost:8080/api/market/refresh`

#### 5. High Memory Usage

**Symptoms**: Container using excessive memory

**Solutions**:

- Increase refresh interval to reduce data collection frequency
- Implement memory limits in docker-compose.yml
- Monitor cache size via `/api/market/stats` endpoint
- Consider restarting container periodically

### Log Analysis

Enable debug logging for detailed troubleshooting:

```bash
LOG_LEVEL=Debug
```

Key log patterns to look for:

- `✓ All startup validations passed` - Successful startup
- `Session invalid, reconnecting` - Orleans session issues
- `Cache Stats - Markets: X, Orders: Y` - Data collection status
- `Web API server started successfully` - Web server ready

### Performance Monitoring

Monitor key metrics:

```bash
# Check container stats
docker stats mod_MarketBrowser

# Monitor API response times
curl -w "@curl-format.txt" -o /dev/null -s http://localhost:8080/api/market/stats

# Check health status
curl http://localhost:8080/health
```

## Advanced Configuration

### Custom Configuration Files

Mount custom configuration files:

```yaml
volumes:
  - ./custom-appsettings.json:/Mod/appsettings.Production.json:ro
```

### Multiple Instances

Run multiple instances for load balancing:

```yaml
marketbrowser1:
  # ... configuration ...
  networks:
    vpcbr:
      ipv4_address: 10.5.0.52

marketbrowser2:
  # ... configuration ...
  networks:
    vpcbr:
      ipv4_address: 10.5.0.53
```

### Integration with Monitoring Systems

For Prometheus monitoring:

```yaml
environment:
  ENABLE_METRICS: "true"
  METRICS_PORT: "9090"
```

## Support and Maintenance

### Regular Maintenance

- Monitor container logs regularly
- Update bot passwords periodically
- Review performance metrics
- Update container images when available

### Backup and Recovery

The mod uses in-memory caching, so no persistent data backup is required. Configuration and logs should be backed up as part of your regular server backup procedures.

### Updates and Upgrades

To update the mod:

```bash
# Pull latest changes
git pull

# Rebuild container
docker-compose build marketbrowser

# Restart with new version
docker-compose up -d marketbrowser
```

## Getting Help

For support:

1. Check this deployment guide
2. Review container logs for error messages
3. Verify configuration against examples
4. Check MyDU community forums
5. Submit issues to the project repository

## License and Contributing

This mod is part of the MyDU server modding toolkit. See the main project documentation for licensing terms and contribution guidelines.
