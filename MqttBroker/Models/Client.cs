using System.Collections.Generic;

namespace MqttBroker.Models
{
    public class Client
    {
        
            public int Id { get; set; }
            public string ClientId { get; set; }

            // Add for login functionality
            public string Username { get; set; }
            public string PasswordHash { get; set; }


            // Differentiate between client/admin
            public string Role { get; set; }  // e.g., "admin" or "client"

            public ICollection<Subscription> Subscriptions { get; set; }
    }
}
