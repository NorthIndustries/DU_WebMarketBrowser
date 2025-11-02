using Orleans;

namespace NQ.Interfaces
{
    public interface IAsteroidManagerGrain : IGrainWithIntegerKey
    {
        Task Initialize();
        Task<AsteroidList> GATList();
        Task MaybePublish();
        Task MaybeDiscover(ulong asteroid);
        Task ForcePublish(ulong asteroid);
        Task Despawn(ulong asteroid);
        Task Reconcile();
        Task<ulong> SpawnAsteroid(int tier, string model, Vec3 position, ulong planet);
        Task<(List<List<Vec3>>, List<Vec3>, List<List<ulong>>)> Randomize(List<int> counts);
        Task LoadSafeZones();
        Task DespawnAll();
    }
}
