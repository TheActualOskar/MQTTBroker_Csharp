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
    public class SmartFilterDetailsModel : PageModel
    {
        private readonly BrokerDbContext _db;
        private readonly MetadataService _metadataService;

        public SmartFilterDetailsModel(BrokerDbContext db, MetadataService metadataService)
        {
            _db = db;
            _metadataService = metadataService;
        }

        public NamedSubscription Subscription { get; set; }
        public List<CreateTopicModel.StreamPreview> Streams { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var username = HttpContext.Session.GetString("Username");
            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Username == username);
            if (client == null) return RedirectToPage("/Login");

            Subscription = await _db.NamedSubscriptions
                .Include(s => s.SubscribedClients)
                .FirstOrDefaultAsync(s => s.Id == id && s.SubscribedClients.Any(c => c.ClientId == client.Id));

            if (Subscription == null)
                return NotFound();

            // Execute the Cypher query just for view purposes (note: not re-evaluation for live logic)
            Streams = await _metadataService.QueryStreamsByCypherAsync(Subscription.CypherQuery);

            return Page();
        }
    }
}
