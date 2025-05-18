using System.Security.Cryptography;
using System.Text;
using MqttBroker.Database;
using MqttBroker.Models;
using Microsoft.EntityFrameworkCore;
using Akka.Actor;
using MqttBroker.Actors;
using Neo4j.Driver;
using MqttBroker.Helpers;
using MqttBroker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSession();
builder.Services.AddControllers(); // ? Add Controller Support

// Graph database -> my topics
var config = builder.Configuration.GetSection("Neo4j");
builder.Services.AddSingleton(new MqttBroker.Web.Services.MetadataService(
    config["Uri"],
    config["Username"],
    config["Password"]
));

builder.Services.AddSingleton<Neo4j.Driver.IDriver>(provider =>
    GraphDatabase.Driver(
        config["Uri"],
        AuthTokens.Basic(config["Username"], config["Password"])
    )
);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<MqttBroker.Web.Services.SubscriptionService>();

// Client database -> users and subscriptions
builder.Services.AddDbContext<BrokerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Actor System
var actorSystem = ActorSystem.Create("MqttBrokerWebSystem");

// Register EventNotifier Actor
builder.Services.AddSingleton<IActorRef>(provider =>
{
    var dbOptions = new DbContextOptionsBuilder<BrokerDbContext>()
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .Options;

    var dbContext = new BrokerDbContext(dbOptions);
    var config = builder.Configuration;

    return actorSystem.ActorOf(EventNotifier.Props(dbContext, config), "EventNotifierWeb");
});

// Register VirtualTopicValidator Actor (corrected)
builder.Services.AddSingleton<IVirtualTopicValidatorActorRef>(provider =>
{
    var neo4jDriver = provider.GetRequiredService<IDriver>();
    var topicProvider = new Neo4jVirtualTopicDefinitionProvider(neo4jDriver);

    var actorRef = actorSystem.ActorOf(
        Props.Create(() => new VirtualTopicValidatorActor(neo4jDriver, topicProvider)),
        "VirtualTopicValidator"
    );

    return new VirtualTopicValidatorActorRef(actorRef);
});




var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers(); // ? Map Controllers Here

// Ensure admin and test-client exist in the DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BrokerDbContext>();

    if (!db.Clients.Any(c => c.Username == "admin"))
    {
        using var sha256 = SHA256.Create();
        var hashedPassword = Convert.ToBase64String(
            sha256.ComputeHash(Encoding.UTF8.GetBytes("admin123")));

        db.Clients.Add(new Client
        {
            ClientId = Guid.NewGuid().ToString(),
            Username = "admin",
            PasswordHash = hashedPassword,
            Role = "admin"
        });

        db.SaveChanges();
    }

    if (!db.Clients.Any(c => c.Username == "test-client"))
    {
        using var sha256 = SHA256.Create();
        var hashedPassword = Convert.ToBase64String(
            sha256.ComputeHash(Encoding.UTF8.GetBytes("client123")));

        db.Clients.Add(new Client
        {
            ClientId = Guid.NewGuid().ToString(),
            Username = "test-client",
            PasswordHash = hashedPassword,
            Role = "client"
        });

        db.SaveChanges();
    }
}

app.Run();
