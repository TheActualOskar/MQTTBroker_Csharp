using Neo4j.Driver;

namespace MqttBroker.Tests
{
    public class GraphIngestionTests
    {
        [Fact]
        public async Task Should_Create_Building_Room_Datastream_Graph()
        {
            var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "12345678"));

            var helper = new GraphTestHelper(driver);
            await helper.ClearDatabase();

            await helper.CreateBuildingRoomDatastreamGraph("BuildingA", "RoomA", "sensor-123", "Temperature");

            var check = await helper.CheckNodeExists("Datastream", "sensor-123");
            Assert.True(check);
        }
    }
}
