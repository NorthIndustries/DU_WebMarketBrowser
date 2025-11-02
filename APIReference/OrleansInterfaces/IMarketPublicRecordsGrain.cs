namespace NQ.Interfaces
{
    public interface IMarketPublicRecordsGrain : Orleans.IGrainWithGuidKey
    {
        Task Start();
        Task Stop();
        Task ResetState();
        Task ProcessHourly();
        Task ProcessDaily();
        Task TransactionBacklog(DateTime start, DateTime end);
    }
}
