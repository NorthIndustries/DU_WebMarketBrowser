namespace NQ.Interfaces
{
    public class LinkResult
    {
        public List<ItemAndQuantity> Content;
        public double AdditionalVolume;
    }

    /// <summary>
    /// Beware that some method returns a StorageOperations that must be applied by the caller in an
    /// sql transaction.
    /// </summary>
    public interface IContainerGrain : IAbstractStorage
    {

        // Reload Initialization
        Task Reload();
        // Reload Initialization
        Task ReloadProperties(List<ElementPropertyUpdate> updates);
        // Delete container
        Task Delete(PlayerId byPlayerId);
        // Declare as Primary
        Task<bool> CanBeUsedAsPrimary();

        Task SetAutoClaim(ulong requester, ContainerClaimMode mode);

        /// <summary>
        /// As a Container. Link to a hub.
        /// (not a public API)
        /// </summary>
        /// <param name="hubId"></param>
        /// <returns>a StorageOperations (inside the LinkResult) to be applied</returns>
        Task<LinkResult> ContainerHubLinkingToUs(IItemStorageTransaction tr, ElementId hubId);

        /// <summary>
        /// unlink
        /// </summary>
        /// <param name="content"></param>
        /// <returns>a StorageOperations to be applied</returns>
        Task Unlink(IItemStorageTransaction tr, List<StorageSlot> content);

        /// <summary>
        /// As a ContainerHub. Create a link bewtween self and containerId.
        /// </summary>
        /// <param name="containerId"></param>
        /// <returns></returns>
        Task CreateLink(ElementId containerId);
        // Remove the link bewtween self and containerId
        Task DeleteLink(ElementId containerId);

        /// <summary>
        /// Remove containerId from linked container
        /// Note: RemoveLinkedContainer is equivalent to DeleteLink but skips
        /// linked container publication (useful when container has been deleted)
        /// </summary>
        /// <param name="containerId"></param>
        /// <returns>a StorageOperations to be applied</returns>
        Task RemoveLinkedContainer(IItemStorageTransaction tr, ulong containerId);

        Task RegisterIndustryInput(ulong ind);
        Task RegisterIndustryOutput(ulong ind);
        Task UnregisterIndustryInput(ulong ind);
        Task UnregisterIndustryOutput(ulong ind);
        Task RegisterMinerOutput(ulong miner);
        Task UnregisterMinerOutput(ulong miner);
        Task SetIsSchematicContainer(bool isSchematicContainer);

        // internal
        Task PublishMaxVolumeDynProp();
        Task<ulong?> MasterContainer();
    }
}
