namespace MqttBroker.Messages
{
    public class SubscriptionMessage
    {
        public string ClientId { get; }
        public string[] Topics { get; }

        public SubscriptionMessage(string clientId, string[] topics)
        {
            ClientId = clientId;
            Topics = topics;
        }
    }
}
