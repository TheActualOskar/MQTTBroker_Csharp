using Akka.Actor;

namespace MqttBroker.Web.Services
{
    public class VirtualTopicValidatorActorRef : IVirtualTopicValidatorActorRef
    {
        public IActorRef Ref { get; }

        public VirtualTopicValidatorActorRef(IActorRef actorRef)
        {
            Ref = actorRef;
        }
    }
}
