using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MqttBroker.Database;
using MqttBroker.Models;
using MqttBroker.Web.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MqttBroker.Web.Pages
{
    public class ClientDashboardModel : PageModel
    {
        private readonly BrokerDbContext _context;
        private readonly MetadataService _metadataService;

        public ClientDashboardModel(BrokerDbContext context, MetadataService metadataService)
        {
            _context = context;
            _metadataService = metadataService;
        }

        public string Username { get; set; }
        public List<string> SubscribedTopics { get; set; } = new();
        public List<string> AvailableTopics { get; set; } = new();

        public async Task OnGetAsync()
        {
            Username = HttpContext.Session.GetString("Username");

            var client = _context.Clients
                .Include(c => c.Subscriptions)
                .FirstOrDefault(c => c.Username == Username);

            if (client != null && client.Subscriptions != null)
            {
                SubscribedTopics = client.Subscriptions.Select(s => s.Topic).ToList();
            }

            // Pull all available topics from Neo4j
            AvailableTopics = await _metadataService.GetAllTopicsAsync();
        }
    }
}
