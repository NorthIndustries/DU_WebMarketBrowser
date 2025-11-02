using Orleans;

namespace NQ.Interfaces
{
    public interface IConstructAutoCompactorGrain : IGrainWithGuidKey
    {
        Task Start();
        Task Stop();
        Task AutoCompact();
    }
}
