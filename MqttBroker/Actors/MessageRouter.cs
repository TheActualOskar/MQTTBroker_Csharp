using Akka.Actor;
using MqttBroker.Messages;
using MqttBroker.Helpers;
using System;
using System.Net.Sockets;

namespace MqttBroker.Actors
{
    public class MessageRouter : ReceiveActor
    {
        public MessageRouter()
        {
            Receive<PublishMessage>(msg =>
            {
                var subscribers = SubscriptionManager.GetSubscribers(msg.Topic);

                foreach (var stream in subscribers)
                {
                    try
                    {
                        var packet = BuildPublishPacket(msg.Topic, msg.Payload);
                        stream.Write(packet, 0, packet.Length);
                        stream.Flush();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error sending to subscriber: {ex.Message}");
                    }
                }
            });
        }

        private byte[] BuildPublishPacket(string topic, byte[] payload)
        {
            byte[] topicBytes = System.Text.Encoding.UTF8.GetBytes(topic);
            byte[] topicLength = { (byte)(topicBytes.Length >> 8), (byte)(topicBytes.Length & 0xFF) };
            byte[] fixedHeader = { 0x30, (byte)(2 + topicBytes.Length + payload.Length) };

            return fixedHeader
                .Concat(topicLength)
                .Concat(topicBytes)
                .Concat(payload)
                .ToArray();
        }
    }
}
