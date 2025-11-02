using Orleans;

namespace NQ.Interfaces
{
    public interface IAsteroidTrackerGrain : IGrainWithIntegerKey
    {
        Task<ScanPoint> StartTracking(ulong requester, ulong asteroid);
        Task<ScanPoint> Scan(ulong requester);
        Task<ScanPoint> NextScanPoint();
        Task Destroy();
    }
}
