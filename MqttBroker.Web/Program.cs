using System.Security.Cryptography;
using System.Text;
using MqttBroker.Database;
using MqttBroker.Models;


using Microsoft.EntityFrameworkCore;
using Akka.Actor;
using MqttBroker.Actors;
using Neo4j.Driver;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSession();


//graph database -> my topics
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


//client database -> users and subscriptions
builder.Services.AddDbContext<BrokerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));




builder.Services.AddSingleton<IActorRef>(provider =>
{
    var system = ActorSystem.Create("MqttBrokerWebSystem");

    var dbOptions = new DbContextOptionsBuilder<BrokerDbContext>()
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .Options;

    var dbContext = new BrokerDbContext(dbOptions);
    var config = builder.Configuration; // ? Get IConfiguration

    return system.ActorOf(EventNotifier.Props(dbContext, config), "EventNotifierWeb");
});




var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapRazorPages();



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
