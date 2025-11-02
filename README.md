# Market Browser Mod for MyDU

A comprehensive market browser and analysis tool for MyDU (My Dual Universe) game servers. This mod provides a web-based interface for browsing market orders, analyzing trading opportunities, and planning profitable routes across all markets in your Dual Universe server.

## Overview

The Market Browser Mod connects to your MyDU server's database and Orleans services to collect real-time market data. It provides both a web interface and a RESTful API for accessing market information, finding profitable trading opportunities, and analyzing market trends.

The mod runs as a Docker container alongside your MyDU server and automatically refreshes market data at configurable intervals. All market data is cached locally to minimize database queries and improve performance.

## Features

### Market Data Collection

- Automatic discovery of all active markets on the server
- Real-time order fetching from all markets (buy and sell orders)
- Item name resolution from game database
- Player name resolution for orders
- Intelligent caching system to reduce server load
- Support for both PostgreSQL database queries and Orleans service calls

### Web Interface

- Responsive design that works on desktop and mobile devices
- Market browsing with search and filtering capabilities
- Order search with multiple filter options (item, market, planet, price range, order type)
- Sortable tables with multiple sorting options
- Real-time statistics dashboard
- Profit opportunity analysis with detailed breakdowns
- Route optimization suggestions
- Distance calculations between markets
- Planet-based market organization

### Profit Analysis

- Automatic identification of profitable trading opportunities
- Profit margin calculations
- Total profit potential calculations
- Maximum quantity analysis
- Distance-based route planning
- Profit per kilometer metrics
- Base market distance calculations
- Route optimization with multiple stops

### API Endpoints

- Comprehensive RESTful API for integration with other tools
- Market data endpoints with pagination and filtering
- Order search endpoints with advanced query options
- Profit opportunity endpoints with customizable filters
- Statistics and health check endpoints
- Distance calculation endpoints
- Route optimization endpoints

## Requirements

- MyDU game server (running version that supports mods)
- Docker and Docker Compose
- PostgreSQL database access (for market data)
- Orleans service access (for real-time data)
- Bot account on the MyDU server with market access permissions
- Port 8080 available (configurable)

## Installation

### Prerequisites

Before installing, ensure you have:

1. Access to your MyDU server root folder and docker-compose.yml file
2. Bot account credentials created through the MyDU backoffice
3. Network access to the server's PostgreSQL database and Orleans services
4. An available IP address in your server's network range (default: 10.5.0.52)

### MyDU Server Integration

This mod integrates directly into your MyDU server installation. Follow these steps to set it up:

#### Step 1: Clone the Repository

Clone this repository into your MyDU server root folder:

```bash
cd /path/to/your/mydu/server/root
git clone https://github.com/NorthIndustries/DU_WebMarketBrowser.git MarketBrowserMod
cd MarketBrowserMod
```

Or using SSH:

```bash
cd /path/to/your/mydu/server/root
git clone git@github.com:NorthIndustries/DU_WebMarketBrowser.git MarketBrowserMod
cd MarketBrowserMod
```

Stay in the MyDU server root folder for all subsequent commands (not inside the MarketBrowserMod folder).

#### Step 2: Edit docker-compose.yml

Add the following service configuration to your MyDU server's `docker-compose.yml` file:

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
    LOG_LEVEL: Warning
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

**Important notes:**

- Adjust the `ipv4_address` (10.5.0.52) to match an available IP in your server's network range
- Ensure the port `8080` is available or change it to another port
- The network name `vpcbr` should match your MyDU server's network configuration

#### Step 3: Configure Environment Variables

Edit your MyDU server's `.env` file and add the following bot account credentials:

```bash
MARKET_BOT_LOGIN=marketbrowser_bot
MARKET_BOT_PASSWORD=your_secure_password_here
```

Replace `your_secure_password_here` with a secure password. You will need to create a bot account in the MyDU backoffice with these exact credentials.

**Note:** Check the `MarketBrowserMod` folder for any example environment files (like `.env.example`) that may contain additional configuration options.

#### Step 4: Create Bot Account

Before starting the container, create the bot account through the MyDU backoffice:

1. Log into your MyDU backoffice
2. Create a new bot account with:
   - Username: Must match `MARKET_BOT_LOGIN` from your `.env` file
   - Password: Must match `MARKET_BOT_PASSWORD` from your `.env` file
   - Permissions: Basic market access permissions (no special privileges required)

#### Step 5: Build and Start the Container

From your MyDU server root folder, run:

```bash
docker compose build marketbrowser
docker compose up -d marketbrowser
```

The first command builds the Docker image, and the second starts the container in detached mode.

#### Step 6: Auto-Start on Server Restart (Optional)

To automatically start the marketbrowser when your MyDU server starts, edit the `./scripts/up.sh` file and add the following lines at the end:

```bash
sleep 5
docker compose up -d marketbrowser
```

This ensures the marketbrowser container starts automatically after a short delay when the server boots.

#### Step 7: Verify Installation

Check that the container is running:

```bash
docker ps | grep MarketBrowser
```

View logs to verify connection:

```bash
docker logs mod_MarketBrowser
```

Access the web interface at `http://your-server-ip:8080` or `http://localhost:8080` if accessing from the server itself.

## Configuration

### Environment Variables

The mod can be configured using environment variables:

| Variable                   | Default       | Description                                                |
| -------------------------- | ------------- | ---------------------------------------------------------- |
| `BOT_LOGIN`                | _required_    | Bot account username                                       |
| `BOT_PASSWORD`             | _required_    | Bot account password                                       |
| `WEB_PORT`                 | `8080`        | Web server port number                                     |
| `REFRESH_INTERVAL_MINUTES` | `15`          | How often to refresh market data (in minutes)              |
| `MAX_CACHE_AGE_MINUTES`    | `60`          | Maximum cache age before data is considered stale          |
| `LOG_LEVEL`                | `Information` | Application log level (Debug, Information, Warning, Error) |
| `PROFIT_MARGIN_THRESHOLD`  | `0.1`         | Minimum profit margin (10%) for opportunity filtering      |

### Configuration Files

The mod uses the standard MyDU configuration files:

- `dual.yaml` - Main server configuration
- `config.json` - Additional mod-specific settings (if needed)

## Usage

### Web Interface

Once running, access the web interface at:

```
http://your-server-ip:8080
```

The interface includes several views:

**Markets View**

- Browse all markets on the server
- Filter by planet name or market name
- View order counts per market
- View distance from origin or base market

**Orders View**

- Search and filter all market orders
- Filter by item name, market, planet, price range, order type
- Sort by any column
- Pagination for large result sets

**Profit Opportunities View**

- View all profitable trading opportunities
- Filter by minimum profit margin
- Filter by distance constraints
- View detailed profit breakdowns
- See route optimization suggestions

**Statistics View**

- Market statistics overview
- Order statistics
- Cache status information
- System health monitoring

### API Usage

The mod provides a comprehensive REST API for programmatic access.

#### Market Endpoints

```
GET /api/market/markets
GET /api/market/markets/{marketId}
GET /api/market/markets/planet/{planetName}
```

Query parameters: `page`, `pageSize`, `sortBy`, `sortOrder`, `planetName`, `marketName`

#### Order Endpoints

```
GET /api/market/orders
GET /api/market/orders/{orderId}
GET /api/market/orders/search/item?itemName={name}
GET /api/market/orders/search/market?marketName={name}
GET /api/market/orders/search/player?playerName={name}
```

Query parameters: `itemName`, `marketName`, `planetName`, `orderType` (buy/sell), `minPrice`, `maxPrice`, `playerName`, `page`, `pageSize`, `sortBy`, `sortOrder`

#### Profit Opportunity Endpoints

```
GET /api/market/profits
GET /api/market/profits/top?count={n}&metric={metric}
GET /api/market/profits/item/{itemName}
GET /api/market/profits/distance?maxDistance={distance}
GET /api/market/profits/routes/optimized
```

Query parameters: `itemName`, `minProfitMargin`, `maxDistance`, `baseMarketId`, `page`, `pageSize`, `sortBy`, `sortOrder`

#### System Endpoints

```
GET /api/market/status
GET /api/market/stats
GET /api/market/version
GET /health/live
GET /health/ready
```

For detailed API documentation, see the inline code documentation or inspect the endpoints directly.

## Project Structure

```
MarketBrowserMod/
├── Controllers/          # API controllers (MarketController)
├── Models/              # Data models and DTOs
├── Services/            # Business logic services
│   ├── MarketDataService.cs
│   ├── DatabaseMarketService.cs
│   ├── ProfitAnalysisService.cs
│   ├── RouteOptimizationService.cs
│   └── ...
├── Utils/               # Utility classes
│   ├── DistanceFormatter.cs
│   └── PriceConverter.cs
├── wwwroot/             # Web interface files
│   └── index.html
├── Tests/               # Unit and integration tests
├── APIReference/        # API documentation and IDL files
├── Program.cs           # Main application entry point
├── Dockerfile.mod       # Docker build file
└── README.md           # This file
```

## Development

### Building from Source

To build the project from source:

```bash
dotnet restore
dotnet build -c Release
```

### Running Tests

```bash
dotnet test
```

Or use the provided test script:

```bash
./run-tests.sh
```

### Extending the Mod

The mod is designed to be extensible:

- Add new API endpoints in `Controllers/MarketController.cs`
- Extend data models in `Models/`
- Add new analysis features in `Services/`
- Customize the web interface in `wwwroot/index.html`

### Code Organization

- **Controllers**: Handle HTTP requests and route to services
- **Models**: Data structures, DTOs, and model extensions
- **Services**: Business logic and data access
- **Utils**: Shared utility functions and helpers

## Troubleshooting

### Bot Login Failed

- Verify `BOT_LOGIN` and `BOT_PASSWORD` environment variables are correct
- Ensure the bot account exists in the MyDU backoffice
- Check that the bot account has market access permissions
- Review container logs: `docker logs mod_MarketBrowser`

### No Market Data Appearing

- Verify markets exist on your server
- Check Orleans connection is working (check logs)
- Verify PostgreSQL database connectivity
- Ensure the bot account has proper permissions
- Check the refresh interval hasn't expired

### Web Interface Not Loading

- Verify port 8080 is accessible and not blocked by firewall
- Check container is running: `docker ps | grep MarketBrowser`
- Check container logs for errors: `docker logs mod_MarketBrowser`
- Verify health check endpoint: `curl http://localhost:8080/health/live`

### Slow Performance

- Increase `REFRESH_INTERVAL_MINUTES` to reduce refresh frequency
- Check server resources (CPU/memory usage)
- Review database query performance
- Consider reducing `MAX_CACHE_AGE_MINUTES` if data freshness is not critical

### Database Connection Issues

- Verify PostgreSQL is accessible from the container
- Check database connection string in configuration
- Ensure database user has proper permissions
- Review database logs for connection errors

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

Contributions are welcome and encouraged. To contribute:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Test thoroughly
5. Commit your changes (`git commit -m 'Add some amazing feature'`)
6. Push to the branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

Please ensure your code follows the existing style and includes appropriate tests.

## Support

For support and questions:

- Check this README and the DEPLOYMENT.md file
- Review the server logs for error messages
- Check the MyDU modding documentation
- Ask in the MyDU community forums or Discord

## Credits

Developed for the MyDU community by karich.design.

Copyright (c) 2025. All rights reserved.
