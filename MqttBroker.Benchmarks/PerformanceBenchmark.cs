using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttBroker.Benchmarks
{
    public class PerformanceBenchmark
    {/*
        int messagesPerBatch = 10;
        int durationSeconds = 30;
        string brokerHost = "localhost";
        int brokerPort = 18883;

        for (int batch = 0; batch<durationSeconds; batch++)
        {
            int baseIndex = batch * messagesPerBatch;

            for (int i = 0; i<messagesPerBatch; i++)
            {
                int index = baseIndex + i;

        ThreadPool.QueueUserWorkItem(_ =>
                {
                    string topic = $"room/{index % 10}/temperature";
        string jsonPayload = $"{{\\\"id\\\":{index},\\\"value\\\":{20 + index % 10}}}";
        string arguments = $"-t {topic} -m \"{jsonPayload}\" -h {brokerHost} -p {brokerPort}";

                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "mosquitto_pub",
                            Arguments = arguments,
                            RedirectStandardOutput = false,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to publish message {index}: {ex.Message}");
                    }
                });
            }

            Console.WriteLine($"> Dispatched {messagesPerBatch} messages at second {batch + 1}");
Thread.Sleep(1000); // Wait one second before next batch
        }

        Console.WriteLine("Finished dispatching all messages.");
    }
    */
    }
}
