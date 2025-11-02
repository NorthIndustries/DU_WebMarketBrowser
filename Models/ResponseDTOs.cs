using System;
using System.Collections.Generic;
using NQ;
using MarketBrowserMod.Utils;

namespace MarketBrowserMod.Models
{
    /// <summary>
    /// Response DTO for market information in API endpoints
    /// </summary>
    public class MarketResponse
    {
        public ulong MarketId { get; set; }
        public string Name { get; set; } = "";
        public ulong ConstructId { get; set; }
        public Vec3? Position { get; set; }
        public ulong PlanetId { get; set; }
        public string PlanetName { get; set; } = "";
        public DateTime LastUpdated { get; set; }
        public int OrderCount { get; set; }
        public double DistanceFromOrigin { get; set; }
        public double? DistanceFromBase { get; set; }
        public string? DistanceFromBaseFormatted { get; set; }
        public string DistanceFromOriginFormatted { get; set; } = "";
    }

    /// <summary>
    /// Response DTO for order information in API endpoints
    /// </summary>
    public class OrderResponse
    {
        public ulong OrderId { get; set; }
        public ulong MarketId { get; set; }
        public string MarketName { get; set; } = "";
        public ulong ItemType { get; set; }
        public string ItemName { get; set; } = "";
        public long BuyQuantity { get; set; }
        public long SellQuantity { get; set; }
        public double UnitPrice { get; set; }
        public ulong PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public DateTime ExpirationDate { get; set; }
        public DateTime LastUpdated { get; set; }
        public double DistanceFromOrigin { get; set; }
        public double? DistanceFromBase { get; set; }
        public string? DistanceFromBaseFormatted { get; set; }
        public string DistanceFromOriginFormatted { get; set; } = "";
        public bool IsBuyOrder { get; set; }
        public long Quantity { get; set; }
        public string OrderType { get; set; } = "";
        public Vec3? MarketPosition { get; set; }
        public string PlanetName { get; set; } = "";
    }

    /// <summary>
    /// Response DTO for profit opportunity information in API endpoints
    /// </summary>
    public class ProfitOpportunityResponse
    {
        public string ItemName { get; set; } = "";
        public ulong ItemType { get; set; }
        public OrderSummary BuyOrder { get; set; } = new();
        public OrderSummary SellOrder { get; set; } = new();
        public double ProfitPerUnit { get; set; }
        public double ProfitMargin { get; set; }
        public long MaxQuantity { get; set; }
        public double TotalProfit { get; set; }
        public double Distance { get; set; }
        public string DistanceFormatted { get; set; } = "";
        public double ProfitPerKm { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Simplified order summary for profit opportunity responses
    /// </summary>
    public class OrderSummary
    {
        public ulong OrderId { get; set; }
        public ulong MarketId { get; set; }
        public string MarketName { get; set; } = "";
        public string PlanetName { get; set; } = "";
        public Vec3? Position { get; set; }
        public long Quantity { get; set; }
        public double UnitPrice { get; set; }
        public string PlayerName { get; set; } = "";
        public DateTime ExpirationDate { get; set; }
    }

    /// <summary>
    /// Response DTO for paginated results
    /// </summary>
    public class PagedResponse<T>
    {
        public List<T> Data { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Filter parameters for order queries
    /// </summary>
    public class OrderFilter
    {
        public string? ItemName { get; set; }
        public ulong? MarketId { get; set; }
        public string? MarketName { get; set; }
        public string? PlanetName { get; set; }
        public long? MinPrice { get; set; }
        public long? MaxPrice { get; set; }
        public string? OrderType { get; set; } // "buy", "sell", or null for both
        public string? PlayerName { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string? SortBy { get; set; } = "UnitPrice";
        public string? SortOrder { get; set; } = "asc"; // "asc" or "desc"
    }

    /// <summary>
    /// Filter parameters for profit opportunity queries
    /// </summary>
    public class ProfitFilter
    {
        public string? ItemName { get; set; }
        public double? MinProfitMargin { get; set; }
        public long? MinTotalProfit { get; set; }
        public double? MaxDistance { get; set; }
        
        // Route-based filtering
        public ulong? BaseMarketId { get; set; }
        public ulong? DestinationMarketId { get; set; }
        public ulong? BasePlanetId { get; set; }
        public ulong? DestinationPlanetId { get; set; }
        public double? MaxDistanceFromBase { get; set; }
        public double? MaxDistanceToDestination { get; set; }
        public bool RouteOptimization { get; set; } = false;
        
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string? SortBy { get; set; } = "TotalProfit";
        public string? SortOrder { get; set; } = "desc"; // "asc" or "desc"
    }

    /// <summary>
    /// Response DTO for planet information in API endpoints
    /// Requirement 4.4: Planet name mapping and coordinate display functionality
    /// </summary>
    public class PlanetResponse
    {
        public ulong PlanetId { get; set; }
        public string Name { get; set; } = "";
        public Vec3? Position { get; set; }
        public double DistanceFromOrigin { get; set; }
        public int MarketCount { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Response DTO for location-based queries
    /// Requirement 4.3, 4.6: Distance calculations and route planning
    /// </summary>
    public class LocationResponse
    {
        public Vec3? Position { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = ""; // "market", "planet", etc.
        public double DistanceFromOrigin { get; set; }
        public double? DistanceFromQuery { get; set; }
    }

    /// <summary>
    /// System status response for health checks
    /// </summary>
    public class SystemStatusResponse
    {
        public bool IsHealthy { get; set; }
        public DateTime LastDataRefresh { get; set; }
        public int MarketCount { get; set; }
        public int OrderCount { get; set; }
        public int ProfitOpportunityCount { get; set; }
        public TimeSpan DataAge { get; set; }
        public string Status { get; set; } = "";
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Enhanced trading route with multiple opportunities and route metrics
    /// </summary>
    public class TradingRoute
    {
        public string ItemName { get; set; } = "";
        public List<MarketData> Markets { get; set; } = new();
        public List<ProfitOpportunity> Opportunities { get; set; } = new();
        public double TotalDistance { get; set; }
        public long TotalProfit { get; set; }
        public double ProfitPerKm { get; set; }
        public TimeSpan EstimatedTime { get; set; }
        public int StopCount => Markets.Count;
        public string RouteDescription => $"{StopCount} stops, {TotalDistance:F0}m, {TotalProfit:N0} profit";
        public string Description => $"{ItemName} route: {Opportunities.Count} hops, " +
                                   $"{TotalProfit:N0} profit over {TotalDistance:F0}km " +
                                   $"({ProfitPerKm:F2}/km, ~{EstimatedTime.TotalHours:F1}h)";
    }
}