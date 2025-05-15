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
            var datastreamLabels = await LoadDatastreamLabelsAsync(msg.StreamId);


            var topics = await _topicProvider.GetActiveVirtualTopicsAsync();

            foreach (var topic in topics)
            {
                if (topic.ExpectedLabels.All(datastreamLabels.Contains))
                {
                    await ApplyVirtualTopicLabel(msg.StreamId, topic.Name);
                }
            }
        }

        private async Task ApplyVirtualTopicLabel(string streamId, string topicName)
        {
            if (string.IsNullOrWhiteSpace(topicName))
                return;

            var label = $"VirtualTopic_{topicName.Replace(" ", "_")}";

            var cypher = $@"
                MATCH (d:Datastream {{id: $streamId}})
                SET d:`{label}`
            ";

            await using var session = _neo4jDriver.AsyncSession();
            await session.RunAsync(cypher, new { streamId });
        }
        private async Task<List<string>> LoadDatastreamLabelsAsync(string streamId)
        {
            await using var session = _neo4jDriver.AsyncSession();
            var cursor = await session.RunAsync(@"
                 MATCH (d:Datastream {id: $streamId})
                 RETURN labels(d) AS labels
              ", new { streamId });


            if (await cursor.FetchAsync())
            {
                return cursor.Current["labels"].As<List<object>>()
                             .Select(label => label.ToString())
                             .ToList();
            }

            return new List<string>();
        }
    }
}
