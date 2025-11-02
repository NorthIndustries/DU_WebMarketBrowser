using NQutils.Storage;

namespace NQ.Interfaces
{
    public interface IInventoryGrain : IAbstractStorage
    {
        Task ResetToDefault(IItemStorageTransaction transaction, bool preserveBlueprints = false);
        Task ResetToTools(IItemStorageTransaction transaction);
        Task ResetToDefaultBots(IItemStorageTransaction transaction);
        Task Fill(IItemStorageTransaction transaction, long quantity);
        Task RestoreTools(IItemStorageTransaction transaction);
        Task Replace(IItemStorageTransaction transaction, List<StorageSlot> content, OperationOptions options);
        Task WipeOnDeath(IItemStorageTransaction transaction);
        Task SetAdmin(bool admin);
    }
}
