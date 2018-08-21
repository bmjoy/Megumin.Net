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
                CoolDownTime coolDown = new CoolDownTime() {  MinDelta = TimeSpan.FromSeconds(30) };
                while (true)
                {
                    MainThreadScheduler.Update(0);
                    //Thread.Sleep(1);
                    if (coolDown)
                    {
                        GC.Collect();
                    }
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
            re.Receiver = MessagePipline.TestReceiver;
        }

        private static void TestConnect(IRemote re)
        {
            re.UserToken = connectCount;
            re.OnDisConnect += (er) =>
            {
                Console.WriteLine($"连接断开{re.UserToken}");
            };
        }
    }
}
