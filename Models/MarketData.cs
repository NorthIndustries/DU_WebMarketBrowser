using System;
using System.Collections.Generic;
using System.Linq;
using NQ;

namespace MarketBrowserMod.Models
{
    /// <summary>
    /// Core data model representing a market with its location and orders
    /// </summary>
    public class MarketData
    {
        public ulong MarketId { get; set; }
        public string Name { get; set; } = "";
        public ulong ConstructId { get; set; }
        public Vec3? Position { get; set; }
        public ulong PlanetId { get; set; }
        public string PlanetName { get; set; } = "";
        public DateTime LastUpdated { get; set; }
        public List<OrderData> Orders { get; set; } = new();
        public double DistanceFromOrigin { get; set; }
    }

    /// <summary>
    /// Core data model representing a market order with player and timing information
    /// </summary>
    public class OrderData
    {
        public ulong OrderId { get; set; }
        public ulong MarketId { get; set; }
        public string MarketName { get; set; } = "";
        public ulong ItemType { get; set; }
        public string ItemName { get; set; } = "";
        public long BuyQuantity { get; set; }
        public long SellQuantity { get; set; }
        public Currency UnitPrice { get; set; }
        public ulong PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public DateTime ExpirationDate { get; set; }
        public DateTime LastUpdated { get; set; }
        public double DistanceFromOrigin { get; set; }
        
        /// <summary>
        /// Indicates if this is a buy order (BuyQuantity > 0) or sell order (SellQuantity > 0)
        /// </summary>
        public bool IsBuyOrder => BuyQuantity > 0;
        
        /// <summary>
        /// Gets the effective quantity for this order
        /// </summary>
        public long Quantity => IsBuyOrder ? BuyQuantity : SellQuantity;
    }

    /// <summary>
    /// Data model representing planet information for location context
    /// </summary>
    public class PlanetData
    {
        public ulong PlanetId { get; set; }
        public string Name { get; set; } = "";
        public Vec3? Position { get; set; }
        public double DistanceFromOrigin { get; set; }
    }

    /// <summary>
    /// Data model representing a profit opportunity between buy and sell orders
    /// Requirements 3.1, 3.2, 3.3: Profit calculation with comprehensive metrics
    /// </summary>
    public class ProfitOpportunity
    {
        public string ItemName { get; set; } = "";
        public ulong ItemType { get; set; }
        public OrderData BuyOrder { get; set; } = new();
        public OrderData SellOrder { get; set; } = new();
        public long ProfitPerUnit { get; set; }
        public double ProfitMargin { get; set; }
        public long MaxQuantity { get; set; }
        public long TotalProfit { get; set; }
        public double Distance { get; set; }
        public double ProfitPerKm { get; set; }
        
        /// <summary>
        /// Investment required to execute this trade (buy order cost)
        /// </summary>
        public long InvestmentRequired => SellOrder?.UnitPrice != null ? SellOrder.UnitPrice.amount * MaxQuantity : 0;
        
        /// <summary>
        /// Return on investment percentage
        /// </summary>
        public double ROI => InvestmentRequired > 0 ? (double)TotalProfit / InvestmentRequired * 100 : 0;
        
        /// <summary>
        /// Profit efficiency score combining profit, distance, and volume
        /// </summary>
        public double EfficiencyScore => Distance > 0 && MaxQuantity > 0 
            ? (TotalProfit / Distance) * Math.Log(MaxQuantity + 1) 
            : 0;
        
        /// <summary>
        /// Risk assessment based on order expiration times
        /// </summary>
        public string RiskLevel
        {
            get
            {
                var now = DateTime.UtcNow;
                var minExpiration = new[] { BuyOrder.ExpirationDate, SellOrder.ExpirationDate }.Min();
                var timeToExpiration = minExpiration - now;
                
                return timeToExpiration.TotalHours switch
                {
                    < 1 => "High",
                    < 6 => "Medium",
                    < 24 => "Low",
                    _ => "Very Low"
                };
            }
        }
        
        /// <summary>
        /// Calculates comprehensive profit metrics based on buy and sell orders
        /// Requirements 3.2, 3.3: Profit margin, total profit, and volume calculations
        /// </summary>
        public void CalculateProfitMetrics()
        {
            if (BuyOrder?.UnitPrice != null && SellOrder?.UnitPrice != null)
            {
                // Requirement 3.2: Compute profit per unit and profit margin percentage
                ProfitPerUnit = BuyOrder.UnitPrice.amount - SellOrder.UnitPrice.amount;
                
                // Requirement 3.3: Use the minimum of available buy and sell quantities
                MaxQuantity = Math.Min(BuyOrder.Quantity, SellOrder.Quantity);
                TotalProfit = ProfitPerUnit * MaxQuantity;
                
                // Calculate profit margin based on sell price (cost basis)
                if (SellOrder.UnitPrice.amount > 0)
                {
                    ProfitMargin = (double)ProfitPerUnit / SellOrder.UnitPrice.amount * 100;
                }
                
                // Distance-based profit efficiency metrics (profit per kilometer)
                if (Distance > 0)
                {
                    ProfitPerKm = (double)TotalProfit / Distance;
                }
            }
        }
        
        /// <summary>
        /// Validates that this is a profitable opportunity
        /// </summary>
        public bool IsValid => ProfitPerUnit > 0 && MaxQuantity > 0 && TotalProfit > 0;
        
        /// <summary>
        /// Gets a summary description of this opportunity
        /// </summary>
        public string GetSummary()
        {
            return $"{ItemName}: Buy at {SellOrder.MarketName} for {SellOrder.UnitPrice.amount:N0}, " +
                   $"sell at {BuyOrder.MarketName} for {BuyOrder.UnitPrice.amount:N0}, " +
                   $"profit {ProfitPerUnit:N0}/unit ({ProfitMargin:F1}%), " +
                   $"max {MaxQuantity:N0} units, total {TotalProfit:N0}, " +
                   $"distance {Distance:F0}km, efficiency {ProfitPerKm:F2}/km";
        }
    }

    /// <summary>
    /// Data model representing a trading route with multiple markets and opportunities
    /// </summary>
    public class TradeRoute
    {
        public List<MarketData> Markets { get; set; } = new();
        public List<ProfitOpportunity> Opportunities { get; set; } = new();
        public double TotalDistance { get; set; }
        public long TotalProfit { get; set; }
        public double ProfitPerKm { get; set; }
    }

    /// <summary>
    /// Data model representing cache statistics for monitoring and debugging
    /// </summary>
    public class CacheStatistics
    {
        public int MarketCount { get; set; }
        public int OrderCount { get; set; }
        public int PlayerNameCount { get; set; }
        public int ItemNameCount { get; set; }
        public DateTime LastSuccessfulRefresh { get; set; }
        public DateTime LastRefreshAttempt { get; set; }
        public TimeSpan CacheAge { get; set; }
        public bool IsStale { get; set; }
        public bool IsRefreshing { get; set; }
        public int ConsecutiveFailures { get; set; }
        public bool OrleansAvailable { get; set; }
    }
}