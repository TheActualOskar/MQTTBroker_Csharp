using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using LibreHardwareMonitor.Hardware;
using MqttBroker.Tests; // For GraphTestHelper

public static class BrokerBenchmark
{

    public static async Task RunScenarioMultipleTimes(int virtualTopicCount, int repetitions = 3)
    {
        for (int i = 1; i <= repetitions; i++)
        {
            Console.WriteLine($"--- Run {i} of {repetitions} for virtualTopicCount={virtualTopicCount} ---");
            await RunScenario(virtualTopicCount);
        }
    }

    public static async Task RunScenario(int virtualTopicCount)
    {
        var driver = Neo4j.Driver.GraphDatabase.Driver("bolt://localhost:7687", Neo4j.Driver.AuthTokens.Basic("neo4j", "12345678"));
        var helper = new GraphTestHelper(driver);
        await helper.ClearDatabase();

        for (int i = 1; i <= 10; i++)
            await helper.CreateBuildingRoomDatastreamGraph("BuildingA", $"Room{i}", $"sensor-{i}", "Temperature");

        for (int i = 11; i <= 200; i++)
            await helper.CreateBuildingRoomDatastreamGraph("BuildingA", $"Room{((i - 1) % 10) + 1}", $"sensor-{i}", "Temperature");

        for (int i = 1; i <= virtualTopicCount; i++)
            await helper.CreateTestVirtualTopic($"AutoTopic-{i}", new[] { "Temperature", $"Room{((i - 1) % 10) + 1}" });

        var proc = Process.GetProcessesByName("MqttBroker.Web").FirstOrDefault();
        if (proc == null)
        {
            Console.WriteLine("Broker process not found.");
            return;
        }

        var cts = new CancellationTokenSource();
        var monitor = new LiveMetricsMonitor(proc, cts.Token);
        var monitorTask = monitor.StartAsync();

        var client = new HttpClient();
        
        var sw = Stopwatch.StartNew();
        var response = await client.PostAsync("https://localhost:7086/admin/refresh-virtual-topics", null);
        response.EnsureSuccessStatusCode();

        
        while (true)
        {
            // Check if the first datastream has the expected relationship to the first virtual topic
            bool isReady = await helper.CheckIfDatastreamHasPublishedAsRelationship("sensor-1", "AutoTopic-1");
            if (isReady)
                break;
            await Task.Delay(100); // Poll every 100ms
        }
        sw.Stop();

        cts.Cancel();
        await monitorTask;

        var line = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2:F1},{3:F1}",
            virtualTopicCount, sw.ElapsedMilliseconds, monitor.MaxCpu, monitor.MaxProcessMemoryMb
        );
        Console.WriteLine($"VirtualTopics: {line}");

        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "refresh_benchmark_results.csv");
        if (!File.Exists(filePath))
            File.AppendAllText(filePath, "virtualTopicCount,timeMs,maxCpuLoad,maxProcessMemoryMb\n");
        File.AppendAllText(filePath, line + Environment.NewLine);
    }

    // --- Query-Loop Benchmark ---

    public static async Task RunQueryLoopScenarioMultipleTimes(int queryCount, int repetitions = 3)
    {
        for (int i = 1; i <= repetitions; i++)
        {
            Console.WriteLine($"--- QueryLoop Run {i} of {repetitions} for queryCount={queryCount} ---");
            await RunQueryLoopScenario(queryCount);
        }
    }

    public static async Task RunQueryLoopScenario(int queryCount)
    {
        var driver = Neo4j.Driver.GraphDatabase.Driver("bolt://localhost:7687", Neo4j.Driver.AuthTokens.Basic("neo4j", "12345678"));
        var helper = new GraphTestHelper(driver);
        await helper.ClearDatabase();

        // Setup: 200 datastreams
        for (int i = 1; i <= 10; i++)
            await helper.CreateBuildingRoomDatastreamGraph("BuildingA", $"Room{i}", $"sensor-{i}", "Temperature");
        for (int i = 11; i <= 200; i++)
            await helper.CreateBuildingRoomDatastreamGraph("BuildingA", $"Room{((i - 1) % 10) + 1}", $"sensor-{i}", "Temperature");

        // Find the Neo4j process for monitoring
        var proc = Process.GetProcessesByName("java").FirstOrDefault();
        if (proc == null)
        {
            Console.WriteLine("Neo4j process not found.");
            return;
        }

        var cts = new CancellationTokenSource();
        var monitor = new LiveMetricsMonitor(proc, cts.Token);
        var monitorTask = monitor.StartAsync();

        // Simple "get data" Cypher query for each datastream (simulate live data access)
        string cypher = "MATCH (d:Datastream {id: $id}) RETURN d.id, d.type";
        var session = driver.AsyncSession();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < queryCount; i++)
        {
            var id = $"sensor-{(i % 200) + 1}";
            var result = await session.RunAsync(cypher, new { id });
            await result.ConsumeAsync();
        }

        sw.Stop();
        await session.CloseAsync();

        cts.Cancel();
        await monitorTask;

        var line = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2:F1},{3:F1}",
            queryCount, sw.ElapsedMilliseconds, monitor.MaxCpu, monitor.MaxProcessMemoryMb
        );
        Console.WriteLine($"QueryLoop: {line}");

        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "queryloop_benchmark_results.csv");
        if (!File.Exists(filePath))
            File.AppendAllText(filePath, "queryCount,timeMs,maxCpuLoad,maxProcessMemoryMb\n");
        File.AppendAllText(filePath, line + Environment.NewLine);
    }

    public class LiveMetricsMonitor
    {
        private readonly Process _proc;
        private readonly Computer _computer = new Computer { IsCpuEnabled = true, IsMemoryEnabled = true };
        private float _maxCpu = 0;
        private float _maxProcMemMb = 0;
        private readonly CancellationToken _token;
        private bool _debuggedMemorySensors = false;

        public float MaxCpu => _maxCpu;
        public float MaxProcessMemoryMb => _maxProcMemMb;

        public LiveMetricsMonitor(Process proc, CancellationToken token)
        {
            _proc = proc;
            _token = token;
            _computer.Open();
        }

        public async Task StartAsync()
        {
            Console.WriteLine($"[DEBUG] Culture: {System.Globalization.CultureInfo.CurrentCulture.Name}");

            while (!_token.IsCancellationRequested)
            {
                foreach (var hw in _computer.Hardware)
                {
                    hw.Update();

                    if (hw.HardwareType == HardwareType.Cpu)
                    {
                        foreach (var sensor in hw.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Name == "CPU Total")
                            {
                                if (sensor.Value.HasValue && !float.IsNaN(sensor.Value.Value))
                                    _maxCpu = Math.Max(_maxCpu, sensor.Value.Value);
                            }
                        }
                    }

                    if (hw.HardwareType == HardwareType.Memory)
                    {
                        foreach (var sensor in hw.Sensors)
                        {
                            if (!_debuggedMemorySensors)
                                Console.WriteLine($"[DEBUG] MemSensor: {sensor.Name} [{sensor.SensorType}] = {sensor.Value}");
                        }

                        _debuggedMemorySensors = true;
                    }
                }

                _proc.Refresh();
                _maxProcMemMb = Math.Max(_maxProcMemMb, _proc.WorkingSet64 / 1024f / 1024f);

                Console.WriteLine($"[LiveMonitor] CPU Max: {_maxCpu:F1}%, Proc Mem Max: {_maxProcMemMb:F1} MB");

                await Task.Delay(200);
            }

            _computer.Close();
        }
    }
}
