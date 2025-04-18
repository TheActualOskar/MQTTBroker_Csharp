namespace MqttBroker.Messages
{
    public class PublishMessage
    {
        public string Topic { get; }
        public byte[] Payload { get; }

        public PublishMessage(string topic, byte[] payload)
        {
            Topic = topic;
            Payload = payload;
        }
    }
}
