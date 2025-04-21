using Akka.Actor;
using MqttBroker.Messages;
using System;
using System.Text;

namespace MqttBroker.Actors
{
    public class PublishHandler : ReceiveActor
    {
        private readonly IActorRef _messageRouter;
        private readonly IActorRef _webSocketServer;

        public PublishHandler(IActorRef messageRouter, IActorRef webSocketServer)
        {
            _messageRouter = messageRouter;
            _webSocketServer = webSocketServer;

            Receive<MqttRawPacket>(packet =>
            {
                var (topic, payload) = ParsePublishPacket(packet.RawBytes);
                var payloadText = Encoding.UTF8.GetString(payload);

                Console.WriteLine($"📦 Received publish for topic: '{topic}' | Payload: {payloadText}");

                // Push to message router (TCP clients)
                _messageRouter.Tell(new PublishMessage(topic, payload));

                // Push to WebSocket clients (browser)
                _webSocketServer.Tell(new PublishToWebSocket(topic, payloadText));
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
