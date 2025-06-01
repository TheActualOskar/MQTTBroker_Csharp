using Akka.Actor;
using MqttBroker.Database;
using MqttBroker.Messages;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace MqttBroker.Actors
{
    public class EventNotifier : ReceiveActor
    {
        private readonly BrokerDbContext _dbContext;

        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;

        public static Props Props(BrokerDbContext dbContext, IConfiguration config) =>
            Akka.Actor.Props.Create(() => new EventNotifier(dbContext, config));

        public EventNotifier(BrokerDbContext dbContext, IConfiguration config)
        {
            _dbContext = dbContext;

            // Load SMTP config from appsettings
            _smtpHost = config["SmtpSettings:Host"];
            _smtpPort = int.Parse(config["SmtpSettings:Port"]);
            _smtpUser = config["SmtpSettings:User"];
            _smtpPassword = config["SmtpSettings:Password"];
            _fromEmail = config["SmtpSettings:From"];

            Receive<NewTopicCreated>(msg => HandleNewTopic(msg.TopicName));
            Receive<VirtualTopicBatchUpdate>(HandleBatchUpdate);

        }

        private void HandleNewTopic(string newTopic)
        {
            var subscribers = (
    from client in _dbContext.Clients
    join link in _dbContext.ClientNamedSubscriptions on client.Id equals link.ClientId
    join namedSub in _dbContext.NamedSubscriptions on link.NamedSubscriptionId equals namedSub.Id
    where newTopic.StartsWith(namedSub.TopicName)
    select client
).Distinct().ToList();


            foreach (var client in subscribers)
            {
                SendEmail(
                    to: client.Email,
                    subject: "New Topic Available!",
                    body: $"A new topic '{newTopic}' was added under your subscription."
                );
            }

            Console.WriteLine($"📬 Notified {subscribers.Count} clients about topic '{newTopic}'");
        }

        private void SendEmail(string to, string subject, string body)
        {
            try
            {
                using var smtp = new SmtpClient(_smtpHost)
                {
                    Port = _smtpPort,
                    Credentials = new NetworkCredential(_smtpUser, _smtpPassword),
                    EnableSsl = true
                };

                var message = new MailMessage(_fromEmail, to, subject, body);
                smtp.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email to {to}: {ex.Message}");
            }
        }
        private void HandleBatchUpdate(VirtualTopicBatchUpdate update)
        {
            foreach (var kvp in update.TopicToStreamIds)
            {
                var topic = kvp.Key;
                var affectedStreamIds = kvp.Value;

                var subscribers = _dbContext.Clients
                    .Include(c => c.Subscriptions)
                    .Where(c => c.Subscriptions.Any(s => topic.StartsWith(s.Topic)))
                    .ToList();

                foreach (var client in subscribers)
                {
                    SendEmail(
                        to: client.Email,
                        subject: $"Virtual Topic Updated: {topic}",
                        body: $"The virtual topic '{topic}' was updated with {affectedStreamIds.Count} new stream(s)."
                    );
                }

                Console.WriteLine($"Notified {subscribers.Count} clients about batch update for '{topic}'");
            }
        }

    }
}
