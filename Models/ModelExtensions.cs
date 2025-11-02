using System;
using System.Linq;
using NQ;
using MarketBrowserMod.Utils;
using MarketBrowserMod.Services;

namespace MarketBrowserMod.Models
{
    /// <summary>
    /// Extension methods for converting between core data models and response DTOs
    /// </summary>
    public static class ModelExtensions
    {
        /// <summary>
        /// Converts a Currency amount from quantas (whole number format) to decimal price.
        /// Dual Universe stores prices as whole numbers where the last 2 digits represent decimals.
        /// Example: 100000 quantas in DB = 1000.00 quantas (divide by 100)
        /// </summary>
        private static double ConvertToDecimalPrice(Currency currency)
        {
            return PriceConverter.ToDecimalPrice(currency.amount);
        }
        /// <summary>
        /// Converts MarketData to MarketResponse DTO
        /// </summary>
        public static MarketResponse ToResponse(this MarketData market)
        {
            var distanceFromOrigin = CalculateDistanceFromOrigin(market.Position);
            
            return new MarketResponse
            {
                MarketId = market.MarketId,
                Name = market.Name,
                ConstructId = market.ConstructId,
                Position = market.Position,
                PlanetId = market.PlanetId,
                PlanetName = market.PlanetName,
                LastUpdated = market.LastUpdated,
                OrderCount = market.Orders?.Count ?? 0,
                DistanceFromOrigin = distanceFromOrigin,
                DistanceFromOriginFormatted = DistanceFormatter.FormatDistance(distanceFromOrigin)
            };
        }

        /// <summary>
        /// Converts MarketData to MarketResponse DTO with base market distance calculations
        /// </summary>
        public static MarketResponse ToResponse(this MarketData market, BaseMarketService? baseMarketService, MarketDataService? marketDataService)
        {
            var distanceFromOrigin = CalculateDistanceFromOrigin(market.Position);
            var response = new MarketResponse
            {
                MarketId = market.MarketId,
                Name = market.Name,
                ConstructId = market.ConstructId,
                Position = market.Position,
                PlanetId = market.PlanetId,
                PlanetName = market.PlanetName,
                LastUpdated = market.LastUpdated,
                OrderCount = market.Orders?.Count ?? 0,
                DistanceFromOrigin = distanceFromOrigin,
                DistanceFromOriginFormatted = DistanceFormatter.FormatDistance(distanceFromOrigin)
            };

            // Calculate distance from base market if available
            if (baseMarketService != null && marketDataService != null)
            {
                var distanceFromBase = baseMarketService.CalculateDistanceFromBase(market, marketDataService);
                if (distanceFromBase.HasValue)
                {
                    response.DistanceFromBase = distanceFromBase.Value;
                    response.DistanceFromBaseFormatted = DistanceFormatter.FormatDistance(distanceFromBase.Value);
                }
            }

            return response;
        }

        /// <summary>
        /// Converts OrderData to OrderResponse DTO
        /// </summary>
        public static OrderResponse ToResponse(this OrderData order, MarketData? market = null)
        {
            var response = new OrderResponse
            {
                OrderId = order.OrderId,
                MarketId = order.MarketId,
                MarketName = order.MarketName,
                ItemType = order.ItemType,
                ItemName = order.ItemName,
                BuyQuantity = order.BuyQuantity,
                SellQuantity = order.SellQuantity,
                UnitPrice = ConvertToDecimalPrice(order.UnitPrice),
                PlayerId = order.PlayerId,
                PlayerName = order.PlayerName,
                ExpirationDate = order.ExpirationDate,
                LastUpdated = order.LastUpdated,
                DistanceFromOrigin = order.DistanceFromOrigin,
                DistanceFromOriginFormatted = DistanceFormatter.FormatDistance(order.DistanceFromOrigin),
                IsBuyOrder = order.IsBuyOrder,
                Quantity = order.Quantity,
                OrderType = order.IsBuyOrder ? "buy" : "sell",
                MarketPosition = market?.Position ?? new NQ.Vec3(),
                PlanetName = market?.PlanetName ?? ""
            };

            return response;
        }

        /// <summary>
        /// Converts OrderData to OrderResponse DTO with base market distance calculations
        /// </summary>
        public static OrderResponse ToResponse(this OrderData order, MarketData? market, BaseMarketService? baseMarketService, MarketDataService? marketDataService)
        {
            var response = order.ToResponse(market);

            // Calculate distance from base market if available
            if (baseMarketService != null && marketDataService != null && market != null)
            {
                var distanceFromBase = baseMarketService.CalculateDistanceFromBase(market, marketDataService);
                if (distanceFromBase.HasValue)
                {
                    response.DistanceFromBase = distanceFromBase.Value;
                    response.DistanceFromBaseFormatted = DistanceFormatter.FormatDistance(distanceFromBase.Value);
                }
            }

            return response;
        }

        /// <summary>
        /// Converts ProfitOpportunity to ProfitOpportunityResponse DTO
        /// </summary>
        public static ProfitOpportunityResponse ToResponse(this ProfitOpportunity opportunity, 
            MarketData? buyMarket = null, MarketData? sellMarket = null)
        {
            // Convert profit values from quantas to decimal
            var profitPerUnitDecimal = PriceConverter.ToDecimalPrice(opportunity.ProfitPerUnit);
            var totalProfitDecimal = PriceConverter.ToDecimalPrice(opportunity.TotalProfit);
            
            return new ProfitOpportunityResponse
            {
                ItemName = opportunity.ItemName,
                ItemType = opportunity.ItemType,
                BuyOrder = opportunity.BuyOrder.ToOrderSummary(buyMarket),
                SellOrder = opportunity.SellOrder.ToOrderSummary(sellMarket),
                ProfitPerUnit = profitPerUnitDecimal,
                ProfitMargin = opportunity.ProfitMargin,
                MaxQuantity = opportunity.MaxQuantity,
                TotalProfit = totalProfitDecimal,
                Distance = opportunity.Distance,
                DistanceFormatted = DistanceFormatter.FormatDistance(opportunity.Distance),
                ProfitPerKm = opportunity.ProfitPerKm,
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Converts OrderData to OrderSummary for profit opportunity responses
        /// </summary>
        public static OrderSummary ToOrderSummary(this OrderData order, MarketData? market = null)
        {
            return new OrderSummary
            {
                OrderId = order.OrderId,
                MarketId = order.MarketId,
                MarketName = order.MarketName,
                PlanetName = market?.PlanetName ?? "Unknown Planet",
                Position = market?.Position ?? new NQ.Vec3(),
                Quantity = order.Quantity,
                UnitPrice = ConvertToDecimalPrice(order.UnitPrice),
                PlayerName = order.PlayerName,
                ExpirationDate = order.ExpirationDate
            };
        }

        /// <summary>
        /// Converts PlanetData to PlanetResponse DTO
        /// Requirement 4.4: Planet name mapping and coordinate display functionality
        /// </summary>
        public static PlanetResponse ToResponse(this PlanetData planet, int marketCount = 0)
        {
            return new PlanetResponse
            {
                PlanetId = planet.PlanetId,
                Name = planet.Name,
                Position = planet.Position,
                DistanceFromOrigin = planet.DistanceFromOrigin,
                MarketCount = marketCount,
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Converts MarketData to LocationResponse
        /// Requirement 4.3, 4.6: Distance calculations and route planning
        /// </summary>
        public static LocationResponse ToLocationResponse(this MarketData market, Vec3? queryPosition = null)
        {
            var response = new LocationResponse
            {
                Position = market.Position,
                Name = $"{market.Name} ({market.PlanetName})",
                Type = "market",
                DistanceFromOrigin = market.DistanceFromOrigin
            };

            if (queryPosition != null)
            {
                response.DistanceFromQuery = CalculateDistance(queryPosition, market.Position);
            }

            return response;
        }

        /// <summary>
        /// Converts PlanetData to LocationResponse
        /// Requirement 4.3, 4.6: Distance calculations and route planning
        /// </summary>
        public static LocationResponse ToLocationResponse(this PlanetData planet, Vec3? queryPosition = null)
        {
            var response = new LocationResponse
            {
                Position = planet.Position,
                Name = planet.Name,
                Type = "planet",
                DistanceFromOrigin = planet.DistanceFromOrigin
            };

            if (queryPosition != null)
            {
                response.DistanceFromQuery = CalculateDistance(queryPosition, planet.Position);
            }

            return response;
        }

        /// <summary>
        /// Creates a paginated response from a collection
        /// </summary>
        public static PagedResponse<T> ToPagedResponse<T>(
            this System.Collections.Generic.IEnumerable<T> items,
            int page,
            int pageSize,
            int totalCount)
        {
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            
            return new PagedResponse<T>
            {
                Data = items.ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = page < totalPages,
                HasPreviousPage = page > 1,
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Calculates distance from origin (0,0,0) for a position
        /// Requirement 4.3: Vec3 distance calculations
        /// </summary>
        private static double CalculateDistanceFromOrigin(NQ.Vec3? position)
        {
            if (position == null) return 0;
            
            return Math.Sqrt(
                position.Value.x * position.Value.x +
                position.Value.y * position.Value.y +
                position.Value.z * position.Value.z
            );
        }

        /// <summary>
        /// Calculates distance between two positions
        /// Requirement 4.3: Vec3 distance calculations between market positions
        /// </summary>
        public static double CalculateDistance(NQ.Vec3? pos1, NQ.Vec3? pos2)
        {
            if (pos1 == null || pos2 == null) return 0;
            
            var dx = pos1.Value.x - pos2.Value.x;
            var dy = pos1.Value.y - pos2.Value.y;
            var dz = pos1.Value.z - pos2.Value.z;
            
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}