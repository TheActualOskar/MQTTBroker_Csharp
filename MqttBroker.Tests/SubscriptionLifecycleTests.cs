using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.EntityFrameworkCore;
using MqttBroker.Actors;
using MqttBroker.Database;
using MqttBroker.Messages;
using MqttBroker.Models;
using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.Linq;
using System.Collections.Generic;

namespace MqttBroker.Tests
{
    public class SubscriptionLifecycleTests : TestKit
    {
        [Fact]
        public async Task Should_Store_Subscription_In_Database()
        {
            var dbName = Guid.NewGuid().ToString();

            var optionsBuilder = new DbContextOptionsBuilder<BrokerDbContext>()
                .UseInMemoryDatabase(dbName);

            var actorOptions = optionsBuilder.Options;
            var verifyOptions = optionsBuilder.Options;

            //Insert a client with all required fields
            using (var seedContext = new BrokerDbContext(actorOptions))
            {
                seedContext.Clients.Add(new Client
                {
                    ClientId = "test-client",
                    Username = "client",
                    Email = "test@example.com", 
                    PasswordHash = "dummy",
                    Role = "client",
                    Subscriptions = new List<Subscription>()
                });
                seedContext.SaveChanges();
            }

            using var actorContext = new BrokerDbContext(actorOptions);
            var actor = Sys.ActorOf(Props.Create(() => new SubscribeHandler(actorContext)));

            var topic = "temp/roomA";
            var packet = BuildValidSubscribePacket(topic);
            var mqttPacket = new MqttRawPacket(MqttPacketType.Subscribe, packet);

            // Act
            actor.Tell(mqttPacket);
            await Task.Delay(200);

            using var verifyContext = new BrokerDbContext(verifyOptions);
            var allClients = await verifyContext.Clients
                .Include(c => c.Subscriptions)
                .ToListAsync();

            var client = allClients.FirstOrDefault(c => c.ClientId == "test-client");

            Assert.NotNull(client);
            Assert.Contains(client.Subscriptions, s => s.Topic == topic);
        }

        private static byte[] BuildValidSubscribePacket(string topic)
        {
            var topicBytes = Encoding.UTF8.GetBytes(topic);
            int topicLength = topicBytes.Length;
            byte[] packet = new byte[2 + 2 + 2 + topicLength + 1];

            packet[0] = 0x82;
            packet[1] = (byte)(packet.Length - 2);
            packet[2] = 0x00; packet[3] = 0x01; // Packet ID
            packet[4] = (byte)(topicLength >> 8);
            packet[5] = (byte)(topicLength & 0xFF);
            Buffer.BlockCopy(topicBytes, 0, packet, 6, topicLength);
            packet[6 + topicLength] = 0x00; // QoS

            return packet;
        }
    }
}
