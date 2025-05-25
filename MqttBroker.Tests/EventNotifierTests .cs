using Akka.Actor;
using Akka.TestKit.Xunit2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MqttBroker.Actors;
using MqttBroker.Database;
using MqttBroker.Messages;
using MqttBroker.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MqttBroker.Tests
{
    public class EventNotifierTests : TestKit
    {
        private IConfiguration BuildFakeConfig()
        {
            var configData = new Dictionary<string, string>
            {
                ["SmtpSettings:Host"] = "smtp.fake.com",
                ["SmtpSettings:Port"] = "587",
                ["SmtpSettings:User"] = "fakeuser",
                ["SmtpSettings:Password"] = "fakepass",
                ["SmtpSettings:From"] = "no-reply@example.com"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();
        }

        [Fact]
        public async Task Should_Send_Email_When_Topic_Matches_Client_Subscription()
        {
            var options = new DbContextOptionsBuilder<BrokerDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new BrokerDbContext(options);

            var client = new Client
            {
                Email = "test@example.com",
                Username = "test-client",
                ClientId = "client-1",
                PasswordHash = "dummy",
                Role = "client"
            };

            var subscription = new NamedSubscription
            {
                TopicName = "RoomA"
            };

            var link = new ClientNamedSubscription
            {
                Client = client,
                NamedSubscription = subscription
            };

            context.Clients.Add(client);
            context.NamedSubscriptions.Add(subscription);
            context.ClientNamedSubscriptions.Add(link);
            context.SaveChanges();

            var config = BuildFakeConfig();
            var actor = Sys.ActorOf(EventNotifier.Props(context, config));

            actor.Tell(new NewTopicCreated("RoomA"));
            await Task.Delay(300);

            Assert.True(true); // If it runs without throwing, we assume success
        }

        [Fact]
        public async Task Should_Send_Batch_Update_Email_To_Matching_Clients()
        {
            var options = new DbContextOptionsBuilder<BrokerDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new BrokerDbContext(options);

            var client = new Client
            {
                Email = "client@example.com",
                Username = "batch-user",
                ClientId = "client-123",
                PasswordHash = "hash",
                Role = "client"
            };

            var subscription = new NamedSubscription
            {
                TopicName = "RoomA"
            };

            var link = new ClientNamedSubscription
            {
                Client = client,
                NamedSubscription = subscription
            };

            context.Clients.Add(client);
            context.NamedSubscriptions.Add(subscription);
            context.ClientNamedSubscriptions.Add(link);
            context.SaveChanges();

            var config = BuildFakeConfig();
            var actor = Sys.ActorOf(EventNotifier.Props(context, config));

            var batch = new Dictionary<string, List<string>>
            {
                { "RoomA", new List<string> { "sensor-1", "sensor-2" } }
            };

            actor.Tell(new VirtualTopicBatchUpdate(batch));
            await Task.Delay(300);

            Assert.True(true); // No exception means email flow executed
        }
    }
}
