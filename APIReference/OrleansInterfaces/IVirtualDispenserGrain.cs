using Orleans;

namespace NQ.Interfaces
{
    /// <summary>
    /// This represent the content (filter) of a virtual dispenser.
    /// </summary>
    public interface IVirtualDispenserContentGrain : IGrainWithIntegerKey
    {
        Task<StorageInfo> Get();
        Task<StorageSlot> GetSlotContent(int slot);
        Task DropSlot(int slot, long quantity);
        Task AddSlot(ItemInfo item, int slot, long quantity);
        Task AddItems(List<ItemAndQuantity> items);
        Task Move(int fromSlot, int toSlot, long quantity);
        Task Swap(int slot1, int slot2);
        Task SetSlot(StorageSlot slot);
        Task DropProperties(List<int> slots);
        Task Stack();
    }
}
