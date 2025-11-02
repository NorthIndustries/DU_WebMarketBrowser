using Orleans;


namespace NQ.Interfaces
{
    public interface IMiningPoolGrain : IGrainWithStringKey
    {
        Task<double> Take(ulong ore, ulong elementId, double currentRate, double maxRate);
        Task Free(ulong ore, ulong elementId);
        Task<string> State(ulong ore);
        Task<List<OrePool>> Available(List<ulong> ores);
    }
}
