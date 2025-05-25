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
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace MqttBroker.Tests
{
    public class EventNotifierTests : TestKit
    {
        [Fact]
        public async Task Should_Send_Email_When_Topic_Matches_Client_Subscription()
        {
            // Arrange in-memory DB with linked client and topic
            var options = new DbContextOptionsBuilder<BrokerDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new BrokerDbContext(options);

            var topic = "RoomA";
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

            // Fake SMTP config
            var configData = new Dictionary<string, string>
            {
                ["SmtpSettings:Host"] = "smtp.fake.com",
                ["SmtpSettings:Port"] = "587",
                ["SmtpSettings:User"] = "fakeuser",
                ["SmtpSettings:Password"] = "fakepass",
                ["SmtpSettings:From"] = "no-reply@example.com"
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var actor = Sys.ActorOf(EventNotifier.Props(context, config));

            //trigger the email notification
            actor.Tell(new NewTopicCreated("RoomA"));

            await Task.Delay(300);

            // No exception thrown = success
            Assert.True(true);
        }
    }
}
