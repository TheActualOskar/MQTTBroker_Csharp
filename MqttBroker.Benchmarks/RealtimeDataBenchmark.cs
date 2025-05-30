using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

namespace MqttBroker.Benchmarks
{
    public static class RealtimeDataBenchmark
    {
        public static async Task<long> RunOnce(int datastreamCount = 10, string brokerHost = "localhost", int brokerPort = 18883)
        {
            var factory = new MqttFactory();
            var mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, brokerPort)
                .Build();

            var received = 0;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var virtualTopic = "AutoTopic-1";
            var expectedPayloads = new ConcurrentDictionary<string, bool>();

            for (int i = 1; i <= datastreamCount; i++)
                expectedPayloads.TryAdd($"sensor-{i}", false);

            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                var payload = e.ApplicationMessage.Payload == null
                    ? string.Empty
                    : Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());

                if (expectedPayloads.TryUpdate(payload, true, false))
                {
                    var current = Interlocked.Increment(ref received);
                    if (current >= datastreamCount)
                        tcs.TrySetResult(true);
                }
                return Task.CompletedTask;
            };

            await mqttClient.ConnectAsync(options);
            await mqttClient.SubscribeAsync(virtualTopic);

            // Ensure subscription is active
            await Task.Delay(300);

            var sw = Stopwatch.StartNew();

            // Publish all messages
            for (int i = 1; i <= datastreamCount; i++)
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(virtualTopic)
                    .WithPayload($"sensor-{i}")
                    .Build();

                await mqttClient.PublishAsync(message);
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
