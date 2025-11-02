using Orleans;

namespace NQ.Interfaces
{
    public interface IElementManagementGrain : IGrainWithIntegerKey
    {
        Task<bool> HasSkinFor(PlayerId playerId, ElementId elementId, string skin);
        Task<PlayerSkins> GetAllSkins(PlayerId playerId);
        Task<ItemAndQuantity> SalvageElement(PlayerId playerId, ElementId element);
        Task ReplaceElement(PlayerId playerId, ElementInConstruct element);

        Task<ElementInfo> ElementAdd(PlayerId pid, ElementDeploy deploy);

        Task<ElementInfo> ElementDelete(PlayerId pid, ElementInConstruct elementId);

        Task<ElementInfo> ElementDestroy(PlayerId pid, ElementInConstruct elementId);


        Task<ElementInfo> MoveElement(PlayerId pid, ElementLocation element);

        Task ElementPropertyUpdate(PlayerId pid, ElementPropertyUpdate update);

        Task ElementLinkCreate(PlayerId pid, LinkInfo li);
        Task ElementLinkDelete(PlayerId pid, LinkInfo li);
        Task ElementLinkBatchEdit(PlayerId pid, LinkBatchEdit batch);

        Task<ScrapRepairResult> ElementRepair(PlayerId pid, ScrapRepair sr);
    }
}
