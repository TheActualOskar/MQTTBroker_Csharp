using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MqttBroker.Tests; // Make sure this reference is available

namespace MqttBroker.Benchmarks
{
    public static class RealtimeDataBenchmark
    {
        public static async Task<long> RunWithSetup(
            int datastreamCount = 5,
            int messagesPerDatastream = 10,
            string brokerHost = "localhost",
            int brokerPort = 18883)
        {
            // --- 1. Register virtual topic and datastreams ---
            var driver = Neo4j.Driver.GraphDatabase.Driver(
                "bolt://localhost:7687",
                Neo4j.Driver.AuthTokens.Basic("neo4j", "12345678"));
            var helper = new GraphTestHelper(driver);

            // Clean up and create virtual topic
            await helper.ClearDatabase();
            await helper.CreateTestVirtualTopic("AutoTopic-1", new[] { "Temperature" });

            // Create datastreams and associate with the topic
            for (int i = 1; i <= datastreamCount; i++)
            {
                await helper.CreateBuildingRoomDatastreamGraph(
                    "BuildingA", $"Room{i}", $"sensor-{i}", "Temperature");
            }

            // --- 2. MQTT subscribe/publish benchmark ---
            var factory = new MqttFactory();
            var mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, brokerPort)
                .Build();

            int totalMessages = datastreamCount * messagesPerDatastream;
            int received = 0;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var virtualTopic = "AutoTopic-1";
            var expectedPayloads = new ConcurrentDictionary<string, int>();

            for (int i = 1; i <= datastreamCount; i++)
                expectedPayloads.TryAdd($"sensor-{i}", 0);

            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                var payload = e.ApplicationMessage.Payload == null
                    ? string.Empty
                    : Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());

                if (expectedPayloads.ContainsKey(payload))
                {
                    expectedPayloads.AddOrUpdate(payload, 1, (key, oldValue) => oldValue + 1);
                    var current = Interlocked.Increment(ref received);
                    if (current >= totalMessages)
                        tcs.TrySetResult(true);
                }
                return Task.CompletedTask;
            };

            await mqttClient.ConnectAsync(options);
            await mqttClient.SubscribeAsync(virtualTopic);

            // Ensure subscription is active
            await Task.Delay(300);

            var sw = Stopwatch.StartNew();

            // Publish messages for each datastream
            for (int i = 1; i <= datastreamCount; i++)
            {
                for (int j = 1; j <= messagesPerDatastream; j++)
                {
                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic(virtualTopic)
                        .WithPayload($"sensor-{i}")
                        .Build();

                    await mqttClient.PublishAsync(message);
                }
            }

            // Wait for all messages or timeout
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            sw.Stop();

            await mqttClient.DisconnectAsync();

            if (!tcs.Task.IsCompleted)
                throw new TimeoutException($"Timeout: Only {received} messages received in {sw.ElapsedMilliseconds} ms.");

            return sw.ElapsedMilliseconds;
        }
    }
}
