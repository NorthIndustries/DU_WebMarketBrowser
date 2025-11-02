using Orleans;

namespace NQ.Interfaces
{
    public interface IWreckManagerGrain : IGrainWithIntegerKey
    {
        Task Initialize();
        Task Reconcile();
        Task<ulong> SpawnWreck(string model, Vec3 position);
        Task<ScanResult> ScanWrecks();
    }
}
