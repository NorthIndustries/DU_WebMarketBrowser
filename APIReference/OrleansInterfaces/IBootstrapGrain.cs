using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public interface IBootstrapGrain : IGrainWithGuidKey
    {
        [OneWay]
        Task Initialize(bool onlyConstructs);

        [AlwaysInterleave]
        Task<bool> IsStackReady();

        Task Stop();
    }
}

