using System;
using System.Threading.Tasks;
using MarketBrowserMod.Models;
using Microsoft.Extensions.Logging;
using NQ;

namespace MarketBrowserMod.Services
{
    /// <summary>
    /// Service for managing the selected base market for distance calculations
    /// </summary>
    public class BaseMarketService
    {
        private readonly ILogger<BaseMarketService> logger;
        private MarketData? baseMarket;
        private readonly object lockObject = new object();

        public BaseMarketService(ILogger<BaseMarketService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get the currently selected base market
        /// </summary>
        public MarketData? GetBaseMarket()
        {
            lock (lockObject)
            {
                return baseMarket;
            }
        }

        /// <summary>
        /// Set the base market for distance calculations
        /// </summary>
        /// <param name="market">The market to use as base, or null to clear</param>
        public void SetBaseMarket(MarketData? market)
        {
            lock (lockObject)
            {
                baseMarket = market;
                if (market != null)
                {
                    logger.LogInformation($"Base market set to: {market.Name} on {market.PlanetName} (ID: {market.MarketId})");
                }
                else
                {
                    logger.LogInformation("Base market cleared");
                }
            }
        }

        /// <summary>
        /// Calculate distance from the base market to another market
        /// </summary>
        /// <param name="targetMarket">The target market</param>
        /// <param name="marketDataService">Market data service for distance calculations</param>
        /// <returns>Distance in meters, or null if no base market is set</returns>
        public double? CalculateDistanceFromBase(MarketData targetMarket, MarketDataService marketDataService)
        {
            var baseMarketData = GetBaseMarket();
            if (baseMarketData == null)
                return null;

            if (baseMarketData.MarketId == targetMarket.MarketId)
                return 0.0; // Same market

            return marketDataService.CalculateDistance(baseMarketData.MarketId, targetMarket.MarketId);
        }

        /// <summary>
        /// Calculate distance from the base market to a specific position
        /// </summary>
        /// <param name="targetPosition">The target position</param>
        /// <param name="marketDataService">Market data service for distance calculations</param>
        /// <returns>Distance in meters, or null if no base market is set</returns>
        public double? CalculateDistanceFromBase(Vec3 targetPosition, MarketDataService marketDataService)
        {
            var baseMarketData = GetBaseMarket();
            if (baseMarketData?.Position == null)
                return null;

            return marketDataService.CalculateDistance(baseMarketData.Position, targetPosition);
        }

        /// <summary>
        /// Get base market information for API responses
        /// </summary>
        public BaseMarketInfo? GetBaseMarketInfo()
        {
            var market = GetBaseMarket();
            if (market == null)
                return null;

            return new BaseMarketInfo
            {
                MarketId = market.MarketId,
                Name = market.Name,
                PlanetName = market.PlanetName,
                PlanetId = market.PlanetId,
                Position = market.Position,
                LastUpdated = market.LastUpdated
            };
        }
    }

    /// <summary>
    /// Information about the selected base market
    /// </summary>
    public class BaseMarketInfo
    {
        public ulong MarketId { get; set; }
        public string Name { get; set; } = "";
        public string PlanetName { get; set; } = "";
        public ulong PlanetId { get; set; }
        public Vec3? Position { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}