using Akka.Actor;
using Fleck;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MqttBroker.Actors
{
    public class WebSocketServerActor : ReceiveActor
    {
        private readonly WebSocketServer _server;
        private readonly ConcurrentDictionary<string, List<IWebSocketConnection>> _topicConnections;

        public WebSocketServerActor()
        {
            _topicConnections = new ConcurrentDictionary<string, List<IWebSocketConnection>>();

            _server = new WebSocketServer("ws://0.0.0.0:9001");
            _server.Start(socket =>
            {
                string subscribedTopic = null;

                socket.OnOpen = () => Console.WriteLine($"🌐 WebSocket client connected: {socket.ConnectionInfo.ClientIpAddress}");

                socket.OnClose = () =>
                {
                    if (!string.IsNullOrEmpty(subscribedTopic) && _topicConnections.ContainsKey(subscribedTopic))
                    {
                        _topicConnections[subscribedTopic].Remove(socket);
                        Console.WriteLine($"❌ WebSocket client unsubscribed from '{subscribedTopic}'");
                    }
                };

                socket.OnMessage = message =>
                {
                    // First message = topic name to subscribe to
                    subscribedTopic = message;

                    _topicConnections.AddOrUpdate(subscribedTopic,
                        _ => new List<IWebSocketConnection> { socket },
                        (_, list) =>
                        {
                            list.Add(socket);
                            return list;
                        });

                    Console.WriteLine($"🔗 WebSocket client subscribed to: {subscribedTopic}");
                };
            });

            // Handler to receive published messages and push to WebSocket clients
            Receive<PublishToWebSocket>(msg =>
            {
                if (_topicConnections.TryGetValue(msg.Topic, out var connections))
                {
                    foreach (var conn in connections)
                    {
                        if (conn.IsAvailable)
                        {
                            conn.Send(msg.Payload);
                        }
                    }
                }
            });
        }

        protected override void PostStop()
        {
            _server.Dispose();
            base.PostStop();
        }

        public static Props Props() => Akka.Actor.Props.Create(() => new WebSocketServerActor());
    }

    public class PublishToWebSocket
    {
        public string Topic { get; }
        public string Payload { get; }

        public PublishToWebSocket(string topic, string payload)
        {
            Topic = topic;
            Payload = payload;
        }
    }
}
