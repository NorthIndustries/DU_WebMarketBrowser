using NQ.Interfaces.Debug;
using NQ.Interfaces.Organization;
using NQ.RDMS;
using NQutils.Exceptions;
using Orleans;

namespace NQ.Interfaces
{
    public static class GrainGetterExtensions
    {
        public static IContainerGrain GetContainerGrain(this IGrainFactory client, ElementId containerId)
            => client.GetGrain<IContainerGrain>(containerId.AsLong);
        public static ElementId GetGrainElementId(this IContainerGrain grain)
            => (ulong)grain.GetPrimaryKeyLong();

        public static IInventoryGrain GetInventoryGrain(this IGrainFactory client, PlayerId player)
            => client.GetGrain<IInventoryGrain>(player.AsLong);

        public static IAbstractStorage GetAbstractStorage(this IGrainFactory client, PlayerId player, ElementId containerId)
        {
            if (containerId != 0)
                return client.GetContainerGrain(containerId);
            else
                return client.GetInventoryGrain(player);
        }
        public static ITalentGrain GetTalentGrain(this IGrainFactory client, PlayerId player)
            => client.GetGrain<ITalentGrain>($"{player.id}");
        public static PlayerId GetGrainPlayerId(this ITalentGrain grain)
            => UInt64.Parse(grain.GetPrimaryKeyString());

        public static IRecipeGrain GetRecipeGrain(this IGrainFactory client, PlayerId player)
            => client.GetGrain<IRecipeGrain>(player.AsLong);

        public static IPlayerGrain GetPlayerGrain(this IGrainFactory client, PlayerId player)
            => client.GetGrain<IPlayerGrain>(player.AsLong);
        public static IPlayerGrain GetPlayerGrain(this IGrainFactory client, long player)
            => client.GetGrain<IPlayerGrain>(player);

        public static IEntityGrain GetEntityGrain(this IGrainFactory client, EntityId entity)
        {
            if (entity.IsPlayer())
                return client.GetPlayerGrain(entity.playerId);
            else if (entity.IsOrg())
                return client.GetOrganizationGrain(entity.organizationId);
            throw new BusinessException(ErrorCode.InvalidParameters, $"This is neither a player nor an org {entity.LazyJSon()}");
        }

        public static IPlayerInventoryGrain GetPlayerInventoryGrain(this IGrainFactory client, PlayerId player)
            => client.GetGrain<IPlayerInventoryGrain>(player.AsLong);

        public static IPlayerUsageGrain GetPlayerUsageGrain(this IGrainFactory client, PlayerId player)
            => client.GetGrain<IPlayerUsageGrain>(player.AsLong);

        public static IRDMSRightGrain GetRDMSRightGrain(this IGrainFactory client, PlayerId player)
            => client.GetGrain<IRDMSRightGrain>(player.AsLong);

        public static IRDMSRegistryGrain GetRDMSRegistryGrain(this IGrainFactory client, EntityId entity)
            => client.GetGrain<IRDMSRegistryGrain>(entity.ToGrainKey());

        public static EntityId GetGrainEntityId(this IRDMSRegistryGrain grain)
        {
            string identity = grain.GetPrimaryKeyString();
            List<string> parts = identity.Split("/", StringSplitOptions.RemoveEmptyEntries).ToList();
            return new EntityId().FromGrainKey(parts);
        }

        public static IRDMSAssetGrain GetRDMSAssetGrain(this IGrainFactory client, AssetId assetId)
        {
            if (assetId.type == AssetType.Construct)
                return client.GetGrain<IRDMSConstructAssetGrain>(assetId.ToGrainKey());
            else if (assetId.type == AssetType.Element)
                return client.GetGrain<IRDMSConstructAssetGrain>((new AssetId { type = AssetType.Construct, construct = assetId.construct }).ToGrainKey());
            else
                return client.GetGrain<IRDMSOtherAssetGrain>(assetId.ToGrainKey());
        }

        public static AssetId GetGrainAssetId(this IRDMSAssetGrain grain)
        {
            string identity = grain.GetPrimaryKeyString();
            List<string> parts = identity.Split("/", StringSplitOptions.RemoveEmptyEntries).ToList();
            return new AssetId().FromGrainKey(parts);
        }

        public static IAsteroidManagerGrain GetAsteroidManagerGrain(this IGrainFactory client)
            => client.GetGrain<IAsteroidManagerGrain>(0);

        public static IIndustryUnitGrain GetIndustryUnitGrain(this IGrainFactory client, ulong elementId)
            => client.GetGrain<IIndustryUnitGrain>($"{elementId}");

        public static IRepairUnitGrain GetRepairUnitGrain(this IGrainFactory client, ElementId repairUnit)
            => client.GetGrain<IRepairUnitGrain>(repairUnit.AsLong);

        public static ISPSDirectoryGrain GetSPSDirectoryGrain(this IGrainFactory client)
            => client.GetGrain<ISPSDirectoryGrain>(0);
        public static ISPSGrain GetSPSGrain(this IGrainFactory client, PlayerId id)
            => client.GetGrain<ISPSGrain>(id.AsLong);
        public static IResurrectionNodeGrain GetResurrectionNodeGrain(this IGrainFactory client)
            => client.GetGrain<IResurrectionNodeGrain>(0);

        public static IBookmarkGrain GetBookmarkGrain(this IGrainFactory client)
            => client.GetGrain<IBookmarkGrain>(0);

        public static IMiningPoolGrain GetMiningPoolGrain(this IGrainFactory client, ulong construct, int tile)
            => client.GetGrain<IMiningPoolGrain>($"{construct}/{tile}");

        public static IBootstrapGrain GetBootstrapGrain(this IGrainFactory client) => client.GetGrain<IBootstrapGrain>(Guid.Empty);

        public static IOrganizationGrain GetOrganizationGrain(this IGrainFactory client, OrganizationId org)
            => client.GetGrain<IOrganizationGrain>(org.AsLong);

        public static IOrganizationDirectoryGrain GetOrganizationDirectoryGrain(this IGrainFactory client)
            => client.GetGrain<IOrganizationDirectoryGrain>(0);

        public static ILocatorGrain GetLocatorGrain(this IGrainFactory client, LocationDescriptor descriptor)
            => client.GetGrain<ILocatorGrain>(descriptor.GrainKey());
        public static INotificationGrain GetNotificationGrain(this IGrainFactory client, PlayerId playerId)
            => client.GetGrain<INotificationGrain>(playerId.AsLong);

        public static IConstructInfoGrain GetConstructInfoGrain(this IGrainFactory client, ConstructId constructId)
            => client.GetGrain<IConstructInfoGrain>(constructId.AsLong);


        public static IConstructFightGrain GetConstructFightGrain(this IGrainFactory client, ConstructId constructId)
            => client.GetGrain<IConstructFightGrain>(constructId.AsLong);

        public static IConstructGrain GetConstructGrain(this IGrainFactory client, ConstructId constructId)
            => client.GetGrain<IConstructGrain>(constructId.AsLong);
        public static IConstructGrain GetConstructGrain(this IGrainFactory client, long constructId)
            => client.GetGrain<IConstructGrain>(constructId);

        public static IRadarGrain GetRadarGrain(this IGrainFactory client, ElementId elementId)
            => client.GetGrain<IRadarGrain>(elementId.AsLong);

        public static IConstructParentingGrain GetConstructParentingGrain(this IGrainFactory client)
            => client.GetGrain<IConstructParentingGrain>(0);

        public static IPlayerSandboxGrain GetPlayerSandboxGrain(this IGrainFactory client, PlayerId playerId)
            => client.GetGrain<IPlayerSandboxGrain>(playerId.AsLong);

        public static IPlanetTerritoryGrain GetPlanetTerritoryGrain(this IGrainFactory client, ConstructId cid)
            => client.GetGrain<IPlanetTerritoryGrain>(cid.AsLong);

        public static IDirectServiceGrain GetDirectServiceGrain(this IGrainFactory client)
            => client.GetGrain<IDirectServiceGrain>(0);

        public static IConstructCreatorGrain GetConstructCreatorGrain(this IGrainFactory client)
            => client.GetGrain<IConstructCreatorGrain>(0);

        public static IScenegraphGrain GetScenegraphGrain(this IGrainFactory client)
            => client.GetGrain<IScenegraphGrain>(0);
        public static IPlayerDirectoryGrain GetPlayerDirectoryGrain(this IGrainFactory client)
            => client.GetGrain<IPlayerDirectoryGrain>(0);


        public static IMarketGrain GetMarketGrain(this IGrainFactory client)
            => client.GetGrain<IMarketGrain>(0);

        public static IVoteGrain GetVoteGrain(this IGrainFactory client, ulong voteId)
            => client.GetGrain<IVoteGrain>((long)voteId);

        public static IDispenserGrain GetDispenserGrain(this IGrainFactory client, ulong dispenserId)
            => client.GetGrain<IDispenserGrain>((long)dispenserId);

        public static IChatGrain GetChatGrain(this IGrainFactory client, PlayerId playerId)
            => client.GetGrain<IChatGrain>(playerId.AsLong);

        public static IPlayerMissionGrain GetPlayerMissionGrain(this IGrainFactory client, PlayerId playerId)
            => client.GetGrain<IPlayerMissionGrain>(playerId.AsLong);

        public static IPlayerFormalMissionGrain GetPlayerFormalMissionGrain(this IGrainFactory client, PlayerId playerId)
            => client.GetGrain<IPlayerFormalMissionGrain>(playerId.AsLong);

        public static IAsteroidTrackerGrain GetAsteroidTrackerGrain(this IGrainFactory client, ElementId elementId)
            => client.GetGrain<IAsteroidTrackerGrain>(elementId.AsLong);

        public static IMiningUnitGrain GetMiningUnitGrain(this IGrainFactory client, ElementId elementId)
            => client.GetGrain<IMiningUnitGrain>(elementId.AsLong);

        public static IMiningPoolGrain GetAlienPoolGrain(this IGrainFactory client, ulong construct)
            => client.GetGrain<IMiningPoolGrain>($"0/{construct}");

        public static IElementManagementGrain GetElementManagementGrain(this IGrainFactory client)
            => client.GetGrain<IElementManagementGrain>(0);

        public static IPlanetPoolGrain GetPlanetPoolGrain(this IGrainFactory client, ConstructId planetId)
            => client.GetGrain<IPlanetPoolGrain>(planetId.AsLong);

        public static IAlienCoreScannerGrain GetAlienCoreScannerGrain(this IGrainFactory client)
            => client.GetGrain<IAlienCoreScannerGrain>(0);

        public static IBlueprintGrain GetBlueprintGrain(this IGrainFactory client)
            => client.GetGrain<IBlueprintGrain>(0);

        public static IFriendGrain GetFriendGrain(this IGrainFactory client, PlayerId playerId)
            => client.GetGrain<IFriendGrain>(playerId.AsLong);


        public static IConstructElementsGrain GetConstructElementsGrain(this IGrainFactory client, ConstructId cid)
            => client.GetGrain<IConstructElementsGrain>(cid.AsLong);
        public static IConstructDamageElementsGrain GetConstructDamageElementsGrain(this IGrainFactory client, ConstructId cid)
            => client.GetGrain<IConstructDamageElementsGrain>(cid.AsLong);

        public static IConstructElementCollisionGrain GetConstructElementCollisionGrain(this IGrainFactory client, ConstructId cid)
            => client.GetGrain<IConstructElementCollisionGrain>(cid.AsLong);

        public static IShootInterface GetFakeShotGrain(this IGrainFactory client)
            => client.GetGrain<IFakeShotGrain>(0);
        public static IShootInterface GetWeaponGrain(this IGrainFactory client, ElementId weaponId)
        {
            if (weaponId == 0)
                return client.GetGrain<IFakeShotGrain>(0);
            return client.GetGrain<IWeaponGrain>(weaponId.AsLong);
        }
        public static IZonesGrain GetZonesGrain(this IGrainFactory client)
            => client.GetGrain<IZonesGrain>(0);

        public static IBotCallsGrain GetBotCallsGrain(this IGrainFactory client)
            => client.GetGrain<IBotCallsGrain>(0);

        public static IPlayerResearchGrain GetPlayerResearchGrain(this IGrainFactory client, PlayerId player)
            => client.GetGrain<IPlayerResearchGrain>(player.AsLong);

        public static IConstructGCGrain GetConstructGCGrain(this IGrainFactory client)
            => client.GetGrain<IConstructGCGrain>(Guid.Empty);

        public static IReminderGrain GetReminderGrain(this IGrainFactory client)
            => client.GetGrain<IReminderGrain>(0);

        public static IMissionTimeouterGrain GetMissionTimeouterGrain(this IGrainFactory client)
            => client.GetGrain<IMissionTimeouterGrain>(Guid.Empty);

        public static IAlienSpaceCoreManagerGrain GetAlienSpaceCoreManagerGrain(this IGrainFactory client)
            => client.GetGrain<IAlienSpaceCoreManagerGrain>(0);

        public static IOrganizationConstructLimitEnforcer GetOrganizationConstructLimitEnforcerGrain(this IGrainFactory client)
            => client.GetGrain<IOrganizationConstructLimitEnforcer>(0);

        public static IConstructAutoCompactorGrain GetConstructAutoCompactorGrain(this IGrainFactory client)
            => client.GetGrain<IConstructAutoCompactorGrain>(Guid.Empty);

        public static IMarketPublicRecordsGrain GetMarketPublicRecordsGrain(this IGrainFactory client)
            => client.GetGrain<IMarketPublicRecordsGrain>(Guid.Empty);

        public static IWreckManagerGrain GetWreckManagerGrain(this IGrainFactory client)
            => client.GetGrain<IWreckManagerGrain>(0);

        public static IBarterGrain GetBarterGrain(this IGrainFactory client, string key)
            => client.GetGrain<IBarterGrain>(key);

        public static ILockManagerGrain GetLockManagerGrain(this IGrainFactory client, string key)
            => client.GetGrain<ILockManagerGrain>(key);

        public static IVirtualDispenserContentGrain GetVirtualDispenserContentGrain(this IGrainFactory client, ElementId dispenserId)
            => client.GetGrain<IVirtualDispenserContentGrain>(dispenserId.AsLong);

        public static IPingGrain GetPing(this IGrainFactory client)
            => client.GetGrain<IPingGrain>(0);

        public static ISentinelMissionGrain GetSentinelMissionGrain(this IGrainFactory client, ulong pid)
            => client.GetGrain<ISentinelMissionGrain>((long)pid);

        public static INpcShotGrain GetNpcShotGrain(this IGrainFactory client, ulong gid=0)
            => client.GetGrain<INpcShotGrain>(0);

        public static ISchematicContainerGrain GetSchematicContainerGrain(this IGrainFactory client, ConstructId construct)
            => client.GetGrain<ISchematicContainerGrain>(construct.AsLong);

        public static IModManagerGrain GetModManagerGrain(this IGrainFactory client)
            => client.GetGrain<IModManagerGrain>(0);
    }
}
