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
        private readonly HashSet<string> _validatedStreamIds = new HashSet<string>();



        public VirtualTopicValidatorActor(IDriver neo4jDriver, IVirtualTopicDefinitionProvider topicProvider)
        {
            _neo4jDriver = neo4jDriver;
            _topicProvider = topicProvider;

            ReceiveAsync<ValidateDatastreamMessage>(HandleValidation);
            ReceiveAsync<ForceFullDatastreamRescan>(_ => HandleFullRescan());


        }


        private async Task HandleValidation(ValidateDatastreamMessage msg)
        {
            // Skip if already validated
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

            // Add to cache after processing
            _validatedStreamIds.Add(msg.StreamId);
        }

        private async Task ApplyVirtualTopicRelationship(string streamId, string topicName)
        {
            if (string.IsNullOrWhiteSpace(topicName))
                return;

            await using var session = _neo4jDriver.AsyncSession();
            var cypher = @"
        MATCH (d:Datastream {id: $streamId})
        MATCH (v:VirtualTopic {name: $topicName})
        MERGE (d)-[:PUBLISHED_AS]->(v)
    ";
            await session.RunAsync(cypher, new { streamId, topicName });
        }

        private async Task<List<string>> LoadDatastreamAndRoomLabelsAsync(string streamId)
        {
            await using var session = _neo4jDriver.AsyncSession();
            var cursor = await session.RunAsync(@"
        MATCH (d:Datastream {id: $streamId})
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
        private async Task HandleFullRescan()
        {
            Console.WriteLine("[Admin] Starting full datastream rescan...");

            await using var session = _neo4jDriver.AsyncSession();
            var cursor = await session.RunAsync("MATCH (d:Datastream) RETURN d.id AS streamId");

            while (await cursor.FetchAsync())
            {
                var streamId = cursor.Current["streamId"].As<string>();
                Console.WriteLine($"[Admin] Rescanning datastream: {streamId}");
                // Bypass cache and force validation
                await HandleValidation(new ValidateDatastreamMessage(streamId, new List<string>()));
            }

            Console.WriteLine("[Admin] Full datastream rescan complete.");
        }

    }
}
