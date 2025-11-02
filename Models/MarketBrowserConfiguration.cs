using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace MarketBrowserMod.Models
{
    /// <summary>
    /// Configuration model for MarketBrowserMod with environment variable support
    /// Task 13: Environment variable configuration for bot credentials, refresh intervals, and network settings
    /// </summary>
    public class MarketBrowserConfiguration
    {
        /// <summary>
        /// Bot login username - from BOT_LOGIN or MARKET_BOT_LOGIN environment variable
        /// </summary>
        public string BotLogin { get; set; } = string.Empty;

        /// <summary>
        /// Bot login password - from BOT_PASSWORD or MARKET_BOT_PASSWORD environment variable
        /// </summary>
        public string BotPassword { get; set; } = string.Empty;

        /// <summary>
        /// Queueing service URL for Orleans connection
        /// Default: http://queueing:9630
        /// </summary>
        public string QueueingUrl { get; set; } = "http://queueing:9630";

        /// <summary>
        /// Web server port for the API and frontend
        /// Default: 8080
        /// </summary>
        public int WebServerPort { get; set; } = 8080;

        /// <summary>
        /// Market data refresh interval in minutes
        /// Default: 15 minutes
        /// </summary>
        public int RefreshIntervalMinutes { get; set; } = 15;

        /// <summary>
        /// Maximum cache age before data is considered stale (minutes)
        /// Default: 60 minutes
        /// </summary>
        public int MaxCacheAgeMinutes { get; set; } = 60;

        /// <summary>
        /// Maximum retry attempts for Orleans operations
        /// Default: 3
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Rate limiting delay between API calls (milliseconds)
        /// Default: 1000ms
        /// </summary>
        public int RateLimitDelayMs { get; set; } = 1000;

        /// <summary>
        /// Connection timeout for Orleans operations (seconds)
        /// Default: 30 seconds
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Delay before attempting session reconnection (milliseconds)
        /// Default: 5000ms
        /// </summary>
        public int SessionReconnectDelayMs { get; set; } = 5000;

        /// <summary>
        /// Maximum consecutive failures before extended delay
        /// Default: 5
        /// </summary>
        public int MaxConsecutiveFailures { get; set; } = 5;

        /// <summary>
        /// Log level for application logging
        /// Default: Information
        /// </summary>
        public string LogLevel { get; set; } = "Information";

        /// <summary>
        /// Maximum distance for route calculations (kilometers)
        /// Default: 1,000,000 km
        /// </summary>
        public double MaxDistanceKm { get; set; } = 1000000.0;

        /// <summary>
        /// Minimum profit margin threshold for opportunities
        /// Default: 0.1 (10%)
        /// </summary>
        public double ProfitMarginThreshold { get; set; } = 0.1;

        /// <summary>
        /// Load configuration from environment variables with fallback to defaults
        /// </summary>
        public static MarketBrowserConfiguration LoadFromEnvironment()
        {
            var config = new MarketBrowserConfiguration();

            // Bot credentials - try multiple environment variable names for compatibility
            config.BotLogin = Environment.GetEnvironmentVariable("BOT_LOGIN") ?? 
                             Environment.GetEnvironmentVariable("MARKET_BOT_LOGIN") ?? 
                             string.Empty;
            config.BotPassword = Environment.GetEnvironmentVariable("BOT_PASSWORD") ?? 
                                Environment.GetEnvironmentVariable("MARKET_BOT_PASSWORD") ?? 
                                string.Empty;

            // Optional environment variables with defaults
            config.QueueingUrl = Environment.GetEnvironmentVariable("QUEUEING") ?? config.QueueingUrl;
            
            if (int.TryParse(Environment.GetEnvironmentVariable("WEB_PORT"), out var webPort))
                config.WebServerPort = webPort;

            if (int.TryParse(Environment.GetEnvironmentVariable("REFRESH_INTERVAL_MINUTES"), out var refreshInterval))
                config.RefreshIntervalMinutes = refreshInterval;

            if (int.TryParse(Environment.GetEnvironmentVariable("MAX_CACHE_AGE_MINUTES"), out var maxCacheAge))
                config.MaxCacheAgeMinutes = maxCacheAge;

            if (int.TryParse(Environment.GetEnvironmentVariable("MAX_RETRY_ATTEMPTS"), out var maxRetries))
                config.MaxRetryAttempts = maxRetries;

            if (int.TryParse(Environment.GetEnvironmentVariable("RATE_LIMIT_DELAY_MS"), out var rateLimit))
                config.RateLimitDelayMs = rateLimit;

            if (int.TryParse(Environment.GetEnvironmentVariable("CONNECTION_TIMEOUT_SECONDS"), out var timeout))
                config.ConnectionTimeoutSeconds = timeout;

            if (int.TryParse(Environment.GetEnvironmentVariable("SESSION_RECONNECT_DELAY_MS"), out var reconnectDelay))
                config.SessionReconnectDelayMs = reconnectDelay;

            if (int.TryParse(Environment.GetEnvironmentVariable("MAX_CONSECUTIVE_FAILURES"), out var maxFailures))
                config.MaxConsecutiveFailures = maxFailures;

            config.LogLevel = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? config.LogLevel;

            if (double.TryParse(Environment.GetEnvironmentVariable("MAX_DISTANCE_KM"), out var maxDistance))
                config.MaxDistanceKm = maxDistance;

            if (double.TryParse(Environment.GetEnvironmentVariable("PROFIT_MARGIN_THRESHOLD"), out var profitThreshold))
                config.ProfitMarginThreshold = profitThreshold;

            return config;
        }

        /// <summary>
        /// Validate that all required configuration is present
        /// </summary>
        public void Validate()
        {
            // Check for bot credentials - warn but don't fail if missing
            if (string.IsNullOrWhiteSpace(BotLogin))
            {
                Console.WriteLine("WARNING: BOT_LOGIN environment variable not set - bot authentication may fail");
            }

            if (string.IsNullOrWhiteSpace(BotPassword))
            {
                Console.WriteLine("WARNING: BOT_PASSWORD environment variable not set - bot authentication may fail");
            }

            // Validate numeric ranges
            if (RefreshIntervalMinutes < 1)
            {
                Console.WriteLine("WARNING: RefreshIntervalMinutes is less than 1, using default of 15 minutes");
                RefreshIntervalMinutes = 15;
            }

            if (WebServerPort < 1 || WebServerPort > 65535)
            {
                Console.WriteLine("WARNING: WebServerPort is invalid, using default of 8080");
                WebServerPort = 8080;
            }

            if (MaxRetryAttempts < 1)
            {
                Console.WriteLine("WARNING: MaxRetryAttempts is less than 1, using default of 3");
                MaxRetryAttempts = 3;
            }
        }

        /// <summary>
        /// Log the current configuration (without sensitive data)
        /// </summary>
        public void LogConfiguration()
        {
            Console.WriteLine("=== MarketBrowserMod Configuration ===");
            Console.WriteLine($"Bot Login: {BotLogin}");
            Console.WriteLine($"Bot Password: {new string('*', Math.Min(BotPassword.Length, 8))}");
            Console.WriteLine($"Queueing URL: {QueueingUrl}");
            Console.WriteLine($"Web Server Port: {WebServerPort}");
            Console.WriteLine($"Refresh Interval: {RefreshIntervalMinutes} minutes");
            Console.WriteLine($"Max Cache Age: {MaxCacheAgeMinutes} minutes");
            Console.WriteLine($"Max Retry Attempts: {MaxRetryAttempts}");
            Console.WriteLine($"Rate Limit Delay: {RateLimitDelayMs}ms");
            Console.WriteLine($"Connection Timeout: {ConnectionTimeoutSeconds}s");
            Console.WriteLine($"Session Reconnect Delay: {SessionReconnectDelayMs}ms");
            Console.WriteLine($"Max Consecutive Failures: {MaxConsecutiveFailures}");
            Console.WriteLine($"Log Level: {LogLevel}");
            Console.WriteLine($"Max Distance: {MaxDistanceKm:N0} km");
            Console.WriteLine($"Profit Margin Threshold: {ProfitMarginThreshold:P1}");
            Console.WriteLine("=====================================");
        }
    }
}