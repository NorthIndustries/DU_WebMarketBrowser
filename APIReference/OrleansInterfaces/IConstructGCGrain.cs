using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public interface IConstructGCGrain : IGrainWithGuidKey
    {
        [AlwaysInterleave]
        Task Initialize(); // setup reminder
        [AlwaysInterleave]
        Task Stop();
        Task UpdateSubscriptions(); // query community and update subscription state
        Task GC(); // abandon and delete constructs
        Task AbandonConstruct(ulong cid);
        Task DeleteConstruct(ulong cid);
        Task SetLastQueryTime(DateTime qtime); // change last query time state
        Task ProcessSubscriptionChange(List<ulong> subs, List<ulong> unsubs); // player ids
        Task<bool> CheckOrganization(ulong oid); // true if switched to abandoned
        Task FirstTimeSetup();
    }
}
