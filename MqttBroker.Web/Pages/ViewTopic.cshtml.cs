using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace MqttBroker.Web.Pages
{
    public class ViewTopicModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string Topic { get; set; }

        public void OnGet() { }
    }
}
