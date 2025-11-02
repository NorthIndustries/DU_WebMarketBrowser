using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketBrowserMod.Models;
using MarketBrowserMod.Utils;
using Microsoft.Extensions.Logging;
using NQ;

namespace MarketBrowserMod.Services
{
    /// <summary>
    /// Service for optimizing trading routes and calculating route-based profit opportunities
    /// </summary>
    public class RouteOptimizationService
    {
        private readonly MarketDataService marketDataService;
        private readonly BaseMarketService baseMarketService;
        private readonly ILogger<RouteOptimizationService> logger;

        public RouteOptimizationService(
            MarketDataService marketDataService,
            BaseMarketService baseMarketService,
            ILogger<RouteOptimizationService> logger)
        {
            this.marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            this.baseMarketService = baseMarketService ?? throw new ArgumentNullException(nameof(baseMarketService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get profit opportunities filtered by route constraints, focusing on base market inventory
        /// </summary>
        public List<ProfitOpportunity> GetRouteBasedOpportunities(ProfitFilter filter)
        {
            var baseMarket = GetBaseMarketForFilter(filter);
            var destinationMarket = GetDestinationMarketForFilter(filter);

            List<ProfitOpportunity> opportunities;

            if (baseMarket != null)
            {
                // Focus on opportunities starting from base market inventory
                opportunities = GetOpportunitiesFromBaseMarket(baseMarket, destinationMarket, filter);
            }
            else
            {
                // Fallback to general opportunities
                opportunities = marketDataService.FindProfitOpportunities(filter);
            }

            var filteredOpportunities = new List<ProfitOpportunity>();

            foreach (var opportunity in opportunities)
            {
                if (IsOpportunityValidForRoute(opportunity, filter))
                {
                    // Calculate route-specific metrics
                    EnhanceOpportunityWithRouteMetrics(opportunity, filter);
                    filteredOpportunities.Add(opportunity);
                }
            }

            // Sort by route efficiency if route optimization is enabled
            if (filter.RouteOptimization)
            {
                filteredOpportunities = filteredOpportunities
                    .OrderByDescending(o => o.EfficiencyScore)
                    .ThenByDescending(o => o.TotalProfit)
                    .ToList();
            }

            return filteredOpportunities;
        }

        /// <summary>
        /// Get profit opportunities based on what's available at the base market
        /// </summary>
        private List<ProfitOpportunity> GetOpportunitiesFromBaseMarket(MarketData baseMarket, MarketData? destinationMarket, ProfitFilter filter)
        {
            var opportunities = new List<ProfitOpportunity>();
            var allOrders = marketDataService.GetAllOrders();
            var allMarkets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);

            // Get sell orders (items available for purchase) at the base market
            var baseMarketSellOrders = allOrders
                .Where(o => o.MarketId == baseMarket.MarketId && !o.IsBuyOrder)
                .ToList();

            foreach (var sellOrder in baseMarketSellOrders)
            {
                // Find buy orders for the same item type
                var buyOrders = allOrders
                    .Where(o => o.ItemType == sellOrder.ItemType && o.IsBuyOrder && o.UnitPrice.amount > sellOrder.UnitPrice.amount)
                    .ToList();

                // If destination market is specified, filter to only that market
                if (destinationMarket != null)
                {
                    buyOrders = buyOrders.Where(o => o.MarketId == destinationMarket.MarketId).ToList();
                }

                foreach (var buyOrder in buyOrders)
                {
                    // Skip if same market (shouldn't happen but safety check)
                    if (sellOrder.MarketId == buyOrder.MarketId) continue;

                    var profitPerUnit = buyOrder.UnitPrice.amount - sellOrder.UnitPrice.amount;
                    if (profitPerUnit <= 0) continue;

                    var maxQuantity = Math.Min(sellOrder.Quantity, buyOrder.Quantity);
                    var totalProfit = profitPerUnit * maxQuantity;
                    var profitMargin = (double)profitPerUnit / sellOrder.UnitPrice.amount * 100;

                    // Apply profit filters
                    if (filter.MinProfitMargin.HasValue && profitMargin < filter.MinProfitMargin.Value * 100) continue;
                    if (filter.MinTotalProfit.HasValue && totalProfit < filter.MinTotalProfit.Value) continue;

                    // Calculate distance
                    var distance = marketDataService.CalculateDistance(sellOrder.MarketId, buyOrder.MarketId);
                    if (filter.MaxDistance.HasValue && distance > filter.MaxDistance.Value) continue;

                    var opportunity = new ProfitOpportunity
                    {
                        ItemType = sellOrder.ItemType,
                        ItemName = sellOrder.ItemName,
                        SellOrder = sellOrder, // Buy from this (sell order)
                        BuyOrder = buyOrder,   // Sell to this (buy order)
                        ProfitPerUnit = profitPerUnit,
                        MaxQuantity = maxQuantity,
                        TotalProfit = totalProfit,
                        ProfitMargin = profitMargin,
                        Distance = distance,
                        ProfitPerKm = distance > 0 ? totalProfit / distance : 0
                    };

                    opportunities.Add(opportunity);
                }
            }

            return opportunities;
        }

        /// <summary>
        /// Check if an opportunity is valid for the specified route constraints
        /// </summary>
        private bool IsOpportunityValidForRoute(ProfitOpportunity opportunity, ProfitFilter filter)
        {
            var markets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);
            
            var buyMarket = markets.TryGetValue(opportunity.SellOrder.MarketId, out var bm) ? bm : null; // Buy from sell order
            var sellMarket = markets.TryGetValue(opportunity.BuyOrder.MarketId, out var sm) ? sm : null; // Sell to buy order

            if (buyMarket == null || sellMarket == null)
                return false;

            // Base market/planet filtering
            if (filter.BaseMarketId.HasValue || filter.BasePlanetId.HasValue)
            {
                var baseMarket = GetBaseMarketForFilter(filter);
                if (baseMarket != null)
                {
                    var distanceFromBase = Math.Min(
                        marketDataService.CalculateDistance(baseMarket.MarketId, buyMarket.MarketId),
                        marketDataService.CalculateDistance(baseMarket.MarketId, sellMarket.MarketId)
                    );

                    if (filter.MaxDistanceFromBase.HasValue && distanceFromBase > filter.MaxDistanceFromBase.Value)
                        return false;
                }
            }

            // Destination market/planet filtering
            if (filter.DestinationMarketId.HasValue || filter.DestinationPlanetId.HasValue)
            {
                var destinationMarket = GetDestinationMarketForFilter(filter);
                if (destinationMarket != null)
                {
                    var distanceToDestination = Math.Min(
                        marketDataService.CalculateDistance(buyMarket.MarketId, destinationMarket.MarketId),
                        marketDataService.CalculateDistance(sellMarket.MarketId, destinationMarket.MarketId)
                    );

                    if (filter.MaxDistanceToDestination.HasValue && distanceToDestination > filter.MaxDistanceToDestination.Value)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Enhance opportunity with route-specific metrics
        /// </summary>
        private void EnhanceOpportunityWithRouteMetrics(ProfitOpportunity opportunity, ProfitFilter filter)
        {
            var markets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);
            
            var buyMarket = markets.TryGetValue(opportunity.SellOrder.MarketId, out var bm) ? bm : null;
            var sellMarket = markets.TryGetValue(opportunity.BuyOrder.MarketId, out var sm) ? sm : null;

            if (buyMarket == null || sellMarket == null)
                return;

            // Calculate route efficiency score
            var baseMarket = GetBaseMarketForFilter(filter);
            var destinationMarket = GetDestinationMarketForFilter(filter);

            if (baseMarket != null || destinationMarket != null)
            {
                var routeDistance = CalculateRouteDistance(baseMarket, buyMarket, sellMarket, destinationMarket);
                var routeEfficiency = routeDistance > 0 ? opportunity.TotalProfit / routeDistance : 0;
                
                // Update the opportunity's efficiency score with route considerations
                opportunity.Distance = routeDistance;
                opportunity.ProfitPerKm = routeEfficiency;
            }
        }

        /// <summary>
        /// Calculate total route distance considering base and destination
        /// </summary>
        private double CalculateRouteDistance(MarketData? baseMarket, MarketData buyMarket, MarketData sellMarket, MarketData? destinationMarket)
        {
            var totalDistance = 0.0;

            // Distance from base to first market (buy market)
            if (baseMarket != null)
            {
                totalDistance += marketDataService.CalculateDistance(baseMarket.MarketId, buyMarket.MarketId);
            }

            // Distance between buy and sell markets
            totalDistance += marketDataService.CalculateDistance(buyMarket.MarketId, sellMarket.MarketId);

            // Distance from sell market to destination
            if (destinationMarket != null)
            {
                totalDistance += marketDataService.CalculateDistance(sellMarket.MarketId, destinationMarket.MarketId);
            }

            return totalDistance;
        }

        /// <summary>
        /// Get base market for filtering
        /// </summary>
        private MarketData? GetBaseMarketForFilter(ProfitFilter filter)
        {
            if (filter.BaseMarketId.HasValue)
            {
                return marketDataService.GetAllMarkets().FirstOrDefault(m => m.MarketId == filter.BaseMarketId.Value);
            }

            if (filter.BasePlanetId.HasValue)
            {
                return marketDataService.GetAllMarkets().FirstOrDefault(m => m.PlanetId == filter.BasePlanetId.Value);
            }

            // Use current base market if no specific base is provided
            return baseMarketService.GetBaseMarket();
        }

        /// <summary>
        /// Get destination market for filtering
        /// </summary>
        private MarketData? GetDestinationMarketForFilter(ProfitFilter filter)
        {
            if (filter.DestinationMarketId.HasValue)
            {
                return marketDataService.GetAllMarkets().FirstOrDefault(m => m.MarketId == filter.DestinationMarketId.Value);
            }

            if (filter.DestinationPlanetId.HasValue)
            {
                return marketDataService.GetAllMarkets().FirstOrDefault(m => m.PlanetId == filter.DestinationPlanetId.Value);
            }

            return null;
        }

        /// <summary>
        /// Generate optimized trading routes with multiple stops between base and destination
        /// </summary>
        public List<TradingRoute> GenerateOptimizedRoutes(ProfitFilter filter, int maxRoutes = 5, int maxStops = 3)
        {
            var baseMarket = GetBaseMarketForFilter(filter);
            var destinationMarket = GetDestinationMarketForFilter(filter);

            if (baseMarket == null)
            {
                logger.LogWarning("No base market specified for route optimization");
                return new List<TradingRoute>();
            }

            var routes = new List<TradingRoute>();

            if (destinationMarket != null)
            {
                // Generate routes between specific base and destination
                routes.AddRange(GenerateBaseToDestinationRoutes(baseMarket, destinationMarket, filter, maxRoutes, maxStops));
            }
            else
            {
                // Generate routes from base to various destinations
                routes.AddRange(GenerateRoutesFromBase(baseMarket, filter, maxRoutes, maxStops));
            }

            return routes.OrderByDescending(r => r.ProfitPerKm).Take(maxRoutes).ToList();
        }

        /// <summary>
        /// Generate routes between specific base and destination markets
        /// </summary>
        private List<TradingRoute> GenerateBaseToDestinationRoutes(MarketData baseMarket, MarketData destinationMarket, ProfitFilter filter, int maxRoutes, int maxStops)
        {
            var routes = new List<TradingRoute>();
            var allMarkets = marketDataService.GetAllMarkets();
            var allOrders = marketDataService.GetAllOrders();

            // Find intermediate markets between base and destination
            var intermediateMarkets = FindIntermediateMarkets(baseMarket, destinationMarket, allMarkets, maxStops - 1);

            // Generate direct route (base -> destination)
            var directRoute = BuildDirectRoute(baseMarket, destinationMarket, filter);
            if (directRoute != null && directRoute.TotalProfit > 0)
            {
                routes.Add(directRoute);
            }

            // Generate routes with stopovers
            foreach (var stopoverCombination in GetStopoverCombinations(intermediateMarkets, maxStops - 1))
            {
                var route = BuildMultiStopRoute(baseMarket, stopoverCombination, destinationMarket, filter);
                if (route != null && route.TotalProfit > 0)
                {
                    routes.Add(route);
                }
            }

            return routes;
        }

        /// <summary>
        /// Generate routes from base market to various destinations
        /// </summary>
        private List<TradingRoute> GenerateRoutesFromBase(MarketData baseMarket, ProfitFilter filter, int maxRoutes, int maxStops)
        {
            var routes = new List<TradingRoute>();
            var opportunities = GetOpportunitiesFromBaseMarket(baseMarket, null, filter);

            // Group by destination market
            var destinationGroups = opportunities
                .GroupBy(o => o.BuyOrder.MarketId)
                .OrderByDescending(g => g.Sum(o => o.TotalProfit))
                .Take(maxRoutes * 2); // Get more candidates

            foreach (var destGroup in destinationGroups)
            {
                var destMarket = marketDataService.GetAllMarkets().FirstOrDefault(m => m.MarketId == destGroup.Key);
                if (destMarket == null) continue;

                var route = BuildOptimalRoute(destGroup.ToList(), baseMarket, destMarket, filter, maxStops);
                if (route != null && route.TotalProfit > 0)
                {
                    routes.Add(route);
                }
            }

            return routes;
        }

        /// <summary>
        /// Build a direct route between two markets
        /// </summary>
        private TradingRoute? BuildDirectRoute(MarketData baseMarket, MarketData destinationMarket, ProfitFilter filter)
        {
            var opportunities = GetOpportunitiesFromBaseMarket(baseMarket, destinationMarket, filter);
            if (!opportunities.Any()) return null;

            var route = new TradingRoute
            {
                Markets = new List<MarketData> { baseMarket, destinationMarket },
                Opportunities = opportunities.OrderByDescending(o => o.TotalProfit).Take(5).ToList()
            };

            route.TotalProfit = route.Opportunities.Sum(o => o.TotalProfit);
            route.TotalDistance = marketDataService.CalculateDistance(baseMarket.MarketId, destinationMarket.MarketId);
            route.ProfitPerKm = route.TotalDistance > 0 ? route.TotalProfit / route.TotalDistance : 0;

            return route;
        }

        /// <summary>
        /// Find intermediate markets that could serve as profitable stopovers
        /// </summary>
        private List<MarketData> FindIntermediateMarkets(MarketData baseMarket, MarketData destinationMarket, List<MarketData> allMarkets, int maxIntermediateStops)
        {
            var directDistance = marketDataService.CalculateDistance(baseMarket.MarketId, destinationMarket.MarketId);
            var maxDetourDistance = directDistance * 1.5; // Allow 50% detour

            return allMarkets
                .Where(m => m.MarketId != baseMarket.MarketId && m.MarketId != destinationMarket.MarketId)
                .Where(m => 
                {
                    var distanceFromBase = marketDataService.CalculateDistance(baseMarket.MarketId, m.MarketId);
                    var distanceToDest = marketDataService.CalculateDistance(m.MarketId, destinationMarket.MarketId);
                    var totalDistance = distanceFromBase + distanceToDest;
                    return totalDistance <= maxDetourDistance;
                })
                .OrderBy(m => 
                {
                    var distanceFromBase = marketDataService.CalculateDistance(baseMarket.MarketId, m.MarketId);
                    var distanceToDest = marketDataService.CalculateDistance(m.MarketId, destinationMarket.MarketId);
                    return distanceFromBase + distanceToDest;
                })
                .Take(10) // Limit candidates for performance
                .ToList();
        }

        /// <summary>
        /// Get combinations of stopovers
        /// </summary>
        private IEnumerable<List<MarketData>> GetStopoverCombinations(List<MarketData> intermediateMarkets, int maxStops)
        {
            // Return single stopovers
            foreach (var market in intermediateMarkets.Take(5))
            {
                yield return new List<MarketData> { market };
            }

            // Return two-stop combinations if allowed
            if (maxStops >= 2)
            {
                for (int i = 0; i < Math.Min(intermediateMarkets.Count, 3); i++)
                {
                    for (int j = i + 1; j < Math.Min(intermediateMarkets.Count, 3); j++)
                    {
                        yield return new List<MarketData> { intermediateMarkets[i], intermediateMarkets[j] };
                    }
                }
            }
        }

        /// <summary>
        /// Build a multi-stop route
        /// </summary>
        private TradingRoute? BuildMultiStopRoute(MarketData baseMarket, List<MarketData> stopovers, MarketData destinationMarket, ProfitFilter filter)
        {
            var route = new TradingRoute();
            var allMarkets = new List<MarketData> { baseMarket };
            allMarkets.AddRange(stopovers);
            allMarkets.Add(destinationMarket);

            route.Markets = allMarkets;
            var totalDistance = 0.0;
            var totalProfit = 0L;

            // Calculate opportunities and distances for each leg
            for (int i = 0; i < allMarkets.Count - 1; i++)
            {
                var fromMarket = allMarkets[i];
                var toMarket = allMarkets[i + 1];

                var legOpportunities = GetOpportunitiesFromBaseMarket(fromMarket, toMarket, filter);
                route.Opportunities.AddRange(legOpportunities.Take(2)); // Limit opportunities per leg

                var legDistance = marketDataService.CalculateDistance(fromMarket.MarketId, toMarket.MarketId);
                totalDistance += legDistance;
                totalProfit += legOpportunities.Sum(o => o.TotalProfit);
            }

            route.TotalDistance = totalDistance;
            route.TotalProfit = totalProfit;
            route.ProfitPerKm = totalDistance > 0 ? totalProfit / totalDistance : 0;

            return route.TotalProfit > 0 ? route : null;
        }

        /// <summary>
        /// Build an optimal route from a set of opportunities between specific markets
        /// </summary>
        private TradingRoute? BuildOptimalRoute(List<ProfitOpportunity> opportunities, MarketData baseMarket, MarketData destinationMarket, ProfitFilter filter, int maxStops)
        {
            if (!opportunities.Any())
                return null;

            var route = new TradingRoute();
            var currentPosition = baseMarket;
            var remainingOpportunities = opportunities.ToList();
            var totalDistance = 0.0;
            var totalProfit = 0L;

            // Add base market to route
            route.Markets.Add(baseMarket);

            // Greedy algorithm: always pick the closest profitable opportunity
            for (int stop = 0; stop < maxStops && remainingOpportunities.Any(); stop++)
            {
                var bestOpportunity = FindBestNextOpportunity(currentPosition, remainingOpportunities);
                if (bestOpportunity == null)
                    break;

                route.Opportunities.Add(bestOpportunity);
                remainingOpportunities.Remove(bestOpportunity);

                // Add markets to route
                var markets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);
                var buyMarket = markets.TryGetValue(bestOpportunity.SellOrder.MarketId, out var bm) ? bm : null;
                var sellMarket = markets.TryGetValue(bestOpportunity.BuyOrder.MarketId, out var sm) ? sm : null;

                if (buyMarket != null && !route.Markets.Any(m => m.MarketId == buyMarket.MarketId))
                {
                    route.Markets.Add(buyMarket);
                }
                if (sellMarket != null && !route.Markets.Any(m => m.MarketId == sellMarket.MarketId))
                {
                    route.Markets.Add(sellMarket);
                }

                // Update totals
                totalProfit += bestOpportunity.TotalProfit;
                if (currentPosition != null && buyMarket != null)
                {
                    totalDistance += marketDataService.CalculateDistance(currentPosition.MarketId, buyMarket.MarketId);
                }
                if (buyMarket != null && sellMarket != null)
                {
                    totalDistance += marketDataService.CalculateDistance(buyMarket.MarketId, sellMarket.MarketId);
                }

                currentPosition = sellMarket;
            }

            // Add distance to destination if not already in route
            if (!route.Markets.Any(m => m.MarketId == destinationMarket.MarketId))
            {
                route.Markets.Add(destinationMarket);
                if (currentPosition != null)
                {
                    totalDistance += marketDataService.CalculateDistance(currentPosition.MarketId, destinationMarket.MarketId);
                }
            }

            route.TotalDistance = totalDistance;
            route.TotalProfit = totalProfit;
            route.ProfitPerKm = totalDistance > 0 ? totalProfit / totalDistance : 0;

            return route;
        }

        /// <summary>
        /// Find the best next opportunity considering distance and profit
        /// </summary>
        private ProfitOpportunity? FindBestNextOpportunity(MarketData? currentPosition, List<ProfitOpportunity> opportunities)
        {
            if (currentPosition == null || !opportunities.Any())
                return opportunities.OrderByDescending(o => o.TotalProfit).FirstOrDefault();

            var markets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);

            return opportunities
                .Select(o => new
                {
                    Opportunity = o,
                    BuyMarket = markets.TryGetValue(o.SellOrder.MarketId, out var bm) ? bm : null,
                    Distance = markets.TryGetValue(o.SellOrder.MarketId, out var bm2) ? 
                        marketDataService.CalculateDistance(currentPosition.MarketId, bm2.MarketId) : double.MaxValue
                })
                .Where(x => x.BuyMarket != null)
                .OrderByDescending(x => x.Opportunity.TotalProfit / Math.Max(x.Distance, 1)) // Profit per distance
                .FirstOrDefault()?.Opportunity;
        }
    }

}