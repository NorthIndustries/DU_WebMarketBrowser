using Orleans;

namespace NQ.Interfaces
{
    public interface IPlayerDirectoryGrain : IGrainWithIntegerKey
    {
        Task<PlayerDescList> FindPlayers(PlayerName nameLike);
        Task<PlayerInfo> GetPlayerInfo(PlayerName name);
        Task<List<PlayerId>> FilterConnectedPlayers(List<PlayerId> players);
        Task SendPopup(PlayerId senderId, PopupMessage message);
        Task Disconnect(PlayerId playerId, DisconnectionNotification reason);
        Task DisconnectAll(DisconnectionNotification reason);
        Task SetPlayerConnected(PlayerId player);
        Task SetPlayerDisconnected(PlayerId player);
    }
}
