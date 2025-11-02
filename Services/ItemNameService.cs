using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace MarketBrowserMod.Services
{
    /// <summary>
    /// Service for resolving item names from the items.yaml file
    /// Provides user-friendly display names instead of internal item names
    /// </summary>
    public class ItemNameService
    {
        private readonly ILogger<ItemNameService> logger;
        private readonly Dictionary<string, string> itemDisplayNames = new();
        private readonly object lockObject = new object();
        private bool isLoaded = false;

        public ItemNameService(ILogger<ItemNameService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Load item definitions from items.yaml file
        /// </summary>
        public async Task LoadItemDefinitionsAsync()
        {
            try
            {
                logger.LogInformation("Loading item definitions from items.yaml...");

                var yamlFilePath = "items.yaml";
                var fullPath = Path.GetFullPath(yamlFilePath);
                logger.LogInformation($"Looking for items.yaml at: {fullPath}");
                
                if (!File.Exists(yamlFilePath))
                {
                    logger.LogError($"items.yaml file not found at {yamlFilePath} (full path: {fullPath})");
                    logger.LogInformation($"Current working directory: {Directory.GetCurrentDirectory()}");
                    logger.LogInformation($"Files in current directory: {string.Join(", ", Directory.GetFiles(".", "*.yaml"))}");
                    return;
                }

                var yamlContent = await File.ReadAllTextAsync(yamlFilePath);
                
                // Parse YAML content
                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                // Split the YAML content by document separators (---)
                var documents = yamlContent.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);
                logger.LogDebug($"Found {documents.Length} YAML documents to process");

                lock (lockObject)
                {
                    itemDisplayNames.Clear();

                    foreach (var document in documents)
                    {
                        if (string.IsNullOrWhiteSpace(document))
                            continue;

                        try
                        {
                            var itemData = deserializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(document);
                            
                            if (itemData != null)
                            {
                                foreach (var (itemKey, itemProperties) in itemData)
                                {
                                    if (itemProperties != null && itemProperties.TryGetValue("displayName", out var displayNameObj))
                                    {
                                        var displayName = displayNameObj?.ToString();
                                        if (!string.IsNullOrEmpty(displayName))
                                        {
                                            itemDisplayNames[itemKey] = displayName;
                                            logger.LogDebug($"Loaded item: {itemKey} -> {displayName}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, $"Failed to parse YAML document (this is normal for some document types): {ex.Message}");
                            // Continue processing other documents - some may not be item definitions
                        }
                    }

                    isLoaded = true;
                }

                logger.LogInformation($"Loaded {itemDisplayNames.Count} item definitions from items.yaml");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load item definitions from items.yaml");
            }
        }

        /// <summary>
        /// Get the display name for an item, falling back to the internal name if not found
        /// </summary>
        public string GetDisplayName(string itemKey)
        {
            if (string.IsNullOrEmpty(itemKey))
                return "Unknown Item";

            lock (lockObject)
            {
                if (!isLoaded)
                {
                    logger.LogDebug("Item definitions not loaded yet, using internal name");
                    return itemKey;
                }

                if (itemDisplayNames.TryGetValue(itemKey, out var displayName))
                {
                    return displayName;
                }

                // Fallback to internal name (only log if debug level is enabled)
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug($"Display name not found for item: {itemKey} (have {itemDisplayNames.Count} items loaded)");
                }
                return itemKey;
            }
        }

        /// <summary>
        /// Check if item definitions are loaded
        /// </summary>
        public bool IsLoaded => isLoaded;

        /// <summary>
        /// Get the total number of loaded item definitions
        /// </summary>
        public int LoadedItemCount
        {
            get
            {
                lock (lockObject)
                {
                    return itemDisplayNames.Count;
                }
            }
        }
    }
}