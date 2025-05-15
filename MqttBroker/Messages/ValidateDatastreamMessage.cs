using System.Collections.Generic;

namespace MqttBroker.Messages
{
    public record ValidateDatastreamMessage(string StreamId, List<string> Labels);
}
