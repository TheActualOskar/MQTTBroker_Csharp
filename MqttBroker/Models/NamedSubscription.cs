using System.ComponentModel.DataAnnotations;

namespace MqttBroker.Models
{
    public class NamedSubscription
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string CypherQuery { get; set; } = string.Empty;

        public int CreatedByClientId { get; set; }
        public Client CreatedByClient { get; set; }

        public int CurrentMatchCount { get; set; }
        public string LastResultHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdated { get; set; }

        public List<ClientNamedSubscription> SubscribedClients { get; set; } = new();
    }
}
