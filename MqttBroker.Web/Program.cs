using System.Security.Cryptography;
using System.Text;
using MqttBroker.Database;
using MqttBroker.Models;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDbContext<BrokerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

app.UseAuthorization();

app.MapRazorPages();

app.Run();

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
}
