using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MqttBroker.Web.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MqttBroker.Web.Pages
{
    public class AdminDashboardModel : PageModel
    {
        private readonly MetadataService _metadataService;

        public AdminDashboardModel(MetadataService metadataService)
        {
            _metadataService = metadataService;
        }

        public string Username { get; set; }
        public List<string> Topics { get; set; } = new();

        [BindProperty] public string DeviceId { get; set; }
        [BindProperty] public string DatastreamId { get; set; }
        [BindProperty] public string Unit { get; set; }
        [BindProperty] public string Frequency { get; set; }
        [BindProperty] public string TopicName { get; set; }

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

            await _metadataService.CreateDatastreamAsync(DeviceId, DatastreamId, Unit, Frequency, TopicName);
            Topics = await _metadataService.GetAllTopicsAsync();

            return RedirectToPage(); // Refresh the page
        }
    }
}
