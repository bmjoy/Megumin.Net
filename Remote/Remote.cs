using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace MMONET.Sockets
{
    /// <summary>
    /// 含有3个channel，默认TCP
    /// <para>TcpChannel内存开销 整体采用内存池优化</para>
    /// <para>发送内存开销 对于TcpChannel实例 动态内存开销，取决于发送速度，内存占用小于发送数据的2倍</para>
    /// <para>                  接收的常驻开销8kb*2,随着接收压力动态调整</para>
    /// 
    /// <para></para>
    /// <para>数据在网络上传输的时候，是以“帧”为单位的，帧最大为1518个字节，最小为64字节。</para>
    /// </summary>
    public class Remote : INetRemote, ITcpNeed, IRemote
    {
        #region 成员属性

        public RemoteChannal ChannalType { get; private set; } = RemoteChannal.TCP;

        public Socket KcpSocket { get; private set; }
        public Socket UdpSocket { get; private set; }

        internal IChannal TcpChannel { get; private set; } = null;

        public bool IsTcpSocketReceiving { get; private set; }


        public IPAddress HostIPv6IPAddress { get; set; }

        public IPAddress HostIPAddress { get; set; }
        public int ReceiveBufferSize { get; private set; }
        public bool IsTcpSending { get; private set; }

        public int RemotePort { get; set; }

        public IPAddress RemoteAddress { get; set; }

        /// <summary>
        /// 接受消息的回调函数
        /// </summary>
        private OnReceiveMessage onReceive;

        /// <summary>
        /// 预留给用户使用的ID，（用户自己赋值ID，自己管理引用，框架不做处理）
        /// </summary>
        public int InstanceID { get; set; }

        #endregion

        #region rpcPool

        short rpcCursor = 0;
        readonly object rpcCursorLock = new object();
        delegate void RpcCallback(object message);
        /// <summary>
        /// 每个session大约每秒30个包，超时时间为30秒；
        /// </summary>
        readonly Dictionary<short, RpcCallback> rpcCallbackPool = new Dictionary<short, RpcCallback>(30);

        /// <summary>
        /// 原子操作 取得RpcId,发送方的的RpcID为1~32767，回复的RpcID为-1~-32767，正负一一对应
        /// <para>0,-32768 为无效值</para>
        /// 最多同时维持32767个Rpc调用
        /// </summary>
        /// <returns></returns>
        short GetRpcID()
        {
            lock (rpcCursorLock)
            {
                if (rpcCursor <= 0 || rpcCursor == short.MaxValue)
                {
                    rpcCursor = 1;
                }
                else
                {
                    rpcCursor++;
                }

                return rpcCursor;
            }
        }

        #endregion

        #region 构造方法


        public Remote(RemoteChannal type)
        {
            this.ChannalType = type;

            if (ChannalType.HasFlag(RemoteChannal.TCP))
            {
                InitTcpChannal();
            }


            if (ChannalType.HasFlag(RemoteChannal.UDP))
            {

            }

            if (ChannalType.HasFlag(RemoteChannal.KCP))
            {

            }
        }



        public Remote() : this(RemoteChannal.TCP)
        {

        }

        /// <summary>
        /// 接受到连接用
        /// </summary>
        /// <param name="type"></param>
        /// <param name="channal"></param>
        private Remote(RemoteChannal type, IChannal channal)
        {
            this.ChannalType = type;
            switch (type)
            {
                case RemoteChannal.TCP:
                    TcpChannel = channal;
                    TcpChannel.SetRemote(this);
                    break;
                case RemoteChannal.KCP:
                    break;
                case RemoteChannal.UDP:
                    break;
                default:
                    break;
            }
        }

        private void InitTcpChannal()
        {
            if (TcpChannel != null)
            {
                TcpChannel.Dispose();
            }

            TcpChannel = new TcpChannel(this);
        }

        #endregion

        #region Listen

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mainPort"></param>
        /// <returns></returns>
        [Popular]
        public async Task<Remote> ListenAsync(int mainPort)
        {
            if (ChannalType.HasFlag(RemoteChannal.TCP))
            {
                IChannal channal = await this.TcpChannel.ListenAsync(mainPort);

                Remote result = new Remote(RemoteChannal.TCP, channal);
                return result;
            }

            return null;
        }

        async Task<IRemote> IRemote.ListenAsync(int mainPort)
        {
            return await ListenAsync(mainPort);
        }

        #endregion

        #region Connect

        /// <summary>
        /// 使用的Channal越多连接耗时越长，建议开始使用Tcp，连接然后在空闲的时候在连接其他channal
        /// <para>异常在底层Task过程中捕获，返回值null表示成功，调用者不必写try catch</para>
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public async ValueTask<Exception> ConnectAsync(IPAddress address, int port)
        {
            this.RemoteAddress = address;
            this.RemotePort = port;
            if (ChannalType.HasFlag(RemoteChannal.TCP))
            {
                Exception ex = await TcpChannel.ConnectAsync();
                return ex;
            }

            return new NullReferenceException();
        }

        public async ValueTask<Exception> ConnectAsync(IPAddress address, int port, int retryCount)
        {
            this.RemoteAddress = address;
            this.RemotePort = port;
            while (retryCount >= 0)
            {
                var ex = await ConnectAsync(address, port);
                if (ex == null)
                {
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

        #region Disconnect


        public bool IsAnyChannelConnected => TcpChannel?.Connected ?? false;

        public event Action<RemoteChannal,SocketError> OnDisConnect;

        /// <summary>
        /// 主动断开连接
        /// </summary>
        /// <param name="channal">todo: describe channal parameter on Disconnect</param>
        /// <param name="triggerOnDisConnectEvent">todo: describe triggerOnDisConnectEvent parameter on Disconnect</param>
        public void Disconnect(RemoteChannal channal =
            RemoteChannal.KCP | RemoteChannal.TCP | RemoteChannal.UDP, bool triggerOnDisConnectEvent = true)
        {
            if (channal.HasFlag(RemoteChannal.TCP))
            {
                TcpChannel.Disconnect(triggerOnDisConnectEvent);
            }
        }

        void ITcpNeed.OnDisconnect(RemoteChannal channal, SocketError socketError, bool triggerOnDisConnectEvent = true)
        {
            if (triggerOnDisConnectEvent)
            {
                OnDisConnect?.Invoke(channal,socketError);
            }
        }

        #endregion

        #region Send

        /// <summary>
        /// 发送消息，无阻塞立刻返回
        /// <para>调用方 无法了解发送情况</para>
        /// 序列化过程同步执行，方法返回表示序列化已结束，修改message内容不影响发送数据。
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type">todo: describe type parameter on SendAsync</param>
        public void Send<T>(T message, RemoteChannal type = RemoteChannal.TCP)
        {
            Send(message, 0, type);
        }

        [HubMethod]
        private void Send<T>(T message, short rpcID, RemoteChannal type = RemoteChannal.TCP)
        {
            if (message == null)
            {
                return;
            }

            switch (type)
            {
                case RemoteChannal.TCP:
                    TcpChannel?.SendAsync(rpcID, message);
                    break;
                case RemoteChannal.KCP:
                    break;
                case RemoteChannal.UDP:
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 异步发送消息，封装了Rpc过程
        /// <para>只要你调用了接收函数，即使回调函数为空，RPC过程的消息仍能正确处理。</para>
        /// </summary>
        /// <typeparam name="RpcResult">期待的Rpc结果类型，如果收到返回类型，但是类型不匹配，返回null</typeparam>
        /// <param name="message">发送消息的类型需要序列化 查找表<see cref="AddFormatterLookUpTabal(ILookUpTabal)"/> 中指定ID和序列化函数</param>
        /// <param name="type"></param>
        /// <returns></returns>
        public Task<(RpcResult Result, Exception Excption)>
            RpcSendAsync<RpcResult>(dynamic message, RemoteChannal type = RemoteChannal.TCP)
        {
            short rpcID = GetRpcID();

            Send(message, rpcID, type);

            TaskCompletionSource<(RpcResult Result, Exception Excption)> source
                = new TaskCompletionSource<(RpcResult Result, Exception Excption)>();

            rpcCallbackPool[(short)(rpcID * -1)] = (resp) =>
              {
                  if (resp is RpcResult result)
                  {
                      source.SetResult((result, null));
                  }
                  else
                  {
                      source.SetResult((default(RpcResult), new NullReferenceException()));
                  }

                //todo
                //source 回收
            };

            return source.Task;
        }

        #endregion

        #region BroadCast

        public static void BroadCastAsync(object message, RemoteChannal type = RemoteChannal.TCP, params Remote[] remotes)
        {
            BroadCastAsync(message, remotes as IEnumerable<Remote>, type);
        }

        /// <summary>
        /// 默认用TCP广播
        /// </summary>
        /// <param name="message"></param>
        /// <param name="remotes"></param>
        public static void BroadCastAsync(object message, params Remote[] remotes)
        {
            BroadCastAsync(message, remotes as IEnumerable<Remote>);
        }

        [HubMethod]
        public static void BroadCastAsync<T>(T message, IEnumerable<Remote> remotes, RemoteChannal type = RemoteChannal.TCP)
            where T : class
        {

            var msgBuffer = Serialize(0, message);

            ///这里需要测试
            Task.Run(() =>
            {
                Parallel.ForEach(remotes,
                async (INetRemote item) =>
                {
                    //(Action<INetRemote>)
                    await item?.BroadCastAsync(msgBuffer, type);
                });
            }).ContinueWith((t) =>
            {
                ///这里需要测试
                BufferPool.Push(msgBuffer.Array);
            });
        }

        async Task INetRemote.BroadCastAsync(ArraySegment<byte> args, RemoteChannal type)
        {
            switch (type)
            {
                case RemoteChannal.TCP:
                    await TcpChannel.BroadCastSendAsync(args);
                    break;
                case RemoteChannal.KCP:
                    break;
                case RemoteChannal.UDP:
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Receive

        /// <summary>
        /// 异步接受消息包
        /// <para>1.channel收到消息大包（小包组）</para>
        /// <para>2.channel 回调remote <see cref="IOnReceivePacket.OnReceive"/></para>
        /// <para>3.消息大包和remote一起放入接收消息池<see cref="receivePool"/>（这一环节为了切换执行异步方法后续的线程）</para>
        /// <para>4.（主线程）<see cref="Update(double)"/>时统一从池中取出消息，反序列化。
        ///          每个小包是一个消息，由remote <see cref="INetRemote.ReceiveCallback"/>>处理</para>
        /// <para>5.1 检查RpcID(内置不可见) 如果是Rpc结果，触发异步方法后续。如果rpc已经超时，消息被直接丢弃</para>
        /// <para>5.2 不是Rpc结果 则remote调用<see cref="onReceive"/>回调函数(当前方法参数)处理消息</para>
        /// </summary>
        /// <param name="onReceive">处理消息方法，如果远端为RPC调用，那么应该返回一个合适的结果，否则返回null</param>
        [Popular]
        public void ReceiveAsync(OnReceiveMessage onReceive)
        {
            this.onReceive = onReceive;

            TcpChannel?.Receive();
        }

        /// <summary>
        /// channal 回调函数
        /// </summary>
        /// <param name="packet"></param>
        void IOnReceivePacket.OnReceive(IReceivedPacket packet)
        {
            receivePool.Enqueue((packet, this));
        }

        async void INetRemote.ReceiveCallback(int messageID, short rpcID, object msg, RemoteChannal receiveChannal)
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
                Send(response, receiveChannal);
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
                    response = await task?? null;
                }

                if (response is ValueTask<object> vtask)
                {
                    response = await vtask?? null;
                }

                if (response == null)
                {
                    return;
                }
                ///rpc的返回 
                Send(response, (short)(rpcID * -1), receiveChannal);
            }
            else
            {
                ///这个消息是rpc返回（回复的RpcID为-1~-32767）
                ///rpc响应
                if (rpcCallbackPool.ContainsKey(rpcID))
                {
                    rpcCallbackPool[rpcID](msg);
                    rpcCallbackPool.Remove(rpcID);
                }
                else
                {
                    ///可能已经超时
                }

                ///无返回
            }
        }

        #endregion

        #region Static

        static Remote()
        {
            MainThreadScheduler.Add(Update);
        }

        //static Queue<(IReceivedPacket Packet, INetRemote Remote)> receivePool
        //    = new Queue<(IReceivedPacket, INetRemote)>(512);
        //static Queue<(IReceivedPacket Packet, INetRemote Remote)> dealPoop
        //    = new Queue<(IReceivedPacket, INetRemote)>(512);
        static ConcurrentQueue<(IReceivedPacket Packet, INetRemote Remote)> receivePool
            = new ConcurrentQueue<(IReceivedPacket, INetRemote)>();
        static ConcurrentQueue<(IReceivedPacket Packet, INetRemote Remote)> dealPoop
            = new ConcurrentQueue<(IReceivedPacket, INetRemote)>();

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
                        var msg = dFormatter[messageID](body);

                        Remote.ReceiveCallback(messageID, rpcID, msg, Packet.Channal);

                    }

                    Packet?.Push2Pool();
                }
            }
        }

        static readonly Dictionary<int, Deserilizer> dFormatter = new Dictionary<int, Deserilizer>();
        static readonly Dictionary<Type, (int MessageID, Delegate Seiralizer)> sFormatter = new Dictionary<Type, (int MessageID, Delegate Seiralizer)>();
       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="lut"></param>
        /// <exception cref="ArgumentException">反序列化messageID冲突，或者序列化类型冲突</exception>
        public static void AddFormatterLookUpTabal(ILookUpTabal lut)
        {
            foreach (var item in lut.DeserilizerKV)
            {
                dFormatter.Add(item.Key, item.Value);
            }

            ///序列化方法第二个参数必须为 ref byte[]
            Type args2type = typeof(byte[]).MakeByRefType();

            foreach (var item in lut.SeiralizerKV)
            {
                var args = item.Value.Seiralizer.Method.GetParameters();
                if (args.Length != 2)
                {
                    throw new ArgumentException("序列化函数参数数量不匹配");
                }
                if (item.Key != args[0].ParameterType && !item.Key.IsSubclassOf(args[0].ParameterType))
                {
                    throw new ArgumentException($"序列化参数1:类型不匹配,{item.Key}不是{nameof(item.Value)}的第一个参数类型或它的子类。");
                }

                if (args[1].ParameterType != args2type)
                {
                    throw new ArgumentException($"序列化函数参数2:不是 byte[]");
                }
                if (item.Value.Seiralizer.Method.ReturnType != typeof(ushort))
                {
                    throw new ArgumentException($"序列化函数返回类型不是 ushort");
                }
                sFormatter.Add(item.Key, item.Value);
            }
        }

        #endregion

        #region Message

        /// <summary>
        /// 描述消息包长度字节所占的字节数
        /// <para>长度类型ushort，所以一个包理论最大长度不能超过65535字节，框架要求一个包不能大于8192 - 8 个 字节</para>
        /// <para>建议单个包大小10到1024字节</para>
        /// 
        /// 按照千兆网卡计算，一个玩家每秒10~30包，大约10~30KB，大约能负载3000玩家。
        /// </summary>
        public const int MessageLengthByteCount = sizeof(ushort);

        /// <summary>
        /// 消息包类型ID 字节长度
        /// </summary>
        public const int MessageIDByteCount = sizeof(int);

        /// <summary>
        /// 消息包类型ID 字节长度
        /// </summary>
        public const int RpcIDByteCount = sizeof(ushort);

        /// <summary>
        /// 报头总长度
        /// </summary>
        public const int TotalHeaderByteCount =
            MessageLengthByteCount + MessageIDByteCount + RpcIDByteCount;

        #endregion

        #region ChannalCallback

        internal static ArraySegment<byte> Serialize<T>(short rpcID, T message)
        {
            ///序列化消息
            
            var (MessageID, Seiralizer) = sFormatter[message.GetType()];

            ///序列化用buffer
            var buffer65536 = BufferPool.Pop65536();

            Seiralizer<T> seiralizer = Seiralizer as Seiralizer<T>;

            ushort length = seiralizer(message as dynamic,ref buffer65536);

            if (length > 8192 - TotalHeaderByteCount)
            {
                BufferPool.Push65536(buffer65536);
                ///消息过长
                throw new ArgumentOutOfRangeException($"消息长度大于{8192 - TotalHeaderByteCount}," +
                    $"请拆分发送。");
            }

            ///待发送buffer
            var messagebuffer = BufferPool.Pop(length + TotalHeaderByteCount);
            
            ///封装报头
            MakePacket(length, MessageID, rpcID, messagebuffer);
            ///第一次消息值拷贝
            Buffer.BlockCopy(buffer65536, 0, messagebuffer, TotalHeaderByteCount, length);
            ///返还序列化用buffer
            BufferPool.Push65536(buffer65536);

            return new ArraySegment<byte>(messagebuffer, 0, length + TotalHeaderByteCount);
        }

        /// <summary>
        /// 封包
        /// </summary>
        /// <param name="length"></param>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="sbuffer"></param>
        internal static void MakePacket(ushort length, int messageID, short rpcID, byte[] sbuffer)
        {
            int offset = 0;

            sbuffer[offset] = unchecked((byte)(length >> 8));
            sbuffer[offset + 1] = unchecked((byte)(length));
            //BitConverter.GetBytes(length).CopyTo(sbuffer, 0);
            offset += MessageLengthByteCount;


            sbuffer[offset]     = unchecked((byte)(messageID >> 24));
            sbuffer[offset + 1] = unchecked((byte)(messageID >> 16));
            sbuffer[offset + 2] = unchecked((byte)(messageID >> 8));
            sbuffer[offset + 3] = unchecked((byte)(messageID));
            offset += MessageIDByteCount;


            sbuffer[offset] = unchecked((byte)(rpcID >> 8));
            sbuffer[offset + 1] = unchecked((byte)(rpcID));

            offset += RpcIDByteCount;
        }

        /// <summary>
        /// 解析报头 (长度至少要大于8（8个字节也就是一个报头长度）)
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">数据长度小于报头长度</exception>
        internal static (ushort Size,int MessageID,short RpcID) 
            ParsePacketHeader(byte[] buffer,int offset)
        {
            if (buffer.Length - offset >= TotalHeaderByteCount)
            {
                ushort size = (ushort)(buffer[offset] << 8 | buffer[offset + 1]);

                int messageID = (int)(buffer[offset + MessageLengthByteCount    ] << 24
                                    | buffer[offset + MessageLengthByteCount + 1] << 16
                                    | buffer[offset + MessageLengthByteCount + 2] << 8
                                    | buffer[offset + MessageLengthByteCount + 3]);

                short rpcID = (short)(buffer[offset + MessageLengthByteCount + MessageIDByteCount    ] << 8
                                    | buffer[offset + MessageLengthByteCount + MessageIDByteCount + 1]);

                return (size, messageID, rpcID);
            }
            else
            {
                throw new ArgumentOutOfRangeException("数据长度小于报头长度");
            }
        }

        #endregion
    }

    

    internal interface IReceivedPacket : IPoolElement
    {
        Queue<(int messageID, short rpcID, ArraySegment<byte> body)> MessagePacket { get; }
        RemoteChannal Channal { get; }
    }


}
