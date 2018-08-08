using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Network.Remote;

namespace MMONET.Remote
{
    using System.Net;
    using ByteMessageList = ListPool<ArraySegment<byte>>;
    using ExtraMessage = System.ValueTuple<int?, int?, int?, int?>;


    public partial class TCPRemote2 :IRemote,ISuperRemote
    {
        public Socket Client { get; }

        public IPEndPoint ConnectIPEndPoint { get; set; }
        public EndPoint RemappedEndPoint => Client.RemoteEndPoint;
        public IRpcCallbackPool RpcCallbackPool { get; } = new RpcCallbackPool(31);
        public DateTime LastReceiveTime { get; protected set; } = DateTime.Now;
        public event OnReceiveMessage OnReceive;
        /// <summary>
        /// 当前是否为手动关闭中
        /// </summary>
        bool manualDisconnecting = false;

        public TCPRemote2() : this(new Socket(SocketType.Stream, ProtocolType.Tcp))
        {

        }

        internal TCPRemote2(Socket client)
        {
            this.Client = client;
            IsVaild = true;
        }

        void OnSocketException(SocketError error)
        {
            OnDisConnect?.Invoke(error);

            TryDisConnectSocket();
        }

        void TryDisConnectSocket()
        {
            try
            {
                if (Client.Connected)
                {
                    Client.Shutdown(SocketShutdown.Both);
                    Client.Disconnect(false);
                    Client.Close();
                }
            }
            catch (Exception)
            {
                //todo
            }
        }

        /// <summary>
        /// 这是留给用户赋值的
        /// </summary>
        public int UserToken { get; set; }
        public bool IsVaild { get; protected set; }
        public int Guid { get; } = InterlockedID<IRemote>.NewID();

        

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    try
                    {
                        if (Client.Connected)
                        {
                            Disconnect();
                        }
                    }
                    catch (Exception)
                    {

                    }
                    finally
                    {
                        Client?.Dispose();
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。
                IsVaild = false;
                lock (sendlock)
                {
                    foreach (var item in sendWaitList)
                    {
                        BufferPool.Push(item.Array);
                    }
                    sendWaitList.Clear();
                    foreach (var item in dealList)
                    {
                        BufferPool.Push(item.Array);
                    }
                    dealList.Clear();
                }

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~TCPRemote2()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(false);
        }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    ///连接 断开连接
    partial class TCPRemote2:IConnectable
    {
        public event Action<SocketError> OnDisConnect;

        public async Task<Exception> ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            this.ConnectIPEndPoint = endPoint;
            while (retryCount >= 0)
            {
                try
                {
                    await Client.ConnectAsync(ConnectIPEndPoint);
                    return null;
                }
                catch (Exception e)
                {
                    if (retryCount <= 0)
                    {
                        return e;
                    }
                    else
                    {
                        retryCount--;
                    }
                }
            }

            return new NullReferenceException();
        }

        public void Disconnect()
        {
            IsVaild = false;
            manualDisconnecting = true;
            TryDisConnectSocket();
        }
    }

    /// 发送实例消息
    partial class TCPRemote2 : ISendMessage,IRpcSendMessage,ILazyRpcSendMessage
    {
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
        void SendAsync<T>(short rpcID, T message)
        {
            ///序列化用buffer
            var sbuffer = BufferPool.Pop65536();
            var (messageID, byteMessage) = SerializeMessage(sbuffer, message);

            var sendbuffer = PacketBuffer(messageID,rpcID,default, byteMessage);
            BufferPool.Push65536(sbuffer);

            SendByteBuffer(sendbuffer);
        }

        /// <summary>
        /// 转发入口
        /// </summary>
        /// <param name="mappedID"></param>
        /// <param name="rpcID"></param>
        /// <param name="messageID"></param>
        /// <param name="messageBodyBuffer"></param>
        void ZhuanFa(int mappedID, short rpcID, int messageID, ArraySegment<byte> messageBodyBuffer)
        {
            PacketBuffer(messageID, rpcID, (mappedID, default, default, default), messageBodyBuffer);
        }

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

        public Task BroadCastSendAsync(ArraySegment<byte> msgBuffer) => Client.SendAsync(msgBuffer, SocketFlags.None);
    }

    /// 发送字节消息
    partial class TCPRemote2
    {
        List<ArraySegment<byte>> sendWaitList = new List<ArraySegment<byte>>(13);
        List<ArraySegment<byte>> dealList = new List<ArraySegment<byte>>(13);
        bool isSending;
        protected SocketAsyncEventArgs sendArgs;
        protected readonly object sendlock = new object();

        /// <summary>
        /// 注意，发送完成时内部回收了buffer。
        /// </summary>
        /// <param name="bufferMsg"></param>
        protected void SendByteBuffer(ArraySegment<byte> bufferMsg)
        {
            sendWaitList.Add(bufferMsg);
            SendStart();
        }

        /// <summary>
        /// 检测是否应该发送
        /// </summary>
        /// <returns></returns>
        bool CheckCanSend()
        {
            if (!Client.Connected)
            {
                return false;
            }

            ///如果待发送队列有消息，交换列表 ，继续发送
            lock (sendlock)
            {
                if (sendWaitList.Count > 0 && !manualDisconnecting && isSending == false)
                {
                    isSending = true;


                    ///交换等待发送队列
                    var temp = dealList;
                    dealList = sendWaitList;
                    sendWaitList = temp;
                    return true;
                }
            }

            return false;
        }

        void SendStart()
        {
            if (!CheckCanSend())
            {
                return;
            }

            if (sendArgs == null)
            {
                sendArgs = new SocketAsyncEventArgs();
                sendArgs.Completed += SendComplete;
            }

            sendArgs.BufferList = dealList;
            if (!Client.SendAsync(sendArgs))
            {
                SendComplete(this, sendArgs);
            }
            
        }

        void SendComplete(object sender, SocketAsyncEventArgs args)
        {
            ///这个方法由IOCP线程调用。需要尽快结束。
            
            ///无论成功失败，都要清理发送列表
            if (dealList.Count > 0)
            {
                ///((框架约定1)发送字节数组发送完成后由发送逻辑回收)
                ///归还buffer,重置BufferList 
                foreach (var item in dealList)
                {
                    BufferPool.Push(item.Array);
                }
                dealList.Clear();
            }
            isSending = false;

            if (args.SocketError == SocketError.Success)
            {
                ///冗余调用，可以省去
                //args.BufferList = null;

                SendStart();
            }
            else
            {
                if (!manualDisconnecting)
                {
                    ///遇到错误
                    OnSocketException(args.SocketError);
                }
            }
        }
    }

    /// 接收字节消息
    partial class TCPRemote2 : IReceiveMessage,IDealObjectMessage
    {
        bool IDealObjectMessage.TrySetRpcResult(short rpcID, dynamic message) => RpcCallbackPool?.TrySetResult(rpcID, message);

        void IDealObjectMessage.SendAsync<T>(short rpcID, T message) => SendAsync(rpcID, message);

        bool isReceiving;
        const int MaxBufferLength = 8192;
        public void Receive(OnReceiveMessage onReceive)
        {
            this.OnReceive = onReceive;
            ReceiveStart();
        }

        /// <summary>
        /// 线程安全的，多次调用不应该发生错误
        /// </summary>
        void ReceiveStart()
        {
            if (!Client.Connected || isReceiving)
            {
                return;
            }
            ReceiveAsync(new ArraySegment<byte>(BufferPool.Pop(MaxBufferLength), 0, MaxBufferLength));
        }

        async void ReceiveAsync(ArraySegment<byte> buffer)
        {
            if (!Client.Connected || isReceiving)
            {
                return;
            }

            try
            {
                isReceiving = true;
                var length = await Client.ReceiveAsync(buffer, SocketFlags.None);

                if (length == 0)
                {
                    OnSocketException(SocketError.Shutdown);
                    isReceiving = false;
                    return;
                }

                LastReceiveTime = DateTime.Now;
                int totalValidLength = length + buffer.Offset;
                var list = ByteMessageList.Pop();
                ///分包
                var residual = CutOff(totalValidLength, buffer.Array, list);

                var newBuffer = BufferPool.Pop(MaxBufferLength);
                if (residual.Count > 0)
                {
                    ///半包复制
                    Buffer.BlockCopy(residual.Array, residual.Offset, newBuffer, 0, residual.Count);
                }
                ///继续接收
                ReceiveAsync(new ArraySegment<byte>(newBuffer, residual.Count, MaxBufferLength - residual.Count));

                ///处理消息
                DealMessageAsync(list);
            }
            catch (SocketException e)
            {
                if (!manualDisconnecting)
                {
                    OnSocketException(e.SocketErrorCode);
                }
                isReceiving = false;
            }
        }

        private void DealMessageAsync(List<ArraySegment<byte>> list)
        {
            if (list.Count == 0)
            {
                return;
            }

            Task.Run(() =>
            {
                var res = Parallel.ForEach(list, (item) =>
                {
                    ///解包
                    var unpackedMessage = UnPacketBuffer(item);

                    ///处理字节消息
                    (bool IsContinue, bool SwitchThread, short rpcID, dynamic objectMessage)
                        = DealBytesMessage(unpackedMessage.messageID, unpackedMessage.rpcID,
                                            unpackedMessage.extraType, unpackedMessage.extraMessage,
                                            unpackedMessage.byteUserMessage);

                    DealObjectMessage(IsContinue, SwitchThread, rpcID, objectMessage);

                });

                if (res.IsCompleted)
                {
                    ///回收池对象
                    var buffer = list[0].Array;
                    BufferPool.Push(buffer);
                    list.Clear();
                    ByteMessageList.Push(list);
                }
            });
        }
    }


    ///可由继承修改的关键部分
    partial class TCPRemote2:RemoteBase
    {
        /// <summary>
        /// 分离粘包
        /// <para><see cref="CutOff(int, byte[], IList{ArraySegment{byte}})"/> 和 <see cref="RemoteCore.PacketBuffer(int, short, ExtraMessage, ArraySegment{byte})"/> 对应 </para>
        /// </summary>
        /// <param name="length"></param>
        /// <param name="source"></param>
        /// <param name="pushCompleteMessage"></param>
        /// <returns></returns>
        public virtual ArraySegment<byte> CutOff(int length, byte[] source, IList<ArraySegment<byte>> pushCompleteMessage)
        {
            pushCompleteMessage.Add(default);
            return default;
        }

        public virtual ValueTask<dynamic> DealObjectMessage(dynamic message)
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
    }
}
