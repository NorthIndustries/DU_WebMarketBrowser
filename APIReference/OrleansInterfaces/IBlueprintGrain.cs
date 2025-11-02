using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public interface IBlueprintGrain : IGrainWithIntegerKey
    {
        Task<BlueprintId> Create(BlueprintCreate bpCreate, ulong playerId, long maxUse = -1, bool freeDeploy = false, bool safe = false, bool goesInInventory = true, bool bypassPrimaryContainer = false, bool isCompaction = false, bool restoreModifiedProps = true, ulong fuelType = 0, ulong fuelAmount = 0, bool singleUseBlueprint = false, bool isSnapshot = false);
        Task<ConstructId> Use(BlueprintDeploy bpDeploy, ulong playerId);
        Task Delete(BlueprintId blueprint);
        Task Snapshot(BlueprintCreate bpCreate, ulong playerId, bool autoSlot);
        Task<BlueprintId> CompactConstruct(BlueprintCreate bpCreate, ulong playerId);
        [OneWay]
        Task GenerateBlueprint(ulong constructId, ItemInfo blueprintItem, ulong playerId, bool safe = false, bool goesInInventory = true, bool bypassPrimaryContainer = false, bool isCompaction = false, bool enableDRM = true, bool restoreModifiedProps = true, bool singleUseBlueprint = false);

        Task Split(ulong playerId, BlueprintSplit request);
        Task<ItemInfo> Clone(ulong blueprintId);
        Task<ItemInfo> SplitFreeDeploy(ulong blueprintId);
        Task<BlueprintId> Import(BlueprintData blueprint, bool forceId=false);
        Task FixBlueprintVoxels(BlueprintId blueprintId);
        Task<BlueprintData> Export(BlueprintId id, bool dropProtected=false);
        Task<BlueprintProperties> GetBlueprintInfo(BlueprintId id);
        Task<ConstructId> UseRaw(BlueprintDeploy bpDeploy, ulong playerId);
    }
}
