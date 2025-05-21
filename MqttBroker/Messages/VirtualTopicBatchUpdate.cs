using System.Collections.Generic;

namespace MqttBroker.Messages
{
    public class VirtualTopicBatchUpdate
    {
        public Dictionary<string, List<string>> TopicToStreamIds { get; }

        public VirtualTopicBatchUpdate(Dictionary<string, List<string>> topicToStreamIds)
        {
            TopicToStreamIds = topicToStreamIds;
        }
    }
}
