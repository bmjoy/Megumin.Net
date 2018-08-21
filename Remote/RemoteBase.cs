using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MMONET.Message;
using Network.Remote;

namespace MMONET.Remote
{
    public abstract partial class RemoteBase
    {
        public int Guid { get; } = InterlockedID<IRemote>.NewID();
        /// <summary>
        /// 这是留给用户赋值的
        /// </summary>
        public int UserToken { get; set; }
        public bool IsVaild { get; protected set; } = true;
        public IPEndPoint ConnectIPEndPoint { get; set; }
        public DateTime LastReceiveTime { get; protected set; } = DateTime.Now;
        public IRpcCallbackPool RpcCallbackPool { get; } = new RpcCallbackPool(31);
        /// <summary>
        /// 当前是否为手动关闭中
        /// </summary>
        protected bool manualDisconnecting = false;
    }

    /// 发送
    partial class RemoteBase: ISendMessage,IRpcSendMessage,ISafeAwaitSendMessage
    {
        private IPacker<ISuperRemote> packer;

        /// <summary>
        /// 如果没有设置封包器，使用默认封包器。
        /// </summary>
        public IPacker<ISuperRemote> Packer
        {
            get
            {
                return packer ?? MessagePipline.Default;
            }
            set
            {
                packer = value;
            }
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="message"></param>
        public void SendAsync(object message)
        {
            SendAsync(0, message as dynamic);
        }

        /// <summary>
        /// 正常发送入口
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SendAsync<T>(short rpcID, T message);

        public Task<(RpcResult result, Exception exception)> SendAsync<RpcResult>(object message)
        {
            ReceiveStart();

            var (rpcID, source) = RpcCallbackPool.Regist<RpcResult>();

            try
            {
                SendAsync(rpcID, message as dynamic);
                return source;
            }
            catch (Exception e)
            {
                RpcCallbackPool.Remove(rpcID);
                return Task.FromResult<(RpcResult result, Exception exception)>((default, e));
            }
        }

        public ILazyAwaitable<RpcResult> SendAsyncSafeAwait<RpcResult>(object message, Action<Exception> OnException = null)
        {
            ReceiveStart();

            var (rpcID, source) = RpcCallbackPool.Regist<RpcResult>(OnException);

            try
            {
                SendAsync(rpcID, message as dynamic);
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
    partial class RemoteBase:IShuntMessage
    {
        protected const int MaxBufferLength = 8192;

        private IReceiver<ISuperRemote> receiver;
        
        /// <summary>
        /// 如果没有设置接收器，使用默认接收器。
        /// </summary>
        public IReceiver<ISuperRemote> Receiver
        {
            get
            {
                return receiver ?? MessagePipline.Default;
            }
            set
            {
                receiver = value;
            }
        }


        /// <summary>
        /// 应该为线程安全的，多次调用不应该发生错误
        /// </summary>
        public abstract void ReceiveStart();

        public async void ShuntMessage(short rpcID, object msg)
        {
            if (rpcID == 0 || rpcID == short.MinValue)
            {
                ///这个消息是非Rpc请求
                ///普通响应onRely
                var response = await Receiver.DealMessage(msg);

                if (response == null)
                {
                    return;
                }

                if (response is Task<object> task)
                {
                    response = await task ?? null;
                }

                if (response is ValueTask<object> vtask)
                {
                    response = await vtask ?? null;
                }

                /// 普通返回
                SendAsync(response);
            }
            else if (rpcID > 0)
            {
                ///这个消息rpc的请求 
                ///普通响应onRely
                var response = await Receiver.DealMessage(msg);

                if (response == null)
                {
                    return;
                }

                if (response is Task<object> task)
                {
                    response = await task;
                }
                else if (response is ValueTask<object> vtask)
                {
                    response = await vtask;
                }
                ///rpc的返回 
                SendAsync((short)(rpcID * -1), response as dynamic);
            }
            else
            {
                ///这个消息是rpc返回（回复的RpcID为-1~-32767）
                RpcCallbackPool?.TrySetResult(rpcID, msg);
            }
        }
    }
}
