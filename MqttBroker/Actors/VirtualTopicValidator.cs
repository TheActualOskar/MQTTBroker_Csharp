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

        public VirtualTopicValidatorActor(IDriver neo4jDriver, IVirtualTopicDefinitionProvider topicProvider)
        {
            _neo4jDriver = neo4jDriver;
            _topicProvider = topicProvider;

            ReceiveAsync<ValidateDatastreamMessage>(HandleValidation);
        }

        private async Task HandleValidation(ValidateDatastreamMessage msg)
        {
            var datastreamLabels = await LoadDatastreamAndRoomLabelsAsync(msg.StreamId);



            var topics = await _topicProvider.GetActiveVirtualTopicsAsync();

            foreach (var topic in topics)
            {
                if (topic.ExpectedLabels.All(datastreamLabels.Contains))
                {
                    await ApplyVirtualTopicRelationship(msg.StreamId, topic.Name);

                }
            }
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
    }
}
