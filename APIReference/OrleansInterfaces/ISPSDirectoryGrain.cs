using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public class SPSAndRights
    {
        public bool disowned;
        public bool enabled;
        public bool broken;
        public SPSDetail sps;
        public List<ulong> allowedPlayers = new List<ulong>();
        public List<ulong> allowedOrgs = new List<ulong>();
    }
    public interface ISPSDirectoryGrain : IGrainWithIntegerKey
    {
        [AlwaysInterleave]
        Task<Dictionary<ulong, SPSAndRights>> Get();
        [AlwaysInterleave]
        Task SetPodState(ulong elementId, bool state);
        [AlwaysInterleave]
        Task SetPodBroken(ulong elementId, bool broken);
        [AlwaysInterleave]
        Task TerminateSessions(ulong elementId);

        /// <summary>
        /// For debug / tests
        /// </summary>
        [AlwaysInterleave]
        Task ClearCache();
        [AlwaysInterleave]
        Task ForceRecomputeRDMS();
    }
}
