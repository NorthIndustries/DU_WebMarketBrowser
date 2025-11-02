using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public class ConstructDestruction
    {
        public PlayerId? destroyer;
        public string reason;
    }
    public interface IConstructGrain : IGrainWithIntegerKey
    {
        Task OnConstructChangeParent(ParentingChange change);
        #region info
        [AlwaysInterleave]
        Task UnLoad();
        Task<ElementId> GetCoreUnitId();
        Task RenameConstruct(PlayerId playerId, string newName);

        /// <summary>
        /// Update the ConstructInfo of the given construct.
        /// At the moment, the update goes through rabbit and reach the construct visibility.
        ///
        /// </summary>
        /// <param name="update">payload used to update the construct</param>
        /// <returns></returns>
        [AlwaysInterleave]
        Task UpdateConstructInfo(NQ.ConstructInfoUpdate update);

        [AlwaysInterleave]
        Task<PlayerId?> GetPilot();
        [AlwaysInterleave]
        Task<bool> IsBeingControlled();
        [AlwaysInterleave]
        Task<bool> IsBeingRepaired();
        [AlwaysInterleave]
        Task<bool> IsKeyed();
        [AlwaysInterleave]
        Task<ConstructKind> GetConstructKind();
        Task SetConstructRepairedBy(ElementInConstruct repairer);

        [AlwaysInterleave]
        Task<TargetingConstructData> GetTargetingConstructData();
        Task<List<ConstructId>> GetParentedConstructs();
        #endregion
        #region ownership
        Task ConstructSetOwner(PlayerId playerId, ConstructOwnerSet ownerSet, bool doKeyCheck = true, ConstructDestruction? destruction = null);
        Task ConstructCreateKey(PlayerId playerId);
        Task DestroyKey();
        Task ConstructUseKey(PlayerId playerId, ConstructKeyUse constructKeyUse);

        Task ConstructDelete(PlayerId playerId, ConstructDeletion deletion);
        Task<EntityId> GetOwner();
        /// <summary>
        /// Returns null if construct has no owner
        /// </summary>
        Task<PlayerId?> GetOwnerOrLegateOwning();
        #endregion
        Task<PlayerList> GetPlayersInConstruct();
        #region destruction
        [AlwaysInterleave]
        Task OnConstructDestroyed();
        [AlwaysInterleave]
        Task OnConstructRestored();
        #endregion
        #region warping
        [AlwaysInterleave]
        Task CanWarp();
        Task WarpStart(PlayerId playerId);
        Task WarpEnd(PlayerId playerId);
        [AlwaysInterleave]
        Task<bool> IsWarping();
        #endregion
        #region piloting
        Task<ConstructUpdate> PilotingTakeOver(PlayerId playerId, bool isStrongControl);
        Task<ConstructId> PilotingControlChange(PlayerId playerId, bool isStrongControl);
        Task<ConstructId> PilotingStop(PlayerId playerId);
        Task FetchConstruct(PlayerId playerId, ConstructTeleport destination);
        Task TeleportConstruct(RelativeLocation destination);
        Task<NQBool> ForceRelaseWeakControl();
        Task ForceReleaseControl();
        Task ParentIdlenessChange(bool idle);
        Task SetResumeState(NQ.Vec3 worldRelativeAngVelocity, NQ.Vec3 worldRelativeVelocity, bool grounded);
        #endregion

        #region transponder
        Task UpdateTransponderTags(PlayerId playerId, RadarTransponderTagList tagList);
        Task UpdateTransponderTagsInternal(RadarTransponderTagList tagList);
        Task SetTransponderState(PlayerId playerId, RadarTransponderActiveState transponderState);
        Task SetTransponderStateInternal(RadarTransponderActiveState transponderState);
        Task OnRemoveTransponder(ElementId elementId);
        #endregion
        #region build
        [AlwaysInterleave]
        Task CheckBuild(PlayerId playerId);
        [AlwaysInterleave]
        Task EnterBuildMode(PlayerId playerId);
        [AlwaysInterleave]
        Task ExitBuildMode(PlayerId playerId);
        /// <summary>
        /// Check gameplay rules to know if it's possible to place the element
        /// Throws when it's not allowed
        /// </summary>
        Task CheckCanPutElement(ElementInfo elementInfo);

        #endregion
        #region mass
        [AlwaysInterleave]
        Task ConstructUpdatePlayerMass(PlayerMass mass);
        [AlwaysInterleave]
        Task ConstructRemovePlayerMass(PlayerId player);
        [AlwaysInterleave]
        Task<ConstructPlayerMassList> ConstructGetPlayersMass();
        [AlwaysInterleave]
        Task AddElementMass(ElementInfo element);
        [AlwaysInterleave]
        Task RemoveElementMass(ElementInfo element);
        [AlwaysInterleave]
        Task UpdateContainerMass(ElementId element, double mass);
        [AlwaysInterleave]
        Task<double> GetTotalMass();
        [AlwaysInterleave]
        Task<double> GetConstructMass();
        [AlwaysInterleave]
        Task ConstructRemoveChildMass(ConstructId constructId);
        [AlwaysInterleave]
        Task ConstructUpdateChildMass(ConstructId constructId, double mass);
        [AlwaysInterleave]
        Task AddVoxelMass(double deltaMass);
        Task PlayerEnterSurrogate(PlayerId playerId);
        [AlwaysInterleave]
        Task PlayerLeaveSurrogate(PlayerId playerId);
        Task MaintenanceInitializeMassProperties();
        #endregion
        #region BuildEvents
        [AlwaysInterleave]
        Task ElementPickupPre(PlayerId playerId, ElementInfo elementDeleted);
        [AlwaysInterleave]
        Task ElementPickupPost(PlayerId playerId, ElementInfo elementDeleted);

        Task ValidateElementLocation(RelativeLocation location);
        Task OnElementAdded(PlayerId playerId, ElementInfo elementAdded);
        Task OnElementRestored(ElementInfo elementRestored);
        #endregion
        #region pvp
        [AlwaysInterleave]
        Task<ConstructGeometry> GetConstructGeometry();
        [AlwaysInterleave]
        Task<(Vec3 velocity, Vec3 angVelocity)> GetConstructVelocity();
        [AlwaysInterleave]
        Task<bool> IsInSafeZone();
        Task<List<(PlayerId, Vec3)>> GetKillablePlayerListAndPosition();
        #endregion

        #region identification
        Task ConstructStartIdentifying(TargetingConstructData info, ElementId radar);
        Task ConstructEndIdentifying(ConstructId cid, ElementId radar);
        Task ConstructStartAttacking(TargetingConstructData info, ElementId weapon);
        Task ConstructEndAttacking(ConstructId cid, ElementId weapon);
        [AlwaysInterleave]
        Task<TargetingAlertInfoList> GetTargetingAlertInfos();
        Task CheckIdentificationStatus();
        #endregion

    }
}
