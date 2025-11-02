namespace NQ.Interfaces
{
    public interface IBarterGrain : Orleans.IGrainWithStringKey
    {
        /// Try to initialize barter from p1 to p2
        Task<bool> Initialize(ulong p1, ulong p2);
        /// Cancel barter session
        Task Cancel(bool p1IsRequester);
        /// Update session state. Returns false in case of peer state conflict
        Task<bool> Update(bool fromp1, BarterSessionState state);
    }
}
