using Backend.Fixture.Construct;
using NQ;
using NQ.RDMS;

namespace Backend.Business
{
    public interface IDataAccessor
    {

        //////////////////////////////////////////////////////////////
        /// Connectivity
        //////////////////////////////////////////////////////////////

        //////////////////////////////////////////////////////////////
        /// Player
        //////////////////////////////////////////////////////////////

        /// <summary>
        ///     Send an in game popup message to a player.
        /// </summary>
        /// <param name="playerId">
        ///     The player to send the message.
        /// </param>
        /// <param name="message">
        ///     The message to send.
        /// </param>
        Task PlayerPopupAsync(PlayerId playerId, string message);

        /// <summary>
        ///     Send an in game popup message to all players.
        /// </summary>
        /// <param name="message">
        ///     The message to send.
        /// </param>
        Task PopupAllAsync(string message);

        /// <summary>
        ///     Disconnect a player from the game.
        /// </summary>
        /// <param name="playerId">
        ///     The player to disconnect.
        /// </param>
        Task PlayerDisconnectAsync(PlayerId playerId, DisconnectionNotification reason = null);

        /// <summary>
        ///     Demote the admin status of a player.
        /// </summary>
        /// <param name="playerId">
        ///     The player to demote.
        /// </param>
        /// <returns></returns>
        Task PlayerDemoteAdminAsync(PlayerId playerId);

        /// <summary>
        ///     Promote a player as admin.
        /// </summary>
        /// <param name="playerId">
        ///     The player to promote.
        /// </param>
        Task PlayerPromoteAdminAsync(PlayerId playerId);

        /// <summary>
        ///     Teleport a player to another location.
        ///     The destination is represented by a RelativeLocation.
        /// </summary>
        /// <param name="playerId">
        ///     The player to teleport.
        /// </param>
        /// <param name="location">
        ///     The destination.
        /// </param>
        Task PlayerTeleportAsync(PlayerId playerId, RelativeLocation location);
        /// <summary>
        ///     Sets the player as never connected allowing him to recreate a new avatar
        /// </summary>
        /// <param name="playerId"></param>
        /// <returns></returns>
        Task ResetPlayerAsync(PlayerId playerId);

        /// <summary>
        ///     Get the inventory of a player.
        /// </summary>
        /// <param name="playerId">
        ///     The player id.
        /// </param>
        /// <returns>
        ///     The inventory.
        /// </returns>
        Task<StorageInfo> GetInventoryAsync(PlayerId playerId);

        /// <summary>
        ///     Reload the inventory grain of a player
        /// </summary>
        Task ReloadInventoryAsync(PlayerId playerId);

        /// <summary>
        ///     Fill the inventory of the player with all available
        ///     items and a custom quantity.
        /// </summary>
        /// <param name="playerId">
        ///     The player to fill.
        /// </param>
        /// <param name="quantity">
        ///     The quantity of each item.
        /// </param>
        Task PlayerInventoryFillAsync(PlayerId playerId, long quantity);

        /// <summary>
        ///     Reset the player's inventory with the default one.
        /// </summary>
        /// <param name="playerId">
        ///     The player to reset.
        /// </param>
        Task PlayerInventoryResetToDefaultAsync(PlayerId playerId);
        /// <summary>
        ///     Remove all items from a player inventory
        /// </summary>
        /// <param name="playerId">
        ///     The player to clear.
        /// </param>
        Task PlayerInventoryClearAsync(PlayerId playerId);
        /// <summary>
        ///     Restore any missing tools in a player inventory
        /// </summary>
        /// <param name="playerId">
        ///     The player to restore.
        /// </param>
        Task PlayerInventoryRestoreToolsAsync(PlayerId playerId);

        /// <summary>
        ///     Replace a slot in an inventory
        /// </summary>
        /// <param name="playerId">
        ///     The player to change inventory.
        /// </param>
        /// <param name="slot">
        ///     The slot to update.
        /// </param>
        Task PlayerInventoryReplaceAsync(PlayerId playerId, StorageSlot slot);

        /// <summary>
        ///     Create an item for a player
        /// </summary>
        /// <param name="playerId">The player to give the item</param>
        /// <param name="item">The item to give</param>
        /// <returns></returns>
        Task PlayerInventoryCreateAsync(PlayerId playerId, ItemInfo item);

        /// <summary>
        ///     Adds item to a slot
        /// </summary>
        /// <param name="playerId">The player to give the item</param>
        /// <param name="item">The item to give</param>
        /// <returns></returns>
        Task PlayerInventoryAddAsync(PlayerId playerId, StorageSlot item);

        /// <summary>
        ///     Adds items
        /// </summary>
        /// <param name="playerId">The player to give the item</param>
        /// <param name="items">The items to give</param>
        /// <returns></returns>
        Task<StorageSlot> PlayerInventoryGiveAsync(PlayerId playerId, ItemAndQuantity items);

        /// <summary>
        ///     Get the primary container of a player.
        /// </summary>
        /// <param name="playerId">
        ///     The player id.
        /// </param>
        /// <returns>
        ///     The primary container.
        /// </returns>
        Task<ElementId> GetPrimaryContainerAsync(PlayerId playerId);

        /// <summary>
        ///     Set Primary Container for a Player
        /// </summary>
        /// <param name="playerId">
        ///     The player.
        /// </param>
        /// <param name="containerId">
        ///     The container Id
        /// </param>
        Task SetPrimaryContainerAsync(PlayerId playerId, long containerId);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        /// <param name="q"></param>
        /// <returns></returns>
        Task CheckCanUse(PlayerId p, ElementUseQuery q);

        /// <summary>
        ///     Retrive the player's talent progression.
        /// </summary>
        /// <param name="playerId">
        ///     The player to retrive;
        /// </param>
        Task<PlayerTalentState> PlayerTalentAsync(PlayerId playerId);

        /// <summary>
        ///     Give all talents for free to a player.
        /// </summary>
        /// <param name="playerId">
        ///     The player id.
        /// </param>
        Task PlayerTalentGiveAllAsync(PlayerId playerId);

        /// <summary>
        ///     Purchase a talent for a player using its own points.
        /// </summary>
        /// <param name="playerId">
        ///     The player id.
        /// </param>
        /// <param name="talentId">
        ///     The talent to purchase.
        /// </param>
        /// <param name="level">
        ///     The level to purchase (current+1).
        /// </param>
        Task PlayerTalentPurchaseAsync(PlayerId playerId, ulong talentId, long level);

        /// <summary>
        ///     Allow the player to respec his talents by resetting all
        ///     owned talents to the level 0 and giving the corresponding
        ///     amount of points.
        /// </summary>
        /// <param name="playerId">
        ///     The player to reset.
        /// </param>
        Task PlayerTalentRespecAllAsync(PlayerId playerId);

        /// <summary>
        ///     Respec a selected talent for a player by resetting it
        ///     to the level 0 and giving the corresponding amount of
        ///     points.
        /// </summary>
        /// <param name="playerId">
        ///     The player to respec.
        /// </param>
        /// <param name="talentId">
        ///     The talent to respec.
        /// </param>
        Task PlayerTalentRespecAsync(PlayerId playerId, ulong talentId);

        /// <summary>
        ///     Set the total amount of talent points with a new one.
        /// </summary>
        /// <param name="playerId">
        ///     The player id.
        /// </param>
        /// <param name="points">
        ///     The number of points to give.
        /// </param>
        Task PlayerTalentSetAvailableAsync(PlayerId playerId, long points);

        /// <summary>
        ///     Fetch the player's marker orders.
        /// </summary>
        /// <param name="request">
        ///     Request
        /// </param>
        /// <returns>
        ///     All the market orders of the player.
        /// </returns>
        Task<MarketOrders> PlayerMarketOrdersAsync(MarketSelectRequest request);

        Task PlayerWalletUpdateAsync(ulong playerId, long amount);

        //////////////////////////////////////////////////////////////
        /// Construct
        //////////////////////////////////////////////////////////////


        /// <summary>
        ///     Reload an in-game construct.
        /// </summary>
        /// <param name="constructId">
        ///     The construct to reload.
        /// </param>
        /// <param name="soft">hard or soft reload, whatever that means.</param>

        Task ReloadConstructAsync(ConstructId constructId, bool soft = false);

        Task<ConstructInfo> GetConstructInfoAsync(ConstructId constructId);

        Task ContainerMassUpdate(ElementInConstruct eic, double mass);

        Task<System.Memory<byte>> GetConstructMesh(ulong constructId);
        Task SetConstructMesh(ulong constructId, byte[] data);
        /// <summary>
        ///     Teleport a construct to a new position.
        /// </summary>
        /// <param name="constructId">
        ///     The construct to teleport.
        /// </param>
        /// <param name="location">
        ///     Destination
        /// </param>
        Task TeleportConstructAsync(ConstructId constructId, RelativeLocation location);
        Task UnparentConstructAsync(ConstructId constructId, UnparentConstructRequest request, ConstructId? parentOverride);
        Task SoftDeleteConstructAsync(ConstructId constructId);
        Task HardDeleteConstructAsync(ConstructId constructId);
        //////////////////////////////////////////////////////////////
        /// Territory
        //////////////////////////////////////////////////////////////
        Task ClaimTerritory(TerritoryClaim claim, PlayerId playerId);
        Task<TerritoryDetails> ClaimTerritoryBySpecialOwner(TerritoryClaim claim, PlayerId playerId);
        Task ReleaseTerritory(TerritoryRelease release, PlayerId playerId);
        Task UpdateTerritoryFixtures(long planetId, List<TerritoryDetails> territories);
        Task UpdateTerritory(TerritoryUpdate update, PlayerId playerId);

        Task<UsedElementList> ConstructUsedElements(ConstructId constructId);

        //////////////////////////////////////////////////////////////
        /// Element
        //////////////////////////////////////////////////////////////
        Task ElementInventoryGiveAsync(ElementId elementId, NQ.ItemAndQuantity itemAndQuantity);
        Task ElementInventoryReplaceAsync(ElementId elementId, NQ.StorageSlot slot);
        Task ElementInventoryClearAsync(ElementId elementId);

        //////////////////////////////////////////////////////////////
        /// Friend list
        //////////////////////////////////////////////////////////////

        /// <summary>
        ///     Reply to a friend request.
        /// </summary>
        /// <param name="playerId">
        ///     The replying player.
        /// </param>
        /// <param name="friendId">
        ///     The requester player.
        /// </param>
        /// <param name="accepted">
        ///     The request answer.
        /// </param>
        Task FriendListAnswerRequestAsync(FriendRequestResponse response);

        /// <summary>
        ///     Removes a friend from the friend list.
        /// </summary>
        /// <param name="playerId">
        ///     The requesting player.
        /// </param>
        /// <param name="friendId">
        ///     The player to remove from the friend list.
        /// </param>
        Task FriendListRemoveFriendAsync(PlayerId playerId, PlayerId friendId);

        /// <summary>
        ///     Returns the player's friend list.
        /// </summary>
        /// <param name="playerId">
        ///     The player id.
        /// </param>
        Task<FriendList> GetFriendListAsync(PlayerId playerId);

        /// <summary>
        ///     Send a friend request to another player.
        /// </summary>
        /// <param name="playerId">
        ///     The requesting player.
        /// </param>
        /// <param name="toPlayer">
        ///     The targeted player.
        /// </param>
        Task<FriendRequest> FriendListMakeRequestAsync(FriendRequest request);

        //////////////////////////////////////////////////////////////
        /// Connectivity
        //////////////////////////////////////////////////////////////

        /// <summary>
        ///     Kick a player from an organization.
        /// </summary>
        /// <param name="organizationId">
        ///     The organization id.
        /// </param>
        /// <param name="playerId">
        ///     The player to kick.
        /// </param>
        Task OrganizationKickPlayerAsync(long organizationId, PlayerId playerId);

        /// <summary>
        ///     Set an organization description.
        /// </summary>
        /// <param name="organizationId">
        ///     The organization id.
        /// </param>
        /// <param name="description">
        ///     The new description.
        /// </param>
        Task OrganizationSetDescriptionAsync(long organizationId, string description);

        /// <summary>
        ///    Set an organization description.
        /// </summary>
        /// <param name="organizationId">
        ///     The organization id.
        /// </param>
        /// <param name="logo">
        ///     The new logo.
        /// </param>
        Task OrganizationSetLogoAsync(long organizationId, string logo);

        /// <summary>
        ///     Delete an organization.
        /// </summary>
        /// <param name="organizationId">
        ///     The organization id.
        /// </param>
        /// <returns></returns>
        Task OrganizationDeleteAsync(long organizationId);

        /// <summary>
        ///     Apply a vote on an organization.
        /// </summary>
        /// <param name="organizationId">
        ///     The organization id.
        /// </param>
        /// <param name="voteKind"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        Task OrganizationApplyVoteAsync(long organizationId, long voteKind, long target);
        Task<PlayerId> OrganizationEffectiveSuperLegate(long organizationId);
        Task<Organizations> OrganizationDirectorySearchBy(long id, OrganizationSearch search);
        Task OrganizationWalletAdd(OrganizationId id, long amount);
        Task<Organization> OrganizationGet(OrganizationId id);

        Task BlueprintAdminCreate(long constructId, PlayerId playerId, long maxUse, bool magic, BlueprintCreate payload);
        /// Backup the construct (create a blueprint not in anyone's inventory)
        Task BlueprintBackupConstruct(long constructId, BlueprintCreate payload);

        /// <summary>
        /// Export the given blueprint to json data
        /// </summary>
        Task<byte[]> BlueprintExport(long blueprintId);

        /// <summary>
        /// Import the given blueprint from a json string
        /// </summary>
        Task<BlueprintId> BlueprintImport(byte[] bpdata, EntityId creatorId = null);

        /// <summary>
        ///    Set the value of a dynamic Element property
        /// </summary>
        Task SetDynamicProperty(ElementPropertyUpdate epu);
        /// <summary>
        ///    Set the value of a dynamic Element property
        /// </summary>
        Task SetDynamicProperties(IEnumerable<ElementPropertyUpdate> epu);

        /// <summary>
        ///    Set the value of a dynamic Player property
        /// </summary>
        Task SetDynamicProperty(PlayerPropertyUpdate ppu, bool fromServer = false);

        Task<MarketList> FetchMarketListAsync();
        Task<MarketList> FetchMarketListAsync(ConstructId id);

        Task<MarketInfo> FetchMarketAsync(ulong marketId);
        Task<MarketInfo> FetchMarketByElementId(ulong elementId);
        Task MarketBulkOrders(ulong marketId, MarketBulkOrders orders);

        Task<MarketOrderResponse> MarketPostOrder(MarketRequest marketRequest, PlayerId id);
        Task<MarketOrderResponse> MarketUpdateOrder(MarketRequest marketRequest, PlayerId id);
        Task<MarketOrderResponse> MarketPostInstantOrder(MarketRequest marketRequest, PlayerId id);
        Task<MarketOrderResponse> MarketCancelOrder(ulong orderId, EntityId ownerId);
        Task<MarketStorageInfo> MarketGetContainer(MarketSelectRequest req);
        Task MarketDeleteOrderAsync(ulong orderId, PlayerId playerId);
        Task<MarketStorageMoveInfo> MarketMoveContainer(MarketStorageMoveInfo storageRetrieve, ulong playerId);
        Task<VoxelMetadata> VoxelConstructMetaData(long constructId);
        Task<VoxelMetadata> VoxelBlueprintMetaData(long constructId);

        Task VoxelDeleteConstruct(long constructId);
        Task VoxelDeleteBlueprint(BlueprintId blueprintId);

        // RDMS
        Task<Registry> GetPlayerRDMSRegistryAsync(PlayerId playerId, EntityId owner);
        Task ClearRDMSCache(PlayerId playerId);

        /// <summary>
        /// Clear all the recipes of a player (no refound)
        /// </summary>
        Task PlayerRecipeClearAll(PlayerId playerId);

        /// <summary>
        /// Clear all the queued recipes of a player and abort the first one
        /// </summary>
        Task PlayerRecipeClearAbort(PlayerId playerId);

        /// <summary>
        /// Instantly finish player current recipe
        /// </summary>
        Task PlayerRecipeInstantCraft(PlayerId playerId);

        /// <summary>
        ///  Get the crafting queue of a player
        /// </summary>
        Task<List<RecipeStatus>> GetPlayerRecipesAsync(PlayerId playerId);

        /// <summary>
        /// Set up markets based on existing market elements.
        /// This is only for the integration tests at the moment.
        /// </summary>
        /// <returns></returns>
        Task BoostrapMarkets();

        /// <summary>
        /// Export the given construct to a json fixture
        /// </summary>
        Task<string> ExportFixtureFull(ulong constructId);

        /// <summary>
        /// Import the given json fixture
        /// </summary>
        Task<List<ulong>> LoadFixture(string json_fixture);

        /// <summary>
        /// Updates construct <see cref="target"/> to match <see cref="fixt"/> based on <see cref="parameters"/>
        /// </summary>
        Task CloneFixture(ConstructFixture fixt, ConstructId target, ConstructSourceDef parameters);

        /// <summary>
        /// Gives a skin to a player
        /// </summary>
        Task PlayerGiveSkin(PlayerId playerId, ulong element_type, string skin_name);

        /// <summary>
        /// Take a skin to a player
        /// </summary>
        Task PlayerTakeSkin(PlayerId playerId, ulong element_type, string skin_name);

        Task<StorageSlot> ContainerGiveAsync(long containerId, ItemAndQuantity items);
        Task<StorageInfo> ContainerGetAsync(long containerId);

        Task<CommunityReward> GetCommunityRewards(ulong communityId);
        Task ConsumeCommunityRewards(ulong communityId, CommunityReward reward);

        Task<VoxelInternalEditResults> EditConstruct(VoxelEdit edit, ConstructId constructId, string voxelEditKind);
        /// <summary>
        /// destroy = true => won't go in inventory
        /// destroy = false => will go in inventory
        /// 
        /// BE CAREFUL, IF YOU CALL WITH destroy = false, YOU HAVE TO DO THE INVENTORY TRANSACTION YOURSELF
        /// </summary>
        Task ElementDelete(PlayerId playerId, ElementInConstruct element, bool destroy = true);

        Task<List<Mission>> MissionListAll(ulong limit, ulong offset);
        Task MissionDelete(ulong missionId);

        Task<List<FormalMission>> FormalMissionListAll(ulong limit, ulong offset);
        Task FormalMissionDelete(ulong missionId);
        Task<string> FormalMissionFixtureDiff(string fixture);
        Task FormalMissionFixtureApply(string fixture);

        Task PlayerDelete(PlayerId playerId);

        Task PlayerInventoryGrainManualTrigger(ulong playerId, PlayerPositionUpdate pupdate);
        Task RepairUnitFinishRepair(ElementId repairUnit);
        Task ClearSPSCache(PlayerId playerId);
        Task ClearSPSDirectoryCache();

        Task<(List<List<Vec3>>, List<Vec3>)> AsteroidRandomize(List<int> counts);
        Task<ulong> AsteroidSpawn(ulong planetId, int tier, string model, Vec3 position);
        Task AsteroidReconcile();
        Task AsteroidDespawn(ulong asteroidConstructId);
        Task AsteroidDiscover(ulong asteroidConstructId);
        Task AsteroidPublish(ulong asteroidConstructId);
        Task<AsteroidList> AsteroidList();
        Task<bool> StackIsReady();
        Task DebugPvpResetTimer(ConstructId construct);
        Task RefreshPvpTimer(ConstructId construct);
        Task AsteroidDespawnAll();
        Task PlayerSendSettings(PlayerId plaer, DeltaSettings settings);
        Task<ItemLocation> LocateItem(ItemInfo item);
        Task<ShieldHitResult> DebugShieldTakeHit(ConstructId construct, WeaponShotPower power);
        Task DebugRefreshPvpTimer(ConstructId construct);
        Task DebugResetLockdown(ConstructId construct);
        Task DebugSetLockdown(ConstructId construct, DateTimeOffset lockdownEnd);
        Task DebugContainerDump(ElementId containerId);
        Task<string> GetOrePool(ulong planet, ulong tile, ulong ore);

        Task IndustryReminderTick(ElementId industry);
        Task IndustrySetRunEnd(ElementId industry, DateTimeOffset end);
    }
}
