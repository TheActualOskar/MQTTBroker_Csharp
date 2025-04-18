using Akka.Actor;
using MqttBroker.Messages;
using System;
using System.Text;

namespace MqttBroker.Actors
{
    public class PublishHandler : ReceiveActor
    {
        private readonly IActorRef _messageRouter;

        public PublishHandler()
        {
            // Normally this would be injected; for now, create it directly:
            //_messageRouter = Context.ActorOf(Props.Create(() => new MessageRouter()), "MessageRouter");

            Receive<MqttRawPacket>(packet =>
            {
                var (topic, payload) = ParsePublishPacket(packet.RawBytes);

                Console.WriteLine($"📦 Received publish for topic: '{topic}' | Payload: {Encoding.UTF8.GetString(payload)}");

                _messageRouter.Tell(new PublishMessage(topic, payload));
            });
        }
              public PublishHandler(IActorRef messageRouter)
        {
            _messageRouter = messageRouter;

            Receive<MqttRawPacket>(packet =>
            {
                var (topic, payload) = ParsePublishPacket(packet.RawBytes);
                Console.WriteLine($"📦 Received publish for topic: '{topic}' | Payload: {Encoding.UTF8.GetString(payload)}");
                _messageRouter.Tell(new PublishMessage(topic, payload));
            });
        }  

        private (string topic, byte[] payload) ParsePublishPacket(byte[] raw)
        {
            // MQTT PUBLISH packet:
            // Byte 0: Packet type & flags (already parsed)
            // Byte 1+: Remaining Length (skip for now; assume simple)
            // Next 2 bytes: Topic length (big-endian)
            // Next N bytes: Topic string
            // Remaining: Payload

            int topicLength = (raw[2] << 8) + raw[3];
            string topic = Encoding.UTF8.GetString(raw, 4, topicLength);

            int payloadStart = 4 + topicLength;
            byte[] payload = raw[payloadStart..];

            return (topic, payload);
        }
    }
}
