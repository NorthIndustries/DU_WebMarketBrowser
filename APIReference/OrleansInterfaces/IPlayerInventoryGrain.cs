using NQutils.Storage;

namespace NQ.Interfaces
{
    public interface IPlayerInventoryGrain : Orleans.IGrainWithIntegerKey
    {
        Task<OperationResult> GiveOrTakeItems(IItemStorageTransaction tr, IEnumerable<ItemAndQuantity> operations, OperationOptions options);

        Task<ElementId> GetPrimaryContainer();
        Task<LinkedContainerStatus> SetPrimaryContainer(ulong id, bool notifyPlayer = true, bool isDefault = false);
        Task<LinkedContainerStatus> GetPrimaryContainerState();
        Task<ElementId> GetActivePrimaryContainer();

        Task ChangeDefaultStatus(bool isDefault);


        Task<(ulong containerId, bool isDefault)> StateStash();
        Task StateSet((ulong containerId, bool isDefault) newState);

        Task OnPlayerConnected();

        // for debug / integration tests
        Task DebugManualTriggerTimer();
    }
}
