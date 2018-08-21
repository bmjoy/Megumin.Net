using MMONET.Message;
using Network.Remote;
using System.Threading.Tasks;
using MessageQueue = System.Collections.Concurrent.ConcurrentQueue<(short rpcID, object message, Network.Remote.IShuntMessage remote)>;

namespace MMONET.Message
{
    /// <summary>
    /// 接收消息池
    /// 调用<seealso cref="MainThreadScheduler.Update(double)"/>刷新
    /// </summary>
    public partial class MessageThreadTransducer
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
        public static void Push(short rpcID, object message, IShuntMessage remote)
        {
            receivePool.Enqueue((rpcID, message, remote));
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

                while (dealPoop.TryDequeue(out var res))
                {
                    res.remote?.ShuntMessage(res.rpcID, res.message);
                }
            }
        }

    }
}
