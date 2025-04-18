using Akka.Actor;

namespace MqttBroker.Actors
{
    public class EventNotifier : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new EventNotifier());

        public EventNotifier()
        {
            // Setup notification logic later
        }
    }
}
