using Orleans;


namespace NQ.Interfaces
{
    public interface IAlienSpaceCoreManagerGrain : IGrainWithIntegerKey
    {
        Task Reconcile();
        Task Spawn(ulong id, Vec3 position);
    }
}
