using Orleans;

namespace NQ.Interfaces
{
    public interface IMissionTimeouterGrain : IGrainWithGuidKey
    {
        Task Start();
        Task Process();
    }
}
