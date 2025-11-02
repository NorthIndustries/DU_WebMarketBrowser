using Orleans;

namespace NQ.Interfaces
{
    public interface IResurrectionNodeGrain : IGrainWithIntegerKey
    {
        Task<ResurrectionNodeList> GetResurrectionNodeList(PlayerId player);
        Task AddResurrectionNode(PlayerId player, ElementId elementId);
        Task RemoveResurrectionNode(PlayerId player, ElementId element);
        Task DisableResurrectionNodes(ConstructId constructId);
        Task ResurrectionNodeBroken(ElementId elementId);
    }
}
