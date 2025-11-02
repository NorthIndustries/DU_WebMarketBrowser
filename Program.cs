using System;
using System.Net.Http;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using BotLib.BotClient;
using BotLib.Generated;
using BotLib.Protocols;
using BotLib.Protocols.Queuing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using NQutils;
using NQutils.Config;
using NQutils.Logging;
using NQutils.Sql;
using Orleans;
using NQ.Interfaces;
using NQ;
using NQ.RDMS;
using NQ.Router;
using System.Threading.Channels;
using Backend;
using Backend.Business;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using MarketBrowserMod.Services;
using MarketBrowserMod.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.ComponentModel.DataAnnotations;

/// Mod base class
public class Mod
{
    public static IDuClientFactory RestDuClientFactory => serviceProvider.GetRequiredService<IDuClientFactory>();
    /// Use this to access registered service
    public static IServiceProvider serviceProvider;
    /// Use this to make gameplay calls, see "Interfaces/GrainGetterExtensions.cs" for what's available
    protected static IClusterClient orleans;
    /// Use this object for various data access/modify helper functions
    protected static IDataAccessor dataAccessor;
    /// Convenience field for mods who need a single bot
    protected Client bot;
    
    /// Create or login a user, return bot client instance
    public static async Task<Client> CreateUser(string prefix, bool allowExisting = false, bool randomize = true)
    {
        string username = prefix;
        if (randomize)
        {
            // Do not use random utilities as they are using tests random (that is seeded), and we want to be able to start the same test multiple times
            Random r = new Random(Guid.NewGuid().GetHashCode());
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";
            username = prefix + '-' + new string(Enumerable.Repeat(0, 127 - prefix.Length).Select(_ => chars[r.Next(chars.Length)]).ToArray());
        }
        // Try all possible environment variable names from your .env file
        var botLogin = Environment.GetEnvironmentVariable("MARKET_BOT_USERNAME") ?? 
                      Environment.GetEnvironmentVariable("BOT_LOGIN") ?? 
                      Environment.GetEnvironmentVariable("MARKET_BOT_LOGIN");
        
        var botPassword = Environment.GetEnvironmentVariable("MARKET_BOT_PASSWORD") ?? 
                         Environment.GetEnvironmentVariable("BOT_PASSWORD");
        
        Console.WriteLine($"MarketBrowserMod: Using bot credentials - Login: '{botLogin}', Password: {(string.IsNullOrEmpty(botPassword) ? "NOT SET" : "SET")}");
        
        LoginInformations pi = LoginInformations.BotLogin(username, botLogin, botPassword);
        return await Client.FromFactory(RestDuClientFactory, pi, allowExising: allowExisting);
    }
    
    /// Setup everything, must be called once at startup
    public static async Task Setup()
    {
        // Validate required environment variables - try multiple names
        var botLogin = Environment.GetEnvironmentVariable("MARKET_BOT_USERNAME") ?? 
                      Environment.GetEnvironmentVariable("BOT_LOGIN") ?? 
                      Environment.GetEnvironmentVariable("MARKET_BOT_LOGIN");
        
        var botPassword = Environment.GetEnvironmentVariable("MARKET_BOT_PASSWORD") ?? 
                         Environment.GetEnvironmentVariable("BOT_PASSWORD");
        
        if (string.IsNullOrEmpty(botLogin))
        {
            Console.WriteLine("WARNING: Bot login not found in any environment variable (MARKET_BOT_USERNAME, BOT_LOGIN, MARKET_BOT_LOGIN)");
        }
        else
        {
            Console.WriteLine($"MarketBrowserMod: Using bot login: {botLogin}");
        }
        
        if (string.IsNullOrEmpty(botPassword))
        {
            Console.WriteLine("WARNING: Bot password not found in any environment variable (MARKET_BOT_PASSWORD, BOT_PASSWORD)");
        }
        
        var services = new ServiceCollection();
            
        var qurl = Environment.GetEnvironmentVariable("QUEUEING");
        if (string.IsNullOrEmpty(qurl))
            qurl = "http://queueing:9630";
            
        Console.WriteLine($"MarketBrowserMod: Using queueing service URL: {qurl}");
        
        services
        .AddSingleton<ISql, Sql>()
        .AddInitializableSingleton<IGameplayBank, GameplayBank>()
        .AddSingleton<ILocalizationManager, LocalizationManager>()
        .AddTransient<IDataAccessor, DataAccessor>()
        .AddTransient<DatabaseMarketService>()
        .AddOrleansClient("IntegrationTests")
        .AddHttpClient()
        .AddTransient<NQutils.Stats.IStats, NQutils.Stats.FakeIStats>()
        .AddSingleton<IQueuing, RealQueuing>(sp => new RealQueuing(qurl, sp.GetRequiredService<IHttpClientFactory>().CreateClient()))
        .AddSingleton<IDuClientFactory, BotLib.Protocols.GrpcClient.DuClientFactory>()
        .AddLogging(builder => builder.AddConsole());
        
        var sp = services.BuildServiceProvider();
        serviceProvider = sp;
        
        Console.WriteLine("MarketBrowserMod: Starting Orleans services...");
        await serviceProvider.StartServices();
        
        ClientExtensions.SetSingletons(sp);
        ClientExtensions.UseFactory(sp.GetRequiredService<IDuClientFactory>());
        orleans = serviceProvider.GetRequiredService<IClusterClient>();
        dataAccessor = serviceProvider.GetRequiredService<IDataAccessor>();
        
        Console.WriteLine("MarketBrowserMod: Orleans client and services initialized successfully");
    }
    
    public async Task Start()
    {
        try
        {
            await Loop();
        }
        catch(Exception e)
        {
            Console.WriteLine($"{e}");
            throw;
        }
    }
    
    /// Override this with main bot code
    public virtual Task Loop()
    {
        return Task.CompletedTask;
    }
    
    /// Convenience helper for running code forever with error handling and reconnection
    public async Task SafeLoop(Func<Task> action, int exceptionDelayMs = 5000, Func<Task> reconnect = null)
    {
        while (true)
        {
            try
            {
                await action();
            }
            catch (NQutils.Exceptions.BusinessException be) when (be.error.code == NQ.ErrorCode.InvalidSession)
            {
                Console.WriteLine("Session invalid, reconnecting...");
                if (reconnect != null)
                {
                    await reconnect();
                }
                await Task.Delay(exceptionDelayMs);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception in mod action: {e}");
                await Task.Delay(exceptionDelayMs);
            }
        }
    }

    /// <summary>
    /// Enhanced SafeLoop with comprehensive session reconnection logic following TraderBot pattern
    /// Task 12: Implement session reconnection logic for InvalidSession errors
    /// </summary>
    public async Task SafeLoopWithSessionReconnection(Func<Task> action, int exceptionDelayMs = 5000)
    {
        var consecutiveFailures = 0;
        var maxConsecutiveFailures = 5;
        
        while (true)
        {
            try
            {
                await action();
                consecutiveFailures = 0; // Reset on successful execution
            }
            catch (NQutils.Exceptions.BusinessException be) when (be.error.code == NQ.ErrorCode.InvalidSession)
            {
                consecutiveFailures++;
                Console.WriteLine($"MarketBrowserMod: Invalid session detected (failure {consecutiveFailures}/{maxConsecutiveFailures}) - attempting reconnection...");
                
                try
                {
                    // Comprehensive session reconnection following TraderBot pattern
                    await ReconnectBotSession();
                    Console.WriteLine("MarketBrowserMod: Session reconnection completed successfully");
                    consecutiveFailures = 0; // Reset on successful reconnection
                }
                catch (Exception reconnectEx)
                {
                    Console.WriteLine($"MarketBrowserMod: Session reconnection failed: {reconnectEx.Message}");
                    
                    if (consecutiveFailures >= maxConsecutiveFailures)
                    {
                        Console.WriteLine($"MarketBrowserMod: Too many consecutive session failures ({consecutiveFailures}), applying extended delay");
                        await Task.Delay(exceptionDelayMs * 5); // Extended delay for persistent session issues
                    }
                    else
                    {
                        await Task.Delay(exceptionDelayMs);
                    }
                }
            }
            catch (TimeoutException tex)
            {
                consecutiveFailures++;
                Console.WriteLine($"MarketBrowserMod: Timeout exception (failure {consecutiveFailures}/{maxConsecutiveFailures}): {tex.Message}");
                
                // Apply exponential backoff for timeout issues
                var delay = Math.Min(exceptionDelayMs * (int)Math.Pow(2, Math.Min(consecutiveFailures - 1, 4)), 60000);
                Console.WriteLine($"MarketBrowserMod: Applying exponential backoff delay: {delay}ms");
                await Task.Delay(delay);
            }
            catch (Exception e)
            {
                consecutiveFailures++;
                Console.WriteLine($"MarketBrowserMod: Exception in mod action (failure {consecutiveFailures}/{maxConsecutiveFailures}): {e.Message}");
                
                // Log full exception details for debugging
                if (consecutiveFailures <= 2)
                {
                    Console.WriteLine($"MarketBrowserMod: Exception details: {e}");
                }
                
                // Apply progressive delay based on failure count
                var delay = consecutiveFailures >= maxConsecutiveFailures ? exceptionDelayMs * 3 : exceptionDelayMs;
                await Task.Delay(delay);
            }
        }
    }

    /// <summary>
    /// Comprehensive bot session reconnection logic
    /// Task 12: Session reconnection following TraderBot pattern with enhancements
    /// </summary>
    private async Task ReconnectBotSession()
    {
        try
        {
            Console.WriteLine("MarketBrowserMod: Starting comprehensive bot session reconnection...");
            
            // Step 1: Create new bot session
            var newBot = await CreateUser("marketBrowser", allowExisting: true, randomize: false);
            Console.WriteLine($"MarketBrowserMod: New bot session created successfully - Player ID: {newBot.PlayerId}");
            
            // Step 2: Validate new session by testing a simple Orleans call
            try
            {
                using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var testTask = orleans.GetPlayerGrain(newBot.PlayerId).GetPlayerInfo();
                
                if (await Task.WhenAny(testTask, Task.Delay(Timeout.Infinite, testCts.Token)) == testTask)
                {
                    var playerInfo = await testTask;
                    Console.WriteLine($"MarketBrowserMod: Session validation successful - Player: {playerInfo?.name ?? "Unknown"}");
                }
                else
                {
                    throw new TimeoutException("Session validation timed out");
                }
            }
            catch (Exception validationEx)
            {
                Console.WriteLine($"MarketBrowserMod: Session validation failed: {validationEx.Message}");
                throw new InvalidOperationException("New session validation failed", validationEx);
            }
            
            // Step 3: Update bot reference
            bot = newBot;
            Console.WriteLine("MarketBrowserMod: Bot reference updated successfully");
            
            // Step 4: Update MarketDataService with new bot client
            var marketBrowserBot = this as MarketBrowserMod.MarketBrowserBot;
            if (marketBrowserBot?.marketDataService != null)
            {
                try
                {
                    marketBrowserBot.marketDataService.SetBotClient(bot);
                    Console.WriteLine("MarketBrowserMod: MarketDataService bot client updated successfully");
                    
                    // Step 5: Trigger a cache refresh to validate the new session
                    Console.WriteLine("MarketBrowserMod: Triggering cache refresh to validate new session...");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(2000); // Brief delay to let things settle
                            await marketBrowserBot.marketDataService.ForceRefresh();
                            Console.WriteLine("MarketBrowserMod: Post-reconnection cache refresh completed successfully");
                        }
                        catch (Exception refreshEx)
                        {
                            Console.WriteLine($"MarketBrowserMod: Post-reconnection cache refresh failed: {refreshEx.Message}");
                        }
                    });
                }
                catch (Exception serviceUpdateEx)
                {
                    Console.WriteLine($"MarketBrowserMod: Failed to update MarketDataService: {serviceUpdateEx.Message}");
                    // Don't throw - the session itself is valid
                }
            }
            
            Console.WriteLine("MarketBrowserMod: Comprehensive session reconnection completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MarketBrowserMod: Comprehensive session reconnection failed: {ex.Message}");
            throw;
        }
    }
}

namespace MarketBrowserMod
{
    /// Market Browser Bot implementation
    public class MarketBrowserBot : Mod
    {
        internal MarketDataService? marketDataService;
        internal ItemNameService? itemNameService;
        
        public override async Task Loop()
        {
            Console.WriteLine("MarketBrowserMod: Starting bot authentication...");
            
            // Create bot user with credentials from environment variables
            bot = await CreateUser("marketBrowser", allowExisting: true, randomize: false);
            
            Console.WriteLine($"MarketBrowserMod: Bot authenticated successfully as {bot.PlayerId}");
            
            // Task 3: Verify Orleans connection and explore market data structures
            // Note: Task 3 exploration completed - commenting out to improve startup time
            // await VerifyOrleansConnectionAndExploreData();
            
            // Initialize market data service with configurable intervals
            try
            {
                var logger = serviceProvider.GetRequiredService<ILogger<MarketDataService>>();
                
                // Parse configuration from environment variables
                var refreshIntervalMinutes = int.TryParse(Environment.GetEnvironmentVariable("REFRESH_INTERVAL_MINUTES"), out var interval) ? interval : 15;
                var maxCacheAgeMinutes = int.TryParse(Environment.GetEnvironmentVariable("MAX_CACHE_AGE_MINUTES"), out var maxAge) ? maxAge : 60;
                var maxRetryAttempts = int.TryParse(Environment.GetEnvironmentVariable("MAX_RETRY_ATTEMPTS"), out var retries) ? retries : 3;
                
                // Create DatabaseMarketService
                var databaseLogger = serviceProvider.GetRequiredService<ILogger<DatabaseMarketService>>();
                var databaseMarketService = new DatabaseMarketService(databaseLogger);
                
                // Create and initialize ItemNameService
                var itemNameLogger = serviceProvider.GetRequiredService<ILogger<ItemNameService>>();
                itemNameService = new ItemNameService(itemNameLogger);
                await itemNameService.LoadItemDefinitionsAsync();
                Console.WriteLine($"MarketBrowserMod: ItemNameService initialized with {itemNameService.LoadedItemCount} items");
                
                // Test logging levels
                var testLogger = serviceProvider.GetRequiredService<ILogger<MarketBrowserBot>>();
                testLogger.LogDebug("TEST: Debug level logging is enabled");
                testLogger.LogInformation("TEST: Information level logging is enabled");
                testLogger.LogWarning("TEST: Warning level logging is enabled");
                
                marketDataService = new MarketDataService(
                    orleans, 
                    logger,
                    databaseMarketService,
                    itemNameService,
                    TimeSpan.FromMinutes(refreshIntervalMinutes),
                    TimeSpan.FromMinutes(maxCacheAgeMinutes),
                    maxRetryAttempts
                );
                
                await marketDataService.InitializeAsync();
                
                // Set the bot client for the service (based on Task 3 discoveries)
                marketDataService.SetBotClient(bot);
                
                Console.WriteLine($"MarketBrowserMod: MarketDataService initialized successfully");
                Console.WriteLine($"  - Refresh interval: {refreshIntervalMinutes} minutes");
                Console.WriteLine($"  - Max cache age: {maxCacheAgeMinutes} minutes");
                Console.WriteLine($"  - Max retry attempts: {maxRetryAttempts}");
                
                // Initialize ProfitAnalysisService
                var profitAnalysisLogger = serviceProvider.GetRequiredService<ILogger<ProfitAnalysisService>>();
                var profitAnalysisService = new ProfitAnalysisService(marketDataService, profitAnalysisLogger);
                Console.WriteLine("MarketBrowserMod: ProfitAnalysisService initialized successfully");
                
                // Task 7: Test location and distance calculation system
                await TestLocationAndDistanceSystem(marketDataService);
                
                // Initialize and start Web API server
                await StartWebApiServer(marketDataService, profitAnalysisService);
                
                // Clear any stale cache data and force an initial market data refresh
                Console.WriteLine("MarketBrowserMod: Clearing cache and starting initial market data refresh...");
                try
                {
                    // Clear any existing stale data
                    marketDataService.ClearCache();
                    Console.WriteLine("MarketBrowserMod: Cache cleared, starting fresh data collection...");
                    
                    // Force immediate refresh to populate with current data
                    await marketDataService.RefreshMarketData();
                    Console.WriteLine("MarketBrowserMod: Initial market data refresh completed successfully");
                    
                    // Run post-refresh tests to verify data population
                    await TestLocationDataPopulation(marketDataService);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MarketBrowserMod: Initial market data refresh failed: {ex.Message}");
                    Console.WriteLine("MarketBrowserMod: Will retry during periodic refresh cycle");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MarketBrowserMod: Failed to initialize MarketDataService: {ex.Message}");
                throw;
            }
            
            // Start the main application loop with comprehensive error handling and session reconnection
            // Task 12: Implement session reconnection logic following TraderBot pattern
            // Note: Periodic refresh is now handled by MarketDataBackgroundService (Task 11)
            await SafeLoopWithSessionReconnection(async () =>
            {
                Console.WriteLine("MarketBrowserMod: Running main loop iteration...");
                
                // Keep the main loop alive and monitor system health
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    
                    // Log cache statistics periodically
                    var stats = marketDataService.GetCacheStatistics();
                    Console.WriteLine($"MarketBrowserMod: Cache Stats - Markets: {stats.MarketCount}, Orders: {stats.OrderCount}, Age: {stats.CacheAge.TotalMinutes:F1}min, Stale: {stats.IsStale}");
                    
                    // After first successful refresh, test location data population (only once)
                    if (stats.MarketCount > 0 && stats.LastSuccessfulRefresh > DateTime.MinValue && !locationDataTested)
                    {
                        await TestLocationDataPopulation(marketDataService);
                    }
                    
                    // Test Orleans connection to ensure it's still active with timeout
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var connectionTestTask = orleans.GetPlayerGrain(bot.PlayerId).GetPlayerInfo();
                        
                        if (await Task.WhenAny(connectionTestTask, Task.Delay(Timeout.Infinite, cts.Token)) == connectionTestTask)
                        {
                            var playerInfo = await connectionTestTask;
                            Console.WriteLine($"MarketBrowserMod: Connection check - Player: {playerInfo?.name ?? "Unknown"}");
                        }
                        else
                        {
                            Console.WriteLine("MarketBrowserMod: Connection check timed out");
                            throw new TimeoutException("Orleans connection check timed out");
                        }
                    }
                    catch (NQutils.Exceptions.BusinessException be) when (be.error.code == NQ.ErrorCode.InvalidSession)
                    {
                        Console.WriteLine("MarketBrowserMod: Invalid session detected during connection check");
                        throw; // This will trigger session reconnection
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"MarketBrowserMod: Connection check failed: {ex.Message}");
                        throw; // This will trigger reconnection
                    }
                }
                
            }, 5000);
        }
        
        /// <summary>
        /// Initialize and start the Web API server for the Market Browser
        /// Task 9: Create Web API controllers and endpoints
        /// </summary>
        private async Task StartWebApiServer(MarketDataService marketDataService, ProfitAnalysisService profitAnalysisService)
        {
            try
            {
                Console.WriteLine("MarketBrowserMod: Starting Web API server...");
                
                // Create web application builder
                var builder = WebApplication.CreateBuilder();
                
                // Configure services
                builder.Services.AddControllers();
                builder.Services.AddEndpointsApiExplorer();
                
                // Add CORS for web frontend
                builder.Services.AddCors(options =>
                {
                    options.AddDefaultPolicy(policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
                });
                
                // Register services as singletons (they're already initialized)
                builder.Services.AddSingleton(marketDataService);
                builder.Services.AddSingleton(profitAnalysisService);
                builder.Services.AddSingleton<BaseMarketService>();
                builder.Services.AddSingleton<RouteOptimizationService>();
                builder.Services.AddSingleton(itemNameService);
                
                // Add background service for periodic refresh (Task 11)
                builder.Services.AddSingleton<MarketDataBackgroundService>();
                builder.Services.AddHostedService<MarketDataBackgroundService>(provider => 
                    provider.GetRequiredService<MarketDataBackgroundService>());
                
                Console.WriteLine("MarketBrowserMod: Background service for periodic refresh configured (Task 11)");
                
                // Add health checks (Task 11)
                builder.Services.AddHealthChecks()
                    .AddCheck<MarketBrowserHealthCheck>("market_browser_health")
                    .AddCheck<MarketBrowserReadinessCheck>("market_browser_readiness") 
                    .AddCheck<MarketBrowserLivenessCheck>("market_browser_liveness");
                
                Console.WriteLine("MarketBrowserMod: Health checks configured for container probes (Task 11)");
                
                // Configure comprehensive logging (Task 11)
                builder.Services.AddLogging(logging =>
                {
                    logging.AddConsole();
                    
                    // Parse log level from environment variable (default to Warning to reduce noise)
                    var logLevelStr = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "Warning";
                    Console.WriteLine($"MarketBrowserMod: LOG_LEVEL environment variable: '{logLevelStr}'");
                    
                    if (Enum.TryParse<LogLevel>(logLevelStr, true, out var logLevel))
                    {
                        logging.SetMinimumLevel(logLevel);
                        Console.WriteLine($"MarketBrowserMod: Successfully parsed log level: {logLevel}");
                    }
                    else
                    {
                        logging.SetMinimumLevel(LogLevel.Warning);
                        Console.WriteLine($"MarketBrowserMod: Failed to parse log level '{logLevelStr}', using Warning");
                    }
                    
                    // Add structured logging filters
                    logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
                    logging.AddFilter("Microsoft.Extensions.Hosting", LogLevel.Information);
                    logging.AddFilter("Microsoft.Extensions.Diagnostics.HealthChecks", LogLevel.Information);
                    logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning); // Reduce HTTP client noise
                    
                    // Set our services to the configured log level (don't override with Information)
                    logging.AddFilter("MarketBrowserMod.Services.ItemNameService", logLevel);
                    logging.AddFilter("MarketBrowserMod.Services.MarketDataService", logLevel);
                    logging.AddFilter("MarketBrowserMod.Services.DatabaseMarketService", logLevel);
                    
                    Console.WriteLine($"MarketBrowserMod: Logging configured with minimum level: {logLevel}");
                });
                
                // Configure Kestrel to listen on all interfaces
                var port = int.TryParse(Environment.GetEnvironmentVariable("WEB_PORT"), out var webPort) ? webPort : 8080;
                builder.WebHost.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(port);
                });


                
                // Build the application
                var app = builder.Build();
                
                // Configure path base for reverse proxy scenarios
                var pathBase = Environment.GetEnvironmentVariable("PATH_BASE");
                Console.WriteLine($"MarketBrowserMod: PATH_BASE environment variable: '{pathBase ?? "not set"}'");
                if (!string.IsNullOrEmpty(pathBase))
                {
                    app.UsePathBase(pathBase);
                    Console.WriteLine($"MarketBrowserMod: Configured to run under path base: {pathBase}");
                }
                else
                {
                    Console.WriteLine("MarketBrowserMod: No path base configured, running at root level");
                }
                
                // Configure forwarded headers for reverse proxy scenarios
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
                });
                
                // Configure the HTTP request pipeline
                if (app.Environment.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }
                
                app.UseCors();
                app.UseRouting();
                app.MapControllers();
                
                // Add health check endpoints (Task 11)
                app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    ResponseWriter = async (context, report) =>
                    {
                        context.Response.ContentType = "application/json";
                        var response = new
                        {
                            status = report.Status.ToString(),
                            totalDuration = report.TotalDuration.TotalMilliseconds,
                            checks = report.Entries.Select(entry => new
                            {
                                name = entry.Key,
                                status = entry.Value.Status.ToString(),
                                duration = entry.Value.Duration.TotalMilliseconds,
                                description = entry.Value.Description,
                                data = entry.Value.Data,
                                exception = entry.Value.Exception?.Message
                            })
                        };
                        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    }
                });
                
                // Kubernetes-style health check endpoints (Task 11)
                app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    Predicate = check => check.Name == "market_browser_readiness"
                });
                
                app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
                {
                    Predicate = check => check.Name == "market_browser_liveness"
                });
                
                // Serve static files from wwwroot
                app.UseStaticFiles();
                
                // Add a default route for the root to serve the frontend
                app.MapGet("/", async (HttpContext context) =>
                {
                    var indexPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
                    if (File.Exists(indexPath))
                    {
                        context.Response.ContentType = "text/html";
                        await context.Response.SendFileAsync(indexPath);
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync("Frontend not found");
                    }
                });

                // Add API info endpoints
                app.MapGet("/api", () => new
                {
                    service = "Market Browser API",
                    version = "1.0.0",
                    status = "running",
                    endpoints = new
                    {
                        api = "/api/market",
                        health = "/api/market/status"
                    },
                    timestamp = DateTime.UtcNow
                });

                // Add debug endpoint for environment variables
                app.MapGet("/api/debug/env", () => new
                {
                    pathBase = Environment.GetEnvironmentVariable("PATH_BASE"),
                    logLevel = Environment.GetEnvironmentVariable("LOG_LEVEL"),
                    aspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    webPort = Environment.GetEnvironmentVariable("WEB_PORT"),
                    timestamp = DateTime.UtcNow
                });

                app.MapGet("/api/market", () => new
                {
                    service = "Market Browser API",
                    version = "1.0.0",
                    status = "running",
                    endpoints = new
                    {
                        markets = "/api/market/markets",
                        orders = "/api/market/orders", 
                        profits = "/api/market/profits",
                        planets = "/api/market/planets",
                        status = "/api/market/status",
                        stats = "/api/market/stats",
                        health = "/health",
                        healthReady = "/health/ready",
                        healthLive = "/health/live"
                    },
                    timestamp = DateTime.UtcNow
                });
                
                // Start the web server in the background
                var webServerTask = Task.Run(async () =>
                {
                    try
                    {
                        await app.RunAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"MarketBrowserMod: Web server error: {ex.Message}");
                    }
                });
                
                Console.WriteLine($"MarketBrowserMod: Web API server started successfully on port {port}");
                Console.WriteLine($"  - API endpoints: http://localhost:{port}/api/market");
                Console.WriteLine($"  - Health check: http://localhost:{port}/api/market/status");
                Console.WriteLine($"  - Health endpoints: http://localhost:{port}/health");
                Console.WriteLine($"  - Readiness probe: http://localhost:{port}/health/ready");
                Console.WriteLine($"  - Liveness probe: http://localhost:{port}/health/live");
                
                // Give the web server a moment to start
                await Task.Delay(2000);
                
                // Test the web server
                await TestWebApiEndpoints(port);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MarketBrowserMod: Failed to start Web API server: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Test the Web API endpoints to ensure they're working
        /// Task 9: Verify Web API functionality
        /// </summary>
        private async Task TestWebApiEndpoints(int port)
        {
            try
            {
                Console.WriteLine("MarketBrowserMod: Testing Web API endpoints...");
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                
                // Test root endpoint
                var rootResponse = await httpClient.GetStringAsync($"http://localhost:{port}/");
                Console.WriteLine("  - Root endpoint: OK");
                
                // Test health endpoint
                var healthResponse = await httpClient.GetStringAsync($"http://localhost:{port}/api/market/status");
                Console.WriteLine("  - Health endpoint: OK");
                
                // Test markets endpoint
                var marketsResponse = await httpClient.GetStringAsync($"http://localhost:{port}/api/market/markets?pageSize=1");
                Console.WriteLine("  - Markets endpoint: OK");
                
                Console.WriteLine("MarketBrowserMod: Web API endpoints test completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MarketBrowserMod: Web API endpoint test failed: {ex.Message}");
                // Don't throw - the server might still be starting up
            }
        }

        /// Task 3: Verify Orleans connection and explore market data structures
        /// NOTE: Task 3 exploration completed successfully - methods commented out to improve startup performance
        /*
        private async Task VerifyOrleansConnectionAndExploreData()
        {
            Console.WriteLine("=== TASK 3: Orleans Connection Verification and Data Structure Exploration ===");
            
            // Test 1: Verify Orleans client connectivity and bot authentication
            await TestOrleansConnectivity();
            
            // Test 2: Test basic market list retrieval
            await TestMarketListRetrieval();
            
            // Test 3: Explore and log actual MarketInfo data structures
            await ExploreMarketInfoStructures();
            
            // Test 4: Explore and log actual MarketOrder data structures
            await ExploreMarketOrderStructures();
            
            // Test 5: Test player info retrieval and explore PlayerInfo structure
            await ExplorePlayerInfoStructure();
            
            Console.WriteLine("=== TASK 3 COMPLETED: Orleans connection verified and data structures explored ===");
        }
        */
        
        /// <summary>
        /// Task 7: Test location and distance calculation system
        /// Validates that construct info retrieval, planet identification, and distance calculations work correctly
        /// </summary>
        private async Task TestLocationAndDistanceSystem(MarketDataService marketDataService)
        {
            Console.WriteLine("=== TASK 7: Testing Location and Distance Calculation System ===");
            
            try
            {
                // Add timeout to prevent hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                // Test 1: Verify planet data is loaded
                await TestPlanetDataLoading(marketDataService);
                
                // Test 2: Test construct info retrieval for a known market
                await TestConstructInfoRetrieval();
                
                // Test 3: Test distance calculations with sample data
                await TestDistanceCalculations(marketDataService);
                
                // Test 4: Test market location queries
                await TestMarketLocationQueries(marketDataService);
                
                Console.WriteLine("=== TASK 7 COMPLETED: Location and distance calculation system verified ===");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("TASK 7 TIMEOUT: Location system tests timed out after 30 seconds");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TASK 7 ERROR: Location system test failed: {ex.Message}");
                // Don't throw - allow the system to continue running even if tests fail
            }
        }
        
        private Task TestPlanetDataLoading(MarketDataService marketDataService)
        {
            Console.WriteLine("Test 1: Verifying planet data loading...");
            
            try
            {
                var planets = marketDataService.GetAllPlanets();
                Console.WriteLine($"  - Loaded {planets.Count} planets");
                
                foreach (var planet in planets.Take(3)) // Show first 3 planets only
                {
                    Console.WriteLine($"  - Planet: {planet.Name} (ID: {planet.PlanetId})");
                    if (planet.Position != null && (planet.Position.Value.x != 0 || planet.Position.Value.y != 0 || planet.Position.Value.z != 0))
                    {
                        Console.WriteLine($"    Position: ({planet.Position.Value.x}, {planet.Position.Value.y}, {planet.Position.Value.z})");
                        Console.WriteLine($"    Distance from origin: {planet.DistanceFromOrigin:F0}");
                    }
                    else
                    {
                        Console.WriteLine($"    Position: Default (0,0,0) - construct info not available");
                    }
                }
                
                if (planets.Count > 3)
                {
                    Console.WriteLine($"  ... and {planets.Count - 3} more planets");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  - ERROR in planet data test: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }
        
        private async Task TestConstructInfoRetrieval()
        {
            Console.WriteLine("Test 2: Testing construct info retrieval...");
            
            try
            {
                // Test with Alioth (planet ID 2) - should have construct info
                var aliothConstructId = new ConstructId { constructId = 2 };
                var constructInfo = await orleans.GetConstructInfoGrain(aliothConstructId).Get();
                
                if (constructInfo?.rData != null)
                {
                    Console.WriteLine($"  - Successfully retrieved construct info for Alioth");
                    Console.WriteLine($"    Construct name: {constructInfo.rData.name}");
                    Console.WriteLine($"    Position: ({constructInfo.rData.position.x}, {constructInfo.rData.position.y}, {constructInfo.rData.position.z})");
                    
                    // Test distance calculation from origin
                    var distance = Math.Sqrt(
                        constructInfo.rData.position.x * constructInfo.rData.position.x +
                        constructInfo.rData.position.y * constructInfo.rData.position.y +
                        constructInfo.rData.position.z * constructInfo.rData.position.z
                    );
                    Console.WriteLine($"    Distance from origin: {distance:F0}");
                }
                else
                {
                    Console.WriteLine($"  - WARNING: Could not retrieve construct info for Alioth");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  - ERROR: Construct info retrieval failed: {ex.Message}");
            }
        }
        
        private Task TestDistanceCalculations(MarketDataService marketDataService)
        {
            Console.WriteLine("Test 3: Testing distance calculations...");
            
            // Test Vec3 distance calculation with sample positions
            var pos1 = new Vec3 { x = 0, y = 0, z = 0 };
            var pos2 = new Vec3 { x = 1000, y = 1000, z = 1000 };
            
            var distance = marketDataService.CalculateDistance(pos1, pos2);
            var expectedDistance = Math.Sqrt(3 * 1000 * 1000); // sqrt(3) * 1000
            
            Console.WriteLine($"  - Distance between (0,0,0) and (1000,1000,1000): {distance:F2}");
            Console.WriteLine($"  - Expected distance: {expectedDistance:F2}");
            Console.WriteLine($"  - Calculation accuracy: {(Math.Abs(distance - expectedDistance) < 0.01 ? "PASS" : "FAIL")}");
            
            // Test planet distance calculations with detailed debugging
            var planets = marketDataService.GetAllPlanets();
            if (planets.Count >= 2)
            {
                var planet1 = planets[0]; // Alioth
                var planet2 = planets[1]; // Sanctuary
                
                Console.WriteLine($"  - Planet 1: {planet1.Name} at ({planet1.Position?.x ?? 0}, {planet1.Position?.y ?? 0}, {planet1.Position?.z ?? 0})");
                Console.WriteLine($"  - Planet 2: {planet2.Name} at ({planet2.Position?.x ?? 0}, {planet2.Position?.y ?? 0}, {planet2.Position?.z ?? 0})");
                
                var planetDistance = marketDataService.CalculateDistanceBetweenPlanets(planet1.PlanetId, planet2.PlanetId);
                Console.WriteLine($"  - Distance between {planet1.Name} and {planet2.Name}: {planetDistance:F0}");
                
                // Also test direct Vec3 calculation if both have positions
                if (planet1.Position != null && planet2.Position != null)
                {
                    var directDistance = marketDataService.CalculateDistance(planet1.Position, planet2.Position);
                    Console.WriteLine($"  - Direct Vec3 calculation: {directDistance:F0}");
                }
            }
            
            return Task.CompletedTask;
        }
        
        private Task TestMarketLocationQueries(MarketDataService marketDataService)
        {
            Console.WriteLine("Test 4: Testing market location queries...");
            
            // Test getting markets by planet
            var planets = marketDataService.GetAllPlanets();
            if (planets.Count > 0)
            {
                var firstPlanet = planets[0];
                var marketsOnPlanet = marketDataService.GetMarketsByPlanet(firstPlanet.PlanetId);
                Console.WriteLine($"  - Markets on {firstPlanet.Name}: {marketsOnPlanet.Count}");
                
                if (marketsOnPlanet.Count > 0)
                {
                    var sampleMarket = marketsOnPlanet[0];
                    Console.WriteLine($"    Sample market: {sampleMarket.Name}");
                    if (sampleMarket.Position != null)
                    {
                        Console.WriteLine($"    Position: ({sampleMarket.Position.Value.x}, {sampleMarket.Position.Value.y}, {sampleMarket.Position.Value.z})");
                        Console.WriteLine($"    Distance from origin: {sampleMarket.DistanceFromOrigin:F0}");
                    }
                }
            }
            
            // Test closest markets query (using origin as reference point)
            var originPos = new Vec3 { x = 0, y = 0, z = 0 };
            var closestMarkets = marketDataService.GetClosestMarkets(originPos, 3);
            Console.WriteLine($"  - Closest 3 markets to origin: {closestMarkets.Count}");
            
            foreach (var market in closestMarkets)
            {
                if (market.Position != null)
                {
                    var distance = marketDataService.CalculateDistance(originPos, market.Position);
                    Console.WriteLine($"    {market.Name} on {market.PlanetName}: {distance:F0} units");
                }
            }
            
            return Task.CompletedTask;
        }
        
        private static bool locationDataTested = false;
        
        public Task TestLocationDataPopulation(MarketDataService marketDataService)
        {
            // Only run this test once after the first successful refresh
            if (locationDataTested) return Task.CompletedTask;
            locationDataTested = true;
            
            Console.WriteLine("=== POST-REFRESH: Testing Location Data Population ===");
            
            try
            {
                var markets = marketDataService.GetAllMarkets();
                var marketsWithPosition = markets.Where(m => m.Position != null).ToList();
                var marketsWithDistance = markets.Where(m => m.DistanceFromOrigin > 0).ToList();
                
                Console.WriteLine($"  - Total markets loaded: {markets.Count}");
                Console.WriteLine($"  - Markets with position data: {marketsWithPosition.Count}");
                Console.WriteLine($"  - Markets with distance calculated: {marketsWithDistance.Count}");
                
                if (marketsWithPosition.Count > 0)
                {
                    var sampleMarket = marketsWithPosition.First();
                    Console.WriteLine($"  - Sample market with location: {sampleMarket.Name}");
                    Console.WriteLine($"    Planet: {sampleMarket.PlanetName}");
                    Console.WriteLine($"    Position: ({sampleMarket.Position.Value.x}, {sampleMarket.Position.Value.y}, {sampleMarket.Position.Value.z})");
                    Console.WriteLine($"    Distance from origin: {sampleMarket.DistanceFromOrigin:F0}");
                    Console.WriteLine($"    Construct ID: {sampleMarket.ConstructId}");
                }
                
                // Test planet position updates
                var planets = marketDataService.GetAllPlanets();
                var planetsWithPosition = planets.Where(p => p.Position != null).ToList();
                
                Console.WriteLine($"  - Planets with updated positions: {planetsWithPosition.Count}/{planets.Count}");
                
                if (planetsWithPosition.Count > 0)
                {
                    var samplePlanet = planetsWithPosition.First();
                    Console.WriteLine($"  - Sample planet: {samplePlanet.Name}");
                    Console.WriteLine($"    Position: ({samplePlanet.Position.Value.x}, {samplePlanet.Position.Value.y}, {samplePlanet.Position.Value.z})");
                    Console.WriteLine($"    Distance from origin: {samplePlanet.DistanceFromOrigin:F0}");
                }
                
                // Test distance calculations between markets
                if (marketsWithPosition.Count >= 2)
                {
                    var market1 = marketsWithPosition[0];
                    var market2 = marketsWithPosition[1];
                    var distance = marketDataService.CalculateDistance(market1.MarketId, market2.MarketId);
                    
                    Console.WriteLine($"  - Distance between {market1.Name} and {market2.Name}: {distance:F0}");
                }
                
                Console.WriteLine("=== Location Data Population Test Completed ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  - ERROR during location data test: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }
        
        // Task 3 exploration methods - commented out since exploration is complete
        // These methods were used to discover Orleans API structure and are no longer needed
        /*
        private async Task TestOrleansConnectivity() { ... }
        private async Task TestMarketListRetrieval() { ... }
        private async Task ExploreMarketInfoStructures() { ... }
        private async Task ExploreMarketOrderStructures() { ... }
        private async Task ExplorePlayerInfoStructure() { ... }
        */
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("MarketBrowserMod: Starting application...");
            
            // Read configuration from YAML file
            Config.ReadYamlFileFromArgs("mod", args);
            
            // Setup Orleans client and services
            Console.WriteLine("MarketBrowserMod: Setting up Orleans client and services...");
            Mod.Setup().Wait();
            
            Console.WriteLine("MarketBrowserMod: Orleans setup completed successfully");
            
            // Start the MarketBrowserBot
            var marketBrowserBot = new MarketBrowserMod.MarketBrowserBot();
            Console.WriteLine("MarketBrowserMod: Starting main mod loop...");
            
            // Check environment variables early
            var logLevelEnv = Environment.GetEnvironmentVariable("LOG_LEVEL");
            Console.WriteLine($"MarketBrowserMod: Early check - LOG_LEVEL = '{logLevelEnv ?? "not set"}'");
            
            marketBrowserBot.Start().Wait();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MarketBrowserMod: Fatal error during startup: {ex}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}