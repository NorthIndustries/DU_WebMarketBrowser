using Orleans;

namespace NQ.Interfaces
{
    public interface IConstructParentingGrain : IGrainWithIntegerKey
    {
        Task UnparentPlayer(UnparentPlayerRequest request);
        Task UnparentConstruct(UnparentConstructRequest request, ConstructId? pidOverride = null);
        Task DeparentPlayers(ConstructId cid);
        Task DeparentConstructs(ConstructId cid);
        Task UnhideConstruct(ConstructId cid);
        Task DeleteConstruct(ConstructId cid, bool fromVisibility = true, bool softDelete = false, bool hardDelete = false, bool ensureExisting = false, bool asyncVoxels = false);
        Task SpawnConstruct(ConstructId constructId);
        /// <summary>
        /// reload a construct by deactivating all grains and sending update requests
        /// </summary>
        /// <param name="constructId">construct to reload</param>
        /// <param name="force">if true, the construct will be deleted from the visibility and reinjected
        ///         /!\ WARNING /!\ might create flickering on the construct</param>
        /// <returns>promise on the action</returns>
        Task ReloadConstruct(ConstructId constructId, bool force = false);
        Task ForceDeactivateUpdateConstructGrains(IEnumerable<ConstructId> constructIds);
    }
}
