using Microsoft.AspNetCore.Mvc;
using Akka.Actor;
using MqttBroker.Messages;
using MqttBroker.Web.Services;

namespace MqttBroker.Web.Controllers
{
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly IActorRef _validatorActor;

        public AdminController(IVirtualTopicValidatorActorRef validatorWrapper)
        {
            _validatorActor = validatorWrapper.Ref;
        }

        [HttpPost("refresh-virtual-topics")]
        public IActionResult RefreshVirtualTopics()
        {
            _validatorActor.Tell(new ForceFullDatastreamRescan());
            return Ok("Rescan triggered");
        }
    }
}
