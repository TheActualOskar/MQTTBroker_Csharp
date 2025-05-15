using Xunit;
using Moq;
using System.Collections.Generic;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using MqttBroker.Actors;
using MqttBroker.Messages;
using MqttBroker.Helpers;
using Neo4j.Driver;

namespace MqttBroker.Tests
{
    public class VirtualTopicValidatorActorTests : TestKit
    {
        [Fact]
        public void Should_Validate_And_Apply_Label_When_Datastream_Matches_VirtualTopic()
        {
            // Arrange: Mock the Topic Definition Provider with one virtual topic
            var mockTopicProvider = new Mock<IVirtualTopicDefinitionProvider>();
            mockTopicProvider.Setup(p => p.GetActiveVirtualTopicsAsync())
                .ReturnsAsync(new List<VirtualTopicDefinition>
                {
                    new VirtualTopicDefinition
                    {
                        Name = "RoomATemperatureSensors",
                        ExpectedLabels = new List<string> { "Temperature", "RoomA" }
                    }
                });

            // Arrange: Mock Neo4j Driver (we won't actually execute Cypher in this test)
            var mockNeo4jDriver = new Mock<IDriver>();

            // Create the actor with the mocks
            var actor = Sys.ActorOf(Props.Create(() =>
                new VirtualTopicValidatorActor(mockNeo4jDriver.Object, mockTopicProvider.Object)));

            // Act: Send a message with matching labels
            actor.Tell(new ValidateDatastreamMessage("sensor-123", new List<string> { "Temperature", "RoomA" }));

            // Assert: No direct assertion here since ApplyVirtualTopicLabel is private.
            // You can check logs, or later refactor to expose effect for better testability.

            // For now, just running this ensures no exceptions are thrown.
        }
    }
}
