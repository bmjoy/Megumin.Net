using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MMONET.Remote;
using System.Diagnostics;
using MMONET.Remote.Test;
using MMONET;
using MMONET.Message;
using Network.Remote;

namespace RemoteTestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            ConAsync();
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }

        static int MessageCount = 10000;
        static int RemoteCount = 100;
        private static async void ConAsync()
        {
            MessageLUT.AddFormatter(typeof(TestPacket1), 1000, (Seiralizer<TestPacket1>)TestPacket1.S, TestPacket1.D);
            MessageLUT.AddFormatter(typeof(TestPacket2), 1001, (Seiralizer<TestPacket2>)TestPacket2.S, TestPacket2.D);

            ThreadPool.QueueUserWorkItem((A) =>
            {
                while (true)
                {
                    MainThreadScheduler.Update(0);
                    //Thread.Yield();
                }

            });

            ///性能测试
            //TestSpeed();
            ///连接测试
            TestConnect();
        }


        #region 性能测试


        /// <summary>
        /// //峰值 12000 0000 字节每秒，平均 4~7千万字节每秒
        /// int MessageCount = 10000;
        /// int RemoteCount = 100;
        /// </summary>
        private static void TestSpeed()
        {
            for (int i = 0; i < RemoteCount; i++)
            {
                NewRemote(i);
            }
        }

        private static async void NewRemote(int clientIndex)
        {
            TCPRemote remote = new TCPRemote();
            var res = await remote.ConnectAsync(IPAddress.Loopback, 54321);
            if (res == null)
            {
                Console.WriteLine($"Remote{clientIndex}:Success");
            }
            else
            {
                throw res;
            }

            remote.Receive((new Receiver() { Index = clientIndex }).TestReceive);
            Stopwatch look1 = new Stopwatch();
            var msg = new TestPacket1 { Value = 0 };
            look1.Start();

            await Task.Run(() =>
            {
                for (int i = 0; i < MessageCount; i++)
                {
                    //Console.WriteLine($"Remote{clientIndex}:发送{nameof(Packet1)}=={i}");
                    msg.Value = i;
                    remote.SendAsync(msg);

                }
            });

            look1.Stop();

            Console.WriteLine($"Remote{clientIndex}: SendAsync{MessageCount}包 ------ 发送总时间: {look1.ElapsedMilliseconds}----- 平均每秒发送:{MessageCount * 1000 / (look1.ElapsedMilliseconds+1)}");

            var res2 = await remote.SafeRpcSendAsync<TestPacket2>(new TestPacket2() { Value = clientIndex });
            Console.WriteLine($"Rpc调用返回----------------------------------------- {res2.Value}");
            //Remote.BroadCastAsync(new Packet1 { Value = -99999 },remote);

            //var (Result, Excption) = await remote.SendAsync<Packet2>(new Packet1 { Value = 100 });
            //Console.WriteLine($"RPC接收消息{nameof(Packet2)}--{Result.Value}");
        }

        class Receiver
        {
            public int Index { get; set; }
            Stopwatch stopwatch = new Stopwatch();

            public async ValueTask<object> TestReceive(object message)
            {
                switch (message)
                {
                    case TestPacket1 packet1:
                        Console.WriteLine($"Remote{Index}:接收消息{nameof(TestPacket1)}--{packet1.Value}");
                        return new TestPacket2 { Value = packet1.Value };
                    case TestPacket2 packet2:
                        Console.WriteLine($"Remote{Index}:接收消息{nameof(TestPacket2)}--{packet2.Value}");
                        if (packet2.Value == 0)
                        {
                            stopwatch.Restart();
                        }
                        if (packet2.Value == MessageCount - 1)
                        {
                            stopwatch.Stop();

                            Console.WriteLine($"Remote{Index}:TestReceive{MessageCount} ------ {stopwatch.ElapsedMilliseconds}----- 每秒:{MessageCount * 1000 / (stopwatch.ElapsedMilliseconds +1)}");
                        }
                        return null;
                    default:
                        break;
                }
                return null;
            }
        }

        #endregion

        #region 连接测试


        private static async void TestConnect()
        {
            for (int i = 0; i < RemoteCount; i++)
            {
                Connect(i);
            }
        }

        private static async void Connect(int index)
        {
            IRemote remote = new TCPRemote();
            var res = await remote.ConnectAsync(IPAddress.Loopback, 54321);
            if (res == null)
            {
                Console.WriteLine($"Remote{index}:Success");
            }
            else
            {
                Console.WriteLine($"Remote:{res}");
            }

            //remote.SendAsync(new Packet1());
        }

        #endregion
    }


    public struct TestStruct
    {

    }
}
