using Akka.Actor;
using Microsoft.EntityFrameworkCore;
using MqttBroker.Actors;
using MqttBroker.Database;

class Program
{
    static async Task Main(string[] args)
    {
        using var system = ActorSystem.Create("MqttBrokerSystem");

        var connectionString = "Host=localhost;Port=5432;Database=mqttbrokerdb;Username=postgres;Password=1234";

        // ✅ Manually create DbContextOptions
        var optionsBuilder = new DbContextOptionsBuilder<BrokerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        var dbContext = new BrokerDbContext(optionsBuilder.Options);

        var messageRouter = system.ActorOf(Props.Create(() => new MessageRouter()), "MessageRouter");
        var publishHandler = system.ActorOf(Props.Create(() => new PublishHandler(messageRouter)), "PublishHandler");
        var subscribeHandler = system.ActorOf(Props.Create(() => new SubscribeHandler(dbContext)), "SubscribeHandler");

        var connectHandler = system.ActorOf(Props.Create(() => new ConnectHandler()), "ConnectHandler");

        var packageListener = system.ActorOf(
            PackageListener.Props(connectHandler, publishHandler, subscribeHandler),
            "PackageListener");

        // 🔄 Keeps the app running forever
        await Task.Delay(-1);
    }
}
