using Orleans;

namespace NQ.Interfaces
{
    public interface IChatGrain : IGrainWithIntegerKey
    {
        Task OnLogout(PlayerId id);
        Task<ChatJoinInfo> ChannelJoin(MessageChannel channelInfo);
        Task ChannelLeave(MessageChannel channelInfo);
        Task SendMessage(MessageContent message);
    }
}
