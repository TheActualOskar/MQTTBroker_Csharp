using Akka.Actor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MqttBroker.Actors;
using MqttBroker.Messages;
using MqttBroker.Web.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;

namespace MqttBroker.Web.Pages
{
    public class AdminDashboardModel : PageModel
    {
        private readonly MetadataService _metadataService;
        private readonly IActorRef _eventNotifier;
        private readonly IHttpClientFactory _clientFactory;

        public AdminDashboardModel(MetadataService metadataService, IActorRef eventNotifier, IHttpClientFactory clientFactory)
        {
            _metadataService = metadataService;
            _eventNotifier = eventNotifier;
            _clientFactory = clientFactory;
        }

        public string Username { get; set; }
        public List<string> Topics { get; set; } = new();

        [BindProperty] public string DeviceId { get; set; }
        [BindProperty] public string DatastreamId { get; set; }
        [BindProperty] public string Unit { get; set; }
        [BindProperty] public string Frequency { get; set; }
        [BindProperty] public string TopicName { get; set; }
        [BindProperty] public string Labels { get; set; }


        public async Task<IActionResult> OnGetAsync()
        {
            Username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(Username))
                return RedirectToPage("/Login");

            Topics = await _metadataService.GetAllTopicsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(Username))
                return RedirectToPage("/Login");

            // Create the new datastream and topic in Neo4j
            await _metadataService.CreateDatastreamAsync(DeviceId, DatastreamId, Unit, Frequency, TopicName);

            // Notify the EventNotifier actor for subscription alerting
            _eventNotifier.Tell(new NewTopicCreated(TopicName));

            // Refresh topic list and reload page
            Topics = await _metadataService.GetAllTopicsAsync();
            return RedirectToPage();
        }

        
        public async Task<IActionResult> OnPostRefreshVirtualTopics()
        {
            var client = _clientFactory.CreateClient();
            var response = await client.PostAsync("https://localhost:7086/admin/refresh-virtual-topics", null);

            if (response.IsSuccessStatusCode)
            {
                TempData["Message"] = "Refresh triggered successfully.";
            }
            else
            {
                TempData["Message"] = "Failed to trigger refresh.";
            }

            return Page();
        }
        public IActionResult OnPostLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Login");
        }

    }
}
