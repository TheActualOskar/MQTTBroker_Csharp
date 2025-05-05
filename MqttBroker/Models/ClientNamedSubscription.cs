using MqttBroker.Models;

namespace MqttBroker.Models
{
    public class ClientNamedSubscription
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public Client Client { get; set; }
        public int NamedSubscriptionId { get; set; }
        public NamedSubscription NamedSubscription { get; set; }
    }
}
