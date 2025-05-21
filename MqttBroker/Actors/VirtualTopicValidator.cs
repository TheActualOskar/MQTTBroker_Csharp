using Akka.Actor;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MqttBroker.Helpers;
using MqttBroker.Messages;

namespace MqttBroker.Actors
{
    public class VirtualTopicValidatorActor : ReceiveActor
    {
        private readonly IDriver _neo4jDriver;
        private readonly IVirtualTopicDefinitionProvider _topicProvider;
        private readonly HashSet<string> _validatedStreamIds = new();

        public VirtualTopicValidatorActor(IDriver neo4jDriver, IVirtualTopicDefinitionProvider topicProvider)
        {
            _neo4jDriver = neo4jDriver;
            _topicProvider = topicProvider;

            ReceiveAsync<ValidateDatastreamMessage>(HandleValidation);
            ReceiveAsync<ForceFullDatastreamRescan>(_ => HandleFullRescan());
            ReceiveAsync<CreateDatastreamMessage>(HandleDatastreamCreation);
        }

        private async Task HandleDatastreamCreation(CreateDatastreamMessage msg)
        {
            Console.WriteLine($"[Broker] Creating datastream: {msg.StreamId}");

            var validSensorTypes = new[] { "Temperature", "Humidity" };
            if (!validSensorTypes.Contains(msg.SensorType))
            {
                Console.WriteLine($"[Broker] Invalid sensor type: {msg.SensorType}");
                return;
            }

            var cypher = $@"
                MATCH (b:Building {{name: $building}})-[:HAS_ROOM]->(r:Room {{name: $room}})
                CREATE (s:Datastream:{msg.SensorType})
                SET s.streamId = $streamId,
                    s.type = $sensorType
                MERGE (r)-[:HAS_DATASTREAM]->(s)
            ";

            await using var session = _neo4jDriver.AsyncSession();
            await session.RunAsync(cypher, new
            {
                streamId = msg.StreamId,
                building = msg.Building,
                room = msg.Room,
                sensorType = msg.SensorType
            });

            // Immediately validate it
            Self.Tell(new ValidateDatastreamMessage(
                msg.StreamId,
                new List<string> { msg.SensorType, msg.Room }));
        }

        private async Task HandleValidation(ValidateDatastreamMessage msg)
        {
            if (_validatedStreamIds.Contains(msg.StreamId))
            {
                Console.WriteLine($"[Cache] Stream {msg.StreamId} already validated. Skipping.");
                return;
            }

            Console.WriteLine($"[Validator] Validating stream: {msg.StreamId}");

            var datastreamLabels = await LoadDatastreamAndRoomLabelsAsync(msg.StreamId);
            Console.WriteLine($"[Validator] Loaded labels: {string.Join(", ", datastreamLabels)}");

            var topics = await _topicProvider.GetActiveVirtualTopicsAsync();
            Console.WriteLine($"[Validator] Found {topics.Count} virtual topics");

            foreach (var topic in topics)
            {
                Console.WriteLine($"[Validator] Evaluating topic: {topic.Name} with expected labels: {string.Join(", ", topic.ExpectedLabels)}");

                if (topic.ExpectedLabels.All(datastreamLabels.Contains))
                {
                    Console.WriteLine($"[Validator] Match found. Linking {msg.StreamId} to {topic.Name}");
                    await ApplyVirtualTopicRelationship(msg.StreamId, topic.Name);
                }
                else
                {
                    Console.WriteLine($"[Validator] No match for topic {topic.Name}");
                }
            }

            _validatedStreamIds.Add(msg.StreamId);
        }

        private async Task<List<string>> LoadDatastreamAndRoomLabelsAsync(string streamId)
        {
            await using var session = _neo4jDriver.AsyncSession();
            var cursor = await session.RunAsync(@"
                MATCH (d:Datastream {streamId: $streamId})
                OPTIONAL MATCH (r:Room)-[:HAS_DATASTREAM]->(d)
                RETURN labels(d) + r.name AS combinedLabels
            ", new { streamId });

            if (await cursor.FetchAsync())
            {
                return cursor.Current["combinedLabels"].As<List<object>>()
                    .Select(label => label.ToString())
                    .ToList();
            }

            return new List<string>();
        }

        private async Task ApplyVirtualTopicRelationship(string streamId, string topicName)
        {
            if (string.IsNullOrWhiteSpace(topicName))
                return;

            await using var session = _neo4jDriver.AsyncSession();
            await session.RunAsync(@"
                MATCH (d:Datastream {streamId: $streamId})
                MATCH (v:VirtualTopic {name: $topicName})
                MERGE (d)-[:PUBLISHED_AS]->(v)
            ", new { streamId, topicName });
        }

        private async Task HandleFullRescan()
        {
            Console.WriteLine("[Admin] Starting full datastream rescan...");

            await using var session = _neo4jDriver.AsyncSession();
            var cursor = await session.RunAsync("MATCH (d:Datastream) RETURN d.streamId AS streamId");

            while (await cursor.FetchAsync())
            {
                var streamId = cursor.Current["streamId"].As<string>();
                Console.WriteLine($"[Admin] Rescanning datastream: {streamId}");

                await HandleValidation(new ValidateDatastreamMessage(streamId, new List<string>()));
            }

            Console.WriteLine("[Admin] Full datastream rescan complete.");
        }
    }
}
