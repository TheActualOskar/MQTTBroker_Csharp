using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using MqttBroker.Actors;
using MqttBroker.Messages;
using MqttBroker.Helpers;
using Neo4j.Driver;
using System.Linq;
using System;

namespace MqttBroker.Tests
{
    public class VirtualTopicValidatorActorTests : TestKit
    {
        [Fact]
        public async Task Should_Apply_Label_Once_Even_If_Validated_Twice()
        {
            var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "12345678"));

            await ClearDatabase(driver);
            await CreateBuildingRoomDatastreamGraph(driver, "BuildingA", "RoomA", "sensor-123", "Temperature");
            await CreateTestVirtualTopic(driver, "RoomATemperatureSensors", new[] { "Temperature", "RoomA" });

            var topicProvider = new Neo4jVirtualTopicDefinitionProvider(driver);
            var actor = Sys.ActorOf(Props.Create(() =>
            new VirtualTopicValidatorActor(driver, topicProvider, TestActor)));


            // First validation
            actor.Tell(new ValidateDatastreamMessage("sensor-123", new List<string> { "Temperature", "RoomA" }));
            await Task.Delay(500);
            var firstCheck = await CheckIfDatastreamHasPublishedAsRelationship(driver, "sensor-123", "RoomATemperatureSensors");
            Assert.True(firstCheck);

            // Second validation (hit cache and skip)
            actor.Tell(new ValidateDatastreamMessage("sensor-123", new List<string> { "Temperature", "RoomA" }));
            await Task.Delay(500);
            var secondCheck = await CheckIfDatastreamHasPublishedAsRelationship(driver, "sensor-123", "RoomATemperatureSensors");
            Assert.True(secondCheck);
        }

        private async Task ClearDatabase(IDriver driver)
        {
            await using var session = driver.AsyncSession();
            await session.RunAsync("MATCH (n) DETACH DELETE n");
        }

        private async Task CreateBuildingRoomDatastreamGraph(IDriver driver, string buildingName, string roomName, string streamId, string datastreamLabel)
        {
            await using var session = driver.AsyncSession();
            var cypher = $@"
                CREATE (b:Building {{ name: $buildingName }})
                CREATE (r:Room {{ name: $roomName }})
                CREATE (d:Datastream:{datastreamLabel}:`{roomName}` {{
                    streamId: $streamId,
                    type: $datastreamLabel
                }})
                CREATE (b)-[:HAS_ROOM]->(r)
                CREATE (r)-[:HAS_DATASTREAM]->(d)
            ";
            await session.RunAsync(cypher, new { buildingName, roomName, streamId, datastreamLabel });

            // Diagnostic: Log actual labels after creation
            var checkLabelsCypher = @"
                MATCH (d:Datastream {streamId: $streamId})
                RETURN labels(d) AS labels
            ";
            var cursor = await session.RunAsync(checkLabelsCypher, new { streamId });
            if (await cursor.FetchAsync())
            {
                var labels = cursor.Current["labels"].As<List<object>>().Select(x => x.ToString());
                Console.WriteLine($"Datastream labels after creation: {string.Join(", ", labels)}");
            }
        }

        private async Task CreateTestVirtualTopic(IDriver driver, string name, IEnumerable<string> expectedLabels)
        {
            await using var session = driver.AsyncSession();
            var cypher = @"
                CREATE (:VirtualTopic { name: $name, expectedLabels: $expectedLabels })
            ";
            await session.RunAsync(cypher, new { name, expectedLabels = expectedLabels.ToList() });
        }

        private async Task<bool> CheckIfDatastreamHasPublishedAsRelationship(IDriver driver, string streamId, string topicName)
        {
            await using var session = driver.AsyncSession();
            var cypher = @"
                MATCH (d:Datastream {streamId: $streamId})-[:PUBLISHED_AS]->(v:VirtualTopic {name: $topicName})
                RETURN count(v) > 0 AS hasRelationship
            ";
            var cursor = await session.RunAsync(cypher, new { streamId, topicName });
            if (await cursor.FetchAsync())
            {
                return cursor.Current["hasRelationship"].As<bool>();
            }
            return false;
        }
    }
}
