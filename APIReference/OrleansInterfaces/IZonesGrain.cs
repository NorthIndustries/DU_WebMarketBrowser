using Orleans;

namespace NQ.Interfaces
{
    public interface IZonesGrain : IGrainWithIntegerKey
    {
        Task<bool> IsInInterdictionZone(Vec3 pos);
        Task Refresh();
    }
}
