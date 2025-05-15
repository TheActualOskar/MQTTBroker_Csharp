using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttBroker.Messages
{
    public class VirtualTopicDefinition
    {
        public string Name { get; set; }
        public List<string> ExpectedLabels { get; set; }
    }
}
