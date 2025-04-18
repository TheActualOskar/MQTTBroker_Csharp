using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MqttBroker.Web.Pages
{
    public class AdminDashboardModel : PageModel
    {
        public string Username { get; set; }

        public void OnGet()
        {
            Username = HttpContext.Session.GetString("Username");
        }
    }
}
