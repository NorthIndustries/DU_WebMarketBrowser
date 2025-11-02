// TODO: use a specific namespace
// bad practice to have the same namespace on 2 different assemblies
namespace Backend
{
    // TODO: have a proper IHostedService to init async IPub
    public static class IPubExtensions
    {
        public static Task NotifyPlayer<T>(this IPub pub, PlayerId playerId, NQutils.Messages.NQMessage<T> message, bool useJson = false, IPub.MessagePriority priority = IPub.MessagePriority.Low)
        {
            return pub.NotifyTopic<T>(Topics.PlayerNotifications(playerId), message, useJson, priority);
        }

        public static Task NotifyPlayer(this IPub pub, PlayerId playerId, NQ.Visibility.NQPacketWrapper packet, IPub.MessagePriority priority = IPub.MessagePriority.Low)
        {
            return pub.NotifyTopic(Topics.PlayerNotifications(playerId), packet, priority);
        }

        public static Task NotifyTopic<T>(this IPub pub, PubSubTopic topic, NQutils.Messages.NQMessage<T> message, bool useJSON = false, IPub.MessagePriority priority = IPub.MessagePriority.Low)
        {
            Serialization.Format format = useJSON ? Serialization.Format.JSON : Serialization.Format.Binary;
            return pub.NotifyTopicBinary(topic, NQutils.Serialization.Grpc.MakePacket(message, format: format), priority);
        }

        public static Task NotifyTopic(this IPub pub, PubSubTopic topic, AbstractPacket packet, IPub.MessagePriority priority = IPub.MessagePriority.Low)
        {
            return pub.NotifyTopicBinary(topic, packet, priority);
        }

    }

    public interface IPub
    {
        /// <summary>
        /// This makes usage of rabbit priority queues (requires `x-max-priority` set)
        /// when messages are in queue, highest priority not acked message is sent first to consumer
        /// in queue message are still ordered in fifo order, priority is just used at read time (important for limited queues)
        /// warning, if qos is not set, as messages are read as fast as possible even if not acked, priority doesn't change anything
        /// more info in https://www.rabbitmq.com/priority.html
        /// </summary>
        enum MessagePriority
        {
            Low = 0,
            Normal = 1,
            High = 2,
            Highest = 3,
        };
        /// <summary>
        /// Post a serialized payload to the publication system.
        /// Business code should not deal with that method directly.
        /// There are static extension to post a business object and handle the serialization.
        /// </summary>
        /// <param name="topic"></param> identifies the bus to publish to.
        /// <param name="payload"></param> is the binary payload.
        /// <param name="type"></param> identifies the type of payload.
        /// <param name="format"></param> is the serialization format of the binary payload.
        /// <returns></returns>
        Task NotifyTopicBinary(PubSubTopic topic, AbstractPacket packet, IPub.MessagePriority priority = IPub.MessagePriority.Low);


        // TODO: move that to a more generic class... the point is we keep existing exchange here
        Task BindPlayerToTopic(PlayerId player, PubSubTopic topic);
        Task UnbindPlayerFromTopic(PlayerId player, PubSubTopic topic);
    }
}

