using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public class MiningUnitInfo
    {
        public ulong selectedOre;
        public ulong activationTime;
        public double maxMiningRate;
        public double currentMiningRate;
    }
    public interface IMiningUnitGrain : IGrainWithIntegerKey, IRemindable
    {
        Task ContainerOutputChanged();
        Task SetOutputContainer(ulong id, bool fromContainer = false);
        Task Start(ulong requester);
        Task SetOre(ulong requester, ulong ore);
        Task Stop(ulong requester);
        Task EnsureRemovable();
        Task Remove();
        Task<OrePoolList> Available(List<ulong> ores);
        Task<object> CurrentState();
        Task Calibrate(ulong requester);
        Task ApplyCalibrationBonus(ulong requester, double bonus);
        Task ReloadProperties();
        [AlwaysInterleave]
        Task<MiningUnitInfo> MiningState();
        Task ResetTimer(); // debug
        Task DeactivateGrain();
    }
}
