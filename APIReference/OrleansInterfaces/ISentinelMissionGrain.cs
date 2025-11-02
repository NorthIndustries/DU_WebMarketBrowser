using NQ.RDMS;
using Orleans;
using Orleans.Concurrency;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NQ.Interfaces
{
    public class ConsumedShot
    {
        public ulong targetConstructId;
        public string name;
        public Vec3 position;
        public ulong constructId;
        public ulong constructSize;
    }

    public class YVec3: IYamlConvertible
    {
        public double x;
        public double y;
        public double z;
        public Vec3 Vec3 { get { return new Vec3 { x = x, y = y, z = z};} }
        public void Read(IParser parser, Type type, ObjectDeserializer nestedObjectDeserializer)
        {
            if (!parser.TryConsume<Scalar>(out var scalar))
                throw new Exception("Not a scalar");
            var comps = scalar.Value.Split(',');
            x = double.Parse(comps[0].Trim());
            y = double.Parse(comps[1].Trim());
            z = double.Parse(comps[2].Trim());
        }
        public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
        {
            nestedObjectSerializer($"{x},{y},{z}");
        }
    }
    public enum SentinelSpawnReason
    {
        Combat, // spawn when target enters combt
        Death,  // spawn when target is destroyed
        PlayerDetection, // spawn when target area is entered
        Delay, // spawn after a delay
    }
    public class SentinelPattern
    { // TP pattern definition
        public double coolDown { get; set; }
        public double maxJumpDistance { get; set; }
        public double maxAbsoluteDistance { get; set; } // patrol
        public double minJumpDistance { get; set; }     // patrol
        public double triggerMin { get; set; } // combat
        public double triggerMax { get; set; } // combat
        public double targetDistance { get; set; } // combat
    }
    public class SentinelWeapon
    { // NPC weapon definition
        public string weaponItem { get; set; }
        public string ammoItem { get; set; }
        public double fireCooldown { get; set; }
        public double damage { get; set; }
        public double baseAccuracy { get; set; }
        public double optimalCrossSectionDiameter { get; set; }
        public double baseOptimalAimingCone { get; set; }
        public double falloffAimingCone { get; set; }
        public double baseOptimalDistance { get; set; }
        public double falloffDistance { get; set; }
        public double baseOptimalTracking { get; set; }
        public double falloffTracking {get; set; }
        // stasis parameters
        public double range { get; set; }
        public double effectDuration { get; set; }
        public double effectStrength { get; set; }
        // aoe parameteres
        public bool aoe { get; set; }
        public double aoeRange { get; set; }
    }
    public class SentinelShip
    { // NPC ship definition
        public string prettyName { get; set; } = ""; // Used to name NPC entities, visible to players
        public List<string> weapons { get; set; } = new();
        public double shieldHP { get; set; }
        public List<double> shieldResistances { get; set; }
        public string patrol { get; set; }  // patrol behavior name
        public string attack { get; set; }  // attack behavior name
        public double agressionRange { get; set; } // enter attack mode if PC in range
    }
    public class SentinelArea
    { // Definition of an area in warp space
        public YVec3 center { get; set; }
        public double radius { get; set; }
    }
    public class SentinelSpawnTrigger
    { // Spawn condition for a NPC ship
        public string target { get; set; } // Either a ship or an area
        public List<string> anyOf { get; set; } // list of ship,
        public List<string> allOf { get; set; } // list of ship, death only
        public SentinelSpawnReason reason { get; set; }
        public double delay { get; set; }
    }
    public class SentinelShipInstance
    { // Entry in mission definition
        public string name { get; set; }
        public string model { get; set; }
        public YVec3 spawn { get; set; }
        public List<ulong> combatGroup { get; set; } = new();
        public SentinelSpawnTrigger spawnTrigger { get; set; } // null for spawn at start
    }
    // Library shared between all missions
    public class SentinelLibrary
    {
        public Dictionary<string, SentinelPattern> patterns { get; set; }
        public Dictionary<string, SentinelShip> ships { get; set; }
        public Dictionary<string, SentinelWeapon> weapons { get; set; }
    }
    // One PvE mission
    public class SentinelMission
    {
        public List<SentinelShipInstance> ships { get; set; } = new();
        public List<SentinelShipInstance> misc { get; set; } = new();
        // key is area name
        public Dictionary<string, SentinelArea> areas { get; set; } = new();
        // time to complete once entered
        public double expirationDelayMinutes { get; set; }
        public double maxCompletionTime { get; set; }
        public double maxShipMass { get; set; }
        public List<string> forbiddenItems { get; set; }
        public string name  { get; set; }
        public string description { get; set; }
        public ulong rewardQuantas { get; set; }
        public ulong collateralQuantas { get; set; }
        public YVec3 exitBeaconPosition { get; set; }
        public ulong alienPoints { get; set; }
        public List<YVec3> pointsOfInterest { get; set; } = new();
        public object editorData { get; set; }
    }
    public class SentinelInfos
    {
        public int npcTotal;
        public int npcSpawned;
        public int npcDead;
        public ulong constructId;
        public Vec3 location;
        public SentinelMissionDetails details;
        public List<ulong> enteredPlayers;
        public List<ulong> presentPlayers;
    };
    
    public interface ISentinelMissionGrain: IGrainWithIntegerKey, IRemindable
    {
        Task<SentinelMissionTierList> AvailableTiers();
        Task SpawnMission(string missionName);
        Task Teardown();
        Task<(ConsumedShot, SentinelWeapon)> ConsumeShot(ulong shotId);
        [OneWay]
        Task NPCShieldDepleted(ulong npcConstructId, ulong shooterPlayerId);
        [OneWay]
        Task NPCAttacked(ulong npcConstructId, ulong attackerConstructId);
        [AlwaysInterleave]
        Task<Visibility.RadarData> RadarStart(ulong radar);
        [AlwaysInterleave]
        Task RadarStop(ulong radar);
        Task<SentinelMissionDetails> AcquireMission(SentinelMissionParameters parms);
        Task RejectMission(ulong missionId);
        Task<SentinelMissionDetails> AcceptMission(ulong missionId);
        Task EnterMission(ulong missionId);
        Task LeaveMission();
        Task<SentinelMissionBriefList> ActiveMissions();
        Task<SentinelMissionBriefList> History();
        Task<SentinelMissionDetails> MissionDetails(ulong missionId);
        [AlwaysInterleave]
        Task<RelativeLocation> GetEntryBeaconLocation();
        [AlwaysInterleave]
        Task PlayerDied(ulong playerId);
        Task<SentinelForbiddenItemList> ScanShip(ulong constructId, ulong missionId);
        [OneWay]
        Task OnConstructDestroyed(ulong constructId);
        Task AutoExitFailure(string reason);
        Task<SentinelInfos> CurrentMissionInfos();
        Task MarkVisited(ulong poiId);
        Task BossSpawnCheck();
        Task SpawnBoss(string model, Vec3 position);
        Task DespawnBoss(ulong constructId);
        /// reloads tiers and library
        Task Reload();
        Task<SentinelHistoryStats> Stats();
        Task DoThink();
    }
}