using Microsoft.AspNetCore.Mvc;
using MarketBrowserMod.Services;
using MarketBrowserMod.Models;
using MarketBrowserMod.Utils;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NQ;

namespace MarketBrowserMod.Controllers
{
    /// <summary>
    /// Comprehensive Market API Controller with advanced search, filtering, and profit analysis
    /// Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6: Complete Web API implementation
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MarketController : ControllerBase
    {
        private readonly MarketDataService marketDataService;
        private readonly ProfitAnalysisService profitAnalysisService;
        private readonly BaseMarketService baseMarketService;
        private readonly RouteOptimizationService routeOptimizationService;
        private readonly ILogger<MarketController> logger;

        public MarketController(
            MarketDataService marketDataService,
            ProfitAnalysisService profitAnalysisService,
            BaseMarketService baseMarketService,
            RouteOptimizationService routeOptimizationService,
            ILogger<MarketController> logger)
        {
            this.marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            this.profitAnalysisService = profitAnalysisService ?? throw new ArgumentNullException(nameof(profitAnalysisService));
            this.baseMarketService = baseMarketService ?? throw new ArgumentNullException(nameof(baseMarketService));
            this.routeOptimizationService = routeOptimizationService ?? throw new ArgumentNullException(nameof(routeOptimizationService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Market Endpoints

        /// <summary>
        /// Get all markets with optional filtering and pagination
        /// Requirement 2.1: Market browsing interface with sortable and filterable tables
        /// Task 12: Enhanced error handling with graceful degradation
        /// </summary>
        [HttpGet("markets")]
        public IActionResult GetMarkets(
            [FromQuery] string? planetName = null,
            [FromQuery] string? marketName = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? sortBy = "Name",
            [FromQuery] string? sortOrder = "asc")
        {
            try
            {
                // Validate pagination parameters with fallback values
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 1000) pageSize = 50;

                // Check system health and provide appropriate response
                var cacheStats = marketDataService.GetCacheStatistics();
                if (cacheStats.MarketCount == 0)
                {
                    logger.LogWarning("No market data available - system may be initializing or experiencing issues");
                    return ServiceUnavailable("Market data is currently unavailable. The system may be initializing or experiencing connectivity issues.");
                }

                var markets = marketDataService.GetAllMarkets();

                // Apply filtering with null safety
                if (!string.IsNullOrEmpty(planetName))
                {
                    markets = markets.Where(m => m.PlanetName?.Contains(planetName, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }

                if (!string.IsNullOrEmpty(marketName))
                {
                    markets = markets.Where(m => m.Name?.Contains(marketName, StringComparison.OrdinalIgnoreCase) == true).ToList();
                }

                // Apply sorting with error handling
                try
                {
                    markets = ApplyMarketSorting(markets, sortBy, sortOrder);
                }
                catch (Exception sortEx)
                {
                    logger.LogWarning(sortEx, "Error applying market sorting, using default order");
                    markets = markets.OrderBy(m => m.Name ?? "").ToList();
                }

                // Apply pagination
                var totalCount = markets.Count;
                var pagedMarkets = markets
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m =>
                    {
                        try
                        {
                            return m.ToResponse(baseMarketService, marketDataService);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Error converting market {MarketId} to response", m.MarketId);
                            // Return a minimal response for problematic markets
                            return new MarketResponse
                            {
                                MarketId = m.MarketId,
                                Name = m.Name ?? $"Market {m.MarketId}",
                                PlanetName = m.PlanetName ?? "Unknown",
                                OrderCount = 0,
                                LastUpdated = m.LastUpdated,
                                DistanceFromOriginFormatted = "Unknown"
                            };
                        }
                    })
                    .ToList();

                var response = new PagedResponse<MarketResponse>
                {
                    Data = pagedMarkets,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    HasNextPage = page * pageSize < totalCount,
                    HasPreviousPage = page > 1,
                    LastUpdated = cacheStats.LastSuccessfulRefresh
                };

                // Add data freshness warnings
                if (cacheStats.IsStale)
                {
                    Response.Headers["X-Data-Warning"] = "Data may be stale";
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving markets with parameters: planetName={PlanetName}, marketName={MarketName}, page={Page}, pageSize={PageSize}",
                    planetName, marketName, page, pageSize);

                return HandleControllerError(ex, "Failed to retrieve market data");
            }
        }

        /// <summary>
        /// Get market by ID with detailed information
        /// Requirement 2.1: Market browsing interface
        /// </summary>
        [HttpGet("markets/{marketId}")]
        public IActionResult GetMarketById(ulong marketId)
        {
            try
            {
                var markets = marketDataService.GetAllMarkets();
                var market = markets.FirstOrDefault(m => m.MarketId == marketId);

                if (market == null)
                {
                    return NotFound(new { error = "Market not found", marketId });
                }

                return Ok(market.ToResponse());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving market {MarketId}", marketId);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get markets by planet with location information
        /// Requirement 4.4: Planet name mapping and coordinate display functionality
        /// </summary>
        [HttpGet("markets/planet/{planetName}")]
        public IActionResult GetMarketsByPlanet(string planetName)
        {
            try
            {
                var markets = marketDataService.GetAllMarkets()
                    .Where(m => m.PlanetName.Equals(planetName, StringComparison.OrdinalIgnoreCase))
                    .Select(m => m.ToResponse())
                    .ToList();

                return Ok(markets);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving markets for planet {PlanetName}", planetName);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        #endregion

        #region Version Information

        /// <summary>
        /// Get application version and build information
        /// </summary>
        [HttpGet("version")]
        public IActionResult GetVersion()
        {
            try
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                var buildDate = System.IO.File.GetLastWriteTime(System.Reflection.Assembly.GetExecutingAssembly().Location);

                return Ok(new
                {
                    version = version?.ToString() ?? "1.0.0",
                    buildDate = buildDate.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    name = "MyDU Market Browser",
                    description = "Advanced market analysis and trading opportunities for Dual Universe",
                    developer = new
                    {
                        name = "karich.design",
                        website = "https://karich.design/",
                        description = "Professional web development & design solutions"
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving version information");
                return Ok(new
                {
                    version = "1.0.0",
                    name = "MyDU Market Browser",
                    developer = new
                    {
                        name = "karich.design",
                        website = "https://karich.design/"
                    }
                });
            }
        }

        #endregion

        #region Base Market Endpoints

        /// <summary>
        /// Get the currently selected base market
        /// </summary>
        [HttpGet("base-market")]
        public IActionResult GetBaseMarket()
        {
            try
            {
                var baseMarketInfo = baseMarketService.GetBaseMarketInfo();
                if (baseMarketInfo == null)
                {
                    return Ok(new { baseMarket = (object?)null, message = "No base market selected" });
                }

                return Ok(new { baseMarket = baseMarketInfo });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving base market");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Set a market as the base market for distance calculations
        /// </summary>
        [HttpPost("base-market/{marketId}")]
        public IActionResult SetBaseMarket(ulong marketId)
        {
            try
            {
                var markets = marketDataService.GetAllMarkets();
                var market = markets.FirstOrDefault(m => m.MarketId == marketId);

                if (market == null)
                {
                    return NotFound(new { error = "Market not found", marketId });
                }

                baseMarketService.SetBaseMarket(market);
                var baseMarketInfo = baseMarketService.GetBaseMarketInfo();

                return Ok(new
                {
                    message = $"Base market set to {market.Name} on {market.PlanetName}",
                    baseMarket = baseMarketInfo
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error setting base market {MarketId}", marketId);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Clear the base market selection
        /// </summary>
        [HttpDelete("base-market")]
        public IActionResult ClearBaseMarket()
        {
            try
            {
                baseMarketService.SetBaseMarket(null);
                return Ok(new { message = "Base market cleared", baseMarket = (object?)null });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error clearing base market");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        #endregion

        #region Order Endpoints

        /// <summary>
        /// Get orders with comprehensive search and filtering functionality
        /// Requirements 2.1, 2.2, 2.3, 2.4, 2.5: Advanced search and filtering
        /// Task 12: Enhanced error handling with graceful degradation
        /// </summary>
        [HttpGet("orders")]
        public IActionResult GetOrders([FromQuery] OrderFilter filter)
        {
            try
            {
                // Validate pagination parameters with fallback values
                if (filter.Page < 1) filter.Page = 1;
                if (filter.PageSize < 1 || filter.PageSize > 1000) filter.PageSize = 50;

                // Check system health and provide appropriate response
                var cacheStats = marketDataService.GetCacheStatistics();
                if (cacheStats.OrderCount == 0)
                {
                    logger.LogWarning("No order data available - system may be initializing or experiencing issues");
                    return ServiceUnavailable("Order data is currently unavailable. The system may be initializing or experiencing connectivity issues.");
                }

                var orders = marketDataService.GetAllOrders();

                // Apply comprehensive filtering with error handling
                try
                {
                    orders = ApplyOrderFiltering(orders, filter);
                }
                catch (Exception filterEx)
                {
                    logger.LogWarning(filterEx, "Error applying order filtering, returning unfiltered results");
                    // Continue with unfiltered orders rather than failing completely
                }

                // Apply sorting with error handling
                try
                {
                    orders = ApplyOrderSorting(orders, filter.SortBy, filter.SortOrder);
                }
                catch (Exception sortEx)
                {
                    logger.LogWarning(sortEx, "Error applying order sorting, using default order");
                    orders = orders.OrderBy(o => o.UnitPrice.amount).ToList();
                }

                // Apply pagination and get market data for planet names
                var totalCount = orders.Count;
                var markets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);
                var pagedOrders = orders
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .Select(o =>
                    {
                        try
                        {
                            markets.TryGetValue(o.MarketId, out var market);
                            return o.ToResponse(market);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Error converting order {OrderId} to response", o.OrderId);
                            // Return a minimal response for problematic orders
                            return new OrderResponse
                            {
                                OrderId = o.OrderId,
                                MarketId = o.MarketId,
                                MarketName = o.MarketName ?? "Unknown Market",
                                ItemName = o.ItemName ?? $"Item {o.ItemType}",
                                PlayerName = o.PlayerName ?? $"Player {o.PlayerId}",
                                UnitPrice = Utils.PriceConverter.ToDecimalPrice(o.UnitPrice),
                                Quantity = o.Quantity,
                                IsBuyOrder = o.IsBuyOrder,
                                ExpirationDate = o.ExpirationDate,
                                LastUpdated = o.LastUpdated,
                                PlanetName = "Unknown Planet"
                            };
                        }
                    })
                    .ToList();

                var response = new PagedResponse<OrderResponse>
                {
                    Data = pagedOrders,
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize),
                    HasNextPage = filter.Page * filter.PageSize < totalCount,
                    HasPreviousPage = filter.Page > 1,
                    LastUpdated = cacheStats.LastSuccessfulRefresh
                };

                // Add data freshness warnings
                if (cacheStats.IsStale)
                {
                    Response.Headers["X-Data-Warning"] = "Order data may be stale";
                }

                if (cacheStats.ConsecutiveFailures > 0)
                {
                    Response.Headers["X-System-Warning"] = $"System experiencing issues ({cacheStats.ConsecutiveFailures} recent failures)";
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving orders with filter {@Filter}", filter);
                return HandleControllerError(ex, "Failed to retrieve order data");
            }
        }

        /// <summary>
        /// Get order by ID
        /// Requirement 2.1: Order browsing interface
        /// </summary>
        [HttpGet("orders/{orderId}")]
        public IActionResult GetOrderById(ulong orderId)
        {
            try
            {
                var orders = marketDataService.GetAllOrders();
                var order = orders.FirstOrDefault(o => o.OrderId == orderId);

                if (order == null)
                {
                    return NotFound(new { error = "Order not found", orderId });
                }

                return Ok(order.ToResponse());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving order {OrderId}", orderId);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Search orders by item name with advanced filtering
        /// Requirement 2.1: Case-insensitive partial matching for item names
        /// </summary>
        [HttpGet("orders/search/item")]
        public IActionResult SearchOrdersByItem(
            [FromQuery] string itemName,
            [FromQuery] string? orderType = null,
            [FromQuery] long? minPrice = null,
            [FromQuery] long? maxPrice = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                if (string.IsNullOrEmpty(itemName))
                {
                    return BadRequest(new { error = "Item name is required" });
                }

                var filter = new OrderFilter
                {
                    ItemName = itemName,
                    OrderType = orderType,
                    MinPrice = minPrice,
                    MaxPrice = maxPrice,
                    Page = page,
                    PageSize = pageSize
                };

                return GetOrders(filter);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error searching orders by item {ItemName}", itemName);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Search orders by market with location filtering
        /// Requirement 2.2: Market name and location-based filtering
        /// </summary>
        [HttpGet("orders/search/market")]
        public IActionResult SearchOrdersByMarket(
            [FromQuery] string? marketName = null,
            [FromQuery] string? planetName = null,
            [FromQuery] string? orderType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var filter = new OrderFilter
                {
                    MarketName = marketName,
                    PlanetName = planetName,
                    OrderType = orderType,
                    Page = page,
                    PageSize = pageSize
                };

                return GetOrders(filter);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error searching orders by market {MarketName}", marketName);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Search orders by player name
        /// Requirement 2.1: Player-based filtering
        /// </summary>
        [HttpGet("orders/search/player")]
        public IActionResult SearchOrdersByPlayer(
            [FromQuery] string playerName,
            [FromQuery] string? orderType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                if (string.IsNullOrEmpty(playerName))
                {
                    return BadRequest(new { error = "Player name is required" });
                }

                var filter = new OrderFilter
                {
                    PlayerName = playerName,
                    OrderType = orderType,
                    Page = page,
                    PageSize = pageSize
                };

                return GetOrders(filter);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error searching orders by player {PlayerName}", playerName);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        #endregion

        #region Profit Opportunity Endpoints

        /// <summary>
        /// Get profit opportunities with comprehensive filtering and analysis
        /// Requirements 3.1, 3.2, 3.3, 3.5, 3.6: Complete profit opportunity analysis
        /// Task 12: Enhanced error handling with graceful degradation
        /// </summary>
        [HttpGet("profits")]
        public async Task<IActionResult> GetProfitOpportunities([FromQuery] ProfitFilter filter)
        {
            try
            {
                // Validate pagination parameters
                if (filter.Page < 1) filter.Page = 1;
                if (filter.PageSize < 1 || filter.PageSize > 1000) filter.PageSize = 50;

                // Check system health for profit calculations
                var cacheStats = marketDataService.GetCacheStatistics();
                if (cacheStats.OrderCount < 2)
                {
                    logger.LogWarning("Insufficient order data for profit calculations - need at least 2 orders");
                    return ServiceUnavailable("Insufficient data for profit calculations. The system may be initializing or experiencing connectivity issues.");
                }

                // Execute profit calculation with circuit breaker
                var result = await ExecuteWithCircuitBreaker(
                    async () =>
                    {
                        await Task.Yield(); // Make it async
                        return marketDataService.GetPaginatedProfitOpportunities(filter);
                    },
                    "GetPaginatedProfitOpportunities");

                // Convert to response DTOs with market information and error handling
                var markets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);
                var responseData = result.Data.Select(opportunity =>
                {
                    try
                    {
                        var buyMarket = markets.TryGetValue(opportunity.BuyOrder.MarketId, out var bm) ? bm : null;
                        var sellMarket = markets.TryGetValue(opportunity.SellOrder.MarketId, out var sm) ? sm : null;
                        return opportunity.ToResponse(buyMarket, sellMarket);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error converting profit opportunity to response");
                        // Return a minimal response for problematic opportunities
                        return new ProfitOpportunityResponse
                        {
                            ItemName = opportunity.ItemName ?? $"Item {opportunity.ItemType}",
                            ItemType = opportunity.ItemType,
                            ProfitPerUnit = Utils.PriceConverter.ToDecimalPrice(opportunity.ProfitPerUnit),
                            ProfitMargin = opportunity.ProfitMargin,
                            TotalProfit = Utils.PriceConverter.ToDecimalPrice(opportunity.TotalProfit),
                            MaxQuantity = opportunity.MaxQuantity,
                            Distance = opportunity.Distance,
                            DistanceFormatted = Utils.DistanceFormatter.FormatDistance(opportunity.Distance),
                            ProfitPerKm = opportunity.ProfitPerKm,
                            BuyOrder = new OrderSummary { MarketName = "Unknown Market", PlanetName = "Unknown Planet" },
                            SellOrder = new OrderSummary { MarketName = "Unknown Market", PlanetName = "Unknown Planet" },
                            LastUpdated = DateTime.UtcNow
                        };
                    }
                }).ToList();

                var response = new PagedResponse<ProfitOpportunityResponse>
                {
                    Data = responseData,
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                    TotalPages = result.TotalPages,
                    HasNextPage = result.HasNextPage,
                    HasPreviousPage = result.HasPreviousPage,
                    LastUpdated = result.LastUpdated
                };

                // Add data quality warnings
                if (cacheStats.IsStale)
                {
                    Response.Headers["X-Data-Warning"] = "Profit data may be stale";
                }

                if (responseData.Count < result.Data.Count())
                {
                    Response.Headers["X-Processing-Warning"] = "Some profit opportunities could not be processed";
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving profit opportunities with filter {@Filter}", filter);
                return HandleControllerError(ex, "Failed to calculate profit opportunities");
            }
        }

        /// <summary>
        /// Get top profit opportunities by different metrics
        /// Requirements 3.5, 3.6: Ranking opportunities by various criteria
        /// </summary>
        [HttpGet("profits/top")]
        public IActionResult GetTopProfitOpportunities([FromQuery] int count = 10, [FromQuery] string metric = "TotalProfit")
        {
            try
            {
                if (count < 1 || count > 100) count = 10;

                var topOpportunities = marketDataService.GetTopProfitOpportunities(count);

                if (!topOpportunities.ContainsKey($"By{metric}"))
                {
                    return BadRequest(new { error = "Invalid metric", validMetrics = topOpportunities.Keys });
                }

                var opportunities = topOpportunities[$"By{metric}"];
                var markets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);

                var response = opportunities.Select(opportunity =>
                {
                    var buyMarket = markets.TryGetValue(opportunity.BuyOrder.MarketId, out var bm) ? bm : null;
                    var sellMarket = markets.TryGetValue(opportunity.SellOrder.MarketId, out var sm) ? sm : null;
                    return opportunity.ToResponse(buyMarket, sellMarket);
                }).ToList();

                return Ok(new { metric, count = response.Count, opportunities = response });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving top profit opportunities");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get comprehensive profit analysis with market insights
        /// Requirements 3.1, 3.2, 3.3: Advanced profit analysis
        /// </summary>
        [HttpGet("profits/analysis")]
        public IActionResult GetProfitAnalysis([FromQuery] ProfitFilter? filter = null)
        {
            try
            {
                var analysis = profitAnalysisService.AnalyzeProfitOpportunities(filter);
                return Ok(analysis);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error performing profit analysis");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get profit opportunities for a specific item
        /// Requirement 3.1: Item-specific profit analysis
        /// </summary>
        [HttpGet("profits/item/{itemName}")]
        public async Task<IActionResult> GetProfitOpportunitiesForItem(
            string itemName,
            [FromQuery] double minProfitMargin = 0.0,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var filter = new ProfitFilter
                {
                    ItemName = itemName,
                    MinProfitMargin = minProfitMargin,
                    Page = page,
                    PageSize = pageSize,
                    SortBy = "TotalProfit",
                    SortOrder = "desc"
                };

                return await GetProfitOpportunities(filter);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving profit opportunities for item {ItemName}", itemName);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get profit opportunities within distance range
        /// Requirements 3.5, 3.6: Distance-based filtering for route planning
        /// </summary>
        [HttpGet("profits/distance")]
        public async Task<IActionResult> GetProfitOpportunitiesWithinDistance(
            [FromQuery] double maxDistance,
            [FromQuery] double minProfitPerKm = 0.0,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var filter = new ProfitFilter
                {
                    MaxDistance = maxDistance,
                    Page = page,
                    PageSize = pageSize,
                    SortBy = "ProfitPerKm",
                    SortOrder = "desc"
                };

                var result = await GetProfitOpportunities(filter);

                // Additional filtering by profit per km if specified
                if (minProfitPerKm > 0 && result is OkObjectResult okResult && okResult.Value is PagedResponse<ProfitOpportunityResponse> pagedResponse)
                {
                    pagedResponse.Data = pagedResponse.Data.Where(o => o.ProfitPerKm >= minProfitPerKm).ToList();
                    pagedResponse.TotalCount = pagedResponse.Data.Count;
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving profit opportunities within distance {MaxDistance}", maxDistance);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Generate trading route recommendations
        /// Requirements 3.5, 3.6: Route planning and optimization
        /// </summary>
        [HttpGet("profits/routes")]
        public IActionResult GetTradingRoutes(
            [FromQuery] int maxRoutes = 5,
            [FromQuery] double maxTotalDistance = 10000000)
        {
            try
            {
                var routes = profitAnalysisService.GenerateTradingRoutes(maxRoutes, maxTotalDistance);
                return Ok(new { routes, count = routes.Count, maxDistance = maxTotalDistance });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating trading routes");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get route-based profit opportunities with base market and destination filtering
        /// </summary>
        [HttpGet("profits/route")]
        public IActionResult GetRouteBasedProfitOpportunities([FromQuery] ProfitFilter filter)
        {
            try
            {
                // Validate pagination parameters
                if (filter.Page < 1) filter.Page = 1;
                if (filter.PageSize < 1 || filter.PageSize > 1000) filter.PageSize = 50;

                // Use base market from service if not specified in filter
                if (!filter.BaseMarketId.HasValue && !filter.BasePlanetId.HasValue)
                {
                    var baseMarket = baseMarketService.GetBaseMarket();
                    if (baseMarket != null)
                    {
                        filter.BaseMarketId = baseMarket.MarketId;
                    }
                }

                var opportunities = routeOptimizationService.GetRouteBasedOpportunities(filter);

                // Apply pagination
                var totalCount = opportunities.Count;
                var pagedOpportunities = opportunities
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToList();

                // Convert to response DTOs
                var markets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);
                var responseData = pagedOpportunities.Select(opportunity =>
                {
                    var buyMarket = markets.TryGetValue(opportunity.BuyOrder.MarketId, out var bm) ? bm : null;
                    var sellMarket = markets.TryGetValue(opportunity.SellOrder.MarketId, out var sm) ? sm : null;
                    return opportunity.ToResponse(buyMarket, sellMarket);
                }).ToList();

                var response = new PagedResponse<ProfitOpportunityResponse>
                {
                    Data = responseData,
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize),
                    HasNextPage = filter.Page * filter.PageSize < totalCount,
                    HasPreviousPage = filter.Page > 1,
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving route-based profit opportunities");
                return HandleControllerError(ex, "Failed to calculate route-based profit opportunities");
            }
        }

        /// <summary>
        /// Generate optimized trading routes
        /// </summary>
        [HttpGet("profits/routes/optimized")]
        public IActionResult GetOptimizedTradingRoutes([FromQuery] ProfitFilter filter, [FromQuery] int maxRoutes = 5, [FromQuery] int maxStops = 3)
        {
            try
            {
                var routes = routeOptimizationService.GenerateOptimizedRoutes(filter, maxRoutes, maxStops);

                var response = routes.Select(route => new
                {
                    markets = route.Markets.Select(m => m.ToResponse(baseMarketService, marketDataService)),
                    opportunities = route.Opportunities.Select(o =>
                    {
                        var markets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);
                        var buyMarket = markets.TryGetValue(o.BuyOrder.MarketId, out var bm) ? bm : null;
                        var sellMarket = markets.TryGetValue(o.SellOrder.MarketId, out var sm) ? sm : null;
                        return o.ToResponse(buyMarket, sellMarket);
                    }),
                    totalDistance = route.TotalDistance,
                    totalDistanceFormatted = DistanceFormatter.FormatDistance(route.TotalDistance),
                    totalProfit = Utils.PriceConverter.ToDecimalPrice(route.TotalProfit),
                    profitPerKm = route.ProfitPerKm,
                    stopCount = route.StopCount,
                    description = route.RouteDescription
                }).ToList();

                return Ok(new { routes = response, count = response.Count });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating optimized trading routes");
                return HandleControllerError(ex, "Failed to generate optimized trading routes");
            }
        }

        #endregion

        #region Location and Distance Endpoints

        /// <summary>
        /// Get all planets with market information
        /// Requirement 4.4: Planet name mapping and coordinate display functionality
        /// </summary>
        [HttpGet("planets")]
        public IActionResult GetPlanets()
        {
            try
            {
                var planets = marketDataService.GetAllPlanets();
                var markets = marketDataService.GetAllMarkets();

                var response = planets.Select(planet =>
                {
                    var marketCount = markets.Count(m => m.PlanetId == planet.PlanetId);
                    return planet.ToResponse(marketCount);
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving planets");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Calculate distance between two locations
        /// Requirements 4.3, 4.6: Distance calculations for route planning
        /// </summary>
        [HttpGet("distance")]
        public IActionResult CalculateDistance(
            [FromQuery] double x1, [FromQuery] double y1, [FromQuery] double z1,
            [FromQuery] double x2, [FromQuery] double y2, [FromQuery] double z2)
        {
            try
            {
                var pos1 = new Vec3 { x = x1, y = y1, z = z1 };
                var pos2 = new Vec3 { x = x2, y = y2, z = z2 };

                var distance = marketDataService.CalculateDistance(pos1, pos2);

                return Ok(new
                {
                    distance,
                    from = new { x = x1, y = y1, z = z1 },
                    to = new { x = x2, y = y2, z = z2 },
                    unit = "game units"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error calculating distance");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Find markets within distance from a position
        /// Requirements 4.3, 4.6: Distance calculations and route planning
        /// </summary>
        [HttpGet("markets/nearby")]
        public IActionResult GetNearbyMarkets(
            [FromQuery] double x, [FromQuery] double y, [FromQuery] double z,
            [FromQuery] double maxDistance,
            [FromQuery] int count = 10)
        {
            try
            {
                var position = new Vec3 { x = x, y = y, z = z };
                var nearbyMarkets = marketDataService.GetMarketsWithinDistance(position, maxDistance);

                var response = nearbyMarkets
                    .Take(count)
                    .Select(market => market.ToLocationResponse(position))
                    .ToList();

                return Ok(new
                {
                    queryPosition = new { x, y, z },
                    maxDistance,
                    found = response.Count,
                    markets = response
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error finding nearby markets");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        #endregion

        #region Administrative Endpoints



        /// <summary>
        /// Get system status and cache statistics
        /// Requirement 5.3: Cache metadata tracking and system monitoring
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetSystemStatus()
        {
            try
            {
                var stats = marketDataService.GetCacheStatistics();
                var markets = marketDataService.GetAllMarkets();
                var orders = marketDataService.GetAllOrders();
                var profitOpportunities = marketDataService.FindProfitOpportunities();

                var warnings = new List<string>();
                if (stats.IsStale) warnings.Add("Cache data is stale");
                if (stats.ConsecutiveFailures > 0) warnings.Add($"Recent failures: {stats.ConsecutiveFailures}");
                if (!stats.OrleansAvailable) warnings.Add("Orleans services unavailable");

                var response = new SystemStatusResponse
                {
                    IsHealthy = stats.MarketCount > 0 && stats.OrderCount > 0 && !stats.IsStale,
                    LastDataRefresh = stats.LastSuccessfulRefresh,
                    MarketCount = stats.MarketCount,
                    OrderCount = stats.OrderCount,
                    ProfitOpportunityCount = profitOpportunities.Count,
                    DataAge = stats.CacheAge,
                    Status = stats.IsStale ? "Stale" : stats.IsRefreshing ? "Refreshing" : "Healthy",
                    Warnings = warnings
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving system status");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Get comprehensive statistics about markets and orders
        /// Requirement 2.6: System statistics and monitoring
        /// </summary>
        [HttpGet("stats")]
        public IActionResult GetStatistics()
        {
            try
            {
                var markets = marketDataService.GetAllMarkets();
                var orders = marketDataService.GetAllOrders();
                var stats = marketDataService.GetCacheStatistics();

                var response = new
                {
                    markets = new
                    {
                        total = markets.Count,
                        byPlanet = markets.GroupBy(m => m.PlanetName)
                            .ToDictionary(g => g.Key, g => g.Count()),
                        withOrders = markets.Count(m => m.Orders.Any()),
                        averageOrdersPerMarket = markets.Any() ? markets.Average(m => m.Orders.Count) : 0
                    },
                    orders = new
                    {
                        total = orders.Count,
                        buyOrders = orders.Count(o => o.IsBuyOrder),
                        sellOrders = orders.Count(o => !o.IsBuyOrder),
                        uniqueItems = orders.Select(o => o.ItemType).Distinct().Count(),
                        uniquePlayers = orders.Select(o => o.PlayerId).Distinct().Count(),
                        averagePrice = orders.Any() ? Utils.PriceConverter.ToDecimalPrice(orders.Average(o => o.UnitPrice.amount)) : 0,
                        totalVolume = orders.Sum(o => o.Quantity)
                    },
                    cache = new
                    {
                        lastRefresh = stats.LastSuccessfulRefresh,
                        age = stats.CacheAge.TotalMinutes,
                        isStale = stats.IsStale,
                        isRefreshing = stats.IsRefreshing,
                        consecutiveFailures = stats.ConsecutiveFailures,
                        orleansAvailable = stats.OrleansAvailable
                    },
                    timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving statistics");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Apply comprehensive filtering to orders based on OrderFilter criteria
        /// Requirements 2.1, 2.2, 2.3, 2.4: Advanced filtering functionality
        /// </summary>
        private List<OrderData> ApplyOrderFiltering(List<OrderData> orders, OrderFilter filter)
        {
            var query = orders.AsEnumerable();

            // Requirement 2.1: Case-insensitive partial matching for item names
            if (!string.IsNullOrEmpty(filter.ItemName))
            {
                query = query.Where(o => o.ItemName.Contains(filter.ItemName, StringComparison.OrdinalIgnoreCase));
            }

            // Requirement 2.2: Market name and location-based filtering
            if (!string.IsNullOrEmpty(filter.MarketName))
            {
                query = query.Where(o => o.MarketName.Contains(filter.MarketName, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(filter.PlanetName))
            {
                var markets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);
                query = query.Where(o =>
                {
                    if (markets.TryGetValue(o.MarketId, out var market))
                    {
                        return market.PlanetName.Contains(filter.PlanetName, StringComparison.OrdinalIgnoreCase);
                    }
                    return false;
                });
            }

            // Requirement 2.3: Price range filtering
            if (filter.MinPrice.HasValue)
            {
                query = query.Where(o => o.UnitPrice.amount >= filter.MinPrice.Value);
            }

            if (filter.MaxPrice.HasValue)
            {
                query = query.Where(o => o.UnitPrice.amount <= filter.MaxPrice.Value);
            }

            // Requirement 2.4: Order type filtering (buy/sell)
            if (!string.IsNullOrEmpty(filter.OrderType))
            {
                switch (filter.OrderType.ToLower())
                {
                    case "buy":
                        query = query.Where(o => o.IsBuyOrder);
                        break;
                    case "sell":
                        query = query.Where(o => !o.IsBuyOrder);
                        break;
                }
            }

            // Player name filtering
            if (!string.IsNullOrEmpty(filter.PlayerName))
            {
                query = query.Where(o => o.PlayerName.Contains(filter.PlayerName, StringComparison.OrdinalIgnoreCase));
            }

            // Market ID filtering
            if (filter.MarketId.HasValue)
            {
                query = query.Where(o => o.MarketId == filter.MarketId.Value);
            }

            return query.ToList();
        }

        /// <summary>
        /// Apply sorting to orders based on specified criteria
        /// Requirement 2.5: Sortable tables with multiple criteria
        /// </summary>
        private List<OrderData> ApplyOrderSorting(List<OrderData> orders, string? sortBy, string? sortOrder)
        {
            var sortByLower = sortBy?.ToLower() ?? "unitprice";
            var sortOrderLower = sortOrder?.ToLower() ?? "asc";

            IOrderedEnumerable<OrderData> sortedOrders = sortByLower switch
            {
                "unitprice" or "price" => sortOrderLower == "desc"
                    ? orders.OrderByDescending(o => o.UnitPrice.amount)
                    : orders.OrderBy(o => o.UnitPrice.amount),
                "quantity" => sortOrderLower == "desc"
                    ? orders.OrderByDescending(o => o.Quantity)
                    : orders.OrderBy(o => o.Quantity),
                "itemname" or "item" => sortOrderLower == "desc"
                    ? orders.OrderByDescending(o => o.ItemName)
                    : orders.OrderBy(o => o.ItemName),
                "marketname" or "market" => sortOrderLower == "desc"
                    ? orders.OrderByDescending(o => o.MarketName)
                    : orders.OrderBy(o => o.MarketName),
                "playername" or "player" => sortOrderLower == "desc"
                    ? orders.OrderByDescending(o => o.PlayerName)
                    : orders.OrderBy(o => o.PlayerName),
                "expirationdate" or "expiration" => sortOrderLower == "desc"
                    ? orders.OrderByDescending(o => o.ExpirationDate)
                    : orders.OrderBy(o => o.ExpirationDate),
                "lastupdated" or "updated" => sortOrderLower == "desc"
                    ? orders.OrderByDescending(o => o.LastUpdated)
                    : orders.OrderBy(o => o.LastUpdated),
                "ordertype" or "type" => sortOrderLower == "desc"
                    ? orders.OrderByDescending(o => o.IsBuyOrder)
                    : orders.OrderBy(o => o.IsBuyOrder),
                _ => orders.OrderBy(o => o.UnitPrice.amount) // Default to price ascending
            };

            return sortedOrders.ToList();
        }

        /// <summary>
        /// Apply sorting to markets based on specified criteria
        /// Requirement 2.5: Sortable market tables
        /// </summary>
        private List<MarketData> ApplyMarketSorting(List<MarketData> markets, string? sortBy, string? sortOrder)
        {
            var sortByLower = sortBy?.ToLower() ?? "name";
            var sortOrderLower = sortOrder?.ToLower() ?? "asc";

            IOrderedEnumerable<MarketData> sortedMarkets = sortByLower switch
            {
                "name" => sortOrderLower == "desc"
                    ? markets.OrderByDescending(m => m.Name)
                    : markets.OrderBy(m => m.Name),
                "planetname" or "planet" => sortOrderLower == "desc"
                    ? markets.OrderByDescending(m => m.PlanetName)
                    : markets.OrderBy(m => m.PlanetName),
                "ordercount" or "orders" => sortOrderLower == "desc"
                    ? markets.OrderByDescending(m => m.Orders.Count)
                    : markets.OrderBy(m => m.Orders.Count),
                "distance" => sortOrderLower == "desc"
                    ? markets.OrderByDescending(m => m.DistanceFromOrigin)
                    : markets.OrderBy(m => m.DistanceFromOrigin),
                "lastupdated" or "updated" => sortOrderLower == "desc"
                    ? markets.OrderByDescending(m => m.LastUpdated)
                    : markets.OrderBy(m => m.LastUpdated),
                _ => markets.OrderBy(m => m.Name) // Default to name ascending
            };

            return sortedMarkets.ToList();
        }

        #endregion

        #region Error Handling Helper Methods - Task 12

        /// <summary>
        /// Centralized error handling for controller actions
        /// Task 12: Comprehensive error handling with appropriate HTTP status codes
        /// </summary>
        private IActionResult HandleControllerError(Exception ex, string userMessage)
        {
            return ex switch
            {
                TimeoutException => StatusCode(504, new
                {
                    error = "Gateway Timeout",
                    message = "The request timed out. Please try again later.",
                    userMessage,
                    timestamp = DateTime.UtcNow
                }),

                NQutils.Exceptions.BusinessException be when be.error.code == NQ.ErrorCode.InvalidSession => StatusCode(503, new
                {
                    error = "Service Unavailable",
                    message = "The service is temporarily unavailable due to session issues.",
                    userMessage,
                    timestamp = DateTime.UtcNow
                }),

                InvalidOperationException => StatusCode(503, new
                {
                    error = "Service Unavailable",
                    message = "The service is temporarily unavailable.",
                    userMessage,
                    timestamp = DateTime.UtcNow
                }),

                ArgumentException => BadRequest(new
                {
                    error = "Bad Request",
                    message = "Invalid request parameters.",
                    userMessage,
                    timestamp = DateTime.UtcNow
                }),

                _ => StatusCode(500, new
                {
                    error = "Internal Server Error",
                    message = "An unexpected error occurred.",
                    userMessage,
                    timestamp = DateTime.UtcNow
                })
            };
        }

        /// <summary>
        /// Return a service unavailable response with appropriate caching headers
        /// Task 12: Graceful degradation when services are unavailable
        /// </summary>
        private IActionResult ServiceUnavailable(string message)
        {
            Response.Headers["Retry-After"] = "60"; // Suggest retry after 60 seconds
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";

            return StatusCode(503, new
            {
                error = "Service Unavailable",
                message,
                retryAfter = 60,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Check if the system is in a healthy state for processing requests
        /// Task 12: Circuit breaker pattern for system health checks
        /// </summary>
        private bool IsSystemHealthy()
        {
            try
            {
                var stats = marketDataService.GetCacheStatistics();
                return stats.MarketCount > 0 && stats.OrleansAvailable && !stats.IsStale;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to check system health");
                return false;
            }
        }

        /// <summary>
        /// Execute an action with circuit breaker pattern
        /// Task 12: Circuit breaker pattern for Orleans calls
        /// </summary>
        private async Task<T> ExecuteWithCircuitBreaker<T>(Func<Task<T>> action, string operationName)
        {
            if (!IsSystemHealthy())
            {
                logger.LogWarning("System unhealthy, rejecting request for {OperationName}", operationName);
                throw new InvalidOperationException($"System is currently unhealthy for operation: {operationName}");
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var task = action();

                if (await Task.WhenAny(task, Task.Delay(System.Threading.Timeout.Infinite, cts.Token)) == task)
                {
                    return await task;
                }
                else
                {
                    throw new TimeoutException($"Operation {operationName} timed out");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Circuit breaker triggered for operation {OperationName}", operationName);
                throw;
            }
        }

        #endregion
    }
}