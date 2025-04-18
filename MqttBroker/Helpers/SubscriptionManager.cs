using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;

namespace MqttBroker.Helpers
{
    public static class SubscriptionManager
    {
        private static readonly ConcurrentDictionary<string, List<NetworkStream>> topicSubscribers = new();

        public static void AddSubscriber(string topic, NetworkStream stream)
        {
            var list = topicSubscribers.GetOrAdd(topic, _ => new List<NetworkStream>());
            lock (list)
            {
                if (!list.Contains(stream))
                    list.Add(stream);
            }
        }

        public static List<NetworkStream> GetSubscribers(string topic)
        {
            return topicSubscribers.TryGetValue(topic, out var list) ? list : new List<NetworkStream>();
        }
    }
}
