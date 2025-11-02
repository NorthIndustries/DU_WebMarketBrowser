using Orleans;

#nullable enable

namespace NQ.Interfaces
{
    public interface IConstructElementsGrain : IGrainWithIntegerKey
    {
        Task UnLoad();
        Task ElementOperation(ElementOperation op);

        Task<ElementList> GetVisibleAt(ElementLOD lod);
        Task<ElementInfo> GetElement(ElementId elementId);
        Task<List<ElementId>> GetElementsOfType<T>() where T : NQutils.Def.Element;

        Task<PropertyValue> GetPropertyValue(ElementPropertyId v);

        Task<bool> CanAdd(ItemType elementType);
        /// <summary>
        /// db write the element property coming from the client.  It might store
        /// the property value in S3.  Returns the update
        /// where the property value might have been replaced by the hash
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        Task<ElementPropertyUpdates> UpdateElementProperty(ElementPropertyUpdateInternal update);

        Task UpdateElementProperty(ElementPropertyUpdate update, bool fromServer = true);
        Task UpdateElementProperties(List<ElementPropertyUpdate> update, bool fromServer = true);

        Task<ElementInfo> AddElement(PlayerId from, ElementInfo element, List<PropertyUpgrade> upgrades);

        /// <summary>
        /// Delete the described element.
        /// </summary>
        /// <param name="eid"></param>
        /// <param name="newOwner">when newOwner is given, the line is preserved in the element table but it is
        /// marked as in the inventory of newOwner.</param>
        /// <returns></returns>
        Task<ElementInfo> DeleteElement(ElementId eid, PlayerId? newOwner, bool deleteFromDB = false, List<string>? propertiesToRemove = null);

        Task BatchEdit(LinkBatchEdit batch);

        /// <summary>
        /// apply a batch of updates on the same element.
        /// </summary>
        /// <param name="updates"></param>
        /// <returns></returns>
        Task BatchInternalUpdate(ElementId elementId, List<ElementPropertyUpdate> updates);

        Task MoveElement(ElementLocation elementLoc);

        Task<List<PlayerId>> GetSurrogateUser();

        Task<double> RepairElement(ElementId eid, double amount);

        Task RepairAllAdmin();

        /// <summary>
        /// Implementation of `ElementManagementGrain.ReplaceElement
        /// This is required to prevent concurrent replaces of the same element
        /// </summary>
        Task Gameplay_ReplaceElement(PlayerId pid, ElementId eid);

        #region Usage
        Task ElementUseStart(ElementUse usage);
        Task ElementUseStop(ElementUse usage);

        Task<UsedElementList> ConstructUsedElements();

        Task<PlayerId?> GetElementUser(ElementId elementId);

        #endregion
        Task Reload();
        Task UnloadAllContainers();
        Task UnloadAllProducers();
    }
}
