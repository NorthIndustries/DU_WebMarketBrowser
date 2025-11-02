using System;
using System.Collections.Generic;
using System.Linq;
using MarketBrowserMod.Models;
using Microsoft.Extensions.Logging;

namespace MarketBrowserMod.Services
{
    /// <summary>
    /// Advanced profit analysis service providing comprehensive trading analytics
    /// Requirements 3.1, 3.2, 3.3, 3.5, 3.6: Advanced profit opportunity analysis
    /// </summary>
    public class ProfitAnalysisService
    {
        private readonly MarketDataService marketDataService;
        private readonly ILogger<ProfitAnalysisService> logger;

        public ProfitAnalysisService(MarketDataService marketDataService, ILogger<ProfitAnalysisService> logger)
        {
            this.marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Analyze profit opportunities with advanced metrics and insights
        /// Requirements 3.1, 3.2, 3.3: Comprehensive profit analysis
        /// </summary>
        public ProfitAnalysisResult AnalyzeProfitOpportunities(ProfitFilter? filter = null)
        {
            var opportunities = marketDataService.FindProfitOpportunities(filter);
            
            if (!opportunities.Any())
            {
                return new ProfitAnalysisResult
                {
                    TotalOpportunities = 0,
                    Message = "No profit opportunities found with the specified criteria."
                };
            }

            var result = new ProfitAnalysisResult
            {
                TotalOpportunities = opportunities.Count,
                TotalPotentialProfit = opportunities.Sum(o => o.TotalProfit),
                AverageProfitMargin = opportunities.Average(o => o.ProfitMargin),
                AverageDistance = opportunities.Where(o => o.Distance > 0).DefaultIfEmpty().Average(o => o?.Distance ?? 0),
                AverageProfitPerKm = opportunities.Where(o => o.ProfitPerKm > 0).DefaultIfEmpty().Average(o => o?.ProfitPerKm ?? 0),
                
                // Top opportunities by different metrics
                TopByTotalProfit = opportunities.OrderByDescending(o => o.TotalProfit).Take(5).ToList(),
                TopByProfitMargin = opportunities.OrderByDescending(o => o.ProfitMargin).Take(5).ToList(),
                TopByEfficiency = opportunities.Where(o => o.Distance > 0).OrderByDescending(o => o.ProfitPerKm).Take(5).ToList(),
                
                // Market analysis
                MarketAnalysis = AnalyzeMarketDistribution(opportunities),
                ItemAnalysis = AnalyzeItemDistribution(opportunities),
                DistanceAnalysis = AnalyzeDistanceDistribution(opportunities),
                
                LastUpdated = DateTime.UtcNow
            };

            return result;
        }

        /// <summary>
        /// Analyze market distribution in profit opportunities
        /// Requirement 3.5: Market-based analysis for profit opportunities
        /// </summary>
        private List<MarketProfitAnalysis> AnalyzeMarketDistribution(List<ProfitOpportunity> opportunities)
        {
            var marketStats = new Dictionary<string, MarketProfitStats>();
            var allMarkets = marketDataService.GetAllMarkets().ToDictionary(m => m.MarketId, m => m);

            foreach (var opportunity in opportunities)
            {
                // Get market data for planet names
                var buyMarket = allMarkets.TryGetValue(opportunity.BuyOrder.MarketId, out var bm) ? bm : null;
                var sellMarket = allMarkets.TryGetValue(opportunity.SellOrder.MarketId, out var sm) ? sm : null;

                // Analyze buy markets (where you sell)
                var buyMarketKey = buyMarket != null 
                    ? $"{opportunity.BuyOrder.MarketName} ({buyMarket.PlanetName})"
                    : opportunity.BuyOrder.MarketName;
                    
                if (!marketStats.ContainsKey(buyMarketKey))
                {
                    marketStats[buyMarketKey] = new MarketProfitStats { MarketName = buyMarketKey };
                }
                marketStats[buyMarketKey].BuyOpportunities++;
                marketStats[buyMarketKey].TotalBuyProfit += opportunity.TotalProfit;

                // Analyze sell markets (where you buy)
                var sellMarketKey = sellMarket != null 
                    ? $"{opportunity.SellOrder.MarketName} ({sellMarket.PlanetName})"
                    : opportunity.SellOrder.MarketName;
                    
                if (!marketStats.ContainsKey(sellMarketKey))
                {
                    marketStats[sellMarketKey] = new MarketProfitStats { MarketName = sellMarketKey };
                }
                marketStats[sellMarketKey].SellOpportunities++;
                marketStats[sellMarketKey].TotalSellProfit += opportunity.TotalProfit;
            }

            return marketStats.Values
                .Select(stats => new MarketProfitAnalysis
                {
                    MarketName = stats.MarketName,
                    TotalOpportunities = stats.BuyOpportunities + stats.SellOpportunities,
                    BuyOpportunities = stats.BuyOpportunities,
                    SellOpportunities = stats.SellOpportunities,
                    TotalPotentialProfit = stats.TotalBuyProfit + stats.TotalSellProfit,
                    AverageProfit = stats.BuyOpportunities + stats.SellOpportunities > 0 
                        ? (stats.TotalBuyProfit + stats.TotalSellProfit) / (stats.BuyOpportunities + stats.SellOpportunities)
                        : 0
                })
                .OrderByDescending(m => m.TotalPotentialProfit)
                .Take(10)
                .ToList();
        }

        /// <summary>
        /// Analyze item distribution in profit opportunities
        /// Requirement 3.5: Item-based analysis for profit opportunities
        /// </summary>
        private List<ItemProfitAnalysis> AnalyzeItemDistribution(List<ProfitOpportunity> opportunities)
        {
            return opportunities
                .GroupBy(o => o.ItemName)
                .Select(g => new ItemProfitAnalysis
                {
                    ItemName = g.Key,
                    OpportunityCount = g.Count(),
                    TotalPotentialProfit = g.Sum(o => o.TotalProfit),
                    AverageProfitMargin = g.Average(o => o.ProfitMargin),
                    AverageDistance = g.Where(o => o.Distance > 0).DefaultIfEmpty().Average(o => o?.Distance ?? 0),
                    BestOpportunity = g.OrderByDescending(o => o.TotalProfit).First(),
                    TotalVolume = g.Sum(o => o.MaxQuantity)
                })
                .OrderByDescending(i => i.TotalPotentialProfit)
                .Take(20)
                .ToList();
        }

        /// <summary>
        /// Analyze distance distribution in profit opportunities
        /// Requirement 3.6: Distance-based analysis for route planning
        /// </summary>
        private DistanceProfitAnalysis AnalyzeDistanceDistribution(List<ProfitOpportunity> opportunities)
        {
            var validDistances = opportunities.Where(o => o.Distance > 0).ToList();
            
            if (!validDistances.Any())
            {
                return new DistanceProfitAnalysis();
            }

            var distances = validDistances.Select(o => o.Distance).ToList();
            var profitPerKm = validDistances.Select(o => o.ProfitPerKm).ToList();

            return new DistanceProfitAnalysis
            {
                ShortRange = AnalyzeDistanceRange(validDistances.Where(o => o.Distance <= 1000000), "Short Range (≤1M km)"),
                MediumRange = AnalyzeDistanceRange(validDistances.Where(o => o.Distance > 1000000 && o.Distance <= 5000000), "Medium Range (1M-5M km)"),
                LongRange = AnalyzeDistanceRange(validDistances.Where(o => o.Distance > 5000000), "Long Range (>5M km)"),
                
                MinDistance = distances.Min(),
                MaxDistance = distances.Max(),
                AverageDistance = distances.Average(),
                MedianDistance = GetMedian(distances),
                
                MinProfitPerKm = profitPerKm.Min(),
                MaxProfitPerKm = profitPerKm.Max(),
                AverageProfitPerKm = profitPerKm.Average(),
                MedianProfitPerKm = GetMedian(profitPerKm)
            };
        }

        /// <summary>
        /// Analyze opportunities within a specific distance range
        /// </summary>
        private DistanceRangeAnalysis AnalyzeDistanceRange(IEnumerable<ProfitOpportunity> opportunities, string rangeName)
        {
            var opps = opportunities.ToList();
            
            if (!opps.Any())
            {
                return new DistanceRangeAnalysis { RangeName = rangeName, Count = 0 };
            }

            return new DistanceRangeAnalysis
            {
                RangeName = rangeName,
                Count = opps.Count,
                TotalProfit = opps.Sum(o => o.TotalProfit),
                AverageProfit = opps.Average(o => o.TotalProfit),
                AverageProfitMargin = opps.Average(o => o.ProfitMargin),
                AverageProfitPerKm = opps.Average(o => o.ProfitPerKm),
                BestOpportunity = opps.OrderByDescending(o => o.ProfitPerKm).First()
            };
        }

        /// <summary>
        /// Calculate median value from a list of doubles
        /// </summary>
        private double GetMedian(List<double> values)
        {
            if (!values.Any()) return 0;
            
            var sorted = values.OrderBy(x => x).ToList();
            var count = sorted.Count;
            
            if (count % 2 == 0)
            {
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
            }
            else
            {
                return sorted[count / 2];
            }
        }

        /// <summary>
        /// Generate trading route recommendations based on profit opportunities
        /// Requirements 3.5, 3.6: Route planning and optimization
        /// </summary>
        public List<TradingRoute> GenerateTradingRoutes(int maxRoutes = 5, double maxTotalDistance = 10000000)
        {
            var opportunities = marketDataService.FindProfitOpportunities();
            var routes = new List<TradingRoute>();

            // Group opportunities by item to create multi-hop routes
            var itemGroups = opportunities.GroupBy(o => o.ItemName);

            foreach (var itemGroup in itemGroups.Take(maxRoutes))
            {
                var itemOpportunities = itemGroup.OrderByDescending(o => o.ProfitPerKm).ToList();
                
                if (itemOpportunities.Any())
                {
                    var route = new TradingRoute
                    {
                        ItemName = itemGroup.Key,
                        Opportunities = itemOpportunities.Take(3).ToList(), // Max 3 hops
                        TotalDistance = itemOpportunities.Take(3).Sum(o => o.Distance),
                        TotalProfit = itemOpportunities.Take(3).Sum(o => o.TotalProfit),
                        EstimatedTime = CalculateEstimatedTravelTime(itemOpportunities.Take(3).Sum(o => o.Distance))
                    };

                    if (route.TotalDistance <= maxTotalDistance)
                    {
                        route.ProfitPerKm = route.TotalDistance > 0 ? route.TotalProfit / route.TotalDistance : 0;
                        routes.Add(route);
                    }
                }
            }

            return routes.OrderByDescending(r => r.ProfitPerKm).ToList();
        }

        /// <summary>
        /// Calculate estimated travel time based on distance
        /// Assumes average travel speed for route planning
        /// </summary>
        private TimeSpan CalculateEstimatedTravelTime(double totalDistance)
        {
            // Assume average speed of 30,000 km/h (typical for space travel in DU)
            var averageSpeed = 30000.0; // km/h
            var hours = totalDistance / averageSpeed;
            return TimeSpan.FromHours(hours);
        }
    }

    // Supporting classes for profit analysis results

    public class MarketProfitStats
    {
        public string MarketName { get; set; } = "";
        public int BuyOpportunities { get; set; }
        public int SellOpportunities { get; set; }
        public long TotalBuyProfit { get; set; }
        public long TotalSellProfit { get; set; }
    }

    public class ProfitAnalysisResult
    {
        public int TotalOpportunities { get; set; }
        public long TotalPotentialProfit { get; set; }
        public double AverageProfitMargin { get; set; }
        public double AverageDistance { get; set; }
        public double AverageProfitPerKm { get; set; }
        public List<ProfitOpportunity> TopByTotalProfit { get; set; } = new();
        public List<ProfitOpportunity> TopByProfitMargin { get; set; } = new();
        public List<ProfitOpportunity> TopByEfficiency { get; set; } = new();
        public List<MarketProfitAnalysis> MarketAnalysis { get; set; } = new();
        public List<ItemProfitAnalysis> ItemAnalysis { get; set; } = new();
        public DistanceProfitAnalysis DistanceAnalysis { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public string Message { get; set; } = "";
    }

    public class MarketProfitAnalysis
    {
        public string MarketName { get; set; } = "";
        public int TotalOpportunities { get; set; }
        public int BuyOpportunities { get; set; }
        public int SellOpportunities { get; set; }
        public long TotalPotentialProfit { get; set; }
        public double AverageProfit { get; set; }
    }

    public class ItemProfitAnalysis
    {
        public string ItemName { get; set; } = "";
        public int OpportunityCount { get; set; }
        public long TotalPotentialProfit { get; set; }
        public double AverageProfitMargin { get; set; }
        public double AverageDistance { get; set; }
        public ProfitOpportunity BestOpportunity { get; set; } = new();
        public long TotalVolume { get; set; }
    }

    public class DistanceProfitAnalysis
    {
        public DistanceRangeAnalysis ShortRange { get; set; } = new();
        public DistanceRangeAnalysis MediumRange { get; set; } = new();
        public DistanceRangeAnalysis LongRange { get; set; } = new();
        public double MinDistance { get; set; }
        public double MaxDistance { get; set; }
        public double AverageDistance { get; set; }
        public double MedianDistance { get; set; }
        public double MinProfitPerKm { get; set; }
        public double MaxProfitPerKm { get; set; }
        public double AverageProfitPerKm { get; set; }
        public double MedianProfitPerKm { get; set; }
    }

    public class DistanceRangeAnalysis
    {
        public string RangeName { get; set; } = "";
        public int Count { get; set; }
        public long TotalProfit { get; set; }
        public double AverageProfit { get; set; }
        public double AverageProfitMargin { get; set; }
        public double AverageProfitPerKm { get; set; }
        public ProfitOpportunity BestOpportunity { get; set; } = new();
    }

}