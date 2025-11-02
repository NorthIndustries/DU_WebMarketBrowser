namespace NQ.Interfaces
{
    public class PackageFixture
    {
        public string name;
        public Dictionary<string, ulong> content; // itemTypeName -> ItemQuantity (count or liters)
    }
    public class FormalMissionFixture
    {
        public string sourceMarket;
        public string destinationMarket;
        public string sourceConstruct;
        public string destinationConstruct;
        public string package; // name of 'packages' entry
        public string title; // optional
        public string description; // optional
        public ulong reward; // in quantas
        public ulong collateral; // in quantas
    }
    public class FormalMissionsFixture
    {
        public Dictionary<string, PackageFixture> packages;
        public List<FormalMissionFixture> missions;
    }

    public interface IPlayerFormalMissionGrain : Orleans.IGrainWithIntegerKey
    {
        Task<FormalMissionId> Create(FormalMissionCreation fmc);
        Task<FormalMissionId> Apply(FormalMissionId mid);
        Task<FormalMissionActive> MyActive();
        Task<FormalMissionHistory> History(ulong playerId);
        Task<FormalMissionList> Live();
        Task Pickup(FormalMissionId mid);
        Task Deliver(FormalMissionId mid);
        Task<MissionPendingRatingList> PendingRatings();
        Task Rate(FormalMissionRating rating);
        Task Abort(FormalMissionId mid);
        Task DoAbort(FormalMissionId mid, bool noSplitReward, EnumNotificationCode? reason = null, bool packageDestroyed = false, bool placeholderDestroyed = false);
        Task Timeout(ulong mid);
        Task<MissionContainerList> MyMissionContainers();
        Task PackageOpening(ulong mid);
        Task PackageDestroyed(ulong mid);
        Task DestinationDestroyed(ulong mid);
        Task<MissionStats> Statistics();
        Task<FormalMissionList> All(ulong limit, ulong offset);
        Task Update(FormalMissionUpdate fmu);
        Task ValidateFixture(FormalMissionsFixture f, Dictionary<string, ulong> marketMap, Dictionary<string, ulong> containerMap);
        Task ApplyFixture(FormalMissionsFixture f);
        Task ApplyRawFixture(string rawFixture);
        Task<string> DiffWithFixture(FormalMissionsFixture f);
        Task<string> DiffWithRawFixture(string rawFixture);
    }
}
