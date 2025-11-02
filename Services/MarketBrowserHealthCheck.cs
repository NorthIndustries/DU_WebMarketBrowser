using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketBrowserMod.Services
{
    /// <summary>
    /// Comprehensive health check for Market Browser Mod
    /// Requirements 7.4, 7.5: Health check endpoints for container readiness and liveness probes
    /// </summary>
    public class MarketBrowserHealthCheck : IHealthCheck
    {
        private readonly MarketDataService marketDataService;
        private readonly MarketDataBackgroundService backgroundService;
        private readonly ILogger<MarketBrowserHealthCheck> logger;
        
        // Health check thresholds (configurable via environment variables)
        private readonly TimeSpan maxCacheAge;
        private readonly int maxConsecutiveFailures;
        private readonly int minMarketCount;
        private readonly int minOrderCount;

        public MarketBrowserHealthCheck(
            MarketDataService marketDataService,
            MarketDataBackgroundService backgroundService,
            ILogger<MarketBrowserHealthCheck> logger)
        {
            this.marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            this.backgroundService = backgroundService ?? throw new ArgumentNullException(nameof(backgroundService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Parse health check thresholds from environment variables
            var maxCacheAgeMinutes = int.TryParse(Environment.GetEnvironmentVariable("HEALTH_MAX_CACHE_AGE_MINUTES"), out var cacheAge) ? cacheAge : 120;
            var maxFailures = int.TryParse(Environment.GetEnvironmentVariable("HEALTH_MAX_CONSECUTIVE_FAILURES"), out var failures) ? failures : 5;
            var minMarkets = int.TryParse(Environment.GetEnvironmentVariable("HEALTH_MIN_MARKET_COUNT"), out var markets) ? markets : 1;
            var minOrders = int.TryParse(Environment.GetEnvironmentVariable("HEALTH_MIN_ORDER_COUNT"), out var orders) ? orders : 10;
            
            this.maxCacheAge = TimeSpan.FromMinutes(maxCacheAgeMinutes);
            this.maxConsecutiveFailures = maxFailures;
            this.minMarketCount = minMarkets;
            this.minOrderCount = minOrders;
            
            logger.LogInformation("MarketBrowserHealthCheck configured:");
            logger.LogInformation("  - Max cache age: {MaxCacheAge} minutes", maxCacheAgeMinutes);
            logger.LogInformation("  - Max consecutive failures: {MaxFailures}", maxFailures);
            logger.LogInformation("  - Min market count: {MinMarkets}", minMarkets);
            logger.LogInformation("  - Min order count: {MinOrders}", minOrders);
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var healthData = new Dictionary<string, object>();
            var issues = new List<string>();
            var warnings = new List<string>();
            
            try
            {
                logger.LogDebug("Performing health check...");
                
                // Check cache statistics
                var cacheStats = marketDataService.GetCacheStatistics();
                healthData["cache"] = new
                {
                    marketCount = cacheStats.MarketCount,
                    orderCount = cacheStats.OrderCount,
                    playerNameCount = cacheStats.PlayerNameCount,
                    itemNameCount = cacheStats.ItemNameCount,
                    lastSuccessfulRefresh = cacheStats.LastSuccessfulRefresh,
                    lastRefreshAttempt = cacheStats.LastRefreshAttempt,
                    cacheAge = cacheStats.CacheAge.TotalMinutes,
                    isStale = cacheStats.IsStale,
                    isRefreshing = cacheStats.IsRefreshing,
                    consecutiveFailures = cacheStats.ConsecutiveFailures,
                    orleansAvailable = cacheStats.OrleansAvailable
                };
                
                // Check background service status
                var serviceStatus = backgroundService.GetStatus();
                healthData["backgroundService"] = new
                {
                    isRunning = serviceStatus.IsRunning,
                    consecutiveFailures = serviceStatus.ConsecutiveFailures,
                    lastFailureTime = serviceStatus.LastFailureTime,
                    requestCount = serviceStatus.RequestCount,
                    lastRequestTime = serviceStatus.LastRequestTime,
                    nextRefreshTime = serviceStatus.NextRefreshTime
                };
                
                // Critical checks (will cause health check to fail)
                
                // 1. Check if we have minimum required data
                if (cacheStats.MarketCount < minMarketCount)
                {
                    issues.Add($"Insufficient market data: {cacheStats.MarketCount} markets (minimum: {minMarketCount})");
                }
                
                if (cacheStats.OrderCount < minOrderCount)
                {
                    issues.Add($"Insufficient order data: {cacheStats.OrderCount} orders (minimum: {minOrderCount})");
                }
                
                // 2. Check if Orleans services are available
                if (!cacheStats.OrleansAvailable)
                {
                    issues.Add("Orleans services are unavailable");
                }
                
                // 3. Check for excessive consecutive failures
                if (cacheStats.ConsecutiveFailures >= maxConsecutiveFailures)
                {
                    issues.Add($"Too many consecutive failures: {cacheStats.ConsecutiveFailures} (maximum: {maxConsecutiveFailures})");
                }
                
                // 4. Check if background service is running
                if (!serviceStatus.IsRunning)
                {
                    issues.Add("Background refresh service is not running");
                }
                
                // 5. Check if we've never had a successful refresh
                if (cacheStats.LastSuccessfulRefresh == DateTime.MinValue)
                {
                    issues.Add("No successful data refresh has occurred");
                }
                
                // Warning checks (will not cause failure but indicate potential issues)
                
                // 1. Check cache age
                if (cacheStats.CacheAge > maxCacheAge)
                {
                    warnings.Add($"Cache data is old: {cacheStats.CacheAge.TotalMinutes:F1} minutes (threshold: {maxCacheAge.TotalMinutes} minutes)");
                }
                
                // 2. Check if cache is marked as stale
                if (cacheStats.IsStale)
                {
                    warnings.Add("Cache data is marked as stale");
                }
                
                // 3. Check for recent failures
                if (cacheStats.ConsecutiveFailures > 0 && cacheStats.ConsecutiveFailures < maxConsecutiveFailures)
                {
                    warnings.Add($"Recent refresh failures: {cacheStats.ConsecutiveFailures}");
                }
                
                // 4. Check background service failures
                if (serviceStatus.ConsecutiveFailures > 0)
                {
                    warnings.Add($"Background service failures: {serviceStatus.ConsecutiveFailures}");
                }
                
                // 5. Check if refresh is taking too long
                if (cacheStats.IsRefreshing && cacheStats.LastRefreshAttempt != DateTime.MinValue)
                {
                    var refreshDuration = DateTime.UtcNow - cacheStats.LastRefreshAttempt;
                    if (refreshDuration > TimeSpan.FromMinutes(10))
                    {
                        warnings.Add($"Refresh has been running for {refreshDuration.TotalMinutes:F1} minutes");
                    }
                }
                
                // Add warnings to health data
                if (warnings.Count > 0)
                {
                    healthData["warnings"] = warnings;
                }
                
                // Determine overall health status
                if (issues.Count > 0)
                {
                    healthData["issues"] = issues;
                    logger.LogWarning("Health check failed with {IssueCount} issues: {Issues}", 
                        issues.Count, string.Join("; ", issues));
                    
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        $"Market Browser has {issues.Count} critical issues", 
                        data: healthData));
                }
                
                if (warnings.Count > 0)
                {
                    logger.LogInformation("Health check passed with {WarningCount} warnings: {Warnings}", 
                        warnings.Count, string.Join("; ", warnings));
                    
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Market Browser is healthy but has {warnings.Count} warnings", 
                        data: healthData));
                }
                
                logger.LogDebug("Health check passed - all systems healthy");
                return Task.FromResult(HealthCheckResult.Healthy("Market Browser is fully operational", data: healthData));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Health check failed due to exception");
                
                healthData["error"] = ex.Message;
                healthData["exception"] = ex.GetType().Name;
                
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Health check failed due to exception", 
                    ex, 
                    data: healthData));
            }
        }
    }

    /// <summary>
    /// Readiness health check - determines if the service is ready to accept requests
    /// Requirements 7.5: Container readiness probes
    /// </summary>
    public class MarketBrowserReadinessCheck : IHealthCheck
    {
        private readonly MarketDataService marketDataService;
        private readonly ILogger<MarketBrowserReadinessCheck> logger;

        public MarketBrowserReadinessCheck(
            MarketDataService marketDataService,
            ILogger<MarketBrowserReadinessCheck> logger)
        {
            this.marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogDebug("Performing readiness check...");
                
                var cacheStats = marketDataService.GetCacheStatistics();
                var healthData = new Dictionary<string, object>
                {
                    ["hasData"] = cacheStats.MarketCount > 0 && cacheStats.OrderCount > 0,
                    ["orleansAvailable"] = cacheStats.OrleansAvailable,
                    ["lastSuccessfulRefresh"] = cacheStats.LastSuccessfulRefresh,
                    ["marketCount"] = cacheStats.MarketCount,
                    ["orderCount"] = cacheStats.OrderCount
                };
                
                // Service is ready if:
                // 1. We have some market and order data
                // 2. Orleans services are available OR we have cached data to serve
                // 3. We've had at least one successful refresh
                
                var hasData = cacheStats.MarketCount > 0 && cacheStats.OrderCount > 0;
                var canServeRequests = cacheStats.OrleansAvailable || hasData;
                var hasInitialized = cacheStats.LastSuccessfulRefresh != DateTime.MinValue;
                
                if (hasData && canServeRequests && hasInitialized)
                {
                    logger.LogDebug("Readiness check passed - service is ready to accept requests");
                    return Task.FromResult(HealthCheckResult.Healthy("Service is ready to accept requests", data: healthData));
                }
                
                var issues = new List<string>();
                if (!hasData) issues.Add("No market/order data available");
                if (!canServeRequests) issues.Add("Cannot serve requests (Orleans unavailable and no cached data)");
                if (!hasInitialized) issues.Add("Service has not completed initial data load");
                
                logger.LogWarning("Readiness check failed: {Issues}", string.Join("; ", issues));
                healthData["issues"] = issues;
                
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Service is not ready: {string.Join("; ", issues)}", 
                    data: healthData));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Readiness check failed due to exception");
                
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Readiness check failed due to exception", 
                    ex, 
                    data: new Dictionary<string, object> { ["error"] = ex.Message }));
            }
        }
    }

    /// <summary>
    /// Liveness health check - determines if the service is alive and should not be restarted
    /// Requirements 7.5: Container liveness probes
    /// </summary>
    public class MarketBrowserLivenessCheck : IHealthCheck
    {
        private readonly MarketDataBackgroundService backgroundService;
        private readonly ILogger<MarketBrowserLivenessCheck> logger;
        private readonly TimeSpan maxUnresponsiveTime;

        public MarketBrowserLivenessCheck(
            MarketDataBackgroundService backgroundService,
            ILogger<MarketBrowserLivenessCheck> logger)
        {
            this.backgroundService = backgroundService ?? throw new ArgumentNullException(nameof(backgroundService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Parse max unresponsive time from environment variable
            var maxUnresponsiveMinutes = int.TryParse(Environment.GetEnvironmentVariable("HEALTH_MAX_UNRESPONSIVE_MINUTES"), out var minutes) ? minutes : 60;
            this.maxUnresponsiveTime = TimeSpan.FromMinutes(maxUnresponsiveMinutes);
            
            logger.LogInformation("MarketBrowserLivenessCheck configured with max unresponsive time: {MaxUnresponsiveTime} minutes", maxUnresponsiveMinutes);
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogDebug("Performing liveness check...");
                
                var serviceStatus = backgroundService.GetStatus();
                var healthData = new Dictionary<string, object>
                {
                    ["isRunning"] = serviceStatus.IsRunning,
                    ["lastRequestTime"] = serviceStatus.LastRequestTime,
                    ["consecutiveFailures"] = serviceStatus.ConsecutiveFailures,
                    ["lastFailureTime"] = serviceStatus.LastFailureTime
                };
                
                // Service is alive if:
                // 1. Background service is running
                // 2. Service has been responsive recently (made requests or had activity)
                
                if (!serviceStatus.IsRunning)
                {
                    logger.LogError("Liveness check failed - background service is not running");
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        "Background service is not running", 
                        data: healthData));
                }
                
                // Check if service has been unresponsive for too long
                var timeSinceLastActivity = DateTime.UtcNow - serviceStatus.LastRequestTime;
                if (serviceStatus.LastRequestTime != DateTime.MinValue && timeSinceLastActivity > maxUnresponsiveTime)
                {
                    logger.LogError("Liveness check failed - service has been unresponsive for {UnresponsiveTime} minutes", 
                        timeSinceLastActivity.TotalMinutes);
                    
                    healthData["timeSinceLastActivity"] = timeSinceLastActivity.TotalMinutes;
                    
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        $"Service has been unresponsive for {timeSinceLastActivity.TotalMinutes:F1} minutes", 
                        data: healthData));
                }
                
                logger.LogDebug("Liveness check passed - service is alive and responsive");
                return Task.FromResult(HealthCheckResult.Healthy("Service is alive and responsive", data: healthData));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Liveness check failed due to exception");
                
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Liveness check failed due to exception", 
                    ex, 
                    data: new Dictionary<string, object> { ["error"] = ex.Message }));
            }
        }
    }
}