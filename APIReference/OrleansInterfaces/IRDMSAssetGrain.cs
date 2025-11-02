using NQ.RDMS;
using Orleans;

namespace NQ.Interfaces
{
    public interface IRDMSAssetGrain : IGrainWithStringKey
    {
        Task UpdateTags(ulong requester, AssetUpdateTags update);
        Task UpdateTags(AssetUpdateTags update);
        Task<AssetTagData> GetTagList(AssetId asset);
        Task<EntityId> GetOwner();
        Task<(EntityId, AssetTagData)> GetOwnerAndTagList(AssetId asset);
    }
}
