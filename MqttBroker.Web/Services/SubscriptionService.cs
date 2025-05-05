using MqttBroker.Models;
using Neo4j.Driver;
using Microsoft.EntityFrameworkCore;
using MqttBroker.Database;

namespace MqttBroker.Web.Services
{
    public class SubscriptionService
    {
        private readonly BrokerDbContext _dbContext;
        private readonly IDriver _neo4j;

        public SubscriptionService(BrokerDbContext dbContext, IDriver neo4j)
        {
            _dbContext = dbContext;
            _neo4j = neo4j;
        }

        public async Task<NamedSubscription> CreateNamedSubscriptionAsync(
            int clientId,
            string name,
            string description,
            string cypherQuery)
        {
            // Run initial query to get baseline result + match count
            var result = await RunCypherQuery(cypherQuery);
            var resultHash = HashResult(result);
            int matchCount = result.Count;

            var namedSubscription = new NamedSubscription
            {
                Name = name,
                Description = description,
                CypherQuery = cypherQuery,
                CreatedByClientId = clientId,
                LastResultHash = resultHash,
                CurrentMatchCount = matchCount,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                SubscribedClients = new List<ClientNamedSubscription>
                {
                    new ClientNamedSubscription
                    {
                        ClientId = clientId
                    }
                }
            };

            _dbContext.NamedSubscriptions.Add(namedSubscription);
            await _dbContext.SaveChangesAsync();

            return namedSubscription;
        }

        private async Task<List<Dictionary<string, object>>> RunCypherQuery(string query)
        {
            var results = new List<Dictionary<string, object>>();

            var session = _neo4j.AsyncSession();
            try
            {
                var cursor = await session.RunAsync(query);
                while (await cursor.FetchAsync())
                {
                    results.Add(cursor.Current.Values.ToDictionary(k => k.Key, v => v.Value));
                }
            }
            finally
            {
                await session.CloseAsync();
            }

            return results;
        }

        private string HashResult(List<Dictionary<string, object>> result)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(result);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(hashBytes);
        }
    }
}
