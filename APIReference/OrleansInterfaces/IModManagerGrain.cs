using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public interface IModManagerGrain : IGrainWithIntegerKey
    {
        Task<ModInfoList> GetModsFor(ulong playerId);
        [AlwaysInterleave]
        Task<EmptyStruct> TriggerModAction(ulong playerId, ModAction action);
        Task RegisterMod(IMod mod);
        Task Initialize();
        Task<object> InvokeHook(string modName, string modAction, List<byte[]> sargs);
    }
}