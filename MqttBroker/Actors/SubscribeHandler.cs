using Akka.Actor;
using MqttBroker.Helpers;
using MqttBroker.Messages;
using MqttBroker.Database;
using MqttBroker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace MqttBroker.Actors
{
    public class SubscribeHandler : ReceiveActor
    {
        private readonly BrokerDbContext _dbContext;

        public SubscribeHandler(BrokerDbContext dbContext)
        {
            _dbContext = dbContext;

          
            Receive<MqttRawPacket>(packet =>
            {
                var clientId = "test-client"; // Temporary placeholder
                var topics = ParseSubscribePacket(packet.RawBytes);

                Console.WriteLine("SubscribeHandler: Client wants to subscribe to:");
                foreach (var topic in topics)
                {
                    Console.WriteLine($"  - {topic}");
                }

                // Save to PostgreSQL (Include Subscriptions to avoid null)
                var client = _dbContext.Clients
                    .Include(c => c.Subscriptions)
                    .FirstOrDefault(c => c.ClientId == clientId);

                if (client == null)
                {
                    client = new Client
                    {
                        ClientId = clientId,
                        Subscriptions = new List<Subscription>()
                    };
                    _dbContext.Clients.Add(client);
                }

                foreach (var topic in topics)
                {
                    if (!client.Subscriptions.Any(s => s.Topic == topic))
                    {
                        client.Subscriptions.Add(new Subscription
                        {
                            Topic = topic,
                            Client = client
                        });
                    }
                }

                _dbContext.SaveChanges();

                Console.WriteLine($"✅ Saved {topics.Length} subscription(s) for '{clientId}'");

                // You can still forward this to another actor if needed
                // _customerDb.Tell(new SubscriptionMessage(clientId, topics));
            });

            // For stream routing
            Receive<InboundMqttPacket>(packet =>
            {
                var topics = ParseSubscribePacket(packet.RawBytes);

                Console.WriteLine("SubscribeHandler: Client wants to subscribe to:");
                foreach (var topic in topics)
                {
                    Console.WriteLine($"  - {topic}");

                    SubscriptionManager.AddSubscriber(topic, packet.Stream);
                }
            });
        }

        private string[] ParseSubscribePacket(byte[] raw)
        {
            var topics = new List<string>();
            int index = 4;

            while (index < raw.Length)
            {
                if (index + 2 > raw.Length) break;

                int topicLength = (raw[index] << 8) + raw[index + 1];
                index += 2;

                if (index + topicLength > raw.Length) break;

                string topic = Encoding.UTF8.GetString(raw, index, topicLength);
                index += topicLength;

                byte qos = raw[index];
                index++;

                topics.Add(topic);
            }

            return topics.ToArray();
        }
    }
}
