namespace MqttBroker.Models
{
    public class Subscription
    {
        public int Id { get; set; }                 // Auto-generated primary key
        public string Topic { get; set; }           // Topic the client subscribed to

        public int ClientId { get; set; }           // Foreign key to Client
        public Client Client { get; set; }
    }
}
