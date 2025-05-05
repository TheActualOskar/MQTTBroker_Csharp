using Microsoft.AspNetCore.Mvc;
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
        public List<NamedSubscription> SubscribedSmartFilters { get; set; } = new();

        public async Task OnGetAsync()
        {
            Username = HttpContext.Session.GetString("Username");

            var client = await _context.Clients
                .Include(c => c.Subscriptions)
                .Include(c => c.ClientNamedSubscriptions)
                    .ThenInclude(cns => cns.NamedSubscription)
                .FirstOrDefaultAsync(c => c.Username == Username);

            if (client != null)
            {
                if (client.Subscriptions != null)
                {
                    SubscribedTopics = client.Subscriptions.Select(s => s.Topic).ToList();
                }

                if (client.ClientNamedSubscriptions != null)
                {
                    SubscribedSmartFilters = client.ClientNamedSubscriptions
                        .Select(cns => cns.NamedSubscription)
                        .ToList();
                }
            }

            AvailableTopics = await _metadataService.GetAllTopicsAsync();
        }

        public async Task<IActionResult> OnPostAsync(string Action, string TopicName, int? SmartFilterId)
        {
            Username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(Username))
                return RedirectToPage("/Login");

            var client = await _context.Clients
                .Include(c => c.Subscriptions)
                .Include(c => c.ClientNamedSubscriptions)
                .FirstOrDefaultAsync(c => c.Username == Username);

            if (client == null)
                return RedirectToPage();

            if (Action == "subscribe" && !string.IsNullOrEmpty(TopicName))
            {
                if (!client.Subscriptions.Any(s => s.Topic == TopicName))
                {
                    client.Subscriptions.Add(new Subscription
                    {
                        Topic = TopicName,
                        ClientId = client.Id
                    });

                    await _context.SaveChangesAsync();
                }
            }
            else if (Action == "unsubscribe" && !string.IsNullOrEmpty(TopicName))
            {
                var sub = client.Subscriptions.FirstOrDefault(s => s.Topic == TopicName);
                if (sub != null)
                {
                    _context.Subscriptions.Remove(sub);
                    await _context.SaveChangesAsync();
                }
            }
            else if (Action == "unsubscribeSmartFilter" && SmartFilterId.HasValue)
            {
                var filterSub = await _context.ClientNamedSubscriptions
                    .FirstOrDefaultAsync(s => s.ClientId == client.Id && s.NamedSubscriptionId == SmartFilterId.Value);

                if (filterSub != null)
                {
                    _context.ClientNamedSubscriptions.Remove(filterSub);
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToPage();
        }
    }
}
