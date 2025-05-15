using System.Collections.Generic;
using System.Threading.Tasks;
using MqttBroker.Messages;

namespace MqttBroker.Helpers
{
    public interface IVirtualTopicDefinitionProvider
    {
        Task<List<VirtualTopicDefinition>> GetActiveVirtualTopicsAsync();
    }
}
