using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Network.Remote;
using MessageQueue2 = System.Collections.Concurrent.ConcurrentQueue<(int messageID, short rpcID, System.ArraySegment<byte> messageBody, Network.Remote.INetRemote2 remote)>;

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

        static ConcurrentQueue<(IReceivedPacket Packet, INetRemote2 Remote)> receivePool
            = new ConcurrentQueue<(IReceivedPacket, INetRemote2)>();
        static ConcurrentQueue<(IReceivedPacket Packet, INetRemote2 Remote)> dealPoop
            = new ConcurrentQueue<(IReceivedPacket, INetRemote2)>();

        /// <summary>
        /// 消息大包和remote一起放入接收消息池
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="remote"></param>
        /// <param name="switchThread">是否切换处理线程</param>
        public static void PushReceivePacket(IReceivedPacket packet, INetRemote2 remote, bool switchThread)
        {
            if (switchThread)
            {
                receivePool.Enqueue((packet, remote));
            }
            else
            {
                DealLargeReceivePacket(packet, remote);
            }
        }

        static MessageQueue2 receivePool2 = new MessageQueue2();
        static MessageQueue2 dealPoop2 = new MessageQueue2();
        /// <summary>
        /// 小包消息
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="body"></param>
        /// <param name="remote"></param>
        /// <param name="switchThread">是否切换处理线程</param>
        public static void PushReceivePacket(int messageID, short rpcID, ArraySegment<byte> body, INetRemote2 remote, bool switchThread)
        {
            if (switchThread)
            {
                receivePool2.Enqueue((messageID, rpcID, body, remote));
            }
            else
            {
                var msg = MessageLUT.Deserialize(messageID, body);
                ReceiveCallback(remote, messageID, rpcID, msg);
            }
        }

        /// <summary>
        /// 在控制执行顺序的线程中刷新，所有异步方法的后续部分都在这个方法中执行
        /// </summary>
        /// <param name="delta"></param>
        static void Update(double delta)
        {
            DealLargePackatPool();
            DealSmallPackatPool();
        }

        private static void DealSmallPackatPool()
        {
            bool haveMessage = false;
            //处理接受
            //lock (receivePool)
            //{
            if (receivePool2.Count > 0)
            {
                haveMessage = true;
            }
            //}

            if (haveMessage)
            {
                var temp = receivePool2;
                receivePool2 = dealPoop2;
                dealPoop2 = temp;

                while (dealPoop2.Count > 0)
                {
                    //var (Packet, Remote) = dealPoop.Dequeue();
                    if (!dealPoop2.TryDequeue(out var res))
                    {
                        //todo
                        //throw new Exception();
                    }
                    var (messageID, RpcID, body, Remote) = res;

                    var msg = MessageLUT.Deserialize(messageID, body);

                    ReceiveCallback(Remote, messageID, RpcID, msg);
                }
            }
        }

        private static void DealLargePackatPool()
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

                    (IReceivedPacket Packet, INetRemote2 Remote) = res;
                    DealLargeReceivePacket(Packet, Remote);
                }
            }
        }

        static void DealLargeReceivePacket(IReceivedPacket Packet, INetRemote2 Remote)
        {
            while (Packet?.MessagePacket.Count > 0)
            {
                var (messageID, rpcID, body) = Packet.MessagePacket.Dequeue();
                var msg = MessageLUT.Deserialize(messageID, body);

                ReceiveCallback(Remote, messageID, rpcID, msg);
            }

            Packet?.Push2Pool();
        }

        static async void ReceiveCallback(INetRemote2 remote, int messageID, short rpcID, dynamic msg)
        {
            if (remote == null)
            {
                return;
            }
            if (rpcID == 0 || rpcID == short.MinValue)
            {
                if (remote.OnReceive == null)
                {
                    return;
                }
                ///这个消息是非Rpc请求
                ///普通响应onRely
                var response = await remote.OnReceive(msg);

                if (response is Task<dynamic> task)
                {
                    response = await task ?? null;
                }

                if (response is ValueTask<dynamic> vtask)
                {
                    response = await vtask ?? null;
                }

                if (response == null)
                {
                    return;
                }
                /// 普通返回
                remote.SendAsync(response);
            }
            else if (rpcID > 0)
            {
                if (remote.OnReceive == null)
                {
                    return;
                }
                ///这个消息rpc的请求 
                ///普通响应onRely
                var response = await remote.OnReceive(msg);
                if (response is Task<object> task)
                {
                    response = await task ?? null;
                }

                if (response is ValueTask<object> vtask)
                {
                    response = await vtask ?? null;
                }

                if (response == null)
                {
                    return;
                }
                ///rpc的返回 
                remote.SendAsync((short)(rpcID * -1), response);
            }
            else
            {
                ///这个消息是rpc返回（回复的RpcID为-1~-32767）
                remote.RpcCallbackPool.TrySetResult(rpcID, msg);
            }
        }
    }
}
