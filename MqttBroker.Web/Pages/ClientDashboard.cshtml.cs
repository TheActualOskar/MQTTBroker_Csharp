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

            AvailableTopics = await _metadataService.GetAllTopicsAsync();
        }

        public async Task<IActionResult> OnPostAsync(string Action, string TopicName)
        {
            Username = HttpContext.Session.GetString("Username");

            Console.WriteLine($"[POST] Username from session: {Username}");
            Console.WriteLine($"[POST] Action: {Action}, Topic: {TopicName}");

            if (string.IsNullOrEmpty(Action) || string.IsNullOrEmpty(TopicName) || string.IsNullOrEmpty(Username))
                return RedirectToPage();

            var client = _context.Clients
                .Include(c => c.Subscriptions)
                .FirstOrDefault(c => c.Username == Username);

            if (client == null)
                return RedirectToPage();

            if (Action == "subscribe")
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
            else if (Action == "unsubscribe")
            {
                var sub = client.Subscriptions.FirstOrDefault(s => s.Topic == TopicName);
                if (sub != null)
                {
                    _context.Subscriptions.Remove(sub);
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToPage();
        }
    }
}
