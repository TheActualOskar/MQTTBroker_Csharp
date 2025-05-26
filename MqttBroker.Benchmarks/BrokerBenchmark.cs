using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MqttBroker.Tests; // For GraphTestHelper

class Program
{

    static async Task RunScenario(int virtualTopicCount)
    {
        // 1. Prepare data
        var driver = Neo4j.Driver.GraphDatabase.Driver("bolt://localhost:7687", Neo4j.Driver.AuthTokens.Basic("neo4j", "12345678"));
        var helper = new GraphTestHelper(driver);
        await helper.ClearDatabase();

        // Create rooms and datastreams
        await using (var session = driver.AsyncSession())
        {
            await session.RunAsync(@"
                MERGE (b:Building {name: 'BuildingA'})
                WITH b
                UNWIND range(1, 10) AS i
                MERGE (r:Room {name: 'Room' + i})
                MERGE (b)-[:HAS_ROOM]->(r)
            ");
        }
        await using (var session = driver.AsyncSession())
        {
            await session.RunAsync(@"
                UNWIND range(1, 200) AS i
                MATCH (r:Room) WHERE r.name = 'Room' + ((i % 10) + 1)
                CREATE (d:Datastream:Temperature {streamId: 'sensor-' + i, type: 'Temperature'})
                MERGE (r)-[:HAS_DATASTREAM]->(d)
            ");
        }
        for (int i = 1; i <= virtualTopicCount; i++)
        {
            var label = "Temperature";
            var room = $"Room{(i % 10) + 1}";
            var topicName = $"AutoTopic-{i}";
            await helper.CreateTestVirtualTopic(topicName, new[] { label, room });
        }

        // 2. Attach to broker process
        var proc = Process.GetProcessesByName("MqttBroker.Web").FirstOrDefault();
        if (proc == null)
        {
            Console.WriteLine("Broker process not found.");
            return;
        }

        // 3. Measure before
        proc.Refresh();
        var cpuBefore = proc.TotalProcessorTime;
        var memBefore = proc.WorkingSet64;

        // 4. Trigger refresh via API
        var client = new HttpClient();
        var sw = Stopwatch.StartNew();
        var response = await client.PostAsync("https://localhost:7086/admin/refresh-virtual-topics", null);
        response.EnsureSuccessStatusCode();

        // 5. Wait for refresh to complete (adjust as needed)
        await Task.Delay(3000);

        sw.Stop();

        // 6. Measure after
        proc.Refresh();
        var cpuAfter = proc.TotalProcessorTime;
        var memAfter = proc.WorkingSet64;

        // 7. Output results
        var line = $"{virtualTopicCount},{sw.ElapsedMilliseconds},{(cpuAfter - cpuBefore).TotalMilliseconds:F1},{(memAfter - memBefore) / (1024.0 * 1024.0):F2}";
        Console.WriteLine($"VirtualTopics: {line}");

        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "refresh_benchmark_results.csv");
        if (!File.Exists(filePath))
            File.AppendAllText(filePath, "virtualTopicCount,timeMs,cpuMs,workingSetDeltaMb\n");
        File.AppendAllText(filePath, line + Environment.NewLine);
    }
}
