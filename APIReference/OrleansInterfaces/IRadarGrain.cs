using Orleans;

namespace NQ.Interfaces
{
    public interface IRadarGrain : IGrainWithIntegerKey
    {
        Task ScanStart(PlayerId pid, RadarScan v);

        Task ScanStop(PlayerId pid, RadarScan v);

        Task IdentifyStart(PlayerId pid, RadarIdentifyTarget target);

        Task IdentifyStop(PlayerId pid, RadarIdentifyTarget target);

        Task<bool> EnterWormhole(PlayerId wormhole);

        Task LeaveWormhole(PlayerId wormhole);
    }
}
