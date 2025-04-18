using Akka.Actor;
using MqttBroker.Messages;
using System;

namespace MqttBroker.Actors
{
    public class ConnectHandler : ReceiveActor
    {
        public ConnectHandler()
        {
            Receive<MqttRawPacket>(packet =>
            {
                Console.WriteLine("🔌 ConnectHandler: Client sent CONNECT packet.");

                // TODO: Parse clientId and store session later
            });
        }
    }
}
