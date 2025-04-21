using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MqttBroker.Web.Services
{
    public class MetadataService : IAsyncDisposable
    {
        private readonly IDriver _driver;

        public MetadataService(string uri, string user, string password)
        {
            _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        }

        public async Task<List<string>> GetAllTopicsAsync()
        {
            var topics = new List<string>();

            var session = _driver.AsyncSession();

            try
            {
                var result = await session.RunAsync(
                    "MATCH (d:Device)-[:PROVIDES]->(:Datastream)-[:PUBLISHED_AS]->(t:Topic) RETURN t.name AS topic");

                await result.ForEachAsync(record =>
                {
                    topics.Add(record["topic"].As<string>());
                });
            }
            finally
            {
                await session.CloseAsync();
            }

            return topics;
        }
        public async Task CreateDatastreamAsync(string deviceId, string streamId, string unit, string frequency, string topicName)
        {
            var session = _driver.AsyncSession();

            try
            {
                await session.WriteTransactionAsync(async tx =>
                {
                    await tx.RunAsync(@"
                MERGE (d:Device {id: $deviceId})
                MERGE (s:Datastream {id: $streamId})
                SET s.unit = $unit, s.frequency = $frequency
                MERGE (t:Topic {name: $topicName})
                MERGE (d)-[:PROVIDES]->(s)
                MERGE (s)-[:PUBLISHED_AS]->(t)
            ",
                    new
                    {
                        deviceId,
                        streamId,
                        unit,
                        frequency,
                        topicName
                    });
                });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_driver != null)
                await _driver.DisposeAsync();
        }
    }
}
