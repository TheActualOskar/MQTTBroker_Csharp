using Akka.Actor;

namespace MqttBroker.Web.Services
{
    public interface IVirtualTopicValidatorActorRef
    {
        IActorRef Ref { get; }
    }
}
