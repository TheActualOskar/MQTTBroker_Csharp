using System.Threading.Tasks;
using MqttBroker.Benchmarks;

namespace MqttBroker.Benchmarks
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            int[] topicCounts = { 10, 100, 1000, 4000 };
            var benchmark = new BrokerBenchmark();
            foreach (var n in topicCounts)
            {
                await benchmark.Run(n);
            }
            Console.WriteLine("All benchmarks complete.");
        }
    }
}
