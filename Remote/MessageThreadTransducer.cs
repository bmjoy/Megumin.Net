using Megumin.Message;
using Megumin.Remote;
using Net.Remote;
using System;
using System.Buffers;
using System.Threading.Tasks;
using MessageQueue = System.Collections.Concurrent.ConcurrentQueue<System.Action>;

namespace Megumin.Message
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

        /// <summary>
        /// 在控制执行顺序的线程中刷新，所有异步方法的后续部分都在这个方法中执行
        /// </summary>
        /// <param name="delta"></param>
        static void Update(double delta)
        {
            while (receivePool.TryDequeue(out var res))
            {
                res?.Invoke();
            }
        }

        internal static IMiniAwaitable<object> Push(int rpcID, object message, IObjectMessageReceiver r)
        {
            MiniTask<object> task1 = MiniTask<object>.Rent();
            Action action = async () =>
            {
                ///此处可以忽略异常处理
                ///
                var response = await r.Deal(rpcID, message);

                if (response is Task<object> task)
                {
                    response = await task;
                }

                if (response is ValueTask<object> vtask)
                {
                    response = await vtask;
                }

                task1.SetResult(response);
            };

            receivePool.Enqueue(action);

            return task1;
        }
    }
}
