using Akka.Actor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MqttBroker.Messages;
using MqttBroker.Web.Services;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MqttBroker.Web.Pages
{
    public class AdminDashboardModel : PageModel
    {
        private readonly MetadataService _metadataService;
        private readonly IActorRef _eventNotifier;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IVirtualTopicValidatorActorRef _validatorActorRef;

        public AdminDashboardModel(
            MetadataService metadataService,
            IActorRef eventNotifier,
            IHttpClientFactory clientFactory,
            IVirtualTopicValidatorActorRef validatorActorRef)
        {
            _metadataService = metadataService;
            _eventNotifier = eventNotifier;
            _clientFactory = clientFactory;
            _validatorActorRef = validatorActorRef;
        }

        public string Username { get; set; }
        public List<string> Topics { get; set; } = new();
        public List<string> AvailableBuildings { get; set; } = new();
        public List<string> AvailableRooms { get; set; } = new();

        [BindProperty] public string DeviceId { get; set; }
        [BindProperty] public string SensorType { get; set; }
        [BindProperty] public string SelectedBuilding { get; set; }
        [BindProperty] public string SelectedRoom { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(Username))
                return RedirectToPage("/Login");

            Topics = await _metadataService.GetAllTopicsAsync();
            AvailableBuildings = await _metadataService.GetAllBuildingsAsync();
            AvailableRooms = new(); // empty until building is selected
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(Username))
                return RedirectToPage("/Login");

            Topics = await _metadataService.GetAllTopicsAsync();
            AvailableBuildings = await _metadataService.GetAllBuildingsAsync();

            if (!string.IsNullOrEmpty(SelectedBuilding) && string.IsNullOrWhiteSpace(SensorType))
            {
                AvailableRooms = await _metadataService.GetRoomsInBuildingAsync(SelectedBuilding);
                return Page();
            }

            AvailableRooms = await _metadataService.GetRoomsInBuildingAsync(SelectedBuilding);

            if (string.IsNullOrWhiteSpace(SensorType) || string.IsNullOrWhiteSpace(SelectedRoom))
            {
                TempData["Message"] = "Sensor type and room are required.";
                return Page();
            }

            string datastreamId = $"{SelectedBuilding}-{SelectedRoom}-{SensorType.ToLower()}";

            // ? Send creation request to the broker
            _validatorActorRef.Ref.Tell(new CreateDatastreamMessage(
                datastreamId,
                SelectedBuilding,
                SelectedRoom,
                SensorType
            ));

            TempData["Message"] = $"Creation request for datastream '{datastreamId}' sent to broker.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRefreshVirtualTopics()
        {
            var client = _clientFactory.CreateClient();
            var response = await client.PostAsync("https://localhost:7086/admin/refresh-virtual-topics", null);

            TempData["Message"] = response.IsSuccessStatusCode
                ? "Refresh triggered successfully."
                : "Failed to trigger refresh.";

            return Page();
        }

        public IActionResult OnPostLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Login");
        }
    }
}
