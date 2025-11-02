using Orleans;

namespace NQ.Interfaces
{
    public class PvpDamageResult
    {
        public bool CoreUnitDestroyed = false;
        public Dictionary<ulong, double> elementsDamage = new();
        public List<(ElementId, ulong)> broken = new();
        public List<(ElementId, ulong)> destroyed = new();
    }
    public interface IConstructDamageElementsGrain : IGrainWithIntegerKey
    {
        Task ApplyElementDamage(PlayerId playerId, ElementDamageOperation operation);
        Task<PvpDamageResult> ApplyPvpElementsDamage(List<ElementListVoxelServiceOutputData> damages, PlayerDeathInfoPvPData deathInfoPvp);
        Task TriggerCoreUnitStressDestruction(PlayerDeathInfoPvPData playerDeathInfoPvp);
    }
}
