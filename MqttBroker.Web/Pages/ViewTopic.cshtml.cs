using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace MqttBroker.Web.Pages
{
    public class ViewTopicModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string Topic { get; set; }

        public List<string> Messages { get; set; } = new();

        public void OnGet()
        {
            // ?? For now, we simulate or leave empty until real data
            // ?? Later this will query live data from MQTT broker
            Messages = new List<string>
            {
                $"Sample data for topic: {Topic}",
                "Message 1...",
                "Message 2...",
                "Message 3..."
            };
        }
    }
}
