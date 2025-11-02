namespace Backend
{
    public interface ISubObserver
    {
        string GetObserverKey();

        Task OnSubscriptionMessageReceived(PubSubTopic topic, AbstractPacket message);
    }

    public interface ISub
    {
        Task InitializeAsync(string broker);
        Task<bool> IsSubscribed(PubSubTopic topic, ISubObserver observer);
        Task Subscribe(PubSubTopic topic, ISubObserver observer);
        Task Unsubscribe(PubSubTopic topic, ISubObserver observer);
    };
}

