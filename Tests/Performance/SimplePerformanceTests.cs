using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using MarketBrowserMod.Models;

namespace MarketBrowserMod.Tests.Performance
{
    /// <summary>
    /// Simple performance tests for cache operations and data processing
    /// Requirements: Performance optimization validation
    /// </summary>
    public class SimplePerformanceTests
    {
        private readonly ITestOutputHelper output;

        public SimplePerformanceTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ProfitOpportunity_CalculationPerformance_ShouldBeReasonable()
        {
            // Arrange
            var opportunities = CreateTestProfitOpportunities(1000);
            var stopwatch = new Stopwatch();

            // Act
            stopwatch.Start();
            foreach (var opportunity in opportunities)
            {
                opportunity.CalculateProfitMetrics();
            }
            stopwatch.Stop();

            // Assert
            var timePerCalculation = stopwatch.ElapsedMilliseconds / (double)opportunities.Count;
            output.WriteLine($"Calculated {opportunities.Count} profit opportunities in {stopwatch.ElapsedMilliseconds}ms");
            output.WriteLine($"Average time per calculation: {timePerCalculation:F3}ms");

            timePerCalculation.Should().BeLessThan(1.0, "Each profit calculation should take less than 1ms");
        }

        [Fact]
        public void DataModel_MemoryUsage_ShouldBeReasonable()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);

            // Act - Create large dataset
            var markets = CreateTestMarkets(100);
            var orders = CreateTestOrders(1000);
            var opportunities = CreateTestProfitOpportunities(500);

            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = finalMemory - initialMemory;

            // Assert
            output.WriteLine($"Memory used for test data: {memoryUsed / 1024}KB");
            output.WriteLine($"Markets: {markets.Count}, Orders: {orders.Count}, Opportunities: {opportunities.Count}");

            memoryUsed.Should().BeLessThan(10 * 1024 * 1024, "Memory usage should be less than 10MB for test dataset");
        }

        [Fact]
        public void List_FilteringPerformance_ShouldBeReasonable()
        {
            // Arrange
            var orders = CreateTestOrders(10000);
            var stopwatch = new Stopwatch();

            // Act - Test various filtering operations
            stopwatch.Start();
            var buyOrders = orders.Where(o => o.IsBuyOrder).ToList();
            var sellOrders = orders.Where(o => !o.IsBuyOrder).ToList();
            var expensiveOrders = orders.Where(o => o.UnitPrice.amount > 5000).ToList();
            var recentOrders = orders.Where(o => o.LastUpdated > DateTime.UtcNow.AddHours(-1)).ToList();
            stopwatch.Stop();

            // Assert
            output.WriteLine($"Filtered {orders.Count} orders in {stopwatch.ElapsedMilliseconds}ms");
            output.WriteLine($"Buy orders: {buyOrders.Count}, Sell orders: {sellOrders.Count}");
            output.WriteLine($"Expensive orders: {expensiveOrders.Count}, Recent orders: {recentOrders.Count}");

            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Filtering should complete within 100ms");
        }

        [Fact]
        public void List_SortingPerformance_ShouldBeReasonable()
        {
            // Arrange
            var orders = CreateTestOrders(10000);
            var stopwatch = new Stopwatch();

            // Act - Test various sorting operations
            stopwatch.Start();
            var sortedByPrice = orders.OrderBy(o => o.UnitPrice.amount).ToList();
            var sortedByQuantity = orders.OrderByDescending(o => o.Quantity).ToList();
            var sortedByDate = orders.OrderBy(o => o.LastUpdated).ToList();
            stopwatch.Stop();

            // Assert
            output.WriteLine($"Sorted {orders.Count} orders (3 different sorts) in {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.ElapsedMilliseconds.Should().BeLessThan(200, "Sorting should complete within 200ms");
            
            // Verify sorting worked correctly
            sortedByPrice.First().UnitPrice.amount.Should().BeLessOrEqualTo(sortedByPrice.Last().UnitPrice.amount);
            sortedByQuantity.First().Quantity.Should().BeGreaterOrEqualTo(sortedByQuantity.Last().Quantity);
        }

        [Fact]
        public async Task ConcurrentAccess_ShouldHandleMultipleReaders()
        {
            // Arrange
            var orders = CreateTestOrders(1000);
            var tasks = new List<Task>();
            var results = new List<int>();
            var lockObject = new object();

            // Act - Simulate concurrent read operations
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var buyOrderCount = orders.Count(o => o.IsBuyOrder);
                    lock (lockObject)
                    {
                        results.Add(buyOrderCount);
                    }
                }));
            }

            var stopwatch = Stopwatch.StartNew();
            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            output.WriteLine($"Completed {tasks.Count} concurrent read operations in {stopwatch.ElapsedMilliseconds}ms");
            
            results.Should().HaveCount(10);
            results.Should().AllSatisfy(count => count.Should().BeGreaterThan(0));
            results.Distinct().Should().HaveCount(1, "All concurrent reads should return the same result");
            
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Concurrent operations should complete within 1 second");
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(5000)]
        public void DataProcessing_ShouldScaleLinearly(int dataSize)
        {
            // Arrange
            var orders = CreateTestOrders(dataSize);
            var stopwatch = new Stopwatch();

            // Act
            stopwatch.Start();
            var processedData = orders
                .Where(o => o.UnitPrice.amount > 1000)
                .OrderBy(o => o.UnitPrice.amount)
                .Take(100)
                .ToList();
            stopwatch.Stop();

            // Assert
            var timePerItem = stopwatch.ElapsedMilliseconds / (double)dataSize;
            output.WriteLine($"Processed {dataSize} items in {stopwatch.ElapsedMilliseconds}ms ({timePerItem:F4}ms per item)");

            timePerItem.Should().BeLessThan(0.1, "Processing time per item should be less than 0.1ms");
            processedData.Should().HaveCountLessOrEqualTo(100);
        }

        private List<MarketData> CreateTestMarkets(int count)
        {
            var markets = new List<MarketData>();
            var planetNames = new[] { "Alioth", "Sanctuary", "Madis", "Thades", "Talemai" };

            for (int i = 0; i < count; i++)
            {
                markets.Add(new MarketData
                {
                    MarketId = (ulong)(1000 + i),
                    Name = $"Market {i + 1}",
                    PlanetId = (ulong)(i % 5 + 2),
                    PlanetName = planetNames[i % planetNames.Length],
                    Position = new NQ.Vec3 { x = i * 1000, y = i * 500, z = i * 250 },
                    LastUpdated = DateTime.UtcNow.AddMinutes(-i),
                    Orders = new List<OrderData>()
                });
            }

            return markets;
        }

        private List<OrderData> CreateTestOrders(int count)
        {
            var orders = new List<OrderData>();
            var itemNames = new[] { "Iron Ore", "Carbon Fiber", "Aluminum", "Silicon", "Chromium" };
            var marketNames = new[] { "Market A", "Market B", "Market C", "Market D", "Market E" };
            var playerNames = new[] { "Player1", "Player2", "Player3", "Player4", "Player5" };

            for (int i = 0; i < count; i++)
            {
                var isBuyOrder = i % 2 == 0;
                orders.Add(new OrderData
                {
                    OrderId = (ulong)(2000 + i),
                    MarketId = (ulong)(1000 + (i % 5)),
                    MarketName = marketNames[i % marketNames.Length],
                    ItemType = (ulong)(3000 + (i % itemNames.Length)),
                    ItemName = itemNames[i % itemNames.Length],
                    BuyQuantity = isBuyOrder ? 100 + (i % 200) : 0,
                    SellQuantity = isBuyOrder ? 0 : 80 + (i % 150),
                    UnitPrice = new NQ.Currency { amount = 1000 + (i % 10000) },
                    PlayerId = (ulong)(4000 + (i % playerNames.Length)),
                    PlayerName = playerNames[i % playerNames.Length],
                    ExpirationDate = DateTime.UtcNow.AddDays(1 + (i % 7)),
                    LastUpdated = DateTime.UtcNow.AddMinutes(-i)
                });
            }

            return orders;
        }

        private List<ProfitOpportunity> CreateTestProfitOpportunities(int count)
        {
            var opportunities = new List<ProfitOpportunity>();
            var itemNames = new[] { "Iron Ore", "Carbon Fiber", "Aluminum", "Silicon", "Chromium" };

            for (int i = 0; i < count; i++)
            {
                var sellPrice = 1000 + (i % 5000);
                var buyPrice = sellPrice + 200 + (i % 1000);
                var quantity = 50 + (i % 200);

                var opportunity = new ProfitOpportunity
                {
                    ItemName = itemNames[i % itemNames.Length],
                    ItemType = (ulong)(3000 + (i % itemNames.Length)),
                    BuyOrder = new OrderData
                    {
                        OrderId = (ulong)(5000 + (i * 2)),
                        MarketId = (ulong)(1000 + (i % 10)),
                        MarketName = $"Buy Market {i % 10}",
                        UnitPrice = new NQ.Currency { amount = buyPrice },
                        BuyQuantity = quantity + 20,
                        SellQuantity = 0
                    },
                    SellOrder = new OrderData
                    {
                        OrderId = (ulong)(5000 + (i * 2) + 1),
                        MarketId = (ulong)(1000 + ((i + 5) % 10)),
                        MarketName = $"Sell Market {(i + 5) % 10}",
                        UnitPrice = new NQ.Currency { amount = sellPrice },
                        BuyQuantity = 0,
                        SellQuantity = quantity
                    },
                    Distance = 1000000 + (i % 5000000)
                };

                opportunities.Add(opportunity);
            }

            return opportunities;
        }
    }
}