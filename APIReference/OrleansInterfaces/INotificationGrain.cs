namespace NQ.Interfaces
{
    public interface INotificationGrain : Orleans.IGrainWithIntegerKey
    {
        Task<PlayerNotificationStateList> AllNotifications();

        Task<PlayerNotificationStateList> AllUnsentNotifications();

        Task<PlayerNotificationState> AddNewNotification(NotificationMessage notification);
        Task DeleteNotification(ulong notificationId);
        Task DeleteExpiredNotifications();
        Task DeleteAllNotifications();
        Task MarkRead();
        Task AcknowledgeNotification(NotificationId id);

        Task DeactivateGrain();
    }
}
