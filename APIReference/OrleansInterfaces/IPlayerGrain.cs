using NQ.RDMS;
using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public interface IPlayerGrain : IGrainWithIntegerKey, IEntityGrain
    {
        #region connection
        Task<PlayerConnectionReady> NotifyConnection(PlayerConnectionInfo connectionInfo);
        Task NotifyDisconnection();
        [AlwaysInterleave]
        Task<bool> IsConnected();
        Task<PlayerLoginState> GetPlayerLoginState(AuthUserInfo authInfo);
        Task<LoginResponseOrCreation> GetLoginResponseOrCreation(AuthUserInfo authInfo);
        Task<RelativeLocation> CreatePlayerAvatar(PlayerCreationInfo creationInfo);
        Task InventoryReady();
        #endregion

        #region playerinfo
        [AlwaysInterleave]
        Task<PlayerInfo> GetPlayerInfo();
        Task SetPlayerInfo(PlayerInfo info);

        [AlwaysInterleave]
        Task<bool> IsMuted();
        Task UpdatePlayerProperty(PlayerPropertyUpdate update, bool fromServer = false);
        Task UpdatePlayerPropertyEx(string name, PropertyValue value, bool fromServer);
        [AlwaysInterleave]
        Task<PlayerMass> GetMass();
        [AlwaysInterleave]
        Task TeleportPlayer(RelativeLocation location);
        [AlwaysInterleave]
        Task<PlayerPositionUpdate> GetPositionUpdate();
        [AlwaysInterleave]
        Task<ConstructId> GetPlanet();
        [AlwaysInterleave]
        Task OnPlayerChangeConstruct(ParentingChange change);
        [AlwaysInterleave]
        Task<PlayerRoles> GetRoles();
        [AlwaysInterleave]
        Task<Titles> GetTitleList();
        [AlwaysInterleave]
        Task SetActiveTitle(string title);
        [AlwaysInterleave]
        Task SetActivePet(ulong pet);
        #endregion
        #region admin
        [AlwaysInterleave]
        Task<bool> IsAdmin();
        [AlwaysInterleave]
        Task<bool> IsBot();
        Task HandleAnticheatMessage(AnticheatMessage message);
        Task NotifyAdminRightsSet(bool admin);
        Task NotifyReset();
        Task PlayerHardRespawn();
        Task PlayerDieOperation(PlayerDeathInfo deathInfo);
        #endregion

        #region organization
        Task<OrganizationId> CreateOrganization(OrganizationCreationData data);
        [AlwaysInterleave]
        Task<OrganizationIds> MyOrganizations(OrganizationRankFilter level);
        [AlwaysInterleave]
        Task<OrganizationMemberships> MyOrganizationsWithRoles(OrganizationRankFilter level);
        Task<OrganizationMemberships> OrganizationsInCommon(ulong playerId);
        Task<OrganizationAndRights> OrganizationsWithWalletRights();
        #endregion

        #region FTUE
        Task TestLanderSequence();
        Task<StartupChoices> GetStartupChoices();
        Task<RelativeLocation> StartupSequence(StartupData data);
        Task LanderTouchdown(NQ.LanderTouchdown td);
        Task<TerritoryDetails> ReserveTerritory(TerritoryClaim claim);
        #endregion

        #region sandbox interaction
        Task<int> StopMiningChargeTimer();
        Task StartMiningChargeTimer(int stashedValue);
        #endregion

        [AlwaysInterleave]
        Task<bool> CanCreateConstruct();
        [AlwaysInterleave]
        Task<ConstructCount> GetConstructCount();
        [AlwaysInterleave]
        Task<OwnedConstructDataList> ConstructOwnedPositionGet();
        Task<OwnedConstructDataList> GetWarpDestinations();

        [AlwaysInterleave]
        Task<List<ActorId>> GetRDMSActorIds();


        Task<RelativeLocation> TeleportToReferrer(ulong playerId);
        Task AchievementCompleted(AchievementUnlock au);

        Task ToggleDRMFlag(ToggleDRM request);

        [AlwaysInterleave]
        Task<Currency> GetWallet();

        [AlwaysInterleave]
        Task SetToolbar(ToolbarInfo info);
        [AlwaysInterleave]
        Task<ToolbarInfo> GetToolbar();


        [AlwaysInterleave]
        Task<TerritoryList> MyTerritories();

        [AlwaysInterleave]
        Task<ElementInfo> AddConstructElement(ElementInfo element);

        Task TeleportTo(RelativeLocation rl, bool sendTeleportRequestToClient = true);

        Task ConsumeWarpCells(WarpRequest warpRequest);

        Task SendDeltaSettings(DeltaSettings settings);

        Task TerritoryMoneyTransfer(TerritoryBalanceUpdate tbu);
        Task ConsumeTerritoryKey(TerritoryKeyConsumption tkc);
        Task TokenizeTerritory(TerritoryTileIndex territory);
        Task RequisitionConstruct(ConstructId constructId);
        Task CancelRequisitionConstruct(ConstructId constructId);

        Task<bool> ConsumeMiningCalibrationCharge();

        Task InternalAllocateOrgConstructSlots(ConstructSlotAllocation allocs, bool bypassCooldown);
        Task AllocateOrgConstructSlots(ConstructSlotAllocation allocs);
        Task<ConstructSlotAllocation> GetOrgConstructAllocation();
        Task<ConstructSlotAllocationLog> GetOrgConstructAllocationLog(ulong orgId);
    }
}

