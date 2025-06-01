using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MqttBroker.Database;
using MqttBroker.Models;
using MqttBroker.Web.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttBroker.Web.Pages
{
    public class CreateTopicModel : PageModel
    {
        private readonly MetadataService _metadataService;
        private readonly BrokerDbContext _db;

        public CreateTopicModel(MetadataService metadataService, BrokerDbContext db)
        {
            _metadataService = metadataService;
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty]
        public FilterModel Filter { get; set; } = new();

        public PreviewResultModel? PreviewResults { get; set; }
        public NamedSubscription? Result { get; set; }

        public List<string> AvailableBuildings { get; set; } = new();
        public List<string> AvailableRooms { get; set; } = new();
        public List<string> AvailableSensorTypes { get; set; } = new();

        public class InputModel
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        public class FilterModel
        {
            public List<string> Building { get; set; } = new();
            public List<string> Room { get; set; } = new();
            public List<string> SensorType { get; set; } = new();
            public bool ActiveOnly { get; set; }

            public string TempThresholdOperator { get; set; }
            public double? TempThresholdValue { get; set; }

            public string HumidityThresholdOperator { get; set; }
            public double? HumidityThresholdValue { get; set; }
        }

        public class PreviewResultModel
        {
            public int Total { get; set; }
            public int Active { get; set; }
            public int Inactive { get; set; }
            public List<StreamPreview> Streams { get; set; } = new();
        }

        public class StreamPreview
        {
            public string StreamId { get; set; }
            public string Type { get; set; }
            public string Location { get; set; }
            public string LastSeen { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDropdownOptionsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string action)
        {
            await LoadDropdownOptionsAsync();

            if (action == "preview")
            {
                var streams = await _metadataService.QueryStreamsByFilterAsync(Filter);

                PreviewResults = new PreviewResultModel
                {
                    Total = streams.Count,
                    Active = streams.Count(s => s.LastSeen.Contains("min")),
                    Inactive = streams.Count(s => !s.LastSeen.Contains("min")),
                    Streams = streams
                };

                return Page();
            }

            if (action == "create")
            {
                var username = HttpContext.Session.GetString("Username");
                var client = await _db.Clients.FirstOrDefaultAsync(c => c.Username == username);
                if (client == null) return RedirectToPage("/Login");

                var query = BuildCypherQueryFromFilter(Filter);
                var virtualTopicName = $"virtual/{client.Id}/{Input.Name.Replace(" ", "_")}";

                if (PreviewResults == null)
                {
                    var preview = await _metadataService.QueryStreamsByFilterAsync(Filter);
                    PreviewResults = new PreviewResultModel
                    {
                        Total = preview.Count,
                        Active = preview.Count(s => s.LastSeen.Contains("min")),
                        Inactive = preview.Count(s => !s.LastSeen.Contains("min")),
                        Streams = preview
                    };
                }

                var namedSubscription = new NamedSubscription
                {
                    Name = Input.Name ?? string.Empty,
                    Description = Input.Description ?? string.Empty,
                    CypherQuery = query,
                    TopicName = virtualTopicName,
                    CurrentMatchCount = PreviewResults.Streams.Count,
                    CreatedByClientId = client.Id,
                    CreatedAt = DateTime.UtcNow,
                    LastResultHash = string.Empty,
                    SubscribedClients = new List<ClientNamedSubscription>()
                };

                _db.NamedSubscriptions.Add(namedSubscription);
                await _db.SaveChangesAsync();

                _db.ClientNamedSubscriptions.Add(new ClientNamedSubscription
                {
                    ClientId = client.Id,
                    NamedSubscriptionId = namedSubscription.Id
                });

                await _db.SaveChangesAsync();

                var streamIds = PreviewResults.Streams.Select(s => s.StreamId).ToList();
                var expectedLabels = new List<string>();
                if (Filter.SensorType != null) expectedLabels.AddRange(Filter.SensorType);
                if (Filter.Room != null) expectedLabels.AddRange(Filter.Room);

                await _metadataService.CreateVirtualTopicFromSmartFilter(
                    virtualTopicName,
                    client.Id,
                    streamIds,
                    expectedLabels
                );

                Result = namedSubscription;
                return Page();
            }

            return Page();
        }

        private async Task LoadDropdownOptionsAsync()
        {
            AvailableBuildings = await _metadataService.GetAllBuildingsAsync();
            AvailableRooms = await _metadataService.GetAllRoomsAsync();
            AvailableSensorTypes = await _metadataService.GetAllSensorTypesAsync();
        }

        private string BuildCypherQueryFromFilter(FilterModel filter)
        {
            var query = new StringBuilder();
            query.AppendLine("MATCH (b:Building)-[:HAS_ROOM]->(r:Room)-[:HAS_DATASTREAM]->(s:Datastream)");
            query.AppendLine("WHERE 1=1");

            if (filter.Building.Any())
                query.AppendLine($"AND b.name IN {FormatList(filter.Building)}");

            if (filter.Room.Any())
                query.AppendLine($"AND r.name IN {FormatList(filter.Room)}");

            if (filter.SensorType.Any())
                query.AppendLine($"AND s.type IN {FormatList(filter.SensorType)}");

            if (filter.ActiveOnly)
                query.AppendLine("AND s.lastSeen > timestamp() - 5 * 60 * 1000");

            if (!string.IsNullOrEmpty(filter.TempThresholdOperator) && filter.TempThresholdValue.HasValue)
                query.AppendLine($"AND (s.type <> 'Temperature' OR s.value {filter.TempThresholdOperator} {filter.TempThresholdValue.Value})");

            if (!string.IsNullOrEmpty(filter.HumidityThresholdOperator) && filter.HumidityThresholdValue.HasValue)
                query.AppendLine($"AND (s.type <> 'Humidity' OR s.value {filter.HumidityThresholdOperator} {filter.HumidityThresholdValue.Value})");

            query.AppendLine("RETURN s");
            return query.ToString();
        }

        private string FormatList(IEnumerable<string> items)
        {
            return "[" + string.Join(", ", items.Select(i => $"'{i}'")) + "]";
        }
    }
}
