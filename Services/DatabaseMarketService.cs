using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketBrowserMod.Models;
using NQ;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MarketBrowserMod.Services
{
    /// <summary>
    /// Database-driven market service for accurate planet identification and distance calculation
    /// Uses direct PostgreSQL access to query the database chain: market_order -> market -> element -> construct -> base_id (planet)
    /// </summary>
    public class DatabaseMarketService
    {
        private readonly string connectionString;
        private readonly ILogger<DatabaseMarketService> logger;

        public DatabaseMarketService(ILogger<DatabaseMarketService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // PostgreSQL connection string for the dual universe database
            // Based on docker-compose.yml: postgres at 10.5.0.9, user: dual, password: dual, database: dual
            this.connectionString = "Host=10.5.0.9;Database=dual;Username=dual;Password=dual;Port=5432";
        }

        /// <summary>
        /// Get market information with correct planet identification using direct PostgreSQL queries
        /// Implements the database chain: market -> element -> construct -> base_id (planet)
        /// </summary>
        public async Task<Dictionary<ulong, MarketLocationInfo>> GetMarketLocationInfoAsync()
        {
            try
            {
                logger.LogInformation("Retrieving market location information from PostgreSQL database...");

                var marketLocationInfo = new Dictionary<ulong, MarketLocationInfo>();

                // First, let's count total markets to see if we're missing any
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                
                using var countCommand = new NpgsqlCommand("SELECT COUNT(*) FROM market", connection);
                var totalMarkets = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                logger.LogInformation($"Total markets in database: {totalMarkets}");

                // Recursive query to traverse construct hierarchy and sum relative positions to get absolute market positions
                // Accumulates relative positions from market -> parent -> ... -> planet/root, then adds planet/root absolute position
                var query = @"
                    WITH RECURSIVE construct_hierarchy AS (
                        -- Base case: start with market constructs
                        SELECT 
                            m.id as market_id,
                            m.name as market_name,
                            m.element_id,
                            e.construct_id as market_construct_id,
                            c.id as current_construct_id,
                            c.base_id,
                            c.position_x as rel_pos_x,
                            c.position_y as rel_pos_y,
                            c.position_z as rel_pos_z,
                            -- Accumulate relative positions as we traverse up the hierarchy
                            c.position_x as accumulated_x,
                            c.position_y as accumulated_y,
                            c.position_z as accumulated_z,
                            0 as depth
                        FROM market m
                        LEFT JOIN element e ON m.element_id = e.id
                        LEFT JOIN construct c ON e.construct_id = c.id
                        WHERE e.construct_id IS NOT NULL AND c.id IS NOT NULL
                        
                        UNION ALL
                        
                        -- Recursive case: follow base_id chain until we reach root (base_id IS NULL or 0 or planet 1-100)
                        SELECT 
                            ch.market_id,
                            ch.market_name,
                            ch.element_id,
                            ch.market_construct_id,
                            c.id as current_construct_id,
                            c.base_id,
                            c.position_x as rel_pos_x,
                            c.position_y as rel_pos_y,
                            c.position_z as rel_pos_z,
                            -- Add current construct's relative position to accumulated positions
                            ch.accumulated_x + c.position_x as accumulated_x,
                            ch.accumulated_y + c.position_y as accumulated_y,
                            ch.accumulated_z + c.position_z as accumulated_z,
                            ch.depth + 1
                        FROM construct_hierarchy ch
                        JOIN construct c ON ch.base_id = c.id
                        WHERE ch.base_id IS NOT NULL 
                        AND ch.base_id != 0
                        AND ch.base_id NOT BETWEEN 1 AND 100  -- Continue until we reach a planet or space
                        AND ch.depth < 10  -- Prevent infinite loops
                    )
                    SELECT DISTINCT
                        ch.market_id,
                        ch.market_name,
                        ch.element_id,
                        ch.market_construct_id as construct_id,
                        -- Planet ID: if base_id is NULL or 0, it's space. If between 1-100, it's that planet ID
                        CASE 
                            WHEN ch.base_id IS NULL OR ch.base_id = 0 THEN 0
                            WHEN ch.base_id BETWEEN 1 AND 100 THEN ch.base_id
                            ELSE 0
                        END as planet_id,
                        -- Calculate absolute position: accumulated relative positions + planet/root absolute position
                        -- For planets: accumulated includes all relative positions (market + intermediate constructs)
                        --             but NOT the planet's absolute position, so we add it
                        -- For space stations: accumulated includes all relative positions up to the parent of root
                        --                     When base_id IS NULL, current_construct_id IS the root construct
                        --                     We need to get the root's absolute position and add it
                        CASE 
                            WHEN ch.base_id BETWEEN 1 AND 100 THEN ch.accumulated_x + p.position_x
                            WHEN ch.base_id IS NULL OR ch.base_id = 0 THEN 
                                ch.accumulated_x + COALESCE(root_pos.position_x, 0)
                            ELSE ch.accumulated_x
                        END as position_x,
                        CASE 
                            WHEN ch.base_id BETWEEN 1 AND 100 THEN ch.accumulated_y + p.position_y
                            WHEN ch.base_id IS NULL OR ch.base_id = 0 THEN 
                                ch.accumulated_y + COALESCE(root_pos.position_y, 0)
                            ELSE ch.accumulated_y
                        END as position_y,
                        CASE 
                            WHEN ch.base_id BETWEEN 1 AND 100 THEN ch.accumulated_z + p.position_z
                            WHEN ch.base_id IS NULL OR ch.base_id = 0 THEN 
                                ch.accumulated_z + COALESCE(root_pos.position_z, 0)
                            ELSE ch.accumulated_z
                        END as position_z,
                        COALESCE(p.name, 
                            CASE 
                                WHEN ch.base_id IS NULL OR ch.base_id = 0 THEN 'Space Stations'
                                WHEN ch.base_id BETWEEN 1 AND 100 THEN CONCAT('Planet ', ch.base_id)
                                ELSE 'Unknown Location'
                            END
                        ) as planet_name
                    FROM construct_hierarchy ch
                    LEFT JOIN construct p ON ch.base_id = p.id AND p.id BETWEEN 1 AND 100
                    -- For space stations (base_id IS NULL), current_construct_id IS the root construct with absolute position
                    LEFT JOIN construct root_pos ON ch.current_construct_id = root_pos.id 
                        AND (ch.base_id IS NULL OR ch.base_id = 0)
                    WHERE (ch.base_id IS NULL OR ch.base_id = 0 OR ch.base_id BETWEEN 1 AND 100)  -- Only final destinations (root/parent)
                    ORDER BY ch.market_id";

                // Execute the main query first
                using (var command = new NpgsqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var marketId = Convert.ToUInt64(reader["market_id"]);
                        var marketName = reader["market_name"]?.ToString() ?? $"Market {marketId}";
                        var elementId = Convert.ToUInt64(reader["element_id"]);
                        var constructId = Convert.ToUInt64(reader["construct_id"]);
                        var planetId = Convert.ToUInt64(reader["planet_id"]);
                        var planetName = reader["planet_name"]?.ToString() ?? (planetId == 0 ? "Space Stations" : $"Planet {planetId}");
                        
                        var position = new Vec3
                        {
                            x = Convert.ToDouble(reader["position_x"]),
                            y = Convert.ToDouble(reader["position_y"]),
                            z = Convert.ToDouble(reader["position_z"])
                        };

                        marketLocationInfo[marketId] = new MarketLocationInfo
                        {
                            MarketId = marketId,
                            MarketName = marketName,
                            ElementId = elementId,
                            ConstructId = constructId,
                            PlanetId = planetId,
                            PlanetName = planetName,
                            Position = position,
                            DistanceFromOrigin = CalculateDistanceFromOrigin(position)
                        };

                        // Only log at debug level for detailed market info
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug($"Market {marketId} ({marketName}): planet={planetName}, construct={constructId}");
                    }
                    }
                } // Reader is now closed

                // Now handle markets without elements (add them as space stations)
                using (var missingElementsCommand = new NpgsqlCommand(@"
                    SELECT m.id, m.name 
                    FROM market m 
                    LEFT JOIN element e ON m.element_id = e.id 
                    WHERE e.id IS NULL", connection))
                using (var missingElementsReader = await missingElementsCommand.ExecuteReaderAsync())
                {
                    var missingElementsCount = 0;
                    while (await missingElementsReader.ReadAsync())
                    {
                        var marketId = Convert.ToUInt64(missingElementsReader["id"]);
                        var marketName = missingElementsReader["name"]?.ToString() ?? $"Market {marketId}";
                        
                        // Add markets without elements as space stations
                        marketLocationInfo[marketId] = new MarketLocationInfo
                        {
                            MarketId = marketId,
                            MarketName = marketName,
                            ElementId = 0,
                            ConstructId = 0,
                            PlanetId = 0,
                            PlanetName = "Space Stations",
                            Position = new Vec3 { x = 0, y = 0, z = 0 },
                            DistanceFromOrigin = 0
                        };
                        
                        logger.LogDebug($"Market {marketId} ({marketName}) has no element - added as space station");
                        missingElementsCount++;
                    }
                    
                    if (missingElementsCount > 0)
                    {
                        logger.LogInformation($"Added {missingElementsCount} markets without elements as space stations");
                    }
                }

                logger.LogInformation($"Retrieved location information for {marketLocationInfo.Count} markets from database (out of {totalMarkets} total)");
                
                return marketLocationInfo;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to retrieve market location information from database");
                return new Dictionary<ulong, MarketLocationInfo>();
            }
        }

        /// <summary>
        /// Get planet information from PostgreSQL database
        /// </summary>
        public async Task<Dictionary<ulong, PlanetInfo>> GetPlanetInfoAsync()
        {
            try
            {
                logger.LogInformation("Retrieving planet information from PostgreSQL database...");

                var planetInfo = new Dictionary<ulong, PlanetInfo>();

                // Query to get planet information (planets are constructs with id between 1-100)
                var query = @"
                    SELECT 
                        c.id as planet_id,
                        c.id as construct_id,
                        c.position_x,
                        c.position_y,
                        c.position_z,
                        c.name as planet_name
                    FROM construct c
                    WHERE c.id BETWEEN 1 AND 100";

                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                
                using var command = new NpgsqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var planetId = Convert.ToUInt64(reader["planet_id"]);
                    var constructId = Convert.ToUInt64(reader["construct_id"]);
                    var planetName = reader["planet_name"]?.ToString() ?? $"Planet {planetId}";
                    
                    var position = new Vec3
                    {
                        x = Convert.ToDouble(reader["position_x"]),
                        y = Convert.ToDouble(reader["position_y"]),
                        z = Convert.ToDouble(reader["position_z"])
                    };

                    planetInfo[planetId] = new PlanetInfo
                    {
                        PlanetId = planetId,
                        ConstructId = constructId,
                        Name = planetName,
                        Position = position,
                        DistanceFromOrigin = CalculateDistanceFromOrigin(position)
                    };

                    logger.LogDebug($"Planet {planetId} ({planetName}): construct_id={constructId}, position=({position.x}, {position.y}, {position.z})");
                }

                // Add a special entry for space stations (planet_id = 0)
                planetInfo[0] = new PlanetInfo
                {
                    PlanetId = 0,
                    ConstructId = 0,
                    Name = "Space Stations",
                    Position = new Vec3 { x = 0, y = 0, z = 0 }, // Origin position
                    DistanceFromOrigin = 0
                };

                logger.LogInformation($"Retrieved information for {planetInfo.Count} planets from database (including space stations)");
                return planetInfo;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to retrieve planet information from database");
                return new Dictionary<ulong, PlanetInfo>();
            }
        }

        /// <summary>
        /// Get markets on the same planet
        /// </summary>
        public async Task<Dictionary<ulong, List<ulong>>> GetMarketsByPlanetAsync()
        {
            try
            {
                var marketsByPlanet = new Dictionary<ulong, List<ulong>>();
                var marketLocationInfo = await GetMarketLocationInfoAsync();

                foreach (var (marketId, locationInfo) in marketLocationInfo)
                {
                    if (!marketsByPlanet.ContainsKey(locationInfo.PlanetId))
                    {
                        marketsByPlanet[locationInfo.PlanetId] = new List<ulong>();
                    }
                    marketsByPlanet[locationInfo.PlanetId].Add(marketId);
                }

                logger.LogInformation($"Grouped markets by planet: {marketsByPlanet.Count} planets with markets");
                foreach (var (planetId, markets) in marketsByPlanet)
                {
                    logger.LogDebug($"Planet {planetId}: {markets.Count} markets");
                }

                return marketsByPlanet;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to group markets by planet");
                return new Dictionary<ulong, List<ulong>>();
            }
        }

        /// <summary>
        /// Calculate accurate distance between two markets using database positions
        /// </summary>
        public async Task<double> CalculateMarketDistanceAsync(ulong marketId1, ulong marketId2)
        {
            try
            {
                var marketLocationInfo = await GetMarketLocationInfoAsync();
                
                if (!marketLocationInfo.TryGetValue(marketId1, out var market1Info) ||
                    !marketLocationInfo.TryGetValue(marketId2, out var market2Info))
                {
                    logger.LogWarning($"Could not find location info for markets {marketId1} and/or {marketId2}");
                    return 0.0;
                }

                // If markets are on the same planet, distance is 0 (or very small)
                if (market1Info.PlanetId == market2Info.PlanetId)
                {
                    logger.LogDebug($"Markets {marketId1} and {marketId2} are on the same planet ({market1Info.PlanetId})");
                    return 0.0; // Same planet distance
                }

                var distance = CalculateDistance(market1Info.Position, market2Info.Position);
                logger.LogDebug($"Distance between market {marketId1} (planet {market1Info.PlanetId}) and market {marketId2} (planet {market2Info.PlanetId}): {distance:F0}");
                
                return distance;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to calculate distance between markets {marketId1} and {marketId2}");
                return 0.0;
            }
        }

        /// <summary>
        /// Calculate distance from origin (0,0,0)
        /// </summary>
        private double CalculateDistanceFromOrigin(Vec3 position)
        {
            return Math.Sqrt(position.x * position.x + position.y * position.y + position.z * position.z);
        }

        /// <summary>
        /// Calculate distance between two positions
        /// </summary>
        private double CalculateDistance(Vec3 pos1, Vec3 pos2)
        {
            var dx = pos1.x - pos2.x;
            var dy = pos1.y - pos2.y;
            var dz = pos1.z - pos2.z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }


    }

    /// <summary>
    /// Market location information from database
    /// </summary>
    public class MarketLocationInfo
    {
        public ulong MarketId { get; set; }
        public string MarketName { get; set; } = "";
        public ulong ElementId { get; set; }
        public ulong ConstructId { get; set; }
        public ulong PlanetId { get; set; }
        public string PlanetName { get; set; } = "";
        public Vec3 Position { get; set; }
        public double DistanceFromOrigin { get; set; }
    }

    /// <summary>
    /// Planet information from database
    /// </summary>
    public class PlanetInfo
    {
        public ulong PlanetId { get; set; }
        public ulong ConstructId { get; set; }
        public string Name { get; set; } = "";
        public Vec3 Position { get; set; }
        public double DistanceFromOrigin { get; set; }
    }
}