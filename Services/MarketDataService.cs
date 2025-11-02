using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using MarketBrowserMod.Models;
using NQ;
using NQ.Interfaces;
using Orleans;
using BotLib.BotClient;
using BotLib.Generated;
using NQutils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Backend;
using Backend.Business;

namespace MarketBrowserMod.Services
{
    /// <summary>
    /// Service for collecting and caching market data from Orleans grains
    /// Implements requirements 1.3, 1.4, 5.1, 5.2, 5.3, 5.5, 8.1, 8.2, 8.4 from the specification
    /// </summary>
    public class MarketDataService
    {
        private readonly IClusterClient orleans;
        private readonly ILogger<MarketDataService> logger;
        private readonly DatabaseMarketService databaseMarketService;
        private readonly ItemNameService itemNameService;
        
        // Thread-safe cache structures (Requirement 5.1)
        private readonly Dictionary<ulong, MarketData> marketCache = new();
        private readonly Dictionary<ulong, PlanetData> planetCache = new();
        private readonly Dictionary<ulong, string> playerNameCache = new();
        private readonly Dictionary<ulong, string> itemNameCache = new();
        private readonly List<OrderData> allOrders = new();
        private readonly ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();
        
        // Database-driven location cache for accurate planet identification
        private readonly Dictionary<ulong, MarketLocationInfo> marketLocationCache = new();
        private readonly Dictionary<ulong, PlanetInfo> planetInfoCache = new();
        private readonly Dictionary<ulong, List<ulong>> marketsByPlanetCache = new();
        
        // Cache metadata for staleness detection (Requirement 5.3)
        private DateTime lastSuccessfulRefresh = DateTime.MinValue;
        private DateTime lastRefreshAttempt = DateTime.MinValue;
        private bool isRefreshing = false;
        private int consecutiveFailures = 0;
        private readonly object refreshLock = new object();
        
        // Configuration for refresh intervals (Requirement 5.2)
        private readonly TimeSpan refreshInterval;
        private readonly TimeSpan maxCacheAge;
        private readonly int maxRetryAttempts;
        
        // Orleans service availability tracking (Requirement 5.5)
        private bool orleansAvailable = true;
        private DateTime lastOrleansFailure = DateTime.MinValue;
        
        private Client? bot;
        private IGameplayBank? gameplayBank;

        public MarketDataService(IClusterClient orleans, ILogger<MarketDataService> logger, 
            DatabaseMarketService databaseMarketService, ItemNameService itemNameService, TimeSpan? refreshInterval = null, TimeSpan? maxCacheAge = null, int maxRetryAttempts = 3)
        {
            this.orleans = orleans ?? throw new ArgumentNullException(nameof(orleans));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.databaseMarketService = databaseMarketService ?? throw new ArgumentNullException(nameof(databaseMarketService));
            this.itemNameService = itemNameService ?? throw new ArgumentNullException(nameof(itemNameService));
            
            // Configurable intervals (Requirement 5.2)
            this.refreshInterval = refreshInterval ?? TimeSpan.FromMinutes(15);
            this.maxCacheAge = maxCacheAge ?? TimeSpan.FromHours(1);
            this.maxRetryAttempts = maxRetryAttempts;
            
            logger.LogInformation($"MarketDataService configured with refresh interval: {this.refreshInterval}, max cache age: {this.maxCacheAge}");
        }

        /// <summary>
        /// Initialize the MarketDataService with bot authentication and initial data loading
        /// Requirement 1.1, 1.2: Bot authentication and Orleans client setup
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                logger.LogInformation("Initializing MarketDataService...");
                
                // Note: Based on Task 3 discoveries, we should reuse the existing bot session
                // rather than creating a new one to avoid InvalidSession errors
                logger.LogInformation("MarketDataService will use the main bot session from Program.cs");

                // Initialize GameplayBank for item name resolution
                // Note: We'll get the GameplayBank when we need it since serviceProvider access is restricted
                logger.LogInformation("MarketDataService initialized, GameplayBank will be accessed as needed");

                // Load initial planet data with actual positions
                await LoadPlanetData();
                logger.LogInformation("Planet data loaded");

                logger.LogInformation("MarketDataService initialized successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize MarketDataService");
                throw;
            }
        }

        /// <summary>
        /// Set the bot client to use for market data collection
        /// Based on Task 3 discoveries, we reuse the main bot session
        /// </summary>
        public void SetBotClient(Client botClient)
        {
            bot = botClient ?? throw new ArgumentNullException(nameof(botClient));
            logger.LogInformation($"Bot client set for MarketDataService: Player ID {bot.PlayerId}");
        }

        /// <summary>
        /// Load planet data for market location context using database-driven approach
        /// Requirement 4.1, 4.2: Planet identification and location data
        /// </summary>
        private async Task LoadPlanetData()
        {
            try
            {
                logger.LogInformation("Loading planet data using database-driven approach...");

                // Load planet information from database
                var planetInfoFromDb = await databaseMarketService.GetPlanetInfoAsync();
                var marketLocationFromDb = await databaseMarketService.GetMarketLocationInfoAsync();
                var marketsByPlanetFromDb = await databaseMarketService.GetMarketsByPlanetAsync();

                // Convert database planet info to PlanetData for compatibility
                var planetDataList = new List<PlanetData>();
                
                foreach (var (planetId, planetInfo) in planetInfoFromDb)
                {
                    var planetData = new PlanetData
                    {
                        PlanetId = planetInfo.PlanetId,
                        Name = planetInfo.Name,
                        Position = planetInfo.Position,
                        DistanceFromOrigin = planetInfo.DistanceFromOrigin
                    };

                    planetDataList.Add(planetData);
                    logger.LogDebug($"Loaded planet {planetInfo.Name} (ID: {planetId}) from database: distance={planetInfo.DistanceFromOrigin:F0}");
                }

                // Fallback to Orleans data for planets not found in database
                var knownPlanets = new Dictionary<ulong, string>
                {
                    { 2, "Alioth" },
                    { 26, "Sanctuary" },
                    { 27, "Madis" },
                    { 30, "Thades" },
                    { 31, "Talemai" }
                };

                foreach (var (planetId, name) in knownPlanets)
                {
                    if (!planetInfoFromDb.ContainsKey(planetId))
                    {
                        logger.LogInformation($"Planet {name} (ID: {planetId}) not found in database, using Orleans fallback");
                        
                        var planetData = new PlanetData
                        {
                            PlanetId = planetId,
                            Name = name,
                            Position = new Vec3 { x = 0, y = 0, z = 0 }, // Default position
                            DistanceFromOrigin = 0
                        };

                        // Try to load from Orleans as fallback
                        try
                        {
                            var constructId = new ConstructId { constructId = planetId };
                            var constructInfo = await orleans.GetConstructInfoGrain(constructId).Get();
                            
                            if (constructInfo?.rData != null)
                            {
                                planetData.Position = constructInfo.rData.position;
                                planetData.DistanceFromOrigin = CalculateDistanceFromOrigin(constructInfo.rData.position);
                                
                                logger.LogInformation($"Loaded planet {name} from Orleans: ({constructInfo.rData.position.x}, {constructInfo.rData.position.y}, {constructInfo.rData.position.z}), distance: {planetData.DistanceFromOrigin:F0}");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, $"Failed to load Orleans data for planet {name} (ID: {planetId})");
                        }

                        planetDataList.Add(planetData);
                    }
                }

                // Update all caches atomically
                cacheLock.EnterWriteLock();
                try
                {
                    // Update legacy planet cache
                    planetCache.Clear();
                    foreach (var planetData in planetDataList)
                    {
                        planetCache[planetData.PlanetId] = planetData;
                    }

                    // Update database-driven caches
                    marketLocationCache.Clear();
                    foreach (var (marketId, locationInfo) in marketLocationFromDb)
                    {
                        marketLocationCache[marketId] = locationInfo;
                    }

                    planetInfoCache.Clear();
                    foreach (var (planetId, planetInfo) in planetInfoFromDb)
                    {
                        planetInfoCache[planetId] = planetInfo;
                    }

                    marketsByPlanetCache.Clear();
                    foreach (var (planetId, markets) in marketsByPlanetFromDb)
                    {
                        marketsByPlanetCache[planetId] = markets;
                    }
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }

                logger.LogInformation($"Loaded {planetCache.Count} planets total ({planetInfoFromDb.Count} from database, {planetCache.Count - planetInfoFromDb.Count} from Orleans fallback)");
                logger.LogInformation($"Loaded location information for {marketLocationCache.Count} markets from database");
                logger.LogInformation($"Grouped markets by planet: {marketsByPlanetCache.Count} planets with markets");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load planet data");
            }
        }

        /// <summary>
        /// Refresh market data from all planets using Orleans grains with comprehensive error handling and resilience
        /// Requirements 1.3, 1.4, 5.2, 5.3, 5.5, 8.2, 8.3: Market discovery, order collection, cache refresh, error handling
        /// </summary>
        public virtual async Task RefreshMarketData()
        {
            // Check if refresh is already in progress (Requirement 5.2)
            lock (refreshLock)
            {
                if (isRefreshing)
                {
                    logger.LogDebug("Market data refresh already in progress, skipping");
                    return;
                }
                isRefreshing = true;
                lastRefreshAttempt = DateTime.UtcNow;
            }

            try
            {
                if (bot == null)
                {
                    logger.LogWarning("Bot not initialized, skipping market data refresh");
                    return;
                }

                // Check Orleans availability with circuit breaker pattern (Requirement 5.5)
                if (!orleansAvailable && DateTime.UtcNow - lastOrleansFailure < TimeSpan.FromMinutes(5))
                {
                    logger.LogWarning("Orleans services unavailable (circuit breaker open), using cached data");
                    return;
                }

                logger.LogInformation("Starting comprehensive market data refresh for all planets...");
                
                // Use timeout wrapper for Orleans calls to prevent hanging
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                
                var marketGrain = orleans.GetMarketGrain();
                var newMarkets = new Dictionary<ulong, MarketData>();
                var newOrders = new List<OrderData>();
                var totalMarketsProcessed = 0;
                var totalOrdersProcessed = 0;
                var partialFailures = 0;

                // Use database-driven market discovery instead of Orleans planet-based discovery
                Dictionary<ulong, List<ulong>> marketsByPlanetFromDb;
                Dictionary<ulong, MarketLocationInfo> marketLocationFromDb;
                Dictionary<ulong, PlanetInfo> planetInfoFromDb;
                
                cacheLock.EnterReadLock();
                try
                {
                    marketsByPlanetFromDb = new Dictionary<ulong, List<ulong>>(marketsByPlanetCache);
                    marketLocationFromDb = new Dictionary<ulong, MarketLocationInfo>(marketLocationCache);
                    planetInfoFromDb = new Dictionary<ulong, PlanetInfo>(planetInfoCache);
                }
                finally
                {
                    cacheLock.ExitReadLock();
                }

                logger.LogInformation($"Using database-driven market discovery: {marketLocationFromDb.Count} markets across {marketsByPlanetFromDb.Count} planets");

                // Process markets using database information for accurate planet assignment
                foreach (var (marketId, locationInfo) in marketLocationFromDb)
                {
                    try
                    {
                        // Use planet name from database location info (already resolved in the query)
                        var planetName = locationInfo.PlanetName;

                        logger.LogDebug($"Processing market {marketId} on planet {planetName} (ID: {locationInfo.PlanetId}) using database info...");
                        
                        // Create a minimal MarketInfo structure for Orleans compatibility
                        var marketInfo = new MarketInfo
                        {
                            marketId = marketId,
                            name = $"Market {marketId}", // We'll get the real name from Orleans if available
                            relativeLocation = new RelativeLocation
                            {
                                constructId = locationInfo.ConstructId,
                                position = locationInfo.Position
                            }
                        };

                        var marketData = await ProcessMarketWithDatabaseInfo(marketInfo, locationInfo, planetName, marketGrain, timeoutCts.Token);
                        if (marketData != null)
                        {
                            newMarkets[marketId] = marketData;
                            newOrders.AddRange(marketData.Orders);
                            totalMarketsProcessed++;
                            totalOrdersProcessed += marketData.Orders.Count;
                        }
                        else
                        {
                            partialFailures++;
                        }
                    }
                    catch (NQutils.Exceptions.BusinessException be) when (be.error.code == NQ.ErrorCode.InvalidSession)
                    {
                        logger.LogWarning($"Invalid session detected during market {marketId} processing - session reconnection needed");
                        throw; // Propagate session errors for reconnection handling
                    }
                    catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                    {
                        logger.LogError($"Timeout occurred while processing market {marketId}");
                        partialFailures++;
                        // Continue with other markets
                    }
                    catch (Exception ex)
                    {
                        partialFailures++;
                        logger.LogError(ex, $"Failed to process market {marketId}");
                        // Continue processing other markets - partial failure handling
                    }
                }

                // Update cache atomically with partial success handling (Requirement 5.1, 5.5)
                cacheLock.EnterWriteLock();
                try
                {
                    // Only clear cache if we have new data or this is a complete refresh
                    if (newMarkets.Count > 0 || (totalMarketsProcessed == 0 && partialFailures == 0))
                    {
                        marketCache.Clear();
                        allOrders.Clear();
                    }
                    
                    foreach (var (marketId, marketData) in newMarkets)
                    {
                        marketCache[marketId] = marketData;
                    }
                    
                    allOrders.AddRange(newOrders);
                    
                    // Update cache metadata (Requirement 5.3)
                    if (newMarkets.Count > 0 || partialFailures == 0)
                    {
                        lastSuccessfulRefresh = DateTime.UtcNow;
                        consecutiveFailures = 0;
                        orleansAvailable = true;
                    }
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }

                logger.LogInformation($"Market data refresh completed:");
                logger.LogInformation($"  - Processed {totalMarketsProcessed} markets across {marketsByPlanetFromDb.Count} planets");
                logger.LogInformation($"  - Collected {totalOrdersProcessed} total orders");
                logger.LogInformation($"  - Partial failures: {partialFailures}");
                logger.LogInformation($"  - Cache age: {GetCacheAge().TotalMinutes:F1} minutes");
                
                cacheLock.EnterReadLock();
                try
                {
                    logger.LogInformation($"  - Cached {playerNameCache.Count} player names");
                    logger.LogInformation($"  - Cached {itemNameCache.Count} item names");
                }
                finally
                {
                    cacheLock.ExitReadLock();
                }

                // Log warnings for partial failures but don't fail the entire refresh
                if (partialFailures > 0)
                {
                    logger.LogWarning($"Refresh completed with {partialFailures} partial failures - some data may be incomplete");
                }
            }
            catch (NQutils.Exceptions.BusinessException be) when (be.error.code == NQ.ErrorCode.InvalidSession)
            {
                logger.LogError("Invalid session error during market data refresh - session reconnection required");
                
                // Mark Orleans as temporarily unavailable to trigger reconnection
                lock (refreshLock)
                {
                    consecutiveFailures++;
                    orleansAvailable = false;
                    lastOrleansFailure = DateTime.UtcNow;
                }
                
                throw; // Propagate session errors for higher-level reconnection handling
            }
            catch (Exception ex)
            {
                // Handle Orleans service failures with circuit breaker pattern (Requirement 5.5)
                lock (refreshLock)
                {
                    consecutiveFailures++;
                    if (consecutiveFailures >= maxRetryAttempts)
                    {
                        orleansAvailable = false;
                        lastOrleansFailure = DateTime.UtcNow;
                        logger.LogError($"Orleans services marked as unavailable after {consecutiveFailures} consecutive failures (circuit breaker opened)");
                    }
                }
                
                logger.LogError(ex, $"Failed to refresh market data (attempt {consecutiveFailures}/{maxRetryAttempts})");
                
                // Graceful degradation: Don't rethrow if we have cached data to fall back on (Requirement 5.5)
                if (HasValidCachedData())
                {
                    logger.LogWarning("Using cached data due to refresh failure - system operating in degraded mode");
                }
                else
                {
                    logger.LogCritical("No cached data available and refresh failed - system may be unavailable");
                    throw;
                }
            }
            finally
            {
                lock (refreshLock)
                {
                    isRefreshing = false;
                }
            }
        }

        /// <summary>
        /// Execute Orleans operation with timeout and retry logic for resilience
        /// Requirements 5.5, 8.2, 8.3: Request timeout handling and circuit breaker patterns
        /// </summary>
        private async Task<T> ExecuteWithTimeoutAndRetry<T>(Func<Task<T>> operation, string operationName, CancellationToken cancellationToken, int maxRetries = 2)
        {
            var retryCount = 0;
            Exception? lastException = null;

            while (retryCount <= maxRetries)
            {
                try
                {
                    // Create timeout for individual operation
                    using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    operationCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout per operation

                    var task = operation();
                    
                    // Wait for either completion or timeout
                    if (await Task.WhenAny(task, Task.Delay(Timeout.Infinite, operationCts.Token)) == task)
                    {
                        return await task;
                    }
                    else
                    {
                        throw new TimeoutException($"Operation {operationName} timed out after 30 seconds");
                    }
                }
                catch (NQutils.Exceptions.BusinessException be) when (be.error.code == NQ.ErrorCode.InvalidSession)
                {
                    logger.LogWarning($"Invalid session during {operationName} - propagating for reconnection");
                    throw; // Don't retry session errors, let higher level handle reconnection
                }
                catch (Exception ex) when (retryCount < maxRetries)
                {
                    retryCount++;
                    lastException = ex;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                    
                    logger.LogWarning(ex, $"Operation {operationName} failed (attempt {retryCount}/{maxRetries + 1}), retrying in {delay.TotalSeconds} seconds");
                    
                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // Respect cancellation
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break; // Final attempt failed
                }
            }

            logger.LogError(lastException, $"Operation {operationName} failed after {retryCount} retries");
            throw lastException ?? new InvalidOperationException($"Operation {operationName} failed");
        }

        /// <summary>
        /// Process a single market using database information for accurate planet identification
        /// Requirement 1.4: Implement order collection using IMarketGrain.MarketSelectItem() for each market
        /// Requirements 4.1, 4.2: Use database-driven planet identification
        /// Requirements 5.5, 8.2, 8.3: Graceful degradation and error handling
        /// </summary>
        private async Task<MarketData?> ProcessMarketWithDatabaseInfo(MarketInfo market, MarketLocationInfo locationInfo, string planetName, IMarketGrain marketGrain, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogDebug($"Processing market {market.marketId} using database info: planet {planetName} (ID: {locationInfo.PlanetId}), construct {locationInfo.ConstructId}");

                var marketData = new MarketData
                {
                    MarketId = market.marketId,
                    Name = !string.IsNullOrEmpty(locationInfo.MarketName) ? locationInfo.MarketName : (market.name ?? $"Market {market.marketId}"),
                    ConstructId = locationInfo.ConstructId,
                    PlanetId = locationInfo.PlanetId,
                    PlanetName = locationInfo.PlanetName,
                    Position = locationInfo.Position,
                    DistanceFromOrigin = locationInfo.DistanceFromOrigin,
                    LastUpdated = DateTime.UtcNow
                };

                logger.LogInformation($"Market {marketData.Name} (ID: {marketData.MarketId}) using database location: planet {marketData.PlanetName} (ID: {marketData.PlanetId}), construct {marketData.ConstructId}, position=({marketData.Position?.x}, {marketData.Position?.y}, {marketData.Position?.z})");

                // Get the actual market name from Orleans if possible
                try
                {
                    // Try to get market details from Orleans to get the real name
                    var marketSelectRequest = new MarketSelectRequest();
                    marketSelectRequest.marketIds.Add(market.marketId);
                    
                    var ordersResponse = await ExecuteWithTimeoutAndRetry(
                        async () => await marketGrain.MarketSelectItem(marketSelectRequest, bot.PlayerId),
                        $"MarketSelectItem for market {market.marketId}",
                        cancellationToken);

                    if (ordersResponse?.orders != null)
                    {
                        logger.LogDebug($"Processing {ordersResponse.orders.Count} orders for market {marketData.Name}");

                        // Process each order with individual error handling
                        var processedOrders = 0;
                        var failedOrders = 0;

                        foreach (var order in ordersResponse.orders)
                        {
                            try
                            {
                                var orderData = await ProcessOrderWithResilience(order, marketData, cancellationToken);
                                if (orderData != null)
                                {
                                    marketData.Orders.Add(orderData);
                                    processedOrders++;
                                }
                                else
                                {
                                    failedOrders++;
                                }
                            }
                            catch (Exception ex)
                            {
                                failedOrders++;
                                logger.LogDebug(ex, $"Failed to process order {order.orderId} in market {market.marketId} - skipping");
                                // Continue processing other orders - partial failure handling
                            }
                        }

                        if (failedOrders > 0)
                        {
                            logger.LogWarning($"Market {marketData.Name}: processed {processedOrders} orders, failed {failedOrders} orders");
                        }
                    }
                    else
                    {
                        logger.LogDebug($"No orders returned for market {market.marketId}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to retrieve orders for market {market.marketId} - market will have no orders");
                    // Continue with market data but no orders - partial success
                }

                logger.LogDebug($"Successfully processed market {marketData.Name} with {marketData.Orders.Count} orders using database info");
                return marketData;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to process market {market.marketId} completely");
                return null; // Return null to indicate complete failure for this market
            }
        }

        /// <summary>
        /// Process a single market with comprehensive error handling and resilience (legacy Orleans-based method)
        /// Requirement 1.4: Implement order collection using IMarketGrain.MarketSelectItem() for each market
        /// Requirements 4.1, 4.2: Implement construct info retrieval and planet identification
        /// Requirements 5.5, 8.2, 8.3: Graceful degradation and error handling
        /// </summary>
        private async Task<MarketData?> ProcessMarketWithResilience(MarketInfo market, ulong planetId, string planetName, IMarketGrain marketGrain, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogDebug($"Processing market {market.name} (ID: {market.marketId}) on {planetName}");

                var marketData = new MarketData
                {
                    MarketId = market.marketId,
                    Name = market.name ?? $"Market {market.marketId}",
                    ConstructId = market.relativeLocation?.constructId ?? 0,
                    PlanetId = planetId,
                    PlanetName = planetName,
                    LastUpdated = DateTime.UtcNow
                };

                // Use database-driven planet identification if available (most accurate)
                if (marketLocationCache.TryGetValue(market.marketId, out var dbLocationInfo))
                {
                    marketData.PlanetId = dbLocationInfo.PlanetId;
                    marketData.ConstructId = dbLocationInfo.ConstructId;
                    marketData.Position = dbLocationInfo.Position;
                    marketData.DistanceFromOrigin = dbLocationInfo.DistanceFromOrigin;
                    
                    // Get planet name from database info
                    if (planetInfoCache.TryGetValue(dbLocationInfo.PlanetId, out var dbPlanetInfo))
                    {
                        marketData.PlanetName = dbPlanetInfo.Name;
                    }
                    
                    logger.LogDebug($"Market {marketData.Name} (ID: {marketData.MarketId}) using database location: planet {marketData.PlanetName} (ID: {marketData.PlanetId}), construct {marketData.ConstructId}");
                }
                else
                {
                    logger.LogInformation($"Market {marketData.Name} (ID: {marketData.MarketId}) not found in database, using Orleans data: planet {planetName} (ID: {planetId}), construct ID: {market.relativeLocation?.constructId ?? 0}");
                }
                
                // Debug the RelativeLocation structure and compare with expected values
                if (market.relativeLocation != null)
                {
                    var pos = market.relativeLocation.position;
                    var posStr = $"({pos.x}, {pos.y}, {pos.z})";
                    logger.LogInformation($"Market {marketData.Name} RelativeLocation details: constructId={market.relativeLocation.constructId}, position={posStr}");
                    
                    // Log expected vs actual construct ID based on backend data
                    var expectedConstructId = GetExpectedConstructId(marketData.MarketId);
                    if (expectedConstructId.HasValue && expectedConstructId.Value != market.relativeLocation.constructId)
                    {
                        logger.LogWarning($"Market {marketData.Name} (ID: {marketData.MarketId}) construct ID mismatch! Expected: {expectedConstructId.Value}, Got: {market.relativeLocation.constructId}");
                    }
                }
                else
                {
                    logger.LogWarning($"Market {marketData.Name} has null RelativeLocation");
                }

                // Requirement 4.1, 4.2: Use Orleans RelativeLocation data directly due to GetConstructInfo serialization issues
                // The Orleans market data contains valid position information in RelativeLocation
                try
                {
                    var correctConstructId = GetExpectedConstructId(marketData.MarketId);
                    if (correctConstructId.HasValue)
                    {
                        logger.LogInformation($"Market {marketData.Name} (ID: {marketData.MarketId}) construct ID mismatch detected. Expected: {correctConstructId.Value}, Got: {market.relativeLocation?.constructId ?? 0}");
                        logger.LogInformation($"Using Orleans RelativeLocation position data directly due to GetConstructInfo serialization issues");
                    }
                    
                    // Use Orleans RelativeLocation data directly - it contains valid position information
                    await RetrieveMarketLocationInfoWithResilience(marketData, market, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, $"Failed to retrieve location info for market {marketData.Name} - continuing with default location");
                    // Continue processing without location data - graceful degradation
                }

                // Requirement 4.3: Calculate distance from origin for this market
                if (marketData.Position != null)
                {
                    marketData.DistanceFromOrigin = CalculateDistanceFromOrigin(marketData.Position);
                }

                // Requirement 1.4: Get all orders for this market using MarketSelectItem with resilience
                try
                {
                    var marketSelectRequest = new MarketSelectRequest();
                    marketSelectRequest.marketIds.Add(market.marketId);
                    // Empty itemTypes list means all items

                    var ordersResponse = await ExecuteWithTimeoutAndRetry(
                        async () => await marketGrain.MarketSelectItem(marketSelectRequest, bot.PlayerId),
                        $"MarketSelectItem for market {market.marketId}",
                        cancellationToken);

                    if (ordersResponse?.orders != null)
                    {
                        logger.LogDebug($"Processing {ordersResponse.orders.Count} orders for market {market.name}");

                        // Process each order with individual error handling
                        var processedOrders = 0;
                        var failedOrders = 0;

                        foreach (var order in ordersResponse.orders)
                        {
                            try
                            {
                                var orderData = await ProcessOrderWithResilience(order, marketData, cancellationToken);
                                if (orderData != null)
                                {
                                    marketData.Orders.Add(orderData);
                                    processedOrders++;
                                }
                                else
                                {
                                    failedOrders++;
                                }
                            }
                            catch (Exception ex)
                            {
                                failedOrders++;
                                logger.LogDebug(ex, $"Failed to process order {order.orderId} in market {market.marketId} - skipping");
                                // Continue processing other orders - partial failure handling
                            }
                        }

                        if (failedOrders > 0)
                        {
                            logger.LogWarning($"Market {market.name}: processed {processedOrders} orders, failed {failedOrders} orders");
                        }
                    }
                    else
                    {
                        logger.LogDebug($"No orders returned for market {market.marketId}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to retrieve orders for market {market.marketId} - market will have no orders");
                    // Continue with market data but no orders - partial success
                }

                logger.LogDebug($"Successfully processed market {market.name} with {marketData.Orders.Count} orders");
                return marketData;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to process market {market.marketId} completely");
                return null; // Return null to indicate complete failure for this market
            }
        }

        /// <summary>
        /// Process a single market and collect all its orders (legacy method for compatibility)
        /// Requirement 1.4: Implement order collection using IMarketGrain.MarketSelectItem() for each market
        /// Requirements 4.1, 4.2: Implement construct info retrieval and planet identification
        /// </summary>
        private async Task<MarketData?> ProcessMarket(MarketInfo market, ulong planetId, string planetName, IMarketGrain marketGrain)
        {
            try
            {
                logger.LogDebug($"Processing market {market.name} (ID: {market.marketId}) on {planetName}");

                var marketData = new MarketData
                {
                    MarketId = market.marketId,
                    Name = market.name ?? $"Market {market.marketId}",
                    ConstructId = market.relativeLocation?.constructId ?? 0,
                    PlanetId = planetId,
                    PlanetName = planetName,
                    LastUpdated = DateTime.UtcNow
                };

                // Requirement 4.1, 4.2: Implement construct info retrieval using IConstructInfoGrain for market locations
                await RetrieveMarketLocationInfo(marketData, market);

                // Requirement 4.3: Calculate distance from origin for this market
                if (marketData.Position != null)
                {
                    marketData.DistanceFromOrigin = CalculateDistanceFromOrigin(marketData.Position);
                }

                // Requirement 1.4: Get all orders for this market using MarketSelectItem
                var marketSelectRequest = new MarketSelectRequest();
                marketSelectRequest.marketIds.Add(market.marketId);
                // Empty itemTypes list means all items

                var ordersResponse = await marketGrain.MarketSelectItem(marketSelectRequest, bot.PlayerId);

                if (ordersResponse.orders == null)
                {
                    logger.LogDebug($"No orders returned for market {market.marketId}");
                    return marketData;
                }

                logger.LogDebug($"Processing {ordersResponse.orders.Count} orders for market {market.name}");

                // Process each order
                foreach (var order in ordersResponse.orders)
                {
                    try
                    {
                        var orderData = await ProcessOrder(order, marketData);
                        if (orderData != null)
                        {
                            marketData.Orders.Add(orderData);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"Failed to process order {order.orderId} in market {market.marketId}");
                    }
                }

                logger.LogDebug($"Successfully processed market {market.name} with {marketData.Orders.Count} orders");
                return marketData;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to process market {market.marketId}");
                return null;
            }
        }

        /// <summary>
        /// Process a single market order with comprehensive error handling and resilience
        /// Requirements 8.1, 8.2, 8.3: Player name resolution with graceful degradation
        /// </summary>
        private async Task<OrderData?> ProcessOrderWithResilience(MarketOrder order, MarketData marketData, CancellationToken cancellationToken)
        {
            try
            {
                var orderData = new OrderData
                {
                    OrderId = order.orderId,
                    MarketId = order.marketId,
                    MarketName = marketData.Name,
                    ItemType = order.itemType,
                    UnitPrice = order.unitPrice ?? new Currency { amount = 0 },
                    PlayerId = order.ownerId?.playerId ?? 0,
                    ExpirationDate = order.expirationDate.ToDateTime().DateTime,
                    LastUpdated = DateTime.UtcNow
                };

                // Based on Task 3 discoveries: Handle buy/sell order detection properly with validation
                if (order.buyQuantity > 0)
                {
                    orderData.BuyQuantity = order.buyQuantity;
                    orderData.SellQuantity = 0;
                }
                else if (order.buyQuantity < 0)
                {
                    // Sell order - buyQuantity is negative, convert to positive sell quantity
                    orderData.BuyQuantity = 0;
                    orderData.SellQuantity = Math.Abs(order.buyQuantity);
                }
                else
                {
                    // buyQuantity == 0, this shouldn't happen based on discoveries, but handle it gracefully
                    logger.LogDebug($"Order {order.orderId} has buyQuantity = 0, using fallback detection");
                    
                    // Fallback: assume it's a sell order with quantity 1 if we can't determine
                    orderData.BuyQuantity = 0;
                    orderData.SellQuantity = 1;
                }

                // Requirement 8.1, 8.2, 8.3: Resolve player name with comprehensive error handling
                try
                {
                    orderData.PlayerName = await ResolvePlayerNameWithResilience(order.ownerId?.playerId ?? 0, order.ownerName, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, $"Failed to resolve player name for order {order.orderId}");
                    // Requirement 8.2: Graceful handling with placeholder
                    orderData.PlayerName = order.ownerName ?? $"Player {order.ownerId?.playerId ?? 0}";
                }

                // Resolve item name with graceful degradation
                try
                {
                    orderData.ItemName = ResolveItemNameWithResilience(order.itemType);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, $"Failed to resolve item name for order {order.orderId}");
                    // Requirement 8.2: Graceful handling with placeholder
                    orderData.ItemName = $"Item {order.itemType}";
                }

                return orderData;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to process order {order.orderId} completely");
                return null; // Return null to indicate complete failure for this order
            }
        }

        /// <summary>
        /// Process a single market order and resolve player names and item names (legacy method for compatibility)
        /// Requirements 8.1, 8.2: Player name resolution using IPlayerGrain.GetPlayerInfo()
        /// </summary>
        private async Task<OrderData?> ProcessOrder(MarketOrder order, MarketData marketData)
        {
            try
            {
                var orderData = new OrderData
                {
                    OrderId = order.orderId,
                    MarketId = order.marketId,
                    MarketName = marketData.Name,
                    ItemType = order.itemType,
                    UnitPrice = order.unitPrice ?? new Currency { amount = 0 },
                    PlayerId = order.ownerId?.playerId ?? 0,
                    ExpirationDate = order.expirationDate.ToDateTime().DateTime,
                    LastUpdated = DateTime.UtcNow
                };

                // Based on Task 3 discoveries: Handle buy/sell order detection properly
                // buyQuantity > 0 = buy order, buyQuantity < 0 = sell order (negative quantity)
                if (order.buyQuantity > 0)
                {
                    orderData.BuyQuantity = order.buyQuantity;
                    orderData.SellQuantity = 0;
                }
                else if (order.buyQuantity < 0)
                {
                    // Sell order - buyQuantity is negative, convert to positive sell quantity
                    orderData.BuyQuantity = 0;
                    orderData.SellQuantity = Math.Abs(order.buyQuantity);
                }
                else
                {
                    // buyQuantity == 0, this shouldn't happen based on discoveries, but handle it
                    logger.LogWarning($"Order {order.orderId} has buyQuantity = 0, skipping");
                    return null;
                }

                // Requirement 8.1, 8.2: Resolve player name using IPlayerGrain.GetPlayerInfo()
                orderData.PlayerName = await ResolvePlayerName(order.ownerId?.playerId ?? 0, order.ownerName);

                // Resolve item name using GameplayBank
                orderData.ItemName = ResolveItemName(order.itemType);

                return orderData;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to process order {order.orderId}");
                return null;
            }
        }

        /// <summary>
        /// Resolve player name with comprehensive error handling and resilience
        /// Requirements 8.1, 8.2, 8.3: Player name resolution with graceful degradation and timeout handling
        /// </summary>
        private async Task<string> ResolvePlayerNameWithResilience(ulong playerId, string? fallbackName, CancellationToken cancellationToken)
        {
            if (playerId == 0)
            {
                return fallbackName ?? "Unknown Player";
            }

            // Check cache first (thread-safe read)
            cacheLock.EnterReadLock();
            try
            {
                if (playerNameCache.TryGetValue(playerId, out var cachedName))
                {
                    return cachedName;
                }
            }
            finally
            {
                cacheLock.ExitReadLock();
            }

            try
            {
                // Requirement 8.1: Use IPlayerGrain.GetPlayerInfo() with timeout and retry
                var resolvedName = await ExecuteWithTimeoutAndRetry(
                    async () =>
                    {
                        var playerGrain = orleans.GetPlayerGrain(playerId);
                        var playerInfo = await playerGrain.GetPlayerInfo();
                        return playerInfo?.name ?? fallbackName ?? $"Player {playerId}";
                    },
                    $"GetPlayerInfo for player {playerId}",
                    cancellationToken,
                    maxRetries: 1); // Limit retries for player info to avoid delays

                // Cache the result (thread-safe write)
                cacheLock.EnterWriteLock();
                try
                {
                    playerNameCache[playerId] = resolvedName;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }

                return resolvedName;
            }
            catch (NQutils.Exceptions.BusinessException be) when (be.error.code == NQ.ErrorCode.InvalidSession)
            {
                logger.LogDebug($"Invalid session while resolving player name for ID {playerId} - using fallback");
                throw; // Propagate session errors
            }
            catch (Exception ex)
            {
                // Requirement 8.2, 8.3: Gracefully handle exceptions and use placeholder text
                logger.LogDebug(ex, $"Failed to resolve player name for ID {playerId}: {ex.Message}");
                var placeholderName = fallbackName ?? $"Player {playerId}";
                
                // Cache the placeholder to avoid repeated failed lookups (thread-safe write)
                cacheLock.EnterWriteLock();
                try
                {
                    playerNameCache[playerId] = placeholderName;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }

                return placeholderName;
            }
        }

        /// <summary>
        /// Resolve player name using IPlayerGrain.GetPlayerInfo() with thread-safe caching (legacy method for compatibility)
        /// Requirements 8.1, 8.2, 8.4: Player name resolution with graceful degradation
        /// </summary>
        private async Task<string> ResolvePlayerName(ulong playerId, string? fallbackName)
        {
            if (playerId == 0)
            {
                return fallbackName ?? "Unknown Player";
            }

            // Check cache first (thread-safe read)
            cacheLock.EnterReadLock();
            try
            {
                if (playerNameCache.TryGetValue(playerId, out var cachedName))
                {
                    return cachedName;
                }
            }
            finally
            {
                cacheLock.ExitReadLock();
            }

            try
            {
                // Requirement 8.1: Use IPlayerGrain.GetPlayerInfo() to fetch player information
                var playerGrain = orleans.GetPlayerGrain(playerId);
                var playerInfo = await playerGrain.GetPlayerInfo();
                
                var resolvedName = playerInfo?.name ?? fallbackName ?? $"Player {playerId}";

                // Cache the result (thread-safe write)
                cacheLock.EnterWriteLock();
                try
                {
                    playerNameCache[playerId] = resolvedName;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }

                return resolvedName;
            }
            catch (Exception ex)
            {
                // Requirement 8.2, 8.4: Gracefully handle exceptions and use placeholder text
                logger.LogDebug($"Failed to resolve player name for ID {playerId}: {ex.Message}");
                var placeholderName = fallbackName ?? $"Player {playerId}";
                
                // Cache the placeholder to avoid repeated failed lookups (thread-safe write)
                cacheLock.EnterWriteLock();
                try
                {
                    playerNameCache[playerId] = placeholderName;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }

                return placeholderName;
            }
        }

        /// <summary>
        /// Resolve item name with comprehensive error handling and graceful degradation
        /// Uses ItemNameService for display names from items.yaml, falls back to GameplayBank
        /// Requirements 5.1, 5.5, 8.2: Thread-safe caching with graceful degradation and missing item handling
        /// </summary>
        private string ResolveItemNameWithResilience(ulong itemType)
        {
            // Check cache first (thread-safe read)
            cacheLock.EnterReadLock();
            try
            {
                if (itemNameCache.TryGetValue(itemType, out var cachedName))
                {
                    return cachedName;
                }
            }
            finally
            {
                cacheLock.ExitReadLock();
            }

            string resolvedName;
            string itemKey = null;
            
            try
            {
                // First, try to get the item key from GameplayBank
                if (gameplayBank == null)
                {
                    try
                    {
                        gameplayBank = Mod.serviceProvider?.GetRequiredService<IGameplayBank>();
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Failed to get GameplayBank from service provider");
                        gameplayBank = null;
                    }
                }

                if (gameplayBank != null)
                {
                    try
                    {
                        var itemDefinition = gameplayBank.GetDefinition(itemType);
                        itemKey = itemDefinition?.Name; // This is the internal key like "IronPure"
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, $"GameplayBank.GetDefinition failed for item type {itemType}");
                    }
                }

                // Try to get display name from ItemNameService using the item key
                if (!string.IsNullOrEmpty(itemKey) && itemNameService.IsLoaded)
                {
                    var displayName = itemNameService.GetDisplayName(itemKey);
                    if (displayName != itemKey) // ItemNameService returns the key if no display name found
                    {
                        resolvedName = displayName;
                        logger.LogDebug($"Resolved item {itemType} ({itemKey}) to display name: {displayName}");
                    }
                    else
                    {
                        // Use the internal key as fallback
                        resolvedName = itemKey;
                        logger.LogDebug($"No display name found for item {itemType} ({itemKey}), using internal name");
                    }
                }
                else if (!string.IsNullOrEmpty(itemKey))
                {
                    // ItemNameService not loaded, use internal key
                    resolvedName = itemKey;
                    logger.LogDebug($"ItemNameService not loaded (IsLoaded: {itemNameService.IsLoaded}), using internal name for item {itemType}: {itemKey}");
                }
                else
                {
                    // Complete fallback
                    resolvedName = $"Item {itemType}";
                    logger.LogDebug($"Could not resolve item name for type {itemType}, using fallback");
                }

                // Additional validation for item name
                if (string.IsNullOrWhiteSpace(resolvedName) || resolvedName == "null")
                {
                    resolvedName = $"Item {itemType}";
                }

                // Cache the result (thread-safe write)
                cacheLock.EnterWriteLock();
                try
                {
                    itemNameCache[itemType] = resolvedName;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }

                return resolvedName;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, $"Comprehensive failure resolving item name for type {itemType}");
                
                // Fallback to placeholder with enhanced information (thread-safe write)
                var placeholderName = $"Item {itemType}";
                cacheLock.EnterWriteLock();
                try
                {
                    itemNameCache[itemType] = placeholderName;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }

                return placeholderName;
            }
        }

        /// <summary>
        /// Resolve item name using ItemNameService and GameplayBank (legacy method for compatibility)
        /// Requirements 5.1, 5.5: Thread-safe caching with graceful degradation
        /// </summary>
        private string ResolveItemName(ulong itemType)
        {
            // Use the resilient method for consistency
            return ResolveItemNameWithResilience(itemType);
        }

        /// <summary>
        /// Get all cached markets with thread-safe access
        /// Requirement 5.1: Thread-safe cache access
        /// </summary>
        public List<MarketData> GetAllMarkets()
        {
            cacheLock.EnterReadLock();
            try
            {
                return marketCache.Values.ToList();
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get all cached orders with thread-safe access
        /// Requirement 5.1: Thread-safe cache access
        /// </summary>
        public List<OrderData> GetAllOrders()
        {
            cacheLock.EnterReadLock();
            try
            {
                return allOrders.ToList();
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Search orders with thread-safe cache access
        /// Requirement 5.1: Thread-safe cache access
        /// </summary>
        public List<OrderData> SearchOrders(string itemName = "", string marketName = "", bool buyOrdersOnly = false, bool sellOrdersOnly = false)
        {
            cacheLock.EnterReadLock();
            try
            {
                var query = allOrders.AsEnumerable();

                if (!string.IsNullOrEmpty(itemName))
                {
                    query = query.Where(o => o.ItemName.Contains(itemName, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(marketName))
                {
                    query = query.Where(o => o.MarketName.Contains(marketName, StringComparison.OrdinalIgnoreCase));
                }

                if (buyOrdersOnly)
                {
                    query = query.Where(o => o.BuyQuantity > 0);
                }

                if (sellOrdersOnly)
                {
                    query = query.Where(o => o.SellQuantity > 0);
                }

                return query.ToList();
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Find profit opportunities with comprehensive analysis and filtering
        /// Requirements 3.1, 3.2, 3.3, 3.5, 3.6: Profit calculation engine with filtering and sorting
        /// </summary>
        public List<ProfitOpportunity> FindProfitOpportunities(ProfitFilter? filter = null)
        {
            filter ??= new ProfitFilter();
            
            cacheLock.EnterReadLock();
            try
            {
                var opportunities = new List<ProfitOpportunity>();

                // Group orders by item type for efficient comparison
                var ordersByItem = allOrders.GroupBy(o => o.ItemType);

                foreach (var itemGroup in ordersByItem)
                {
                    // Apply item name filter early if specified
                    if (!string.IsNullOrEmpty(filter.ItemName))
                    {
                        var firstOrder = itemGroup.FirstOrDefault();
                        if (firstOrder == null || !firstOrder.ItemName.Contains(filter.ItemName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    // Requirement 3.1: Compare buy and sell orders for the same item across different markets
                    var buyOrders = itemGroup.Where(o => o.BuyQuantity > 0).OrderByDescending(o => o.UnitPrice.amount);
                    var sellOrders = itemGroup.Where(o => o.SellQuantity > 0).OrderBy(o => o.UnitPrice.amount);

                    foreach (var buyOrder in buyOrders)
                    {
                        foreach (var sellOrder in sellOrders)
                        {
                            // Skip same market trades
                            if (buyOrder.MarketId == sellOrder.MarketId) continue;
                            
                            // Skip if no profit potential
                            if (buyOrder.UnitPrice.amount <= sellOrder.UnitPrice.amount) continue;

                            var opportunity = CalculateProfitOpportunity(buyOrder, sellOrder);
                            
                            // Apply filters
                            if (ApplyProfitFilters(opportunity, filter))
                            {
                                opportunities.Add(opportunity);
                            }
                        }
                    }
                }

                // Apply sorting
                opportunities = ApplyProfitSorting(opportunities, filter);

                return opportunities;
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Calculate comprehensive profit metrics for a buy/sell order pair
        /// Requirements 3.2, 3.3: Profit margin, total profit, and volume calculations
        /// </summary>
        private ProfitOpportunity CalculateProfitOpportunity(OrderData buyOrder, OrderData sellOrder)
        {
            // Requirement 3.2: Compute profit per unit, total profit potential, and profit margin percentage
            var profitPerUnit = buyOrder.UnitPrice.amount - sellOrder.UnitPrice.amount;
            var profitMargin = sellOrder.UnitPrice.amount > 0 ? (double)profitPerUnit / sellOrder.UnitPrice.amount * 100 : 0;
            
            // Requirement 3.3: Use the minimum of available buy and sell quantities
            var maxQuantity = Math.Min(buyOrder.BuyQuantity, sellOrder.SellQuantity);
            var totalProfit = profitPerUnit * maxQuantity;

            // Requirement 4.3: Calculate distance between markets
            var distance = CalculateDistanceInternal(buyOrder.MarketId, sellOrder.MarketId);
            
            // Distance-based profit efficiency metrics (profit per kilometer)
            var profitPerKm = distance > 0 ? (double)totalProfit / distance : 0;

            var opportunity = new ProfitOpportunity
            {
                ItemName = buyOrder.ItemName,
                ItemType = buyOrder.ItemType,
                BuyOrder = buyOrder,
                SellOrder = sellOrder,
                ProfitPerUnit = profitPerUnit,
                ProfitMargin = profitMargin,
                MaxQuantity = maxQuantity,
                TotalProfit = totalProfit,
                Distance = distance,
                ProfitPerKm = profitPerKm
            };

            // Use the built-in calculation method to ensure consistency
            opportunity.CalculateProfitMetrics();
            
            return opportunity;
        }

        /// <summary>
        /// Apply filtering criteria to profit opportunities
        /// Requirements 3.5, 3.6: Filtering for profit opportunities by various criteria
        /// </summary>
        private bool ApplyProfitFilters(ProfitOpportunity opportunity, ProfitFilter filter)
        {
            // Filter by minimum profit margin
            if (filter.MinProfitMargin.HasValue && opportunity.ProfitMargin < filter.MinProfitMargin.Value)
            {
                return false;
            }

            // Filter by minimum total profit
            if (filter.MinTotalProfit.HasValue && opportunity.TotalProfit < filter.MinTotalProfit.Value)
            {
                return false;
            }

            // Filter by maximum distance
            if (filter.MaxDistance.HasValue && opportunity.Distance > filter.MaxDistance.Value)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Apply sorting to profit opportunities based on specified criteria
        /// Requirements 3.5, 3.6: Sorting for profit opportunities by various criteria
        /// </summary>
        private List<ProfitOpportunity> ApplyProfitSorting(List<ProfitOpportunity> opportunities, ProfitFilter filter)
        {
            var sortBy = filter.SortBy?.ToLower() ?? "totalprofit";
            var sortOrder = filter.SortOrder?.ToLower() ?? "desc";

            IOrderedEnumerable<ProfitOpportunity> sortedOpportunities = sortBy switch
            {
                "totalprofit" => sortOrder == "asc" 
                    ? opportunities.OrderBy(o => o.TotalProfit)
                    : opportunities.OrderByDescending(o => o.TotalProfit),
                "profitmargin" => sortOrder == "asc"
                    ? opportunities.OrderBy(o => o.ProfitMargin)
                    : opportunities.OrderByDescending(o => o.ProfitMargin),
                "profitperunit" => sortOrder == "asc"
                    ? opportunities.OrderBy(o => o.ProfitPerUnit)
                    : opportunities.OrderByDescending(o => o.ProfitPerUnit),
                "profitkm" or "profitkm" => sortOrder == "asc"
                    ? opportunities.OrderBy(o => o.ProfitPerKm)
                    : opportunities.OrderByDescending(o => o.ProfitPerKm),
                "distance" => sortOrder == "asc"
                    ? opportunities.OrderBy(o => o.Distance)
                    : opportunities.OrderByDescending(o => o.Distance),
                "maxquantity" => sortOrder == "asc"
                    ? opportunities.OrderBy(o => o.MaxQuantity)
                    : opportunities.OrderByDescending(o => o.MaxQuantity),
                "itemname" => sortOrder == "asc"
                    ? opportunities.OrderBy(o => o.ItemName)
                    : opportunities.OrderByDescending(o => o.ItemName),
                _ => opportunities.OrderByDescending(o => o.TotalProfit) // Default to total profit descending
            };

            return sortedOpportunities.ToList();
        }

        /// <summary>
        /// Get paginated profit opportunities with comprehensive filtering and sorting
        /// Requirements 3.5, 3.6: Advanced filtering and sorting for profit opportunities
        /// </summary>
        public PagedResponse<ProfitOpportunity> GetPaginatedProfitOpportunities(ProfitFilter filter)
        {
            var allOpportunities = FindProfitOpportunities(filter);
            
            var totalCount = allOpportunities.Count;
            var pagedOpportunities = allOpportunities
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            return new PagedResponse<ProfitOpportunity>
            {
                Data = pagedOpportunities,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize),
                HasNextPage = filter.Page * filter.PageSize < totalCount,
                HasPreviousPage = filter.Page > 1,
                LastUpdated = lastSuccessfulRefresh
            };
        }

        /// <summary>
        /// Get top profit opportunities by different metrics
        /// Requirements 3.5, 3.6: Ranking opportunities by total profit, profit margin, and profit per distance unit
        /// </summary>
        public Dictionary<string, List<ProfitOpportunity>> GetTopProfitOpportunities(int topCount = 10)
        {
            var allOpportunities = FindProfitOpportunities();
            
            return new Dictionary<string, List<ProfitOpportunity>>
            {
                ["ByTotalProfit"] = allOpportunities
                    .OrderByDescending(o => o.TotalProfit)
                    .Take(topCount)
                    .ToList(),
                ["ByProfitMargin"] = allOpportunities
                    .OrderByDescending(o => o.ProfitMargin)
                    .Take(topCount)
                    .ToList(),
                ["ByProfitPerKm"] = allOpportunities
                    .Where(o => o.Distance > 0)
                    .OrderByDescending(o => o.ProfitPerKm)
                    .Take(topCount)
                    .ToList(),
                ["ByEfficiency"] = allOpportunities
                    .Where(o => o.Distance > 0 && o.MaxQuantity > 0)
                    .OrderByDescending(o => (o.TotalProfit / o.Distance) * Math.Log(o.MaxQuantity + 1))
                    .Take(topCount)
                    .ToList()
            };
        }

        /// <summary>
        /// Get profit opportunities for a specific item with detailed analysis
        /// Requirements 3.1, 3.2, 3.3: Item-specific profit analysis
        /// </summary>
        public List<ProfitOpportunity> GetProfitOpportunitiesForItem(string itemName, double minProfitMargin = 0.0)
        {
            var filter = new ProfitFilter
            {
                ItemName = itemName,
                MinProfitMargin = minProfitMargin,
                SortBy = "TotalProfit",
                SortOrder = "desc"
            };
            
            return FindProfitOpportunities(filter);
        }

        /// <summary>
        /// Get profit opportunities within a distance range
        /// Requirements 3.5, 3.6: Distance-based filtering for route planning
        /// </summary>
        public List<ProfitOpportunity> GetProfitOpportunitiesWithinDistance(double maxDistance, double minProfitPerKm = 0.0)
        {
            var filter = new ProfitFilter
            {
                MaxDistance = maxDistance,
                SortBy = "ProfitPerKm",
                SortOrder = "desc"
            };
            
            var opportunities = FindProfitOpportunities(filter);
            
            if (minProfitPerKm > 0)
            {
                opportunities = opportunities.Where(o => o.ProfitPerKm >= minProfitPerKm).ToList();
            }
            
            return opportunities;
        }

        /// <summary>
        /// Retrieve market location info using the correct construct ID from backend data
        /// </summary>
        private async Task RetrieveMarketLocationInfoWithCorrectConstructId(MarketData marketData, ulong correctConstructId, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation($"Retrieving construct info for market {marketData.Name} (ID: {marketData.MarketId}) using correct construct ID: {correctConstructId}");
                
                // Requirement 4.1: Use IConstructInfoGrain with timeout and retry
                // Handle Orleans serialization issues gracefully
                ConstructInfo? constructInfo = null;
                try
                {
                    constructInfo = await ExecuteWithTimeoutAndRetry(
                        async () => await orleans.GetConstructInfoGrain(new ConstructId { constructId = correctConstructId }).Get(),
                        $"GetConstructInfo for market {marketData.Name}",
                        cancellationToken,
                        maxRetries: 1);
                }
                catch (NotSupportedException ex) when (ex.Message.Contains("ILSerializerTypeToken"))
                {
                    logger.LogWarning($"Orleans serialization issue for construct {correctConstructId} in market {marketData.Name} - using fallback approach");
                    // Fall back to using the original Orleans market data position
                    throw new InvalidOperationException("Serialization issue - fallback needed", ex);
                }
                
                if (constructInfo?.rData != null)
                {
                    // Requirement 4.2: Extract position data from construct information with validation
                    if (IsValidPosition(constructInfo.rData.position))
                    {
                        marketData.Position = constructInfo.rData.position;
                        logger.LogInformation($"Market {marketData.Name} (ID: {marketData.MarketId}) position: ({constructInfo.rData.position.x}, {constructInfo.rData.position.y}, {constructInfo.rData.position.z})");
                    }
                    else
                    {
                        logger.LogWarning($"Market {marketData.Name} (ID: {marketData.MarketId}) has invalid position data, using default");
                        marketData.Position = new Vec3 { x = 0, y = 0, z = 0 };
                    }

                    // Update the market's construct ID to the correct one
                    marketData.ConstructId = correctConstructId;
                    
                    // Requirement 4.2: Planet identification logic using constructId with error handling
                    if (constructInfo.rData.parentId > 0)
                    {
                        logger.LogInformation($"Market {marketData.Name} (ID: {marketData.MarketId}) has parent construct ID: {constructInfo.rData.parentId}");
                        
                        // Check if the parent construct ID matches a known planet
                        cacheLock.EnterReadLock();
                        try
                        {
                            if (planetCache.TryGetValue(constructInfo.rData.parentId, out var actualPlanet))
                            {
                                logger.LogInformation($"Reassigning market {marketData.Name} from {marketData.PlanetName} to {actualPlanet.Name} based on parent construct");
                                marketData.PlanetId = actualPlanet.PlanetId;
                                marketData.PlanetName = actualPlanet.Name;
                            }
                        }
                        finally
                        {
                            cacheLock.ExitReadLock();
                        }
                        
                        try
                        {
                            await UpdatePlanetInfoFromConstructWithResilience(constructInfo.rData.parentId, constructInfo.rData.parentId, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, $"Failed to update planet info from parent construct {constructInfo.rData.parentId}");
                        }
                    }
                    else
                    {
                        logger.LogDebug($"Market {marketData.Name} (ID: {marketData.MarketId}) construct {correctConstructId} has no parent construct (parentId: {constructInfo.rData.parentId}) - construct is directly in universe");
                        
                        // Check if this construct ID corresponds to a known planet
                        cacheLock.EnterReadLock();
                        try
                        {
                            if (planetCache.TryGetValue(correctConstructId, out var constructPlanet))
                            {
                                logger.LogInformation($"Market {marketData.Name} is on construct {correctConstructId} which is planet {constructPlanet.Name}");
                                marketData.PlanetId = constructPlanet.PlanetId;
                                marketData.PlanetName = constructPlanet.Name;
                            }
                        }
                        finally
                        {
                            cacheLock.ExitReadLock();
                        }
                    }
                }
                else
                {
                    logger.LogWarning($"Market {marketData.Name} (ID: {marketData.MarketId}) construct info returned null data for construct {correctConstructId}");
                    marketData.Position = new Vec3 { x = 0, y = 0, z = 0 };
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"Failed to retrieve construct info for market {marketData.Name} (ID: {marketData.MarketId}) using construct {correctConstructId}");
                marketData.Position = new Vec3 { x = 0, y = 0, z = 0 };
            }
        }

        /// <summary>
        /// Retrieve market location information using Orleans RelativeLocation data directly
        /// Requirements 4.1, 4.2, 5.5: Use available position data with graceful degradation
        /// </summary>
        private async Task RetrieveMarketLocationInfoWithResilience(MarketData marketData, MarketInfo market, CancellationToken cancellationToken)
        {
            try
            {
                if (market.relativeLocation?.constructId > 0)
                {
                    logger.LogInformation($"Using Orleans RelativeLocation data for market {marketData.Name} (ID: {marketData.MarketId}) on {marketData.PlanetName} (construct ID: {market.relativeLocation.constructId})");
                    
                    // Use Orleans RelativeLocation position data directly (avoiding GetConstructInfo serialization issues)
                    if (market.relativeLocation != null && IsValidPosition(market.relativeLocation.position))
                    {
                        marketData.Position = market.relativeLocation.position;
                        logger.LogInformation($"Market {marketData.Name} (ID: {marketData.MarketId}) position from RelativeLocation: ({market.relativeLocation.position.x}, {market.relativeLocation.position.y}, {market.relativeLocation.position.z})");
                        
                        // Try to determine the correct planet based on position if we have planet data
                        await AssignCorrectPlanetBasedOnPosition(marketData, cancellationToken);
                    }
                    else
                    {
                        logger.LogWarning($"Market {marketData.Name} (ID: {marketData.MarketId}) has invalid RelativeLocation position data, using default");
                        marketData.Position = new Vec3 { x = 0, y = 0, z = 0 };
                    }
                }
                else
                {
                    logger.LogWarning($"Market {marketData.Name} (ID: {marketData.MarketId}) on {marketData.PlanetName} has no construct ID for location retrieval");
                    marketData.Position = new Vec3 { x = 0, y = 0, z = 0 }; // Default position
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"Failed to retrieve construct info for market {marketData.Name} (ID: {marketData.MarketId})");
                // Continue processing without location data - graceful degradation
                marketData.Position = new Vec3 { x = 0, y = 0, z = 0 }; // Default position
            }
        }





        /// <summary>
        /// Assign correct planet based on market position using distance calculations
        /// </summary>
        private Task AssignCorrectPlanetBasedOnPosition(MarketData marketData, CancellationToken cancellationToken)
        {
            if (marketData.Position == null || !IsValidPosition(marketData.Position))
            {
                return Task.CompletedTask;
            }

            try
            {
                cacheLock.EnterReadLock();
                try
                {
                    var closestPlanet = planetCache.Values
                        .Where(p => p.Position != null && IsValidPosition(p.Position))
                        .OrderBy(p => CalculateDistance(marketData.Position, p.Position))
                        .FirstOrDefault();

                    if (closestPlanet != null)
                    {
                        var distance = CalculateDistance(marketData.Position, closestPlanet.Position);
                        
                        // Only reassign if the market is significantly closer to a different planet
                        if (closestPlanet.PlanetId != marketData.PlanetId && distance < 1000000) // Within 1M units
                        {
                            logger.LogInformation($"Reassigning market {marketData.Name} from {marketData.PlanetName} to {closestPlanet.Name} based on position (distance: {distance:F0})");
                            marketData.PlanetId = closestPlanet.PlanetId;
                            marketData.PlanetName = closestPlanet.Name;
                        }
                    }
                }
                finally
                {
                    cacheLock.ExitReadLock();
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, $"Failed to assign correct planet for market {marketData.Name} based on position");
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get expected construct ID based on backend database data for debugging
        /// </summary>
        private ulong? GetExpectedConstructId(ulong marketId)
        {
            // Based on backend data provided - mapping market ID to expected construct ID
            var expectedConstructIds = new Dictionary<ulong, ulong>
            {
                {1, 535}, {2, 652}, {3, 308}, {4, 332}, {5, 370}, {6, 378}, {7, 386}, {8, 394}, {9, 402}, {10, 410},
                {11, 418}, {12, 425}, {13, 433}, {14, 441}, {15, 449}, {16, 457}, {17, 465}, {18, 476}, {19, 491},
                {20, 576}, {21, 606}, {22, 629}, {23, 686}, {24, 763}, {25, 800}, {26, 861}, {27, 919}, {28, 976},
                {29, 1059}, {30, 1080}, {31, 1156}, {32, 1170}, {33, 1278}, {34, 1319}, {35, 1333}, {36, 1373},
                {37, 2114}, {38, 2122}, {39, 2130}, {40, 2138}, {41, 2146}, {42, 2154}, {43, 2162}, {44, 2170},
                {45, 2177}, {46, 2186}, {47, 2194}, {48, 2201}, {49, 2209}, {50, 2217}, {51, 2225}, {52, 2233},
                {53, 2241}, {54, 2249}, {55, 2256}, {56, 2265}, {57, 2273}, {58, 2280}, {59, 2288}, {60, 2296},
                {61, 2304}, {62, 2312}, {63, 2320}, {64, 2328}, {65, 2336}, {66, 2343}, {67, 2351}, {68, 2359},
                {69, 2367}, {70, 2375}, {71, 2383}, {72, 2391}, {73, 2399}, {74, 2416}, {75, 2410}, {76, 2411},
                {77, 2431}, {78, 2440}, {79, 2443}, {80, 2454}, {81, 2464}, {82, 2466}, {83, 2478}, {84, 2486},
                {85, 2494}, {86, 2501}, {87, 2509}, {88, 2517}, {89, 2525}, {90, 2533}, {91, 2541}, {92, 2549},
                {93, 2557}, {94, 2565}, {95, 2573}, {96, 2580}, {97, 2588}, {98, 2596}, {99, 2604}, {100, 2612},
                {101, 2620}, {102, 2628}, {103, 2636}, {104, 2644}, {105, 2652}, {106, 2660}, {107, 2667}, {108, 2675},
                {109, 2683}, {110, 2691}, {111, 2707}, {112, 2699}, {113, 2715}, {114, 2723}, {115, 2730}, {116, 2739},
                {117, 2747}, {118, 2780}, {119, 2800}, {120, 2870}, {121, 2919}, {122, 2996}, {123, 3045}, {124, 3091}, {125, 3126}
            };
            
            return expectedConstructIds.TryGetValue(marketId, out var constructId) ? constructId : null;
        }

        /// <summary>
        /// Validate if a Vec3 position contains reasonable values
        /// </summary>
        private bool IsValidPosition(Vec3? position)
        {
            if (position == null) return false;
            
            // Check for NaN, infinity, or extremely large values that might indicate invalid data
            var pos = position.Value;
            return !double.IsNaN(pos.x) && !double.IsNaN(pos.y) && !double.IsNaN(pos.z) &&
                   !double.IsInfinity(pos.x) && !double.IsInfinity(pos.y) && !double.IsInfinity(pos.z) &&
                   Math.Abs(pos.x) < 1e15 && Math.Abs(pos.y) < 1e15 && Math.Abs(pos.z) < 1e15;
        }

        /// <summary>
        /// Update planet information from construct data with resilience
        /// Requirement 4.2: Planet identification logic using constructId to determine market planets
        /// </summary>
        private async Task UpdatePlanetInfoFromConstructWithResilience(ulong parentConstructId, ulong expectedPlanetId, CancellationToken cancellationToken)
        {
            try
            {
                var planetConstructInfo = await ExecuteWithTimeoutAndRetry(
                    async () => await orleans.GetConstructInfoGrain(new ConstructId { constructId = parentConstructId }).Get(),
                    $"GetConstructInfo for planet construct {parentConstructId}",
                    cancellationToken,
                    maxRetries: 1);
                
                if (planetConstructInfo?.rData != null && IsValidPosition(planetConstructInfo.rData.position))
                {
                    cacheLock.EnterWriteLock();
                    try
                    {
                        if (planetCache.TryGetValue(expectedPlanetId, out var planetData))
                        {
                            // Update planet position if we have valid construct info
                            planetData.Position = planetConstructInfo.rData.position;
                            planetData.DistanceFromOrigin = CalculateDistanceFromOrigin(planetConstructInfo.rData.position);
                            
                            logger.LogDebug($"Updated planet {planetData.Name} position: ({planetConstructInfo.rData.position.x}, {planetConstructInfo.rData.position.y}, {planetConstructInfo.rData.position.z})");

                            // Update planet name if construct has a name and it's different
                            if (!string.IsNullOrEmpty(planetConstructInfo.rData.name) && 
                                planetConstructInfo.rData.name != planetData.Name)
                            {
                                logger.LogDebug($"Planet construct name '{planetConstructInfo.rData.name}' differs from expected '{planetData.Name}'");
                            }
                        }
                    }
                    finally
                    {
                        cacheLock.ExitWriteLock();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, $"Failed to update planet info from construct {parentConstructId}");
                // Continue without planet position update - not critical
            }
        }

        /// <summary>
        /// Retrieve market location information using IConstructInfoGrain (legacy method for compatibility)
        /// Requirements 4.1, 4.2: Construct info retrieval and planet identification logic
        /// </summary>
        private async Task RetrieveMarketLocationInfo(MarketData marketData, MarketInfo market)
        {
            try
            {
                if (market.relativeLocation?.constructId > 0)
                {
                    logger.LogDebug($"Retrieving construct info for market {marketData.Name} (construct ID: {market.relativeLocation.constructId})");
                    
                    // Requirement 4.1: Use IConstructInfoGrain to get construct details
                    var constructInfo = await orleans.GetConstructInfoGrain(new ConstructId { constructId = market.relativeLocation.constructId }).Get();
                    
                    if (constructInfo?.rData != null)
                    {
                        // Requirement 4.2: Extract position data from construct information
                        marketData.Position = constructInfo.rData.position;
                        logger.LogDebug($"Market {marketData.Name} position: ({constructInfo.rData.position.x}, {constructInfo.rData.position.y}, {constructInfo.rData.position.z})");

                        // Requirement 4.2: Planet identification logic using constructId
                        if (constructInfo.rData.parentId > 0)
                        {
                            // The parent construct is typically the planet
                            await UpdatePlanetInfoFromConstruct(constructInfo.rData.parentId, marketData.PlanetId);
                        }
                        
                        // Store construct name if available for debugging
                        if (!string.IsNullOrEmpty(constructInfo.rData.name))
                        {
                            logger.LogDebug($"Market {marketData.Name} construct name: {constructInfo.rData.name}");
                        }
                    }
                }
                else
                {
                    logger.LogDebug($"Market {marketData.Name} has no construct ID for location retrieval");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"Failed to retrieve construct info for market {marketData.Name} (ID: {marketData.MarketId})");
                // Continue processing without location data - graceful degradation
            }
        }

        /// <summary>
        /// Update planet information from construct data
        /// Requirement 4.2: Planet identification logic using constructId to determine market planets
        /// </summary>
        private async Task UpdatePlanetInfoFromConstruct(ulong parentConstructId, ulong expectedPlanetId)
        {
            try
            {
                var planetConstructInfo = await orleans.GetConstructInfoGrain(new ConstructId { constructId = parentConstructId }).Get();
                
                if (planetConstructInfo?.rData != null)
                {
                    cacheLock.EnterWriteLock();
                    try
                    {
                        if (planetCache.TryGetValue(expectedPlanetId, out var planetData))
                        {
                            // Update planet position if we have construct info
                            planetData.Position = planetConstructInfo.rData.position;
                            planetData.DistanceFromOrigin = CalculateDistanceFromOrigin(planetConstructInfo.rData.position);
                            
                            logger.LogDebug($"Updated planet {planetData.Name} position: ({planetConstructInfo.rData.position.x}, {planetConstructInfo.rData.position.y}, {planetConstructInfo.rData.position.z})");

                            // Update planet name if construct has a name
                            if (!string.IsNullOrEmpty(planetConstructInfo.rData.name) && 
                                planetConstructInfo.rData.name != planetData.Name)
                            {
                                logger.LogDebug($"Planet construct name '{planetConstructInfo.rData.name}' differs from expected '{planetData.Name}'");
                            }
                        }
                    }
                    finally
                    {
                        cacheLock.ExitWriteLock();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, $"Failed to update planet info from construct {parentConstructId}");
                // Continue without planet position update - not critical
            }
        }

        /// <summary>
        /// Calculate distance from origin (0,0,0) for a position
        /// Requirement 4.3: Implement Vec3 distance calculations
        /// </summary>
        private double CalculateDistanceFromOrigin(Vec3? position)
        {
            if (position == null) return 0;
            
            return Math.Sqrt(
                position.Value.x * position.Value.x +
                position.Value.y * position.Value.y +
                position.Value.z * position.Value.z
            );
        }

        /// <summary>
        /// Calculate distance between two Vec3 positions
        /// Requirement 4.3: Implement Vec3 distance calculations between market positions
        /// </summary>
        public double CalculateDistance(Vec3? pos1, Vec3? pos2)
        {
            if (pos1 == null || pos2 == null) return 0;
            
            var dx = pos1.Value.x - pos2.Value.x;
            var dy = pos1.Value.y - pos2.Value.y;
            var dz = pos1.Value.z - pos2.Value.z;
            
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Calculate distance between two markets using database-driven accurate positions
        /// Requirement 4.3: Distance calculations between market positions
        /// </summary>
        public double CalculateDistance(ulong marketId1, ulong marketId2)
        {
            if (marketId1 == marketId2) return 0;
            
            cacheLock.EnterReadLock();
            try
            {
                return CalculateDistanceInternal(marketId1, marketId2);
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Internal method to calculate distance without acquiring locks using database-driven positions
        /// </summary>
        private double CalculateDistanceInternal(ulong marketId1, ulong marketId2)
        {
            if (marketId1 == marketId2) return 0;
            
            // First try database-driven market location information (most accurate)
            var location1 = marketLocationCache.TryGetValue(marketId1, out var loc1) ? loc1 : null;
            var location2 = marketLocationCache.TryGetValue(marketId2, out var loc2) ? loc2 : null;
            
            if (location1 != null && location2 != null)
            {
                // If markets are on the same planet, distance is effectively 0
                if (location1.PlanetId == location2.PlanetId)
                {
                    logger.LogDebug($"Markets {marketId1} and {marketId2} are on the same planet ({location1.PlanetId}) - distance: 0");
                    return 0;
                }
                
                // Calculate inter-planetary distance using accurate database positions
                var distance = CalculateDistance(location1.Position, location2.Position);
                logger.LogDebug($"Database-driven distance between market {marketId1} (planet {location1.PlanetId}) and market {marketId2} (planet {location2.PlanetId}): {distance:F0}");
                return distance;
            }
            
            // Fallback to Orleans market cache positions
            var market1 = marketCache.TryGetValue(marketId1, out var m1) ? m1 : null;
            var market2 = marketCache.TryGetValue(marketId2, out var m2) ? m2 : null;
            
            if (market1?.Position != null && market2?.Position != null)
            {
                logger.LogDebug($"Using Orleans market positions for distance calculation between {marketId1} and {marketId2}");
                return CalculateDistance(market1.Position, market2.Position);
            }
            
            // Fallback to planet-based distance if market positions unavailable
            if (market1 != null && market2 != null)
            {
                logger.LogDebug($"Using planet-based distance calculation between {marketId1} (planet {market1.PlanetId}) and {marketId2} (planet {market2.PlanetId})");
                return CalculateDistanceBetweenPlanetsInternal(market1.PlanetId, market2.PlanetId);
            }
            
            // Last resort: arbitrary distance based on market IDs
            logger.LogWarning($"No position data available for markets {marketId1} and {marketId2}, using fallback distance calculation");
            return Math.Abs((long)marketId1 - (long)marketId2) * 1000;
        }

        /// <summary>
        /// Calculate distance between planets using their positions
        /// Requirement 4.3: Distance calculations for route planning
        /// </summary>
        public double CalculateDistanceBetweenPlanets(ulong planetId1, ulong planetId2)
        {
            if (planetId1 == planetId2) return 0;
            
            cacheLock.EnterReadLock();
            try
            {
                return CalculateDistanceBetweenPlanetsInternal(planetId1, planetId2);
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Internal method to calculate distance between planets without acquiring locks using database-driven positions
        /// </summary>
        private double CalculateDistanceBetweenPlanetsInternal(ulong planetId1, ulong planetId2)
        {
            if (planetId1 == planetId2) return 0;
            
            // First try database-driven planet information (most accurate)
            var planetInfo1 = planetInfoCache.TryGetValue(planetId1, out var pi1) ? pi1 : null;
            var planetInfo2 = planetInfoCache.TryGetValue(planetId2, out var pi2) ? pi2 : null;
            
            if (planetInfo1 != null && planetInfo2 != null)
            {
                var distance = CalculateDistance(planetInfo1.Position, planetInfo2.Position);
                logger.LogDebug($"Database-driven distance between planet {planetId1} ({planetInfo1.Name}) and planet {planetId2} ({planetInfo2.Name}): {distance:F0}");
                return distance;
            }
            
            // Fallback to Orleans planet cache
            var planet1 = planetCache.TryGetValue(planetId1, out var p1) ? p1 : null;
            var planet2 = planetCache.TryGetValue(planetId2, out var p2) ? p2 : null;
            
            if (planet1?.Position != null && planet2?.Position != null)
            {
                logger.LogDebug($"Using Orleans planet positions for distance calculation between {planetId1} ({planet1.Name}) and {planetId2} ({planet2.Name})");
                return CalculateDistance(planet1.Position, planet2.Position);
            }
            
            // Fallback to arbitrary distance if positions unavailable
            logger.LogWarning($"No position data available for planets {planetId1} and {planetId2}, using fallback distance calculation");
            return Math.Abs((long)planetId1 - (long)planetId2) * 10000000; // Larger scale for planets
        }

        /// <summary>
        /// Get all markets on the same planet as the specified market
        /// </summary>
        public List<MarketData> GetMarketsOnSamePlanet(ulong marketId)
        {
            cacheLock.EnterReadLock();
            try
            {
                // First try database-driven approach
                if (marketLocationCache.TryGetValue(marketId, out var locationInfo))
                {
                    if (marketsByPlanetCache.TryGetValue(locationInfo.PlanetId, out var marketsOnPlanet))
                    {
                        var result = new List<MarketData>();
                        foreach (var otherMarketId in marketsOnPlanet)
                        {
                            if (marketCache.TryGetValue(otherMarketId, out var marketData))
                            {
                                result.Add(marketData);
                            }
                        }
                        logger.LogDebug($"Found {result.Count} markets on planet {locationInfo.PlanetId} (database-driven)");
                        return result;
                    }
                }

                // Fallback to Orleans cache approach
                if (marketCache.TryGetValue(marketId, out var market))
                {
                    var marketsOnSamePlanet = marketCache.Values
                        .Where(m => m.PlanetId == market.PlanetId)
                        .ToList();
                    
                    logger.LogDebug($"Found {marketsOnSamePlanet.Count} markets on planet {market.PlanetName} (Orleans fallback)");
                    return marketsOnSamePlanet;
                }

                logger.LogWarning($"Market {marketId} not found in any cache");
                return new List<MarketData>();
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get markets grouped by planet
        /// </summary>
        public Dictionary<string, List<MarketData>> GetMarketsByPlanet()
        {
            cacheLock.EnterReadLock();
            try
            {
                var result = new Dictionary<string, List<MarketData>>();
                
                // Use database-driven grouping if available
                if (marketsByPlanetCache.Count > 0)
                {
                    foreach (var (planetId, marketIds) in marketsByPlanetCache)
                    {
                        var planetName = planetInfoCache.TryGetValue(planetId, out var planetInfo) 
                            ? planetInfo.Name 
                            : $"Planet {planetId}";
                        
                        var markets = new List<MarketData>();
                        foreach (var marketId in marketIds)
                        {
                            if (marketCache.TryGetValue(marketId, out var marketData))
                            {
                                markets.Add(marketData);
                            }
                        }
                        
                        if (markets.Count > 0)
                        {
                            result[planetName] = markets;
                        }
                    }
                }
                else
                {
                    // Fallback to Orleans cache grouping
                    result = marketCache.Values
                        .GroupBy(m => m.PlanetName)
                        .ToDictionary(g => g.Key, g => g.ToList());
                }

                logger.LogDebug($"Grouped markets by planet: {result.Count} planets with markets");
                return result;
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Start periodic refresh with configurable intervals and error handling
        /// Requirements 5.2, 5.3: Configurable refresh intervals with error handling
        /// </summary>
        public async Task StartPeriodicRefresh(CancellationToken cancellationToken)
        {
            logger.LogInformation($"Starting periodic market data refresh every {refreshInterval.TotalMinutes} minutes");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(refreshInterval, cancellationToken);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await RefreshMarketData();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during periodic market data refresh");
                    
                    // Exponential backoff on failures (Requirement 5.2)
                    var backoffDelay = TimeSpan.FromMinutes(Math.Min(30, Math.Pow(2, consecutiveFailures)));
                    logger.LogInformation($"Backing off for {backoffDelay.TotalMinutes} minutes after failure");
                    
                    try
                    {
                        await Task.Delay(backoffDelay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            logger.LogInformation("Periodic market data refresh stopped");
        }

        /// <summary>
        /// Get cache age for staleness detection
        /// Requirement 5.3: Cache age and staleness detection
        /// </summary>
        public TimeSpan GetCacheAge()
        {
            return DateTime.UtcNow - lastSuccessfulRefresh;
        }

        /// <summary>
        /// Check if cache data is stale
        /// Requirement 5.3: Cache staleness detection
        /// </summary>
        public bool IsCacheStale()
        {
            return GetCacheAge() > maxCacheAge;
        }

        /// <summary>
        /// Check if we have valid cached data to fall back on
        /// Requirement 5.5: Graceful degradation
        /// </summary>
        public bool HasValidCachedData()
        {
            cacheLock.EnterReadLock();
            try
            {
                return marketCache.Count > 0 && allOrders.Count > 0 && lastSuccessfulRefresh != DateTime.MinValue;
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get cache statistics for monitoring
        /// Requirement 5.3: Cache metadata tracking
        /// </summary>
        public virtual CacheStatistics GetCacheStatistics()
        {
            cacheLock.EnterReadLock();
            try
            {
                return new CacheStatistics
                {
                    MarketCount = marketCache.Count,
                    OrderCount = allOrders.Count,
                    PlayerNameCount = playerNameCache.Count,
                    ItemNameCount = itemNameCache.Count,
                    LastSuccessfulRefresh = lastSuccessfulRefresh,
                    LastRefreshAttempt = lastRefreshAttempt,
                    CacheAge = GetCacheAge(),
                    IsStale = IsCacheStale(),
                    IsRefreshing = isRefreshing,
                    ConsecutiveFailures = consecutiveFailures,
                    OrleansAvailable = orleansAvailable
                };
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Force a cache refresh (for administrative purposes)
        /// Requirement 5.2: Manual cache refresh capability
        /// </summary>
        public async Task ForceRefresh()
        {
            logger.LogInformation("Forcing cache refresh...");
            
            // Reset failure counters to allow immediate refresh
            lock (refreshLock)
            {
                consecutiveFailures = 0;
                orleansAvailable = true;
            }
            
            await RefreshMarketData();
        }

        /// <summary>
        /// Get all cached planets with thread-safe access
        /// Requirement 4.4: Planet name mapping and coordinate display functionality
        /// </summary>
        public List<PlanetData> GetAllPlanets()
        {
            cacheLock.EnterReadLock();
            try
            {
                return planetCache.Values.ToList();
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get planet information by ID
        /// Requirement 4.4: Planet name mapping and coordinate display functionality
        /// </summary>
        public PlanetData? GetPlanetById(ulong planetId)
        {
            cacheLock.EnterReadLock();
            try
            {
                return planetCache.TryGetValue(planetId, out var planet) ? planet : null;
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get markets by planet with location information
        /// Requirement 4.4: Planet name mapping and coordinate display functionality
        /// </summary>
        public List<MarketData> GetMarketsByPlanet(ulong planetId)
        {
            cacheLock.EnterReadLock();
            try
            {
                return marketCache.Values.Where(m => m.PlanetId == planetId).ToList();
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get markets within a certain distance from a position
        /// Requirement 4.3, 4.6: Distance calculations and route planning
        /// </summary>
        public List<MarketData> GetMarketsWithinDistance(Vec3? position, double maxDistance)
        {
            if (position == null) return new List<MarketData>();
            
            cacheLock.EnterReadLock();
            try
            {
                return marketCache.Values
                    .Where(m => m.Position != null && CalculateDistance(position, m.Position) <= maxDistance)
                    .OrderBy(m => CalculateDistance(position, m.Position))
                    .ToList();
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get the closest markets to a given position
        /// Requirement 4.3, 4.6: Distance calculations and route planning
        /// </summary>
        public List<MarketData> GetClosestMarkets(Vec3? position, int count = 10)
        {
            if (position == null) return new List<MarketData>();
            
            cacheLock.EnterReadLock();
            try
            {
                return marketCache.Values
                    .Where(m => m.Position != null)
                    .OrderBy(m => CalculateDistance(position, m.Position))
                    .Take(count)
                    .ToList();
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Clear all cached data (for administrative purposes)
        /// Requirement 5.1: Cache management
        /// </summary>
        public void ClearCache()
        {
            logger.LogInformation("Clearing all cached data...");
            
            cacheLock.EnterWriteLock();
            try
            {
                marketCache.Clear();
                allOrders.Clear();
                playerNameCache.Clear();
                itemNameCache.Clear();
                planetCache.Clear();
                
                lastSuccessfulRefresh = DateTime.MinValue;
                lastRefreshAttempt = DateTime.MinValue;
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
            
            // Reload planet data
            _ = Task.Run(async () => await LoadPlanetData());
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            cacheLock?.Dispose();
        }
    }
}