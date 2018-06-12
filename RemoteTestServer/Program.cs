using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MMONET;
using MMONET.Sockets;
using MMONET.Sockets.Test;

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
            Remote.AddFormatterLookUpTabal(new TestLut());
            ThreadPool.QueueUserWorkItem((A) =>
            {
                while (true)
                {
                    MainThreadScheduler.Update(0);
                    //Thread.Yield();
                }

            });
            Remote remote = new Remote();
            Listen(remote);
        }

        static int connectCount;

        private static async void Listen(Remote remote)
        {
            /// 最近一次测试本机同时运行客户端服务器16000+连接时，服务器拒绝连接。
            var re = await remote.ListenAsync(54321);
            Console.WriteLine($"接收到连接{connectCount++}");
            Listen(remote);

            //TestConnect(re);
            TestSpeed(re);
        }

        private static void TestConnect(Remote re)
        {
            re.InstanceID = connectCount;
            re.ReceiveAsync(null);
            re.OnDisConnect += (ch, er) =>
            {
                Console.WriteLine($"连接断开{re.InstanceID}");
            };
        }

        private static void TestSpeed(Remote re)
        {
            re.ReceiveAsync(TestReceive);
        }

        private static async ValueTask<dynamic> TestReceive(dynamic message)
        {
            return await TestSpeed(message);
        }

        static int totalCount = 0;
        /// <summary>
        /// 性能测试
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async ValueTask<dynamic> TestSpeed(dynamic message)
        {
            totalCount++;
            switch (message)
            {
                case TestPacket1 packet1:
                    Console.WriteLine($"接收消息{nameof(TestPacket1)}--{packet1.Value}------总消息数{totalCount}");
                    //return null;
                    //return new Struct111111111111();
                    return new TestPacket2 { Value = packet1.Value };
                case TestPacket2 packet2:
                    Console.WriteLine($"接收消息{nameof(TestPacket2)}--{packet2.Value}");
                    return null;
                default:
                    break;
            }
            return null;
        }
    }

    public struct Struct111111111111
    {

    }
}
