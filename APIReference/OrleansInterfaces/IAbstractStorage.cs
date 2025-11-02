using NQutils.Storage;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public interface IAbstractStorage : Orleans.IGrainWithIntegerKey
    {
        // /// Reserve operations, returning Error (possibly null), transaction Id
        // Task<StorageTransaction> DoReserve(IEnumerable<ItemAndQuantity> operations, bool allowPartial = false,
        //                                    StorageReserveReason reason = StorageReserveReason.RESERVE_EXACT,
        //                                    ulong requester = 0, EntityId user = null, bool allowOverload = false,
        //                                    bool automaticRollback = true, bool bypassLock = false, bool fillManifest = false, bool bypassRDMS = false);


        Task<OperationResult> GiveOrTakeItems(IItemStorageTransaction tr, IEnumerable<ItemAndQuantity> ops, OperationOptions opts);

        /// Transaction management : get the sql operations
        [AlwaysInterleave]
        Task<(List<SlotOperation> slotOperations, List<(ulong type, ulong id, long qty)> diff)> GetOps(Guid transactionId);

        /// Commit a transaction
        /// Returns the current (after commit) volume and mass
        [AlwaysInterleave]
        Task<(double volume, double mass, double maxVol)> Commit(Guid transactionId);

        /// Rollback a transaction
        [AlwaysInterleave]
        Task Rollback(Guid transactionId);

        /// Read operations
        Task<StorageInfo> Get(PlayerId requester);
        Task<ulong> AmountOf(ItemReference item);

        Task<ElementId> GetRootContainerId();

        /// Create Operation
        Task<DataItemIds> CreateItem(long playerId, ulong type, Dictionary<string, PropertyValue> properties);

        /// Slot operations
        Task Move(IItemStorageTransaction tr, long requester, int fromSlot, int toSlot, long quantity);
        Task Drop(IItemStorageTransaction tr, long requester, int fromSlot, long quantity, bool onBehalfOfElement = false);
        Task Swap(IItemStorageTransaction tr, long requester, int fromSlot, int toSlot);
        Task SwapBetween(long requester, int fromSlot, IAbstractStorage target, int toSlot);
        Task MoveBetween(long requester, int fromSlot, IAbstractStorage target, int toSlot, long qty);

        Task Add(IItemStorageTransaction tr, StorageSlot slot, ulong requester);
        Task<StorageSlot> GiveItems(IItemStorageTransaction tr, ItemAndQuantity items, ulong requester, OperationOptions options);
        Task Replace(IItemStorageTransaction tr, StorageSlot slot, OperationOptions options);
        Task Stack(IItemStorageTransaction tr);
        Task Clear(IItemStorageTransaction tr);
        Task Claim(IItemStorageTransaction tr, long requester, int slot, EntityId owner);

        // used to perform swap with an other storage
        // replace the slot with `items` and return the old content
        Task<ItemAndQuantity?> Internal_PerformSwap(IItemStorageTransaction tr, int slot, ItemAndQuantity? items, OperationOptions options);

        /// Un-instantiate items in the given list of slots
        Task DropProperties(long requester, List<int> slots);
        Task<ElementId> CreatePackage(ulong requester, List<int> slots, string name);
        Task OpenPackage(ulong requester, int slot);
        Task SetProperties(ItemReference item, Dictionary<string, PropertyValue> properties, OperationOptions options);
        /// Internal
        Task CommitHook(bool added, bool removed, bool addedSchematics);

        /// Debug
        Task<string> PrettyGet();
        Task Dump();
        Task DeactivateGrain();
    }
}
