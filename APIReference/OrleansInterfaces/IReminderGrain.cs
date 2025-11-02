using Orleans;
using Orleans.Concurrency;

namespace NQ.Interfaces
{
    public interface IReminderGrain : IGrainWithIntegerKey
    {
        Task Start();
        Task Stop();
        [AlwaysInterleave]
        Task CreateOrUpdateReminder(string grainInterface, string grainKey, string reminderName, TimeSpan dueTime, TimeSpan interval);
        [AlwaysInterleave]
        Task DeleteReminder(string grainInterface, string grainKey, string reminderName);
    }
}
