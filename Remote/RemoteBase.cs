using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MMONET.Message;
using Network.Remote;
using ExtraMessage = System.ValueTuple<int?, int?, int?, int?>;

namespace MMONET.Remote
{
    public abstract partial class RemoteBase : RemoteCore
    {
        public int Guid { get; } = InterlockedID<IRemote>.NewID();
        /// <summary>
        /// 这是留给用户赋值的
        /// </summary>
        public int UserToken { get; set; }
        public bool IsVaild { get; protected set; } = true;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public event OnReceiveMessage OnReceive;
        public DateTime LastReceiveTime { get; protected set; } = DateTime.Now;
        public IRpcCallbackPool RpcCallbackPool { get; } = new RpcCallbackPool(31);
        /// <summary>
        /// 当前是否为手动关闭中
        /// </summary>
        protected bool manualDisconnecting = false;
    }

    /// 发送
    partial class RemoteBase: ISendMessage,IRpcSendMessage,ILazyRpcSendMessage
    {
        /// <summary>
        /// 异步发送
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        public void SendAsync<T>(T message)
        {
            SendAsync(0, message);
        }

        /// <summary>
        /// 正常发送入口
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        protected void SendAsync<T>(short rpcID, T message)
        {
            ///序列化用buffer,使用堆外内存
            using (var memoryOwner = BufferPool.NativeRent(16384))
            {
                var span = memoryOwner.Memory.Span;

                var (messageID, length) = SerializeMessage(span, message);
                var sendbuffer = PacketBuffer(messageID, rpcID, default, span.Slice(0, length));
                SendByteBufferAsync(sendbuffer);
            }
        }

        /// <summary>
        /// ((框架约定1)发送字节数组发送完成后由发送逻辑回收)
        /// </summary>
        /// <param name="bufferMsg"></param>
        /// <remarks>个人猜测，此处是性能敏感区域，使用Task可能会影响性能（并没有经过测试）</remarks>
        protected abstract void SendByteBufferAsync(IMemoryOwner<byte> bufferMsg);

        public Task<(RpcResult result, Exception exception)> RpcSendAsync<RpcResult>(dynamic message)
        {
            ReceiveStart();

            var (rpcID, source) = RpcCallbackPool.Regist<RpcResult>();

            try
            {
                SendAsync(rpcID, message);
                return source;
            }
            catch (Exception e)
            {
                RpcCallbackPool.Remove(rpcID);
                return Task.FromResult<(RpcResult result, Exception exception)>((default, e));
            }
        }

        public ILazyAwaitable<RpcResult> LazyRpcSendAsync<RpcResult>(dynamic message, Action<Exception> OnException = null)
        {
            ReceiveStart();

            var (rpcID, source) = RpcCallbackPool.Regist<RpcResult>(OnException);

            try
            {
                SendAsync(rpcID, message);
                return source;
            }
            catch (Exception e)
            {
                source.CancelWithNotExceptionAndContinuation();
                OnException?.Invoke(e);
                return source;
            }
        }
    }

    /// 接收
    partial class RemoteBase:IDealMessage
    {
        protected const int MaxBufferLength = 8192;

        public void Receive(OnReceiveMessage onReceive)
        {
            this.OnReceive = onReceive;
            ReceiveStart();
        }

        /// <summary>
        /// 应该为线程安全的，多次调用不应该发生错误
        /// </summary>
        protected abstract void ReceiveStart();

        /// <summary>
        /// 处理经过反序列化的消息
        /// </summary>
        /// <param name="IsContinue"></param>
        /// <param name="SwitchThread"></param>
        /// <param name="rpcID"></param>
        /// <param name="objectMessage"></param>
        protected void DealObjectMessage(bool IsContinue, bool SwitchThread, short rpcID, dynamic objectMessage)
        {
            if (IsContinue)
            {
                ///处理实例消息
                MessageThreadTransducer.Push(rpcID, objectMessage, this, SwitchThread);
            }
        }

        /// <summary>
        /// 处理收到的实例消息
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public virtual ValueTask<dynamic> OnReceiveMessage(dynamic message)
        {
            if (OnReceive == null)
            {
                return new ValueTask<dynamic>(Task.FromResult<dynamic>(null));
            }
            else
            {
                return OnReceive(message);
            }
        }

        void IDealMessage.SendAsync<T>(short rpcID, T message) => SendAsync(rpcID, message);

        bool IDealMessage.TrySetRpcResult(short rpcID, dynamic message) => RpcCallbackPool?.TrySetResult(rpcID, message);
    }
}
