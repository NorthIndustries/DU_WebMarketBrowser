using Orleans;


namespace NQ.Interfaces
{
    public interface IPlayerSandboxGrain : IGrainWithIntegerKey
    {
        Task StashAndResetInventory();
        Task UnstashInventory();
        Task<(bool, ulong, List<ulong>)> CreateEnvironment(string templateName, bool permabox = false);
        Task<RelativeLocation> FetchEntrypoint(ulong constructId);
        Task<RelativeLocation> EnterSandbox(RelativeLocation currentLocation, string templateName);
        Task DeleteSandbox(bool? isSpace, ulong cid, List<ulong> instantiatedConstructs, bool bootstrap = false);
        Task<TutorialExit> LeaveSandbox(bool completed);
        Task<TutorialExit> LeaveAndCleanSandbox(bool completed, bool fastClean = false);
        Task<TutorialInfo> GetTutorialInfo(string templateName);
        Task<ulong> GarbageCollect(ulong maxLifefitemSeconds);
        Task OnPlayerDisconnected(bool bootstrap);
        Task BootTimeCleanup(ulong sandboxConstructId);
        Task<ConstructId> CurrentSandbox();
        Task<TutorialInfos> ListTutorials();
        Task<RelativeLocation> EnterPermabox(RelativeLocation currentLocation);
        Task<RelativeLocation> LeavePermabox(RelativeLocation currentLocation);
        Task<PermaboxInformation> PermaboxInfo();
        Task<bool> IsWalletStashed();
        Task UpdateStashedWallet(long amount); // must be positive
    }
}
