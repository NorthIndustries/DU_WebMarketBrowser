using Orleans;


namespace NQ.Interfaces
{
    public interface IPlayerUsageGrain : IGrainWithIntegerKey
    {
        Task ElementUseStart(ElementUse usage);
        Task ElementUseStop(ElementUse usage);

    }
}
