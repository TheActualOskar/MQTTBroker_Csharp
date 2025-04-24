using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttBroker.Messages
{
    public class NewTopicCreated
    {
        public string TopicName { get; }

        public NewTopicCreated(string topicName)
        {
            TopicName = topicName;
        }
    }
}

