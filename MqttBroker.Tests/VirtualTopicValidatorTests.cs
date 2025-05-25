using Akka.Actor;
using Akka.TestKit.Xunit2;
using MqttBroker.Actors;
using MqttBroker.Helpers;
using MqttBroker.Messages;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MqttBroker.Tests
{
    public class VirtualTopicValidatorTests : TestKit
    {
        [Fact]
        public async Task Should_Link_Datastream_To_VirtualTopic_On_Label_Match()
        {
            var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "12345678"));
            var helper = new GraphTestHelper(driver);
            await helper.ClearDatabase();

            //Create building and room in advance, like the real system expects
            await using (var session = driver.AsyncSession())
            {
                await session.RunAsync(@"
                    MERGE (b:Building { name: $building })
                    MERGE (r:Room { name: $room })
                    MERGE (b)-[:HAS_ROOM]->(r)
                ", new { building = "BuildingA", room = "RoomA" });
            }

            await helper.CreateTestVirtualTopic("RoomATemperatureSensors", new[] { "Temperature", "RoomA" });

            var topicProvider = new Neo4jVirtualTopicDefinitionProvider(driver);
            var actor = Sys.ActorOf(Props.Create(() =>
                new VirtualTopicValidatorActor(driver, topicProvider, TestActor)));

            //simulate stream creation (this triggers validation)
            actor.Tell(new CreateDatastreamMessage(
                "sensor-123",
                "BuildingA",
                "RoomA",
                "Temperature"
            ));

            await Task.Delay(300);

            //Relationship created by the actor
            var check = await helper.CheckIfDatastreamHasPublishedAsRelationship("sensor-123", "RoomATemperatureSensors");
            Assert.True(check);
        }

        [Fact]
        public async Task Should_Not_Apply_Label_Twice_When_Already_Validated()
        {
            var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "12345678"));
            var helper = new GraphTestHelper(driver);
            await helper.ClearDatabase();

            //Create building and room
            await using (var session = driver.AsyncSession())
            {
                await session.RunAsync(@"
                    MERGE (b:Building { name: $building })
                    MERGE (r:Room { name: $room })
                    MERGE (b)-[:HAS_ROOM]->(r)
                ", new { building = "BuildingA", room = "RoomA" });
            }

            await helper.CreateTestVirtualTopic("RoomATemperatureSensors", new[] { "Temperature", "RoomA" });

            var topicProvider = new Neo4jVirtualTopicDefinitionProvider(driver);
            var actor = Sys.ActorOf(Props.Create(() =>
                new VirtualTopicValidatorActor(driver, topicProvider, TestActor)));

            // Simulate stream creation (this will link it once)
            actor.Tell(new CreateDatastreamMessage(
                "sensor-123",
                "BuildingA",
                "RoomA",
                "Temperature"
            ));
            await Task.Delay(300);

            //Try to validate again (should be skipped due to cache)
            actor.Tell(new ValidateDatastreamMessage("sensor-123", new List<string>()));
            await Task.Delay(300);

            //Only one relationship exists
            var count = await helper.CountPublishedAsRelationships("sensor-123", "RoomATemperatureSensors");
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Should_Rescan_All_Datastreams_And_Link_Them_If_They_Match()
        {
            var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "12345678"));
            var helper = new GraphTestHelper(driver);
            await helper.ClearDatabase();

            // Arrange: insert rooms and datastreams manually
            await using (var session = driver.AsyncSession())
            {
                await session.RunAsync(@"
            MERGE (b:Building {name: 'BuildingA'})
            MERGE (r:Room {name: 'RoomA'})
            MERGE (b)-[:HAS_ROOM]->(r)
            WITH r
            UNWIND range(1, 3) AS i
            CREATE (d:Datastream:Temperature {streamId: 'sensor-' + i, type: 'Temperature'})
            MERGE (r)-[:HAS_DATASTREAM]->(d)
        ");
            }

            // Add a virtual topic that matches these
            await helper.CreateTestVirtualTopic("RoomATemperatureSensors", new[] { "Temperature", "RoomA" });

            var topicProvider = new Neo4jVirtualTopicDefinitionProvider(driver);
            var actor = Sys.ActorOf(Props.Create(() =>
                new VirtualTopicValidatorActor(driver, topicProvider, TestActor)));

            // Act: simulate admin-triggered rescan
            actor.Tell(new ForceFullDatastreamRescan());
            await Task.Delay(500); // wait for actor to process all

            // Assert: all 3 datastreams should be linked
            for (int i = 1; i <= 3; i++)
            {
                var streamId = $"sensor-{i}";
                var check = await helper.CheckIfDatastreamHasPublishedAsRelationship(streamId, "RoomATemperatureSensors");
                Assert.True(check, $"Stream {streamId} should be linked to RoomATemperatureSensors");
            }
        }
        [Fact]
        public async Task Should_Not_Link_Datastream_When_Labels_Do_Not_Match()
        {
            var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "12345678"));
            var helper = new GraphTestHelper(driver);
            await helper.ClearDatabase();

            // Arrange: setup a building and a room that does NOT match the topic
            await using (var session = driver.AsyncSession())
            {
                await session.RunAsync(@"
            MERGE (b:Building { name: $building })
            MERGE (r:Room { name: $room })
            MERGE (b)-[:HAS_ROOM]->(r)
        ", new { building = "BuildingB", room = "RoomX" });
            }

            // Topic expects RoomA, not RoomX
            await helper.CreateTestVirtualTopic("RoomATemperatureSensors", new[] { "Temperature", "RoomA" });

            var topicProvider = new Neo4jVirtualTopicDefinitionProvider(driver);
            var actor = Sys.ActorOf(Props.Create(() =>
                new VirtualTopicValidatorActor(driver, topicProvider, TestActor)));

            //Create datastream in RoomX
            actor.Tell(new CreateDatastreamMessage(
                "sensor-999",
                "BuildingB",
                "RoomX",
                "Temperature"
            ));

            await Task.Delay(300);

            // Should NOT be linked to the virtual topic
            var check = await helper.CheckIfDatastreamHasPublishedAsRelationship("sensor-999", "RoomATemperatureSensors");
            Assert.False(check, "Datastream with unmatched room should not be linked to the topic.");
        }

    }
}
