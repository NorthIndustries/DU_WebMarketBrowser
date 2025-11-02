using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using MarketBrowserMod.Models;
using NQ;

namespace MarketBrowserMod.Tests
{
    /// <summary>
    /// Helper class for creating test data and common test utilities
    /// </summary>
    public static class TestHelper
    {
        /// <summary>
        /// Create a mock logger that captures log messages for testing
        /// </summary>
        public static Mock<ILogger<T>> CreateMockLogger<T>()
        {
            var mockLogger = new Mock<ILogger<T>>();
            
            // Setup to capture log calls if needed for assertions
            mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

            return mockLogger;
        }

        /// <summary>
        /// Create test market data with realistic values
        /// </summary>
        public static List<MarketData> CreateTestMarkets(int count = 5)
        {
            var markets = new List<MarketData>();
            var planetNames = new[] { "Alioth", "Sanctuary", "Madis", "Thades", "Talemai" };
            var planetIds = new ulong[] { 2, 26, 27, 30, 31 };

            for (int i = 0; i < count; i++)
            {
                var planetIndex = i % planetNames.Length;
                markets.Add(new MarketData
                {
                    MarketId = (ulong)(1000 + i),
                    Name = $"Test Market {i + 1}",
                    ConstructId = (ulong)(2000 + i),
                    PlanetId = planetIds[planetIndex],
                    PlanetName = planetNames[planetIndex],
                    Position = new Vec3 
                    { 
                        x = i * 1000000, 
                        y = i * 500000, 
                        z = i * 250000 
                    },
                    LastUpdated = DateTime.UtcNow.AddMinutes(-i),
                    DistanceFromOrigin = Math.Sqrt(Math.Pow(i * 1000000, 2) + Math.Pow(i * 500000, 2) + Math.Pow(i * 250000, 2)),
                    Orders = new List<OrderData>()
                });
            }

            return markets;
        }

        /// <summary>
        /// Create test order data with realistic values
        /// </summary>
        public static List<OrderData> CreateTestOrders(int count = 10, List<MarketData>? markets = null)
        {
            markets ??= CreateTestMarkets(3);
            var orders = new List<OrderData>();
            var itemNames = new[] { "Iron Ore", "Carbon Fiber", "Aluminum", "Silicon", "Chromium" };
            var playerNames = new[] { "TestPlayer1", "TestPlayer2", "TestPlayer3", "TestPlayer4", "TestPlayer5" };

            for (int i = 0; i < count; i++)
            {
                var market = markets[i % markets.Count];
                var itemIndex = i % itemNames.Length;
                var playerIndex = i % playerNames.Length;
                var isBuyOrder = i % 2 == 0;

                var order = new OrderData
                {
                    OrderId = (ulong)(3000 + i),
                    MarketId = market.MarketId,
                    MarketName = market.Name,
                    ItemType = (ulong)(4000 + itemIndex),
                    ItemName = itemNames[itemIndex],
                    BuyQuantity = isBuyOrder ? 100 + (i * 10) : 0,
                    SellQuantity = isBuyOrder ? 0 : 80 + (i * 8),
                    UnitPrice = new Currency { amount = 1000 + (i * 100) + (isBuyOrder ? 500 : 0) },
                    PlayerId = (ulong)(5000 + playerIndex),
                    PlayerName = playerNames[playerIndex],
                    ExpirationDate = DateTime.UtcNow.AddDays(1 + i),
                    LastUpdated = DateTime.UtcNow.AddMinutes(-i * 2),
                    DistanceFromOrigin = market.DistanceFromOrigin
                };

                orders.Add(order);
            }

            return orders;
        }

        /// <summary>
        /// Create test profit opportunities with realistic calculations
        /// </summary>
        public static List<ProfitOpportunity> CreateTestProfitOpportunities(int count = 5)
        {
            var markets = CreateTestMarkets(count * 2);
            var opportunities = new List<ProfitOpportunity>();
            var itemNames = new[] { "Iron Ore", "Carbon Fiber", "Aluminum", "Silicon", "Chromium" };

            for (int i = 0; i < count; i++)
            {
                var buyMarket = markets[i * 2];
                var sellMarket = markets[(i * 2) + 1];
                var itemName = itemNames[i % itemNames.Length];
                
                var sellPrice = 1000 + (i * 200);
                var buyPrice = sellPrice + 300 + (i * 100); // Ensure profit
                var quantity = 50 + (i * 20);

                var buyOrder = new OrderData
                {
                    OrderId = (ulong)(6000 + (i * 2)),
                    MarketId = buyMarket.MarketId,
                    MarketName = buyMarket.Name,
                    ItemType = (ulong)(4000 + (i % itemNames.Length)),
                    ItemName = itemName,
                    BuyQuantity = quantity + 20,
                    SellQuantity = 0,
                    UnitPrice = new Currency { amount = buyPrice },
                    PlayerId = (ulong)(7000 + i),
                    PlayerName = $"Buyer{i}",
                    ExpirationDate = DateTime.UtcNow.AddDays(2)
                };

                var sellOrder = new OrderData
                {
                    OrderId = (ulong)(6000 + (i * 2) + 1),
                    MarketId = sellMarket.MarketId,
                    MarketName = sellMarket.Name,
                    ItemType = (ulong)(4000 + (i % itemNames.Length)),
                    ItemName = itemName,
                    BuyQuantity = 0,
                    SellQuantity = quantity,
                    UnitPrice = new Currency { amount = sellPrice },
                    PlayerId = (ulong)(7000 + i + 100),
                    PlayerName = $"Seller{i}",
                    ExpirationDate = DateTime.UtcNow.AddDays(3)
                };

                var opportunity = new ProfitOpportunity
                {
                    ItemName = itemName,
                    ItemType = (ulong)(4000 + (i % itemNames.Length)),
                    BuyOrder = buyOrder,
                    SellOrder = sellOrder,
                    Distance = CalculateDistance(buyMarket.Position, sellMarket.Position)
                };

                opportunity.CalculateProfitMetrics();
                opportunities.Add(opportunity);
            }

            return opportunities;
        }

        /// <summary>
        /// Create test planet data
        /// </summary>
        public static List<PlanetData> CreateTestPlanets()
        {
            return new List<PlanetData>
            {
                new PlanetData
                {
                    PlanetId = 2,
                    Name = "Alioth",
                    Position = new Vec3 { x = 0, y = 0, z = 0 },
                    DistanceFromOrigin = 0
                },
                new PlanetData
                {
                    PlanetId = 26,
                    Name = "Sanctuary",
                    Position = new Vec3 { x = 5000000, y = 0, z = 0 },
                    DistanceFromOrigin = 5000000
                },
                new PlanetData
                {
                    PlanetId = 27,
                    Name = "Madis",
                    Position = new Vec3 { x = 0, y = 8000000, z = 0 },
                    DistanceFromOrigin = 8000000
                },
                new PlanetData
                {
                    PlanetId = 30,
                    Name = "Thades",
                    Position = new Vec3 { x = 12000000, y = 0, z = 0 },
                    DistanceFromOrigin = 12000000
                },
                new PlanetData
                {
                    PlanetId = 31,
                    Name = "Talemai",
                    Position = new Vec3 { x = 0, y = 0, z = 15000000 },
                    DistanceFromOrigin = 15000000
                }
            };
        }

        /// <summary>
        /// Calculate distance between two Vec3 positions
        /// </summary>
        public static double CalculateDistance(Vec3? pos1, Vec3? pos2)
        {
            if (pos1 == null || pos2 == null) return 1000000; // Default distance

            var dx = pos1.Value.x - pos2.Value.x;
            var dy = pos1.Value.y - pos2.Value.y;
            var dz = pos1.Value.z - pos2.Value.z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Create cache statistics for testing
        /// </summary>
        public static CacheStatistics CreateTestCacheStatistics(bool isHealthy = true)
        {
            return new CacheStatistics
            {
                MarketCount = isHealthy ? 25 : 0,
                OrderCount = isHealthy ? 150 : 0,
                PlayerNameCount = isHealthy ? 50 : 0,
                ItemNameCount = isHealthy ? 20 : 0,
                LastSuccessfulRefresh = isHealthy ? DateTime.UtcNow.AddMinutes(-5) : DateTime.MinValue,
                LastRefreshAttempt = DateTime.UtcNow.AddMinutes(-1),
                CacheAge = isHealthy ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(2),
                IsStale = !isHealthy,
                IsRefreshing = false,
                ConsecutiveFailures = isHealthy ? 0 : 3,
                OrleansAvailable = isHealthy
            };
        }

        /// <summary>
        /// Create test filters with various configurations
        /// </summary>
        public static OrderFilter CreateTestOrderFilter(
            string? itemName = null,
            string? marketName = null,
            string? orderType = null,
            long? minPrice = null,
            long? maxPrice = null,
            int page = 1,
            int pageSize = 50)
        {
            return new OrderFilter
            {
                ItemName = itemName,
                MarketName = marketName,
                OrderType = orderType,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Create test profit filter with various configurations
        /// </summary>
        public static ProfitFilter CreateTestProfitFilter(
            string? itemName = null,
            double? minProfitMargin = null,
            long? minTotalProfit = null,
            double? maxDistance = null,
            int page = 1,
            int pageSize = 50)
        {
            return new ProfitFilter
            {
                ItemName = itemName,
                MinProfitMargin = minProfitMargin,
                MinTotalProfit = minTotalProfit,
                MaxDistance = maxDistance,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Verify that a paged response has correct pagination metadata
        /// </summary>
        public static void VerifyPagedResponse<T>(PagedResponse<T> response, int expectedPage, int expectedPageSize)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            if (response.Page != expectedPage)
                throw new InvalidOperationException($"Expected page {expectedPage}, got {response.Page}");

            if (response.PageSize != expectedPageSize)
                throw new InvalidOperationException($"Expected page size {expectedPageSize}, got {response.PageSize}");

            if (response.TotalPages != (int)Math.Ceiling((double)response.TotalCount / response.PageSize))
                throw new InvalidOperationException("Total pages calculation is incorrect");

            if (response.HasNextPage != (response.Page < response.TotalPages))
                throw new InvalidOperationException("HasNextPage calculation is incorrect");

            if (response.HasPreviousPage != (response.Page > 1))
                throw new InvalidOperationException("HasPreviousPage calculation is incorrect");
        }

        /// <summary>
        /// Create a realistic profit opportunity with specific parameters
        /// </summary>
        public static ProfitOpportunity CreateProfitOpportunity(
            string itemName,
            long sellPrice,
            long buyPrice,
            long sellQuantity,
            long buyQuantity,
            double distance = 1000000)
        {
            var opportunity = new ProfitOpportunity
            {
                ItemName = itemName,
                ItemType = 1001,
                BuyOrder = new OrderData
                {
                    OrderId = 2001,
                    MarketId = 3001,
                    MarketName = "Buy Market",
                    UnitPrice = new Currency { amount = buyPrice },
                    BuyQuantity = buyQuantity,
                    SellQuantity = 0
                },
                SellOrder = new OrderData
                {
                    OrderId = 2002,
                    MarketId = 3002,
                    MarketName = "Sell Market",
                    UnitPrice = new Currency { amount = sellPrice },
                    BuyQuantity = 0,
                    SellQuantity = sellQuantity
                },
                Distance = distance
            };

            opportunity.CalculateProfitMetrics();
            return opportunity;
        }
    }
}