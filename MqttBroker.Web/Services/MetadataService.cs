using MqttBroker.Web.Pages;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Text;
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

        public async Task<List<string>> GetAllBuildingsAsync()
        {
            var results = new List<string>();
            var session = _driver.AsyncSession();

            try
            {
                var cursor = await session.RunAsync("MATCH (b:Building) RETURN b.name AS name ORDER BY name");
                await cursor.ForEachAsync(record => results.Add(record["name"].As<string>()));
            }
            finally
            {
                await session.CloseAsync();
            }

            return results;
        }

        public async Task<List<string>> GetAllRoomsAsync()
        {
            var results = new List<string>();
            var session = _driver.AsyncSession();

            try
            {
                var cursor = await session.RunAsync("MATCH (r:Room) RETURN r.name AS name ORDER BY name");
                await cursor.ForEachAsync(record => results.Add(record["name"].As<string>()));
            }
            finally
            {
                await session.CloseAsync();
            }

            return results;
        }

        public async Task<List<string>> GetAllSensorTypesAsync()
        {
            var results = new List<string>();
            var session = _driver.AsyncSession();

            try
            {
                var cursor = await session.RunAsync("MATCH (s:Stream) RETURN DISTINCT s.type AS type ORDER BY type");
                await cursor.ForEachAsync(record => results.Add(record["type"].As<string>()));
            }
            finally
            {
                await session.CloseAsync();
            }

            return results;
        }

        public async Task<List<CreateTopicModel.StreamPreview>> QueryStreamsByFilterAsync(CreateTopicModel.FilterModel filter)
        {
            var query = new StringBuilder();
            query.AppendLine("MATCH (b:Building)-[:CONTAINS]->(r:Room)-[:HAS_STREAM]->(s:Stream)");
            query.AppendLine("WHERE 1=1");

            var parameters = new Dictionary<string, object>();

            if (filter.Building?.Any() == true)
            {
                query.AppendLine("AND b.name IN $buildings");
                parameters["buildings"] = filter.Building;
            }

            if (filter.Room?.Any() == true)
            {
                query.AppendLine("AND r.name IN $rooms");
                parameters["rooms"] = filter.Room;
            }

            if (filter.SensorType?.Any() == true)
            {
                query.AppendLine("AND s.type IN $types");
                parameters["types"] = filter.SensorType;
            }

            if (filter.ActiveOnly)
            {
                var threshold = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (5 * 60 * 1000); // 5 minutes
                query.AppendLine("AND s.lastSeen > $activeSince");
                parameters["activeSince"] = threshold;
            }

            if (!string.IsNullOrEmpty(filter.TempThresholdOperator) && filter.TempThresholdValue.HasValue)
            {
                query.AppendLine($"AND (s.type <> 'Temperature' OR s.value {filter.TempThresholdOperator} $tempVal)");
                parameters["tempVal"] = filter.TempThresholdValue.Value;
            }

            if (!string.IsNullOrEmpty(filter.HumidityThresholdOperator) && filter.HumidityThresholdValue.HasValue)
            {
                query.AppendLine($"AND (s.type <> 'Humidity' OR s.value {filter.HumidityThresholdOperator} $humVal)");
                parameters["humVal"] = filter.HumidityThresholdValue.Value;
            }

            query.AppendLine("RETURN s.streamId AS streamId, s.type AS type, r.name AS location, s.lastSeen AS lastSeen");

            var results = new List<CreateTopicModel.StreamPreview>();
            var session = _driver.AsyncSession();

            try
            {
                var cursor = await session.RunAsync(query.ToString(), parameters);
                await cursor.ForEachAsync(record =>
                {
                    results.Add(new CreateTopicModel.StreamPreview
                    {
                        StreamId = record["streamId"].As<string>(),
                        Type = record["type"].As<string>(),
                        Location = record["location"].As<string>(),
                        LastSeen = FormatLastSeen(record["lastSeen"].As<long>())
                    });
                });
            }
            finally
            {
                await session.CloseAsync();
            }

            return results;
        }

        public async Task<List<CreateTopicModel.StreamPreview>> QueryStreamsByCypherAsync(string cypherQuery)
        {
            var results = new List<CreateTopicModel.StreamPreview>();
            var session = _driver.AsyncSession();

            try
            {
                var cursor = await session.RunAsync(cypherQuery);
                await cursor.ForEachAsync(record =>
                {
                    var streamNode = record["s"].As<INode>();
                    results.Add(new CreateTopicModel.StreamPreview
                    {
                        StreamId = streamNode.Properties.ContainsKey("streamId") ? streamNode.Properties["streamId"].As<string>() : "",
                        Type = streamNode.Properties.ContainsKey("type") ? streamNode.Properties["type"].As<string>() : "",
                        Location = streamNode.Properties.ContainsKey("location") ? streamNode.Properties["location"].As<string>() : "",
                        LastSeen = streamNode.Properties.ContainsKey("lastSeen") ? FormatLastSeen(streamNode.Properties["lastSeen"].As<long>()) : "unknown"
                    });
                });
            }
            finally
            {
                await session.CloseAsync();
            }

            return results;
        }

        public async ValueTask DisposeAsync()
        {
            if (_driver != null)
            {
                await _driver.DisposeAsync();
            }
        }

        private static string FormatLastSeen(long timestamp)
        {
            var lastSeen = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            var minutesAgo = (DateTimeOffset.UtcNow - lastSeen).TotalMinutes;
            return $"{Math.Round(minutesAgo)} min ago";
        }
    }
}
