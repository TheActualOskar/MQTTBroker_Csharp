using System.Net.Sockets;

namespace MqttBroker.Messages
{
    public class InboundMqttPacket
    {
        public MqttPacketType PacketType { get; }
        public byte[] RawBytes { get; }
        public NetworkStream Stream { get; }

        public InboundMqttPacket(MqttPacketType type, byte[] rawBytes, NetworkStream stream)
        {
            PacketType = type;
            RawBytes = rawBytes;
            Stream = stream;
        }
    }
}
