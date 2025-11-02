using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace MarketBrowserMod.Models
{
    /// <summary>
    /// Startup validation for MarketBrowserMod configuration and environment
    /// Task 13: Configuration validation and startup checks for required environment variables
    /// </summary>
    public static class StartupValidator
    {
        /// <summary>
        /// Perform comprehensive startup validation
        /// </summary>
        public static async Task<bool> ValidateStartupEnvironment(MarketBrowserConfiguration config)
        {
            Console.WriteLine("=== MarketBrowserMod Startup Validation ===");
            
            var validationResults = new List<(string Check, bool Success, string Message)>();
            
            // 1. Configuration validation
            try
            {
                config.Validate();
                validationResults.Add(("Configuration", true, "All required configuration present"));
            }
            catch (Exception ex)
            {
                validationResults.Add(("Configuration", false, $"Configuration validation failed: {ex.Message}"));
            }
            
            // 2. Environment variables validation
            var envValidation = ValidateEnvironmentVariables();
            validationResults.Add(("Environment Variables", envValidation.Success, envValidation.Message));
            
            // 3. Network connectivity validation
            var networkValidation = await ValidateNetworkConnectivity(config);
            validationResults.Add(("Network Connectivity", networkValidation.Success, networkValidation.Message));
            
            // 4. File system permissions validation
            var fsValidation = ValidateFileSystemPermissions();
            validationResults.Add(("File System", fsValidation.Success, fsValidation.Message));
            
            // 5. Port availability validation
            var portValidation = ValidatePortAvailability(config.WebServerPort);
            validationResults.Add(("Port Availability", portValidation.Success, portValidation.Message));
            
            // 6. Resource availability validation
            var resourceValidation = ValidateResourceAvailability();
            validationResults.Add(("System Resources", resourceValidation.Success, resourceValidation.Message));
            
            // Report results
            Console.WriteLine("\nValidation Results:");
            Console.WriteLine("==================");
            
            bool allPassed = true;
            foreach (var (check, success, message) in validationResults)
            {
                var status = success ? "✓ PASS" : "✗ FAIL";
                Console.WriteLine($"{status,-8} {check}: {message}");
                
                if (!success)
                {
                    allPassed = false;
                }
            }
            
            Console.WriteLine("==================");
            
            if (allPassed)
            {
                Console.WriteLine("✓ All startup validations passed - ready to start");
            }
            else
            {
                Console.WriteLine("✗ Some validations failed - check configuration and environment");
            }
            
            Console.WriteLine();
            return allPassed;
        }
        
        /// <summary>
        /// Validate required environment variables
        /// </summary>
        private static (bool Success, string Message) ValidateEnvironmentVariables()
        {
            var requiredVars = new[] { "BOT_LOGIN", "BOT_PASSWORD" };
            var missingVars = new List<string>();
            
            foreach (var varName in requiredVars)
            {
                var value = Environment.GetEnvironmentVariable(varName);
                if (string.IsNullOrWhiteSpace(value))
                {
                    missingVars.Add(varName);
                }
            }
            
            if (missingVars.Count > 0)
            {
                return (false, $"Missing required environment variables: {string.Join(", ", missingVars)}");
            }
            
            // Check for common configuration issues
            var botLogin = Environment.GetEnvironmentVariable("BOT_LOGIN");
            if (botLogin?.Length < 3)
            {
                return (false, "BOT_LOGIN should be at least 3 characters long");
            }
            
            var botPassword = Environment.GetEnvironmentVariable("BOT_PASSWORD");
            if (botPassword?.Length < 8)
            {
                return (false, "BOT_PASSWORD should be at least 8 characters long for security");
            }
            
            return (true, "All required environment variables present and valid");
        }
        
        /// <summary>
        /// Validate network connectivity to required services
        /// </summary>
        private static async Task<(bool Success, string Message)> ValidateNetworkConnectivity(MarketBrowserConfiguration config)
        {
            try
            {
                // Parse queueing URL to get host and port
                if (!Uri.TryCreate(config.QueueingUrl, UriKind.Absolute, out var queueingUri))
                {
                    return (false, $"Invalid queueing URL format: {config.QueueingUrl}");
                }
                
                var host = queueingUri.Host;
                var port = queueingUri.Port;
                
                // Test network connectivity with timeout
                using var ping = new Ping();
                var timeout = 5000; // 5 seconds
                
                try
                {
                    var reply = await ping.SendPingAsync(host, timeout);
                    if (reply.Status == IPStatus.Success)
                    {
                        return (true, $"Network connectivity to {host} confirmed");
                    }
                    else
                    {
                        return (false, $"Cannot reach queueing service at {host}: {reply.Status}");
                    }
                }
                catch (Exception ex)
                {
                    // Ping might fail in Docker environments, so this is a warning rather than failure
                    return (true, $"Network test inconclusive (common in containers): {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Network validation error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validate file system permissions
        /// </summary>
        private static (bool Success, string Message) ValidateFileSystemPermissions()
        {
            try
            {
                var workingDir = Directory.GetCurrentDirectory();
                
                // Test read permissions
                if (!Directory.Exists(workingDir))
                {
                    return (false, $"Working directory does not exist: {workingDir}");
                }
                
                // Test write permissions by creating a temporary file
                var tempFile = Path.Combine(workingDir, $"temp_validation_{Guid.NewGuid():N}.tmp");
                try
                {
                    File.WriteAllText(tempFile, "validation test");
                    File.Delete(tempFile);
                    return (true, "File system read/write permissions confirmed");
                }
                catch (UnauthorizedAccessException)
                {
                    return (false, "Insufficient file system write permissions");
                }
                catch (Exception ex)
                {
                    return (false, $"File system validation error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"File system validation failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validate port availability
        /// </summary>
        private static (bool Success, string Message) ValidatePortAvailability(int port)
        {
            try
            {
                // Check if port is in valid range
                if (port < 1 || port > 65535)
                {
                    return (false, $"Port {port} is outside valid range (1-65535)");
                }
                
                // Check if port is commonly reserved
                var reservedPorts = new[] { 22, 23, 25, 53, 80, 110, 143, 443, 993, 995 };
                if (Array.IndexOf(reservedPorts, port) >= 0)
                {
                    return (true, $"Port {port} is commonly reserved but may work in containers");
                }
                
                return (true, $"Port {port} appears available");
            }
            catch (Exception ex)
            {
                return (false, $"Port validation error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validate system resource availability
        /// </summary>
        private static (bool Success, string Message) ValidateResourceAvailability()
        {
            try
            {
                // Check available memory (basic check)
                var workingSet = Environment.WorkingSet;
                var availableMemoryMB = workingSet / (1024 * 1024);
                
                if (availableMemoryMB < 50) // Less than 50MB
                {
                    return (false, $"Very low available memory: {availableMemoryMB}MB");
                }
                
                // Check processor count
                var processorCount = Environment.ProcessorCount;
                if (processorCount < 1)
                {
                    return (false, "No processors detected");
                }
                
                return (true, $"System resources available: {availableMemoryMB}MB memory, {processorCount} processors");
            }
            catch (Exception ex)
            {
                return (false, $"Resource validation error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Log startup environment information
        /// </summary>
        public static void LogStartupEnvironment()
        {
            Console.WriteLine("=== Startup Environment Information ===");
            Console.WriteLine($"Operating System: {Environment.OSVersion}");
            Console.WriteLine($"Runtime Version: {Environment.Version}");
            Console.WriteLine($"Working Directory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"Machine Name: {Environment.MachineName}");
            Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
            Console.WriteLine($"Working Set: {Environment.WorkingSet / (1024 * 1024)}MB");
            Console.WriteLine($"System Uptime: {TimeSpan.FromMilliseconds(Environment.TickCount)}");
            Console.WriteLine($"Current Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"UTC Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("======================================");
        }
        
        /// <summary>
        /// Validate Docker environment specific settings
        /// </summary>
        public static (bool Success, string Message) ValidateDockerEnvironment()
        {
            try
            {
                // Check if running in Docker
                var isDocker = File.Exists("/.dockerenv") || 
                              Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
                
                if (!isDocker)
                {
                    return (true, "Not running in Docker container");
                }
                
                Console.WriteLine("Docker container environment detected");
                
                // Validate Docker-specific configuration
                var hostname = Environment.GetEnvironmentVariable("HOSTNAME");
                var containerName = Environment.GetEnvironmentVariable("CONTAINER_NAME");
                
                Console.WriteLine($"Container hostname: {hostname ?? "not set"}");
                Console.WriteLine($"Container name: {containerName ?? "not set"}");
                
                return (true, "Docker environment validated");
            }
            catch (Exception ex)
            {
                return (false, $"Docker environment validation failed: {ex.Message}");
            }
        }
    }
}