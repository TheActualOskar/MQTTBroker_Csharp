using System.Collections.Generic;

namespace MqttBroker.Messages
{
    public class ResolvedVirtualTopics
    {
        public byte[] Payload { get; }
        public List<string> VirtualTopics { get; }
        public string PayloadText { get; }

        public ResolvedVirtualTopics(byte[] payload, List<string> virtualTopics, string payloadText)
        {
            Payload = payload;
            VirtualTopics = virtualTopics;
            PayloadText = payloadText;
        }
    }
}
