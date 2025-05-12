using Akka.Actor;
using MqttBroker.Messages;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MqttBroker.Helpers;

namespace MqttBroker.Actors
{
    public class PackageListener : ReceiveActor
    {
        private readonly IActorRef _publishHandler;
        private readonly IActorRef _subscribeHandler;
        private readonly IActorRef _connectHandler;
        private TcpListener _listener;

        public PackageListener(IActorRef connectHandler, IActorRef publishHandler, IActorRef subscribeHandler)
        {
            _connectHandler = connectHandler;
            _publishHandler = publishHandler;
            _subscribeHandler = subscribeHandler;

            Receive<MqttRawPacket>(packet =>
            {
                switch (packet.PacketType)
                {
                    case MqttPacketType.Connect:
                        _connectHandler.Tell(packet);
                        break;
                    case MqttPacketType.Publish:
                        _publishHandler.Tell(packet);
                        break;
                    case MqttPacketType.Subscribe:
                        _subscribeHandler.Tell(packet);
                        break;
                    default:
                        Console.WriteLine($"❓ Unknown packet type: {packet.PacketType}");
                        break;
                }
            });

            ReceiveAsync<StartListening>(async _ =>
            {
                await StartTcpListener();
            });
        }

        protected override void PreStart()
        {
            // Automatically start listening when actor starts
            Self.Tell(new StartListening());
        }

        private async Task StartTcpListener()
        {
            _listener = new TcpListener(IPAddress.Any, 18883);
            _listener.Start();
            Console.WriteLine("📡 Listening for clients on port 18883...");

            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                Console.WriteLine($"[+] Client connected: {client.Client.RemoteEndPoint}");

                // Handle each client on its own task
                _ = HandleClient(client);
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            var stream = client.GetStream();
            var buffer = new byte[1024];

            while (client.Connected)
            {
                try
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("[-] Client disconnected: clean disconnect.");
                        break;
                    }

                    var packetData = buffer.Take(bytesRead).ToArray();

                    // Diagnostic Logging
                    Console.WriteLine($"[DEBUG] Raw packet ({bytesRead} bytes): {BitConverter.ToString(packetData)}");

                    if (bytesRead > 1)
                    {
                        int remainingLength = packetData[1];
                        Console.WriteLine($"[DEBUG] Remaining Length (from header): {remainingLength}");
                    }

                    var packetType = GetPacketType(packetData[0]);
                    Console.WriteLine($"[DEBUG] Packet Type Detected: {packetType}");

                    var packet = new InboundMqttPacket(packetType, packetData, stream);

                    switch (packet.PacketType)
                    {
                        case MqttPacketType.Connect:
                            Console.WriteLine("🔌 Received CONNECT — sending CONNACK...");
                            SendConnAck(stream);
                            break;

                        case MqttPacketType.Publish:
                            _publishHandler.Tell(new MqttRawPacket(packet.PacketType, packet.RawBytes));
                            break;

                        case MqttPacketType.Subscribe:
                            _subscribeHandler.Tell(new MqttRawPacket(packet.PacketType, packet.RawBytes));
                            _subscribeHandler.Tell(packet);
                            break;

                        default:
                            Console.WriteLine($"❓ Unknown packet type: {packet.PacketType}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error with client {client.Client?.RemoteEndPoint}: {ex.Message}");
                    break;
                }
            }

            client.Close();
            Console.WriteLine($"[-] Client disconnected.");
        }


        private void SendConnAck(NetworkStream stream)
        {
            try
            {
                // MQTT CONNACK packet:
                // Byte 1: 0x20 (type = 2 << 4)
                // Byte 2: 0x02 (remaining length = 2)
                // Byte 3: 0x00 (session present flag = 0)
                // Byte 4: 0x00 (connect return code = 0, success)

                var connack = new byte[] { 0x20, 0x02, 0x00, 0x00 };
                stream.Write(connack, 0, connack.Length);
                stream.Flush();

                Console.WriteLine("✅ CONNACK sent to client.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to send CONNACK: {ex.Message}");
            }
        }

        private MqttPacketType GetPacketType(byte firstByte)
        {
            int type = (firstByte & 0xF0) >> 4;

            return type switch
            {
                1 => MqttPacketType.Connect,
                3 => MqttPacketType.Publish,
                8 => MqttPacketType.Subscribe,
                _ => MqttPacketType.Unknown
            };
        }

        public static Props Props(IActorRef connectHandler, IActorRef publishHandler, IActorRef subscribeHandler)
        {
            return Akka.Actor.Props.Create(() => new PackageListener(connectHandler, publishHandler, subscribeHandler));
        }
    }

    public class StartListening { }
}
