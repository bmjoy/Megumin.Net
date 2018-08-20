using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MMONET;
using MMONET.Message;
using MMONET.Message.TestMessage;
using MMONET.Remote;
using Network.Remote;

namespace RemoteTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ListenAsync();
            //CoommonListen();
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }

        private static void CoommonListen()
        {
            Socket listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.IPv6Any, 54321));
            bool IPV6 = Socket.OSSupportsIPv6;
            listener.Listen(10);
            var s = listener.Accept();
            Console.WriteLine("接收到连接");
        }

        private static async void ListenAsync()
        {
            ThreadPool.QueueUserWorkItem((A) =>
            {
                while (true)
                {
                    MainThreadScheduler.Update(0);
                    //Thread.Yield();
                }

            });
            IRemoteListener<TCPRemote> remote = new TCPRemoteListener(54321);
            Listen(remote);
        }

        static int connectCount;

        private static async void Listen(IRemoteListener<TCPRemote> remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var re = await remote.ListenAsync();
            Console.WriteLine($"接收到连接{connectCount++}");
            Listen(remote);

            //TestConnect(re);
            TestSpeed(re);
        }

        private static void TestConnect(IRemote re)
        {
            re.UserToken = connectCount;
            re.Receive(null);
            re.OnDisConnect += (er) =>
            {
                Console.WriteLine($"连接断开{re.UserToken}");
            };
        }

        private static void TestSpeed(IRemote re)
        {
            re.Receive(TestReceive);
        }

        private static async ValueTask<object> TestReceive(object message)
        {
            return await TestSpeed(message);
        }

        static int totalCount = 0;
        /// <summary>
        /// 性能测试
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async ValueTask<object> TestSpeed(object message)
        {
            totalCount++;
            switch (message)
            {
                case TestPacket1 packet1:
                    Console.WriteLine($"接收消息{nameof(TestPacket1)}--{packet1.Value}------总消息数{totalCount}");
                    return null;
                case TestPacket2 packet2:
                    Console.WriteLine($"接收消息{nameof(TestPacket2)}--{packet2.Value}");
                    return new TestPacket2 { Value = packet2.Value };
                default:
                    break;
            }
            return null;
        }
    }
}
