using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MMONET.Message;
using Network.Remote;
using static MMONET.Message.MessageLUT;

namespace MMONET.Remote
{
    /// <summary>
    /// <para>TcpChannel内存开销 整体采用内存池优化</para>
    /// <para>发送内存开销 对于TcpChannel实例 动态内存开销，取决于发送速度，内存实时占用为发送数据的1~2倍</para>
    /// <para>                  接收的常驻开销8kb*2,随着接收压力动态调整</para>
    /// </summary>
    public class TCPRemote : IRemote, ISendMessage, IReceiveMessage, IConnectable,
        INetRemote, IUpdateRpcResult
    {
        public Socket Socket => tcpHelper.Socket;

        private TCPHelper tcpHelper;

        public TCPRemote() : this(new TCPHelper())
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="socket"></param>
        protected TCPRemote(TCPHelper tcpHelper)
        {
            Init(tcpHelper);

            ///断开连接将remote设置为无效
            OnDisConnect += (er) => { IsVaild = false; };
            this.AddToPool();
        }

        /// <summary>
        /// 设置Socket
        /// </summary>
        /// <param name="tcpHelper"></param>
        protected void Init(TCPHelper tcpHelper)
        {
            if (this.tcpHelper != null)
            {
                tcpHelper.Dispose();
            }

            this.tcpHelper = tcpHelper;
            tcpHelper.OnReceivedPacket += (args) =>
            {
                MessagePool.PushReceivePacket(args, this);
            };
            tcpHelper.OnDisConnect += this.OnDisConnect;
        }

        public bool Connected
        {
            get
            {
                return Socket?.Connected ?? false;
            }
        }

        /// <summary>
        /// RemoteBase类型中的唯一ID。
        /// </summary>
        public int Guid { get; } = InterlockedID<IRemote>.NewID();
        public int InstanceID { get; set; }
        /// <summary>
        /// 如果为false <see cref="MainThreadScheduler.Update(double)"/>会检测从<see cref="RemotePool"/>中移除
        /// </summary>
        public bool IsVaild { get; protected set; } = true;

        #region RPC

        public double RpcTimeOutMilliseconds
        {
            get => rpcCallbackPool.RpcTimeOutMilliseconds;
            set => rpcCallbackPool.RpcTimeOutMilliseconds = value;
        }

        readonly IRpcCallbackPool rpcCallbackPool = new RpcCallbackPool(31);

        public void UpdateRpcResult(double delta) => rpcCallbackPool.UpdateRpcResult(delta);

        #endregion

        #region Connect

        public IPEndPoint IPEndPoint { get; set; }

        public event Action<SocketError> OnDisConnect;

        public void Disconnect(bool triggerOnDisConnectEvent)
        {
            IsVaild = false;
            if (Connected)
            {
                tcpHelper.Dispose();
            }

            if (triggerOnDisConnectEvent)
            {
                OnDisConnect?.Invoke(SocketError.Disconnecting);
            }
        }

        public async Task<Exception> ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            this.IPEndPoint = endPoint;
            while (retryCount >= 0)
            {
                var ex = await tcpHelper.ConnectAsync(IPEndPoint);
                if (ex == null)
                {
                    ///连接成功
                    return null;
                }
                else
                {
                    if (retryCount <= 0)
                    {
                        return ex;
                    }
                    else
                    {
                        retryCount--;
                    }
                }
            }

            return new NullReferenceException();
        }

        #endregion

        #region Send

        public bool IsSending => tcpHelper.IsSending;

        public Task<(RpcResult result, Exception exception)> RpcSendAsync<RpcResult>(dynamic message)
        {
            CheckReceive();

            var (rpcID, source) = rpcCallbackPool.Regist<RpcResult>();

            var ex = tcpHelper.SendAsync(rpcID, message);

            if (ex != null)
            {
                rpcCallbackPool.Remove(rpcID);
                return Task.FromResult<(RpcResult result, Exception exception)>((default, ex));
            }

            return source;
        }

        void CheckReceive()
        {
            if (!IsReceiving)
            {
                tcpHelper.Receive();
            }
        }


        /// <summary>
        /// <see cref="IRpcSendMessage.SafeRpcSendAsync{RpcResult}(dynamic, Action{Exception})"/>
        /// </summary>
        /// <typeparam name="RpcResult"></typeparam>
        /// <param name="message"></param>
        /// <param name="OnException"></param>
        /// <returns></returns>
        public ICanAwaitable<RpcResult> SafeRpcSendAsync<RpcResult>(dynamic message, Action<Exception> OnException = null)
        {
            CheckReceive();

            var (rpcID, source) = rpcCallbackPool.Regist<RpcResult>(OnException);

            var ex = tcpHelper.SendAsync(rpcID, message);

            return source;
        }

        public void SendAsync<T>(T message) => tcpHelper?.SendAsync(0, message);


        #endregion

        #region Receive

        public bool IsReceiving { get; protected set; }

        public DateTime LastReceiveTime { get; protected set; }
        /// <summary>
        /// 接受消息的回调函数
        /// </summary>
        protected OnReceiveMessage onReceive;

        /// <summary>
        /// 异步接受消息包
        /// <para>1.remote收到消息大包（拼合的小包组）</para>
        /// <para>2.remote 调用 <see cref="MessagePool.PushReceivePacket(IReceivedPacket, INetRemote)"/></para>
        /// <para>消息大包和remote一起放入接收消息池<see cref="MessagePool"/>（这一环节为了切换执行异步方法后续的线程）</para>
        /// <para>3.（主线程）<see cref="MainThreadScheduler.Update(double)"/>时统一从池中取出消息，反序列化。
        ///          每个小包是一个消息，由remote <see cref="INetRemote.ReceiveCallback"/>>处理</para>
        /// <para>4.1 检查RpcID(内置不可见) 如果是Rpc结果，触发异步方法后续。如果rpc已经超时，消息被直接丢弃</para>
        /// <para>4.2 不是Rpc结果 则remote调用<paramref name="onReceive"/>回调函数(当前方法参数)处理消息</para>
        /// </summary>
        /// <param name="onReceive">处理消息方法，如果远端为RPC调用，那么应该返回一个合适的结果，否则返回null</param>
        public void Receive(OnReceiveMessage onReceive)
        {
            this.onReceive += onReceive;

            Receive();
        }

        #endregion

        async void INetRemote.ReceiveCallback(int messageID, short rpcID, dynamic msg)
        {
            if (rpcID == 0 || rpcID == short.MinValue)
            {
                if (onReceive == null)
                {
                    return;
                }
                ///这个消息是非Rpc请求
                ///普通响应onRely
                var response = await onReceive(msg);

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
                SendAsync(response);
            }
            else if (rpcID > 0)
            {
                if (onReceive == null)
                {
                    return;
                }

                ///这个消息rpc的请求 
                ///普通响应onRely
                var response = await onReceive(msg);
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
                sender.SendAsync((short)(rpcID * -1), response);
            }
            else
            {
                ///这个消息是rpc返回（回复的RpcID为-1~-32767）
                rpcCallbackPool.Call(rpcID, msg);
            }
        }

        public Task BroadCastSendAsync(ArraySegment<byte> msgBuffer)
        {
            return Task.Run(() =>
            {
                Socket.Send(msgBuffer.Array, msgBuffer.Offset, msgBuffer.Count, SocketFlags.None);
            });
        }
    }

    /// <summary>
    /// 出现任何错误直接作废
    /// </summary>
    public class TCPHelper
    {
        public TCPHelper()
        {
            this.Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        }

        public TCPHelper(Socket socket)
        {
            this.Socket = socket;
        }

        public Socket Socket { get; }

        #region ConnectAsync

        bool isConnecting = false;
        public async Task<Exception> ConnectAsync(IPEndPoint endPoint)
        {
            if (isConnecting)
            {
                return new Exception("连接正在进行中");
            }

            isConnecting = true;
            Exception exception = null;
            await Task.Run(() =>
            {
                try
                {
                    Socket.Connect(endPoint);
                }
                catch (Exception e)
                {
                    exception = e;
                }
            });

            isConnecting = false;
            return exception;
        }

        #endregion

        #region Send
        public event Action<SocketError> OnDisConnect;
        List<ArraySegment<byte>> sendWaitList = new List<ArraySegment<byte>>(13);
        List<ArraySegment<byte>> dealList = new List<ArraySegment<byte>>(13);
        public bool IsSending { get; protected set; }
        protected SocketAsyncEventArgs sendArgs;
        private bool disposedValue;

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        public Exception SendAsync<T>(short rpcID, T message)
        {
            if (!Socket.Connected)
            {
                return null;
            }

            if (disposedValue)
            {
                ///当前发送器已被释放
                IsSending = false;
                return new ObjectDisposedException(nameof(TCPHelper));
            }

            var bufferMsg = MessageLUT.Serialize(rpcID, message);

            bool sendNow = false;

            ///如果现在没有发送消息，手动调用发送。
            lock (this)
            {
                sendWaitList.Add(bufferMsg);
                sendNow = IsSending == false;
                if (sendNow)
                {
                    ///交换队列放在这里而不是Send函数是为了加锁。
                    ///交换等待发送队列
                    var temp = dealList;
                    dealList = sendWaitList;
                    sendWaitList = temp;
                }
            }

            if (sendNow)
            {
                Send();
            }

            return null;
        }


        void Send()
        {
            if (disposedValue)
            {
                ///当前发送器已被释放
                IsSending = false;
                return;
            }

            IsSending = true;
            if (sendArgs == null)
            {
                sendArgs = new SocketAsyncEventArgs();
                sendArgs.Completed += SendComplete;
            }

            sendArgs.BufferList = dealList;

            if (!Socket.SendAsync(sendArgs))
            {
                SendComplete(this, sendArgs);
            }
        }

        void SendComplete(object sender, SocketAsyncEventArgs args)
        {
            ///这个方法由其他线程调用

            if (args.SocketError == SocketError.Success)
            {
                //Console.WriteLine("发送完成");

                ///如果发送完成时，待发送队列还有消息，继续发送
                ///否则，标记发送停止
                bool continueSent = false;
                lock (this)
                {
                    ///归还buffer,重置BufferList
                    foreach (var item in args.BufferList)
                    {
                        BufferPool.Push(item.Array);
                    }
                    args.BufferList.Clear();
                    args.BufferList = null;

                    if (sendWaitList.Count > 0 && !disposedValue)
                    {
                        ///交换等待发送队列
                        var temp = dealList;
                        dealList = sendWaitList;
                        sendWaitList = temp;

                        continueSent = true;
                    }
                    else
                    {
                        IsSending = false;
                    }
                }
                if (continueSent)
                {
                    Send();
                }
            }
            else
            {
                if (!disposedValue)
                {
                    ///遇到错误
                    OnDisConnect?.Invoke(args.SocketError);
                }

                Dispose();
            }
        }

        #endregion

        #region Receive
        public bool IsReceiving { get; protected set; }
        public DateTime LastReceiveTime { get; protected set; }
        public new int ReceiveBufferSize => RemoteArgs.ReceiveBufferSize;
        public void Receive()
        {
            if (Socket.Connected)
            {
                if (!IsReceiving)
                {
                    IsReceiving = true;
                    RemoteArgs remoteArgs = RemoteArgs.Pop();
                    Receive(remoteArgs);
                }
            }
        }
        /// <summary>
        /// 收到了大消息包
        /// </summary>
        public event Action<IReceivedPacket> OnReceivedPacket;

        void Receive(RemoteArgs receiveArgs)
        {
            receiveArgs.Completed += TcpReceiveComplete;

            if (!Socket.ReceiveAsync(receiveArgs))
            {
                TcpReceiveComplete(this, receiveArgs);
            }
        }

        void TcpReceiveComplete(object sender, SocketAsyncEventArgs e)
        {
            RemoteArgs args = e as RemoteArgs;
            args.Completed -= TcpReceiveComplete;

            if (args.SocketError == SocketError.Success)
            {
                ///本次接收的长度
                int length = args.BytesTransferred;

                if (length <= 0)
                {
                    OnReceiveError(args, SocketError.NoData);
                    return;
                }

                ///上次接收的半包长度
                int alreadyHave = args.Offset;

                //////有效消息长度
                length += alreadyHave;
                LastReceiveTime = DateTime.Now;
                ////调试
                //Thread.Sleep(20);

                var (IsHaveMesssag, Residual) = DealTcpPacket(length, args);

                if (IsHaveMesssag)
                {
                    var newArgs = RemoteArgs.Pop();
                    if (Residual.Count > 0)
                    {
                        ///半包复制
                        Buffer.BlockCopy(Residual.Array, Residual.Offset, newArgs.Buffer, 0, Residual.Count);
                    }

                    newArgs.SetBuffer(Residual.Count, RemoteArgs.ReceiveBufferSize - Residual.Count);
                    Receive(newArgs);

                    OnReceivedPacket?.Invoke(args);
                }
                else
                {
                    ///没有完整包，继续接收
                    ///重设起始位置
                    args.SetBuffer(length, RemoteArgs.ReceiveBufferSize - length);///这一行代码耗费了我8个小时
                    Receive(args);
                    return;
                }
            }
            else
            {
                OnReceiveError(args, args.SocketError);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="length"></param>
        /// <param name="args">todo: describe args parameter on DealTcpPacket</param>
        /// <returns>IsHaveMesssag 是否至少含有一个完整消息。
        /// Residual剩余的半包。
        /// </returns>
        static (bool IsHaveMesssag, ArraySegment<byte> Residual) DealTcpPacket(int length, RemoteArgs args)
        {
            lock (args)
            {
                ///已经完整读取消息包的长度
                int tempoffset = 0;
                bool IsHaveMesssag = false;

                ///长度至少要大于8（2个字节也就是一个报头长度）
                while ((length - tempoffset) > TotalHeaderByteCount)
                {
                    ///取得当前消息包正文的长度
                    var (Size, MessageID, RpcID) = ParsePacketHeader(args.Buffer, tempoffset);

                    if (Size > length - tempoffset - TotalHeaderByteCount)
                    {
                        ///剩余消息长度不是一个完整包
                        break;
                    }
#if DEBUG
                    ///测试 这里可能存在解析错误
                    if (Size == 0)
                    {
                        throw new ArgumentException("Size =================0");
                    }
#endif
                    ///取得消息正文，起始偏移为tempoffset + 报头总长度；
                    ArraySegment<byte> messagePacket = new ArraySegment<byte>(args.Buffer, tempoffset + TotalHeaderByteCount, Size);
                    args.MessagePacket.Enqueue((MessageID, RpcID, messagePacket));
                    IsHaveMesssag = true;
                    ///识别的消息包，移动起始位置
                    tempoffset += (Size + TotalHeaderByteCount);
                }

                return (IsHaveMesssag, new ArraySegment<byte>(args.Buffer, tempoffset, length - tempoffset));
            }
        }

        void OnReceiveError(RemoteArgs args, SocketError socketError)
        {
            ///遇到错误关闭连接
            if (!disposedValue)
            {
                OnDisConnect?.Invoke(socketError);
            }
            Dispose();
            args.Push2Pool();
        }

        #endregion

        public void Stop()
        {
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Disconnect(false);
            Socket.Close();
            Socket.Dispose();
        }

        public void Dispose()
        {
            Socket.Dispose();
            disposedValue = true;

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
            IsSending = false;
            IsReceiving = false;
        }
    }

}
