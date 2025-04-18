namespace MqttBroker.Messages
{
    public enum MqttPacketType
    {
        Connect = 1,
        Publish = 3,
        Subscribe = 8,
        Unknown = 255
    }

    public class MqttRawPacket
    {
        public MqttPacketType PacketType { get; }
        public byte[] RawBytes { get; }

        public MqttRawPacket(MqttPacketType type, byte[] rawBytes)
        {
            PacketType = type;
            RawBytes = rawBytes;
        }
    }
}
