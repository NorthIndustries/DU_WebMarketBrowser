using Orleans;


namespace NQ.Interfaces
{
    public interface ISPSGrain : IGrainWithIntegerKey
    {
        Task<SPSSearchResult> Search(SPSSearch query);
        Task<RelativeLocation> Enter(RelativeLocation currentLocation, ulong elementId);
        Task<RelativeLocation> Leave(bool teleport=true, bool disconnection=false);
        Task<RelativeLocation> OnPlayerDisconnected();
        Task<long> CurrentSession();

        /// <summary>
        /// clear cache for debug / tests
        /// </summary>
        /// <returns></returns>
        Task ClearCache();
    }
}
