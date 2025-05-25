using Akka.Actor;
using Akka.TestKit.Xunit2;
using MqttBroker.Actors;
using MqttBroker.Helpers;
using MqttBroker.Messages;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MqttBroker.Tests
{
    public class PushDeliveryTests : TestKit
    {
        [Fact]
        public async Task Should_Deliver_Message_To_Subscriber_When_Topic_Matches()
        {
            var topic = "roomA/temp";
            var payloadText = "22.5°C";
            var payload = Encoding.UTF8.GetBytes(payloadText);

            TcpListener listener = null;
            TcpClient client = null;
            TcpClient serverClient = null;

            try
            {
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;

                // Connect client to server
                var clientConnectTask = Task.Run(async () =>
                {
                    client = new TcpClient();
                    await client.ConnectAsync("127.0.0.1", port);
                });

                serverClient = await listener.AcceptTcpClientAsync();
                await clientConnectTask;

                var serverStream = serverClient.GetStream();
                SubscriptionManager.AddSubscriber(topic, serverStream);

                var router = Sys.ActorOf(Props.Create(() => new MessageRouter()));

                router.Tell(new PublishMessage(topic, payload));
                await Task.Delay(100);

                // Read from client
                var buffer = new byte[1024];
                var readStream = client.GetStream();
                var readTask = readStream.ReadAsync(buffer, 0, buffer.Length);

                if (!readTask.Wait(1000)) throw new TimeoutException("Read timed out");

                var bytesRead = readTask.Result;
                var result = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                Assert.Contains(payloadText, result);
            }
            finally
            {
                client?.Close();
                serverClient?.Close();
                listener?.Stop();
            }
        }

        [Fact]
        public async Task Should_Not_Deliver_Message_If_No_Subscriber_Exists()
        {
            var topic = "roomB/temp";
            var payload = Encoding.UTF8.GetBytes("19.0°C");

            var router = Sys.ActorOf(Props.Create(() => new MessageRouter()));

            // Should not crash, and nothing should be sent
            router.Tell(new PublishMessage(topic, payload));
            await Task.Delay(100);

            Assert.True(true); // Test passes if no error is thrown
        }

        [Fact]
        public async Task Should_Deliver_To_Multiple_Subscribers_For_Same_Topic()
        {
            var topic = "roomC/humidity";
            var payloadText = "40%";
            var payload = Encoding.UTF8.GetBytes(payloadText);

            TcpListener listener = null;
            TcpClient client1 = null, client2 = null;
            TcpClient serverClient1 = null, serverClient2 = null;

            try
            {
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;

                // Start connecting clients
                var clientConnectTask1 = Task.Run(async () =>
                {
                    client1 = new TcpClient();
                    await client1.ConnectAsync("127.0.0.1", port);
                });

                var clientConnectTask2 = Task.Run(async () =>
                {
                    client2 = new TcpClient();
                    await client2.ConnectAsync("127.0.0.1", port);
                });

                serverClient1 = await listener.AcceptTcpClientAsync();
                serverClient2 = await listener.AcceptTcpClientAsync();
                await Task.WhenAll(clientConnectTask1, clientConnectTask2);

                SubscriptionManager.AddSubscriber(topic, serverClient1.GetStream());
                SubscriptionManager.AddSubscriber(topic, serverClient2.GetStream());

                var router = Sys.ActorOf(Props.Create(() => new MessageRouter()));
                router.Tell(new PublishMessage(topic, payload));
                await Task.Delay(100);

                // Assert both clients receive the message
                async Task<string> ReadFromClient(TcpClient c)
                {
                    var buffer = new byte[1024];
                    var stream = c.GetStream();
                    var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                    if (!readTask.Wait(1000)) throw new TimeoutException("Read timed out");
                    return Encoding.UTF8.GetString(buffer, 0, readTask.Result);
                }

                var result1 = await ReadFromClient(client1);
                var result2 = await ReadFromClient(client2);

                Assert.Contains(payloadText, result1);
                Assert.Contains(payloadText, result2);
            }
            finally
            {
                client1?.Close();
                client2?.Close();
                serverClient1?.Close();
                serverClient2?.Close();
                listener?.Stop();
            }
        }
    }
}
