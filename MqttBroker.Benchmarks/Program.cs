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
    }
}


