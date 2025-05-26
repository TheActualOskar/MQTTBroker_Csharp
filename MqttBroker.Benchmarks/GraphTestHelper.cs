using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MqttBroker.Tests
{
    public class GraphTestHelper
    {
        private readonly IDriver _driver;

        public GraphTestHelper(IDriver driver)
        {
            _driver = driver;
        }

        public async Task ClearDatabase()
        {
            await using var session = _driver.AsyncSession();
            await session.RunAsync("MATCH (n) DETACH DELETE n");
        }

        public async Task CreateBuildingRoomDatastreamGraph(string building, string room, string streamId, string label)
        {
            var cypher = @"
                CREATE (b:Building { name: $building })
                CREATE (r:Room { name: $room })
                CREATE (d:Datastream:" + label + ":`" + room + @"` {
                    streamId: $streamId, type: $label })
                CREATE (b)-[:HAS_ROOM]->(r)
                CREATE (r)-[:HAS_DATASTREAM]->(d)
            ";

            await using var session = _driver.AsyncSession();
            await session.RunAsync(cypher, new { building, room, streamId, label });
        }

        public async Task CreateTestVirtualTopic(string name, IEnumerable<string> expectedLabels)
        {
            var cypher = @"
                CREATE (:VirtualTopic { name: $name, expectedLabels: $expectedLabels })
            ";

            await using var session = _driver.AsyncSession();
            await session.RunAsync(cypher, new { name, expectedLabels = expectedLabels.ToList() });
        }

        public async Task<bool> CheckIfDatastreamHasPublishedAsRelationship(string streamId, string topic)
        {
            var cypher = @"
                MATCH (d:Datastream {streamId: $streamId})-[:PUBLISHED_AS]->(v:VirtualTopic {name: $topic})
                RETURN count(v) > 0 AS hasRelationship
            ";

            await using var session = _driver.AsyncSession();
            var cursor = await session.RunAsync(cypher, new { streamId, topic });
            return await cursor.FetchAsync() && cursor.Current["hasRelationship"].As<bool>();
        }

        public async Task<int> CountPublishedAsRelationships(string streamId, string topic)
        {
            var cypher = @"
                MATCH (d:Datastream {streamId: $streamId})-[:PUBLISHED_AS]->(v:VirtualTopic {name: $topic})
                RETURN count(v) AS count
            ";

            await using var session = _driver.AsyncSession();
            var cursor = await session.RunAsync(cypher, new { streamId, topic });
            return await cursor.FetchAsync() ? cursor.Current["count"].As<int>() : 0;
        }

        public async Task<bool> CheckNodeExists(string label, string streamId)
        {
            var cypher = $"MATCH (n:{label} {{ streamId: $streamId }}) RETURN count(n) > 0 AS exists";

            await using var session = _driver.AsyncSession();
            var cursor = await session.RunAsync(cypher, new { streamId });
            return await cursor.FetchAsync() && cursor.Current["exists"].As<bool>();
        }
    }
}
