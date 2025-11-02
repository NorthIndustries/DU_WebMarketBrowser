using Orleans;

namespace NQ.Interfaces
{
    public interface IScenegraphGrain : IGrainWithIntegerKey
    {
        Task<ConstructTree> GetConstructTree(ConstructId id);
    }
}
