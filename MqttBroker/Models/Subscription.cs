namespace MqttBroker.Models
{
    public class Subscription
    {
        public int Id { get; set; }             
        public string Topic { get; set; }           

        public int ClientId { get; set; }           
        public Client Client { get; set; }
    }
}
