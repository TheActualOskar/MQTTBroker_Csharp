using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttBroker.Messages
{
    public class CreateDatastreamMessage
    {
        public string StreamId { get; }
        public string Building { get; }
        public string Room { get; }
        public string SensorType { get; }

        public CreateDatastreamMessage(string streamId, string building, string room, string sensorType)
        {
            StreamId = streamId;
            Building = building;
            Room = room;
            SensorType = sensorType;
        }
    }

}
