using System.Threading.Tasks;
using MessageQueue = System.Collections.Concurrent.ConcurrentQueue<(short rpcID, dynamic message, MMONET.Remote.IDealMessage remote)>;

namespace MMONET.Remote
{
    /// <summary>
    /// 接收消息池
    /// 调用<seealso cref="MainThreadScheduler.Update(double)"/>刷新
    /// </summary>
    internal partial class MessageThreadTransducer
    {
        static MessageThreadTransducer()
        {
            MainThreadScheduler.Add(Update);
        }

        static MessageQueue receivePool = new MessageQueue();
        static MessageQueue dealPoop = new MessageQueue();

        /// <summary>
        /// 实例消息
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <param name="remote"></param>
        /// <param name="switchThread">是否切换处理线程</param>
        internal static void Push(short rpcID, dynamic message, IDealMessage remote, bool switchThread)
        {
            if (switchThread)
            {
                receivePool.Enqueue((rpcID, message, remote));
            }
            else
            {
                ReceiveCallback(remote, rpcID, message);
            }
        }

        /// <summary>
        /// 在控制执行顺序的线程中刷新，所有异步方法的后续部分都在这个方法中执行
        /// </summary>
        /// <param name="delta"></param>
        static void Update(double delta)
        {
            DealSmallPackatPool();
        }

        private static void DealSmallPackatPool()
        {
            bool haveMessage = false;
            
            if (receivePool.Count > 0)
            {
                haveMessage = true;
            }

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
                        continue;
                    }

                    ReceiveCallback(res.remote, res.rpcID,res.message);
                }
            }
        }

        static async void ReceiveCallback(IDealMessage remote, short rpcID, dynamic msg)
        {
            if (remote == null)
            {
                return;
            }
            if (rpcID == 0 || rpcID == short.MinValue)
            {
                ///这个消息是非Rpc请求
                ///普通响应onRely
                var response = await remote.OnReceiveMessage(msg);

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
                ///这个消息rpc的请求 
                ///普通响应onRely
                var response = await remote.OnReceiveMessage(msg);

                if (response is Task<object> task)
                {
                    response = await task;
                }
                else if (response is ValueTask<object> vtask)
                {
                    response = await vtask;
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
                remote.TrySetRpcResult(rpcID, msg);
            }
        }
    }
}
