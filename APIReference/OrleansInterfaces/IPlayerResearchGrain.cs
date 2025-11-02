using Orleans;

namespace NQ.Interfaces
{
    public interface IPlayerResearchGrain : IGrainWithIntegerKey
    {
        Task<ResearchState> State();
        Task SetResearchSlot(ulong itemTypeId, ulong batchSize);
        Task Collect(ulong slot);
        Task Cancel(ulong slot);
    }
}
