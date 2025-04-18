using Microsoft.EntityFrameworkCore;
using MqttBroker.Models;

namespace MqttBroker.Database
{
    public class BrokerDbContext : DbContext
    {
        public DbSet<Client> Clients { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }

        public BrokerDbContext(DbContextOptions<BrokerDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Client>()
                .HasMany(c => c.Subscriptions)
                .WithOne(s => s.Client)
                .HasForeignKey(s => s.ClientId);
        }
    }
}
