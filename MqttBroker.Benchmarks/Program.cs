using MqttBroker.Benchmarks;
using System.Threading.Tasks;

using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        int[] testSizes = { 10, 100, 1000, 4000 };
        foreach (var count in testSizes)
        {
            await BrokerBenchmark.RunScenarioMultipleTimes(count, repetitions: 3);
        }
        foreach (var count in testSizes)
        {
            await BrokerBenchmark.RunQueryLoopScenarioMultipleTimes(count, repetitions: 3);
        }

        /*
        try
        {
            var elapsed = await RealtimeDataBenchmark.RunOnce();
            Console.WriteLine($"All messages received in {elapsed} ms.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Benchmark failed: {ex.Message}");
        }*/
    }
}


