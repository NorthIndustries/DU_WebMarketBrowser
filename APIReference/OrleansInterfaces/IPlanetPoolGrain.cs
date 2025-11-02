using Orleans;


namespace NQ.Interfaces
{
    public interface IPlanetPoolGrain : IGrainWithIntegerKey
    {
        Task<double> GetOrePoolFor(int tile, ulong ore);
        Task<OrePoolList> GetOrePoolAt(uint tile);
    }
}
