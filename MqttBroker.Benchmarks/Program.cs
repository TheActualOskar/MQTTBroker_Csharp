using MqttBroker.Benchmarks;
using System;
using System.Diagnostics;
using System.Threading;

class Program
{
        static async Task Main(string[] args)
        {
            await RealtimeDataBenchmark.RunAsync();
        }

    
}
