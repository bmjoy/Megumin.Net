using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MMONET;
using MMONET.Message;
using MMONET.Remote;
using MMONET.Remote.Test;
using Network.Remote;

namespace UnitFunc
{
    [TestClass]
    public class UnitTestRemote
    {
        [TestMethod]
        public void TestMethodTcpConnect()
        {
            CancellationTokenSource cancellation = new CancellationTokenSource();
            const int Port = 54321;
            StartTcpListen(Port, cancellation);

            List<IRemote> remotes = new List<IRemote>();
            for (int i = 0; i < 10; i++)
            {
                remotes.Add(new TCPRemote());
            }
            TestConnect(remotes, Port).Wait();
            cancellation.Cancel();
        }

        [TestMethod]
        public void TestTcpSend()
        {
            const int Port = 54322;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            PrepareEnvironment(cancellation);
            StartTcpListen(Port, cancellation);

            TCPRemote remote = new TCPRemote();

            remote.ConnectAsync(new IPEndPoint(IPAddress.Loopback, Port)).Wait();
            SafeRpcSendAsync(remote).Wait();
            RpcSendAsync(remote).Wait();
            cancellation.Cancel();
        }


        [TestMethod]
        public void TestUdpSend()
        {
            const int Port = 54323;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            PrepareEnvironment(cancellation);
            StartUdpListen(Port, cancellation);

            IRemote remote = new UDPRemote();

            remote.ConnectAsync(new IPEndPoint(IPAddress.Loopback, Port)).Wait();
            SafeRpcSendAsync(remote).Wait();
            RpcSendAsync(remote).Wait();
            cancellation.Cancel();
        }



























        private static void PrepareEnvironment(CancellationTokenSource cancellation)
        {
            MessageLUT.AddFormatter(typeof(TestPacket1), 1000, (Seiralizer<TestPacket1>)TestPacket1.S, TestPacket1.D);
            MessageLUT.AddFormatter(typeof(TestPacket2), 1001, (Seiralizer<TestPacket2>)TestPacket2.S, TestPacket2.D);

            ThreadPool.QueueUserWorkItem((A) =>
            {
                while (!cancellation.Token.IsCancellationRequested)
                {
                    MainThreadScheduler.Update(0);
                    Thread.Yield();
                }
            });
            Task.Delay(50).Wait();
        }

        private static void StartTcpListen(int port, CancellationTokenSource cancellation)
        {
            ThreadPool.QueueUserWorkItem(async state =>
            {
                TCPRemoteListener listener = new TCPRemoteListener(port);
                while (!cancellation.Token.IsCancellationRequested)
                {
                    var r = await listener.ListenAsync();
                    r.Receive(Receive);
                }
            });
            Task.Delay(50).Wait();
        }

        private static void StartUdpListen(int port, CancellationTokenSource cancellation)
        {
            ThreadPool.QueueUserWorkItem(async state =>
            {
                UDPRemoteListener listener = new UDPRemoteListener(port);
                while (!cancellation.Token.IsCancellationRequested)
                {
                    var r = await listener.ListenAsync();
                    r.Receive(Receive);
                }
            });
            Task.Delay(50).Wait();
        }

        private static async ValueTask<dynamic> Receive(dynamic message)
        {
            switch (message)
            {
                case TestPacket1 packet1:
                    Console.WriteLine($"接收消息{nameof(TestPacket1)}--{packet1.Value}");
                    return null;
                case TestPacket2 packet2:
                    Console.WriteLine($"接收消息{nameof(TestPacket2)}--{packet2.Value}");
                    return new TestPacket2 { Value = packet2.Value };
                default:
                    break;
            }
            return null;
        }

        async Task TestConnect(IList<IRemote> remotes,int port)
        {
            foreach (var item in remotes)
            {
                var res = await item.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));
                Assert.AreEqual(null, res);
            }
            return;
        }

        private static async Task SafeRpcSendAsync(IRemote remote)
        {
            TestPacket2 packet2 = new TestPacket2() { Value = new Random().Next() };
            var res = await remote.SafeRpcSendAsync<TestPacket2>(packet2);
            Assert.AreEqual(packet2.Value, res.Value);
        }

        private static async Task RpcSendAsync(IRemote remote)
        {
            TestPacket2 packet2 = new TestPacket2() { Value = new Random().Next() };
            var (result, exception) = await remote.RpcSendAsync<TestPacket2>(packet2);
            Assert.AreEqual(null, exception);
            Assert.AreEqual(packet2.Value, result.Value);
        }
    }
}
