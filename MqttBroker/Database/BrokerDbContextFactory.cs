using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MqttBroker.Database
{
    public class BrokerDbContextFactory : IDesignTimeDbContextFactory<BrokerDbContext>
    {
        public BrokerDbContext CreateDbContext(string[] args)
        {
            var connectionString = "Host=localhost;Port=5432;Database=mqttbrokerdb;Username=postgres;Password=1234";

            var optionsBuilder = new DbContextOptionsBuilder<BrokerDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new BrokerDbContext(optionsBuilder.Options);
        }
    }
}
