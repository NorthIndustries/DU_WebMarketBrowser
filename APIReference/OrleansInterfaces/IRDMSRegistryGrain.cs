using NQ.RDMS;
using Orleans;

namespace NQ.Interfaces
{
    public interface IRDMSRegistryGrain : IGrainWithStringKey
    {
        //
        // Registry
        //
        Task<Registry> GetRegistry();

        //
        // Asset
        //
        Task<AssetInfoList> GetAssetListFromTag(TagId tagId);

        //
        // Actor
        //
        Task<ActorId> CreateActor(ActorData data);
        Task DeleteActor(ActorId actor);
        Task UpdateActor(ActorData newPolicyData);
        Task<ActorData> GetActorData(ActorId actor);
        Task<ActorDataList> GetActorDataList();

        //
        // Tag
        //
        Task<TagId> CreateTag(TagData newTag);
        Task<TagId> CreateOrGetTag(TagData newTag);
        Task DeleteTag(TagId tag);
        Task UpdateTag(TagData newTagData);
        Task<TagData> GetTagData(TagId tag);
        Task<TagDataList> GetTagDataList();

        //
        // Policy
        //
        Task<PolicyId> CreatePolicy(PolicyData policy);
        Task DeletePolicy(PolicyId policy);
        Task UpdatePolicy(PolicyData newPolicyData);
        Task<PolicyData> GetPolicyData(PolicyId policy);
        Task<PolicyDataList> GetPolicyDataList();

        //
        // Debug / Tests
        //
        Task ClearCache();
    }
}
