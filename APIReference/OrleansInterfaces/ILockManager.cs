namespace NQ.Interfaces
{
    public interface ILockManagerGrain : Orleans.IGrainWithStringKey
    {
        /// Query the state of given Lock, returns locked,owner
        Task<Tuple<bool, string>> Query(string lockName);
        /// Try to acquire a list of locks for owner. Returns acquired, currentOwners
        Task<Tuple<bool, List<string>>> Acquire(List<string> locks, string owner);
        /// Release given list of locks
        Task Release(List<string> locks);
    }
}
