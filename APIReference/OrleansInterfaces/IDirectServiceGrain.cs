using NQutils.Def;
using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces;
public interface IDirectServiceGrain : IGrainWithIntegerKey
{
    #region voxel
    Task<VoxelEditResult> VoxelEditRequest(PlayerId playerId, VoxelEdit voxelEdit);
    [OneWay]
    Task PropagateShotImpact(WeaponShot shot);
    Task<VoxelInternalEditResults> MakeVoxelDamages(WeaponFire weaponFire,
                                                                  Ammo ammoDef,
                                                                  double power,
                                                                  IEnumerable<(PlayerId, Vec3)> playerPositions);
    #endregion
    #region abuse
    Task ReportAbuse(PlayerId playerId, AbuseDescription abuse);
    #endregion
    #region wallet log
    Task<WalletOperations> WalletLog(PlayerId pid, WalletOperationsQuery query);
    Task WalletTransfer(PlayerId pid, WalletTransfer transfer);
    #endregion
    #region barter
    Task BarterStart(PlayerId owner, PlayerId peer);
    Task BarterCancel(PlayerId pid);
    Task BarterUpdate(PlayerId pid, BarterSessionState requested);
    #endregion
    #region storage
    Task StorageMove(PlayerId pid, StorageMove op);
    Task StorageSwap(PlayerId pid, StorageSwap op);
    Task StorageDrop(PlayerId pid, StorageDrop op);
    Task StorageDropProperties(PlayerId pid, StorageDropProperties op);
    Task StorageClaim(PlayerId pid, StorageClaim op);
    Task StorageStack(PlayerId pid, ElementId cid);
    Task<DataItemIds> DataItemCreate(PlayerId pid, DataItemCreate model);
    Task<ElementId> PackageCreate(PlayerId pid, PackageCreate pc);
    Task PackageExpand(PlayerId pid, PackageExpand pe);
    Task<StorageInfo> StorageContainerGet(PlayerId pid, ElementId eid);
    Task SalvageElement(PlayerId pid, StorageDrop op);

    Task SetSchematicContainer(PlayerId pid, ElementId eid, ConstructId cid);
    Task<SphereList> GetPlanetarySafeZones();
    #endregion

    #region Visibility Persistence
    Task SavePlayerLocation(PlayerPositionUpdate ppu);
    Task SaveConstructLocation(ConstructUpdate cu);
    #endregion
    Task<SentinelScore> SentinelScore();
    Task NotifyEntity(EntityId target, NotificationMessage msg);
}
