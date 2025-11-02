using Orleans;

namespace NQ.Interfaces
{
    public interface IConstructFightGrain : IGrainWithIntegerKey
    {

        /// <summary>
        /// Called by C++ for gameplay checks
        /// </summary>
        Task<TimePoint> GetPvpTimerEnd();
        Task<bool> IsInPvp();

        /// <summary>
        /// Is called by c++ when the pvp timer needs to be refreshed
        /// </summary>
        Task RefreshPvpTimer();
        Task RefreshPvpTimer(System.TimeSpan duration);

        /// <summary>
        /// Called by the PSA when a hit should hit Construct
        /// </summary>
        /// <returns>If the shield absorbed the hit</returns>
        Task<ShieldHitResult> ConstructTakeHit(WeaponShotPower power);

        /// <summary>
        /// Called by the integration tests
        /// </summary>
        Task PvpTimerReset();

        // TEMPORARY : schedule stress hp recalculation
        Task TempScheduleStressHPRecalculation();

        #region Shield

        /// <summary>
        /// Called by the PSA when a shield element is added
        /// </summary>
        Task ShieldElementAdded(ElementId shield, PlayerId player);

        /// <summary>
        /// Called by the PSA when a shield element is removed
        /// </summary>
        Task ShieldElementRemoved(ElementId shield);


        /// <summary>
        /// Called by other grains when talent are re-applied
        /// </summary>
        Task ShieldPropertyUpdated(List<ElementPropertyUpdate> update);

        /// <summary>
        /// Called by a player
        /// </summary>
        Task ShieldToggle(PlayerId caller, PvpShieldToggleRequest changeState);

        Task SetResistances(PlayerId caller, PvpShieldResistance resistance);
        Task StartVenting(PlayerId caller);
        Task StopVenting(PlayerId caller);

        /// <summary>
        /// Called by C++
        /// </summary>
        Task OnShieldRestored();
        /// <summary>
        /// Called by C++
        /// </summary>
        Task OnShieldDestroyed();

        /// <summary>
        /// Called by C++
        /// </summary>
        Task OnBaseShieldRestored();
        /// <summary>
        /// Called by C++
        /// </summary>
        Task OnBaseShieldDestroyed();

        #endregion Shield

        #region BaseShield

        /// <summary>
        /// Called by the PSA when a baseShield element is added
        /// </summary>
        Task BaseShieldElementAdded(ElementId shield, PlayerId player);
        Task BaseShieldElementRemoved(ElementId shield);

        Task<NQBool> IsInLockdown();

        Task DebugEndLockdown();
        Task DebugSetEndLockdown(DateTimeOffset lockdownEnd);

        Task SetLockdownExitTime(PlayerId caller, ulong minutesFromMidnightUtc);

        #endregion BaseShield

        Task UnLoad(bool isDeletion);
    }
}
