using Akka.Actor;
using Akka.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MqttBroker.Actors;
using MqttBroker.Database;
using Neo4j.Driver;

class Program
{
    static async Task Main(string[] args)
    {
        using var system = ActorSystem.Create("MqttBrokerSystem");

        var connectionString = "Host=localhost;Port=5432;Database=mqttbrokerdb;Username=postgres;Password=1234";

        //  Manually create DbContextOptions
        var optionsBuilder = new DbContextOptionsBuilder<BrokerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        var dbContext = new BrokerDbContext(optionsBuilder.Options);

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var eventNotifier = system.ActorOf(EventNotifier.Props(dbContext, config), "EventNotifier");

        //  Setup Neo4j Driver
        var neo4jDriver = GraphDatabase.Driver(
            "bolt://localhost:7687",
            AuthTokens.Basic("neo4j", "12345678")
        );

        var webSocketServer = system.ActorOf(WebSocketServerActor.Props(), "WebSocketServer");
        var messageRouter = system.ActorOf(Props.Create(() => new MessageRouter()), "MessageRouter");

        //  Inject MessageRouter, WebSocketServer, and Neo4j Driver
        var publishHandler = system.ActorOf(
            Props.Create(() => new PublishHandler(messageRouter, webSocketServer, neo4jDriver)),
            "PublishHandler"
        );

        var subscribeHandler = system.ActorOf(Props.Create(() => new SubscribeHandler(dbContext)), "SubscribeHandler");
        var connectHandler = system.ActorOf(Props.Create(() => new ConnectHandler()), "ConnectHandler");

        var packageListener = system.ActorOf(
            PackageListener.Props(connectHandler, publishHandler, subscribeHandler),
            "PackageListener"
        );

        // Keeps the app running forever
        await Task.Delay(-1);
    }
}
