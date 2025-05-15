using Neo4j.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MqttBroker.Messages;

namespace MqttBroker.Helpers
{
    public class Neo4jVirtualTopicDefinitionProvider : IVirtualTopicDefinitionProvider
    {
        private readonly IDriver _neo4jDriver;

        public Neo4jVirtualTopicDefinitionProvider(IDriver neo4jDriver)
        {
            _neo4jDriver = neo4jDriver;
        }

        public async Task<List<VirtualTopicDefinition>> GetActiveVirtualTopicsAsync()
        {
            var result = new List<VirtualTopicDefinition>();

            await using var session = _neo4jDriver.AsyncSession();
            var cursor = await session.RunAsync(@"
                MATCH (v:VirtualTopic)
                RETURN v.name AS name, v.expectedLabels AS labels
            ");

            while (await cursor.FetchAsync())
            {
                var name = cursor.Current["name"].As<string>();
                var labels = cursor.Current["labels"].As<List<object>>()
                                .Select(l => l.ToString())
                                .ToList();

                result.Add(new VirtualTopicDefinition
                {
                    Name = name,
                    ExpectedLabels = labels
                });
            }

            return result;
        }
    }
}
