using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MarketBrowserMod.Services
{
    /// <summary>
    /// Background service for periodic market data refresh with rate limiting and exponential backoff
    /// Requirements 5.4, 5.6: Periodic refresh with rate limiting and comprehensive logging
    /// </summary>
    public class MarketDataBackgroundService : BackgroundService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<MarketDataBackgroundService> logger;
        private readonly TimeSpan refreshInterval;
        private readonly TimeSpan initialDelay;
        private readonly int maxRetryAttempts;
        private readonly TimeSpan maxBackoffDelay;
        
        // Rate limiting configuration
        private readonly int maxRequestsPerMinute;
        private readonly SemaphoreSlim rateLimitSemaphore;
        private DateTime lastRequestTime = DateTime.MinValue;
        private int requestCount = 0;
        private readonly object rateLimitLock = new object();
        
        // Exponential backoff state
        private int consecutiveFailures = 0;
        private DateTime lastFailureTime = DateTime.MinValue;

        public MarketDataBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<MarketDataBackgroundService> logger)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Parse configuration from environment variables with defaults
            var refreshIntervalMinutes = int.TryParse(Environment.GetEnvironmentVariable("REFRESH_INTERVAL_MINUTES"), out var interval) ? interval : 15;
            var initialDelaySeconds = int.TryParse(Environment.GetEnvironmentVariable("INITIAL_DELAY_SECONDS"), out var delay) ? delay : 30;
            var maxRetries = int.TryParse(Environment.GetEnvironmentVariable("MAX_RETRY_ATTEMPTS"), out var retries) ? retries : 3;
            var maxBackoffMinutes = int.TryParse(Environment.GetEnvironmentVariable("MAX_BACKOFF_MINUTES"), out var backoff) ? backoff : 30;
            var maxRequests = int.TryParse(Environment.GetEnvironmentVariable("MAX_REQUESTS_PER_MINUTE"), out var requests) ? requests : 10;
            
            this.refreshInterval = TimeSpan.FromMinutes(refreshIntervalMinutes);
            this.initialDelay = TimeSpan.FromSeconds(initialDelaySeconds);
            this.maxRetryAttempts = maxRetries;
            this.maxBackoffDelay = TimeSpan.FromMinutes(maxBackoffMinutes);
            this.maxRequestsPerMinute = maxRequests;
            this.rateLimitSemaphore = new SemaphoreSlim(maxRequests, maxRequests);
            
            logger.LogInformation("MarketDataBackgroundService configured:");
            logger.LogInformation("  - Refresh interval: {RefreshInterval} minutes", refreshIntervalMinutes);
            logger.LogInformation("  - Initial delay: {InitialDelay} seconds", initialDelaySeconds);
            logger.LogInformation("  - Max retry attempts: {MaxRetries}", maxRetries);
            logger.LogInformation("  - Max backoff delay: {MaxBackoff} minutes", maxBackoffMinutes);
            logger.LogInformation("  - Rate limit: {MaxRequests} requests per minute", maxRequests);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("MarketDataBackgroundService starting...");
            
            try
            {
                // Initial delay to allow system to fully initialize
                logger.LogInformation("Waiting {InitialDelay} seconds for system initialization...", initialDelay.TotalSeconds);
                await Task.Delay(initialDelay, stoppingToken);
                
                if (stoppingToken.IsCancellationRequested)
                {
                    logger.LogInformation("MarketDataBackgroundService cancelled during initial delay");
                    return;
                }
                
                logger.LogInformation("MarketDataBackgroundService initialized, starting periodic refresh cycle");
                
                // Main refresh loop
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await PerformRefreshCycle(stoppingToken);
                        
                        // Reset failure counter on successful refresh
                        if (consecutiveFailures > 0)
                        {
                            logger.LogInformation("Refresh successful, resetting failure counter from {FailureCount}", consecutiveFailures);
                            consecutiveFailures = 0;
                        }
                        
                        // Wait for next refresh interval
                        logger.LogDebug("Waiting {RefreshInterval} minutes until next refresh cycle", refreshInterval.TotalMinutes);
                        await Task.Delay(refreshInterval, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        logger.LogInformation("MarketDataBackgroundService refresh cycle cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        consecutiveFailures++;
                        lastFailureTime = DateTime.UtcNow;
                        
                        logger.LogError(ex, "Error during refresh cycle (failure {FailureCount}/{MaxRetries})", 
                            consecutiveFailures, maxRetryAttempts);
                        
                        // Calculate exponential backoff delay
                        var backoffDelay = CalculateBackoffDelay();
                        logger.LogWarning("Applying exponential backoff: waiting {BackoffDelay} minutes before retry", 
                            backoffDelay.TotalMinutes);
                        
                        try
                        {
                            await Task.Delay(backoffDelay, stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            logger.LogInformation("MarketDataBackgroundService cancelled during backoff delay");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Fatal error in MarketDataBackgroundService");
                throw;
            }
            finally
            {
                logger.LogInformation("MarketDataBackgroundService stopped");
            }
        }

        /// <summary>
        /// Perform a single refresh cycle with rate limiting and comprehensive logging
        /// Requirements 5.4, 5.6: Rate limiting and comprehensive logging
        /// </summary>
        private async Task PerformRefreshCycle(CancellationToken cancellationToken)
        {
            var cycleStartTime = DateTime.UtcNow;
            logger.LogInformation("Starting market data refresh cycle at {StartTime}", cycleStartTime);
            
            // Apply rate limiting
            await ApplyRateLimit(cancellationToken);
            
            // Get MarketDataService from DI container
            using var scope = serviceProvider.CreateScope();
            var marketDataService = scope.ServiceProvider.GetRequiredService<MarketDataService>();
            
            // Log pre-refresh statistics
            var preRefreshStats = marketDataService.GetCacheStatistics();
            logger.LogInformation("Pre-refresh cache statistics:");
            logger.LogInformation("  - Markets: {MarketCount}", preRefreshStats.MarketCount);
            logger.LogInformation("  - Orders: {OrderCount}", preRefreshStats.OrderCount);
            logger.LogInformation("  - Cache age: {CacheAge} minutes", preRefreshStats.CacheAge.TotalMinutes);
            logger.LogInformation("  - Is stale: {IsStale}", preRefreshStats.IsStale);
            logger.LogInformation("  - Orleans available: {OrleansAvailable}", preRefreshStats.OrleansAvailable);
            
            // Perform the actual refresh
            var refreshStartTime = DateTime.UtcNow;
            await marketDataService.RefreshMarketData();
            var refreshDuration = DateTime.UtcNow - refreshStartTime;
            
            // Log post-refresh statistics
            var postRefreshStats = marketDataService.GetCacheStatistics();
            logger.LogInformation("Market data refresh completed in {Duration} seconds", refreshDuration.TotalSeconds);
            logger.LogInformation("Post-refresh cache statistics:");
            logger.LogInformation("  - Markets: {MarketCount} (change: {MarketChange})", 
                postRefreshStats.MarketCount, postRefreshStats.MarketCount - preRefreshStats.MarketCount);
            logger.LogInformation("  - Orders: {OrderCount} (change: {OrderChange})", 
                postRefreshStats.OrderCount, postRefreshStats.OrderCount - preRefreshStats.OrderCount);
            logger.LogInformation("  - Player names cached: {PlayerCount}", postRefreshStats.PlayerNameCount);
            logger.LogInformation("  - Item names cached: {ItemCount}", postRefreshStats.ItemNameCount);
            logger.LogInformation("  - Last successful refresh: {LastRefresh}", postRefreshStats.LastSuccessfulRefresh);
            
            var totalCycleDuration = DateTime.UtcNow - cycleStartTime;
            logger.LogInformation("Refresh cycle completed in {TotalDuration} seconds", totalCycleDuration.TotalSeconds);
            
            // Log performance metrics
            if (postRefreshStats.OrderCount > 0)
            {
                var ordersPerSecond = postRefreshStats.OrderCount / Math.Max(1, refreshDuration.TotalSeconds);
                logger.LogInformation("Performance: {OrdersPerSecond:F1} orders processed per second", ordersPerSecond);
            }
            
            // Log warnings if needed
            if (postRefreshStats.IsStale)
            {
                logger.LogWarning("Cache is still marked as stale after refresh");
            }
            
            if (postRefreshStats.ConsecutiveFailures > 0)
            {
                logger.LogWarning("There were {FailureCount} consecutive failures during refresh", 
                    postRefreshStats.ConsecutiveFailures);
            }
        }

        /// <summary>
        /// Apply rate limiting to prevent server overload
        /// Requirement 5.4: Rate limiting for API calls to prevent server overload
        /// </summary>
        private async Task ApplyRateLimit(CancellationToken cancellationToken)
        {
            lock (rateLimitLock)
            {
                var now = DateTime.UtcNow;
                
                // Reset request count if more than a minute has passed
                if (now - lastRequestTime > TimeSpan.FromMinutes(1))
                {
                    requestCount = 0;
                    lastRequestTime = now;
                    
                    // Release all semaphore slots
                    var currentCount = rateLimitSemaphore.CurrentCount;
                    if (currentCount < maxRequestsPerMinute)
                    {
                        rateLimitSemaphore.Release(maxRequestsPerMinute - currentCount);
                    }
                }
                
                requestCount++;
            }
            
            // Wait for semaphore slot (rate limiting)
            logger.LogDebug("Acquiring rate limit semaphore (request {RequestCount}/{MaxRequests})", 
                requestCount, maxRequestsPerMinute);
            
            await rateLimitSemaphore.WaitAsync(cancellationToken);
            
            try
            {
                logger.LogDebug("Rate limit semaphore acquired, proceeding with refresh");
                
                // Ensure minimum delay between requests
                var timeSinceLastRequest = DateTime.UtcNow - lastRequestTime;
                var minDelay = TimeSpan.FromSeconds(60.0 / maxRequestsPerMinute);
                
                if (timeSinceLastRequest < minDelay)
                {
                    var additionalDelay = minDelay - timeSinceLastRequest;
                    logger.LogDebug("Applying additional rate limit delay: {Delay} ms", additionalDelay.TotalMilliseconds);
                    await Task.Delay(additionalDelay, cancellationToken);
                }
                
                lock (rateLimitLock)
                {
                    lastRequestTime = DateTime.UtcNow;
                }
            }
            finally
            {
                // Release semaphore slot after a delay to maintain rate limiting
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), CancellationToken.None);
                    rateLimitSemaphore.Release();
                });
            }
        }

        /// <summary>
        /// Calculate exponential backoff delay based on consecutive failures
        /// Requirement 5.4: Exponential backoff for API calls to prevent server overload
        /// </summary>
        private TimeSpan CalculateBackoffDelay()
        {
            if (consecutiveFailures <= 0)
            {
                return TimeSpan.Zero;
            }
            
            // Exponential backoff: 2^failures minutes, capped at maxBackoffDelay
            var backoffMinutes = Math.Min(Math.Pow(2, consecutiveFailures - 1), maxBackoffDelay.TotalMinutes);
            var backoffDelay = TimeSpan.FromMinutes(backoffMinutes);
            
            logger.LogDebug("Calculated backoff delay: {BackoffDelay} minutes for {FailureCount} consecutive failures", 
                backoffDelay.TotalMinutes, consecutiveFailures);
            
            return backoffDelay;
        }

        /// <summary>
        /// Get current service status for health checks
        /// </summary>
        public ServiceStatus GetStatus()
        {
            return new ServiceStatus
            {
                IsRunning = !ExecuteTask?.IsCompleted ?? false,
                ConsecutiveFailures = consecutiveFailures,
                LastFailureTime = lastFailureTime,
                RequestCount = requestCount,
                LastRequestTime = lastRequestTime,
                NextRefreshTime = lastRequestTime.Add(refreshInterval)
            };
        }

        public override void Dispose()
        {
            rateLimitSemaphore?.Dispose();
            base.Dispose();
        }
    }

    /// <summary>
    /// Service status information for health checks
    /// </summary>
    public class ServiceStatus
    {
        public bool IsRunning { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime LastFailureTime { get; set; }
        public int RequestCount { get; set; }
        public DateTime LastRequestTime { get; set; }
        public DateTime NextRefreshTime { get; set; }
    }
}