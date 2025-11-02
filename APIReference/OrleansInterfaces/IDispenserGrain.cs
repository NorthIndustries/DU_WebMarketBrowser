using Orleans;

namespace NQ.Interfaces
{
    public interface IDispenserGrain : IGrainWithIntegerKey
    {
        Task Configure(ulong requester, DispenserParameters parms);
        Task<InventoryGiveOperation> Use(ulong requester, ulong orgWallet);
        Task<DispenserStatus> Status(ulong requester);
        Task<bool> EditAllowed(ulong requester);
        Task DeactivateGrain();
    }
}
