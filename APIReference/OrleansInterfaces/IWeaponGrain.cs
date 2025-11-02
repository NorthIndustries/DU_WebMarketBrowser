using Orleans;

namespace NQ.Interfaces
{
    public interface IShootInterface
    {
        Task AttackStart(PlayerId playerId, AttackOrderTarget target);
        Task AttackStop(PlayerId playerId, AttackOrderTarget target);
        Task WeaponReload(PlayerId playerId, WeaponLoad weaponLoad);
        Task<WeaponFireResult> WeaponFireOnce(PlayerId playerId, WeaponFire weaponFire);
    }
    public interface IWeaponGrain : IShootInterface, IGrainWithIntegerKey
    {
    }
    public interface IFakeShotGrain : IShootInterface, IGrainWithIntegerKey
    {
    }
}
