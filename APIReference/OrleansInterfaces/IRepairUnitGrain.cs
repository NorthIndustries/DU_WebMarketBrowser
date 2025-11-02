using Orleans;

namespace NQ.Interfaces
{
    public interface IRepairUnitGrain : IGrainWithIntegerKey
    {
        Task<bool> IsRepairing();
        Task<RepairUnitDiff> DiffWithSnapshot(ulong playerId, RepairUnitOperation op);
        Task StartRepairing(ulong playerId, RepairUnitOperation op);
        Task FinalizeRepair(ulong playerId, bool force);
        Task Cancel(ulong playerId, bool force);
        Task SetContainer(ulong containerId);
        Task<RepairUnitScanResult> Scan(ulong playerId);
        Task<SnapshotsInfo> GetSnapshotsInfo(ulong playerId, ulong cid);
        Task Destroy();
        Task<bool> IsDismantling();

        /// <summary>
        /// Allows to skip the repair wait for debug / tests purposes
        /// </summary>
        Task DebugFinishRepair();
    }
}
