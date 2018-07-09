using System;
using System.Collections.Concurrent;
using Network.Remote;

namespace MMONET.Message
{
    /// <summary>
    /// 接收消息池
    /// 调用<seealso cref="MainThreadScheduler.Update(double)"/>刷新
    /// </summary>
    public partial class MessagePool
    {
        static MessagePool()
        {
            MainThreadScheduler.Add(Update);
        }

        static ConcurrentQueue<(IReceivedPacket Packet, INetRemote Remote)> receivePool
            = new ConcurrentQueue<(IReceivedPacket, INetRemote)>();
        static ConcurrentQueue<(IReceivedPacket Packet, INetRemote Remote)> dealPoop
            = new ConcurrentQueue<(IReceivedPacket, INetRemote)>();

        /// <summary>
        /// 消息大包和remote一起放入接收消息池
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="remote"></param>
        public static void PushReceivePacket(IReceivedPacket packet, INetRemote remote)
        {
            receivePool.Enqueue((packet, remote));
        }

        /// <summary>
        /// 在控制执行顺序的线程中刷新，所有异步方法的后续部分都在这个方法中执行
        /// </summary>
        /// <param name="delta"></param>
        static void Update(double delta)
        {
            bool haveMessage = false;
            //处理接受
            //lock (receivePool)
            //{
            if (receivePool.Count > 0)
            {
                haveMessage = true;
            }
            //}

            if (haveMessage)
            {
                var temp = receivePool;
                receivePool = dealPoop;
                dealPoop = temp;

                while (dealPoop.Count > 0)
                {
                    //var (Packet, Remote) = dealPoop.Dequeue();
                    if (!dealPoop.TryDequeue(out var res))
                    {
                        //todo
                        //throw new Exception();
                    }
                    var (Packet, Remote) = res;
                    while (Packet?.MessagePacket.Count > 0)
                    {
                        var (messageID, rpcID, body) = Packet.MessagePacket.Dequeue();
                        var msg = MessageLUT.Deserialize(messageID, body);

                        Remote.ReceiveCallback(messageID, rpcID, msg);

                    }

                    Packet?.Push2Pool();
                }
            }
        }
    }
}
