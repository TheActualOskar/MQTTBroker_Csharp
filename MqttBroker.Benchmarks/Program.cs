using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        string topic = "test/latency";
        string brokerHost = "localhost";
        int brokerPort = 18883;
        int messageCount = 20;

        List<double> latencies = new();

        // Start mosquitto_sub and listen on stdout
        var subProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "mosquitto_sub",
                Arguments = $"-t {topic} -h {brokerHost} -p {brokerPort}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        subProcess.Start();
        var reader = subProcess.StandardOutput;

        // Give subscriber time to connect
        await Task.Delay(1000);

        for (int i = 0; i < messageCount; i++)
        {
            string payload = $"msg-{i}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            var stopwatch = Stopwatch.StartNew();

            var pubProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "mosquitto_pub",
                    Arguments = $"-t {topic} -m {payload} -h {brokerHost} -p {brokerPort}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            pubProcess.Start();
            pubProcess.WaitForExit();

            string? received = await reader.ReadLineAsync();
            stopwatch.Stop();

            if (received != null && received.StartsWith("msg-"))
            {
                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
                Console.WriteLine($" Message {i + 1}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            }
            else
            {
                Console.WriteLine($" Message {i + 1} failed or not received.");
            }

            await Task.Delay(10); // Brief delay between messages
        }

        subProcess.Kill();

        // Output results
        if (latencies.Count > 0)
        {
            double avg = latencies.Average();
            double min = latencies.Min();
            double max = latencies.Max();

            Console.WriteLine($"\n Test Summary ({latencies.Count} messages received):");
            Console.WriteLine($" Avg Latency: {avg:F2} ms");
            Console.WriteLine($" Min Latency: {min:F2} ms");
            Console.WriteLine($" Max Latency: {max:F2} ms");
        }
        else
        {
            Console.WriteLine(" No messages were received.");
        }
    }
}
