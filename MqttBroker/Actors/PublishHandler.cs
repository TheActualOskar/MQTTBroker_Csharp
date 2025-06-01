using Akka.Actor;
using MqttBroker.Messages;
using Neo4j.Driver;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MqttBroker.Actors
{
    public class PublishHandler : ReceiveActor
    {
        private readonly IActorRef _messageRouter;
        private readonly IActorRef _webSocketServer;
        private readonly IDriver _neo4jDriver;
        private readonly IActorRef _virtualTopicValidator;

        public PublishHandler(IActorRef messageRouter, IActorRef webSocketServer, IDriver neo4jDriver, IActorRef virtualTopicValidator)
        {
            _messageRouter = messageRouter;
            _webSocketServer = webSocketServer;
            _neo4jDriver = neo4jDriver;

            Receive<MqttRawPacket>(HandlePublish);
            Receive<ResolvedVirtualTopics>(HandleResolvedTopics);
            _virtualTopicValidator = virtualTopicValidator;
        }

        private void HandlePublish(MqttRawPacket packet)
        {
            var (topic, payload) = ParsePublishPacket(packet.RawBytes);
            var payloadText = Encoding.UTF8.GetString(payload);

            Console.WriteLine($"Received publish for topic: '{topic}'");

            _messageRouter.Tell(new PublishMessage(topic, payload));
            _webSocketServer.Tell(new PublishToWebSocket(topic, payloadText));

            ResolveVirtualTopicsAsync(topic)
                .PipeTo(Self, success: topics => new ResolvedVirtualTopics(payload, topics, payloadText));
        }

        private void HandleResolvedTopics(ResolvedVirtualTopics result)
        {
            foreach (var vTopic in result.VirtualTopics)
            {
                Console.WriteLine($"Routing to virtual topic: {vTopic}");
                _messageRouter.Tell(new PublishMessage(vTopic, result.Payload));
                _webSocketServer.Tell(new PublishToWebSocket(vTopic, result.PayloadText));
            }
        }

        private (string topic, byte[] payload) ParsePublishPacket(byte[] raw)
        {
            int topicLength = (raw[2] << 8) + raw[3];
            string topic = Encoding.UTF8.GetString(raw, 4, topicLength);
            byte[] payload = raw[(4 + topicLength)..];
            return (topic, payload);
        }

        private async Task<List<string>> ResolveVirtualTopicsAsync(string topicOrStreamId)
        {
            var virtualTopics = new List<string>();

            await using var session = _neo4jDriver.AsyncSession();
            var cursor = await session.RunAsync(@"
                MATCH (s:Stream {id: $streamId})-[:PUBLISHED_AS]->(v:VirtualTopic)
                RETURN v.name AS topic
            ", new { streamId = topicOrStreamId });

            while (await cursor.FetchAsync())
            {
                virtualTopics.Add(cursor.Current["topic"].As<string>());
            }

            return virtualTopics;
        }
    }
}
