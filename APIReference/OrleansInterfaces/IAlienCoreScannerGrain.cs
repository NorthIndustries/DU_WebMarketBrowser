using Orleans;

namespace NQ.Interfaces
{
    public interface IAlienCoreScannerGrain : IGrainWithIntegerKey
    {
        Task<AlienCoreList> ListAlienCores();
    }
}
