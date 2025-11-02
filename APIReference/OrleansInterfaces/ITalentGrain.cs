using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public interface ITalentGrain : Orleans.IGrainWithStringKey
    {
        Task<PlayerTalentState> Status();
        Task<TalentAndLevelPoints> Purchase(TalentAndLevel talentAndLevel);
        Task<TalentAndLevelPoints> Queue(TalentQueue newQueue);
        /// Suspend account, it will no longer accumulate talent points
        Task Suspend();
        /// Resume account at current date
        Task Resume();
        /// Add given value in seconds to current accumulation start date
        Task AddTime(long seconds);
        Task SetStartTime(ulong secondsSinceEpoch, bool onlyAtCreation);
        /// Reset talents, queue, partials, pointsSpent to 0. Full refund for respec.
        Task Respecialize();
        Task Reset();
        /// Remove given talent from queue, talents, partials, and refund points invested.
        Task<TalentRefund> RemoveTalent(ulong talent, List<long> costs = null);
        Task DeactivateGrain();
        /// Set number of available bank points.
        Task SetAvailable(long points);
        /// Add to available bank points
        Task AddAvailable(long points);
        /// Give all talents at max level.
        Task GiveAll();
        /// Give a talent at specific level
        Task Give(ulong talent, int level);
        Task UpgradeElement(ulong elementId);
        [AlwaysInterleave]
        Task UpgradeConstruct(ulong constructId);
        /// Switch effect system on or off
        Task SwitchEffectSystem(bool enabled, bool useElementBypass);
        Task<bool> IsEnabled();
        Task PlayerConnected();
        Task ForceAdvance();
        /// Set value and notify client. Use for untrainable talents only
        Task SetLevel(ulong talent, int level);

        /// Try try to use/deploy/craft itemId. Returns modifiers to apply or throws if requirements are not met
        Task<GameplayModifiers> Use(ulong itemId);
        /// Try to use given deployed element type
        Task UseElement(ulong itemId);
        /// Return modifiers granted by talents for this item without checking requirements
        [AlwaysInterleave]
        Task<GameplayModifiers> Bonuses(ulong itemId, bool forceEnable = false);
        [AlwaysInterleave]
        Task<List<TalentEffect>> BonusesEx(ulong itemId, bool forceEnable = false);
        /// Return max possible modifiers granted by maxed talents
        Task<GameplayModifiers> MaxBonuses(ulong itemId);
        Task<List<TalentEffect>> MaxBonusesEx(ulong itemId);
        Task<GameplayModifiers> PlayerProperty(string prop, bool forceEnable = false);
    }
}
