using Orleans;

namespace NQ.Interfaces
{
    public interface IConstructCreatorGrain : IGrainWithIntegerKey
    {
        Task<ConstructCreation> CreateConstruct(ulong requester, ConstructRequest cr);
    }
}
