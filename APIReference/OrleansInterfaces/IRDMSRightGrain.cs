using NQ.RDMS;
using Orleans;

namespace NQ.Interfaces
{
    public interface IRDMSRightGrain : IGrainWithIntegerKey
    {
        //
        // Get Players Rights on an Asset
        //
        Task<RightList> GetRightsForPlayerOnAsset(PlayerId playerId, AssetId asset, bool adminOverride = false);
        // Default argument is screwing up the CxxBridge
        Task<RightList> GetRightsForPlayerOnAssetNoOverride(AssetId asset);

        Task<RightList> GetRightsForOrganizationOnAsset(long organizationId, AssetId asset);

        //
        // Debug / Tests
        //
        Task ClearCache();
    }
}
